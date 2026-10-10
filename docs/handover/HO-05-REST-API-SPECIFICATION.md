# HO-05: REST API Specification

**Service:** Central Licensing Web API (`LicenseSystem.Api`)  
**Base URL:** `https://licensing.yourdomain.com`  
**Content-Type:** `application/json`  
**Authentication:** Public Endpoints (Client Hardware Signed Verification) / Admin Portal Session Cookie

---

## 1. Endpoints Overview

| Method | Path | Description | Typical Response |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/license/activate` | Activate license on current machine | `200 OK` (Signed License JSON) |
| `POST` | `/api/v1/license/validate` | Periodic validation of active license | `200 OK` / `403 Forbidden` |
| `POST` | `/api/v1/license/heartbeat` | Keepalive check for device seat | `200 OK` (`isAcknowledged: true`) |
| `GET` | `/api/v1/license/public-key` | Retrieve server SPKI public key | `200 OK` (`publicKeySpki: "..."`) |
| `POST` | `/api/v1/reactivate/request` | Submit online reactivation request | `200 OK` (`status: "Pending"`) |
| `POST` | `/api/v1/reactivate/approve` | Approve reactivation & issue signed payload | `200 OK` (Signed Reactivation JSON) |

---

## 2. Endpoint Details

### 2.1 Activate License
**`POST /api/v1/license/activate`**

Binds a license to the requesting hardware and returns an ECDSA P-256 signed license artifact.

#### Request Body
```json
{
  "productCode": "CAD-STUDIO-PRO",
  "licenseKey": "LIC-9B7C1A2F-8E3D4A5B",
  "hardwareHash": "e2652b0bca8cc353e1bc9d08ef08186e51282fd949da90caea8664af2a87c197",
  "motherboardId": "MB-ROG-MAXIMUS-Z790",
  "cpuId": "BFEBFBFF00090672",
  "primaryDiskId": "SAMSUNG-MZVL21T0HCLR-00B00",
  "deviceName": "DEVELOPER-WORKSTATION",
  "isVirtualMachine": false
}
```

#### Responses
- **`200 OK`**: Successfully activated.
  ```json
  {
    "isSuccess": true,
    "signedLicenseJson": "{\"header\":{\"version\":\"1.0\",\"algorithm\":\"ES256\"},\"payload\":{...},\"signature\":\"...\"}"
  }
  ```
- **`400 Bad Request`**: Missing required parameters.
- **`403 Forbidden`**: License is revoked or expired.
- **`404 Not Found`**: License key or product code does not exist.
- **`409 Conflict`**: Maximum allowed seats (`MaxActivations`) already reached by other machines.

---

### 2.2 Validate License
**`POST /api/v1/license/validate`**

Performs online verification that the license is still active and assigned to this hardware hash.

#### Request Body
```json
{
  "licenseKey": "LIC-9B7C1A2F-8E3D4A5B",
  "productCode": "CAD-STUDIO-PRO",
  "hardwareHash": "e2652b0bca8cc353e1bc9d08ef08186e51282fd949da90caea8664af2a87c197"
}
```

#### Responses
- **`200 OK`**: License is valid and active.
  ```json
  {
    "isValid": true,
    "status": "Active",
    "expiresAtUtc": "2026-11-10T12:00:00Z"
  }
  ```
- **`403 Forbidden`**: License has been revoked by administrator.
- **`200 OK` with `status: "HardwareMismatch"`**: Hardware does not match active binding.

---

### 2.3 Send Heartbeat
**`POST /api/v1/license/heartbeat`**

Updates the `last_heartbeat_utc` timestamp for the active device seat.

#### Request Body
```json
{
  "licenseKey": "LIC-9B7C1A2F-8E3D4A5B",
  "hardwareHash": "e2652b0bca8cc353e1bc9d08ef08186e51282fd949da90caea8664af2a87c197"
}
```

#### Responses
- **`200 OK`**:
  ```json
  {
    "isAcknowledged": true,
    "isRevoked": false
  }
  ```

---

### 2.4 Retrieve Server Public Key
**`GET /api/v1/license/public-key`**

Returns the server's SPKI public key (ECDSA P-256) for bootstrapping or client configuration verification.

#### Responses
- **`200 OK`**:
  ```json
  {
    "algorithm": "ECDSA_P256_SHA256",
    "publicKeySpki": "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE..."
  }
  ```

---

### 2.5 Submit Online Reactivation Request
**`POST /api/v1/reactivate/request`**

Generates a pending reactivation request when a customer changes hardware.

#### Request Body
```json
{
  "productCode": "CAD-STUDIO-PRO",
  "licenseKey": "LIC-9B7C1A2F-8E3D4A5B",
  "newHardwareHash": "f1839c09aa8cc353e1bc9d08ef08186e51282fd949da90caea8664af2a87b208",
  "nonce": "c4d87216a9ef451e892c5789a0b3f812"
}
```

#### Responses
- **`200 OK`**: Request enqueued for administrator review.
  ```json
  {
    "isSuccess": true,
    "nonce": "c4d87216a9ef451e892c5789a0b3f812",
    "status": "Pending"
  }
  ```
