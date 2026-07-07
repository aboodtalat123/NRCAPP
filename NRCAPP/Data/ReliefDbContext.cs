using Microsoft.EntityFrameworkCore;

namespace NRCAPP.Data;

public sealed class ReliefDbContext(DbContextOptions<ReliefDbContext> options) : DbContext(options)
{
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<DistributionPlan> DistributionPlans => Set<DistributionPlan>();
    public DbSet<DistributionRegistration> DistributionRegistrations => Set<DistributionRegistration>();
    public DbSet<SyncQueueItem> SyncQueueItems => Set<SyncQueueItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.IsSqlServer();

        modelBuilder.Entity<Beneficiary>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.NationalId).IsUnique();
            entity.Property(x => x.NationalId).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CurrentSector).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(32);
            if (!isSqlServer)
            {
                entity.Property(x => x.CreatedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }

            if (isSqlServer)
            {
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });

        modelBuilder.Entity<DistributionRegistration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BeneficiaryId, x.DistributionPlanId }).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            if (!isSqlServer)
            {
                entity.Property(x => x.RequestedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                entity.Property(x => x.AttendanceConfirmedAt)
                    .HasConversion(v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null, v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);
                entity.Property(x => x.DeliveredAt)
                    .HasConversion(v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null, v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);
            }

            if (isSqlServer)
            {
                entity.Property(x => x.RequestedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }

            entity.HasOne(x => x.Beneficiary)
                .WithMany(x => x.DistributionRegistrations)
                .HasForeignKey(x => x.BeneficiaryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.DistributionPlan)
                .WithMany(x => x.Registrations)
                .HasForeignKey(x => x.DistributionPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LicenseId).IsUnique();
            entity.Property(x => x.LicenseId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.NgoName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.AuthorizedPerson).HasMaxLength(140).IsRequired();
            entity.Property(x => x.SecurePasscodeHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.AccessLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            if (!isSqlServer)
            {
                entity.Property(x => x.CreatedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }

            if (isSqlServer)
            {
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(40).IsRequired();
            if (!isSqlServer)
            {
                entity.Property(x => x.CreatedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                entity.Property(x => x.LastLoginAt)
                    .HasConversion(v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null, v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);
            }

            if (isSqlServer)
            {
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });

        modelBuilder.Entity<DistributionPlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetSector, x.AidType, x.ScheduledDate });
            entity.Property(x => x.AidType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.TargetSector).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.Property(x => x.ConflictMessage).HasMaxLength(500);
            if (!isSqlServer)
            {
                entity.Property(x => x.ScheduledDate)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
                entity.Property(x => x.CreatedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }

            if (isSqlServer)
            {
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }

            entity.HasOne(x => x.Organization)
                .WithMany(x => x.DistributionPlans)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SyncQueueItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LocalDeviceActionId).IsUnique();
            entity.Property(x => x.LocalDeviceActionId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.Property(x => x.SyncStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ErrorMessage).HasMaxLength(500);
            if (!isSqlServer)
            {
                entity.Property(x => x.Timestamp)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }

            if (isSqlServer)
            {
                entity.Property(x => x.Timestamp).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(60).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(1000);
            if (!isSqlServer)
            {
                entity.Property(x => x.Timestamp)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
            if (isSqlServer)
            {
                entity.Property(x => x.Timestamp).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });

        modelBuilder.Entity<Volunteer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Sector).HasMaxLength(80).IsRequired();
            if (!isSqlServer)
            {
                entity.Property(x => x.JoinedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
            if (isSqlServer)
            {
                entity.Property(x => x.JoinedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }
            entity.HasOne(x => x.DistributionPlan)
                .WithMany()
                .HasForeignKey(x => x.DistributionPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
            if (!isSqlServer)
            {
                entity.Property(x => x.UpdatedAt)
                    .HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            }
            if (isSqlServer)
            {
                entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            }
        });
    }
}
