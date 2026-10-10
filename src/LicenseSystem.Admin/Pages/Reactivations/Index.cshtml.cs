using System.Text.Json;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages.Reactivations;

public class IndexModel : PageModel
{
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditRepository _auditRepo;
    private readonly ISigningService _signingService;

    public IndexModel(ILicenseRepository licenseRepo, IAuditRepository auditRepo, ISigningService signingService)
    {
        _licenseRepo = licenseRepo;
        _auditRepo = auditRepo;
        _signingService = signingService;
    }

    public List<ReactivationTransaction> PendingTransactions { get; set; } = new();

    [BindProperty]
    public string? OfflineRequestJson { get; set; }

    public string? GeneratedResponseJson { get; set; }

    public async Task OnGetAsync()
    {
        var allLicenses = await _licenseRepo.GetAllAsync();
        PendingTransactions = allLicenses
            .SelectMany(l => l.ReactivationTransactions)
            .Where(rt => rt.Status == ReactivationStatus.Pending)
            .OrderByDescending(rt => rt.RequestedAtUtc)
            .ToList();
    }

    public async Task<IActionResult> OnPostApproveOnlineAsync(string nonce)
    {
        var transaction = await _licenseRepo.GetReactivationByNonceAsync(nonce);
        if (transaction != null && transaction.Status == ReactivationStatus.Pending)
        {
            var license = transaction.License;
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

            await _auditRepo.LogEventAsync("HardwareReactivationApproved", "Admin", null, license.LicenseKey, new { nonce, transaction.NewHardwareHash });
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostProcessOfflineFileAsync()
    {
        await OnGetAsync();

        if (string.IsNullOrWhiteSpace(OfflineRequestJson))
        {
            return Page();
        }

        try
        {
            using var doc = JsonDocument.Parse(OfflineRequestJson);
            var root = doc.RootElement;

            var licenseKey = root.GetProperty("license_key").GetString() ?? "";
            var newHardwareHash = root.GetProperty("new_hardware_hash").GetString() ?? "";
            var nonce = root.GetProperty("nonce").GetString() ?? "";

            var license = await _licenseRepo.GetByKeyAsync(licenseKey);
            if (license == null)
            {
                ModelState.AddModelError("", "License key in request not found.");
                return Page();
            }

            // Create or update transaction
            var transaction = await _licenseRepo.GetReactivationByNonceAsync(nonce);
            if (transaction == null)
            {
                transaction = new ReactivationTransaction
                {
                    LicenseId = license.Id,
                    OldHardwareHash = license.Activations.FirstOrDefault(a => a.IsActive)?.HardwareHash ?? "INITIAL",
                    NewHardwareHash = newHardwareHash,
                    Nonce = nonce,
                    Status = ReactivationStatus.Approved,
                    RequestedAtUtc = DateTime.UtcNow,
                    ResolvedAtUtc = DateTime.UtcNow
                };
                await _licenseRepo.AddReactivationTransactionAsync(transaction);
            }
            else
            {
                transaction.Status = ReactivationStatus.Approved;
                transaction.ResolvedAtUtc = DateTime.UtcNow;
                await _licenseRepo.UpdateReactivationTransactionAsync(transaction);
            }

            // Update activations
            foreach (var act in license.Activations)
            {
                act.IsActive = false;
                await _licenseRepo.UpdateActivationAsync(act);
            }

            var newAct = new Activation
            {
                LicenseId = license.Id,
                HardwareHash = newHardwareHash,
                MotherboardId = "OFFLINE_REPLACED",
                CpuId = "OFFLINE_REPLACED",
                PrimaryDiskId = "OFFLINE_REPLACED",
                DeviceName = "Offline Reactivated Device",
                IsActive = true,
                ActivatedAtUtc = DateTime.UtcNow,
                LastHeartbeatUtc = DateTime.UtcNow
            };
            await _licenseRepo.AddActivationAsync(newAct);

            // Generate signed response
            var payload = new ReactivationPayload
            {
                LicenseId = license.Id.ToString(),
                LicenseKey = license.LicenseKey,
                NewHardwareHash = newHardwareHash,
                Nonce = nonce,
                AuthorizedAtUtc = DateTime.UtcNow.ToString("o"),
                ValidUntilUtc = license.ExpiresAtUtc.ToString("o")
            };

            var canonicalPayload = CanonicalJsonSerializer.Serialize(payload);
            var signature = _signingService.SignData(canonicalPayload);

            var artifact = new SignedArtifact<ReactivationPayload>
            {
                Header = new ArtifactHeader { Version = "1.0", Algorithm = "ES256" },
                Payload = payload,
                Signature = signature
            };

            GeneratedResponseJson = CanonicalJsonSerializer.Serialize(artifact);

            await _auditRepo.LogEventAsync("OfflineReactivationIssued", "Admin", null, license.LicenseKey, new { nonce, newHardwareHash });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to parse offline request: {ex.Message}");
        }

        return Page();
    }
}
