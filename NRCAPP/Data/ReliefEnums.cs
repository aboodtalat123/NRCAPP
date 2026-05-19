namespace NRCAPP.Data;

public enum VerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2
}

public enum AccessLevel
{
    StandardUser = 0,
    Admin = 1
}

public enum AidKind
{
    FoodBasket = 0,
    SafeDrinkingWater = 1,
    MedicalSupplies = 2,
    ShelterKits = 3
}

public enum DistributionPlanStatus
{
    Draft = 0,
    Authorized = 1,
    Warning = 2,
    Completed = 3,
    Cancelled = 4
}

public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    ConflictError = 2
}

public enum RegistrationStatus
{
    Requested = 0,
    Approved = 1,
    AttendanceConfirmed = 2,
    Delivered = 3,
    Rejected = 4
}
