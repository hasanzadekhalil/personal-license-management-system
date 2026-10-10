using System.Text.Json;
using FluentAssertions;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Models;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

public class CryptographyTests
{
    [Fact]
    public void CanonicalJsonSerializer_ShouldProduceDeterministicSortedJson()
    {
        // Arrange
        var payload = new LicensePayload
        {
            LicenseId = "id-123",
            LicenseKey = "KEY-AAA",
            ProductCode = "PROD-1",
            LicenseMode = "OfflineWithGrace",
            HardwareHash = "hash-xyz",
            IssuedAtUtc = "2026-10-10T12:00:00Z",
            ActivatedAtUtc = "2026-10-10T12:05:00Z",
            ExpiresAtUtc = "2026-11-09T12:05:00Z",
            GracePeriodHours = 6
        };

        // Act
        var json1 = CanonicalJsonSerializer.Serialize(payload);
        var json2 = CanonicalJsonSerializer.Serialize(payload);

        // Assert
        json1.Should().Be(json2);
        json1.Should().NotContain(" ");
        json1.Should().StartWith("{\"activated_at\":"); // 'activated_at' comes before 'expires_at' etc alphabetically
    }

    [Fact]
    public void EcdsaP256_SignAndVerify_ShouldSucceedForValidData()
    {
        // Arrange
        using var signingService = new EcdsaP256SigningService();
        var publicKeySpki = signingService.GetPublicKeySpki();
        var verifier = new EcdsaP256SignatureVerifier();

        var dataToSign = "{\"license_id\":\"test-1\",\"product_code\":\"PROD-01\"}";

        // Act
        var signature = signingService.SignData(dataToSign);
        var isValid = verifier.VerifySignature(dataToSign, signature, publicKeySpki);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        isValid.Should().BeTrue();
    }

    [Fact]
    public void EcdsaP256_Verify_ShouldFailWhenDataIsTampered()
    {
        // Arrange
        using var signingService = new EcdsaP256SigningService();
        var publicKeySpki = signingService.GetPublicKeySpki();
        var verifier = new EcdsaP256SignatureVerifier();

        var originalData = "{\"amount\":100,\"license_id\":\"id-123\"}";
        var signature = signingService.SignData(originalData);

        var tamperedData = "{\"amount\":999,\"license_id\":\"id-123\"}";

        // Act
        var isValid = verifier.VerifySignature(tamperedData, signature, publicKeySpki);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void EcdsaP256_Verify_ShouldFailWithDifferentPublicKey()
    {
        // Arrange
        using var signingService1 = new EcdsaP256SigningService();
        using var signingService2 = new EcdsaP256SigningService();
        var differentPublicKeySpki = signingService2.GetPublicKeySpki();
        var verifier = new EcdsaP256SignatureVerifier();

        var data = "{\"license_id\":\"test-1\"}";
        var signature = signingService1.SignData(data);

        // Act
        var isValid = verifier.VerifySignature(data, signature, differentPublicKeySpki);

        // Assert
        isValid.Should().BeFalse();
    }
}
