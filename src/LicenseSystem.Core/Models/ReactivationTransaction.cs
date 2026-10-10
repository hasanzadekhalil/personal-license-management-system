using LicenseSystem.Core.Enums;

namespace LicenseSystem.Core.Models;

public class ReactivationTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;

    public string OldHardwareHash { get; set; } = string.Empty;
    public string NewHardwareHash { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;

    public ReactivationStatus Status { get; set; } = ReactivationStatus.Pending;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
    public AdminUser? ResolvedByAdmin { get; set; }
}
