namespace LicenseSystem.ClientSdk.Hardware;

public class MockHardwareFingerprintProvider : IHardwareFingerprintProvider
{
    public string MotherboardId { get; set; } = "BASEBOARD-MOCK-SERIAL-001";
    public string CpuId { get; set; } = "CPUID-BFEBFBFF000906EA";
    public string PrimaryDiskId { get; set; } = "SSD-NVME-SAMSUNG-980-S5R001";
    public bool IsVirtualMachine { get; set; } = false;
    public string DeviceName { get; set; } = "DEV-WORKSTATION-01";
    public string StorageRetrievalMethod { get; set; } = "physical_serial";

    public HardwareInfo GetHardwareInfo()
    {
        return new HardwareInfo
        {
            MotherboardId = MotherboardId,
            CpuId = CpuId,
            PrimaryDiskId = PrimaryDiskId,
            IsVirtualMachine = IsVirtualMachine,
            DeviceName = DeviceName,
            StorageRetrievalMethod = StorageRetrievalMethod
        };
    }

    public string GetCompositeHash(string salt = "LicenseSystem_Salt_2026")
    {
        return HardwareFingerprintHelper.ComputeCompositeHash(MotherboardId, CpuId, PrimaryDiskId, salt);
    }

    public void ReplaceMotherboard(string newId = "BASEBOARD-MOCK-SERIAL-REPLACED")
    {
        MotherboardId = newId;
    }

    public void ReplaceCpu(string newId = "CPUID-REPLACED-INTEL-I9")
    {
        CpuId = newId;
    }

    public void ReplacePrimaryDisk(string newId = "SSD-WD-BLACK-SN850X-REPLACED")
    {
        PrimaryDiskId = newId;
    }
}
