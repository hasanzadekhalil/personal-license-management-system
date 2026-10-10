namespace LicenseSystem.ClientSdk.Hardware;

public class HardwareInfo
{
    public string MotherboardId { get; set; } = string.Empty;
    public string CpuId { get; set; } = string.Empty;
    public string PrimaryDiskId { get; set; } = string.Empty;
    public bool IsVirtualMachine { get; set; } = false;
    public string DeviceName { get; set; } = string.Empty;
    public string StorageRetrievalMethod { get; set; } = "physical_serial"; // "physical_serial" or "volume_guid"

    public string ComputeCompositeHash(string salt = "LicenseSystem_Salt_2026")
    {
        return HardwareFingerprintHelper.ComputeCompositeHash(MotherboardId, CpuId, PrimaryDiskId, salt);
    }
}

public interface IHardwareFingerprintProvider
{
    HardwareInfo GetHardwareInfo();
    string GetCompositeHash(string salt = "LicenseSystem_Salt_2026");
}
