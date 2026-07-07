using System.ComponentModel.DataAnnotations;

namespace NRCAPP.Data;

public sealed class Beneficiary
{
    public int Id { get; set; }

    [MaxLength(32)]
    public required string NationalId { get; set; }

    [MaxLength(160)]
    public required string FullName { get; set; }

    public int FamilyMembersCount { get; set; }

    [MaxLength(80)]
    public required string CurrentSector { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

    [MaxLength(32)]
    public required string PhoneNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DistributionRegistration> DistributionRegistrations { get; set; } = [];
}

public sealed class Organization
{
    public int Id { get; set; }

    [MaxLength(64)]
    public required string LicenseId { get; set; }

    [MaxLength(180)]
    public required string NgoName { get; set; }

    [MaxLength(140)]
    public required string AuthorizedPerson { get; set; }

    [MaxLength(128)]
    public required string SecurePasscodeHash { get; set; }

    public AccessLevel AccessLevel { get; set; } = AccessLevel.StandardUser;

    public bool IsVerified { get; set; } = false;

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DistributionPlan> DistributionPlans { get; set; } = [];
}

public sealed class Admin
{
    public int Id { get; set; }

    [MaxLength(64)]
    public required string Username { get; set; }

    [MaxLength(128)]
    public required string PasswordHash { get; set; }

    [MaxLength(160)]
    public required string FullName { get; set; }

    [MaxLength(40)]
    public string Role { get; set; } = "Admin";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class DistributionPlan
{
    public int Id { get; set; }

    public AidKind AidType { get; set; }

    public DateTimeOffset ScheduledDate { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    [MaxLength(80)]
    public required string TargetSector { get; set; }

    public int Quantity { get; set; }

    public int MaxBeneficiaryCapacity { get; set; }

    public int OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public DistributionPlanStatus Status { get; set; } = DistributionPlanStatus.Draft;

    [MaxLength(500)]
    public string? ConflictMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DistributionRegistration> Registrations { get; set; } = [];
}

public sealed class DistributionRegistration
{
    public int Id { get; set; }

    public int BeneficiaryId { get; set; }

    public Beneficiary? Beneficiary { get; set; }

    public int DistributionPlanId { get; set; }

    public DistributionPlan? DistributionPlan { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Requested;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AttendanceConfirmedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

public sealed class SyncQueueItem
{
    public int Id { get; set; }

    [MaxLength(80)]
    public required string LocalDeviceActionId { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

public sealed class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [MaxLength(40)]
    public required string ActorType { get; set; }

    [MaxLength(60)]
    public required string Action { get; set; }

    [MaxLength(80)]
    public required string EntityName { get; set; }

    public int? EntityId { get; set; }

    [MaxLength(1000)]
    public string? Details { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Volunteer
{
    public int Id { get; set; }

    [MaxLength(160)]
    public required string FullName { get; set; }

    [MaxLength(32)]
    public required string PhoneNumber { get; set; }

    [MaxLength(80)]
    public required string Sector { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? DistributionPlanId { get; set; }

    public DistributionPlan? DistributionPlan { get; set; }
}

public sealed class SystemSetting
{
    public int Id { get; set; }

    [MaxLength(80)]
    public required string Key { get; set; }

    [MaxLength(500)]
    public required string Value { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
