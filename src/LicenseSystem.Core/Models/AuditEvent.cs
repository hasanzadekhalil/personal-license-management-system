namespace LicenseSystem.Core.Models;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? LicenseKey { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
