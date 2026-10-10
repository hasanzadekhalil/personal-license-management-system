using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using LicenseSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseSystem.Infrastructure.Repositories;

public class LicenseRepository : ILicenseRepository
{
    private readonly LicenseDbContext _db;

    public LicenseRepository(LicenseDbContext db)
    {
        _db = db;
    }

    public async Task<License?> GetByIdAsync(Guid id)
    {
        return await _db.Licenses
            .Include(l => l.LicenseProducts)
                .ThenInclude(lp => lp.Product)
            .Include(l => l.Activations)
            .Include(l => l.ReactivationTransactions)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<License?> GetByKeyAsync(string licenseKey)
    {
        return await _db.Licenses
            .Include(l => l.LicenseProducts)
                .ThenInclude(lp => lp.Product)
            .Include(l => l.Activations)
            .Include(l => l.ReactivationTransactions)
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey);
    }

    public async Task<List<License>> GetAllAsync()
    {
        return await _db.Licenses
            .Include(l => l.LicenseProducts)
                .ThenInclude(lp => lp.Product)
            .Include(l => l.Activations)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddAsync(License license)
    {
        await _db.Licenses.AddAsync(license);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(License license)
    {
        _db.Licenses.Update(license);
        await _db.SaveChangesAsync();
    }

    public async Task<Activation?> GetActivationAsync(Guid licenseId, string hardwareHash)
    {
        return await _db.Activations
            .FirstOrDefaultAsync(a => a.LicenseId == licenseId && a.HardwareHash == hardwareHash);
    }

    public async Task AddActivationAsync(Activation activation)
    {
        await _db.Activations.AddAsync(activation);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateActivationAsync(Activation activation)
    {
        _db.Activations.Update(activation);
        await _db.SaveChangesAsync();
    }

    public async Task<ReactivationTransaction?> GetReactivationByNonceAsync(string nonce)
    {
        return await _db.ReactivationTransactions
            .Include(rt => rt.License)
            .FirstOrDefaultAsync(rt => rt.Nonce == nonce);
    }

    public async Task AddReactivationTransactionAsync(ReactivationTransaction transaction)
    {
        await _db.ReactivationTransactions.AddAsync(transaction);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateReactivationTransactionAsync(ReactivationTransaction transaction)
    {
        _db.ReactivationTransactions.Update(transaction);
        await _db.SaveChangesAsync();
    }
}
