# Personal License Management System — Comprehensive Architecture & Design Specification

**Specification Version:** 1.0 (Derived from Owner Brief v1.3)  
**Date:** 10 October 2026  
**Status:** Approved for Implementation  
**Target Platform:** .NET LTS (ASP.NET Core, C#), PostgreSQL, Windows Desktop (C# SDK / WPF Demo)

---

## 1. System Vision & Purpose

The **Personal License Management System** is a private, production-grade licensing infrastructure designed for owner-controlled Windows desktop applications. It provides:
1. **Central Licensing Authority:** ASP.NET Core Web API and Razor Pages Admin portal hosted on Linux VPS.
2. **Asymmetric Cryptographic Security:** ECDSA (P-256 / SHA-256) with strict server-side private key isolation; client binaries carry only the public verification key.
3. **Decoupled Architecture:** Reusable C# Client SDK, WPF demo application, and language-agnostic REST API ready for future non-.NET clients.
4. **Resilient Hardware Binding:** 3-component binding (Motherboard, CPU, Primary SSD) running without mandatory UAC elevation, featuring deterministic lockouts and dual-path reactivation (online and signed-offline).
5. **Configurable Policy Modes:** Deterministic enforcement of `OnlineOnly`, `OfflineWithGrace` (strict 6-hour offline window), and `AirGapped` modes.
6. **Verifiable Quality:** 100% automated test coverage mapped directly to Acceptance Tests **AT-01 through AT-22** and Handover items **HO-01 through HO-11**.

---

## 2. Technology Baseline & Solution Architecture

### 2.1 Technology Stack
* **Runtime:** .NET 8 / 9 / 10 LTS (C# 12 / 13)
* **Backend Framework:** ASP.NET Core Web API (REST) & ASP.NET Core Razor Pages (Admin UI)
* **Database:** PostgreSQL 16+ with Entity Framework Core (Code-First Migrations, connection pooling, and automated backup/restore scripts)
* **Cryptography:** System.Security.Cryptography (`ECDsaCng` on Windows / `ECDsaOpenSsl` on Linux, standardized via `ECDsa.Create(ECCurve.NamedCurves.nistP256)`)
* **Serialization:** RFC 8785 JSON Canonicalization Scheme (JCS) for deterministic signature hashes
* **Client UI:** WPF (.NET Windows Desktop) + CLI Test Runner for Linux/Windows headless test execution
* **Obfuscation:** Obfuscar integrated into Release MSBuild pipeline

### 2.2 Solution Project Structure
```
LicenseSystem/
├── src/
│   ├── LicenseSystem.Core/             # Pure Domain Models, Cryptographic Contracts, Enums, DTOs
│   ├── LicenseSystem.Infrastructure/   # EF Core DbContext, PostgreSQL Repositories, Migrations, Key Vault
│   ├── LicenseSystem.Api/              # ASP.NET Core Web API (Endpoints: /activate, /validate, /heartbeat, /reactivate)
│   ├── LicenseSystem.Admin/            # ASP.NET Core Razor Pages (RBAC Admin Portal, Product/License Management, Reactivation Hub)
│   ├── LicenseSystem.ClientSdk/        # Reusable Client Library (Hardware Fingerprinting, Offline Verifier, Watermark Engine)
│   └── LicenseSystem.WpfDemo/          # Windows Desktop Demo Application showcasing real-time status and lock/unlock workflows
├── tests/
│   ├── LicenseSystem.Tests.Unit/       # Cryptographic, Policy, and Hardware Engine Unit Tests
│   └── LicenseSystem.Tests.Acceptance/ # Automated Verification Suite executing AT-01 through AT-22
├── docker/
│   ├── docker-compose.yml              # PostgreSQL + Server Orchestration
│   └── Dockerfile                      # Multi-stage production build for Linux VPS
└── docs/                               # Handover Deliverables (HO-01 to HO-11)
```

---

## 3. Core Architectural Decisions (Resolving the 7 Design Pillars)

### Pillar 1: Windows User Privileges & Hardware Fingerprint
* **Constraint:** Zero mandatory UAC Administrator requirements. Applications execute cleanly in standard Windows user sessions (`Medium Integrity`).
* **Hardware Collection:**
  * **Motherboard:** Primary query `Win32_BaseBoard.SerialNumber`. Fallback if empty/generic (`"Default String"`, `"None"`): `Win32_ComputerSystemProduct.UUID`.
  * **CPU:** `Win32_Processor.ProcessorId` + Core count.
  * **Primary SSD:** Primary query `Win32_DiskDrive` (DeviceID `\\.\PHYSICALDRIVE0` -> `SerialNumber`).
  * **Documented Fallback:** If SSD physical serial is inaccessible without UAC or masked by RAID/VMD drivers, fallback to System Drive (`%SystemDrive%`) Volume Serial Number & Volume GUID via Win32 `GetVolumeInformationW`.
* **Composite Hash:** `SHA-256(Salt + Motherboard + CPU + PrimaryDisk)`. If any single component changes, lock immediately.

### Pillar 2: Virtual Machine Handling & Clone Protection
* **Configurable Policy:** Products and Licenses support `AllowVirtualMachines: true | false`.
* **VM Detection:** Hypervisor bit (CPUID ECX bit 31) + Manufacturer checks (`VMware`, `VirtualBox`, `Microsoft Corporation`, `QEMU`).
* **Clone Mitigation:**
  * Online Mode: Concurrent session heartbeat tracking detects duplicate identical hashes from differing IPs and revokes active sessions.
  * Offline Mode: Internal monotonic sequence counters detect snapshot restoration and trigger clock/state tampering lockouts.

### Pillar 3: License-to-Product Topology
* **Database Model:** Normalized relational schema with `products`, `licenses`, and a many-to-many junction table `license_products`.
* **Scope:** Single-product licenses implemented in initial UI/API; schema inherently supports multi-product suites/bundles for zero-migration future expansion.

### Pillar 4: Offline Reactivation Protocol
* **No Unnecessary Encryption:** The request file (`.licreq`) contains only public identifiers: License Key, Product Code, New Hardware Fingerprint, Random Cryptographic Nonce, and Timestamp.
* **Integrity & Replay Defense:**
  1. Client exports `.licreq`.
  2. Admin uploads to Razor Pages portal, verifies mismatch history, and approves.
  3. Server signs `.licresp` using private key: `{ LicenseKey, NewHardwareHash, Nonce, ValidUntil, Signature }`.
  4. Client verifies ECDSA signature, matches Nonce against local state, and updates binding. Old or replayed `.licresp` files are instantly rejected.

### Pillar 5: License Modes & Policy Precedence
1. **`OnlineOnly`:** Requires network verification at startup or heartbeat. Unexpected network loss causes **Immediate Lock** (no grace period).
2. **`OfflineWithGrace`:** Designed for mobile/intermittent connectivity. Unexpected connection failure triggers a **6-Hour Grace Period** with user warnings. If connection is not restored within 6 hours, protected features lock.
3. **`AirGapped`:** 100% offline via signed `.lic` artifact. Never attempts network connections. Operates until signed `ExpiresAt` date.
4. **Policy Precedence Rule:** Revocation, expiration, signature failure, product mismatch, and hardware mismatch **NEVER** receive grace time.

### Pillar 6: Temporal Integrity & Anti-Clock Rollback Engine
* **Multi-Layered Checks:**
  1. Internal UTC Normalization.
  2. Monotonic Uptime Tracking via `Stopwatch.GetTimestamp()` / `Environment.TickCount64`.
  3. Encrypted Local Watermark State (records `LastKnownUtcTime` and `MonotonicSequence`).
* **Drift Tolerance:** 30-minute grace window for natural timezone changes / CMOS clock drift. Any backward step exceeding 30 minutes triggers immediate `ERR_CLOCK_TAMPERING` lockout.

### Pillar 7: Obfuscation & Protection Pipeline
* **Integration:** Automated **Obfuscar** execution during `Release` builds.
* **Coverage:** Renames private symbols, flattens control-flow logic, encrypts string literals, and preserves public SDK DTOs and test fixtures.

---

## 4. Database Schema (PostgreSQL DDL)

```sql
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Products Table
CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    allow_vms BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Admin Users Table (RBAC)
CREATE TABLE admin_users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL DEFAULT 'Admin', -- 'SuperAdmin', 'Admin', 'Auditor'
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Master Licenses Table
CREATE TABLE licenses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    license_key VARCHAR(64) UNIQUE NOT NULL,
    license_type VARCHAR(20) NOT NULL, -- 'Monthly' (30 days), 'Annual' (365 days), 'Perpetual'
    license_mode VARCHAR(25) NOT NULL, -- 'OnlineOnly', 'OfflineWithGrace', 'AirGapped'
    max_activations INT NOT NULL DEFAULT 1,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    revocation_reason TEXT,
    revoked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activated_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ NOT NULL
);

-- License to Products Junction
CREATE TABLE license_products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    license_id UUID NOT NULL REFERENCES licenses(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    features JSONB NOT NULL DEFAULT '{}',
    UNIQUE (license_id, product_id)
);

-- Activations and Hardware Binding Records
CREATE TABLE activations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    license_id UUID NOT NULL REFERENCES licenses(id) ON DELETE CASCADE,
    hardware_hash VARCHAR(64) NOT NULL,
    motherboard_id VARCHAR(100) NOT NULL,
    cpu_id VARCHAR(100) NOT NULL,
    disk_id VARCHAR(100) NOT NULL,
    is_vm BOOLEAN NOT NULL DEFAULT FALSE,
    device_name VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    activated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_heartbeat TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Reactivation Transactions (Replay-Protected)
CREATE TABLE reactivation_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    license_id UUID NOT NULL REFERENCES licenses(id) ON DELETE CASCADE,
    old_hardware_hash VARCHAR(64) NOT NULL,
    new_hardware_hash VARCHAR(64) NOT NULL,
    nonce VARCHAR(64) UNIQUE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected', 'Used'
    requested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ,
    resolved_by UUID REFERENCES admin_users(id)
);

-- Comprehensive Non-Sensitive Audit Log
CREATE TABLE audit_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_type VARCHAR(50) NOT NULL, -- 'LicenseCreated', 'Activated', 'MismatchDetected', 'Reactivated', 'Revoked'
    actor VARCHAR(50) NOT NULL,
    product_code VARCHAR(50),
    license_key VARCHAR(64),
    details JSONB NOT NULL DEFAULT '{}',
    ip_address VARCHAR(45),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 5. Cryptographic Specification

### 5.1 Key Architecture
* **Algorithm:** ECDSA over NIST P-256 Curve (`secp256r1`) with SHA-256 hashing.
* **Server Authority:** The private key (`ECPrivateKey.pem`) is loaded exclusively from environment variables or secure Linux file permissions (`chmod 600`), isolated completely from web root and git.
* **Client SDK:** The public key (`ECPublicKey.pem` or Base64 SPKI) is embedded directly into the C# SDK for instant mathematical signature verification.

### 5.2 Signed License Artifact Format (`.lic`)
```json
{
  "header": {
    "version": "1.0",
    "algorithm": "ES256"
  },
  "payload": {
    "license_id": "8f3b2e7a-9a1c-4b5d-8f2e-1a2b3c4d5e6f",
    "license_key": "LIC-9482-1092-4821-9921",
    "product_code": "PROD-WIN-01",
    "license_mode": "OfflineWithGrace",
    "hardware_hash": "c6b45a2798e219e8b2b1a3e6f98101a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8",
    "issued_at": "2026-10-10T12:00:00Z",
    "activated_at": "2026-10-10T12:05:00Z",
    "expires_at": "2026-11-09T12:05:00Z",
    "grace_period_hours": 6
  },
  "signature": "MEYCIQC3Z...base64_encoded_ecdsa_signature..."
}
```
* **Canonical Hash:** Prior to signing or verification, the `payload` JSON is serialized deterministically in sorted lexicographical order with UTF-8 encoding.

---

## 6. Acceptance Test Suite (AT-01 to AT-22)

Each acceptance test defined in Section 08 of the developer brief has a dedicated, automated xUnit test:

1. **AT-01 (Valid Activation):** Activates license with identical hardware hash; asserts `IsAuthorized == true`.
2. **AT-02 (Server Policy Enforcement):** Attempts activation beyond limit; asserts HTTP 409 Conflict.
3. **AT-03 (Tamper Detection):** Mutates 1 byte in payload; asserts cryptographic signature verification failure.
4. **AT-04 (Key Isolation):** Static reflection/binary scan verifying private key is 100% absent from client binaries.
5. **AT-05 (Online Failure Immediate Lock):** In `OnlineOnly` mode, cuts connection; asserts instant lock with zero grace.
6. **AT-06 (Motherboard Mismatch):** Mutates Motherboard UUID; asserts immediate `HardwareMismatch` lock.
7. **AT-07 (CPU Mismatch):** Mutates CPU ID; asserts immediate `HardwareMismatch` lock.
8. **AT-08 (Primary SSD Mismatch):** Mutates Primary SSD serial; asserts immediate `HardwareMismatch` lock.
9. **AT-09 (Lock Persistence):** Verifies app stays locked until approved reactivation occurs.
10. **AT-10 (Local Tamper Resistance):** Modifies local cached state; asserts integrity check rejects modified state.
11. **AT-11 (Offline 6h Grace):** `OfflineWithGrace` mode operates normally for 6h without network before locking.
12. **AT-12 (Offline Mismatch Priority):** Offline hardware mismatch overrides grace and locks immediately.
13. **AT-13 (Dual Reactivation):** Verifies both online API approval and signed `.licresp` unlock the application.
14. **AT-14 (Non-Sensitive Audit):** Verifies all events logged without exposing passwords or keys.
15. **AT-15 (Backup Restoration):** Automated script executes `pg_dump` and verifies clean database restore.
16. **AT-16 (SDK & Demo Execution):** Clean compilation and execution of SDK and WPF Demo.
17. **AT-17 (Hardware Documentation):** Verified delivery of `HARDWARE_BINDING_GUIDE.md`.
18. **AT-18 (Exact Durations):** Asserts Monthly is exactly 30 days from activation; Annual is exactly 365 days.
19. **AT-19 (Online Outage Isolation):** Unexpected outage on `OnlineOnly` license does not trigger 6h grace.
20. **AT-20 (Clock Rollback Defense):** Reversing system time by 2 hours triggers `ERR_CLOCK_TAMPERING`.
21. **AT-21 (Replay Attack Rejection):** Replaying a used `.licresp` reactivation token is rejected.
22. **AT-22 (Admin RBAC):** Non-admin account attempting revocation or key management receives HTTP 403.

---

## 7. Handover Deliverables (HO-01 to HO-11)

* **HO-01:** Full source repository, EF Core migrations, deployment configuration.
* **HO-02:** Build instructions, dependency inventory, and release artifacts.
* **HO-03:** C# SDK source, API documentation, sample requests/responses, runnable demo.
* **HO-04:** Deployment guide: Linux VPS setup, Nginx, HTTPS, systemd services, firewall.
* **HO-05:** Security documentation: ECDSA P-256 format, key rotation, anti-tamper controls.
* **HO-06:** Offline licensing guide: 6h grace, signed file format, clock drift handling.
* **HO-07:** Hardware-binding guide: Motherboard, CPU, SSD querying, fallback methods.
* **HO-08:** Acceptance checklist with executable test results for AT-01 to AT-22.
* **HO-09:** Database backup and restore procedures with verification evidence.
* **HO-10:** Troubleshooting & recovery manual for hardware changes and admin recovery.
* **HO-11:** Agreed post-delivery support and ownership transfer terms.
