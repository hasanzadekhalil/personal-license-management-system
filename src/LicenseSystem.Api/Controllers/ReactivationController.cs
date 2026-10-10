using LicenseSystem.Api.DTOs;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace LicenseSystem.Api.Controllers;

[ApiController]
[Route("api/v1/reactivate")]
public class ReactivationController : ControllerBase
{
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditRepository _auditRepo;
    private readonly ISigningService _signingService;

    public ReactivationController(
        ILicenseRepository licenseRepo,
        IAuditRepository auditRepo,
        ISigningService signingService)
    {
        _licenseRepo = licenseRepo;
        _auditRepo = auditRepo;
        _signingService = signingService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> RequestReactivation([FromBody] ReactivationApiRequest req)
    {
        var license = await _licenseRepo.GetByKeyAsync(req.LicenseKey);
        if (license == null)
        {
            return NotFound(new ReactivationApiResponse { IsSuccess = false, ErrorMessage = "License not found." });
        }

        if (license.IsRevoked)
        {
            return StatusCode(403, new ReactivationApiResponse { IsSuccess = false, ErrorMessage = "License is revoked." });
        }

        var activeActivation = license.Activations.FirstOrDefault(a => a.IsActive);
        var oldHash = activeActivation?.HardwareHash ?? "INITIAL";

        var transaction = new ReactivationTransaction
        {
            LicenseId = license.Id,
            OldHardwareHash = oldHash,
            NewHardwareHash = req.NewHardwareHash,
            Nonce = req.Nonce,
            Status = ReactivationStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow
        };

        await _licenseRepo.AddReactivationTransactionAsync(transaction);
        await _auditRepo.LogEventAsync("ReactivationRequested", "Client", req.ProductCode, req.LicenseKey, new { req.Nonce, req.NewHardwareHash }, GetIp());

        return Ok(new { IsSuccess = true, Nonce = req.Nonce, Status = "Pending" });
    }

    [HttpPost("approve")]
    public async Task<IActionResult> ApproveReactivation([FromBody] ReactivationApiRequest req)
    {
        var transaction = await _licenseRepo.GetReactivationByNonceAsync(req.Nonce);
        if (transaction == null)
        {
            return NotFound(new ReactivationApiResponse { IsSuccess = false, ErrorMessage = "Transaction not found." });
        }

        if (transaction.Status != ReactivationStatus.Pending)
        {
            return BadRequest(new ReactivationApiResponse { IsSuccess = false, ErrorMessage = $"Transaction is already {transaction.Status}." });
        }

        var license = transaction.License;

        // Deactivate old bindings and activate new hardware
        foreach (var act in license.Activations)
        {
            act.IsActive = false;
            await _licenseRepo.UpdateActivationAsync(act);
        }

        var newAct = new Activation
        {
            LicenseId = license.Id,
            HardwareHash = transaction.NewHardwareHash,
            MotherboardId = "REPLACED",
            CpuId = "REPLACED",
            PrimaryDiskId = "REPLACED",
            DeviceName = "Reactivated Device",
            IsActive = true,
            ActivatedAtUtc = DateTime.UtcNow,
            LastHeartbeatUtc = DateTime.UtcNow
        };
        await _licenseRepo.AddActivationAsync(newAct);

        transaction.Status = ReactivationStatus.Approved;
        transaction.ResolvedAtUtc = DateTime.UtcNow;
        await _licenseRepo.UpdateReactivationTransactionAsync(transaction);

        // Generate Signed Reactivation Response
        var payload = new ReactivationPayload
        {
            LicenseId = license.Id.ToString(),
            LicenseKey = license.LicenseKey,
            NewHardwareHash = transaction.NewHardwareHash,
            Nonce = transaction.Nonce,
            AuthorizedAtUtc = DateTime.UtcNow.ToString("o"),
            ValidUntilUtc = license.ExpiresAtUtc.ToString("o")
        };

        var canonicalPayload = CanonicalJsonSerializer.Serialize(payload);
        var signature = _signingService.SignData(canonicalPayload);

        var signedArtifact = new SignedArtifact<ReactivationPayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = signature
        };

        var signedJson = CanonicalJsonSerializer.Serialize(signedArtifact);

        await _auditRepo.LogEventAsync("ReactivationApproved", "Admin", null, license.LicenseKey, new { transaction.Nonce, transaction.NewHardwareHash }, GetIp());

        return Ok(new ReactivationApiResponse
        {
            IsSuccess = true,
            SignedReactivationJson = signedJson
        });
    }

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
