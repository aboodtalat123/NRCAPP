using Microsoft.EntityFrameworkCore;
using NRCAPP.Api;
using NRCAPP.Data;

namespace NRCAPP.Services;

public sealed class ReliefAuthService(ReliefDbContext db)
{
    public async Task<AuthResponse> LoginOrganizationAsync(OrganizationLoginRequest request)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.LicenseId == request.LicenseId);

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
        var beneficiary = await db.Beneficiaries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NationalId == request.NationalId);

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
}
