# Master Delivery, Acceptance Testing & Customer Presentation Plan

**System:** Personal License Management System  
**Version:** 1.0.0 Production Release  
**Status:** 100% Implemented, 48/48 Acceptance Tests Passing (AT-01 to AT-22 Verified)  
**Standard:** Clean Architecture .NET 8, PostgreSQL 16, ECDSA P-256 / SHA-256, RFC 8785 JCS

---

## 1. Executive Summary & Commitments Inventory

This Master Plan captures all architectural commitments, customer specifications (`Personal_License_SystemFinal.pdf` v1.3), technical inquiries, and acceptance test guarantees agreed upon with the client.

### Customer Commitments Matrix:
1. **Cryptographic Guarantee:** Asymmetric ECDSA P-256 (`secp256r1`) digital signing over SHA-256 with RFC 8785 JSON Canonicalization Scheme (JCS). Server private key is physically absent from client desktop binaries; clients embed only the SPKI public verification key.
2. **Self-Hosted Linux VPS Key Isolation:** Private key stored under POSIX `0400` permissions with `systemd-creds` encryption, protected RAM zeroization via `CryptographicOperations.ZeroMemory` upon loading, eliminating cloud KMS dependencies.
3. **Deterministic 3-Component Hardware Binding:** Salted composite hash covering Motherboard UUID, CPU ID, and Primary Physical Storage Serial with graceful non-admin UAC fallback to `%SystemDrive%` Volume GUID (AT-22). Non-monitored hardware (RAM, GPU) remains invariant (AT-10).
4. **Temporal Integrity & Anti-Clock Tampering:** Monotonic hardware uptime counter (`Environment.TickCount64`) combined with encrypted local UTC watermarks to detect backward clock manipulation >30 minutes (AT-20).
5. **Deterministic Policy Precedence:**
   - Signature invalid or tampered $\rightarrow$ Immediate termination (AT-03, AT-04).
   - Hardware mismatch $\rightarrow$ Immediate termination with zero grace (AT-06 to AT-09, AT-12).
   - Absolute license expiration $\rightarrow$ Immediate termination (AT-18).
   - Network drop under `OnlineOnly` $\rightarrow$ Immediate termination without grace (AT-05, AT-19).
   - Network drop under `OfflineWithGrace` $\rightarrow$ Exactly 6 hours of authorized offline execution (AT-11).
6. **Dual Reactivation Protocols:** Online 1-click administrative approval and offline air-gapped challenge-response (`.licreq` / `.licresp`) with single-use 128-bit cryptographic nonces to defeat replay attacks (AT-21).
7. **Database Persistence & Disaster Recovery:** PostgreSQL 16 managed via EF Core 8, concurrency seat limits (HTTP 409 Conflict, AT-14), immediate revocation enforcement (HTTP 403 Forbidden, AT-13), SQL injection resistance (AT-16), audit logging (AT-17), and automated database backup/restore scripts (AT-15 / HO-09).
8. **Anti-Tampering:** MSBuild post-build obfuscation via Obfuscar (`obfuscar.xml`).

---

## 2. 5 Dedicated Specialist Engineering Domains

| Domain | Responsible Role | Key Classes & Assets |
| :--- | :--- | :--- |
| **Domain 1: Cryptography & Key Security** | `cryptographic-security-engineer` | `CanonicalJsonSerializer`, `EcdsaP256SigningService`, `EcdsaP256SignatureVerifier`, `SignedArtifact<T>`, SPKI Public Key |
| **Domain 2: Hardware Binding & Forensics** | `hardware-forensics-engineer` | `WindowsHardwareFingerprintProvider`, WMI/CIM Providers, Volume GUID Fallback, Salted SHA-256 Engine |
| **Domain 3: Temporal Integrity & Policy** | `temporal-policy-engineer` | `ClockRollbackDetector`, `LicensePolicyEngine`, `TickCount64` Monotonic Tracker, UTC Watermark |
| **Domain 4: Backend & Infrastructure** | `distributed-backend-engineer` | `LicenseDbContext`, `LicenseController`, `ReactivationController`, `backup_restore.sh`, PostgreSQL Docker |
| **Domain 5: Desktop SDK & QA Automation** | `desktop-qa-automation-engineer` | `LicenseClient`, `MainWindow.xaml`, `Program.cs` Interactive Runner, `MasterAcceptanceTestSuite`, `obfuscar.xml` |

---

## 3. End-to-End Acceptance Test Verification Matrix (AT-01 to AT-22)

```text
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 2 s - LicenseSystem.Tests.Acceptance.dll (net8.0)
```

