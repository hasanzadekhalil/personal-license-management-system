using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace LicenseSystem.ClientSdk.Hardware;

public class WindowsHardwareFingerprintProvider : IHardwareFingerprintProvider
{
    public HardwareInfo GetHardwareInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return QueryWindowsHardware();
        }

        // Graceful non-Windows fallback for Linux VPS or dev machines
        return QueryLinuxHardware();
    }

    public string GetCompositeHash(string salt = "LicenseSystem_Salt_2026")
    {
        var info = GetHardwareInfo();
        return info.ComputeCompositeHash(salt);
    }

    private HardwareInfo QueryWindowsHardware()
    {
        var mb = GetWindowsWmiProperty("Win32_BaseBoard", "SerialNumber");
        if (string.IsNullOrWhiteSpace(mb) || HardwareFingerprintHelper.CleanIdentifier(mb, "") == "")
        {
            mb = GetWindowsWmiProperty("Win32_ComputerSystemProduct", "UUID");
        }

        var cpu = GetWindowsWmiProperty("Win32_Processor", "ProcessorId");

        var disk = GetWindowsWmiProperty("Win32_DiskDrive", "SerialNumber");
        var diskMethod = "physical_serial";

        // Standard user fallback: If physical serial is restricted by UAC or RAID driver
        if (string.IsNullOrWhiteSpace(disk) || HardwareFingerprintHelper.CleanIdentifier(disk, "") == "")
        {
            disk = GetSystemDriveVolumeSerial();
            diskMethod = "volume_guid";
        }

        var manufacturer = GetWindowsWmiProperty("Win32_ComputerSystem", "Manufacturer") ?? "";
        var model = GetWindowsWmiProperty("Win32_ComputerSystem", "Model") ?? "";

        var isVm = manufacturer.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
                   manufacturer.Contains("innotek", StringComparison.OrdinalIgnoreCase) ||
                   manufacturer.Contains("QEMU", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase);

        return new HardwareInfo
        {
            MotherboardId = mb ?? "WIN_MB_DEFAULT",
            CpuId = cpu ?? "WIN_CPU_DEFAULT",
            PrimaryDiskId = disk ?? "WIN_DISK_DEFAULT",
            IsVirtualMachine = isVm,
            DeviceName = Environment.MachineName,
            StorageRetrievalMethod = diskMethod
        };
    }

    private string? GetWindowsWmiProperty(string wmiClass, string propertyName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-CimInstance -ClassName {wmiClass} -ErrorAction SilentlyContinue).{propertyName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private string GetSystemDriveVolumeSerial()
    {
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            var driveInfo = new DriveInfo(systemDrive);
            return driveInfo.VolumeLabel + "_" + driveInfo.TotalSize;
        }
        catch
        {
            return "VOL_FALLBACK_" + Environment.MachineName;
        }
    }

    private HardwareInfo QueryLinuxHardware()
    {
        string machineId = "LINUX_GENERIC_ID";
        if (File.Exists("/etc/machine-id"))
        {
            machineId = File.ReadAllText("/etc/machine-id").Trim();
        }

        var cpuHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.ProcessorCount.ToString()))).ToLowerInvariant();
        var diskHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(machineId + "_DISK"))).ToLowerInvariant();

        return new HardwareInfo
        {
            MotherboardId = "LINUX_MB_" + machineId[..Math.Min(12, machineId.Length)],
            CpuId = "LINUX_CPU_" + cpuHash[..12],
            PrimaryDiskId = "LINUX_DISK_" + diskHash[..12],
            IsVirtualMachine = false,
            DeviceName = Environment.MachineName,
            StorageRetrievalMethod = "volume_guid"
        };
    }
}
