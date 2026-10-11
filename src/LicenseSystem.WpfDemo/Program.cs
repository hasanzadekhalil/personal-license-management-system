using System.Net.Http.Json;
using System.Text.Json;
using LicenseSystem.ClientSdk;
using LicenseSystem.ClientSdk.Hardware;
using LicenseSystem.ClientSdk.Policy;
using LicenseSystem.ClientSdk.Temporal;
using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Enums;
using LicenseSystem.Core.Models;

namespace LicenseSystem.WpfDemo;

public class Program
{
    private static MockHardwareFingerprintProvider _mockHardware = new();
    private static MockTimeProvider _mockTime = new();
    private static LicenseClient? _client;
    private static string _serverUrl = "http://localhost:5000";
    private static string _productCode = "PROD-DESKTOP-APP";
    private static string _publicKeySpki = string.Empty;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Fetch or load server public key
        await InitializeClientEnvironmentAsync();

        if (args.Length > 0)
        {
            await HandleCliArgumentsAsync(args);
            return;
        }

        await RunInteractiveMenuAsync();
    }

    private static async Task InitializeClientEnvironmentAsync()
    {
        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"{_serverUrl}/api/v1/license/public-key");
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (doc.TryGetProperty("publicKeySpki", out var pk))
                {
                    _publicKeySpki = pk.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Server offline, use fallback test public key
            var keyGen = new EcdsaP256SigningService();
            _publicKeySpki = keyGen.GetPublicKeySpki();
        }

        var options = new LicenseClientOptions
        {
            ProductCode = _productCode,
            ServerBaseUrl = _serverUrl,
            PublicKeySpki = _publicKeySpki,
            StorageDirectory = Path.Combine(AppContext.BaseDirectory, "LicenseStorage")
        };

        _client = new LicenseClient(options, null, _mockHardware, new EcdsaP256SignatureVerifier(), _mockTime);
    }

    private static async Task HandleCliArgumentsAsync(string[] args)
    {
        var cmd = args[0].ToLowerInvariant();
        switch (cmd)
        {
            case "info":
                PrintHardwareInfo();
                break;
            case "activate" when args.Length > 1:
                var key = args[1];
                Console.WriteLine($"[+] Activating license '{key}' against {_serverUrl}...");
                var actRes = await _client!.ActivateOnlineAsync(key);
                PrintStatusBadge(actRes);
                break;
            case "status":
                var initRes = await _client!.InitializeAsync();
                PrintStatusBadge(initRes);
                break;
            case "req-export" when args.Length > 2:
                var expKey = args[1];
                var outPath = args[2];
                await _client!.ExportOfflineReactivationRequestAsync(expKey, outPath);
                Console.WriteLine($"[+] Offline reactivation request exported to: {outPath}");
                break;
            case "resp-import" when args.Length > 1:
                var inPath = args[1];
                var impRes = await _client!.ImportOfflineReactivationResponseAsync(inPath);
                Console.WriteLine($"[+] Offline response processed:");
                PrintStatusBadge(impRes);
                break;
            default:
                await RunInteractiveMenuAsync();
                break;
        }
    }

    private static async Task RunInteractiveMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine("             PERSONAL LICENSE MANAGEMENT SYSTEM — DESKTOP DEMO                  ");
            Console.WriteLine("                  Owner-Controlled Licensing SDK & Runner                       ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            Console.WriteLine($" Product Code   : {_productCode}");
            Console.WriteLine($" License Server : {_serverUrl}");
            Console.WriteLine($" Virtual Time   : {_mockTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();

            Console.WriteLine("--- HARDWARE FINGERPRINT (3-COMPONENT SALTED SHA-256) ---");
            Console.WriteLine($" Motherboard ID : {_mockHardware.MotherboardId}");
            Console.WriteLine($" CPU ID         : {_mockHardware.CpuId}");
            Console.WriteLine($" Primary SSD ID : {_mockHardware.PrimaryDiskId}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" Composite Hash : {_mockHardware.GetCompositeHash()}");
            Console.ResetColor();
            Console.WriteLine();

            Console.Write(" Current License Status: ");
            PrintStatusBadge(_client!.CurrentStatus);
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- ACTIONS & SIMULATION MENU ---");
            Console.ResetColor();
            Console.WriteLine(" [1] Enter & Activate License Key (Online)");
            Console.WriteLine(" [2] Validate License (Online / Server Sync)");
            Console.WriteLine(" [3] Simulate Offline Mode & Check Grace Period (AT-11 / AT-19)");
            Console.WriteLine(" [4] Simulate Motherboard Replacement (AT-06 Hardware Mismatch)");
            Console.WriteLine(" [5] Simulate CPU Replacement (AT-07 Hardware Mismatch)");
            Console.WriteLine(" [6] Simulate Primary SSD Replacement (AT-08 Hardware Mismatch)");
            Console.WriteLine(" [7] Simulate Clock Rollback / Tampering (AT-20 Anti-Clock Rollback)");
            Console.WriteLine(" [8] Export Offline Reactivation Request (.licreq - AT-21)");
            Console.WriteLine(" [9] Import Offline Reactivation Response (.licresp - AT-21)");
            Console.WriteLine(" [R] Reset Hardware & Time to Initial Clean State");
            Console.WriteLine(" [Q] Quit Demo");
            Console.WriteLine();
            Console.Write("Select an option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(input)) continue;
            if (input == "Q") break;

            await HandleMenuOptionAsync(input);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }
    }

    private static async Task HandleMenuOptionAsync(string option)
    {
        switch (option)
        {
            case "1":
                Console.Write("\nEnter License Key [Press Enter for default: LIC-DEMO-2026-OK]: ");
                var key = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrEmpty(key))
                {
                    key = "LIC-DEMO-2026-OK";
                }
                Console.WriteLine($"[*] Contacting licensing server ({_serverUrl}) for product '{_productCode}'...");
                var res = await _client!.ActivateOnlineAsync(key);
                PrintStatusBadge(res);
                break;

            case "2":
                Console.WriteLine("\n[*] Validating against server...");
                var valRes = await _client!.ValidateOnlineAsync();
                PrintStatusBadge(valRes);
                break;

            case "3":
                Console.WriteLine("\n[+] Advancing offline time by 3 hours...");
                _mockTime.AdvanceTime(TimeSpan.FromHours(3));
                var offlineRes = await _client!.ValidateOnlineAsync(); // Fails online, evaluates grace
                PrintStatusBadge(offlineRes);
                break;

            case "4":
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[!] Simulating Motherboard swap: Replacing with new UUID...");
                Console.ResetColor();
                _mockHardware.ReplaceMotherboard("NEW-REPLACED-MB-" + Guid.NewGuid().ToString("N")[..8]);
                var mbRes = await _client!.ValidateOnlineAsync();
                PrintStatusBadge(mbRes);
                break;

            case "5":
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[!] Simulating CPU swap: Replacing with upgraded Processor...");
                Console.ResetColor();
                _mockHardware.ReplaceCpu("NEW-REPLACED-CPU-" + Guid.NewGuid().ToString("N")[..8]);
                var cpuRes = await _client!.ValidateOnlineAsync();
                PrintStatusBadge(cpuRes);
                break;

            case "6":
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[!] Simulating SSD replacement: Replacing system drive...");
                Console.ResetColor();
                _mockHardware.ReplacePrimaryDisk("NEW-REPLACED-NVME-" + Guid.NewGuid().ToString("N")[..8]);
                var diskRes = await _client!.ValidateOnlineAsync();
                PrintStatusBadge(diskRes);
                break;

            case "7":
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[!] Simulating clock rollback: Rolling back clock by 5 days...");
                Console.ResetColor();
                _mockTime.RollbackTime(TimeSpan.FromDays(5));
                var clockRes = await _client!.ValidateOnlineAsync();
                PrintStatusBadge(clockRes);
                break;

            case "8":
                Console.Write("\nEnter License Key to request reactivation for: ");
                var reqKey = Console.ReadLine()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(reqKey))
                {
                    var reqFile = Path.Combine(AppContext.BaseDirectory, "client_request.licreq");
                    await _client!.ExportOfflineReactivationRequestAsync(reqKey, reqFile);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[✓] Successfully exported signed .licreq to: {reqFile}");
                    Console.ResetColor();
                }
                break;

            case "9":
                Console.Write("\nEnter path to approved .licresp file (or press enter for default): ");
                var respFile = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrEmpty(respFile))
                {
                    respFile = Path.Combine(AppContext.BaseDirectory, "admin_response.licresp");
                }
                if (File.Exists(respFile))
                {
                    var impRes = await _client!.ImportOfflineReactivationResponseAsync(respFile);
                    PrintStatusBadge(impRes);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"File '{respFile}' does not exist.");
                    Console.ResetColor();
                }
                break;

            case "R":
                _mockHardware = new MockHardwareFingerprintProvider();
                _mockTime = new MockTimeProvider();
                await InitializeClientEnvironmentAsync();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[✓] Environment reset to initial state.");
                Console.ResetColor();
                break;

            default:
                Console.WriteLine("Invalid option.");
                break;
        }
    }

    private static void PrintStatusBadge(PolicyEvaluationResult res)
    {
        if (res.IsAuthorized)
        {
            if (res.RemainingGracePeriod.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[GRACE PERIOD ACTIVE] Remaining: {res.RemainingGracePeriod.Value.TotalHours:F1}h - {res.Message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[ACTIVE & AUTHORIZED] {res.Message}");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[LOCKED - {res.Status}] {res.Message}");
        }
        Console.ResetColor();
    }

    private static void PrintHardwareInfo()
    {
        var hw = new WindowsHardwareFingerprintProvider();
        var info = hw.GetHardwareInfo();
        Console.WriteLine("Hardware Diagnostic Information:");
        Console.WriteLine($"Motherboard ID : {info.MotherboardId}");
        Console.WriteLine($"CPU ID         : {info.CpuId}");
        Console.WriteLine($"Primary Disk ID: {info.PrimaryDiskId}");
        Console.WriteLine($"Composite Hash : {hw.GetCompositeHash()}");
    }
}