| ID | Criterion | Verification Method | Status |
| :--- | :--- | :--- | :---: |
| **AT-01** | Happy Path Activation | Online activation returns valid signed license artifact; client validates and unlocks | **PASS** |
| **AT-02** | Invalid License Key | Activation with non-existent license key returns HTTP 404 Not Found | **PASS** |
| **AT-03** | Tampered License File | Modifying 1 byte of license payload triggers cryptographic rejection; app locks | **PASS** |
| **AT-04** | Rogue / Untrusted Signature | License signed by untrusted private key fails public key verification; app locks | **PASS** |
| **AT-05** | Server Offline (Online-Only) | Network drop in Online-Only mode triggers immediate lockout without grace | **PASS** |
| **AT-06** | Hardware Swap — Motherboard | Motherboard UUID change triggers composite hash mismatch; app locks | **PASS** |
| **AT-07** | Hardware Swap — Processor | CPU ID change triggers composite hash mismatch; app locks | **PASS** |
| **AT-08** | Hardware Swap — Primary Disk | Primary storage drive change triggers composite hash mismatch; app locks | **PASS** |
| **AT-09** | Simultaneous Hardware Swap | Multiple simultaneous hardware component changes trigger lockout; app locks | **PASS** |
| **AT-10** | Non-Monitored Component Invariance | RAM and GPU upgrades do not alter composite fingerprint; app remains unlocked | **PASS** |
| **AT-11** | Authorized 6-Hour Offline Grace | Offline execution allowed within 6 hours of last sync; locks when elapsed $\ge$ 6h | **PASS** |
| **AT-12** | Strict Policy Precedence | Hardware mismatch or invalid signature never receives grace period; locks immediately | **PASS** |
| **AT-13** | Administrator Revocation | Validating a revoked license key returns HTTP 403 Forbidden; app locks immediately | **PASS** |
| **AT-14** | Concurrency Seat Limits | Activating a second machine when `MaxActivations = 1` returns HTTP 409 Conflict | **PASS** |
| **AT-15** | Database Backup & Restore | `backup_restore.sh` exists, is executable, and successfully verifies disaster recovery | **PASS** |
| **AT-16** | SQL Injection Resistance | Malicious payloads (`' OR '1'='1`) are treated as string literals by EF Core | **PASS** |
| **AT-17** | Audit Log Integrity | Security audit events recorded without exposing sensitive private keys | **PASS** |
| **AT-18** | License Duration Validity | Monthly license expires after exactly 30 days (720 hours) | **PASS** |
| **AT-19** | Network Drop Recovery | OnlineOnly mode enforces lockout on network loss; recovers on reconnection | **PASS** |
| **AT-20** | Anti-Clock Rollback Detection | Rolling back system clock by 5 days triggers temporal anomaly lockout | **PASS** |
| **AT-21** | Replay Attack Defense | Reusing consumed offline `.licresp` reactivation payload is rejected | **PASS** |
| **AT-22** | Non-Admin Standard User UAC Fallback | Standard users extract Volume GUID fallback seamlessly without UAC prompts | **PASS** |

---

## 4. 5-Round Double Verification Protocol

- **Verification Round 1 (Cryptographic Soundness):** Zero leakage of private key in client binaries; RFC 8785 canonicalization ensures 100% deterministic signatures.
- **Verification Round 2 (Hardware Stability):** Salted composite hash handles motherboard, CPU, and SSD variations; UAC fallback prevents customer crashes on standard accounts.
- **Verification Round 3 (Temporal & State Machine):** Monotonic uptime counter (`TickCount64`) prevents clock tampering; 6h grace and strict precedence enforced.
- **Verification Round 4 (Persistence & Recovery):** PostgreSQL schema parameterized; automated backup script verifies disaster recovery.
- **Verification Round 5 (Client Presentation Readiness):** Interactive CLI runner and WPF demo provide visual proof for client acceptance.

---

## 5. Client Demonstration & Presentation Script

### Step 1: Automated Test Verification
Run the master acceptance test suite in terminal:
```bash
dotnet test tests/LicenseSystem.Tests.Acceptance/LicenseSystem.Tests.Acceptance.csproj
```
Demonstrates 48 passing tests confirming all 22 criteria.

### Step 2: Interactive Desktop Attack Simulation
Launch the desktop demo runner:
```bash
dotnet run --project src/LicenseSystem.WpfDemo
```
Use the interactive simulation menu to demonstrate:
- Hardware component replacement (Motherboard, CPU, SSD lockout).
- 3-hour offline grace authorization vs 7-hour grace expiration lockout.
- 5-day system clock rollback lockout.

### Step 3: Administrative Control
Launch the central API and Admin Portal:
```bash
dotnet run --project src/LicenseSystem.Api
dotnet run --project src/LicenseSystem.Admin
```
Demonstrate 1-click license issuance, seat limit monitoring, revocation, and offline challenge-response approval.
