using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages.Licenses;

public class CreateModel : PageModel
{
    private readonly ILicenseRepository _licenseRepo;
    private readonly IProductRepository _productRepo;
    private readonly IAuditRepository _auditRepo;

    public CreateModel(ILicenseRepository licenseRepo, IProductRepository productRepo, IAuditRepository auditRepo)
    {
        _licenseRepo = licenseRepo;
        _productRepo = productRepo;
        _auditRepo = auditRepo;
    }

    public List<Product> AvailableProducts { get; set; } = new();

    [BindProperty]
    public LicenseInputModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        AvailableProducts = await _productRepo.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AvailableProducts = await _productRepo.GetAllAsync();
        if (!ModelState.IsValid || Input.ProductId == Guid.Empty)
        {
            return Page();
        }

        var key = string.IsNullOrWhiteSpace(Input.CustomKey)
            ? $"LIC-{Guid.NewGuid().ToString("N")[..4].ToUpper()}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}"
            : Input.CustomKey.Trim().ToUpperInvariant();

        var type = Enum.Parse<LicenseType>(Input.LicenseType);
        var mode = Enum.Parse<LicenseMode>(Input.LicenseMode);

        var expiresAt = type switch
        {
            LicenseType.Monthly => DateTime.UtcNow.AddDays(30),
            LicenseType.Annual => DateTime.UtcNow.AddDays(365),
            LicenseType.Perpetual => DateTime.UtcNow.AddYears(100),
            _ => DateTime.UtcNow.AddDays(30)
        };

        var license = new License
        {
            LicenseKey = key,
            LicenseType = type,
            LicenseMode = mode,
            MaxActivations = Input.MaxActivations,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = DateTime.UtcNow
        };

        var lp = new LicenseProduct
        {
            LicenseId = license.Id,
            ProductId = Input.ProductId
        };
        license.LicenseProducts.Add(lp);

        await _licenseRepo.AddAsync(license);

        var product = AvailableProducts.FirstOrDefault(p => p.Id == Input.ProductId);
        await _auditRepo.LogEventAsync("LicenseCreated", "Admin", product?.Code, license.LicenseKey, new
        {
            type = type.ToString(),
            mode = mode.ToString(),
            maxActivations = Input.MaxActivations
        });

        return RedirectToPage("/Licenses/Index");
    }

    public class LicenseInputModel
    {
        public Guid ProductId { get; set; }
        public string LicenseType { get; set; } = "Monthly";
        public string LicenseMode { get; set; } = "OfflineWithGrace";
        public int MaxActivations { get; set; } = 1;
        public string? CustomKey { get; set; }
    }
}
