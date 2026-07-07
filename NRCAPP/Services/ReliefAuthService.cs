using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NRCAPP.Api;
using NRCAPP.Data;
using System.Security.Claims;

namespace NRCAPP.Services;

public sealed class ReliefAuthService(ReliefDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<ReliefAuthService> logger)
{
    public async Task<AuthResponse> RegisterOrganizationAsync(OrganizationRegistrationRequest request)
    {
        var licenseId = request.LicenseId.Trim();
        var ngoName = request.NgoName.Trim();
        var authorizedPerson = request.AuthorizedPerson.Trim();

        if (string.IsNullOrWhiteSpace(licenseId) ||
            string.IsNullOrWhiteSpace(ngoName) ||
            string.IsNullOrWhiteSpace(authorizedPerson) ||
            string.IsNullOrWhiteSpace(request.Passcode))
        {
            return new AuthResponse(false, OrganizationActorType, null, ngoName, "", "يرجى تعبئة كل بيانات المؤسسة قبل إنشاء الحساب.");
        }

        if (request.Passcode.Trim().Length < 6)
        {
            return new AuthResponse(false, OrganizationActorType, null, ngoName, "", "رمز الدخول يجب أن يكون 6 خانات على الأقل.");
        }

        var exists = await db.Organizations.AnyAsync(x => x.LicenseId == licenseId);
        if (exists)
        {
            return new AuthResponse(false, OrganizationActorType, null, ngoName, "", "رقم الترخيص مسجل مسبقاً. استخدم تسجيل الدخول.");
        }

        var organization = new Organization
        {
            LicenseId = licenseId,
            NgoName = ngoName,
            AuthorizedPerson = authorizedPerson,
            SecurePasscodeHash = PasscodeHasher.Hash(request.Passcode.Trim()),
            IsVerified = true
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = OrganizationActorType,
            Action = "تسجيل مؤسسة جديدة",
            EntityName = nameof(Organization),
            EntityId = organization.Id,
            Details = $"المؤسسة: {ngoName}، الترخيص: {licenseId}"
        });
        await db.SaveChangesAsync();

        logger.LogInformation("New organization registered: {NgoName} ({LicenseId}), awaiting admin approval", ngoName, licenseId);

        return new AuthResponse(true, OrganizationActorType, organization.Id, organization.NgoName, "", "تم إنشاء حساب المؤسسة بنجاح.");
    }

    public async Task<AuthResponse> LoginOrganizationAsync(OrganizationLoginRequest request)
    {
        var licenseId = request.LicenseId.Trim();
        var organization = await db.Organizations
            .SingleOrDefaultAsync(x => x.LicenseId == licenseId);

        if (organization is null)
        {
            return new AuthResponse(false, OrganizationActorType, null, "", "", "رقم الترخيص أو رمز الدخول غير صحيح.");
        }

        if (!PasscodeHasher.Verify(request.Passcode, organization.SecurePasscodeHash))
        {
            return new AuthResponse(false, OrganizationActorType, null, "", "", "رقم الترخيص أو رمز الدخول غير صحيح.");
        }

        if (!organization.IsVerified)
        {
            var msg = string.IsNullOrWhiteSpace(organization.RejectionReason)
                ? "حسابكم قيد المراجعة من قبل الإدارة."
                : $"لم تتم الموافقة على حسابكم. السبب: {organization.RejectionReason}";
            return new AuthResponse(false, OrganizationActorType, null, organization.NgoName, "", msg);
        }

        await SignInOrganizationAsync(organization);

        logger.LogInformation("Organization logged in: {NgoName} ({LicenseId})", organization.NgoName, licenseId);

        return new AuthResponse(
            true,
            OrganizationActorType,
            organization.Id,
            organization.NgoName,
            organization.AccessLevel.ToString(),
            "تم توثيق المؤسسة.");
    }

    public async Task<AuthResponse> LoginIndividualAsync(IndividualLoginRequest request)
    {
        var nationalId = request.NationalId.Trim();
        var beneficiary = await db.Beneficiaries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NationalId == nationalId);

        if (beneficiary is null)
        {
            return new AuthResponse(false, IndividualActorType, null, "", "", "رقم الهوية غير موجود.");
        }

        return new AuthResponse(
            beneficiary.VerificationStatus == VerificationStatus.Verified,
            IndividualActorType,
            beneficiary.Id,
            beneficiary.FullName,
            beneficiary.VerificationStatus.ToString(),
            beneficiary.VerificationStatus == VerificationStatus.Verified
                ? "تم التحقق من الهوية."
                : "الهوية موجودة لكنها تحتاج تحققاً ميدانياً.");
    }

    public async Task<AuthResponse> RegisterCitizenAsync(CitizenRegistrationRequest request)
    {
        var nationalId = request.NationalId.Trim();
        var fullName = request.FullName.Trim();
        var currentSector = request.CurrentSector.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        if (string.IsNullOrWhiteSpace(nationalId) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(currentSector) ||
            string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new AuthResponse(false, IndividualActorType, null, fullName, "", "يرجى تعبئة بيانات المواطن كاملة قبل إنشاء الحساب.");
        }

        if (request.FamilyMembersCount <= 0)
        {
            return new AuthResponse(false, IndividualActorType, null, fullName, "", "عدد أفراد الأسرة يجب أن يكون أكبر من صفر.");
        }

        var exists = await db.Beneficiaries.AnyAsync(x => x.NationalId == nationalId);
        if (exists)
        {
            return new AuthResponse(false, IndividualActorType, null, fullName, "", "رقم الهوية مسجل مسبقاً. استخدم دخول المواطن.");
        }

        var beneficiary = new Beneficiary
        {
            NationalId = nationalId,
            FullName = fullName,
            FamilyMembersCount = request.FamilyMembersCount,
            CurrentSector = currentSector,
            PhoneNumber = phoneNumber,
            VerificationStatus = VerificationStatus.Verified
        };

        db.Beneficiaries.Add(beneficiary);
        await db.SaveChangesAsync();

        logger.LogInformation("New citizen registered: {FullName} ({NationalId})", fullName, nationalId);

        return new AuthResponse(true, IndividualActorType, beneficiary.Id, beneficiary.FullName, beneficiary.VerificationStatus.ToString(), "تم إنشاء حساب المواطن.");
    }

    public async Task SignOutAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private async Task SignInOrganizationAsync(Organization organization)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, organization.Id.ToString()),
            new(ClaimTypes.Name, organization.NgoName),
            new(ClaimTypes.Role, "OrgManager"),
            new(ActorTypeClaim, OrganizationActorType),
            new(OrganizationIdClaim, organization.Id.ToString())
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

    public const string ActorTypeClaim = "actor_type";
    public const string OrganizationIdClaim = "organization_id";
    public const string OrganizationActorType = "Organization";
    public const string IndividualActorType = "Individual";
    public const string AdminActorType = "Admin";
}
