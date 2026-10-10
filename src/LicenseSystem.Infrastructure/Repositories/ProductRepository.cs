using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using LicenseSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseSystem.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly LicenseDbContext _db;

    public ProductRepository(LicenseDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _db.Products
            .Include(p => p.LicenseProducts)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetByCodeAsync(string code)
    {
        return await _db.Products
            .Include(p => p.LicenseProducts)
            .FirstOrDefaultAsync(p => p.Code == code);
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _db.Products
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task AddAsync(Product product)
    {
        await _db.Products.AddAsync(product);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync();
    }
}
