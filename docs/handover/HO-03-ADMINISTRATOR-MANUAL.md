# HO-03: Administrator Operations Manual

**Portal:** Web Management Portal (`LicenseSystem.Admin`)  
**URL:** `http://localhost:5001` (or production HTTPS domain)  
**Authentication:** Email & Password with Argon2id Hashing & Anti-Forgery CSRF Tokens

---

## 1. Product Registration & VM Policy Configuration

Navigate to **Products** in the navigation header:
1. Click **+ Register New Product**.
2. Fill in the product details:
   - **Product Code:** Unique alphanumeric identifier (e.g., `CAD-STUDIO-PRO`, `FIN-TRADER`).
   - **Product Name:** User-facing commercial title.
   - **Description:** Internal notes or version scope.
   - **Virtual Machine Policy:** Check *Allow Virtual Machines* if virtualized environments (VMware, VirtualBox, Hyper-V) are permitted, or leave unchecked to block VM activations.
3. Click **Save Product**.

---

## 2. License Generation & Issuance

Navigate to **Licenses** -> **+ Issue New License**:
1. Select one or more **Target Products** (multi-product bundle support).
2. Choose **License Type**:
   - `Monthly`: 30-day term from first activation.
   - `Annual`: 365-day term from first activation.
   - `Perpetual`: Perpetual / permanent validity.
3. Choose **Enforcement Mode**:
   - `OfflineWithGrace`: Standard mode; allows offline execution for up to 6 hours before requiring network validation.
   - `OnlineOnly`: High-security mode; requires continuous online validation and locks immediately if network drops.
   - `AirGapped`: 100% offline mode for isolated secure networks.
4. Set **Max Allowed Activations** (default: `1`).
5. Click **Generate License**. The system generates a formatted cryptographic key (e.g., `LIC-9B7C1A2F-8E3D4A5B`) ready for delivery to the customer.

---

## 3. Monitoring Active Devices & Hardware Fingerprints

In the **Licenses** list:
- Click **Details** on any active license.
- Review all bound devices, including:
  - Motherboard UUID / Serial
  - Processor ID
  - Primary SSD Serial / Volume GUID
  - Salted Composite Hardware Hash
  - Virtual Machine Status
  - Activation Timestamp & Last Heartbeat Timestamp

---

## 4. Revoking a Compromised License

If a customer refunds, disputes a payment, or leaks their license key:
1. Locate the license in the **Licenses** list.
2. Click **Revoke**.
3. Select or enter the **Revocation Reason** (e.g., *Refund issued*, *Piracy detected*, *Breach of terms*).
4. Confirm revocation. The database immediately marks `IsRevoked = true`, and the next online validation or heartbeat attempt from the client machine returns **HTTP 403 Forbidden**, locking the application.

---

## 5. Dual Reactivation Protocol Management

When a legitimate user upgrades their hardware (e.g., replaces their motherboard or SSD), their application locks with `LockedHardwareMismatch`. The administrator can resolve this via two channels:

### Option A: Online One-Click Approval
1. Customer initiates online reactivation from their client app.
2. In the Admin Portal, navigate to **Reactivations**.
3. Under **Pending Online Reactivation Requests**, locate the customer's request.
4. Review the old hardware hash, new hardware hash, and customer reason.
5. Click **Approve**. The server automatically migrates the active binding to the new hardware hash. The next time the client checks in, it unlocks automatically.

### Option B: Offline Air-Gapped Challenge Response (`.licreq` -> `.licresp`)
1. Customer provides their `.licreq` file (exported from their desktop application via email or USB).
2. Open the `.licreq` file in a text editor and copy the JSON payload.
3. In the Admin Portal, navigate to **Reactivations** -> **Process Offline Reactivation File**.
4. Paste the JSON into the **Offline Request JSON (.licreq)** box.
5. Click **Process & Sign Offline Response**.
6. The portal verifies the license, re-binds the device to the new hardware, and generates an unencrypted signed `.licresp` payload.
7. Click **Copy Response** or save as `.licresp` and send it back to the customer. Once imported, their machine unlocks without leaking any private keys.

---

## 6. Audit Trail & Security Event Monitoring

Navigate to **Audit Logs**:
- Inspect all system activities: `LicenseActivated`, `ValidationFailedRevoked`, `HardwareMismatchDetected`, `ReactivationRequested`, `ReactivationApproved`.
- Each log contains timestamp, actor, IP address, and non-sensitive event metadata.
- Cryptographic private keys and user passwords are never stored in audit logs.
