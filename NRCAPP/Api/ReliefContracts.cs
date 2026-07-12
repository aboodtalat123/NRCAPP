using NRCAPP.Data;

namespace NRCAPP.Api;

public sealed record OrganizationLoginRequest(string LicenseId, string Passcode);

public sealed record OrganizationRegistrationRequest(
    string LicenseId,
    string NgoName,
    string AuthorizedPerson,
    string Passcode);

public sealed record IndividualLoginRequest(string NationalId);

public sealed record CitizenRegistrationRequest(
    string NationalId,
    string FullName,
    int FamilyMembersCount,
    string CurrentSector,
    string PhoneNumber);

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AuthResponse(
    bool IsAuthenticated,
    string ActorType,
    int? ActorId,
    string DisplayName,
    string AccessLevel,
    string Message);

public sealed record DistributionPlanRequest(
    AidKind AidType,
    DateTimeOffset ScheduledDate,
    decimal Latitude,
    decimal Longitude,
    string TargetSector,
    int Quantity,
    int OrganizationId,
    int MaxBeneficiaryCapacity = 250);

public sealed record DistributionPlanResponse(
    bool Accepted,
    int? PlanId,
    DistributionPlanStatus Status,
    string Message);

public sealed record SyncPacketRequest(
    string LocalDeviceActionId,
    string PayloadJson,
    DateTimeOffset Timestamp);

public sealed record SyncPacketResponse(
    string LocalDeviceActionId,
    SyncStatus Status,
    string Message);

public sealed record DashboardSummaryResponse(
    int ActiveOrganizations,
    int AuthorizedPlans,
    int WarningPlans,
    int PendingSyncItems,
    IReadOnlyList<DistributionPlanMapItem> Plans);

public sealed record DistributionPlanMapItem(
    int Id,
    AidKind AidType,
    string Sector,
    decimal Latitude,
    decimal Longitude,
    int Quantity,
    DistributionPlanStatus Status,
    string OrganizationName,
    DateTimeOffset ScheduledDate);

public sealed record CitizenProfileResponse(
    int BeneficiaryId,
    string NationalId,
    string FullName,
    string CurrentSector,
    IReadOnlyList<ActiveAgencyItem> ActiveAgencies,
    IReadOnlyList<CitizenRegistrationItem> RegisteredAid,
    IReadOnlyList<LocalDistributionItem> LocalSchedule);

public sealed record ActiveAgencyItem(
    int OrganizationId,
    string OrganizationName,
    int ActivePlans,
    string NextAidType);

public sealed record CitizenRegistrationItem(
    int RegistrationId,
    int DistributionPlanId,
    string OrganizationName,
    AidKind AidType,
    DateTimeOffset ScheduledDate,
    RegistrationStatus Status);

public sealed record LocalDistributionItem(
    int DistributionPlanId,
    string OrganizationName,
    AidKind AidType,
    DateTimeOffset ScheduledDate,
    string Sector,
    int Capacity,
    int ConfirmedAttendance);

public sealed record CitizenEnrollmentRequest(
    int BeneficiaryId,
    int DistributionPlanId);

public sealed record CitizenAttendanceRequest(
    int RegistrationId);

public sealed record OrgApprovalRequest(int OrganizationId, bool Approve, string? RejectionReason);

public sealed record AuditLogItem(
    int Id,
    string ActorType,
    string Action,
    string EntityName,
    int? EntityId,
    string? Details,
    DateTimeOffset Timestamp);

public sealed record VolunteerRequest(
    string FullName,
    string PhoneNumber,
    string Sector);

public sealed record VolunteerItem(
    int Id,
    string FullName,
    string PhoneNumber,
    string Sector,
    bool IsActive,
    DateTimeOffset JoinedAt,
    int? DistributionPlanId);

public sealed record SystemSettingItem(
    int Id,
    string Key,
    string Value,
    string? Description);

public sealed record UpdateSettingRequest(string Key, string Value);

public sealed record AnalyticsResponse(
    int ActiveOrganizations,
    int TotalBeneficiaries,
    int PlansByStatusDraft,
    int PlansByStatusAuthorized,
    int PlansByStatusWarning,
    int PlansByStatusCompleted,
    int PlansByStatusCancelled,
    int TotalDelivered,
    int TotalApproved,
    IReadOnlyList<SectorStatItem> SectorStats);

public sealed record SectorStatItem(
    string Sector,
    int BeneficiaryCount,
    int ActivePlanCount,
    double CoverageRatio);

public sealed record GapItem(
    string Sector,
    int RegisteredBeneficiaries,
    int ActivePlansInDays7,
    int TotalCapacity,
    bool IsGap);

public sealed record TodayDistributionItem(
    string Sector,
    string OrganizationName,
    AidKind AidType,
    DateTimeOffset ScheduledDate,
    int Capacity,
    int ConfirmedAttendance,
    int AvailableSpots);
