using System.Globalization;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;

namespace LicenseSystem.ClientSdk.Policy;

public class LicensePolicyEngine
{
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly IHardwareFingerprintProvider _hardwareProvider;
    private readonly ITimeProvider _timeProvider;
    private readonly ClockRollbackDetector _clockRollbackDetector;
    private readonly string _publicKeySpki;

    private DateTime? _lastSuccessfulOnlineSyncUtc;

    public LicensePolicyEngine(
        ISignatureVerifier signatureVerifier,
        IHardwareFingerprintProvider hardwareProvider,
        ITimeProvider timeProvider,
        string publicKeySpki,
        DateTime? initialClockTime = null)
    {
        _signatureVerifier = signatureVerifier;
        _hardwareProvider = hardwareProvider;
        _timeProvider = timeProvider;
        _publicKeySpki = publicKeySpki;
        _clockRollbackDetector = new ClockRollbackDetector(initialClockTime ?? timeProvider.UtcNow);
    }

    public void RecordSuccessfulOnlineSync(DateTime syncTimeUtc)
    {
        _lastSuccessfulOnlineSyncUtc = syncTimeUtc;
    }

    public PolicyEvaluationResult Evaluate(
        SignedArtifact<LicensePayload> signedArtifact,
        string expectedProductCode,
        bool isOnlineAvailable = true,
        bool serverValidationSucceeded = true,
        SignedArtifact<ReactivationPayload>? reactivationArtifact = null)
    {
        var now = _timeProvider.UtcNow;
        var monotonicTicks = _timeProvider.MonotonicTicks;

        // 1. Clock Rollback Detection (AT-20)
        var isClockValid = _clockRollbackDetector.CheckAndRecordTime(now, monotonicTicks);
        if (!isClockValid)
        {
            return PolicyEvaluationResult.Locked(
                LicenseStatus.LockedClockTampered,
                "System clock rollback or temporal anomaly detected.");
        }

        // 2. Cryptographic Verification (AT-03, AT-04)
        if (signedArtifact == null || signedArtifact.Payload == null || string.IsNullOrWhiteSpace(signedArtifact.Signature))
        {
            return PolicyEvaluationResult.Locked(
                LicenseStatus.Revoked,
                "License artifact is missing or malformed.");
        }

        var canonicalPayload = CanonicalJsonSerializer.Serialize(signedArtifact.Payload);
        var isSignatureValid = _signatureVerifier.VerifySignature(canonicalPayload, signedArtifact.Signature, _publicKeySpki);
        if (!isSignatureValid)
        {
            return PolicyEvaluationResult.Locked(
                LicenseStatus.Revoked,
                "Cryptographic signature verification failed. License payload has been altered or signed with an untrusted key.");
        }

        var payload = signedArtifact.Payload;

        // 3. Product Entitlement Check
        if (!string.Equals(payload.ProductCode, expectedProductCode, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyEvaluationResult.Locked(
                LicenseStatus.Revoked,
                $"License product mismatch: expected '{expectedProductCode}', but license is issued for '{payload.ProductCode}'.");
        }

        // 4. Hardware Binding Check (AT-06, AT-07, AT-08, AT-09, AT-12)
        var currentHardwareHash = _hardwareProvider.GetCompositeHash();
        bool isHardwareAuthorized = string.Equals(payload.HardwareHash, currentHardwareHash, StringComparison.OrdinalIgnoreCase);

        if (!isHardwareAuthorized)
        {
            // Check if there is an authorized signed reactivation artifact for this machine
            if (reactivationArtifact != null && reactivationArtifact.Payload != null && !string.IsNullOrWhiteSpace(reactivationArtifact.Signature))
            {
                var canonicalReactivation = CanonicalJsonSerializer.Serialize(reactivationArtifact.Payload);
                var isReactivationSignatureValid = _signatureVerifier.VerifySignature(canonicalReactivation, reactivationArtifact.Signature, _publicKeySpki);

                if (isReactivationSignatureValid &&
                    string.Equals(reactivationArtifact.Payload.LicenseKey, payload.LicenseKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reactivationArtifact.Payload.NewHardwareHash, currentHardwareHash, StringComparison.OrdinalIgnoreCase))
                {
                    isHardwareAuthorized = true;
                }
            }
        }

        if (!isHardwareAuthorized)
        {
            // CRITICAL POLICY PRECEDENCE (AT-12): Hardware mismatch NEVER receives grace!
            return PolicyEvaluationResult.Locked(
                LicenseStatus.LockedHardwareMismatch,
                "Hardware mismatch detected. Application is locked until administrator authorization.");
        }

        // 5. Expiration Check (AT-03, AT-18)
        if (!DateTimeOffset.TryParse(payload.ExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresOffset))
        {
            return PolicyEvaluationResult.Locked(
                LicenseStatus.Expired,
                "Invalid expiration timestamp format in license artifact.");
        }

        var expiresAtUtc = expiresOffset.UtcDateTime;
        if (now >= expiresAtUtc)
        {
            // Expiration NEVER receives grace!
            return PolicyEvaluationResult.Locked(
                LicenseStatus.Expired,
                $"License expired on {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC.");
        }

        // 6. License Mode Enforcement (AT-05, AT-11, AT-19)
        var mode = payload.LicenseMode;

        if (string.Equals(mode, LicenseMode.OnlineOnly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            // Online-Only Mode (AT-05, AT-19):
            // When online validation is required and fails, immediately lock. No grace period!
            if (!isOnlineAvailable || !serverValidationSucceeded)
            {
                return PolicyEvaluationResult.Locked(
                    LicenseStatus.Active,
                    "Online validation is required for this license and failed. Immediate lock enforced.");
            }

            return PolicyEvaluationResult.Authorized(LicenseStatus.Active, "Online license validated successfully.");
        }

        if (string.Equals(mode, LicenseMode.OfflineWithGrace.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            // Offline-With-Grace Mode (AT-11):
            if (isOnlineAvailable && serverValidationSucceeded)
            {
                _lastSuccessfulOnlineSyncUtc = now;
                return PolicyEvaluationResult.Authorized(LicenseStatus.Active, "License validated online successfully.");
            }

            // Connection lost / offline
            var lastSync = _lastSuccessfulOnlineSyncUtc ?? (DateTimeOffset.TryParse(payload.ActivatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var actOffset) ? actOffset.UtcDateTime : DateTime.UtcNow);
            var offlineDuration = now - lastSync;
            var maxGrace = TimeSpan.FromHours(payload.GracePeriodHours > 0 ? payload.GracePeriodHours : 6);

            if (offlineDuration < maxGrace)
            {
                var remaining = maxGrace - offlineDuration;
                return PolicyEvaluationResult.Grace(remaining, $"Operating under authorized {payload.GracePeriodHours}-hour offline grace period. Time remaining: {remaining.TotalHours:F1}h.");
            }

            return PolicyEvaluationResult.Locked(
                LicenseStatus.LockedGraceExpired,
                $"Authorized {payload.GracePeriodHours}-hour offline grace period has expired. Please connect to the internet to reactivate.");
        }

        if (string.Equals(mode, LicenseMode.AirGapped.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            // Air-Gapped Mode: Fully offline authorized until expiry
            return PolicyEvaluationResult.Authorized(LicenseStatus.Active, "Air-gapped signed license authorized.");
        }

        return PolicyEvaluationResult.Authorized(LicenseStatus.Active);
    }
}
