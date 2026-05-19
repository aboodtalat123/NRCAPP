using Microsoft.EntityFrameworkCore;

namespace NRCAPP.Data;

public static class ReliefDataSeeder
{
    public static async Task SeedAsync(ReliefDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureAdminSchemaAsync(db);
        await EnsureDeliverySchemaAsync(db);
        await SeedDefaultAdminAsync(db);
        await RemoveLegacyDemoDataAsync(db);
    }

    private static async Task EnsureDeliverySchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            var hasDeliveredAt = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('DistributionRegistrations') WHERE name = 'DeliveredAt'")
                .SingleAsync();

            if (hasDeliveredAt == 0)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "DistributionRegistrations" ADD COLUMN "DeliveredAt" INTEGER NULL;
                    """);
            }
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('DistributionRegistrations', 'DeliveredAt') IS NULL
                BEGIN
                    ALTER TABLE [DistributionRegistrations] ADD [DeliveredAt] datetimeoffset NULL;
                END
                """);
        }
    }

    private static async Task EnsureAdminSchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Admins" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Admins" PRIMARY KEY AUTOINCREMENT,
                    "Username" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "FullName" TEXT NOT NULL,
                    "Role" TEXT NOT NULL,
                    "CreatedAt" INTEGER NOT NULL,
                    "LastLoginAt" INTEGER NULL,
                    "IsActive" INTEGER NOT NULL
                );
                """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Admins_Username" ON "Admins" ("Username");
                """);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Admins]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Admins] (
                        [Id] int NOT NULL IDENTITY,
                        [Username] nvarchar(64) NOT NULL,
                        [PasswordHash] nvarchar(128) NOT NULL,
                        [FullName] nvarchar(160) NOT NULL,
                        [Role] nvarchar(40) NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
                        [LastLoginAt] datetimeoffset NULL,
                        [IsActive] bit NOT NULL,
                        CONSTRAINT [PK_Admins] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_Admins_Username] ON [Admins] ([Username]);
                END
                """);
        }
    }

    private static async Task SeedDefaultAdminAsync(ReliefDbContext db)
    {
        if (await db.Admins.AnyAsync())
        {
            return;
        }

        db.Admins.Add(new Admin
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("nrcapp2024"),
            FullName = "مسؤول النظام",
            Role = "SuperAdmin",
            IsActive = true
        });

        await db.SaveChangesAsync();
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
