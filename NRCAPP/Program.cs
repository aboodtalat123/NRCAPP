using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using NRCAPP.Api;
using NRCAPP.Components;
using NRCAPP.Data;
using NRCAPP.Services;
using System.Security.Claims;
using System.Text.Json.Serialization;
using FluentValidation;
using Serilog;

namespace NRCAPP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();
            builder.Host.UseSerilog();

            builder.Services.AddValidatorsFromAssemblyContaining<OrganizationRegistrationValidator>();

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/admin/login";
                    options.Cookie.Name = "NRCAPP.Auth";
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                });
            builder.Services.AddAuthorization();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            var sqlConnection = builder.Configuration.GetConnectionString("ReliefDb");
            var sqliteConnection = builder.Configuration.GetConnectionString("ReliefSqlite")
                ?? "Data Source=App_Data/relief.db";
            builder.Services.AddDbContext<ReliefDbContext>(options =>
            {
                if (string.IsNullOrWhiteSpace(sqlConnection))
                {
                    var sqliteBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqliteConnection);
                    if (!string.IsNullOrWhiteSpace(sqliteBuilder.DataSource))
                    {
                        var directory = Path.GetDirectoryName(Path.GetFullPath(sqliteBuilder.DataSource));
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }

                    options.UseSqlite(sqliteConnection);
                }
                else
                {
                    options.UseSqlServer(sqlConnection);
                }
            });

            builder.Services.AddScoped<ReliefAuthService>();
            builder.Services.AddScoped<AdminAuthService>();
            builder.Services.AddScoped<ConflictDetectionService>();
            builder.Services.AddScoped<SyncQueueService>();
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            app.UseForwardedHeaders();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ReliefDbContext>();
                await ReliefDataSeeder.SeedAsync(db);
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            MapFormAuthEndpoints(app);

            MapReliefApi(app);

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            await app.RunAsync();
        }

        private static void MapReliefApi(WebApplication app)
        {
            var api = app.MapGroup("/api").WithTags("GRCH");

            // === Auth ===
            api.MapPost("/auth/organization/register", async (
                OrganizationRegistrationRequest request,
                ReliefAuthService authService,
                IValidator<OrganizationRegistrationRequest> validator) =>
            {
                var val = await validator.ValidateAsync(request);
                if (!val.IsValid)
                    return Results.BadRequest(new AuthResponse(false, "Organization", null, "", "", val.Errors[0].ErrorMessage));
                var result = await authService.RegisterOrganizationAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/organization", async (
                OrganizationLoginRequest request,
                ReliefAuthService authService,
                IValidator<OrganizationLoginRequest> validator) =>
            {
                var val = await validator.ValidateAsync(request);
                if (!val.IsValid)
                    return Results.BadRequest(new AuthResponse(false, "Organization", null, "", "", val.Errors[0].ErrorMessage));
                var result = await authService.LoginOrganizationAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/individual/register", async (
                CitizenRegistrationRequest request,
                ReliefAuthService authService,
                IValidator<CitizenRegistrationRequest> validator) =>
            {
                var val = await validator.ValidateAsync(request);
                if (!val.IsValid)
                    return Results.BadRequest(new AuthResponse(false, "Individual", null, "", "", val.Errors[0].ErrorMessage));
                var result = await authService.RegisterCitizenAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/individual", async (
                IndividualLoginRequest request,
                ReliefAuthService authService) =>
            {
                var result = await authService.LoginIndividualAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/admin", async (
                AdminLoginRequest request,
                AdminAuthService adminAuth) =>
            {
                var ok = await adminAuth.LoginAsync(request.Username, request.Password);
                return ok ? Results.Ok(new { message = "تم الدخول." }) : Results.Unauthorized();
            });

            api.MapPost("/auth/admin/logout", async (AdminAuthService adminAuth) =>
            {
                await adminAuth.LogoutAsync();
                return Results.Ok(new { message = "تم تسجيل الخروج." });
            });

            api.MapPost("/auth/logout", async (ReliefAuthService authService) =>
            {
                await authService.SignOutAsync();
                return Results.Ok(new { message = "تم تسجيل الخروج." });
            });

            // === Admin: Organization Approval ===
            api.MapGet("/admin/pending-organizations", async (AdminAuthService adminAuth) =>
            {
                if (!adminAuth.IsLoggedIn()) return Results.Unauthorized();
                var pending = await adminAuth.GetPendingOrganizationsAsync();
                return Results.Ok(pending);
            });

            api.MapPost("/admin/approve-organization", async (
                OrgApprovalRequest request,
                AdminAuthService adminAuth) =>
            {
                if (!adminAuth.IsLoggedIn()) return Results.Unauthorized();

                if (request.Approve)
                {
                    var (success, msg) = await adminAuth.ApproveOrganizationAsync(request.OrganizationId);
                    return success ? Results.Ok(new { message = msg }) : Results.BadRequest(new { message = msg });
                }
                else
                {
                    var (success, msg) = await adminAuth.RejectOrganizationAsync(request.OrganizationId, request.RejectionReason ?? "");
                    return success ? Results.Ok(new { message = msg }) : Results.BadRequest(new { message = msg });
                }
            });

            // === Admin: Audit Log ===
            api.MapGet("/admin/audit-log", async (ReliefDbContext db, int? limit) =>
            {
                var logs = await db.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.Timestamp)
                    .Take(limit ?? 100)
                    .Select(x => new AuditLogItem(x.Id, x.ActorType, x.Action, x.EntityName, x.EntityId, x.Details, x.Timestamp))
                    .ToListAsync();
                return Results.Ok(logs);
            });

            // === Dashboard ===
            api.MapGet("/dashboard/summary", async (ReliefDbContext db) =>
            {
                var plans = await db.DistributionPlans
                    .AsNoTracking()
                    .Include(x => x.Organization)
                    .OrderByDescending(x => x.ScheduledDate)
                    .Take(25)
                    .Select(x => new DistributionPlanMapItem(
                        x.Id,
                        x.AidType,
                        x.TargetSector,
                        x.Latitude,
                        x.Longitude,
                        x.Quantity,
                        x.Status,
                        x.Organization == null ? "غير معروف" : x.Organization.NgoName,
                        x.ScheduledDate))
                    .ToListAsync();

                var response = new DashboardSummaryResponse(
                    await db.Organizations.CountAsync(x => x.IsVerified),
                    await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Authorized),
                    await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Warning),
                    await db.SyncQueueItems.CountAsync(x => x.SyncStatus == SyncStatus.Pending),
                    plans);

                return Results.Ok(response);
            });

            // === Distribution Plans ===
            api.MapGet("/distribution-plans", async (ReliefDbContext db, string? sector, string? status) =>
            {
                var query = db.DistributionPlans
                    .AsNoTracking()
                    .Include(x => x.Organization)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(sector))
                    query = query.Where(x => x.TargetSector == sector);
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DistributionPlanStatus>(status, out var statusFilter))
                    query = query.Where(x => x.Status == statusFilter);

                var plans = await query
                    .OrderByDescending(x => x.ScheduledDate)
                    .Select(x => new DistributionPlanMapItem(
                        x.Id, x.AidType, x.TargetSector, x.Latitude, x.Longitude,
                        x.Quantity, x.Status,
                        x.Organization == null ? "غير معروف" : x.Organization.NgoName,
                        x.ScheduledDate))
                    .ToListAsync();
                return Results.Ok(plans);
            });

            api.MapPost("/distribution-plans", async (
                DistributionPlanRequest request,
                ConflictDetectionService conflictDetection,
                IValidator<DistributionPlanRequest> validator) =>
            {
                var val = await validator.ValidateAsync(request);
                if (!val.IsValid)
                    return Results.BadRequest(new DistributionPlanResponse(false, null, DistributionPlanStatus.Warning, val.Errors[0].ErrorMessage));
                var result = await conflictDetection.SubmitPlanAsync(request);
                return result.Accepted ? Results.Created($"/api/distribution-plans/{result.PlanId}", result) : Results.Conflict(result);
            });

            // === Analytics ===
            api.MapGet("/analytics", async (ReliefDbContext db) =>
            {
                var sectors = new[] { "الرمال", "جباليا", "خان يونس", "دير البلح", "رفح" };

                var activeOrganizations = await db.Organizations.CountAsync(x => x.IsVerified);
                var totalBeneficiaries = await db.Beneficiaries.CountAsync();

                var plansByStatus = new Dictionary<DistributionPlanStatus, int>();
                foreach (var status in Enum.GetValues<DistributionPlanStatus>())
                {
                    plansByStatus[status] = await db.DistributionPlans.CountAsync(x => x.Status == status);
                }

                var totalDelivered = await db.DistributionRegistrations
                    .CountAsync(x => x.Status == RegistrationStatus.Delivered);
                var totalApproved = await db.DistributionRegistrations
                    .CountAsync(x => x.Status >= RegistrationStatus.Approved);

                var sectorStats = new List<SectorStatItem>();
                foreach (var sector in sectors)
                {
                    var beneficiaryCount = await db.Beneficiaries
                        .CountAsync(x => x.CurrentSector == sector);
                    var activePlanCount = await db.DistributionPlans
                        .CountAsync(x => x.TargetSector == sector && x.Status == DistributionPlanStatus.Authorized);
                    var totalCapacity = await db.DistributionPlans
                        .Where(x => x.TargetSector == sector && x.Status == DistributionPlanStatus.Authorized)
                        .SumAsync(x => (long?)x.MaxBeneficiaryCapacity) ?? 0;

                    var coverageRatio = beneficiaryCount > 0
                        ? Math.Min(1.0, (double)totalCapacity / beneficiaryCount)
                        : 1.0;

                    sectorStats.Add(new SectorStatItem(sector, beneficiaryCount, activePlanCount, coverageRatio));
                }

                return Results.Ok(new AnalyticsResponse(
                    activeOrganizations, totalBeneficiaries,
                    plansByStatus.GetValueOrDefault(DistributionPlanStatus.Draft),
                    plansByStatus.GetValueOrDefault(DistributionPlanStatus.Authorized),
                    plansByStatus.GetValueOrDefault(DistributionPlanStatus.Warning),
                    plansByStatus.GetValueOrDefault(DistributionPlanStatus.Completed),
                    plansByStatus.GetValueOrDefault(DistributionPlanStatus.Cancelled),
                    (int)totalDelivered, (int)totalApproved,
                    sectorStats));
            });

            // === Gap Detection ===
            api.MapGet("/gaps", async (ReliefDbContext db) =>
            {
                var sectors = new[] { "الرمال", "جباليا", "خان يونس", "دير البلح", "رفح" };
                var now = DateTimeOffset.UtcNow;
                var sevenDaysAgo = now.AddDays(-7);
                var gaps = new List<GapItem>();

                foreach (var sector in sectors)
                {
                    var registeredBeneficiaries = await db.Beneficiaries
                        .CountAsync(x => x.CurrentSector == sector);
                    var activePlansIn7Days = await db.DistributionPlans
                        .CountAsync(x => x.TargetSector == sector
                            && x.Status == DistributionPlanStatus.Authorized
                            && x.ScheduledDate >= sevenDaysAgo
                            && x.ScheduledDate <= now);
                    var totalCapacity = await db.DistributionPlans
                        .Where(x => x.TargetSector == sector
                            && x.Status == DistributionPlanStatus.Authorized
                            && x.ScheduledDate >= sevenDaysAgo
                            && x.ScheduledDate <= now)
                        .SumAsync(x => (long?)x.MaxBeneficiaryCapacity) ?? 0;

                    var isGap = activePlansIn7Days == 0
                        || (registeredBeneficiaries > 0 && totalCapacity < registeredBeneficiaries * 0.5);

                    gaps.Add(new GapItem(sector, registeredBeneficiaries, activePlansIn7Days, (int)totalCapacity, isGap));
                }

                return Results.Ok(gaps);
            });

            // === Volunteers ===
            api.MapGet("/volunteers", async (ReliefDbContext db) =>
            {
                var list = await db.Volunteers
                    .AsNoTracking()
                    .OrderByDescending(x => x.JoinedAt)
                    .Select(x => new VolunteerItem(x.Id, x.FullName, x.PhoneNumber, x.Sector, x.IsActive, x.JoinedAt, x.DistributionPlanId))
                    .ToListAsync();
                return Results.Ok(list);
            });

            api.MapPost("/volunteers", async (VolunteerRequest request, ReliefDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    return Results.BadRequest(new { message = "يرجى تعبئة اسم المتطوع ورقم الهاتف." });
                }

                var volunteer = new Volunteer
                {
                    FullName = request.FullName.Trim(),
                    PhoneNumber = request.PhoneNumber.Trim(),
                    Sector = request.Sector.Trim()
                };
                db.Volunteers.Add(volunteer);
                await db.SaveChangesAsync();
                return Results.Created($"/api/volunteers/{volunteer.Id}", new { message = "تم إضافة المتطوع." });
            });

            api.MapPost("/volunteers/confirm-delivery", async (
                int registrationId,
                int volunteerId,
                ReliefDbContext db) =>
            {
                var registration = await db.DistributionRegistrations
                    .SingleOrDefaultAsync(x => x.Id == registrationId);
                if (registration is null) return Results.NotFound();

                registration.Status = RegistrationStatus.Delivered;
                registration.DeliveredAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();

                var volunteer = await db.Volunteers.FindAsync(volunteerId);
                if (volunteer is not null)
                {
                    volunteer.DistributionPlanId = registration.DistributionPlanId;
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { message = "تم تأكيد التسليم." });
            });

            // === Settings ===
            api.MapGet("/settings", async (ReliefDbContext db) =>
            {
                var settings = await db.SystemSettings
                    .AsNoTracking()
                    .OrderBy(x => x.Key)
                    .Select(x => new SystemSettingItem(x.Id, x.Key, x.Value, x.Description))
                    .ToListAsync();
                return Results.Ok(settings);
            });

            api.MapPost("/settings", async (UpdateSettingRequest request, ReliefDbContext db) =>
            {
                var setting = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == request.Key);
                if (setting is null)
                {
                    return Results.NotFound(new { message = "الإعداد غير موجود." });
                }

                setting.Value = request.Value.Trim();
                setting.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "تم تحديث الإعداد." });
            });

            // === Sync ===
            api.MapPost("/sync/queue", async (
                SyncPacketRequest request,
                SyncQueueService syncQueue) =>
            {
                var result = await syncQueue.QueuePacketAsync(request);
                return Results.Ok(result);
            });

            api.MapGet("/sync/pending", async (SyncQueueService syncQueue) =>
            {
                var pending = await syncQueue.GetPendingAsync();
                return Results.Ok(pending);
            });

            // === AI: my-context (محمي بالمصادقة — يعطي الذكاء سياق المستخدم الحالي) ===
            api.MapGet("/ai/my-context", async (ReliefDbContext db, HttpContext http) =>
            {
                var user = http.User;
                if (user?.Identity?.IsAuthenticated != true)
                    return Results.Ok(new { actor = "guest" });

                var actorType = user.FindFirst("actor_type")?.Value;

                if (actorType == "Admin")
                {
                    var username = user.FindFirst("admin_username")?.Value ?? "";
                    var fullName = user.FindFirst(ClaimTypes.Name)?.Value ?? "";
                    var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "";
                    var stats = new
                    {
                        organizations = await db.Organizations.CountAsync(x => x.IsVerified),
                        beneficiaries = await db.Beneficiaries.CountAsync(),
                        plansActive = await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Authorized),
                        plansWarning = await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Warning),
                        volunteers = await db.Volunteers.CountAsync(x => x.IsActive),
                    };
                    return Results.Ok(new
                    {
                        actor = "admin",
                        username,
                        fullName,
                        role,
                        stats
                    });
                }

                if (actorType == "Individual")
                {
                    var beneficiaryIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(beneficiaryIdStr, out var beneficiaryId))
                    {
                        var ben = await db.Beneficiaries
                            .AsNoTracking()
                            .Include(x => x.DistributionRegistrations)
                                .ThenInclude(x => x.DistributionPlan)
                            .FirstOrDefaultAsync(x => x.Id == beneficiaryId);
                        if (ben is not null)
                        {
                            var registrations = ben.DistributionRegistrations
                                .OrderByDescending(x => x.RequestedAt)
                                .Select(r => new
                                {
                                    r.Id,
                                    planId = r.DistributionPlanId,
                                    planName = r.DistributionPlan!.AidType.ToString(),
                                    sector = r.DistributionPlan.TargetSector,
                                    r.DistributionPlan.ScheduledDate,
                                    status = r.Status.ToString()
                                }).ToList();
                            return Results.Ok(new
                            {
                                actor = "individual",
                                fullName = ben.FullName,
                                nationalId = ben.NationalId,
                                currentSector = ben.CurrentSector,
                                familyMembers = ben.FamilyMembersCount,
                                registrations
                            });
                        }
                    }
                }

                if (actorType == "Organization")
                {
                    var orgIdStr = user.FindFirst("organization_id")?.Value;
                    if (int.TryParse(orgIdStr, out var orgId))
                    {
                        var org = await db.Organizations
                            .AsNoTracking()
                            .Include(x => x.DistributionPlans)
                            .FirstOrDefaultAsync(x => x.Id == orgId);
                        if (org is not null)
                        {
                            var plans = org.DistributionPlans
                                .OrderByDescending(x => x.ScheduledDate)
                                .Select(p => new
                                {
                                    p.Id, p.AidType, p.TargetSector,
                                    p.ScheduledDate, p.Status, p.Quantity
                                }).ToList();
                            return Results.Ok(new
                            {
                                actor = "organization",
                                name = org.NgoName,
                                licenseId = org.LicenseId,
                                authorizedPerson = org.AuthorizedPerson,
                                isVerified = org.IsVerified,
                                plans
                            });
                        }
                    }
                }

                return Results.Ok(new { actor = "guest" });
            });

            // === AI Knowledge Snapshot (live stats) ===
            api.MapGet("/ai/knowledge-snapshot", async (ReliefDbContext db) =>
            {
                var totalOrgs = await db.Organizations.CountAsync(x => x.IsVerified);
                var totalBeneficiaries = await db.Beneficiaries.CountAsync();
                var totalVolunteers = await db.Volunteers.CountAsync(x => x.IsActive);
                var totalPlans = await db.DistributionPlans.CountAsync();
                var plansAuthorized = await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Authorized);
                var plansWarning = await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Warning);
                var plansCompleted = await db.DistributionPlans.CountAsync(x => x.Status == DistributionPlanStatus.Completed);
                var totalDelivered = await db.DistributionRegistrations.CountAsync(x => x.Status == RegistrationStatus.Delivered);

                var sectors = new[] { "الرمال", "جباليا", "خان يونس", "دير البلح", "رفح" };
                var sectorStats = new List<object>();
                foreach (var sector in sectors)
                {
                    var beneficiaryCount = await db.Beneficiaries.CountAsync(x => x.CurrentSector == sector);
                    var planCount = await db.DistributionPlans
                        .CountAsync(x => x.TargetSector == sector && x.Status == DistributionPlanStatus.Authorized);
                    sectorStats.Add(new { sector, beneficiaries = beneficiaryCount, activePlans = planCount });
                }

                return Results.Ok(new
                {
                    organizations = totalOrgs,
                    beneficiaries = totalBeneficiaries,
                    volunteers = totalVolunteers,
                    plans = new
                    {
                        total = totalPlans,
                        authorized = plansAuthorized,
                        warning = plansWarning,
                        completed = plansCompleted
                    },
                    delivered = totalDelivered,
                    sectors = sectorStats
                });
            });

            // === Citizens ===
            api.MapGet("/citizens/{nationalId}/profile", async (
                string nationalId,
                string? sector,
                ReliefDbContext db) =>
            {
                var beneficiary = await db.Beneficiaries
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.NationalId == nationalId);

                if (beneficiary is null)
                {
                    return Results.NotFound();
                }

                var effectiveSector = string.IsNullOrWhiteSpace(sector)
                    ? beneficiary.CurrentSector
                    : sector;
                var now = DateTimeOffset.UtcNow;
                var nextDay = now.AddHours(24);

                var activeAgencies = await db.DistributionPlans
                    .AsNoTracking()
                    .Include(x => x.Organization)
                    .Where(x => x.TargetSector == effectiveSector)
                    .Where(x => x.Status == DistributionPlanStatus.Authorized)
                    .Where(x => x.ScheduledDate >= now)
                    .GroupBy(x => new { x.OrganizationId, OrganizationName = x.Organization == null ? "غير معروف" : x.Organization.NgoName })
                    .Select(x => new ActiveAgencyItem(
                        x.Key.OrganizationId,
                        x.Key.OrganizationName,
                        x.Count(),
                        x.OrderBy(plan => plan.ScheduledDate).Select(plan => plan.AidType.ToString()).First()))
                    .ToListAsync();

                var registeredAid = await db.DistributionRegistrations
                    .AsNoTracking()
                    .Include(x => x.DistributionPlan)
                    .ThenInclude(x => x!.Organization)
                    .Where(x => x.BeneficiaryId == beneficiary.Id)
                    .OrderBy(x => x.DistributionPlan!.ScheduledDate)
                    .Select(x => new CitizenRegistrationItem(
                        x.Id,
                        x.DistributionPlanId,
                        x.DistributionPlan!.Organization == null ? "غير معروف" : x.DistributionPlan.Organization.NgoName,
                        x.DistributionPlan.AidType,
                        x.DistributionPlan.ScheduledDate,
                        x.Status))
                    .ToListAsync();

                var localSchedule = await db.DistributionPlans
                    .AsNoTracking()
                    .Include(x => x.Organization)
                    .Include(x => x.Registrations)
                    .Where(x => x.TargetSector == effectiveSector)
                    .Where(x => x.Status == DistributionPlanStatus.Authorized)
                    .Where(x => x.ScheduledDate >= now && x.ScheduledDate <= nextDay)
                    .OrderBy(x => x.ScheduledDate)
                    .Select(x => new LocalDistributionItem(
                        x.Id,
                        x.Organization == null ? "غير معروف" : x.Organization.NgoName,
                        x.AidType,
                        x.ScheduledDate,
                        x.TargetSector,
                        x.MaxBeneficiaryCapacity,
                        x.Registrations.Count(reg => reg.Status == RegistrationStatus.AttendanceConfirmed || reg.Status == RegistrationStatus.Delivered)))
                    .ToListAsync();

                return Results.Ok(new CitizenProfileResponse(
                    beneficiary.Id,
                    beneficiary.NationalId,
                    beneficiary.FullName,
                    effectiveSector,
                    activeAgencies,
                    registeredAid,
                    localSchedule));
            });

            api.MapPost("/citizens/enroll", async (
                CitizenEnrollmentRequest request,
                ReliefDbContext db) =>
            {
                var exists = await db.DistributionRegistrations
                    .AnyAsync(x => x.BeneficiaryId == request.BeneficiaryId && x.DistributionPlanId == request.DistributionPlanId);

                if (exists)
                {
                    return Results.Ok(new { message = "المواطن مسجل مسبقاً في هذه الخطة." });
                }

                db.DistributionRegistrations.Add(new DistributionRegistration
                {
                    BeneficiaryId = request.BeneficiaryId,
                    DistributionPlanId = request.DistributionPlanId,
                    Status = RegistrationStatus.Requested
                });

                await db.SaveChangesAsync();
                return Results.Created("/api/citizens/enroll", new { message = "تم إرسال طلب التسجيل للمؤسسة." });
            });

            api.MapPost("/citizens/attendance", async (
                CitizenAttendanceRequest request,
                ReliefDbContext db) =>
            {
                var registration = await db.DistributionRegistrations
                    .SingleOrDefaultAsync(x => x.Id == request.RegistrationId);

                if (registration is null)
                {
                    return Results.NotFound();
                }

                if (registration.Status != RegistrationStatus.Delivered)
                {
                    registration.Status = RegistrationStatus.AttendanceConfirmed;
                    registration.AttendanceConfirmedAt = DateTimeOffset.UtcNow;
                }
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "تم تأكيد الحضور وحجز الدور." });
            });
        }

        private static void MapFormAuthEndpoints(WebApplication app)
        {
            app.MapPost("/auth/org/register", async (
                HttpContext context,
                ReliefAuthService authService) =>
            {
                var form = await context.Request.ReadFormAsync();
                var licenseId = form["licenseId"].FirstOrDefault() ?? "";
                var ngoName = form["ngoName"].FirstOrDefault() ?? "";
                var authorizedPerson = form["authorizedPerson"].FirstOrDefault() ?? "";
                var passcode = form["passcode"].FirstOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(licenseId) || string.IsNullOrWhiteSpace(ngoName)
                    || string.IsNullOrWhiteSpace(authorizedPerson) || string.IsNullOrWhiteSpace(passcode))
                {
                    return Results.Redirect("/org/register?error=empty");
                }
                if (passcode.Trim().Length < 6)
                {
                    return Results.Redirect("/org/register?error=passcode");
                }
                var result = await authService.RegisterOrganizationAsync(new Api.OrganizationRegistrationRequest(
                    licenseId.Trim(), ngoName.Trim(), authorizedPerson.Trim(), passcode.Trim()));
                if (!result.IsAuthenticated)
                {
                    return Results.Redirect($"/org/register?error={Uri.EscapeDataString(result.Message ?? "failed")}");
                }
                // Auto-login after registration
                await authService.LoginOrganizationAsync(new Api.OrganizationLoginRequest(licenseId.Trim(), passcode.Trim()));
                return Results.Redirect($"/org/dashboard?orgId={result.ActorId}");
            });

            app.MapPost("/auth/citizen/register", async (
                HttpContext context,
                ReliefAuthService authService) =>
            {
                var form = await context.Request.ReadFormAsync();
                var nationalId = form["nationalId"].FirstOrDefault() ?? "";
                var fullName = form["fullName"].FirstOrDefault() ?? "";
                var familyStr = form["familyMembers"].FirstOrDefault() ?? "1";
                var sector = form["sector"].FirstOrDefault() ?? "الرمال";
                var phone = form["phone"].FirstOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(nationalId) || string.IsNullOrWhiteSpace(fullName)
                    || string.IsNullOrWhiteSpace(phone))
                {
                    return Results.Redirect("/citizen/register?error=empty");
                }
                int.TryParse(familyStr, out var familyMembers);
                if (familyMembers <= 0) familyMembers = 1;
                var result = await authService.RegisterCitizenAsync(new Api.CitizenRegistrationRequest(
                    nationalId.Trim(), fullName.Trim(), familyMembers, sector, phone.Trim()));
                if (!result.IsAuthenticated)
                {
                    return Results.Redirect($"/citizen/register?error={Uri.EscapeDataString(result.Message ?? "failed")}");
                }
                // Auto-login after registration
                await authService.LoginIndividualAsync(new Api.IndividualLoginRequest(nationalId.Trim()));
                return Results.Redirect($"/citizen/profile?nationalId={Uri.EscapeDataString(nationalId.Trim())}&sector={Uri.EscapeDataString(sector)}");
            });

            app.MapPost("/auth/citizen/form-login", async (
                HttpContext context,
                ReliefAuthService authService) =>
            {
                var form = await context.Request.ReadFormAsync();
                var nationalId = form["nationalId"].FirstOrDefault() ?? "";
                var sector = form["sector"].FirstOrDefault() ?? "الرمال";
                if (string.IsNullOrWhiteSpace(nationalId))
                {
                    return Results.Redirect("/citizen/login?error=failed");
                }
                var result = await authService.LoginIndividualAsync(new Api.IndividualLoginRequest(nationalId));
                return result.IsAuthenticated
                    ? Results.Redirect($"/citizen/profile?nationalId={Uri.EscapeDataString(nationalId)}&sector={Uri.EscapeDataString(sector)}")
                    : Results.Redirect("/citizen/login?error=failed");
            });

            app.MapPost("/auth/admin/form-login", async (
                HttpContext context,
                AdminAuthService adminAuth) =>
            {
                var form = await context.Request.ReadFormAsync();
                var username = form["username"].FirstOrDefault() ?? "";
                var password = form["password"].FirstOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return Results.Redirect("/admin/login?error=failed");
                }
                var ok = await adminAuth.LoginAsync(username, password);
                return ok
                    ? Results.Redirect("/admin/dashboard")
                    : Results.Redirect("/admin/login?error=failed");
            });

            app.MapPost("/auth/org/form-login", async (
                HttpContext context,
                ReliefAuthService authService,
                ILogger<Program> logger) =>
            {
                try
                {
                    var form = await context.Request.ReadFormAsync();
                    var licenseId = form["licenseId"].FirstOrDefault() ?? "";
                    var passcode = form["passcode"].FirstOrDefault() ?? "";
                    if (string.IsNullOrWhiteSpace(licenseId) || string.IsNullOrWhiteSpace(passcode))
                    {
                        return Results.Redirect("/org/login?error=failed");
                    }
                    var result = await authService.LoginOrganizationAsync(new Api.OrganizationLoginRequest(licenseId, passcode));
                    return result.IsAuthenticated
                        ? Results.Redirect($"/org/dashboard?orgId={result.ActorId}")
                        : Results.Redirect("/org/login?error=failed");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Org form-login error");
                    return Results.Redirect("/org/login?error=failed");
                }
            });
        }
    }
}
