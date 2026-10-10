using LicenseSystem.Core.Models;

namespace LicenseSystem.Core.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> GetByCodeAsync(string code);
    Task<List<Product>> GetAllAsync();
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
}

public interface ILicenseRepository
{
    Task<License?> GetByIdAsync(Guid id);
    Task<License?> GetByKeyAsync(string licenseKey);
    Task<List<License>> GetAllAsync();
    Task AddAsync(License license);
    Task UpdateAsync(License license);
    Task<Activation?> GetActivationAsync(Guid licenseId, string hardwareHash);
    Task AddActivationAsync(Activation activation);
    Task UpdateActivationAsync(Activation activation);
    Task<ReactivationTransaction?> GetReactivationByNonceAsync(string nonce);
    Task AddReactivationTransactionAsync(ReactivationTransaction transaction);
    Task UpdateReactivationTransactionAsync(ReactivationTransaction transaction);
}

public interface IAuditRepository
{
    Task LogEventAsync(string eventType, string actor, string? productCode, string? licenseKey, object? details, string? ipAddress = null);
    Task<List<AuditEvent>> GetRecentEventsAsync(int limit = 100);
}
