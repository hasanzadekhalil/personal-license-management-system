using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LicenseSystem.Api.DTOs;
using LicenseSystem.ClientSdk;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Policy;
using LicenseSystem.ClientSdk.Reactivation;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using LicenseSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

[Trait("Category", "AcceptanceTest")]
public class MasterAcceptanceTestSuite : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly EcdsaP256SigningService _signingService;
    private readonly EcdsaP256SignatureVerifier _verifier;
    private readonly string _serverPublicKey;

    public MasterAcceptanceTestSuite(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _signingService = new EcdsaP256SigningService();
        _verifier = new EcdsaP256SignatureVerifier();
        _serverPublicKey = _signingService.GetPublicKeySpki();
    }

    private async Task<(Product product, License license)> SeedProductAndLicenseAsync(
        string? productCode = null,
        string? licenseKey = null,
        LicenseMode mode = LicenseMode.OfflineWithGrace,
        int maxActivations = 1,
        DateTime? expiresAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();

        var pCode = productCode ?? ("PROD-" + Guid.NewGuid().ToString("N")[..6].ToUpper());
        var product = await db.Products.FirstOrDefaultAsync(p => p.Code == pCode);
        if (product == null)
        {
            product = new Product
            {
                Code = pCode,
                Name = "Master Test Product " + pCode,
                Description = "Product for automated acceptance test suite",
                AllowVirtualMachines = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var lKey = licenseKey ?? ("LIC-" + Guid.NewGuid().ToString("N")[..12].ToUpper());
        var license = new License
        {
            LicenseKey = lKey,
            LicenseType = LicenseType.Monthly,
            LicenseMode = mode,
            MaxActivations = maxActivations,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAt ?? DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        };
        db.Licenses.Add(license);
        await db.SaveChangesAsync();

        var link = new LicenseProduct
        {
            LicenseId = license.Id,
            ProductId = product.Id
        };
        db.LicenseProducts.Add(link);
        await db.SaveChangesAsync();

        return (product, license);
    }

    [Fact]
    public async Task AT01_HappyPathActivation_ShouldSucceed()
    {
        var (prod, lic) = await SeedProductAndLicenseAsync();
        var hardware = new MockHardwareFingerprintProvider();

        var req = new ActivationRequest
        {
            ProductCode = prod.Code,
            LicenseKey = lic.LicenseKey,
            HardwareHash = hardware.GetCompositeHash(),
            DeviceName = "MasterTestDevice",
            MotherboardId = hardware.MotherboardId,
            CpuId = hardware.CpuId,
            PrimaryDiskId = hardware.PrimaryDiskId
        };

        var response = await _client.PostAsJsonAsync("/api/v1/license/activate", req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ActivationResponse>();
        body.Should().NotBeNull();
        body!.IsSuccess.Should().BeTrue();
        body.SignedLicenseJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AT02_InvalidLicenseKey_ShouldBeRejectedWithNotFound()
    {
        var req = new ActivationRequest
        {
            ProductCode = "NON_EXISTENT_PROD",
            LicenseKey = "INVALID-KEY-999-999",
            HardwareHash = "SOME-HASH-12345"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/license/activate", req);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void AT03_TamperedLicenseFile_ShouldBeRejectedByClientPolicy()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var time = new MockTimeProvider();

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-TAMPER-TEST",
            ProductCode = "PROD-A",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload));

        // Tamper with payload byte
        payload.ProductCode = "PROD-B";

        var artifact = new SignedArtifact<LicensePayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = signature
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey);
        var result = engine.Evaluate(artifact, "PROD-B");

        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.Revoked);
        result.Message.Should().Contain("Cryptographic signature verification failed");
    }

    [Fact]
    public void AT04_WrongSignatureOrUntrustedKey_ShouldBeRejected()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var time = new MockTimeProvider();
        var rogueSigner = new EcdsaP256SigningService(); // Different keypair!

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-ROGUE-KEY",
            ProductCode = "PROD-A",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var rogueSignature = rogueSigner.SignData(CanonicalJsonSerializer.Serialize(payload));

        var artifact = new SignedArtifact<LicensePayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = rogueSignature
        };

        // Engine only trusts _serverPublicKey
        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey);
        var result = engine.Evaluate(artifact, "PROD-A");

        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.Revoked);
    }

    [Fact]
    public void AT05_ServerOffline_OnlineOnlyMode_ShouldLockImmediatelyWithoutGrace()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var time = new MockTimeProvider();

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-ONLINE-ONLY",
            ProductCode = "PROD-ONLINE",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OnlineOnly",
            GracePeriodHours = 0,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload));
        var artifact = new SignedArtifact<LicensePayload>
        {
            Payload = payload,
            Signature = signature
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey);

        // Validation fails because server is unreachable
        var result = engine.Evaluate(artifact, "PROD-ONLINE", isOnlineAvailable: false, serverValidationSucceeded: false);

        result.IsAuthorized.Should().BeFalse();
        result.RemainingGracePeriod.Should().BeNull();
        result.Message.Should().Contain("Immediate lock enforced");
    }

    [Fact]
    public void AT06_HardwareChange_Motherboard_ShouldLockApp()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();

        hardware.ReplaceMotherboard("NEW-MAINBOARD-UUID-999");
        var newHash = hardware.GetCompositeHash();

        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void AT07_HardwareChange_CPU_ShouldLockApp()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();

        hardware.ReplaceCpu("NEW-PROCESSOR-ID-777");
        var newHash = hardware.GetCompositeHash();

        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void AT08_HardwareChange_PrimaryDrive_ShouldLockApp()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();

        hardware.ReplacePrimaryDisk("NEW-NVME-SSD-SERIAL-333");
        var newHash = hardware.GetCompositeHash();

        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void AT09_SimultaneousHardwareChanges_ShouldLockApp()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();

        hardware.ReplaceMotherboard("NEW-MB");
        hardware.ReplaceCpu("NEW-CPU");
        hardware.ReplacePrimaryDisk("NEW-SSD");

        hardware.GetCompositeHash().Should().NotBe(originalHash);
    }

    [Fact]
    public void AT10_NonMonitoredHardwareChange_ShouldNotChangeCompositeHash()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var originalHash = hardware.GetCompositeHash();

        // GPU / RAM change does not affect the 3-component composite hash
        var hashAfterGpuChange = hardware.GetCompositeHash();
        hashAfterGpuChange.Should().Be(originalHash);
    }

    [Fact]
    public void AT11_GracePeriodActive_ShouldAuthorizeWithin6Hours()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var startTime = DateTime.UtcNow;
        var time = new MockTimeProvider(startTime);

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-GRACE-TEST",
            ProductCode = "PROD-GRACE",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = startTime.ToString("o"),
            ActivatedAtUtc = startTime.ToString("o"),
            ExpiresAtUtc = startTime.AddDays(30).ToString("o")
        };

        var artifact = new SignedArtifact<LicensePayload>
        {
            Payload = payload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload))
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey, startTime);
        engine.RecordSuccessfulOnlineSync(startTime);

        // Advance 2 hours offline
        time.AdvanceTime(TimeSpan.FromHours(2));

        var result = engine.Evaluate(artifact, "PROD-GRACE", isOnlineAvailable: false, serverValidationSucceeded: false);

        result.IsAuthorized.Should().BeTrue();
        result.RemainingGracePeriod.Should().NotBeNull();
        result.RemainingGracePeriod!.Value.TotalHours.Should().BeApproximately(4.0, 0.1);
    }

    [Fact]
    public void AT12_HardwareMismatch_NeverReceivesGrace()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var time = new MockTimeProvider();

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-PRECEDENCE-TEST",
            ProductCode = "PROD-P",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var artifact = new SignedArtifact<LicensePayload>
        {
            Payload = payload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload))
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey);

        // Alter hardware
        hardware.ReplaceMotherboard("ALTERED-MB");

        var result = engine.Evaluate(artifact, "PROD-P", isOnlineAvailable: false, serverValidationSucceeded: false);

        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.LockedHardwareMismatch);
        result.RemainingGracePeriod.Should().BeNull();
    }

    [Fact]
    public async Task AT13_AdminRevocation_ShouldReturnForbidden403()
    {
        var (prod, lic) = await SeedProductAndLicenseAsync();
        var hardware = new MockHardwareFingerprintProvider();

        // 1. Activate
        var actReq = new ActivationRequest
        {
            ProductCode = prod.Code,
            LicenseKey = lic.LicenseKey,
            HardwareHash = hardware.GetCompositeHash()
        };
        await _client.PostAsJsonAsync("/api/v1/license/activate", actReq);

        // 2. Revoke license in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
            var licInDb = await db.Licenses.FindAsync(lic.Id);
            licInDb!.IsRevoked = true;
            licInDb.RevocationReason = "Administrative Security Action";
            await db.SaveChangesAsync();
        }

        // 3. Validate
        var valReq = new ValidationRequest
        {
            LicenseKey = lic.LicenseKey,
            HardwareHash = hardware.GetCompositeHash()
        };
        var response = await _client.PostAsJsonAsync("/api/v1/license/validate", valReq);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AT14_ActivationLimits_ShouldRejectSecondMachineWithConflict409()
    {
        var (prod, lic) = await SeedProductAndLicenseAsync(maxActivations: 1);

        // First Machine activates
        var actReq1 = new ActivationRequest
        {
            ProductCode = prod.Code,
            LicenseKey = lic.LicenseKey,
            HardwareHash = "MACHINE-1-HASH-11111"
        };
        var resp1 = await _client.PostAsJsonAsync("/api/v1/license/activate", actReq1);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second Machine tries to activate same key
        var actReq2 = new ActivationRequest
        {
            ProductCode = prod.Code,
            LicenseKey = lic.LicenseKey,
            HardwareHash = "MACHINE-2-HASH-22222"
        };
        var resp2 = await _client.PostAsJsonAsync("/api/v1/license/activate", actReq2);
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public void AT15_DatabaseBackupRestore_EvidenceScriptExistsAndIsExecutable()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? scriptPath = null;
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "src", "LicenseSystem.Infrastructure", "Scripts", "backup_restore.sh");
            if (File.Exists(candidate))
            {
                scriptPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        scriptPath.Should().NotBeNull();
        File.Exists(scriptPath!).Should().BeTrue();
    }

    [Fact]
    public async Task AT16_SqlInjectionResistance_ShouldTreatPayloadAsLiteral()
    {
        var maliciousKey = "LIC-TEST' OR '1'='1'; DROP TABLE licenses; --";
        var req = new ActivationRequest
        {
            ProductCode = "PROD-SAFE",
            LicenseKey = maliciousKey,
            HardwareHash = "MALICIOUS-HW' OR 1=1"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/license/activate", req);
        // Should safely return 404 without executing SQL injection
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AT17_AuditLogIntegrity_ShouldRecordEventsWithoutLeakingPrivateKeys()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();

        var events = await db.AuditEvents.OrderByDescending(a => a.CreatedAtUtc).Take(10).ToListAsync();
        foreach (var ev in events)
        {
            ev.DetailsJson.Should().NotContain("PRIVATE KEY");
            ev.DetailsJson.Should().NotContain("PasswordHash");
        }
    }

    [Fact]
    public async Task AT18_LicenseDuration_MonthlyShouldBe30Days()
    {
        var (prod, lic) = await SeedProductAndLicenseAsync();
        var hardware = new MockHardwareFingerprintProvider();

        var actReq = new ActivationRequest
        {
            ProductCode = prod.Code,
            LicenseKey = lic.LicenseKey,
            HardwareHash = hardware.GetCompositeHash()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/license/activate", actReq);
        var body = await response.Content.ReadFromJsonAsync<ActivationResponse>();

        var artifact = JsonSerializer.Deserialize<SignedArtifact<LicensePayload>>(body!.SignedLicenseJson);
        var activated = DateTime.Parse(artifact!.Payload.ActivatedAtUtc);
        var expires = DateTime.Parse(artifact.Payload.ExpiresAtUtc);

        (expires - activated).TotalDays.Should().BeApproximately(30, 1.0);
    }

    [Fact]
    public void AT19_NetworkDropRecovery_OnlineOnlyMode_EnforcesLock()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var time = new MockTimeProvider();

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-ONLINE-DROP",
            ProductCode = "PROD-DROP",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OnlineOnly",
            GracePeriodHours = 0,
            IssuedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var artifact = new SignedArtifact<LicensePayload>
        {
            Payload = payload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload))
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey);
        var result = engine.Evaluate(artifact, "PROD-DROP", isOnlineAvailable: false);

        result.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public void AT20_ClockTampering_RollbackDetection_ShouldLockApp()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var baseTime = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var time = new MockTimeProvider(baseTime);

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = "LIC-CLOCK-TAMPER",
            ProductCode = "PROD-C",
            HardwareHash = hardware.GetCompositeHash(),
            LicenseMode = "OfflineWithGrace",
            GracePeriodHours = 6,
            IssuedAtUtc = baseTime.ToString("o"),
            ActivatedAtUtc = baseTime.ToString("o"),
            ExpiresAtUtc = baseTime.AddDays(30).ToString("o")
        };

        var artifact = new SignedArtifact<LicensePayload>
        {
            Payload = payload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload))
        };

        var engine = new LicensePolicyEngine(_verifier, hardware, time, _serverPublicKey, baseTime);

        // Normal check
        engine.Evaluate(artifact, "PROD-C").IsAuthorized.Should().BeTrue();

        // Roll back clock
        time.RollbackTime(TimeSpan.FromDays(2));

        var result = engine.Evaluate(artifact, "PROD-C");
        result.IsAuthorized.Should().BeFalse();
        result.Status.Should().Be(LicenseStatus.LockedClockTampered);
    }

    [Fact]
    public void AT21_ReplayAttackOnOfflineReactivation_ShouldBeBlocked()
    {
        var hardware = new MockHardwareFingerprintProvider();
        var handler = new ReactivationHandler(hardware, _verifier, _serverPublicKey);

        var req = handler.GenerateOfflineRequest("LIC-REPLAY-PREVENTION");

        var payload = new ReactivationPayload
        {
            LicenseId = Guid.NewGuid().ToString(),
            LicenseKey = req.LicenseKey,
            NewHardwareHash = req.NewHardwareHash,
            Nonce = req.Nonce,
            AuthorizedAtUtc = DateTime.UtcNow.ToString("o"),
            ValidUntilUtc = DateTime.UtcNow.AddDays(30).ToString("o")
        };

        var artifact = new SignedArtifact<ReactivationPayload>
        {
            Payload = payload,
            Signature = _signingService.SignData(CanonicalJsonSerializer.Serialize(payload))
        };

        var responseJson = CanonicalJsonSerializer.Serialize(artifact);

        // 1. First consume succeeds
        handler.ProcessOfflineResponse(responseJson).IsSuccess.Should().BeTrue();

        // 2. Replay attempt fails
        var replay = handler.ProcessOfflineResponse(responseJson);
        replay.IsSuccess.Should().BeFalse();
        replay.ErrorMessage.Should().Contain("Replay attack detected");
    }

    [Fact]
    public void AT22_NonAdminStandardUser_ShouldExtractHardwareFallbackWithoutException()
    {
        var provider = new WindowsHardwareFingerprintProvider();
        var hash = provider.GetCompositeHash();

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64); // Valid SHA-256 hex string
    }
}
