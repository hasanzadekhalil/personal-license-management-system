using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LicenseSystem.Api.DTOs;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Models;
using LicenseSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Product Product, License License)> SeedProductAndLicenseAsync(
        string productCode = "PROD-API-01",
        string licenseKey = "KEY-API-01",
        LicenseType type = LicenseType.Monthly,
        int maxActivations = 1,
        bool isRevoked = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();

        var existingProd = db.Products.FirstOrDefault(p => p.Code == productCode);
        if (existingProd == null)
        {
            existingProd = new Product
            {
                Code = productCode,
                Name = $"Product {productCode}",
                AllowVirtualMachines = true
            };
            db.Products.Add(existingProd);
            await db.SaveChangesAsync();
        }

        var existingLic = db.Licenses.FirstOrDefault(l => l.LicenseKey == licenseKey);
        if (existingLic != null)
        {
            db.Licenses.Remove(existingLic);
            await db.SaveChangesAsync();
        }

        var license = new License
        {
            LicenseKey = licenseKey,
            LicenseType = type,
            LicenseMode = LicenseMode.OfflineWithGrace,
            MaxActivations = maxActivations,
            IsRevoked = isRevoked,
            RevocationReason = isRevoked ? "Policy violation" : null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };
        db.Licenses.Add(license);
        await db.SaveChangesAsync();

        var lp = new LicenseProduct
        {
            LicenseId = license.Id,
            ProductId = existingProd.Id
        };
        db.LicenseProducts.Add(lp);
        await db.SaveChangesAsync();

        return (existingProd, license);
    }

    [Fact]
    public async Task Activate_WithValidKey_ShouldReturnSignedLicenseAnd200()
    {
        // Arrange
        var (prod, lic) = await SeedProductAndLicenseAsync("PROD-TEST-1", "LIC-ACT-001");
        var req = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "HASH-MOCK-001",
            MotherboardId = "MB-01",
            CpuId = "CPU-01",
            PrimaryDiskId = "DISK-01",
            DeviceName = "TEST-PC"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/license/activate", req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ActivationResponse>();
        content.Should().NotBeNull();
        content!.IsSuccess.Should().BeTrue();
        content.SignedLicenseJson.Should().NotBeNullOrEmpty();

        // Verify that the signed payload is cryptographically valid
        var artifact = JsonSerializer.Deserialize<SignedArtifact<LicensePayload>>(content.SignedLicenseJson!);
        artifact.Should().NotBeNull();
        artifact!.Payload.LicenseKey.Should().Be(lic.LicenseKey);
        artifact.Payload.HardwareHash.Should().Be("HASH-MOCK-001");

        // Verify signature with server's public key
        var keyRes = await _client.GetFromJsonAsync<JsonElement>("/api/v1/license/public-key");
        var publicKeySpki = keyRes.GetProperty("publicKeySpki").GetString();

        var verifier = new EcdsaP256SignatureVerifier();
        var canonicalPayload = CanonicalJsonSerializer.Serialize(artifact.Payload);
        verifier.VerifySignature(canonicalPayload, artifact.Signature, publicKeySpki!).Should().BeTrue();
    }

    [Fact]
    public async Task Activate_ExceedingLimit_ShouldReturn409Conflict()
    {
        // Arrange (MaxActivations = 1)
        var (prod, lic) = await SeedProductAndLicenseAsync("PROD-TEST-2", "LIC-LIMIT-001", maxActivations: 1);

        // First activation on Device 1
        var req1 = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "DEVICE-HASH-A",
            MotherboardId = "MB-A",
            CpuId = "CPU-A",
            PrimaryDiskId = "DISK-A"
        };
        var res1 = await _client.PostAsJsonAsync("/api/v1/license/activate", req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second activation on Device 2 (exceeds limit)
        var req2 = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "DEVICE-HASH-B",
            MotherboardId = "MB-B",
            CpuId = "CPU-B",
            PrimaryDiskId = "DISK-B"
        };
        var res2 = await _client.PostAsJsonAsync("/api/v1/license/activate", req2);

        // Assert
        res2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_RevokedLicense_ShouldReturn403Forbidden()
    {
        // Arrange
        var (prod, lic) = await SeedProductAndLicenseAsync("PROD-TEST-3", "LIC-REVOKED-001", isRevoked: true);
        var req = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "DEVICE-HASH-X",
            MotherboardId = "MB-X",
            CpuId = "CPU-X",
            PrimaryDiskId = "DISK-X"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/license/activate", req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Activate_MonthlyLicense_ShouldEnforceExact30DayDuration()
    {
        // Arrange
        var (prod, lic) = await SeedProductAndLicenseAsync("PROD-TEST-4", "LIC-MONTHLY-001", type: LicenseType.Monthly);
        var req = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "DEVICE-HASH-M",
            MotherboardId = "MB-M",
            CpuId = "CPU-M",
            PrimaryDiskId = "DISK-M"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/license/activate", req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await res.Content.ReadFromJsonAsync<ActivationResponse>();
        var artifact = JsonSerializer.Deserialize<SignedArtifact<LicensePayload>>(content!.SignedLicenseJson!);

        var activatedAt = DateTime.Parse(artifact!.Payload.ActivatedAtUtc);
        var expiresAt = DateTime.Parse(artifact.Payload.ExpiresAtUtc);

        // Assert AT-18: Exactly 30 days
        var diff = expiresAt - activatedAt;
        diff.TotalDays.Should().BeApproximately(30.0, 0.05);
    }

    [Fact]
    public async Task Validate_WithDifferentHardware_ShouldReturnHardwareMismatch()
    {
        // Arrange
        var (prod, lic) = await SeedProductAndLicenseAsync("PROD-TEST-5", "LIC-MISMATCH-001");
        var req = new ActivationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "ORIGINAL-HASH",
            MotherboardId = "MB-1",
            CpuId = "CPU-1",
            PrimaryDiskId = "DISK-1"
        };
        await _client.PostAsJsonAsync("/api/v1/license/activate", req);

        // Act (Validate with different hardware hash)
        var valReq = new ValidationRequest
        {
            LicenseKey = lic.LicenseKey,
            ProductCode = prod.Code,
            HardwareHash = "ALTERED-HASH"
        };
        var valRes = await _client.PostAsJsonAsync("/api/v1/license/validate", valReq);

        // Assert
        valRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var valContent = await valRes.Content.ReadFromJsonAsync<ValidationResponse>();
        valContent!.IsValid.Should().BeFalse();
        valContent.Status.Should().Be("HardwareMismatch");
    }
}
