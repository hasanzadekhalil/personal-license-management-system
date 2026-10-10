namespace LicenseSystem.Core.Interfaces;

public interface ISignatureVerifier
{
    bool VerifySignature(string canonicalData, string signatureBase64, string publicKeySpki);
}
