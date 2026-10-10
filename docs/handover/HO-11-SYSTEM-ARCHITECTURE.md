# HO-11: System Architecture & Design Specification

**System:** Personal License Management System  
**Pattern:** Clean Architecture & Domain-Driven Design (DDD)  
**Security Standard:** Asymmetric Cryptography (ECDSA P-256 / SHA-256) + RFC 8785 JCS

---

## 1. Architectural Layers & Solution Structure

The system is partitioned into decoupled projects, strictly adhering to Clean Architecture dependency rules:

```
                            ┌────────────────────────┐
                            │   LicenseSystem.Core   │
                            │ (Domain, Cryptography) │
                            └───────────▲────────────┘
                                        │
             ┌──────────────────────────┴──────────────────────────┐
             │                                                     │
┌────────────────────────────┐                        ┌────────────────────────────┐
│ LicenseSystem.ClientSdk    │                        │ LicenseSystem.Infrastructure│
│ (WMI, Policy, Temporal)    │                        │ (EF Core, PostgreSQL, Repos)│
└────────────▲───────────────┘                        └─────────────▲──────────────┘
             │                                                      │
┌────────────────────────────┐                        ┌─────────────┴──────────────┐
│ LicenseSystem.WpfDemo      │                        │                            │
│ (Desktop UI & CLI Runner)  │             ┌──────────────────────┐    ┌──────────────────────┐
└────────────────────────────┘             │  LicenseSystem.Api   │    │ LicenseSystem.Admin  │
                                           │  (REST Endpoints)    │    │ (Razor Pages Portal) │
                                           └──────────────────────┘    └──────────────────────┘
```

### Layer Responsibilities:
1. **`LicenseSystem.Core`**:
   - Zero external runtime dependencies.
   - Domain models: `License`, `Product`, `Activation`, `ReactivationTransaction`, `AuditEvent`.
   - Cryptographic engine: `CanonicalJsonSerializer` (RFC 8785 JCS), `EcdsaP256SigningService`, `EcdsaP256SignatureVerifier`.
   - Interfaces: `ISigningService`, `ISignatureVerifier`, `ILicenseRepository`, `IAuditRepository`.
2. **`LicenseSystem.Infrastructure`**:
   - Entity Framework Core with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL`).
   - Repository implementations: `LicenseRepository`, `AuditRepository`.
   - Automated backup and disaster recovery scripts.
3. **`LicenseSystem.Api`**:
   - Central ASP.NET Core Web API controllers: `LicenseController`, `ReactivationController`.
   - DTO models, API request validation, CORS, error handling middleware.
4. **`LicenseSystem.Admin`**:
   - Razor Pages web management portal.
   - Product catalog management, license issuance, revocation, and offline reactivation generator.
5. **`LicenseSystem.ClientSdk`**:
   - Hardware fingerprinting provider (WMI, CIM, and standard user Volume GUID fallback).
   - Temporal integrity engine (`ClockRollbackDetector`, monotonic uptime tracker).
   - Deterministic policy engine (`LicensePolicyEngine`).
   - High-level coordinator (`LicenseClient`).
6. **`LicenseSystem.WpfDemo`**:
   - Interactive desktop application runner with ANSI status badges, XAML visual interface, and Obfuscar configuration.

---

## 2. Database Entity-Relationship Model (PostgreSQL)

```
┌──────────────────┐         1..* ┌────────────────────┐ 1..*         ┌──────────────────┐
│     products     │◄─────────────┤  license_products  ├─────────────►│     licenses     │
├──────────────────┤              ├────────────────────┤              ├──────────────────┤
│ Id (UUID, PK)    │              │ Id (UUID, PK)      │              │ Id (UUID, PK)    │
│ Code (VARCHAR)   │              │ LicenseId (FK)     │              │ LicenseKey (UQ)  │
│ Name (VARCHAR)   │              │ ProductId (FK)     │              │ LicenseType      │
│ AllowVMs (BOOL)  │              │ FeatureFlagsJson   │              │ LicenseMode      │
└──────────────────┘              └────────────────────┘              │ MaxActivations   │
                                                                      │ ExpiresAtUtc     │
                                                                      │ IsRevoked (BOOL) │
                                                                      └────────┬─────────┘
                                                                               │ 1
                                                                               │
                                    ┌──────────────────────────────────────────┼───────────────────┐
                                    │ 0..*                                     │                   │ 0..*
                         ┌──────────▼───────────────┐                          │        ┌──────────▼───────────────┐
                         │       activations        │                          │        │ reactivation_transactions │
                         ├──────────────────────────┤                          │        ├──────────────────────────┤
                         │ Id (UUID, PK)            │                          │        │ Id (UUID, PK)            │
                         │ LicenseId (FK)           │                          │        │ LicenseId (FK)           │
                         │ HardwareHash (VARCHAR)   │                          │        │ OldHardwareHash          │
                         │ MotherboardId            │                          │        │ NewHardwareHash          │
                         │ CpuId                    │                          │        │ Nonce (UQ)               │
                         │ PrimaryDiskId            │                          │        │ Status (Pending/Approved)│
                         │ IsActive (BOOL)          │                          │        └──────────────────────────┘
                         │ LastHeartbeatUtc         │                          │
                         └──────────────────────────┘                          │
                                                                               │
