using Microsoft.EntityFrameworkCore;

namespace NRCAPP.Data;

public static class ReliefDataSeeder
{
    public static async Task SeedAsync(ReliefDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await RemoveLegacyDemoDataAsync(db);
    }

    private static async Task RemoveLegacyDemoDataAsync(ReliefDbContext db)
    {
        string[] demoLicenseIds = ["UNRWA-GZA-001", "UNICEF-GZA-002", "WFP-GZA-003"];
        string[] demoNationalIds = ["900112233", "900445566", "900778899"];

        var demoOrganizationIds = await db.Organizations
            .Where(x => demoLicenseIds.Contains(x.LicenseId) || x.LicenseId.StartsWith("WEB-DIAG-"))
            .Select(x => x.Id)
            .ToListAsync();

        var demoBeneficiaryIds = await db.Beneficiaries
            .Where(x => demoNationalIds.Contains(x.NationalId))
            .Select(x => x.Id)
            .ToListAsync();

        if (demoOrganizationIds.Count == 0 && demoBeneficiaryIds.Count == 0)
        {
            return;
        }

        var demoPlanIds = await db.DistributionPlans
            .Where(x => demoOrganizationIds.Contains(x.OrganizationId))
            .Select(x => x.Id)
            .ToListAsync();

        db.DistributionRegistrations.RemoveRange(db.DistributionRegistrations
            .Where(x => demoBeneficiaryIds.Contains(x.BeneficiaryId) || demoPlanIds.Contains(x.DistributionPlanId)));

        db.DistributionPlans.RemoveRange(db.DistributionPlans.Where(x => demoPlanIds.Contains(x.Id)));
        db.Beneficiaries.RemoveRange(db.Beneficiaries.Where(x => demoBeneficiaryIds.Contains(x.Id)));
        db.Organizations.RemoveRange(db.Organizations.Where(x => demoOrganizationIds.Contains(x.Id)));

        await db.SaveChangesAsync();
    }
}
