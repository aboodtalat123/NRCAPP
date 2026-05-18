using Microsoft.EntityFrameworkCore;
using NRCAPP.Services;

namespace NRCAPP.Data;

public static class ReliefDataSeeder
{
    public static async Task SeedAsync(ReliefDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Organizations.AnyAsync())
        {
            db.Organizations.AddRange(
                new Organization
                {
                    LicenseId = "UNRWA-GZA-001",
                    NgoName = "UNRWA",
                    AuthorizedPerson = "Maya Al-Karim",
                    SecurePasscodeHash = PasscodeHasher.Hash("123456"),
                    AccessLevel = AccessLevel.Admin
                },
                new Organization
                {
                    LicenseId = "UNICEF-GZA-002",
                    NgoName = "UNICEF",
                    AuthorizedPerson = "Omar Nasser",
                    SecurePasscodeHash = PasscodeHasher.Hash("123456"),
                    AccessLevel = AccessLevel.StandardUser
                },
                new Organization
                {
                    LicenseId = "WFP-GZA-003",
                    NgoName = "WFP",
                    AuthorizedPerson = "Lina Barakat",
                    SecurePasscodeHash = PasscodeHasher.Hash("123456"),
                    AccessLevel = AccessLevel.StandardUser
                });
        }

        if (!await db.Beneficiaries.AnyAsync())
        {
            db.Beneficiaries.AddRange(
                new Beneficiary
                {
                    NationalId = "900112233",
                    FullName = "سلمى خليل",
                    FamilyMembersCount = 6,
                    CurrentSector = "الرمال",
                    VerificationStatus = VerificationStatus.Verified,
                    PhoneNumber = "0599000001"
                },
                new Beneficiary
                {
                    NationalId = "900445566",
                    FullName = "أحمد حسن",
                    FamilyMembersCount = 4,
                    CurrentSector = "جباليا",
                    VerificationStatus = VerificationStatus.Verified,
                    PhoneNumber = "0599000002"
                },
                new Beneficiary
                {
                    NationalId = "900778899",
                    FullName = "نورا المصري",
                    FamilyMembersCount = 5,
                    CurrentSector = "خان يونس",
                    VerificationStatus = VerificationStatus.Pending,
                    PhoneNumber = "0599000003"
                });
        }

        await db.SaveChangesAsync();

        if (!await db.DistributionPlans.AnyAsync())
        {
            var unrwa = await db.Organizations.SingleAsync(x => x.LicenseId == "UNRWA-GZA-001");
            var unicef = await db.Organizations.SingleAsync(x => x.LicenseId == "UNICEF-GZA-002");
            var wfp = await db.Organizations.SingleAsync(x => x.LicenseId == "WFP-GZA-003");

            db.DistributionPlans.AddRange(
                new DistributionPlan
                {
                    AidType = AidKind.FoodBasket,
                    ScheduledDate = DateTimeOffset.UtcNow.AddHours(18),
                    Latitude = 31.501m,
                    Longitude = 34.466m,
                    TargetSector = "الرمال",
                    Quantity = 340,
                    MaxBeneficiaryCapacity = 300,
                    OrganizationId = unrwa.Id,
                    Status = DistributionPlanStatus.Authorized
                },
                new DistributionPlan
                {
                    AidType = AidKind.SafeDrinkingWater,
                    ScheduledDate = DateTimeOffset.UtcNow.AddHours(8),
                    Latitude = 31.527m,
                    Longitude = 34.483m,
                    TargetSector = "جباليا",
                    Quantity = 500,
                    MaxBeneficiaryCapacity = 450,
                    OrganizationId = unicef.Id,
                    Status = DistributionPlanStatus.Authorized
                },
                new DistributionPlan
                {
                    AidType = AidKind.FoodBasket,
                    ScheduledDate = DateTimeOffset.UtcNow.AddHours(30),
                    Latitude = 31.346m,
                    Longitude = 34.303m,
                    TargetSector = "خان يونس",
                    Quantity = 220,
                    MaxBeneficiaryCapacity = 180,
                    OrganizationId = wfp.Id,
                    Status = DistributionPlanStatus.Authorized
                });

            await db.SaveChangesAsync();
        }

        if (!await db.DistributionRegistrations.AnyAsync())
        {
            var salma = await db.Beneficiaries.SingleAsync(x => x.NationalId == "900112233");
            var ramalFood = await db.DistributionPlans
                .Include(x => x.Organization)
                .Where(x => x.TargetSector == "الرمال" && x.AidType == AidKind.FoodBasket)
                .OrderBy(x => x.ScheduledDate)
                .FirstAsync();

            db.DistributionRegistrations.Add(new DistributionRegistration
            {
                BeneficiaryId = salma.Id,
                DistributionPlanId = ramalFood.Id,
                Status = RegistrationStatus.Approved
            });

            await db.SaveChangesAsync();
        }
    }
}
