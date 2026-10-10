# HO-08: Security Architecture & Threat Model

**System:** Personal License Management System  
**Threat Analysis Framework:** STRIDE & Defense-in-Depth  
**Assessed Attack Surfaces:** Client Storage, System Clock, Hardware Abstraction, Network Transport, Binary Decompilation

---

## 1. Threat Mitigation Matrix

| Threat Vector | STRIDE Category | Attacker Goal | Architectural Mitigation | Verification Evidence |
| :--- | :--- | :--- | :--- | :--- |
| **1. License Keygenning** | Spoofing | Generating valid unauthorized license files | **ECDSA P-256 Asymmetric Cryptography:** Private key exists strictly on server/admin machine. Keygens are mathematically impossible without the private key. | AT-04 |
| **2. Payload Tampering** | Tampering | Editing `.license` file to alter expiration date or product tier | **RFC 8785 JCS + ECDSA Verification:** Any alteration of even 1 bit in JSON keys or values invalidates the digital signature. | AT-03 |
| **3. Hardware Cloning** | Spoofing | Copying an activated `.license` file to another computer | **3-Component Hardware Binding:** License artifact is bound to salted composite SHA-256 of Motherboard, CPU, and Disk. Hash mismatch forces immediate lock. | AT-06, 07, 08, 09 |
| **4. Clock Rollback Manipulation** | Tampering | Turning back the Windows system clock to prolong expired software | **Temporal Integrity Engine:** Monotonic system uptime ticks (`Environment.TickCount64`) correlated against wall-clock watermarks. Rollbacks >30m force immediate lock. | AT-20 |
| **5. Offline Replay Attack** | Tampering | Reapplying an old `.licresp` file to reactivate unauthorized hardware | **Single-Use Cryptographic Nonces:** Each `.licreq` embeds a 128-bit random nonce. Upon verification, the nonce is burned permanently, blocking replays. | AT-21 |
| **6. Network Interception / Drop** | Elevation of Privilege | Severing internet connection to force infinite grace mode | **Deterministic Grace Precedence:** `OnlineOnly` locks immediately upon network loss. Hardware mismatches and expired licenses NEVER enter grace mode. | AT-05, AT-12, AT-19 |
| **7. Reverse Engineering** | Information Disclosure | Decompiling client assembly to locate bypass switches | **Obfuscar MSBuild Pipeline:** Release assemblies undergo symbol renaming, private API stripping, and string hiding to thwart ILSpy / dotPeek. | Build Pipeline |

---

## 2. In-Depth Mitigation Analyses

### 2.1 Defense Against Clock Rollback (AT-20)
- **Attack Scenario:** A customer with a 30-day monthly license turns back their PC clock to 2020 to prevent expiration.
- **Defense Mechanism:** The client temporal engine records `LastKnownUtcTime` and monitors `Environment.TickCount64` (a monotonic hardware counter that only counts upwards and cannot be set backwards by standard Windows time settings).
- **Outcome:** If wall-clock time moves backwards while monotonic ticks advance, a tampering anomaly is flagged and the application locks immediately.

### 2.2 Defense Against Offline Replay Attacks (AT-21)
- **Attack Scenario:** A user receives a signed `.licresp` file for an approved motherboard upgrade. Six months later, they attempt to re-apply the same file to unlock an unauthorized second machine.
- **Defense Mechanism:** The `.licreq` challenge generates a fresh cryptographic nonce (`RandomNumberGenerator.Fill`). The client tracks consumed nonces in local secure state.
- **Outcome:** Re-submitting an already-consumed nonce fails immediately with `Replay attack detected: Nonce has already been consumed (AT-21)`.

### 2.3 Strict License Policy Precedence (AT-12)
- **Principle:** Grace periods apply **exclusively** to network connectivity outages for `OfflineWithGrace` licenses.
- **Rule Hierarchy:**
  1. *Clock Tampered?* $\rightarrow$ **LOCKED** (Zero grace)
  2. *Signature Invalid?* $\rightarrow$ **LOCKED** (Zero grace)
  3. *Hardware Mismatch?* $\rightarrow$ **LOCKED** (Zero grace)
  4. *Expired?* $\rightarrow$ **LOCKED** (Zero grace)
  5. *Mode == OnlineOnly & Offline?* $\rightarrow$ **LOCKED** (Zero grace)
  6. *Mode == OfflineWithGrace & Offline (< 6 hours)?* $\rightarrow$ **AUTHORIZED IN GRACE**
  7. *Mode == OfflineWithGrace & Offline (>= 6 hours)?* $\rightarrow$ **LOCKED**
