using System.Text.Json.Serialization;

namespace LicenseSystem.Core.Models;

public class ReactivationPayload
{
    [JsonPropertyName("license_id")]
    public string LicenseId { get; set; } = string.Empty;

    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("new_hardware_hash")]
    public string NewHardwareHash { get; set; } = string.Empty;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("authorized_at")]
    public string AuthorizedAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("valid_until")]
    public string ValidUntilUtc { get; set; } = string.Empty;
}
