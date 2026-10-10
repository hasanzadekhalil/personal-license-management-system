namespace LicenseSystem.Core.Models;

public class Activation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;

    public string HardwareHash { get; set; } = string.Empty;
    public string MotherboardId { get; set; } = string.Empty;
    public string CpuId { get; set; } = string.Empty;
    public string PrimaryDiskId { get; set; } = string.Empty;

    public bool IsVirtualMachine { get; set; } = false;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
}
