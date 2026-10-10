namespace LicenseSystem.Core.Models;

public class LicenseProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string FeatureFlagsJson { get; set; } = "{}";
}
