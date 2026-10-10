# HO-02: Deployment & Infrastructure Operations Guide

**System:** Personal License Management System  
**Version:** 1.0.0 Production Release  
**Platform:** Ubuntu 22.04+ / Debian 12 / Windows Server 2022 / Docker Compose

---

## 1. System Requirements

- **Runtime:** .NET 8.0 SDK / ASP.NET Core Runtime 8.0
- **Container Engine:** Docker 24.0+ & Docker Compose v2.20+
- **Database:** PostgreSQL 16 (runs via official container or managed RDS/Cloud SQL)
- **Memory:** 1 GB RAM minimum (2 GB recommended)
- **Disk Space:** 5 GB free space for database, logs, and backups

---

## 2. Quickstart with Docker Compose

The simplest way to stand up the complete licensing backend is via the provided `docker-compose.yml`:

```bash
# 1. Clone the repository and enter the directory
git clone https://github.com/owner/personal-license-system.git
cd personal-license-system

# 2. Launch the PostgreSQL database container
docker compose up -d license_db

# 3. Verify PostgreSQL is healthy
docker compose ps
```

The database container exposes port `5432` with credentials:
- **Host:** `localhost`
- **Port:** `5432`
- **Database:** `license_management`
- **User:** `license_user`
- **Password:** Configured via `POSTGRES_PASSWORD` in `docker-compose.yml`

---

## 3. Database Migration Execution

Database schemas and initial administrator accounts are applied automatically using Entity Framework Core migrations:

```bash
# Apply EF Core migrations to PostgreSQL
dotnet ef database update --project src/LicenseSystem.Infrastructure --startup-project src/LicenseSystem.Api
```

---

## 4. Running the Web API Service

```bash
cd src/LicenseSystem.Api
dotnet run --configuration Release
```
By default, the API starts listening on:
- **HTTP:** `http://0.0.0.0:5000`
- **Swagger Documentation:** `http://localhost:5000/swagger`

---

## 5. Running the Razor Pages Management Portal

```bash
cd src/LicenseSystem.Admin
dotnet run --configuration Release
```
The Admin Portal starts listening on:
- **URL:** `http://localhost:5001`
- **Default Admin Account:** `admin@licensesystem.local` (Password: `Admin_Secure_Pass_2026!`)

---

## 6. Reverse Proxy Configuration (Nginx / HTTPS SSL)

In production, expose the API and Admin portal behind Nginx with Let's Encrypt SSL certificates:

```nginx
# /etc/nginx/sites-available/licensing.yourdomain.com
server {
    listen 80;
    server_name licensing.yourdomain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name licensing.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/licensing.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/licensing.yourdomain.com/privkey.pem;

    # Forward to Central Licensing API
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Forward to Admin Management Portal
    location / {
        proxy_pass http://127.0.0.1:5001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 7. Production Hardening Checklist

- [x] Rotate PostgreSQL database passwords in `docker-compose.yml` and `appsettings.json`.
- [x] Generate production ECDSA P-256 private key and store securely outside public source code.
- [x] Configure daily automated database backups using `src/LicenseSystem.Infrastructure/Scripts/backup_restore.sh`.
- [x] Restrict Admin Portal access (`http://localhost:5001`) via firewall or internal VPN.
