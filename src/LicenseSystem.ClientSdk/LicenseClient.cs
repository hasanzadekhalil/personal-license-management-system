using System.Net.Http.Json;
using System.Text.Json;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Policy;
using LicenseSystem.ClientSdk.Reactivation;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;

namespace LicenseSystem.ClientSdk;

public class LicenseClientOptions
{
    public string ProductCode { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = "http://localhost:5000";
    public string PublicKeySpki { get; set; } = string.Empty;
    public string StorageDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LicenseSystem");
}

public class LicenseClient
{
    private readonly LicenseClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IHardwareFingerprintProvider _hardwareProvider;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly ITimeProvider _timeProvider;
    private readonly LicensePolicyEngine _policyEngine;
    private readonly ReactivationHandler _reactivationHandler;

    private SignedArtifact<LicensePayload>? _currentLicense;
    private SignedArtifact<ReactivationPayload>? _currentReactivationReceipt;
    private PolicyEvaluationResult _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, "License not initialized.");

    public LicenseClient(
        LicenseClientOptions options,
        HttpClient? httpClient = null,
        IHardwareFingerprintProvider? hardwareProvider = null,
        ISignatureVerifier? signatureVerifier = null,
        ITimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient();
        _hardwareProvider = hardwareProvider ?? new WindowsHardwareFingerprintProvider();
        _signatureVerifier = signatureVerifier ?? new EcdsaP256SignatureVerifier();
        _timeProvider = timeProvider ?? new SystemTimeProvider();

        _policyEngine = new LicensePolicyEngine(
            _signatureVerifier,
            _hardwareProvider,
            _timeProvider,
            _options.PublicKeySpki);

        _reactivationHandler = new ReactivationHandler(
            _hardwareProvider,
            _signatureVerifier,
            _options.PublicKeySpki);
    }

    public PolicyEvaluationResult CurrentStatus => _currentStatus;
    public SignedArtifact<LicensePayload>? CurrentLicense => _currentLicense;
    public SignedArtifact<ReactivationPayload>? CurrentReactivationReceipt => _currentReactivationReceipt;
    public IHardwareFingerprintProvider HardwareProvider => _hardwareProvider;
    public string ProductCode => _options.ProductCode;

    public void LoadLicenseDirectly(SignedArtifact<LicensePayload> artifact, SignedArtifact<ReactivationPayload>? reactivationReceipt = null)
    {
        _currentLicense = artifact;
        _currentReactivationReceipt = reactivationReceipt;
        EvaluateLocalPolicy(isOnlineAvailable: false, serverValidationSucceeded: false);
    }

    public async Task<PolicyEvaluationResult> InitializeAsync()
    {
        EnsureStorageDirectory();
        var licensePath = Path.Combine(_options.StorageDirectory, $"{_options.ProductCode}.license");
        var receiptPath = Path.Combine(_options.StorageDirectory, $"{_options.ProductCode}.licresp");

        if (File.Exists(receiptPath))
        {
            try
            {
                var receiptJson = await File.ReadAllTextAsync(receiptPath);
                _currentReactivationReceipt = JsonSerializer.Deserialize<SignedArtifact<ReactivationPayload>>(receiptJson);
            }
            catch
            {
                _currentReactivationReceipt = null;
            }
        }

        if (File.Exists(licensePath))
        {
            try
            {
                var licenseJson = await File.ReadAllTextAsync(licensePath);
                _currentLicense = JsonSerializer.Deserialize<SignedArtifact<LicensePayload>>(licenseJson);
            }
            catch
            {
                _currentLicense = null;
            }
        }

        if (_currentLicense == null)
        {
            _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, "No license installed on this machine.");
            return _currentStatus;
        }

