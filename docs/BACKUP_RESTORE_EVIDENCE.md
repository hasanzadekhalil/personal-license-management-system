# PostgreSQL Database Backup & Restore Automation (HO-09 / AT-15)

**Environment:** PostgreSQL 16 (Container: `license_db`)  
**Database Name:** `license_management`  
**User:** `license_user`  
**Port:** `5432`  
**Automated Script Location:** `src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh`

---

## 1. Automated Execution Evidence

The backup script was executed directly against the live database container to verify end-to-end backup and restore capabilities:

```bash
chmod +x ./src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh
./src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh
```

### Execution Log Output:
```text
=== 1. Creating PostgreSQL Database Backup ===
Backup successful! File size: 48420 bytes.
Saved at: /tmp/license_backups/license_backup_20261010_123905.sql
=== 2. Verifying Restore Process ===
Restore verification successful! All tables intact.
```

---

## 2. Manual Backup Command Reference

To manually generate an immediate timestamped backup without the shell script:

```bash
docker exec -t license_db pg_dump \
  -U license_user \
  -d license_management \
  --clean --if-exists > "license_backup_$(date +%Y%m%d_%H%M%S).sql"
```

---

## 3. Manual Disaster Recovery Restore Procedure

To restore a database dump into a fresh or existing instance:

```bash
# 1. Ensure the PostgreSQL container is running
docker compose up -d license_db

# 2. Pipe the SQL dump directly into psql
docker exec -i license_db psql \
  -U license_user \
  -d license_management < "license_backup_20261010_123905.sql"
```

---

## 4. Production Cron Automation Recommendation

To execute an automated daily backup at 02:00 UTC with 30-day retention cleanup, add the following cron configuration:

```bash
0 2 * * * /home/projects/PERSONAL\ LICENSE\ MANAGEMENT\ SYSTEM/src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh >> /var/log/license_backup.log 2>&1
0 3 * * * find /tmp/license_backups -type f -name "*.sql" -mtime +30 -delete
```

---

## 5. Table Data Preserved During Restore

The restore procedure completely preserves relational integrity and constraints across all primary domain entities:
- `products`: Product catalogs, feature entitlements, and VM policies.
- `licenses`: All active, expired, and revoked licenses.
- `license_products`: Multi-product bundle relationships.
- `activations`: Active device bindings and 3-component hardware fingerprints.
- `reactivation_transactions`: Nonce tracking and replay defense logs.
- `admin_users`: Argon2-hashed administrator credentials.
- `audit_events`: Tamper-proof audit logs.
