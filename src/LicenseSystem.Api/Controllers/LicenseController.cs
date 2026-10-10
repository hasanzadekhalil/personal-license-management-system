using LicenseSystem.Api.DTOs;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace LicenseSystem.Api.Controllers;

[ApiController]
[Route("api/v1/license")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseRepository _licenseRepo;
    private readonly IProductRepository _productRepo;
    private readonly IAuditRepository _auditRepo;
    private readonly ISigningService _signingService;

    public LicenseController(
        ILicenseRepository licenseRepo,
        IProductRepository productRepo,
        IAuditRepository auditRepo,
        ISigningService signingService)
    {
        _licenseRepo = licenseRepo;
        _productRepo = productRepo;
        _auditRepo = auditRepo;
        _signingService = signingService;
    }

    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        return Ok(new { PublicKeySpki = _signingService.GetPublicKeySpki() });
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LicenseKey) ||
            string.IsNullOrWhiteSpace(req.ProductCode) ||
            string.IsNullOrWhiteSpace(req.HardwareHash))
        {
            return BadRequest(new ActivationResponse { IsSuccess = false, ErrorMessage = "Missing required activation fields." });
        }

        var license = await _licenseRepo.GetByKeyAsync(req.LicenseKey);
        if (license == null)
        {
            await _auditRepo.LogEventAsync("ActivationFailed", "Client", req.ProductCode, req.LicenseKey, new { reason = "LicenseNotFound" }, GetIp());
            return NotFound(new ActivationResponse { IsSuccess = false, ErrorMessage = "License key not found." });
        }

        if (license.IsRevoked)
        {
            await _auditRepo.LogEventAsync("ActivationFailed", "Client", req.ProductCode, req.LicenseKey, new { reason = "LicenseRevoked" }, GetIp());
            return StatusCode(403, new ActivationResponse { IsSuccess = false, ErrorMessage = $"License has been revoked: {license.RevocationReason}" });
        }

        var product = await _productRepo.GetByCodeAsync(req.ProductCode);
        if (product == null)
        {
            return BadRequest(new ActivationResponse { IsSuccess = false, ErrorMessage = $"Unknown product code: {req.ProductCode}" });
        }

        // Verify product entitlement on license
        var hasProduct = license.LicenseProducts.Any(lp => lp.ProductId == product.Id);
        if (!hasProduct)
        {
            await _auditRepo.LogEventAsync("ActivationFailed", "Client", req.ProductCode, req.LicenseKey, new { reason = "ProductNotEntitled" }, GetIp());
            return StatusCode(403, new ActivationResponse { IsSuccess = false, ErrorMessage = "License does not grant access to this product." });
        }

        // Check VM Policy
        if (req.IsVirtualMachine && !product.AllowVirtualMachines)
        {
            await _auditRepo.LogEventAsync("ActivationFailed", "Client", req.ProductCode, req.LicenseKey, new { reason = "VirtualMachinesProhibited" }, GetIp());
            return StatusCode(403, new ActivationResponse { IsSuccess = false, ErrorMessage = "Virtual machines are not permitted for this product." });
        }

        // Check or set effective activation and expiration (AT-18: 30 days monthly, 365 days annual)
        var now = DateTime.UtcNow;
        if (license.ActivatedAtUtc == null)
        {
            license.ActivatedAtUtc = now;
            if (license.LicenseType == LicenseType.Monthly)
            {
                license.ExpiresAtUtc = now.AddDays(30);
            }
            else if (license.LicenseType == LicenseType.Annual)
            {
                license.ExpiresAtUtc = now.AddDays(365);
            }
            await _licenseRepo.UpdateAsync(license);
        }
        else if (license.ExpiresAtUtc <= now)
        {
            await _auditRepo.LogEventAsync("ActivationFailed", "Client", req.ProductCode, req.LicenseKey, new { reason = "LicenseExpired" }, GetIp());
            return StatusCode(403, new ActivationResponse { IsSuccess = false, ErrorMessage = "License has expired." });
        }

        // Check device binding
        var existingActivation = license.Activations.FirstOrDefault(a => a.HardwareHash == req.HardwareHash && a.IsActive);
        if (existingActivation == null)
        {
            var activeCount = license.Activations.Count(a => a.IsActive);
            if (activeCount >= license.MaxActivations)
            {
                await _auditRepo.LogEventAsync("ActivationLimitReached", "Client", req.ProductCode, req.LicenseKey, new { activeCount, max = license.MaxActivations }, GetIp());
                return Conflict(new ActivationResponse { IsSuccess = false, ErrorMessage = "Maximum allowed device activation limit reached for this license." });
            }

            var newActivation = new Activation
            {
                LicenseId = license.Id,
                HardwareHash = req.HardwareHash,
                MotherboardId = req.MotherboardId,
                CpuId = req.CpuId,
                PrimaryDiskId = req.PrimaryDiskId,
                IsVirtualMachine = req.IsVirtualMachine,
                DeviceName = req.DeviceName,
                IsActive = true,
                ActivatedAtUtc = now,
                LastHeartbeatUtc = now
            };
            await _licenseRepo.AddActivationAsync(newActivation);
        }
        else
        {
            existingActivation.LastHeartbeatUtc = now;
            await _licenseRepo.UpdateActivationAsync(existingActivation);
        }

        // Build and Sign License Payload
        var payload = new LicensePayload
        {
            LicenseId = license.Id.ToString(),
            LicenseKey = license.LicenseKey,
            ProductCode = product.Code,
            LicenseMode = license.LicenseMode.ToString(),
            HardwareHash = req.HardwareHash,
            IssuedAtUtc = license.CreatedAtUtc.ToString("o"),
            ActivatedAtUtc = license.ActivatedAtUtc?.ToString("o") ?? now.ToString("o"),
            ExpiresAtUtc = license.ExpiresAtUtc.ToString("o"),
            GracePeriodHours = 6
        };

        var canonicalJson = CanonicalJsonSerializer.Serialize(payload);
        var signature = _signingService.SignData(canonicalJson);

        var signedArtifact = new SignedArtifact<LicensePayload>
        {
            Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
            Payload = payload,
            Signature = signature
        };

        var signedArtifactJson = CanonicalJsonSerializer.Serialize(signedArtifact);

        await _auditRepo.LogEventAsync("LicenseActivated", "Client", product.Code, license.LicenseKey, new { hardwareHash = req.HardwareHash }, GetIp());

        return Ok(new ActivationResponse
        {
            IsSuccess = true,
            SignedLicenseJson = signedArtifactJson
        });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidationRequest req)
    {
        var license = await _licenseRepo.GetByKeyAsync(req.LicenseKey);
        if (license == null)
        {
            return NotFound(new ValidationResponse { IsValid = false, Status = "NotFound", ErrorMessage = "License not found." });
        }

        if (license.IsRevoked)
        {
            return Ok(new ValidationResponse { IsValid = false, Status = "Revoked", ErrorMessage = license.RevocationReason });
        }

        if (DateTime.UtcNow >= license.ExpiresAtUtc)
        {
            return Ok(new ValidationResponse { IsValid = false, Status = "Expired", ExpiresAtUtc = license.ExpiresAtUtc, ErrorMessage = "License expired." });
        }

        var isHardwareBound = license.Activations.Any(a => a.HardwareHash == req.HardwareHash && a.IsActive);
        if (!isHardwareBound)
        {
            await _auditRepo.LogEventAsync("HardwareMismatchDetected", "Client", req.ProductCode, req.LicenseKey, new { hardwareHash = req.HardwareHash }, GetIp());
            return Ok(new ValidationResponse { IsValid = false, Status = "HardwareMismatch", ErrorMessage = "Device not authorized for this license." });
        }

        return Ok(new ValidationResponse
        {
            IsValid = true,
            Status = "Active",
            ExpiresAtUtc = license.ExpiresAtUtc
        });
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req)
    {
        var license = await _licenseRepo.GetByKeyAsync(req.LicenseKey);
        if (license == null || license.IsRevoked)
        {
            return Ok(new HeartbeatResponse { IsAcknowledged = false, IsRevoked = license?.IsRevoked ?? true });
        }

        var activation = license.Activations.FirstOrDefault(a => a.HardwareHash == req.HardwareHash && a.IsActive);
        if (activation != null)
        {
            activation.LastHeartbeatUtc = DateTime.UtcNow;
            await _licenseRepo.UpdateActivationAsync(activation);
        }

        return Ok(new HeartbeatResponse { IsAcknowledged = true, IsRevoked = false });
    }

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
