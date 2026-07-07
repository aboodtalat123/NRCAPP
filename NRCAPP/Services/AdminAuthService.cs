using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NRCAPP.Data;
using System.Security.Claims;

namespace NRCAPP.Services;

public sealed class AdminAuthService(ReliefDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<AdminAuthService> logger)
{
    public async Task<bool> LoginAsync(string username, string password)
    {
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var admin = await db.Admins
            .SingleOrDefaultAsync(x => x.Username == username && x.IsActive);

        if (admin is null || !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            return false;
        }

        admin.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Name, admin.FullName),
                new(ClaimTypes.Role, admin.Role),
                new(ReliefAuthService.ActorTypeClaim, ReliefAuthService.AdminActorType),
                new("admin_username", admin.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        logger.LogInformation("Admin logged in: {Username}", username);
        return true;
    }

    public bool IsLoggedIn()
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true &&
            user.FindFirst(ReliefAuthService.ActorTypeClaim)?.Value == ReliefAuthService.AdminActorType;
    }

    public async Task LogoutAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        logger.LogInformation("Admin logged out");
    }

    public async Task<(bool Success, string Message)> ApproveOrganizationAsync(int orgId)
    {
        var org = await db.Organizations.FindAsync(orgId);
        if (org is null)
        {
            return (false, "المؤسسة غير موجودة.");
        }

        if (org.IsVerified)
        {
            return (false, "المؤسسة معتمدة مسبقاً.");
        }

        org.IsVerified = true;
        org.RejectionReason = null;
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = ReliefAuthService.AdminActorType,
            Action = "اعتماد مؤسسة",
            EntityName = nameof(Organization),
            EntityId = org.Id,
            Details = $"تم اعتماد المؤسسة: {org.NgoName} ({org.LicenseId})"
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Organization approved: {NgoName} ({LicenseId})", org.NgoName, org.LicenseId);
        return (true, $"تم اعتماد المؤسسة {org.NgoName}.");
    }

    public async Task<(bool Success, string Message)> RejectOrganizationAsync(int orgId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "الرجاء إدخال سبب الرفض.");
        }

        var org = await db.Organizations.FindAsync(orgId);
        if (org is null)
        {
            return (false, "المؤسسة غير موجودة.");
        }

        if (org.IsVerified)
        {
            return (false, "المؤسسة معتمدة مسبقاً ولا يمكن رفضها.");
        }

        org.RejectionReason = reason.Trim();
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = ReliefAuthService.AdminActorType,
            Action = "رفض مؤسسة",
            EntityName = nameof(Organization),
            EntityId = org.Id,
            Details = $"تم رفض المؤسسة: {org.NgoName} ({org.LicenseId})، السبب: {reason}"
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Organization rejected: {NgoName} ({LicenseId}), reason: {Reason}", org.NgoName, org.LicenseId, reason);
        return (true, $"تم رفض المؤسسة {org.NgoName}.");
    }

    public async Task<IReadOnlyList<PendingOrganizationItem>> GetPendingOrganizationsAsync()
    {
        return await db.Organizations
            .AsNoTracking()
            .Where(x => !x.IsVerified)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PendingOrganizationItem(
                x.Id, x.LicenseId, x.NgoName, x.AuthorizedPerson, x.CreatedAt, x.RejectionReason))
            .ToListAsync();
    }
}

public sealed record PendingOrganizationItem(
    int Id,
    string LicenseId,
    string NgoName,
    string AuthorizedPerson,
    DateTimeOffset CreatedAt,
    string? RejectionReason);
