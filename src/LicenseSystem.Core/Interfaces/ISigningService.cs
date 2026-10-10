namespace LicenseSystem.Core.Interfaces;

public interface ISigningService
{
    string SignData(string canonicalData);
    string GetPublicKeySpki();
}
