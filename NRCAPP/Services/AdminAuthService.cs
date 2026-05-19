using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NRCAPP.Data;
using System.Security.Claims;

namespace NRCAPP.Services;

public sealed class AdminAuthService(ReliefDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private Admin? currentAdmin;

    public Admin? Current => currentAdmin;

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
        currentAdmin = admin;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Name, admin.FullName),
                new(ClaimTypes.Role, admin.Role),
                new("actor_type", "Admin"),
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

        return true;
    }

    public bool IsLoggedIn()
    {
        if (currentAdmin is not null)
        {
            return true;
        }

        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true &&
            user.FindFirst("actor_type")?.Value == "Admin";
    }

    public async Task LogoutAsync()
    {
        currentAdmin = null;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
