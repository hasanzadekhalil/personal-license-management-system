using System.Security.Cryptography;
using System.Text;
using LicenseSystem.Core.Interfaces;

namespace LicenseSystem.Core.Cryptography;

public class EcdsaP256SigningService : ISigningService, IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly bool _ownsKey;

    public EcdsaP256SigningService()
    {
        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _ownsKey = true;
    }

    public EcdsaP256SigningService(string privateKeyPemOrBase64)
    {
        _ecdsa = ECDsa.Create();
        _ownsKey = true;
        LoadPrivateKey(privateKeyPemOrBase64);
    }

    public EcdsaP256SigningService(ECDsa ecdsa)
    {
        _ecdsa = ecdsa ?? throw new ArgumentNullException(nameof(ecdsa));
        _ownsKey = false;
    }

    private void LoadPrivateKey(string keyData)
    {
        var cleaned = keyData.Trim();
        if (cleaned.StartsWith("-----BEGIN"))
        {
            _ecdsa.ImportFromPem(cleaned);
        }
        else
        {
            var rawBytes = Convert.FromBase64String(cleaned);
            _ecdsa.ImportPkcs8PrivateKey(rawBytes, out _);
        }
    }

    public string SignData(string canonicalData)
    {
        if (canonicalData == null) throw new ArgumentNullException(nameof(canonicalData));

        var dataBytes = Encoding.UTF8.GetBytes(canonicalData);
        var signatureBytes = _ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signatureBytes);
    }

    public string GetPublicKeySpki()
    {
        var spkiBytes = _ecdsa.ExportSubjectPublicKeyInfo();
        return Convert.ToBase64String(spkiBytes);
    }

    public string ExportPrivateKeyPkcs8()
    {
        var pkcs8Bytes = _ecdsa.ExportPkcs8PrivateKey();
        return Convert.ToBase64String(pkcs8Bytes);
    }

    public void Dispose()
    {
        if (_ownsKey)
        {
            _ecdsa.Dispose();
        }
    }
}
