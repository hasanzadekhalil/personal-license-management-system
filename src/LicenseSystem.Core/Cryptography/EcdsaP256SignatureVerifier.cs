using System.Security.Cryptography;
using System.Text;
using LicenseSystem.Core.Interfaces;

namespace LicenseSystem.Core.Cryptography;

public class EcdsaP256SignatureVerifier : ISignatureVerifier
{
    public bool VerifySignature(string canonicalData, string signatureBase64, string publicKeySpki)
    {
        if (string.IsNullOrWhiteSpace(canonicalData) ||
            string.IsNullOrWhiteSpace(signatureBase64) ||
            string.IsNullOrWhiteSpace(publicKeySpki))
        {
            return false;
        }

        try
        {
            var cleanedKey = publicKeySpki.Trim();
            using var ecdsa = ECDsa.Create();

            if (cleanedKey.StartsWith("-----BEGIN"))
            {
                ecdsa.ImportFromPem(cleanedKey);
            }
            else
            {
                var spkiBytes = Convert.FromBase64String(cleanedKey);
                ecdsa.ImportSubjectPublicKeyInfo(spkiBytes, out _);
            }

            var dataBytes = Encoding.UTF8.GetBytes(canonicalData);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
