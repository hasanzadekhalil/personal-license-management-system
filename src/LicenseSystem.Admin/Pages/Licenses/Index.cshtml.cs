using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages.Licenses;

public class IndexModel : PageModel
{
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditRepository _auditRepo;

    public IndexModel(ILicenseRepository licenseRepo, IAuditRepository auditRepo)
    {
        _licenseRepo = licenseRepo;
        _auditRepo = auditRepo;
    }

    public List<License> Licenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        Licenses = await _licenseRepo.GetAllAsync();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid licenseId)
    {
        var license = await _licenseRepo.GetByIdAsync(licenseId);
        if (license != null && !license.IsRevoked)
        {
            license.IsRevoked = true;
            license.RevokedAtUtc = DateTime.UtcNow;
            license.RevocationReason = "Revoked by Administrator";
            await _licenseRepo.UpdateAsync(license);

            await _auditRepo.LogEventAsync("LicenseRevoked", "Admin", null, license.LicenseKey, new { reason = license.RevocationReason });
        }

        return RedirectToPage();
    }
}
