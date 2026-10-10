# HO-04: Client SDK Integration Guide for Desktop Applications

**Component:** `LicenseSystem.ClientSdk`  
**Target Runtimes:** .NET 8.0 / .NET Framework 4.8+ / Windows Desktop (WPF, WinForms, Avalonia, Console)  
**Dependencies:** Pure BCL (`System.Security.Cryptography`, `System.Text.Json`, `System.Net.Http`) — Zero third-party runtime dependencies

---

## 1. Adding the SDK Reference

Add a project or package reference to `LicenseSystem.ClientSdk` in your desktop application's `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\LicenseSystem.ClientSdk\LicenseSystem.ClientSdk.csproj" />
</ItemGroup>
```

---

## 2. Configuration & Initialization

Initialize `LicenseClient` during application startup (e.g., in `App.xaml.cs` or `Program.cs`):

```csharp
using LicenseSystem.ClientSdk;
using LicenseSystem.Core.Enums;

// 1. Configure client options
var options = new LicenseClientOptions
{
    ProductCode = "MY-DESKTOP-APP",
    ServerBaseUrl = "https://licensing.yourdomain.com",
    
    // Embed the server's public key (SubjectPublicKeyInfo Base64)
    // NEVER embed the private key on the client!
    PublicKeySpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...YOUR_PUBLIC_KEY...",
    
    // Local directory to store signed license artifacts
    StorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "MyCompany", "MyDesktopApp")
};

// 2. Instantiate client
var licenseClient = new LicenseClient(options);

// 3. Initialize and check local license state on startup
var startupResult = await licenseClient.InitializeAsync();

if (startupResult.IsAuthorized)
{
    if (startupResult.RemainingGracePeriod.HasValue)
    {
        // Operating under offline grace period (e.g., traveling without internet)
        ShowNotification($"Running in offline grace mode. Remaining: {startupResult.RemainingGracePeriod.Value.TotalHours:F1} hours.");
    }
    
    // Launch main application window
    LaunchApplication();
}
else
{
    // Application is locked: show activation or reactivation dialog
    ShowActivationDialog(startupResult.Message);
}
```

---

## 3. Online Activation Workflow

When a customer inputs their purchase key into your application's activation dialog:

```csharp
public async Task OnActivateButtonClicked(string userEnteredKey)
{
    var result = await licenseClient.ActivateOnlineAsync(userEnteredKey);

    if (result.IsAuthorized)
    {
        MessageBox.Show("License successfully activated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        LaunchMainWindow();
    }
    else
    {
        MessageBox.Show($"Activation failed: {result.Message}", "Activation Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

---

## 4. Background Heartbeat & Periodic Validation

To ensure real-time response to revocations and maintain the 6-hour offline grace watermark, schedule a background timer:

```csharp
var heartbeatTimer = new PeriodicTimer(TimeSpan.FromHours(1));

_ = Task.Run(async () =>
{
    while (await heartbeatTimer.WaitForNextTickAsync())
    {
        var validation = await licenseClient.ValidateOnlineAsync();
        if (!validation.IsAuthorized)
        {
            // License revoked, expired, or locked on server
            Dispatcher.Invoke(() => LockApplicationAndExit(validation.Message));
            break;
        }
    }
});
```

---

## 5. Offline Reactivation Workflow (Hardware Replacement)

When a customer replaces their motherboard or SSD, `licenseClient.CurrentStatus.Status` becomes `LockedHardwareMismatch`. Provide an offline challenge-response workflow:

```csharp
// Step 1: Export Challenge File (.licreq)
public async Task ExportChallengeFile(string savePath)
{
    var currentLicenseKey = licenseClient.CurrentLicense.Payload.LicenseKey;
    await licenseClient.ExportOfflineReactivationRequestAsync(currentLicenseKey, savePath);
    MessageBox.Show($"Request exported to {savePath}. Please send this file to support.");
}

// Step 2: Import Signed Response File (.licresp)
public async Task ImportResponseFile(string responseFilePath)
{
    var result = await licenseClient.ImportOfflineReactivationResponseAsync(responseFilePath);
    if (result.IsAuthorized)
    {
        MessageBox.Show("Hardware successfully re-authorized! Application unlocked.", "Success");
        LaunchMainWindow();
    }
    else
    {
        MessageBox.Show($"Reactivation failed: {result.Message}", "Error");
    }
}
```

---

## 6. Obfuscar Configuration for Release Builds

Protect your compiled desktop binaries against decompilation using the included `obfuscar.xml`:

1. Add Obfuscar NuGet package to your project:
   ```xml
   <PackageReference Include="Obfuscar" Version="2.2.38" PrivateAssets="all" />
   ```
2. Copy `obfuscar.xml` into your project root.
3. Configure post-build step in your `.csproj`:
   ```xml
   <Target Name="ObfuscateRelease" AfterTargets="AfterBuild" Condition="'$(Configuration)' == 'Release'">
     <Exec Command="$(Obfuscar) obfuscar.xml" />
   </Target>
   ```
