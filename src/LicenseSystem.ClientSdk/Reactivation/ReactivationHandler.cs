using System.Security.Cryptography;
using System.Text.Json;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;

namespace LicenseSystem.ClientSdk.Reactivation;

public class ReactivationValidationResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public ReactivationPayload? Payload { get; set; }

    public static ReactivationValidationResult Success(ReactivationPayload payload) =>
        new() { IsSuccess = true, Payload = payload };

    public static ReactivationValidationResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}

public class ReactivationHandler
{
    private readonly IHardwareFingerprintProvider _hardwareProvider;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly string _publicKeySpki;

    private readonly HashSet<string> _pendingNonces = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _consumedNonces = new(StringComparer.OrdinalIgnoreCase);

    public ReactivationHandler(
        IHardwareFingerprintProvider hardwareProvider,
        ISignatureVerifier signatureVerifier,
        string publicKeySpki)
    {
        _hardwareProvider = hardwareProvider;
        _signatureVerifier = signatureVerifier;
        _publicKeySpki = publicKeySpki;
    }

    public OfflineReactivationRequest GenerateOfflineRequest(string licenseKey)
    {
        var nonce = GenerateCryptographicNonce();
        var currentHardwareHash = _hardwareProvider.GetCompositeHash();

        _pendingNonces.Add(nonce);

        return new OfflineReactivationRequest
        {
            LicenseKey = licenseKey,
            NewHardwareHash = currentHardwareHash,
            Nonce = nonce,
            RequestedAtUtc = DateTime.UtcNow.ToString("o")
        };
    }

    public ReactivationValidationResult ProcessOfflineResponse(string licRespJson)
    {
        if (string.IsNullOrWhiteSpace(licRespJson))
        {
            return ReactivationValidationResult.Failure("Response payload is empty.");
        }

        SignedArtifact<ReactivationPayload>? artifact;
        try
        {
            artifact = JsonSerializer.Deserialize<SignedArtifact<ReactivationPayload>>(licRespJson);
        }
        catch (Exception ex)
        {
            return ReactivationValidationResult.Failure($"Failed to deserialize response: {ex.Message}");
        }

        if (artifact == null || artifact.Payload == null || string.IsNullOrWhiteSpace(artifact.Signature))
        {
            return ReactivationValidationResult.Failure("Invalid or malformed signed response artifact.");
        }

        // 1. Verify Cryptographic Signature
        var canonicalJson = CanonicalJsonSerializer.Serialize(artifact.Payload);
        var isValidSignature = _signatureVerifier.VerifySignature(canonicalJson, artifact.Signature, _publicKeySpki);
        if (!isValidSignature)
        {
            return ReactivationValidationResult.Failure("Cryptographic signature verification failed. Response artifact is untrusted.");
        }

        var payload = artifact.Payload;

        // 2. Replay Attack Prevention Check (AT-21)
        if (_consumedNonces.Contains(payload.Nonce))
        {
            return ReactivationValidationResult.Failure("Replay attack detected: Nonce has already been consumed (AT-21).");
        }

        if (!_pendingNonces.Contains(payload.Nonce))
        {
            return ReactivationValidationResult.Failure("Replay attack detected or unrequested nonce (AT-21).");
        }

        // 3. Hardware Hash Verification
        var currentHardwareHash = _hardwareProvider.GetCompositeHash();
        if (!string.Equals(payload.NewHardwareHash, currentHardwareHash, StringComparison.OrdinalIgnoreCase))
        {
            return ReactivationValidationResult.Failure("Response hardware hash does not match the current machine hardware.");
        }

        // 4. Validity Period Check
        if (DateTime.TryParse(payload.ValidUntilUtc, out var validUntilUtc))
        {
            if (DateTime.UtcNow >= validUntilUtc)
            {
                return ReactivationValidationResult.Failure("Response authorization validity period has expired.");
            }
        }

        // 5. Commit and Invalidate Nonce
        _pendingNonces.Remove(payload.Nonce);
        _consumedNonces.Add(payload.Nonce);

        return ReactivationValidationResult.Success(payload);
    }

    private static string GenerateCryptographicNonce()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