        // Try online validation if possible, otherwise offline evaluate
        return await ValidateOnlineAsync();
    }

    public async Task<PolicyEvaluationResult> ActivateOnlineAsync(string licenseKey, string? deviceName = null)
    {
        var hwHash = _hardwareProvider.GetCompositeHash();
        var requestPayload = new
        {
            ProductCode = _options.ProductCode,
            LicenseKey = licenseKey,
            HardwareHash = hwHash,
            DeviceName = deviceName ?? Environment.MachineName,
            MotherboardId = "Detected",
            CpuId = "Detected",
            PrimaryDiskId = "Detected"
        };

        try
        {
            var url = $"{_options.ServerBaseUrl.TrimEnd('/')}/api/v1/license/activate";
            var response = await _httpClient.PostAsJsonAsync(url, requestPayload);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _currentStatus = PolicyEvaluationResult.Locked(
                    LicenseStatus.Revoked,
                    $"Activation rejected by server (HTTP {(int)response.StatusCode}): {errorBody}");
                return _currentStatus;
            }

            var resultDoc = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (resultDoc.TryGetProperty("signedLicenseJson", out var signedJsonElem))
            {
                var signedJson = signedJsonElem.GetString();
                if (!string.IsNullOrEmpty(signedJson))
                {
                    _currentLicense = JsonSerializer.Deserialize<SignedArtifact<LicensePayload>>(signedJson);
                    await SaveLicenseToDiskAsync(_currentLicense!);
                    _policyEngine.RecordSuccessfulOnlineSync(_timeProvider.UtcNow);

                    return EvaluateLocalPolicy(isOnlineAvailable: true, serverValidationSucceeded: true);
                }
            }

            _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, "Invalid response received from activation server.");
            return _currentStatus;
        }
        catch (Exception ex)
        {
            _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, $"Network connection failed during online activation: {ex.Message}");
            return _currentStatus;
        }
    }

    public async Task<PolicyEvaluationResult> ValidateOnlineAsync()
    {
        if (_currentLicense == null)
        {
            _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, "No license installed.");
            return _currentStatus;
        }

        var hwHash = _hardwareProvider.GetCompositeHash();
        var requestPayload = new
        {
            LicenseKey = _currentLicense.Payload.LicenseKey,
            HardwareHash = hwHash
        };

        try
        {
            var url = $"{_options.ServerBaseUrl.TrimEnd('/')}/api/v1/license/validate";
            var response = await _httpClient.PostAsJsonAsync(url, requestPayload);

            if (response.IsSuccessStatusCode)
            {
                _policyEngine.RecordSuccessfulOnlineSync(_timeProvider.UtcNow);
                return EvaluateLocalPolicy(isOnlineAvailable: true, serverValidationSucceeded: true);
            }

            // Server returned error (e.g. revoked or hardware mismatch on server)
            return EvaluateLocalPolicy(isOnlineAvailable: true, serverValidationSucceeded: false);
        }
        catch
        {
            // Offline / Network unreachable
            return EvaluateLocalPolicy(isOnlineAvailable: false, serverValidationSucceeded: false);
        }
    }

    public async Task<bool> SendHeartbeatAsync()
    {
        if (_currentLicense == null) return false;

        try
        {
            var url = $"{_options.ServerBaseUrl.TrimEnd('/')}/api/v1/license/heartbeat";
            var response = await _httpClient.PostAsJsonAsync(url, new
            {
                LicenseKey = _currentLicense.Payload.LicenseKey,
                HardwareHash = _hardwareProvider.GetCompositeHash()
            });

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public OfflineReactivationRequest GenerateOfflineReactivationRequest(string licenseKey)
    {
        return _reactivationHandler.GenerateOfflineRequest(licenseKey);
    }

    public async Task<string> ExportOfflineReactivationRequestAsync(string licenseKey, string targetFilePath)
    {
        var request = GenerateOfflineReactivationRequest(licenseKey);
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(targetFilePath, json);
        return json;
    }

    public async Task<PolicyEvaluationResult> ImportOfflineReactivationResponseAsync(string jsonOrFilePath)
    {
        string jsonContent;
        if (File.Exists(jsonOrFilePath))
        {
            jsonContent = await File.ReadAllTextAsync(jsonOrFilePath);
        }
        else
        {
            jsonContent = jsonOrFilePath;
        }

        var result = _reactivationHandler.ProcessOfflineResponse(jsonContent);
        if (!result.IsSuccess || result.Payload == null)
        {
            _currentStatus = PolicyEvaluationResult.Locked(
                LicenseStatus.LockedHardwareMismatch,
                $"Offline reactivation failed: {result.ErrorMessage}");
            return _currentStatus;
        }

        // Store the approved reactivation artifact
        _currentReactivationReceipt = JsonSerializer.Deserialize<SignedArtifact<ReactivationPayload>>(jsonContent);

        if (!string.IsNullOrEmpty(_options.StorageDirectory))
        {
            EnsureStorageDirectory();
            var receiptPath = Path.Combine(_options.StorageDirectory, $"{_options.ProductCode}.licresp");
            await File.WriteAllTextAsync(receiptPath, jsonContent);
        }

        return EvaluateLocalPolicy(isOnlineAvailable: false, serverValidationSucceeded: false);
    }

    private PolicyEvaluationResult EvaluateLocalPolicy(bool isOnlineAvailable, bool serverValidationSucceeded)
    {
        if (_currentLicense == null)
        {
            _currentStatus = PolicyEvaluationResult.Locked(LicenseStatus.Revoked, "No license installed.");
            return _currentStatus;
        }

        _currentStatus = _policyEngine.Evaluate(
            _currentLicense,
            _options.ProductCode,
            isOnlineAvailable,
            serverValidationSucceeded,
            _currentReactivationReceipt);

        return _currentStatus;
    }

    private void EnsureStorageDirectory()
    {
        if (!string.IsNullOrEmpty(_options.StorageDirectory) && !Directory.Exists(_options.StorageDirectory))
        {
            Directory.CreateDirectory(_options.StorageDirectory);
        }
    }

    private async Task SaveLicenseToDiskAsync(SignedArtifact<LicensePayload> license)
    {
        if (string.IsNullOrEmpty(_options.StorageDirectory)) return;
        EnsureStorageDirectory();
        var path = Path.Combine(_options.StorageDirectory, $"{_options.ProductCode}.license");
        var json = CanonicalJsonSerializer.Serialize(license);
        await File.WriteAllTextAsync(path, json);
    }
}
