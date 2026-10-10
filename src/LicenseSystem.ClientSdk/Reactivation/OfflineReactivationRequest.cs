using System.Text.Json.Serialization;

namespace LicenseSystem.ClientSdk.Reactivation;

public class OfflineReactivationRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("new_hardware_hash")]
    public string NewHardwareHash { get; set; } = string.Empty;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("requested_at_utc")]
    public string RequestedAtUtc { get; set; } = string.Empty;
}
