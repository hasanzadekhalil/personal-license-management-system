namespace LicenseSystem.Core.Enums;

public enum LicenseType
{
    Monthly = 0,   // Fixed 30 days from effective activation
    Annual = 1,    // Fixed 365 days / 12 months from effective activation
    Perpetual = 2
}
