# HO-06: Cryptographic Architecture & Key Management Guide

**Algorithm:** Elliptic Curve Digital Signature Algorithm (ECDSA)  
**Curve:** NIST P-256 (`secp256r1` / `prime256v1`)  
**Hash Algorithm:** SHA-256  
**Signature Format:** RFC 3279 ASN.1 DER Sequence (`r`, `s` integers)  
**Serialization Standard:** RFC 8785 JSON Canonicalization Scheme (JCS)

---

## 1. Architectural Philosophy: Strict Asymmetric Isolation

The licensing system enforces a strict asymmetric cryptographic boundary:
- **Server / Admin Portal:** Holds the **ECDSA P-256 Private Key** (PKCS#8). This key is used exclusively to generate digital signatures on license payloads and reactivation receipts.
- **Client Desktop Applications:** Embeds ONLY the **SPKI Public Key** (`SubjectPublicKeyInfo` Base64).
- **Security Invariant:** Even if an attacker has full administrative access to the client machine, decompiles the desktop binary, dumps memory, or modifies client code, they **cannot** forge valid license signatures for other machines because the private key is physically absent from the client.

---

## 2. RFC 8785 Canonical JSON Serialization (JCS)

Digital signatures are computed over raw bytes. In typical JSON serialization, property order, whitespace, and formatting vary between platforms and runtimes, which would invalidate signatures.

To achieve 100% deterministic signatures across .NET, Linux, and Windows, the system implements RFC 8785:
1. **Alphabetical Property Sorting:** Keys are sorted lexicographically by Unicode code point values.
2. **Whitespace Stripping:** No whitespace outside string literals (minified).
3. **Escaping Consistency:** Uniform character escape rules.

### Canonical Example:
```json
{"activated_at":"2026-10-10T12:00:00.0000000Z","expires_at":"2026-11-09T12:00:00.0000000Z","grace_period_hours":6,"hardware_hash":"e2652b0bca8cc353e1bc9d08ef08186e51282fd949da90caea8664af2a87c197","issued_at":"2026-10-09T12:00:00.0000000Z","license_id":"4fae23b8-39dc-4f40-84a8-48b64a2cb586","license_key":"LIC-SAMPLE-1234","license_mode":"OfflineWithGrace","product_code":"CAD-STUDIO-PRO"}
```

---

## 3. Generating Production ECDSA P-256 Keypairs

Production keys should be generated using standard OpenSSL tools:

```bash
# 1. Generate P-256 Elliptic Curve Private Key in PEM format
openssl ecparam -name prime256v1 -genkey -noout -out license_private_key.pem

# 2. Extract Public Key in SPKI PEM format
openssl ec -in license_private_key.pem -pubout -out license_public_key.pem

# 3. View Public Key Base64 string for embedding in Client SDK
cat license_public_key.pem | grep -v -- "-----" | tr -d '\n'
```

---

## 4. Key Storage & Environment Configuration

### Server Configuration (`appsettings.json` or Environment Variable):
```json
{
  "LicenseSecurity": {
    "PrivateKeyPem": "-----BEGIN EC PRIVATE KEY-----\n...\n-----END EC PRIVATE KEY-----"
  }
}
```
Or via Linux environment variable:
```bash
export LicenseSecurity__PrivateKeyPem="$(cat /etc/license_system/keys/private_key.pem)"
```

### Client Embedding (`LicenseClientOptions`):
```csharp
options.PublicKeySpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE4...";
```

---

## 5. Key Rotation Procedure

When rotating the cryptographic keypair:
1. Generate a new ECDSA P-256 keypair (`Key_V2`).
2. Configure the server with `Key_V2` as the primary signing key while retaining `Key_V1` in the verification chain.
3. Update desktop application releases to trust `Key_V2` (and optionally `Key_V1` for legacy licenses).
4. Newly issued and reactivated licenses are signed automatically using `Key_V2`.
