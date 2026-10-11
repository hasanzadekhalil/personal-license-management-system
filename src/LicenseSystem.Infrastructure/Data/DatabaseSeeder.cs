using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseSystem.Infrastructure.Data;

public static class DatabaseSeeder
{
    public const string DefaultDesktopProductCode = "PROD-DESKTOP-APP";
    public const string DefaultDemoLicenseKey = "LIC-DEMO-2026-OK";

    public static async Task SeedDefaultDataAsync(LicenseDbContext db)
    {
        // 1. Seed Desktop Demo Product
        var desktopProduct = await db.Products.FirstOrDefaultAsync(p => p.Code == DefaultDesktopProductCode);
        if (desktopProduct == null)
        {
            desktopProduct = new Product
            {
                Id = Guid.NewGuid(),
                Code = DefaultDesktopProductCode,
                Name = "Personal Desktop Application (Demo & Suite)",
                Description = "Primary desktop client application for personal licensing system",
                AllowVirtualMachines = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Products.Add(desktopProduct);
            await db.SaveChangesAsync();
        }

        // 2. Seed Default Demo License Key
        var demoLicense = await db.Licenses
            .Include(l => l.LicenseProducts)
            .FirstOrDefaultAsync(l => l.LicenseKey == DefaultDemoLicenseKey);

        if (demoLicense == null)
        {
            demoLicense = new License
            {
                Id = Guid.NewGuid(),
                LicenseKey = DefaultDemoLicenseKey,
                LicenseType = LicenseType.Monthly,
                LicenseMode = LicenseMode.OfflineWithGrace,
                MaxActivations = 5,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                IsRevoked = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            demoLicense.LicenseProducts.Add(new LicenseProduct
            {
                Id = Guid.NewGuid(),
                LicenseId = demoLicense.Id,
                ProductId = desktopProduct.Id,
                FeatureFlagsJson = "{\"modules\":[\"Standard\",\"Forensics\",\"OfflineGrace\"]}"
            });

            db.Licenses.Add(demoLicense);
            await db.SaveChangesAsync();
        }
    }
}
