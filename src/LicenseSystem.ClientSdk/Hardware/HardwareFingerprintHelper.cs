using System.Security.Cryptography;
using System.Text;

namespace LicenseSystem.ClientSdk.Hardware;

public static class HardwareFingerprintHelper
{
    public static string ComputeCompositeHash(string motherboardId, string cpuId, string primaryDiskId, string salt = "LicenseSystem_Salt_2026")
    {
        var cleanMb = CleanIdentifier(motherboardId, "MB_UNKNOWN");
        var cleanCpu = CleanIdentifier(cpuId, "CPU_UNKNOWN");
        var cleanDisk = CleanIdentifier(primaryDiskId, "DISK_UNKNOWN");

        var canonicalInput = $"{salt}:{cleanMb}:{cleanCpu}:{cleanDisk}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string CleanIdentifier(string? value, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed is "NONE" or "DEFAULT STRING" or "TO BE FILLED BY O.E.M." or "N/A" or "UNKNOWN")
        {
            return defaultValue;
        }

        return trimmed;
    }
}
