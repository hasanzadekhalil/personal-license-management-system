using System.Text.Json.Serialization;

namespace LicenseSystem.Core.Models;

public class SignedArtifact<T>
{
    [JsonPropertyName("header")]
    public ArtifactHeader Header { get; set; } = new();

    [JsonPropertyName("payload")]
    public T Payload { get; set; } = default!;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class ArtifactHeader
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "ES256";
}
