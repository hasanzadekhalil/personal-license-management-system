using LicenseSystem.Core.Enums;

namespace LicenseSystem.Core.Models;

public class License
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LicenseKey { get; set; } = string.Empty;
    public LicenseType LicenseType { get; set; } = LicenseType.Monthly;
    public LicenseMode LicenseMode { get; set; } = LicenseMode.OfflineWithGrace;
    public int MaxActivations { get; set; } = 1;
    public bool IsRevoked { get; set; } = false;
    public string? RevocationReason { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    public List<LicenseProduct> LicenseProducts { get; set; } = new();
    public List<Activation> Activations { get; set; } = new();
    public List<ReactivationTransaction> ReactivationTransactions { get; set; } = new();
}
