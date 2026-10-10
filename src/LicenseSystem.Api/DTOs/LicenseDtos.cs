namespace LicenseSystem.Api.DTOs;

public class ActivationRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string HardwareHash { get; set; } = string.Empty;
    public string MotherboardId { get; set; } = string.Empty;
    public string CpuId { get; set; } = string.Empty;
    public string PrimaryDiskId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsVirtualMachine { get; set; } = false;
}

public class ActivationResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SignedLicenseJson { get; set; }
}

public class ValidationRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string HardwareHash { get; set; } = string.Empty;
}

public class ValidationResponse
{
    public bool IsValid { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}

public class HeartbeatRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string HardwareHash { get; set; } = string.Empty;
}

public class HeartbeatResponse
{
    public bool IsAcknowledged { get; set; }
    public bool IsRevoked { get; set; }
}

public class ReactivationApiRequest
{
    public string LicenseKey { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string NewHardwareHash { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
}

public class ReactivationApiResponse
{
    public bool IsSuccess { get; set; }
    public string? SignedReactivationJson { get; set; }
    public string? ErrorMessage { get; set; }
}
