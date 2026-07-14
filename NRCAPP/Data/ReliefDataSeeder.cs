using Microsoft.EntityFrameworkCore;

namespace NRCAPP.Data;

public static class ReliefDataSeeder
{
    public static async Task SeedAsync(ReliefDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureAdminSchemaAsync(db);
        await EnsureDeliverySchemaAsync(db);
        await EnsureOrgRejectionReasonSchemaAsync(db);
        await EnsureAuditLogSchemaAsync(db);
        await EnsureVolunteerSchemaAsync(db);
        await EnsureSystemSettingSchemaAsync(db);
        await SeedDefaultAdminAsync(db);
        await SeedDefaultSettingsAsync(db);
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

    private static async Task EnsureOrgRejectionReasonSchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            var colExists = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Organizations') WHERE name = 'RejectionReason'")
                .SingleAsync();

            if (colExists == 0)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "Organizations" ADD COLUMN "RejectionReason" TEXT NULL;
                    """);
            }
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('Organizations', 'RejectionReason') IS NULL
                BEGIN
                    ALTER TABLE [Organizations] ADD [RejectionReason] nvarchar(500) NULL;
                END
                """);
        }
    }

    private static async Task EnsureAuditLogSchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "AuditLogs" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NULL,
                    "ActorType" TEXT NOT NULL,
                    "Action" TEXT NOT NULL,
                    "EntityName" TEXT NOT NULL,
                    "EntityId" INTEGER NULL,
                    "Details" TEXT NULL,
                    "Timestamp" INTEGER NOT NULL
                );
                """);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AuditLogs] (
                        [Id] int NOT NULL IDENTITY,
                        [UserId] int NULL,
                        [ActorType] nvarchar(40) NOT NULL,
                        [Action] nvarchar(60) NOT NULL,
                        [EntityName] nvarchar(80) NOT NULL,
                        [EntityId] int NULL,
                        [Details] nvarchar(1000) NULL,
                        [Timestamp] datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                    );
                END
                """);
        }
    }

    private static async Task EnsureVolunteerSchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Volunteers" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Volunteers" PRIMARY KEY AUTOINCREMENT,
                    "FullName" TEXT NOT NULL,
                    "PhoneNumber" TEXT NOT NULL,
                    "Sector" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "JoinedAt" INTEGER NOT NULL,
                    "DistributionPlanId" INTEGER NULL,
                    CONSTRAINT "FK_Volunteers_DistributionPlans_DistributionPlanId" FOREIGN KEY ("DistributionPlanId") REFERENCES "DistributionPlans" ("Id") ON DELETE SET NULL
                );
                """);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Volunteers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Volunteers] (
                        [Id] int NOT NULL IDENTITY,
                        [FullName] nvarchar(160) NOT NULL,
                        [PhoneNumber] nvarchar(32) NOT NULL,
                        [Sector] nvarchar(80) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [JoinedAt] datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
                        [DistributionPlanId] int NULL,
                        CONSTRAINT [PK_Volunteers] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Volunteers_DistributionPlans_DistributionPlanId] FOREIGN KEY ([DistributionPlanId]) REFERENCES [DistributionPlans] ([Id]) ON DELETE SET NULL
                    );
                END
                """);
        }
    }

    private static async Task EnsureSystemSettingSchemaAsync(ReliefDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SystemSettings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SystemSettings" PRIMARY KEY AUTOINCREMENT,
                    "Key" TEXT NOT NULL,
                    "Value" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "UpdatedAt" INTEGER NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SystemSettings_Key" ON "SystemSettings" ("Key");
                """);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[SystemSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [SystemSettings] (
                        [Id] int NOT NULL IDENTITY,
                        [Key] nvarchar(80) NOT NULL,
                        [Value] nvarchar(500) NOT NULL,
                        [Description] nvarchar(200) NULL,
                        [UpdatedAt] datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);
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
        var configuredUsername = Environment.GetEnvironmentVariable("NRCAPP_ADMIN_USERNAME")?.Trim();
        var configuredPassword = Environment.GetEnvironmentVariable("NRCAPP_ADMIN_PASSWORD");
        configuredUsername = string.IsNullOrWhiteSpace(configuredUsername) ? "admin" : configuredUsername;

        var existingAdmin = await db.Admins.FirstOrDefaultAsync(x => x.Username == configuredUsername)
            ?? await db.Admins.FirstOrDefaultAsync();
        if (existingAdmin is not null)
        {
            if (!string.IsNullOrWhiteSpace(configuredPassword)
                && !BCrypt.Net.BCrypt.Verify(configuredPassword, existingAdmin.PasswordHash))
            {
                existingAdmin.Username = configuredUsername;
                existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword);
                await db.SaveChangesAsync();
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NRCAPP_ADMIN_PASSWORD must be configured before the first production start.");
            configuredPassword = "123456";
        }

        db.Admins.Add(new Admin
        {
            Username = configuredUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword),
            FullName = "مسؤول النظام",
            Role = "SuperAdmin",
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDefaultSettingsAsync(ReliefDbContext db)
    {
        if (await db.SystemSettings.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "registration_enabled", Value = "true", Description = "تفعيل/تعطيل التسجيل الذاتي للمؤسسات", UpdatedAt = now },
            new SystemSetting { Key = "conflict_window_hours", Value = "48", Description = "مدة صلاحية نافذة كشف التعارض بالساعات", UpdatedAt = now },
            new SystemSetting { Key = "offline_mode_enabled", Value = "false", Description = "تفعيل وضع عدم الاتصال", UpdatedAt = now },
            new SystemSetting { Key = "critical_gap_threshold", Value = "0.5", Description = "نسبة الفجوة الحرجة (أقل من 50% تغطية)", UpdatedAt = now }
        );

        await db.SaveChangesAsync();
    }
}
