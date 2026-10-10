using System.Text.Json;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using LicenseSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseSystem.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly LicenseDbContext _db;

    public AuditRepository(LicenseDbContext db)
    {
        _db = db;
    }

    public async Task LogEventAsync(string eventType, string actor, string? productCode, string? licenseKey, object? details, string? ipAddress = null)
    {
        var audit = new AuditEvent
        {
            EventType = eventType,
            Actor = actor,
            ProductCode = productCode,
            LicenseKey = licenseKey,
            DetailsJson = details != null ? JsonSerializer.Serialize(details) : "{}",
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _db.AuditEvents.AddAsync(audit);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditEvent>> GetRecentEventsAsync(int limit = 100)
    {
        return await _db.AuditEvents
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();
    }
}
