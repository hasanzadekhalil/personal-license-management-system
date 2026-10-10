# Personal License Management System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete, production-ready, fully tested Personal License Management System from scratch, featuring an ASP.NET Core Web API, Razor Pages Admin UI, PostgreSQL database with migrations, ECDSA P-256 asymmetric cryptographic signing, 3-component hardware binding, offline grace policy engine, reusable C# SDK, WPF demo application, and automated verification of all 22 Acceptance Tests (AT-01 to AT-22).

**Architecture:** Client-server architecture with strict cryptographic boundaries. The private signing key resides exclusively on the server, while client applications embed only the public verification key. Hardware fingerprinting collects Motherboard, CPU, and Primary SSD identifiers under standard user privileges. Dual reactivation supports both online approval and signed-offline replay-protected tokens.

**Tech Stack:** .NET 8 / 9 / 10 LTS (C# 12 / 13), ASP.NET Core Web API, ASP.NET Core Razor Pages, Entity Framework Core, PostgreSQL, System.Security.Cryptography (ECDSA P-256 / SHA-256), xUnit, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-10-personal-license-management-system-design.md`

## Global Constraints
- Target Framework: .NET 8.0 LTS (baseline runnable on Ubuntu 24.04 and modern Windows)
- Private signing key must NEVER be embedded into client SDK binaries or client configurations.
- Hardware binding must bind to Motherboard, CPU, and Primary SSD without requiring UAC elevation.
- 6-hour offline grace period applies strictly to `OfflineWithGrace` licenses; `OnlineOnly` licenses must lock immediately on validation failure.
- Clock rollback tampering must be detected and trigger immediate lockouts.
- All 22 Acceptance Tests (AT-01 through AT-22) must be executable and produce verifiable evidence.

---

### Task 1: Environment Setup & .NET SDK Installation

**Files:**
- Modify: Host OS environment
- Test: `dotnet --version`

**Interfaces:**
- Produces: Working .NET SDK CLI (`dotnet`) and runtime on Ubuntu 24.04 LTS.

- [ ] **Step 1: Install .NET 8 SDK on Ubuntu 24.04**
```bash
apt-get update && apt-get install -y dotnet-sdk-8.0
```

- [ ] **Step 2: Verify .NET SDK installation**
Run: `dotnet --version`  
Expected: Outputs `8.0.xxx`

- [ ] **Step 3: Setup PostgreSQL container via Docker Compose**
Create `docker-compose.yml` to spin up a local PostgreSQL 16 container for development and testing.

```yaml
version: '3.8'
services:
  db:
    image: postgres:16-alpine
    container_name: license_db
    restart: always
    environment:
      POSTGRES_USER: license_user
      POSTGRES_PASSWORD: license_secure_pass_2026
      POSTGRES_DB: license_management
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

- [ ] **Step 4: Launch database and verify connection**
```bash
docker compose up -d
sleep 3
docker exec license_db pg_isready -U license_user -d license_management
```
Expected: `license_management:5432 - accepting connections`

- [ ] **Step 5: Commit**
```bash
git add docker-compose.yml
git commit -m "chore: setup dotnet sdk and postgresql docker container

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 2: Solution Architecture & Project Scaffolding

**Files:**
- Create: `LicenseSystem.sln`
- Create: `src/LicenseSystem.Core/LicenseSystem.Core.csproj`
- Create: `src/LicenseSystem.Infrastructure/LicenseSystem.Infrastructure.csproj`
- Create: `src/LicenseSystem.Api/LicenseSystem.Api.csproj`
- Create: `src/LicenseSystem.Admin/LicenseSystem.Admin.csproj`
- Create: `src/LicenseSystem.ClientSdk/LicenseSystem.ClientSdk.csproj`
- Create: `src/LicenseSystem.WpfDemo/LicenseSystem.WpfDemo.csproj`
- Create: `tests/LicenseSystem.Tests.Acceptance/LicenseSystem.Tests.Acceptance.csproj`

**Interfaces:**
- Produces: Visual Studio / dotnet solution structure with correct project references.

- [ ] **Step 1: Scaffold solution and project files**
```bash
dotnet new sln -n LicenseSystem
dotnet new classlib -o src/LicenseSystem.Core -f net8.0
dotnet new classlib -o src/LicenseSystem.Infrastructure -f net8.0
dotnet new webapi -o src/LicenseSystem.Api -f net8.0
dotnet new razor -o src/LicenseSystem.Admin -f net8.0
dotnet new classlib -o src/LicenseSystem.ClientSdk -f net8.0
dotnet new console -o src/LicenseSystem.WpfDemo -f net8.0
dotnet new xunit -o tests/LicenseSystem.Tests.Acceptance -f net8.0

dotnet sln add src/LicenseSystem.Core
dotnet sln add src/LicenseSystem.Infrastructure
dotnet sln add src/LicenseSystem.Api
dotnet sln add src/LicenseSystem.Admin
dotnet sln add src/LicenseSystem.ClientSdk
dotnet sln add src/LicenseSystem.WpfDemo
dotnet sln add tests/LicenseSystem.Tests.Acceptance
```

- [ ] **Step 2: Configure Project References**
```bash
# Infrastructure depends on Core
dotnet add src/LicenseSystem.Infrastructure reference src/LicenseSystem.Core

# API depends on Infrastructure and Core
dotnet add src/LicenseSystem.Api reference src/LicenseSystem.Core
dotnet add src/LicenseSystem.Api reference src/LicenseSystem.Infrastructure

# Admin depends on Infrastructure and Core
dotnet add src/LicenseSystem.Admin reference src/LicenseSystem.Core
dotnet add src/LicenseSystem.Admin reference src/LicenseSystem.Infrastructure

# Client SDK depends on Core
dotnet add src/LicenseSystem.ClientSdk reference src/LicenseSystem.Core

# WpfDemo depends on ClientSdk
dotnet add src/LicenseSystem.WpfDemo reference src/LicenseSystem.ClientSdk

# Tests depend on all components
dotnet add tests/LicenseSystem.Tests.Acceptance reference src/LicenseSystem.Core
dotnet add tests/LicenseSystem.Tests.Acceptance reference src/LicenseSystem.Infrastructure
dotnet add tests/LicenseSystem.Tests.Acceptance reference src/LicenseSystem.Api
dotnet add tests/LicenseSystem.Tests.Acceptance reference src/LicenseSystem.ClientSdk
```

- [ ] **Step 3: Add required NuGet packages**
```bash
# Infrastructure: EF Core, Npgsql
dotnet add src/LicenseSystem.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.4
dotnet add src/LicenseSystem.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.0.4

# API: Swashbuckle / OpenAPI
dotnet add src/LicenseSystem.Api package Swashbuckle.AspNetCore --version 6.5.0

# Tests: FluentAssertions, Moq
dotnet add tests/LicenseSystem.Tests.Acceptance package FluentAssertions --version 6.12.0
dotnet add tests/LicenseSystem.Tests.Acceptance package Moq --version 4.20.70
dotnet add tests/LicenseSystem.Tests.Acceptance package Microsoft.AspNetCore.Mvc.Testing --version 8.0.4
```

- [ ] **Step 4: Build entire solution**
Run: `dotnet build LicenseSystem.sln`  
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**
```bash
git add LicenseSystem.sln src/ tests/
git commit -m "build: scaffold clean architecture solution projects

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 3: Domain Core Models & Cryptographic Contracts (`LicenseSystem.Core`)

**Files:**
- Create: `src/LicenseSystem.Core/Enums/LicenseMode.cs`
- Create: `src/LicenseSystem.Core/Enums/LicenseType.cs`
- Create: `src/LicenseSystem.Core/Enums/LicenseStatus.cs`
- Create: `src/LicenseSystem.Core/Models/Product.cs`
- Create: `src/LicenseSystem.Core/Models/License.cs`
- Create: `src/LicenseSystem.Core/Models/Activation.cs`
- Create: `src/LicenseSystem.Core/Models/ReactivationTransaction.cs`
- Create: `src/LicenseSystem.Core/Models/AuditEvent.cs`
- Create: `src/LicenseSystem.Core/Models/LicensePayload.cs`
- Create: `src/LicenseSystem.Core/Models/ReactivationPayload.cs`
- Create: `src/LicenseSystem.Core/Interfaces/ISigningService.cs`
- Create: `src/LicenseSystem.Core/Interfaces/ISignatureVerifier.cs`

**Interfaces:**
- Produces: Core domain entities and cryptographic contracts.

- [ ] **Step 1: Create Enums**
`LicenseMode` (`OnlineOnly`, `OfflineWithGrace`, `AirGapped`)  
`LicenseType` (`Monthly`, `Annual`, `Perpetual`)  
`LicenseStatus` (`Active`, `Expired`, `Revoked`, `LockedHardwareMismatch`, `LockedClockTampered`)

- [ ] **Step 2: Create Domain Models with complete properties**
Implement `Product`, `License`, `Activation`, `ReactivationTransaction`, `AuditEvent`.

- [ ] **Step 3: Define Canonical Cryptographic Interfaces**
```csharp
namespace LicenseSystem.Core.Interfaces;

public interface ISigningService
{
    string SignData(string canonicalData);
    string GetPublicKeySpki();
}

public interface ISignatureVerifier
{
    bool VerifySignature(string canonicalData, string signatureBase64, string publicKeySpki);
}
```

- [ ] **Step 4: Build and Verify Core Project**
Run: `dotnet build src/LicenseSystem.Core`  
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**
```bash
git add src/LicenseSystem.Core/
git commit -m "feat(core): implement domain entities and cryptographic interfaces

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 4: ECDSA P-256 Asymmetric Cryptographic Engine & Canonical JSON Serializer

**Files:**
- Create: `src/LicenseSystem.Core/Cryptography/CanonicalJsonSerializer.cs`
- Create: `src/LicenseSystem.Core/Cryptography/EcdsaP256SigningService.cs`
- Create: `src/LicenseSystem.Core/Cryptography/EcdsaP256SignatureVerifier.cs`
- Test: `tests/LicenseSystem.Tests.Acceptance/CryptographyTests.cs`

**Interfaces:**
- Consumes: `ISigningService`, `ISignatureVerifier`, `LicensePayload`.
- Produces: Deterministic RFC 8785 canonical JSON sorting and ECDSA P-256 signing and verification.

- [ ] **Step 1: Write failing unit tests for Canonical JSON and ECDSA signing**
Write tests verifying:
- Canonical JSON sorts keys alphabetically and removes whitespace.
- A signature generated with private key validates with public key.
- Mutating 1 character in payload causes signature verification to return `false`.

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~CryptographyTests`  
Expected: FAIL (types not yet implemented).

- [ ] **Step 3: Implement CanonicalJsonSerializer and ECDSA Services**
Implement deterministic JSON formatting using System.Text.Json, and ECDSA P-256 using `ECDsa.Create(ECCurve.NamedCurves.nistP256)`.

- [ ] **Step 4: Run tests and ensure all pass**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~CryptographyTests`  
Expected: PASS (All cryptographic tests green).

- [ ] **Step 5: Commit**
```bash
git add src/LicenseSystem.Core/Cryptography tests/LicenseSystem.Tests.Acceptance/CryptographyTests.cs
git commit -m "feat(crypto): implement canonical json serializer and ecdsa p256 signing engine

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 5: PostgreSQL Database Schema & EF Core Infrastructure (`LicenseSystem.Infrastructure`)

**Files:**
- Create: `src/LicenseSystem.Infrastructure/Data/LicenseDbContext.cs`
- Create: `src/LicenseSystem.Infrastructure/Repositories/LicenseRepository.cs`
- Create: `src/LicenseSystem.Infrastructure/Repositories/ProductRepository.cs`
- Create: `src/LicenseSystem.Infrastructure/Repositories/AuditRepository.cs`
- Create: `src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh`
- Test: `tests/LicenseSystem.Tests.Acceptance/DatabaseFixture.cs`

**Interfaces:**
- Consumes: `LicenseSystem.Core` entities.
- Produces: Fully migrated PostgreSQL database with complete relational model and audit logging.

- [ ] **Step 1: Implement `LicenseDbContext` with fluent entity configurations**
Configure table names, primary keys, relationships, foreign keys, and indexes matching Section 4 of the spec.

- [ ] **Step 2: Generate EF Core Migrations and apply to PostgreSQL**
```bash
cd src/LicenseSystem.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../LicenseSystem.Api
dotnet ef database update --startup-project ../LicenseSystem.Api
cd ../..
```

- [ ] **Step 3: Write backup and restore bash script (for AT-15 / HO-09)**
Create `src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh` implementing `pg_dump` and `pg_restore` verification.

- [ ] **Step 4: Test database connectivity and repository operations**
Write repository tests creating a product, issuing a license, and writing an audit event.

- [ ] **Step 5: Commit**
```bash
git add src/LicenseSystem.Infrastructure/
git commit -m "feat(infra): implement ef core db context, migrations, and repositories

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 6: Central License Web API (`LicenseSystem.Api`)

**Files:**
- Create: `src/LicenseSystem.Api/Controllers/LicenseController.cs`
- Create: `src/LicenseSystem.Api/Controllers/ReactivationController.cs`
- Create: `src/LicenseSystem.Api/DTOs/ActivationRequest.cs`
- Create: `src/LicenseSystem.Api/DTOs/ValidationRequest.cs`
- Create: `src/LicenseSystem.Api/DTOs/HeartbeatRequest.cs`
- Modify: `src/LicenseSystem.Api/Program.cs`
- Test: `tests/LicenseSystem.Tests.Acceptance/ApiIntegrationTests.cs`

**Interfaces:**
- Exposes:
  - `POST /api/v1/license/activate` -> issues signed license payload
  - `POST /api/v1/license/validate` -> verifies status, expiration, device binding
  - `POST /api/v1/license/heartbeat` -> updates last active heartbeat timestamp
  - `POST /api/v1/reactivate/request` -> registers hardware change request
  - `POST /api/v1/reactivate/complete` -> verifies and issues signed reactivation artifact

- [ ] **Step 1: Write API Integration tests**
Test valid activation, exceeding device limits (409 Conflict), expired licenses, and unknown product codes.

- [ ] **Step 2: Implement Controllers with dependency injection**
Inject `ILicenseRepository`, `ISigningService`, and `IAuditRepository`. Enforce 30-day monthly / 365-day annual timestamps accurately.

- [ ] **Step 3: Run API tests**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~ApiIntegrationTests`  
Expected: PASS.

- [ ] **Step 4: Commit**
```bash
git add src/LicenseSystem.Api/ tests/LicenseSystem.Tests.Acceptance/ApiIntegrationTests.cs
git commit -m "feat(api): implement central licensing web api endpoints and integration tests

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 7: Razor Pages Admin Portal & RBAC (`LicenseSystem.Admin`)

**Files:**
- Create: `src/LicenseSystem.Admin/Pages/Products/Index.cshtml` & `.cs`
- Create: `src/LicenseSystem.Admin/Pages/Licenses/Index.cshtml` & `.cs`
- Create: `src/LicenseSystem.Admin/Pages/Licenses/Create.cshtml` & `.cs`
- Create: `src/LicenseSystem.Admin/Pages/Reactivations/Index.cshtml` & `.cs`
- Create: `src/LicenseSystem.Admin/Pages/AuditLogs/Index.cshtml` & `.cs`
- Modify: `src/LicenseSystem.Admin/Program.cs`

**Interfaces:**
- Produces: Web management portal for creating products, issuing licenses, approving hardware changes, issuing offline signed `.licresp` files, and inspecting audit trails.

- [ ] **Step 1: Implement Product & License Management Pages**
UI to add products (unlimited), generate Monthly/Annual licenses with choice of `OnlineOnly`, `OfflineWithGrace`, or `AirGapped` modes.

- [ ] **Step 2: Implement Reactivation Hub**
UI to view pending hardware mismatch requests, approve online, or upload a `.licreq` file to download a digitally signed `.licresp` response.

- [ ] **Step 3: Implement Non-sensitive Audit Log Viewer**
Displays administrative actions, IP addresses, timestamps, without displaying private keys or passwords.

- [ ] **Step 4: Build Admin Project**
Run: `dotnet build src/LicenseSystem.Admin`  
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**
```bash
git add src/LicenseSystem.Admin/
git commit -m "feat(admin): implement razor pages management portal and reactivation hub

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 8: Client SDK — Hardware Fingerprinting Engine (`LicenseSystem.ClientSdk`)

**Files:**
- Create: `src/LicenseSystem.ClientSdk/Hardware/IHardwareFingerprintProvider.cs`
- Create: `src/LicenseSystem.ClientSdk/Hardware/WindowsHardwareFingerprintProvider.cs`
- Create: `src/LicenseSystem.ClientSdk/Hardware/MockHardwareFingerprintProvider.cs`
- Create: `src/LicenseSystem.ClientSdk/Hardware/HardwareInfo.cs`
- Test: `tests/LicenseSystem.Tests.Acceptance/HardwareFingerprintTests.cs`

**Interfaces:**
- Produces: `IHardwareFingerprintProvider` computing composite SHA-256 hash of Motherboard, CPU, and Primary SSD (with standard user volume GUID fallback).

- [ ] **Step 1: Write hardware fingerprinting tests**
Assert:
- Composite hash contains components from all 3 elements.
- Changing Motherboard alters composite hash.
- Changing CPU alters composite hash.
- Changing SSD alters composite hash.

- [ ] **Step 2: Implement WindowsHardwareFingerprintProvider and MockHardwareFingerprintProvider**
Implement WMI queries for `Win32_BaseBoard`, `Win32_Processor`, `Win32_DiskDrive` and Win32 `GetVolumeInformationW` fallback.

- [ ] **Step 3: Run hardware tests**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~HardwareFingerprintTests`  
Expected: PASS.

- [ ] **Step 4: Commit**
```bash
git add src/LicenseSystem.ClientSdk/Hardware tests/LicenseSystem.Tests.Acceptance/HardwareFingerprintTests.cs
git commit -m "feat(sdk): implement 3-component hardware fingerprinting engine and test fixtures

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 9: Client SDK — Temporal Integrity & Offline Grace Policy Engine

**Files:**
- Create: `src/LicenseSystem.ClientSdk/Temporal/ITimeProvider.cs`
- Create: `src/LicenseSystem.ClientSdk/Temporal/SystemTimeProvider.cs`
- Create: `src/LicenseSystem.ClientSdk/Temporal/ClockRollbackDetector.cs`
- Create: `src/LicenseSystem.ClientSdk/Policy/LicensePolicyEngine.cs`
- Test: `tests/LicenseSystem.Tests.Acceptance/PolicyEngineTests.cs`

**Interfaces:**
- Produces: Policy engine evaluating license validity, enforcing strict 6-hour offline grace on `OfflineWithGrace`, immediate lock on `OnlineOnly`, and clock rollback detection.

- [ ] **Step 1: Write policy engine and clock rollback unit tests**
Assert:
- `OnlineOnly` license fails validation -> immediately transitions to `Locked`.
- `OfflineWithGrace` license disconnected -> transitions to `GracePeriod` for up to 6 hours.
- Disconnected after 6 hours -> transitions to `Locked`.
- Hardware mismatch -> immediately transitions to `Locked` regardless of grace status.
- Moving system time backwards by 2 hours -> triggers `LockedClockTampered`.

- [ ] **Step 2: Implement ClockRollbackDetector and LicensePolicyEngine**
State machine checking expiration, hardware match, signature validity, and temporal integrity.

- [ ] **Step 3: Run policy tests**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~PolicyEngineTests`  
Expected: PASS.

- [ ] **Step 4: Commit**
```bash
git add src/LicenseSystem.ClientSdk/Temporal src/LicenseSystem.ClientSdk/Policy tests/LicenseSystem.Tests.Acceptance/PolicyEngineTests.cs
git commit -m "feat(sdk): implement temporal integrity verifier and offline grace policy engine

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 10: Client SDK — Dual Reactivation Handler

**Files:**
- Create: `src/LicenseSystem.ClientSdk/Reactivation/IReactivationHandler.cs`
- Create: `src/LicenseSystem.ClientSdk/Reactivation/ReactivationHandler.cs`
- Create: `src/LicenseSystem.ClientSdk/LicenseClient.cs`
- Test: `tests/LicenseSystem.Tests.Acceptance/ReactivationTests.cs`

**Interfaces:**
- Produces: `LicenseClient` exposing complete developer facade:
  - `InitializeAsync()`
  - `ValidateAsync()`
  - `ExportReactivationRequest(string filePath)`
  - `ImportReactivationResponse(string filePath)`

- [ ] **Step 1: Write reactivation tests**
Assert:
- Generates `.licreq` containing valid nonce and new hardware hash.
- Importing valid signed `.licresp` with matching nonce updates binding and unlocks client.
- Replaying old `.licresp` fails with replay protection error.

- [ ] **Step 2: Implement ReactivationHandler and LicenseClient facade**
- [ ] **Step 3: Run reactivation tests**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance --filter FullyQualifiedName~ReactivationTests`  
Expected: PASS.

- [ ] **Step 4: Commit**
```bash
git add src/LicenseSystem.ClientSdk/Reactivation src/LicenseSystem.ClientSdk/LicenseClient.cs tests/LicenseSystem.Tests.Acceptance/ReactivationTests.cs
git commit -m "feat(sdk): implement dual reactivation handler and high-level license client facade

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 11: WPF Demo Desktop Application & CLI Runner (`LicenseSystem.WpfDemo`)

**Files:**
- Create: `src/LicenseSystem.WpfDemo/ViewModels/MainViewModel.cs`
- Create: `src/LicenseSystem.WpfDemo/Views/MainWindow.xaml` (for Windows execution)
- Create: `src/LicenseSystem.WpfDemo/Program.cs` (Dual CLI/WPF entry point runnable on Linux and Windows)

**Interfaces:**
- Produces: Runnable desktop demo application showing:
  - Active License Key, Expiry Date, License Mode
  - Hardware status (Motherboard, CPU, Primary SSD)
  - Interactive triggers: "Simulate Motherboard Replacement", "Simulate Offline Disconnect", "Export Reactivation Request", "Import Signed Reactivation Response"

- [ ] **Step 1: Implement Dual-Mode CLI/WPF Client Runner**
Permits full CLI demonstration of interactive license states on Linux and full visual WPF execution on Windows.

- [ ] **Step 2: Test CLI Runner execution**
Run: `dotnet run --project src/LicenseSystem.WpfDemo -- --demo`  
Expected: Outputs clean diagnostic demonstration of license activation, hardware lock, and offline verification.

- [ ] **Step 3: Commit**
```bash
git add src/LicenseSystem.WpfDemo/
git commit -m "feat(demo): implement interactive desktop demo and cli verification runner

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 12: Acceptance Tests Automation Suite (AT-01 to AT-22)

**Files:**
- Create: `tests/LicenseSystem.Tests.Acceptance/AcceptanceTestsRunner.cs`
- Create: `tests/LicenseSystem.Tests.Acceptance/AT01_To_AT22_TestSuite.cs`
- Create: `docs/ACCEPTANCE_TEST_RESULTS.md`

**Interfaces:**
- Produces: Automated execution of every single test ID from AT-01 through AT-22, generating a markdown evidence report.

- [ ] **Step 1: Implement AT01_To_AT22_TestSuite.cs**
Map every acceptance test condition with verifiable asserts:
- AT-01 to AT-05: Activation, Server enforcement, Tamper rejection, Private key scan, Online lock.
- AT-06 to AT-10: Motherboard mismatch, CPU mismatch, SSD mismatch, Persistence of lock, Config tamper rejection.
- AT-11 to AT-15: 6h offline grace, Offline mismatch priority, Dual reactivation, Audit logging, Backup/restore.
- AT-16 to AT-22: Demo run, Hardware docs, Exact monthly/annual duration, Online outage grace isolation, Clock rollback, Replay attack rejection, Admin RBAC.

- [ ] **Step 2: Execute complete Acceptance Test Suite**
Run: `dotnet test tests/LicenseSystem.Tests.Acceptance -v normal`  
Expected: 100% Passed.

- [ ] **Step 3: Generate verifiable ACCEPTANCE_TEST_RESULTS.md**
- [ ] **Step 4: Commit**
```bash
git add tests/LicenseSystem.Tests.Acceptance/ docs/ACCEPTANCE_TEST_RESULTS.md
git commit -m "test: implement comprehensive acceptance test suite for AT-01 through AT-22

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 13: Database Backup/Restore Automation (AT-15 & HO-09)

**Files:**
- Create: `scripts/backup_db.sh`
- Create: `scripts/restore_db.sh`
- Create: `docs/BACKUP_RESTORE_EVIDENCE.md`

- [ ] **Step 1: Write automated backup script using pg_dump**
- [ ] **Step 2: Write automated restore script using pg_restore**
- [ ] **Step 3: Execute full disaster recovery simulation on Docker PostgreSQL**
Run backup, insert test record, restore backup, verify test record state.

- [ ] **Step 4: Record evidence in BACKUP_RESTORE_EVIDENCE.md**
- [ ] **Step 5: Commit**
```bash
git add scripts/ docs/BACKUP_RESTORE_EVIDENCE.md
git commit -m "feat(db): implement automated postgresql backup and restore verification scripts

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 14: Handover Documentation & Deployment Guide (HO-01 to HO-11)

**Files:**
- Create: `docs/HO-01_SOURCE_AND_SCHEMA.md`
- Create: `docs/HO-02_BUILD_AND_RELEASE.md`
- Create: `docs/HO-03_CSHARP_SDK_GUIDE.md`
- Create: `docs/HO-04_LINUX_VPS_DEPLOYMENT.md`
- Create: `docs/HO-05_SECURITY_SPECIFICATION.md`
- Create: `docs/HO-06_OFFLINE_LICENSING_GUIDE.md`
- Create: `docs/HO-07_HARDWARE_BINDING_GUIDE.md`
- Create: `docs/HO-08_ACCEPTANCE_CHECKLIST.md`
- Create: `docs/HO-09_BACKUP_AND_RESTORE.md`
- Create: `docs/HO-10_TROUBLESHOOTING_AND_RECOVERY.md`
- Create: `docs/HO-11_HANDOVER_AGREEMENT.md`
- Create: `README.md`

- [ ] **Step 1: Author complete documentation package for HO-01 through HO-11**
- [ ] **Step 2: Create root README.md with one-step setup and runnable instructions**
- [ ] **Step 3: Commit**
```bash
git add docs/ README.md
git commit -m "docs: author complete handover documentation package HO-01 through HO-11

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```
