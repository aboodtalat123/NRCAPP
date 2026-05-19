using Microsoft.EntityFrameworkCore;

namespace NRCAPP.Data;

public sealed class ReliefDbContext(DbContextOptions<ReliefDbContext> options) : DbContext(options)
{
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<DistributionPlan> DistributionPlans => Set<DistributionPlan>();
    public DbSet<DistributionRegistration> DistributionRegistrations => Set<DistributionRegistration>();
    public DbSet<SyncQueueItem> SyncQueueItems => Set<SyncQueueItem>();

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
    }
}
