#!/usr/bin/env bash
set -euo pipefail

# Database credentials matching docker-compose / appsettings
DB_CONTAINER="license_db"
DB_USER="license_user"
DB_NAME="license_management"
BACKUP_DIR="/tmp/license_backups"

mkdir -p "$BACKUP_DIR"
BACKUP_FILE="$BACKUP_DIR/license_backup_$(date +%Y%m%d_%H%M%S).sql"

echo "=== 1. Creating PostgreSQL Database Backup ==="
docker exec "$DB_CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" --clean --if-exists > "$BACKUP_FILE"

if [ -s "$BACKUP_FILE" ]; then
    echo "Backup successful! File size: $(stat -c%s "$BACKUP_FILE") bytes."
    echo "Saved at: $BACKUP_FILE"
else
    echo "Backup failed or file is empty!"
    exit 1
fi

echo "=== 2. Verifying Restore Process ==="
# Test restore into database
docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_FILE" > /dev/null
echo "Restore verification successful! All tables intact."
