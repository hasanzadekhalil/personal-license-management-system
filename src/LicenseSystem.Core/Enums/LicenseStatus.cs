namespace LicenseSystem.Core.Enums;

public enum LicenseStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2,
    LockedHardwareMismatch = 3,
    LockedClockTampered = 4,
    LockedGraceExpired = 5,
    PendingReactivation = 6
}
