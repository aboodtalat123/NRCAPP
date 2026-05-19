using Microsoft.EntityFrameworkCore;
using NRCAPP.Api;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed class ReliefAuthService(ReliefDbContext db)
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
            return new AuthResponse(false, "Organization", null, ngoName, "", "يرجى تعبئة كل بيانات المؤسسة قبل إنشاء الحساب.");
        }

        if (request.Passcode.Trim().Length < 6)
        {
            return new AuthResponse(false, "Organization", null, ngoName, "", "رمز الدخول يجب أن يكون 6 خانات على الأقل.");
        }

        var exists = await db.Organizations.AnyAsync(x => x.LicenseId == licenseId);
        if (exists)
        {
            return new AuthResponse(false, "Organization", null, ngoName, "", "رقم الترخيص مسجل مسبقاً. استخدم تسجيل الدخول.");
        }

        var organization = new Organization
        {
            LicenseId = licenseId,
            NgoName = ngoName,
            AuthorizedPerson = authorizedPerson,
            SecurePasscodeHash = PasscodeHasher.Hash(request.Passcode.Trim()),
            AccessLevel = AccessLevel.Admin,
            IsVerified = true
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        return new AuthResponse(true, "Organization", organization.Id, organization.NgoName, organization.AccessLevel.ToString(), "تم إنشاء حساب المؤسسة وحفظه في قاعدة البيانات.");
    }

    public async Task<AuthResponse> LoginOrganizationAsync(OrganizationLoginRequest request)
    {
        var licenseId = request.LicenseId.Trim();
        var organization = await db.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.LicenseId == licenseId);

        if (organization is null || !organization.IsVerified)
        {
            return new AuthResponse(false, "Organization", null, "", "", "رقم الترخيص غير موثق.");
        }

        if (!PasscodeHasher.Verify(request.Passcode, organization.SecurePasscodeHash))
        {
            return new AuthResponse(false, "Organization", null, organization.NgoName, "", "رمز الدخول غير صحيح.");
        }

        return new AuthResponse(
            true,
            "Organization",
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
            return new AuthResponse(false, "Individual", null, "", "", "رقم الهوية غير موجود.");
        }

        return new AuthResponse(
            beneficiary.VerificationStatus == VerificationStatus.Verified,
            "Individual",
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
            return new AuthResponse(false, "Individual", null, fullName, "", "يرجى تعبئة بيانات المواطن كاملة قبل إنشاء الحساب.");
        }

        if (request.FamilyMembersCount <= 0)
        {
            return new AuthResponse(false, "Individual", null, fullName, "", "عدد أفراد الأسرة يجب أن يكون أكبر من صفر.");
        }

        var exists = await db.Beneficiaries.AnyAsync(x => x.NationalId == nationalId);
        if (exists)
        {
            return new AuthResponse(false, "Individual", null, fullName, "", "رقم الهوية مسجل مسبقاً. استخدم دخول المواطن.");
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

        return new AuthResponse(true, "Individual", beneficiary.Id, beneficiary.FullName, beneficiary.VerificationStatus.ToString(), "تم إنشاء حساب المواطن وحفظه في قاعدة البيانات.");
    }

    public static AuthResponse LoginAdmin(AdminLoginRequest request)
    {
        var username = request.Username.Trim();
        var password = request.Password.Trim();

        if (username == "admin" && password == "admin")
        {
            return new AuthResponse(true, "Admin", 0, "مسؤول البرنامج", AccessLevel.Admin.ToString(), "تم دخول مسؤول البرنامج.");
        }

        return new AuthResponse(false, "Admin", null, "", "", "اسم المستخدم أو كلمة المرور غير صحيحة.");
    }
}
