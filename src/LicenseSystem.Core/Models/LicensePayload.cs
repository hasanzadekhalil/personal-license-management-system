using System.Text.Json.Serialization;

namespace LicenseSystem.Core.Models;

public class LicensePayload
{
    [JsonPropertyName("license_id")]
    public string LicenseId { get; set; } = string.Empty;

    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("license_mode")]
    public string LicenseMode { get; set; } = string.Empty; // "OnlineOnly", "OfflineWithGrace", "AirGapped"

    [JsonPropertyName("hardware_hash")]
    public string HardwareHash { get; set; } = string.Empty;

    [JsonPropertyName("issued_at")]
    public string IssuedAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("activated_at")]
    public string ActivatedAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public string ExpiresAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("grace_period_hours")]
    public int GracePeriodHours { get; set; } = 6;
}
