using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using NRCAPP.Api;
using NRCAPP.Components;
using NRCAPP.Data;
using NRCAPP.Services;
using System.Text.Json.Serialization;

namespace NRCAPP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            MapReliefApi(app);

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            await app.RunAsync();
        }

        private static void MapReliefApi(WebApplication app)
        {
            var api = app.MapGroup("/api").WithTags("GRCH");

            api.MapPost("/auth/organization", async (
                OrganizationLoginRequest request,
                ReliefAuthService authService) =>
            {
                var result = await authService.LoginOrganizationAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.Unauthorized();
            });

            api.MapPost("/auth/organization/register", async (
                OrganizationRegistrationRequest request,
                ReliefAuthService authService) =>
            {
                var result = await authService.RegisterOrganizationAsync(request);
                return result.IsAuthenticated ? Results.Created($"/org/dashboard?orgId={result.ActorId}", result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/individual", async (
                IndividualLoginRequest request,
                ReliefAuthService authService) =>
            {
                var result = await authService.LoginIndividualAsync(request);
                return result.IsAuthenticated ? Results.Ok(result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/individual/register", async (
                CitizenRegistrationRequest request,
                ReliefAuthService authService) =>
            {
                var result = await authService.RegisterCitizenAsync(request);
                return result.IsAuthenticated ? Results.Created($"/citizen/profile?nationalId={request.NationalId}", result) : Results.BadRequest(result);
            });

            api.MapPost("/auth/admin", async (
                AdminLoginRequest request,
                AdminAuthService adminAuth) =>
            {
                var ok = await adminAuth.LoginAsync(request.Username, request.Password);
                var admin = adminAuth.Current;
                var result = ok
                    ? new AuthResponse(true, "Admin", admin?.Id, admin?.FullName ?? "مسؤول النظام", admin?.Role ?? "Admin", "تم دخول مسؤول النظام.")
                    : new AuthResponse(false, "Admin", null, "", "", "اسم المستخدم أو كلمة المرور غير صحيحة.");
                return result.IsAuthenticated ? Results.Ok(result) : Results.Unauthorized();
            });

            api.MapPost("/auth/admin/logout", async (AdminAuthService adminAuth) =>
            {
                await adminAuth.LogoutAsync();
                return Results.Ok(new { message = "تم تسجيل الخروج." });
            });

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

            api.MapGet("/distribution-plans", async (ReliefDbContext db) =>
            {
                var plans = await db.DistributionPlans
                    .AsNoTracking()
                    .Include(x => x.Organization)
                    .OrderBy(x => x.ScheduledDate)
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

                return Results.Ok(plans);
            });

            api.MapPost("/distribution-plans", async (
                DistributionPlanRequest request,
                ConflictDetectionService conflictDetection) =>
            {
                var result = await conflictDetection.SubmitPlanAsync(request);
                return result.Accepted ? Results.Created($"/api/distribution-plans/{result.PlanId}", result) : Results.Conflict(result);
            });

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
    }
}
