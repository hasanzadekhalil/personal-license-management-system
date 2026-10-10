# HO-09: Database Backup & Disaster Recovery Guide

**Database Engine:** PostgreSQL 16  
**Automation Script:** `src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh`  
**Evidence Document:** `docs/BACKUP_RESTORE_EVIDENCE.md`

---

## 1. Automated Script Overview

The repository provides an automated backup and restore verification script located at:
`src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh`

### What the Script Performs:
1. Connects to the active PostgreSQL container (`license_db`).
2. Creates an atomic SQL dump using `pg_dump` with `--clean --if-exists` flags.
3. Validates that the resulting archive is non-empty and stores it in `/tmp/license_backups/`.
4. Executes a non-destructive restore test using `psql` to verify archive integrity.

---

## 2. Live Verification Evidence

Execution against the active PostgreSQL container confirmed 100% operational success:

```text
=== 1. Creating PostgreSQL Database Backup ===
Backup successful! File size: 48420 bytes.
Saved at: /tmp/license_backups/license_backup_20261010_123905.sql
=== 2. Verifying Restore Process ===
Restore verification successful! All tables intact.
```

---

## 3. Disaster Recovery Runbook

If the primary database server suffers a catastrophic hardware failure:

### Step 1: Provision Fresh PostgreSQL Container or Instance
```bash
docker run -d \
  --name license_db_recovery \
  -e POSTGRES_DB=license_management \
  -e POSTGRES_USER=license_user \
  -e POSTGRES_PASSWORD=license_secure_pass_2026 \
  -p 5432:5432 \
  postgres:16-alpine
```

### Step 2: Restore Latest Backup Snapshot
```bash
docker exec -i license_db_recovery psql -U license_user -d license_management < /path/to/latest_backup.sql
```

### Step 3: Verify Domain Integrity
```bash
docker exec -it license_db_recovery psql -U license_user -d license_management -c "SELECT count(*) FROM licenses;"
```

---

## 4. Production Cron Retention Policy

Add the following crontab entries on the host machine to execute nightly backups at 02:00 UTC and automatically prune backups older than 30 days:

```cron
# Nightly database backup at 2:00 AM UTC
0 2 * * * /home/projects/PERSONAL\ LICENSE\ MANAGEMENT\ SYSTEM/src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh >> /var/log/license_backup.log 2>&1

# Prune snapshots older than 30 days
0 3 * * * find /tmp/license_backups -type f -name "*.sql" -mtime +30 -delete
```
