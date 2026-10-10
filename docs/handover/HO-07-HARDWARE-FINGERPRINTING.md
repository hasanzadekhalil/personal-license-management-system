# HO-07: Hardware Fingerprinting Specification & UAC Strategy

**Component:** `LicenseSystem.ClientSdk.Hardware`  
**Binding Formula:** Salted Composite SHA-256 over 3 Core Hardware Components  
**Standard User UAC Support:** 100% Non-Elevated Execution (No Admin Required)

---

## 1. 3-Component Hardware Composition

The client SDK computes a device fingerprint using a deterministic composite hash:

$$\text{CompositeHash} = \text{SHA256}(\text{MotherboardId} + "|" + \text{CpuId} + "|" + \text{PrimaryDiskId} + "|" + \text{Salt})$$

### Component Hierarchy:

| Component | Primary Provider (WMI / CIM) | Standard User Fallback | Volatility / Stability |
| :--- | :--- | :--- | :--- |
| **1. Motherboard** | `Win32_BaseBoard.SerialNumber` | `Win32_ComputerSystemProduct.UUID` | Highly Stable (Tied to physical chassis) |
| **2. Processor** | `Win32_Processor.ProcessorId` | Core count & instruction features | Highly Stable (Rarely upgraded in laptops/workstations) |
| **3. Primary Storage** | `Win32_DiskDrive.SerialNumber` | `DriveInfo(SystemDrive).VolumeLabel_TotalSize` | Stable (Tied to primary boot NVMe / SSD drive) |

---

## 2. Why RAM and GPU are Excluded (False Positive Prevention - AT-10)

Many legacy licensing engines bind to RAM size or GPU model:
- **RAM:** Users frequently upgrade, swap RAM slots, or BIOS changes memory mappings, causing false-positive license lockouts.
- **GPU:** Graphic designers and workstation users regularly swap GPUs or update external eGPUs.
- **Decision:** RAM and GPU are explicitly **excluded** from the composite hash (AT-10), ensuring legitimate non-critical hardware upgrades never interrupt application usage.

---

## 3. Standard User UAC Handling (AT-22)

In enterprise Windows environments, standard desktop users run without administrative privileges (non-elevated UAC):
- **The Challenge:** Querying physical disk hardware serial numbers via low-level Win32 `IOCTL_STORAGE_QUERY_PROPERTY` calls requires administrative permissions. Under standard user accounts, physical queries throw `Access Denied`.
- **The Solution:** The SDK checks if physical disk serials are available. If restricted, it seamlessly queries the active Windows `%SystemDrive%` volume serial and geometry via standard BCL `System.IO.DriveInfo`.
- **Outcome:** The SDK runs 100% reliably under restricted standard user accounts without triggering UAC elevation prompts (AT-22).

---

## 4. Virtual Machine Detection & Policy Enforcement

When a desktop application targets high-value or single-seat software, application owners can prohibit virtualized execution to prevent cloning:
1. **Detection Heuristics:**
   - WMI `Win32_ComputerSystem.Manufacturer` & `Model` are inspected for signatures: `VMware`, `innotek` (VirtualBox), `QEMU`, `Virtual Machine`, `Hyper-V`.
2. **Product Configuration:**
   - When registering a product in the Admin Portal, owners toggle **Allow Virtual Machines**:
     - If `AllowVirtualMachines = false`: Activation requests originating from VMs are rejected with `400 Bad Request ("Virtual machines are not permitted for this product")`.
     - If `AllowVirtualMachines = true`: VM activations are accepted normally.
