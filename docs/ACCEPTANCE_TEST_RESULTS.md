# Acceptance Test Execution Evidence (AT-01 to AT-22)

**Test Suite:** `LicenseSystem.Tests.Acceptance.MasterAcceptanceTestSuite`  
**Execution Environment:** .NET 8.0, PostgreSQL 16 (Docker `license_db`), Linux 6.8  
**Result Summary:** 22/22 Tests Passing (100% Pass Rate)  
**Total Acceptance Suite Count:** 48 Tests Passing Across All Suites

---

## Acceptance Test Matrix

| ID | Test Name | Target Layer | Verification Method | Result | Spec Requirement |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **AT-01** | Happy Path Activation | Web API / SDK | `POST /api/v1/license/activate` with valid key and hardware hash | **PASS** | Returns HTTP 200 with valid ECDSA P-256 signed license JSON artifact. |
| **AT-02** | Invalid License Key | Web API | `POST /api/v1/license/activate` with non-existent license key | **PASS** | Returns HTTP 404 Not Found, rejection audit logged. |
| **AT-03** | Tampered License File | Client SDK | Modifying 1 byte of payload data or signature bytes | **PASS** | Verification fails; `LicensePolicyEngine` immediately locks client (`Revoked`). |
| **AT-04** | Wrong Signature / Untrusted Key | Client SDK | License signed by rogue ECDSA P-256 keypair | **PASS** | Client SDK strictly compares signature against embedded SPKI public key and rejects. |
| **AT-05** | Server Offline (OnlineOnly Mode) | Client SDK | Network unreachable when evaluating `OnlineOnly` license | **PASS** | Zero grace allowed; immediately transitions to locked state. |
| **AT-06** | Hardware Change — Motherboard | Hardware / SDK | Motherboard UUID / Serial change | **PASS** | Hash mismatch detected; application locks until admin reactivation. |
| **AT-07** | Hardware Change — CPU | Hardware / SDK | Processor ID alteration | **PASS** | Hash mismatch detected; application locks. |
| **AT-08** | Hardware Change — Primary Disk | Hardware / SDK | Primary SSD / Volume GUID replacement | **PASS** | Hash mismatch detected; application locks. |
| **AT-09** | Simultaneous Hardware Changes | Hardware / SDK | Multiple components swapped simultaneously | **PASS** | Salted composite SHA-256 hash changes completely; application locks. |
| **AT-10** | Non-Monitored Hardware Change | Hardware / SDK | RAM, GPU, or peripheral change | **PASS** | Composite hash remains identical; application stays authorized. |
| **AT-11** | Grace Period Active | Policy Engine | Network down for `OfflineWithGrace` license (< 6 hours elapsed) | **PASS** | Application remains operational in `Grace` mode with remaining countdown timer. |
| **AT-12** | Grace Period Precedence | Policy Engine | Network down with hardware mismatch or expired license | **PASS** | Precedence strictly enforced: Hardware mismatch NEVER enters grace mode. |
| **AT-13** | Admin Revocation | Web API / Admin | Administrator revokes active license via portal/database | **PASS** | Subsequent validation fails with HTTP 403 Forbidden. |
| **AT-14** | Activation Limits & Concurrency | Web API | Attempting to activate license exceeding `MaxActivations` limit | **PASS** | Returns HTTP 409 Conflict, rejecting unauthorized concurrent machine. |
| **AT-15** | Database Backup & Restore | Infrastructure | Automated `pg_dump` and `pg_restore` verification script | **PASS** | `backup_restore.sh` successfully backs up, drops tables, and restores data intact. |
| **AT-16** | SQL Injection Resistance | Web API / EF Core | Submitting `' OR '1'='1'; DROP TABLE licenses; --` in request fields | **PASS** | Handled as literal string data via EF Core parameterized queries; no injection possible. |
| **AT-17** | Audit Log Integrity | Infrastructure | Inspecting audit log entries in `audit_events` | **PASS** | All lifecycle events recorded; sensitive data (private keys, passwords) excluded. |
| **AT-18** | License Duration (Monthly/Annual) | Web API / Core | Activating Monthly license type | **PASS** | Expiration timestamp calculated as exactly 30 days from activation timestamp. |
| **AT-19** | Network Drop Recovery | Client SDK | Simulating connection loss for `OnlineOnly` mode | **PASS** | Enforces immediate lock without grace period fallthrough. |
| **AT-20** | Clock Rollback Detection | Temporal Engine | Rolling back system wall-clock by >30 minutes or monotonic discrepancy | **PASS** | Rollback anomaly detected; client locked with `LockedClockTampered`. |
| **AT-21** | Replay Attack Defense (Reactivation) | Client SDK | Replaying previously applied `.licresp` file with consumed nonce | **PASS** | Replay attack detected and blocked; single-use nonce consumed permanently. |
| **AT-22** | Non-Admin Standard User UAC | Hardware Engine | Running hardware extraction without administrative privileges | **PASS** | Graceful fallback to volume GUID via standard user `DriveInfo` without crashing. |

---

## Execution Command & Output

```bash
dotnet test tests/LicenseSystem.Tests.Acceptance/LicenseSystem.Tests.Acceptance.csproj \
  --filter FullyQualifiedName~MasterAcceptanceTestSuite
```

```
Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 2 s - LicenseSystem.Tests.Acceptance.dll (net8.0)
```
