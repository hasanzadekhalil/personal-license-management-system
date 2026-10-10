using FluentAssertions;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Policy;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Models;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

public class PolicyEngineTests
{
    private readonly EcdsaP256SigningService _signingService;
    private readonly EcdsaP256SignatureVerifier _verifier;
    private readonly string _publicKeySpki;

    public PolicyEngineTests()
    {
        _signingService = new EcdsaP256SigningService();
        _verifier = new EcdsaP256SignatureVerifier();
        _publicKeySpki = _signingService.GetPublicKeySpki();
    }

    private SignedArtifact<LicensePayload> CreateSignedArtifact(
        string productCode = "PROD-ALPHA",
        string hardwareHash = "MOCK-HW-HASH-1234567890",
        string mode = "OfflineWithGrace",
        int graceHours = 6,
        DateTime? activatedAt = null,
        DateTime? expiresAt = null)
    {
        var activated = activatedAt ?? DateTime.UtcNow.AddHours(-1);
        var expires = expiresAt ?? DateTime.UtcNow.AddDays(30);

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(),
            ProductCode = productCode,
            HardwareHash = hardwareHash,
            LicenseMode = mode,
            GracePeriodHours = graceHours,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-1).ToString("o"),
            ActivatedAtUtc = activated.ToString("o"),
            ExpiresAtUtc = expires.ToString("o")
        };

        var canonicalJson = CanonicalJsonSerializer.Serialize(payload);
        var signature = _signingService.SignData(canonicalJson);

        return new SignedArtifact<LicensePayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = signature
        };
    }

    [Fact]
    public void TamperedSignature_ShouldLockLicense_AT03()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var timeProvider = new MockTimeProvider();
        var artifact = CreateSignedArtifact(hardwareHash: hardware.GetCompositeHash());

        // Tamper with the signature
        var tamperedArtifact = new SignedArtifact<LicensePayload>
        {
            Header = artifact.Header,
            Payload = artifact.Payload,
            Signature = artifact.Signature.Substring(0, artifact.Signature.Length - 4) + "AAAA"
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki);

        // Act
        var result = engine.Evaluate(tamperedArtifact, "PROD-ALPHA");

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.Revoked);
        result.Message.Should().Contain("Cryptographic signature verification failed");
    }

    [Fact]
    public void HardwareMismatch_ShouldNeverReceiveGrace_AT12()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var timeProvider = new MockTimeProvider();
        var artifact = CreateSignedArtifact(
            hardwareHash: hardware.GetCompositeHash(),
            mode: "OfflineWithGrace",
            graceHours: 6);

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki);

        // Simulate motherboard replacement (AT-06)
        hardware.ReplaceMotherboard("NEW-ROG-MAXIMUS-UUID");

        // Act - even if offline
        var result = engine.Evaluate(artifact, "PROD-ALPHA", isOnlineAvailable: false);

        // Assert (AT-12: Hardware mismatch MUST NOT fall back into grace mode)
        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.LockedHardwareMismatch);
        result.RemainingGracePeriod.Should().BeNull();
        result.Message.Should().Contain("Hardware mismatch detected");
    }

    [Fact]
    public void ExpiredLicense_ShouldNeverReceiveGrace_AT18()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var timeProvider = new MockTimeProvider();
        var artifact = CreateSignedArtifact(
            hardwareHash: hardware.GetCompositeHash(),
            mode: "OfflineWithGrace",
            expiresAt: DateTime.UtcNow.AddMinutes(-5)); // Expired 5 mins ago

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki);

        // Act - even if offline
        var result = engine.Evaluate(artifact, "PROD-ALPHA", isOnlineAvailable: false);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.Expired);
        result.RemainingGracePeriod.Should().BeNull();
    }

    [Fact]
    public void OnlineOnlyMode_WhenValidationFails_ShouldLockImmediatelyWithoutGrace_AT05_AT19()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var timeProvider = new MockTimeProvider();
        var artifact = CreateSignedArtifact(
            hardwareHash: hardware.GetCompositeHash(),
            mode: "OnlineOnly");

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki);

        // Act - network failure
        var result = engine.Evaluate(artifact, "PROD-ALPHA", isOnlineAvailable: false, serverValidationSucceeded: false);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RemainingGracePeriod.Should().BeNull();
        result.Message.Should().Contain("Immediate lock enforced");
    }

    [Fact]
    public void OfflineWithGrace_Within6Hours_ShouldAuthorizeWithRemainingGrace_AT11()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var startTime = DateTime.UtcNow;
        var timeProvider = new MockTimeProvider(startTime);
        var artifact = CreateSignedArtifact(
            hardwareHash: hardware.GetCompositeHash(),
            mode: "OfflineWithGrace",
            graceHours: 6,
            activatedAt: startTime);

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki, startTime);
        engine.RecordSuccessfulOnlineSync(startTime);

        // Advance time by 3 hours offline
        timeProvider.AdvanceTime(TimeSpan.FromHours(3));

        // Act
        var result = engine.Evaluate(artifact, "PROD-ALPHA", isOnlineAvailable: false, serverValidationSucceeded: false);

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.RemainingGracePeriod.Should().NotBeNull();
        result.RemainingGracePeriod!.Value.TotalHours.Should().BeApproximately(3.0, 0.1);
    }

    [Fact]
    public void OfflineWithGrace_After6Hours_ShouldLockGraceExpired()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var startTime = DateTime.UtcNow;
        var timeProvider = new MockTimeProvider(startTime);
        var artifact = CreateSignedArtifact(
            hardwareHash: hardware.GetCompositeHash(),
            mode: "OfflineWithGrace",
            graceHours: 6,
            activatedAt: startTime);

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki, startTime);
        engine.RecordSuccessfulOnlineSync(startTime);

        // Advance time by 7 hours offline (> 6 hours)
        timeProvider.AdvanceTime(TimeSpan.FromHours(7));

        // Act
        var result = engine.Evaluate(artifact, "PROD-ALPHA", isOnlineAvailable: false, serverValidationSucceeded: false);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.LockedGraceExpired);
    }

    [Fact]
    public void ClockRollback_ShouldBeDetectedAndLockLicense_AT20()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var baseTime = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new MockTimeProvider(baseTime);
        var artifact = CreateSignedArtifact(hardwareHash: hardware.GetCompositeHash());

        var engine = new LicensePolicyEngine(_verifier, hardware, timeProvider, _publicKeySpki, baseTime);

        // Step 1: Normal initial check
        var firstResult = engine.Evaluate(artifact, "PROD-ALPHA");
        firstResult.IsAuthorized.Should().BeTrue();

        // Step 2: Malicious user rolls back system clock by 3 days
        timeProvider.RollbackTime(TimeSpan.FromDays(3));

        // Act
        var rollbackResult = engine.Evaluate(artifact, "PROD-ALPHA");

        // Assert (AT-20)
        rollbackResult.IsAuthorized.Should().BeFalse();
        rollbackResult.Status.Should().Be(LicenseStatus.LockedClockTampered);
        rollbackResult.Message.Should().Contain("clock rollback");
    }
}
