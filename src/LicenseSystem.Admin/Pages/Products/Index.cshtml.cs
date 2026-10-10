using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages.Products;

public class IndexModel : PageModel
{
    private readonly IProductRepository _productRepo;
    private readonly IAuditRepository _auditRepo;

    public IndexModel(IProductRepository productRepo, IAuditRepository auditRepo)
    {
        _productRepo = productRepo;
        _auditRepo = auditRepo;
    }

    public List<Product> Products { get; set; } = new();

    [BindProperty]
    public ProductInputModel NewProduct { get; set; } = new();

    public async Task OnGetAsync()
    {
        Products = await _productRepo.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Products = await _productRepo.GetAllAsync();
            return Page();
        }

        var existing = await _productRepo.GetByCodeAsync(NewProduct.Code.Trim());
        if (existing == null)
        {
            var product = new Product
            {
                Code = NewProduct.Code.Trim().ToUpperInvariant(),
                Name = NewProduct.Name.Trim(),
                Description = NewProduct.Description?.Trim() ?? string.Empty,
                AllowVirtualMachines = NewProduct.AllowVirtualMachines
            };

            await _productRepo.AddAsync(product);
            await _auditRepo.LogEventAsync("ProductRegistered", "Admin", product.Code, null, new { product.Name });
        }

        return RedirectToPage();
    }

    public class ProductInputModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AllowVirtualMachines { get; set; } = true;
    }
}
