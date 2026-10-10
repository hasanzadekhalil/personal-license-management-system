# HO-01: Source Code Repository & Inventory

**System:** Personal License Management System  
**Version:** 1.0.0 Production Release  
**Repository Structure:** Multi-Project .NET 8 Clean Architecture Solution

---

## 1. Project Directory Inventory

| Project / Directory | Target Framework | Purpose | Key Classes |
| :--- | :--- | :--- | :--- |
| **`src/LicenseSystem.Core`** | `net8.0` | Domain entities, cryptographic engines, canonical serialization, and interfaces | `CanonicalJsonSerializer`, `EcdsaP256SigningService`, `EcdsaP256SignatureVerifier`, `SignedArtifact<T>`, `LicensePayload` |
| **`src/LicenseSystem.Infrastructure`** | `net8.0` | EF Core database context, PostgreSQL migrations, repositories, and backup scripts | `LicenseDbContext`, `LicenseRepository`, `AuditRepository`, `backup_restore.sh` |
| **`src/LicenseSystem.Api`** | `net8.0` | Central ASP.NET Core licensing REST Web API | `LicenseController`, `ReactivationController`, `LicenseDtos` |
| **`src/LicenseSystem.Admin`** | `net8.0` | Razor Pages administrative management portal | `Products/Index`, `Licenses/Index`, `Licenses/Create`, `Reactivations/Index`, `AuditLogs/Index` |
| **`src/LicenseSystem.ClientSdk`** | `net8.0` | Desktop client licensing SDK, hardware fingerprinting, temporal integrity, policy engine | `LicenseClient`, `LicensePolicyEngine`, `WindowsHardwareFingerprintProvider`, `ClockRollbackDetector`, `ReactivationHandler` |
| **`src/LicenseSystem.WpfDemo`** | `net8.0` | Interactive desktop application runner, WPF XAML views, and Obfuscar configuration | `Program.cs`, `MainWindow.xaml`, `App.xaml`, `obfuscar.xml` |
| **`tests/LicenseSystem.Tests.Acceptance`** | `net8.0` | Master acceptance test suite (AT-01 to AT-22) and subsystem unit tests | `MasterAcceptanceTestSuite`, `ApiIntegrationTests`, `PolicyEngineTests`, `ReactivationTests`, `HardwareFingerprintTests`, `EcdsaCryptoTests` |
| **`docs/`** | Markdown | Technical specifications, implementation plans, acceptance test evidence, and handover artifacts | `ACCEPTANCE_TEST_RESULTS.md`, `BACKUP_RESTORE_EVIDENCE.md`, `docs/handover/HO-01` to `HO-11` |

---

## 2. Global Solution Properties

Configured via root `Directory.Build.props`:
- **Language Version:** C# 12
- **Nullable Reference Types:** Enabled
- **Implicit Usings:** Enabled
- **Version:** `1.0.0` (Protects against host environment variable overrides)
