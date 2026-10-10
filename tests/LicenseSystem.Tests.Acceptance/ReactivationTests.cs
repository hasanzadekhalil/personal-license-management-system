using FluentAssertions;
using LicenseSystem.ClientSdk;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Reactivation;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Models;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

public class ReactivationTests
{
    private readonly EcdsaP256SigningService _signingService;
    private readonly EcdsaP256SignatureVerifier _verifier;
    private readonly string _publicKeySpki;

    public ReactivationTests()
    {
        _signingService = new EcdsaP256SigningService();
        _verifier = new EcdsaP256SignatureVerifier();
        _publicKeySpki = _signingService.GetPublicKeySpki();
    }

    private string CreateSignedReactivationResponse(string licenseKey, string newHardwareHash, string nonce, DateTime? validUntil = null)
    {
        var payload = new ReactivationPayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = licenseKey,
            NewHardwareHash = newHardwareHash,
            Nonce = nonce,
            AuthorizedAtUtc = DateTime.UtcNow.ToString("o"),
            ValidUntilUtc = (validUntil ?? DateTime.UtcNow.AddDays(30)).ToString("o")
        };

        var canonicalPayload = CanonicalJsonSerializer.Serialize(payload);
        var signature = _signingService.SignData(canonicalPayload);

        var artifact = new SignedArtifact<ReactivationPayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = signature
        };

        return CanonicalJsonSerializer.Serialize(artifact);
    }

    [Fact]
    public void Reactivation_ShouldSucceedForValidRequestAndResponse()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var handler = new ReactivationHandler(hardware, _verifier, _publicKeySpki);

        var request = handler.GenerateOfflineRequest("LIC-TEST-12345");
        var responseJson = CreateSignedReactivationResponse("LIC-TEST-12345", hardware.GetCompositeHash(), request.Nonce);

        // Act
        var result = handler.ProcessOfflineResponse(responseJson);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Payload.Should().NotBeNull();
        result.Payload!.Nonce.Should().Be(request.Nonce);
    }

    [Fact]
    public void Reactivation_ReplayAttackShouldBeBlocked_AT21()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var handler = new ReactivationHandler(hardware, _verifier, _publicKeySpki);

        var request = handler.GenerateOfflineRequest("LIC-TEST-12345");
        var responseJson = CreateSignedReactivationResponse("LIC-TEST-12345", hardware.GetCompositeHash(), request.Nonce);

        // Step 1: First usage succeeds
        var firstResult = handler.ProcessOfflineResponse(responseJson);
        firstResult.IsSuccess.Should().BeTrue();

        // Step 2: Attacker tries to replay the exact same response file (AT-21)
        var replayResult = handler.ProcessOfflineResponse(responseJson);

        // Assert
        replayResult.IsSuccess.Should().BeFalse();
        replayResult.ErrorMessage.Should().Contain("Replay attack detected");
    }

    [Fact]
    public void Reactivation_TamperedSignatureShouldBeRejected()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var handler = new ReactivationHandler(hardware, _verifier, _publicKeySpki);

        var request = handler.GenerateOfflineRequest("LIC-TEST-12345");
        var responseJson = CreateSignedReactivationResponse("LIC-TEST-12345", hardware.GetCompositeHash(), request.Nonce);

        // Tamper with response JSON
        var tamperedJson = responseJson.Replace(request.Nonce, "attacker-modified-nonce");

        // Act
        var result = handler.ProcessOfflineResponse(tamperedJson);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("signature verification failed");
    }

    [Fact]
    public void Reactivation_HardwareMismatchInResponseShouldBeRejected()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var handler = new ReactivationHandler(hardware, _verifier, _publicKeySpki);

        var request = handler.GenerateOfflineRequest("LIC-TEST-12345");
        var responseJson = CreateSignedReactivationResponse("LIC-TEST-12345", "WRONG-FOREIGN-HARDWARE-HASH", request.Nonce);

        // Act
        var result = handler.ProcessOfflineResponse(responseJson);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not match the current machine");
    }

    [Fact]
    public async Task LicenseClient_EndToEndOfflineReactivationFlow_RestoresAuthorization()
    {
        // Arrange
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();
        var timeProvider = new MockTimeProvider();

        var initialLicensePayload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-PROD-RECOVERY-888",
            ProductCode = "PROD-CLIENT",
            HardwareHash = originalHash,
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var initialArtifact = new SignedArtifact<LicensePayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = initialLicensePayload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(initialLicensePayload))
        };

        var options = new LicenseClientOptions
        {
            ProductCode = "PROD-CLIENT",
            PublicKeySpki = _publicKeySpki,
            StorageDirectory = Path.Combine(Path.GetTempPath(), "LicenseTest_" + Guid.NewGuid().ToString("N"))
        };

        var client = new LicenseClient(options, null, hardware, _verifier, timeProvider);
        client.LoadLicenseDirectly(initialArtifact);

        // Initial check: Active and Authorized
        client.CurrentStatus.IsAuthorized.Should().BeTrue();

        // 1. Hardware change occurs: Motherboard replaced!
        hardware.ReplaceMotherboard("NEW-REPLACED-MAINBOARD-UUID-2026");
        var newHash = hardware.GetCompositeHash();
        newHash.Should().NotBe(originalHash);

        // Status check: Locked due to Hardware Mismatch
        client.LoadLicenseDirectly(initialArtifact);
        client.CurrentStatus.IsAuthorized.Should().BeFalse();
        client.CurrentStatus.Status.Should().Be(LicenseStatus.LockedHardwareMismatch);

        // 2. Generate Offline Reactivation Request
        var offlineRequest = client.GenerateOfflineReactivationRequest("LIC-PROD-RECOVERY-888");
        offlineRequest.NewHardwareHash.Should().Be(newHash);

        // 3. Admin signs the reactivation response (.licresp)
        var responseJson = CreateSignedReactivationResponse("LIC-PROD-RECOVERY-888", newHash, offlineRequest.Nonce);

        // 4. Client imports the signed response
        var importResult = await client.ImportOfflineReactivationResponseAsync(responseJson);

        // Assert: System is re-authorized on new hardware!
        importResult.IsAuthorized.Should().BeTrue();
        client.CurrentStatus.IsAuthorized.Should().BeTrue();
        client.CurrentStatus.Status.Should().Be(LicenseStatus.Active);
    }
}