┌──────────────────────────┐                                                   │
│       audit_events       │                                                   │
├──────────────────────────┤                                                   │
│ Id (UUID, PK)            │                                                   │
│ EventType (VARCHAR)      │                                                   │
│ Actor (VARCHAR)          │                                                   │
│ ProductCode              │                                                   │
│ LicenseKey               │◄──────────────────────────────────────────────────┘
│ DetailsJson              │
│ CreatedAtUtc             │
└──────────────────────────┘
```

---

## 3. End-to-End Cryptographic & Verification Data Flows

### A. Online Activation Flow
1. Desktop application collects hardware attributes and computes salted composite SHA-256 hash.
2. Sends `POST /api/v1/license/activate` with `{ productCode, licenseKey, hardwareHash }`.
3. Server validates key, checks concurrency seat limits (`activeActivations < MaxActivations`), records new activation record.
4. Server generates `LicensePayload`, formats into RFC 8785 canonical JSON, and signs using **ECDSA P-256 Private Key**.
5. Client SDK receives `SignedArtifact<LicensePayload>`, verifies signature using embedded **SPKI Public Key**, verifies hardware hash, and caches signed file locally.

### B. Offline Grace Period Execution (AT-11)
1. Desktop application launches with no active internet connection.
2. Client loads cached `SignedArtifact<LicensePayload>`.
3. Validates digital signature with SPKI public key.
4. Validates composite hardware hash against physical machine.
5. Checks temporal integrity (system clock rollback detection).
6. Calculates elapsed time since last verified online synchronization:
   - If $\Delta t < 6\text{ hours}$: Authorizes execution in **Grace Mode** with active countdown.
   - If $\Delta t \ge 6\text{ hours}$: Locks application with `LockedGraceExpired`.

### C. Air-Gapped Offline Reactivation Flow (AT-21)
1. Customer upgrades motherboard or SSD $\rightarrow$ Hardware hash changes $\rightarrow$ Application enters `LockedHardwareMismatch`.
2. Client SDK generates challenge file (`.licreq`) embedding a single-use 128-bit cryptographic nonce and the new hardware hash.
3. Administrator opens `.licreq` in the Admin Portal and approves hardware migration.
4. Admin Portal generates `SignedArtifact<ReactivationPayload>` (`.licresp`) signed by the server's ECDSA private key.
5. Customer imports `.licresp` into the desktop app.
6. Client SDK:
   - Verifies digital signature with SPKI public key.
   - Verifies the nonce matches the active challenge.
   - Invalidates (burns) the nonce permanently to prevent replay attacks (AT-21).
   - Re-authorizes the application on the new hardware.
