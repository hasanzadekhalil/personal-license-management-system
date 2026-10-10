# Personal License Management System

[![Build & Test Status](https://img.shields.io/badge/Tests-48%20Passed%20(100%25)-brightgreen.svg)](docs/ACCEPTANCE_TEST_RESULTS.md)
[![Acceptance Criteria](https://img.shields.io/badge/AT--01%20to%20AT--22-All%20Verified-blue.svg)](docs/ACCEPTANCE_TEST_RESULTS.md)
[![Platform](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![Database](https://img.shields.io/badge/PostgreSQL-16-blue.svg)](https://www.postgresql.org/)
[![Cryptography](https://img.shields.io/badge/Security-ECDSA%20P--256%20%2B%20JCS-darkgreen.svg)](docs/handover/HO-06-CRYPTOGRAPHIC-GUIDE.md)

A private, production-grade, owner-controlled software licensing and hardware-binding management system for Windows desktop applications.

Developed under Clean Architecture with mathematical asymmetric cryptographic guarantees, deterministic 3-component hardware fingerprinting, strict temporal integrity protection against clock rollbacks, dual online/offline reactivation protocols, and an administrative management portal.

---

## 🏛 Architecture & Project Layout

```
├── src/
│   ├── LicenseSystem.Core/             # Pure Domain & Cryptography (ECDSA P-256, RFC 8785 JCS)
│   ├── LicenseSystem.Infrastructure/   # EF Core DbContext, PostgreSQL Migrations, Repositories
│   ├── LicenseSystem.Api/              # Central Licensing REST Web API Service
│   ├── LicenseSystem.Admin/            # Razor Pages Administrative Management Portal
│   ├── LicenseSystem.ClientSdk/        # Desktop Client Licensing SDK (WMI, Policy Engine, Temporal)
│   └── LicenseSystem.WpfDemo/          # Interactive Desktop Client Demo Runner & WPF Views
├── tests/
│   └── LicenseSystem.Tests.Acceptance/ # Acceptance Test Suite (AT-01 to AT-22 Automation)
├── docs/
│   ├── ACCEPTANCE_TEST_RESULTS.md      # Full 22/22 Acceptance Test Verification Evidence
│   ├── BACKUP_RESTORE_EVIDENCE.md      # Automated PostgreSQL Backup & Disaster Recovery Evidence
│   └── handover/                       # Complete Handover Documentation Package (HO-01 to HO-11)
├── docker-compose.yml                  # PostgreSQL 16 Database Container
└── Directory.Build.props               # Global .NET 8 Build & Version Configuration
```

---

## 🚀 Quickstart Guide

### 1. Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Engine & Docker Compose](https://www.docker.com/)

### 2. Start PostgreSQL Database
```bash
docker compose up -d license_db
```
The database starts on `localhost:5432` (database: `license_management`, user: `license_user`).

### 3. Apply Database Migrations
```bash
dotnet ef database update --project src/LicenseSystem.Infrastructure --startup-project src/LicenseSystem.Api
```

### 4. Run Central Licensing Web API
```bash
cd src/LicenseSystem.Api
dotnet run
```
- API Base URL: `http://localhost:5000`
- Swagger Documentation: `http://localhost:5000/swagger`

### 5. Run Admin Management Portal
```bash
cd src/LicenseSystem.Admin
dotnet run
```
- Admin Portal URL: `http://localhost:5001`
- Default Credentials: `admin@licensesystem.local` / `Admin_Secure_Pass_2026!`

### 6. Run Desktop Demo & Security Simulation
```bash
dotnet run --project src/LicenseSystem.WpfDemo
```
Provides an interactive dashboard demonstrating online activation, offline grace countdowns, hardware swaps (motherboard, CPU, SSD), and clock rollback detection.

---

## 🧪 Automated Acceptance Test Suite (AT-01 to AT-22)

Execute the complete master acceptance test suite:

```bash
dotnet test tests/LicenseSystem.Tests.Acceptance/LicenseSystem.Tests.Acceptance.csproj
```

```text
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 2 s - LicenseSystem.Tests.Acceptance.dll (net8.0)
```

| ID | Test Name | Target Layer | Result |
| :--- | :--- | :--- | :---: |
| **AT-01** | Happy Path Activation | Web API / Client SDK | **PASS** |
| **AT-02** | Invalid License Key Rejection (HTTP 404) | Web API | **PASS** |
| **AT-03** | Tampered License File Detection (1-byte check) | Client SDK | **PASS** |
| **AT-04** | Rogue / Untrusted Signature Rejection | Cryptography / SDK | **PASS** |
| **AT-05** | Server Offline (Online-Only Mode Immediate Lock) | Policy Engine | **PASS** |
| **AT-06** | Hardware Change — Motherboard Replacement Lockout | Hardware Engine | **PASS** |
| **AT-07** | Hardware Change — Processor Upgrade Lockout | Hardware Engine | **PASS** |
| **AT-08** | Hardware Change — Primary Storage Drive Lockout | Hardware Engine | **PASS** |
| **AT-09** | Simultaneous Hardware Component Replacement | Hardware Engine | **PASS** |
| **AT-10** | Non-Monitored Component Invariance (RAM/GPU Swap) | Hardware Engine | **PASS** |
| **AT-11** | Authorized 6-Hour Offline Grace Period | Policy Engine | **PASS** |
| **AT-12** | Strict Policy Precedence (No Grace for Hardware Mismatch) | Policy Engine | **PASS** |
| **AT-13** | Administrator Revocation Enforcement (HTTP 403) | Web API / Admin | **PASS** |
| **AT-14** | Concurrency Seat Limits Enforcement (HTTP 409) | Web API | **PASS** |
| **AT-15** | Database Backup & Restore Automated Execution | Infrastructure | **PASS** |
| **AT-16** | SQL Injection Resistance via EF Core Parameterization | Web API / Database | **PASS** |
| **AT-17** | Audit Log Integrity (Non-Sensitive Data Recording) | Infrastructure | **PASS** |
| **AT-18** | License Duration (Monthly = Exactly 30 Days) | Core / Web API | **PASS** |
| **AT-19** | Network Drop Recovery / OnlineOnly Lock | Policy Engine | **PASS** |
| **AT-20** | Anti-Clock Rollback & Temporal Anomaly Detection | Temporal Engine | **PASS** |
| **AT-21** | Replay Attack Defense on Offline Reactivation | Reactivation Handler | **PASS** |
| **AT-22** | Non-Admin Standard User UAC Resilient Fallback | Hardware Engine | **PASS** |

See [docs/ACCEPTANCE_TEST_RESULTS.md](docs/ACCEPTANCE_TEST_RESULTS.md) for full execution evidence.

---

## 📦 Complete Handover Documentation Package (HO-01 to HO-11)

- **[HO-01: Source Code Repository & Inventory](docs/handover/HO-01-SOURCE-INVENTORY.md)** — Solution mapping and project dependencies.
- **[HO-02: Deployment & Operations Guide](docs/handover/HO-02-DEPLOYMENT-GUIDE.md)** — Docker setup, environment variables, Nginx SSL reverse proxy.
- **[HO-03: Administrator Operations Manual](docs/handover/HO-03-ADMINISTRATOR-MANUAL.md)** — Product registration, license issuance, revocation, and offline signing.
- **[HO-04: Client SDK Integration Guide](docs/handover/HO-04-CLIENT-SDK-GUIDE.md)** — Adding SDK to desktop applications, events, and Obfuscar build pipeline.
- **[HO-05: REST API Specification](docs/handover/HO-05-REST-API-SPECIFICATION.md)** — Complete OpenAPI/Swagger schema documentation of endpoints.
- **[HO-06: Cryptographic Architecture & Key Management](docs/handover/HO-06-CRYPTOGRAPHIC-GUIDE.md)** — ECDSA P-256 + SHA-256, RFC 8785 JCS, key generation & rotation.
- **[HO-07: Hardware Fingerprinting Specification](docs/handover/HO-07-HARDWARE-FINGERPRINTING.md)** — 3-component formula, standard user UAC fallback, VM policies.
- **[HO-08: Security Architecture & Threat Model](docs/handover/HO-08-SECURITY-THREAT-MODEL.md)** — STRIDE threat analysis covering 7 key attack vectors and mitigations.
- **[HO-09: Database Backup & Disaster Recovery Guide](docs/handover/HO-09-BACKUP-DISASTER-RECOVERY.md)** — `backup_restore.sh`, automated cron scripts, and disaster recovery.
- **[HO-10: Acceptance Test Verification Report](docs/handover/HO-10-ACCEPTANCE-TEST-REPORT.md)** — Formal quality assurance sign-off covering all 22 acceptance tests.
- **[HO-11: System Architecture & Design Specification](docs/handover/HO-11-SYSTEM-ARCHITECTURE.md)** — Clean Architecture domain diagram, database ERD, and data flows.

---

## 🔒 Security Highlights

1. **Strict Asymmetric Isolation:** The server private signing key is physically absent from client desktop binaries. Client apps embed only the SPKI public verification key.
2. **Deterministic RFC 8785 JCS:** Protects digital signatures against property-ordering ambiguities across heterogeneous runtimes.
3. **Multi-Layer Clock Rollback Defense:** Combines monotonic hardware uptime (`Environment.TickCount64`) with encrypted UTC watermarks to detect clock tampering.
4. **Single-Use Replay Nonces:** Offline reactivation challenge files (`.licreq`) embed single-use nonces that are invalidated permanently upon approval, defeating replay attacks.
5. **No Elevating UAC Prompts:** Hardware fingerprinting extracts system drive volume serials when physical SMART IOCTLs are restricted, operating seamlessly under standard Windows user accounts.
