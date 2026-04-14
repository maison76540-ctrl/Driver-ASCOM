# ArduSafeMon Alpaca Driver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le driver COM in-process (qui crashe NINA) par un serveur HTTP Alpaca autonome (.NET 8) que NINA 3.3 découvre automatiquement et interroge via REST sur localhost.

**Architecture:** Un EXE .NET 8 avec ASP.NET Core (Kestrel) expose l'API REST ASCOM Alpaca sur `http://localhost:11111`. Un service UDP sur le port 32227 répond aux broadcasts de découverte de NINA. La communication série avec l'Arduino est synchrone, dans le thread appelant, sans thread background — zéro race condition, zéro crash possible. En cas d'erreur (port introuvable, Arduino déconnecté), une réponse Alpaca d'erreur est retournée et NINA affiche un message propre.

**Tech Stack:** .NET 8 SDK, ASP.NET Core Minimal APIs, System.IO.Ports (NuGet), NUnit 3.13, InnoSetup 6

---

## Structure des fichiers

```
ArduSafeMon/ (racine du dépôt)
├── ArduSafeMonAlpaca/
│   ├── ArduSafeMonAlpaca.sln
│   ├── ArduSafeMonAlpaca/
│   │   ├── ArduSafeMonAlpaca.csproj   .NET 8 Web, publish self-contained win-x64
│   │   ├── appsettings.json            config (AlpacaPort, ComPort, SimulationMode…)
│   │   ├── AppSettings.cs              classe de config liée à appsettings.json
│   │   ├── AlpacaResponse.cs           DTOs génériques + factory AlpacaResult
│   │   ├── SafetyMonitorDevice.cs      logique série + simulation + cache IsSafe
│   │   ├── DiscoveryService.cs         IHostedService UDP port 32227
│   │   └── Program.cs                  point d'entrée, DI, routes Alpaca
│   └── ArduSafeMonAlpaca.Tests/
│       ├── ArduSafeMonAlpaca.Tests.csproj
│       ├── AlpacaResponseTests.cs
│       └── SafetyMonitorDeviceTests.cs
└── ArduSafeMonAlpacaSetup.iss          InnoSetup : installe EXE + raccourci démarrage
```

---

### Prérequis : Installer le SDK .NET 8

- [ ] **Étape 1 : Télécharger et installer le SDK .NET 8**

Aller sur https://dotnet.microsoft.com/download/dotnet/8.0  
Télécharger **SDK 8.0.x — Windows x64 Installer** et l'installer.

- [ ] **Étape 2 : Vérifier l'installation**

Ouvrir une nouvelle invite de commandes (pas PowerShell) et exécuter :
```
dotnet --version
```
Résultat attendu : `8.0.xxx`

---

### Task 1 : Créer la structure du projet

**Files:**
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca.sln`
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj`
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca.Tests/ArduSafeMonAlpaca.Tests.csproj`

- [ ] **Étape 1 : Créer le dossier et la solution**

Depuis la racine du dépôt (`C:\Users\dalex\Desktop\Driver ASCOM pour Capteur de toit\ArduSafeMon`) :

```cmd
mkdir ArduSafeMonAlpaca
cd ArduSafeMonAlpaca
dotnet new sln -n ArduSafeMonAlpaca
```

- [ ] **Étape 2 : Créer le projet principal**

```cmd
dotnet new web -n ArduSafeMonAlpaca --framework net8.0
dotnet sln add ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj
```

- [ ] **Étape 3 : Ajouter le paquet System.IO.Ports**

```cmd
dotnet add ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj package System.IO.Ports --version 8.0.0
```

- [ ] **Étape 4 : Créer le projet de tests**

```cmd
dotnet new nunit -n ArduSafeMonAlpaca.Tests --framework net8.0
dotnet sln add ArduSafeMonAlpaca.Tests/ArduSafeMonAlpaca.Tests.csproj
dotnet add ArduSafeMonAlpaca.Tests/ArduSafeMonAlpaca.Tests.csproj reference ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj
```

- [ ] **Étape 5 : Remplacer le csproj principal**

Écraser `ArduSafeMonAlpaca/ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj` avec :

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>ArduSafeMonAlpaca</AssemblyName>
    <RootNamespace>ArduSafeMonAlpaca</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Étape 6 : Vérifier que la solution compile**

```cmd
dotnet build ArduSafeMonAlpaca.sln
```

Résultat attendu : `Build succeeded.`

- [ ] **Étape 7 : Commit**

```cmd
cd ..
git add ArduSafeMonAlpaca/
git commit -m "feat: scaffold .NET 8 Alpaca project structure"
```

---

### Task 2 : AlpacaResponse DTOs + tests

**Files:**
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/AlpacaResponse.cs`
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca.Tests/AlpacaResponseTests.cs`
- Delete: le `Program.cs` généré par `dotnet new web` (on le récrira en Task 6)

- [ ] **Étape 1 : Écrire le test**

Créer `ArduSafeMonAlpaca/ArduSafeMonAlpaca.Tests/AlpacaResponseTests.cs` :

```csharp
using NUnit.Framework;
using ArduSafeMonAlpaca;

namespace ArduSafeMonAlpaca.Tests;

[TestFixture]
public class AlpacaResponseTests
{
    [Test]
    public void Ok_setsValueAndZeroError()
    {
        var r = AlpacaResult.Ok("hello");
        Assert.That(r.Value, Is.EqualTo("hello"));
        Assert.That(r.ErrorNumber, Is.EqualTo(0));
        Assert.That(r.ErrorMessage, Is.EqualTo(""));
        Assert.That(r.ServerTransactionID, Is.GreaterThan(0));
    }

    [Test]
    public void Ok_propagatesClientTransactionId()
    {
        var r = AlpacaResult.Ok(true, clientTxId: 42);
        Assert.That(r.ClientTransactionID, Is.EqualTo(42));
    }

    [Test]
    public void Fail_setsErrorNumberAndMessage()
    {
        var r = AlpacaResult.Fail<bool>(0x0400, "Not connected");
        Assert.That(r.ErrorNumber, Is.EqualTo(0x0400));
        Assert.That(r.ErrorMessage, Is.EqualTo("Not connected"));
        Assert.That(r.Value, Is.False);
    }

    [Test]
    public void ServerTransactionId_incrementsOnEachCall()
    {
        var r1 = AlpacaResult.Ok(1);
        var r2 = AlpacaResult.Ok(2);
        Assert.That(r2.ServerTransactionID, Is.GreaterThan(r1.ServerTransactionID));
    }
}
```

- [ ] **Étape 2 : Vérifier que le test échoue**

```cmd
cd ArduSafeMonAlpaca
dotnet test ArduSafeMonAlpaca.Tests/ArduSafeMonAlpaca.Tests.csproj --filter AlpacaResponseTests
```

Résultat attendu : erreur de compilation (AlpacaResult n'existe pas encore).

- [ ] **Étape 3 : Implémenter AlpacaResponse.cs**

Créer `ArduSafeMonAlpaca/ArduSafeMonAlpaca/AlpacaResponse.cs` :

```csharp
namespace ArduSafeMonAlpaca;

/// <summary>
/// Réponse générique ASCOM Alpaca.
/// Tous les endpoints retournent ce type sérialisé en JSON.
/// </summary>
public record AlpacaResponse<T>(
    T Value,
    int ClientTransactionID,
    int ServerTransactionID,
    int ErrorNumber,
    string ErrorMessage);

/// <summary>Factory de réponses Alpaca avec compteur de transaction auto-incrémenté.</summary>
public static class AlpacaResult
{
    private static int _txId;

    public static AlpacaResponse<T> Ok<T>(T value, int clientTxId = 0) =>
        new(value, clientTxId, Interlocked.Increment(ref _txId), 0, "");

    public static AlpacaResponse<T> Fail<T>(int errorNumber, string message,
        T defaultValue = default!, int clientTxId = 0) =>
        new(defaultValue, clientTxId, Interlocked.Increment(ref _txId), errorNumber, message);
}
```

- [ ] **Étape 4 : Vérifier que les tests passent**

```cmd
dotnet test ArduSafeMonAlpaca.Tests/ArduSafeMonAlpaca.Tests.csproj --filter AlpacaResponseTests
```

Résultat attendu : `4 passed`.

- [ ] **Étape 5 : Commit**

```cmd
cd ..
git add ArduSafeMonAlpaca/
git commit -m "feat: add AlpacaResponse DTOs and factory"
```

---

### Task 3 : AppSettings + appsettings.json

**Files:**
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/AppSettings.cs`
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/appsettings.json`

- [ ] **Étape 1 : Créer AppSettings.cs**

```csharp
namespace ArduSafeMonAlpaca;

/// <summary>
/// Configuration chargée depuis appsettings.json.
/// Modifier appsettings.json (à côté de l'EXE) pour changer le port COM, etc.
/// </summary>
public class AppSettings
{
    /// <summary>Port HTTP du serveur Alpaca (défaut : 11111).</summary>
    public int AlpacaPort { get; set; } = 11111;

    /// <summary>Port série de l'Arduino (ex : "COM3").</summary>
    public string ComPort { get; set; } = "COM3";

    /// <summary>Intervalle de polling de l'Arduino en millisecondes (défaut : 2000).</summary>
    public int PollIntervalMs { get; set; } = 2000;

    /// <summary>Si true, retourne SimulatedSafe sans ouvrir le port série.</summary>
    public bool SimulationMode { get; set; } = false;

    /// <summary>Valeur retournée par IsSafe en mode simulation.</summary>
    public bool SimulatedSafe { get; set; } = true;
}
```

- [ ] **Étape 2 : Créer appsettings.json**

Remplacer/créer `ArduSafeMonAlpaca/ArduSafeMonAlpaca/appsettings.json` :

```json
{
  "AlpacaPort": 11111,
  "ComPort": "COM3",
  "PollIntervalMs": 2000,
  "SimulationMode": false,
  "SimulatedSafe": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Étape 3 : Commit**

```cmd
git add ArduSafeMonAlpaca/
git commit -m "feat: add AppSettings configuration class and appsettings.json"
```

---

### Task 4 : SafetyMonitorDevice + tests

**Files:**
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/SafetyMonitorDevice.cs`
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca.Tests/SafetyMonitorDeviceTests.cs`

- [ ] **Étape 1 : Écrire les tests**

Créer `ArduSafeMonAlpaca/ArduSafeMonAlpaca.Tests/SafetyMonitorDeviceTests.cs` :

```csharp
using NUnit.Framework;
using ArduSafeMonAlpaca;

namespace ArduSafeMonAlpaca.Tests;

[TestFixture]
public class SafetyMonitorDeviceTests
{
    // ── ParseResponse ──────────────────────────────────────────────────────

    [Test]
    public void ParseResponse_safe_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("safe"), Is.True);

    [Test]
    public void ParseResponse_SAFE_uppercase_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("SAFE"), Is.True);

    [Test]
    public void ParseResponse_safeWithSpaces_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("  safe  "), Is.True);

    [Test]
    public void ParseResponse_notsafe_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse("notsafe"), Is.False);

    [Test]
    public void ParseResponse_empty_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse(""), Is.False);

    [Test]
    public void ParseResponse_null_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse(null!), Is.False);

    // ── Simulation mode ────────────────────────────────────────────────────

    [Test]
    public void Connect_simulationMode_doesNotThrow()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        Assert.DoesNotThrow(() => dev.Connect());
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_simulationModeSafe_returnsTrue()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = true });
        dev.Connect();
        Assert.That(dev.IsSafe, Is.True);
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_simulationModeNotSafe_returnsFalse()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = false });
        dev.Connect();
        Assert.That(dev.IsSafe, Is.False);
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_notConnected_returnsFalse()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = true });
        // Pas de Connect()
        Assert.That(dev.IsSafe, Is.False);
    }

    [Test]
    public void Connected_afterConnect_isTrue()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        dev.Connect();
        Assert.That(dev.Connected, Is.True);
        dev.Disconnect();
    }

    [Test]
    public void Connected_afterDisconnect_isFalse()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        dev.Connect();
        dev.Disconnect();
        Assert.That(dev.Connected, Is.False);
    }

    [Test]
    public void Connect_portNotFound_throwsWithMessage()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = false, ComPort = "COM99" });
        var ex = Assert.Throws<InvalidOperationException>(() => dev.Connect());
        Assert.That(ex!.Message, Does.Contain("COM99"));
    }
}
```

- [ ] **Étape 2 : Vérifier que les tests échouent**

```cmd
cd ArduSafeMonAlpaca
dotnet test ArduSafeMonAlpaca.Tests/ --filter SafetyMonitorDeviceTests
```

Résultat attendu : erreur de compilation (SafetyMonitorDevice n'existe pas).

- [ ] **Étape 3 : Implémenter SafetyMonitorDevice.cs**

Créer `ArduSafeMonAlpaca/ArduSafeMonAlpaca/SafetyMonitorDevice.cs` :

```csharp
using System.IO.Ports;

namespace ArduSafeMonAlpaca;

/// <summary>
/// Gère la communication série avec l'Arduino et met en cache l'état IsSafe.
/// Thread-safe via lock. Aucun thread background — le polling est déclenché
/// par l'appel à IsSafe depuis le thread HTTP de NINA.
/// </summary>
public sealed class SafetyMonitorDevice : IDisposable
{
    private readonly AppSettings _settings;
    private readonly object _lock = new();

    private SerialPort? _port;
    private bool _connected;
    private bool _isSafe;
    private DateTime _lastPoll = DateTime.MinValue;

    public SafetyMonitorDevice(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>True si Connect() a réussi et Disconnect() n'a pas encore été appelé.</summary>
    public bool Connected { get { lock (_lock) return _connected; } }

    /// <summary>
    /// Retourne l'état de sécurité mis en cache.
    /// Si l'intervalle de polling est écoulé, interroge l'Arduino (max 2 s).
    /// Retourne false si non connecté ou en cas d'erreur série.
    /// </summary>
    public bool IsSafe
    {
        get
        {
            lock (_lock)
            {
                if (!_connected) return false;
                if (_settings.SimulationMode) return _settings.SimulatedSafe;

                bool intervalElapsed =
                    (DateTime.Now - _lastPoll).TotalMilliseconds >= _settings.PollIntervalMs;

                if (intervalElapsed)
                    RefreshSafe();

                return _isSafe;
            }
        }
    }

    /// <summary>
    /// Ouvre la connexion série.
    /// Lance InvalidOperationException avec message lisible si le port est introuvable.
    /// Cette exception remonte dans le PUT /connected de l'API Alpaca —
    /// NINA l'affiche comme message d'erreur dans son interface.
    /// </summary>
    public void Connect()
    {
        lock (_lock)
        {
            if (_connected) return;

            if (!_settings.SimulationMode)
                OpenPort(); // lève InvalidOperationException si port absent

            _connected = true;
            _lastPoll = DateTime.MinValue; // force un poll immédiat
            _isSafe = false;
        }
    }

    /// <summary>Ferme la connexion série. Idempotent.</summary>
    public void Disconnect()
    {
        lock (_lock)
        {
            _connected = false;
            _isSafe = false;
            ClosePort();
        }
    }

    public void Dispose() => Disconnect();

    // ── Logique série ────────────────────────────────────────────────────────

    private void RefreshSafe()
    {
        _lastPoll = DateTime.Now;
        try
        {
            if (_port == null || !_port.IsOpen)
                OpenPort();

            _port!.DiscardInBuffer();
            _port.Write("S#");
            string response = _port.ReadTo("#");
            _isSafe = ParseResponse(response);
        }
        catch
        {
            _isSafe = false;
            ClosePort(); // sera rouvert au prochain appel
        }
    }

    private void OpenPort()
    {
        ClosePort();

        // Vérifie que le port existe avant de tenter de l'ouvrir
        bool exists = false;
        try
        {
            foreach (string p in SerialPort.GetPortNames())
                if (string.Equals(p, _settings.ComPort, StringComparison.OrdinalIgnoreCase))
                { exists = true; break; }
        }
        catch { }

        if (!exists)
        {
            string available = string.Join(", ", SerialPort.GetPortNames());
            throw new InvalidOperationException(
                $"Port série '{_settings.ComPort}' introuvable. " +
                $"Ports disponibles : {(string.IsNullOrEmpty(available) ? "(aucun)" : available)}. " +
                "Vérifiez que l'Arduino est branché et modifiez ComPort dans appsettings.json.");
        }

        try
        {
            _port = new SerialPort(_settings.ComPort, 9600)
            {
                ReadTimeout  = 2000,
                WriteTimeout = 2000
            };
            _port.Open();
            Thread.Sleep(500);
            _port.DiscardInBuffer();
        }
        catch (Exception ex)
        {
            ClosePort();
            throw new InvalidOperationException(
                $"Impossible d'ouvrir '{_settings.ComPort}' : {ex.Message}");
        }
    }

    private void ClosePort()
    {
        try { _port?.Close(); }   catch { }
        try { _port?.Dispose(); } catch { }
        _port = null;
    }

    /// <summary>
    /// "safe" (insensible à la casse, espaces ignorés) → true.
    /// Tout autre valeur → false.
    /// </summary>
    internal static bool ParseResponse(string? response)
    {
        if (string.IsNullOrEmpty(response)) return false;
        return string.Equals(response.Trim(), "safe", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Étape 4 : Vérifier que les tests passent**

```cmd
dotnet test ArduSafeMonAlpaca.Tests/ --filter SafetyMonitorDeviceTests -v normal
```

Résultat attendu : `11 passed` (les tests "simulation" passent ; le test COM99 passe car le port n'existe pas sur la machine).

- [ ] **Étape 5 : Commit**

```cmd
cd ..
git add ArduSafeMonAlpaca/
git commit -m "feat: implement SafetyMonitorDevice with serial polling and simulation mode"
```

---

### Task 5 : DiscoveryService (UDP Alpaca)

**Files:**
- Create: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/DiscoveryService.cs`

- [ ] **Étape 1 : Implémenter DiscoveryService.cs**

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ArduSafeMonAlpaca;

/// <summary>
/// Écoute sur UDP port 32227 et répond aux broadcasts de découverte Alpaca de NINA.
/// Quand NINA démarre, il envoie "alpacadiscovery1" en broadcast ;
/// on répond avec le port HTTP du serveur → NINA découvre automatiquement le driver.
/// </summary>
public sealed class DiscoveryService : BackgroundService
{
    private readonly AppSettings _settings;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(AppSettings settings, ILogger<DiscoveryService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 32227));

            _logger.LogInformation(
                "Alpaca discovery listening on UDP 32227 → HTTP port {port}",
                _settings.AlpacaPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udp.ReceiveAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "UDP receive error, retrying in 1 s");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                string msg = Encoding.UTF8.GetString(result.Buffer);
                if (!msg.StartsWith("alpacadiscovery1", StringComparison.OrdinalIgnoreCase))
                    continue;

                string json = JsonSerializer.Serialize(
                    new { AlpacaPort = _settings.AlpacaPort });
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                try
                {
                    await udp.SendAsync(bytes, bytes.Length, result.RemoteEndPoint);
                    _logger.LogInformation(
                        "Discovery: replied to {ep}", result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reply to discovery from {ep}",
                        result.RemoteEndPoint);
                }
            }
        }
        finally
        {
            udp?.Dispose();
        }
    }
}
```

- [ ] **Étape 2 : Vérifier que la solution compile**

```cmd
cd ArduSafeMonAlpaca
dotnet build ArduSafeMonAlpaca.sln
```

Résultat attendu : `Build succeeded.`

- [ ] **Étape 3 : Commit**

```cmd
cd ..
git add ArduSafeMonAlpaca/
git commit -m "feat: add Alpaca UDP discovery service on port 32227"
```

---

### Task 6 : Program.cs — câblage complet

**Files:**
- Modify: `ArduSafeMonAlpaca/ArduSafeMonAlpaca/Program.cs` (remplacer le contenu généré)

- [ ] **Étape 1 : Écrire Program.cs**

Remplacer **entièrement** le contenu de `ArduSafeMonAlpaca/ArduSafeMonAlpaca/Program.cs` :

```csharp
using ArduSafeMonAlpaca;

// ── Configuration ────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var settings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<SafetyMonitorDevice>();
builder.Services.AddHostedService<DiscoveryService>();

builder.WebHost.UseUrls($"http://0.0.0.0:{settings.AlpacaPort}");

var app = builder.Build();

// ── Management API ───────────────────────────────────────────────────────────

app.MapGet("/management/apiversions", () =>
    Results.Json(AlpacaResult.Ok(new[] { 1 })));

app.MapGet("/management/v1/description", () =>
    Results.Json(AlpacaResult.Ok(new
    {
        ServerName    = "ArduSafeMon Alpaca Server",
        Manufacturer  = "dalex",
        ManufacturerVersion = "1.0",
        Location      = ""
    })));

app.MapGet("/management/v1/configureddevices", () =>
    Results.Json(AlpacaResult.Ok(new[]
    {
        new
        {
            DeviceName   = "ArduSafeMon",
            DeviceType   = "SafetyMonitor",
            DeviceNumber = 0,
            UniqueID     = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
        }
    })));

// ── Safety Monitor API ───────────────────────────────────────────────────────
const string P = "/api/v1/safetymonitor/0";

app.MapGet($"{P}/name",             () => Results.Json(AlpacaResult.Ok("ArduSafeMon")));
app.MapGet($"{P}/description",      () => Results.Json(AlpacaResult.Ok("Arduino Roof Safety Monitor")));
app.MapGet($"{P}/driverinfo",       () => Results.Json(AlpacaResult.Ok("ArduSafeMon Alpaca v1.0")));
app.MapGet($"{P}/driverversion",    () => Results.Json(AlpacaResult.Ok("1.0")));
app.MapGet($"{P}/interfaceversion", () => Results.Json(AlpacaResult.Ok(2)));
app.MapGet($"{P}/supportedactions", () => Results.Json(AlpacaResult.Ok(Array.Empty<string>())));

app.MapGet($"{P}/connected", (SafetyMonitorDevice dev) =>
    Results.Json(AlpacaResult.Ok(dev.Connected)));

// NINA envoie PUT /connected avec un form-body : Connected=True&ClientID=...
app.MapPut($"{P}/connected", async (HttpRequest req, SafetyMonitorDevice dev) =>
{
    var form  = await req.ReadFormAsync();
    bool connect = string.Equals(form["Connected"], "true",
        StringComparison.OrdinalIgnoreCase);
    int clientTx = int.TryParse(form["ClientTransactionID"], out var tx) ? tx : 0;

    try
    {
        if (connect) dev.Connect();
        else         dev.Disconnect();
        return Results.Json(AlpacaResult.Ok(connect, clientTx));
    }
    catch (Exception ex)
    {
        // 0x0500 = InvalidOperation — NINA affiche ex.Message à l'utilisateur
        return Results.Json(AlpacaResult.Fail<bool>(0x0500, ex.Message,
            defaultValue: false, clientTxId: clientTx));
    }
});

app.MapGet($"{P}/issafe", (SafetyMonitorDevice dev) =>
{
    if (!dev.Connected)
        return Results.Json(AlpacaResult.Fail<bool>(0x0400, "Not connected"));
    return Results.Json(AlpacaResult.Ok(dev.IsSafe));
});

// ── Démarrage ────────────────────────────────────────────────────────────────
Console.WriteLine($"ArduSafeMon Alpaca  ▶  http://localhost:{settings.AlpacaPort}");
Console.WriteLine($"COM port : {settings.ComPort}  |  Poll : {settings.PollIntervalMs} ms" +
                  (settings.SimulationMode
                      ? $"  |  SIMULATION ({(settings.SimulatedSafe ? "Safe" : "Unsafe")})"
                      : ""));
Console.WriteLine("Appuyez sur Ctrl+C pour arrêter.");

app.Run();
```

- [ ] **Étape 2 : Compiler et tester manuellement**

```cmd
cd ArduSafeMonAlpaca
dotnet run --project ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj
```

Résultat attendu dans la console :
```
ArduSafeMon Alpaca  ▶  http://localhost:11111
COM port : COM3  |  Poll : 2000 ms
Appuyez sur Ctrl+C pour arrêter.
```

Dans un navigateur, ouvrir : `http://localhost:11111/management/apiversions`  
Résultat attendu :
```json
{"Value":[1],"ClientTransactionID":0,"ServerTransactionID":1,"ErrorNumber":0,"ErrorMessage":""}
```

- [ ] **Étape 3 : Tester la découverte dans NINA**

Lancer NINA → aller dans Equipment → Safety Monitor → cliquer le bouton de rafraîchissement/détection Alpaca.  
NINA doit détecter **"ArduSafeMon (Alpaca)"** dans la liste.

- [ ] **Étape 4 : Tester la connexion en mode simulation**

Modifier `appsettings.json` :
```json
{
  "AlpacaPort": 11111,
  "ComPort": "COM3",
  "PollIntervalMs": 2000,
  "SimulationMode": true,
  "SimulatedSafe": true
}
```
Relancer le serveur. Dans NINA, connecter le driver Alpaca.  
Résultat attendu : connecté, IsSafe = true (cadenas vert), **NINA ne crashe pas**.

- [ ] **Étape 5 : Tester la déconnexion**

Dans NINA, déconnecter le driver.  
Résultat attendu : déconnecté proprement, **NINA ne crashe pas**.

- [ ] **Étape 6 : Tester avec port inexistant**

Mettre `"ComPort": "COM99"` dans `appsettings.json`, `"SimulationMode": false`.  
Relancer le serveur. Dans NINA, connecter.  
Résultat attendu : NINA affiche le message d'erreur "Port série 'COM99' introuvable…", **sans crash**.

- [ ] **Étape 7 : Tous les tests unitaires passent**

```cmd
dotnet test ArduSafeMonAlpaca.sln -v minimal
```

Résultat attendu : tous les tests `passed`.

- [ ] **Étape 8 : Commit**

```cmd
cd ..
git add ArduSafeMonAlpaca/
git commit -m "feat: complete Alpaca HTTP server with all Safety Monitor endpoints"
```

---

### Task 7 : Publish self-contained + InnoSetup installer

**Files:**
- Create: `ArduSafeMonAlpacaSetup.iss`

- [ ] **Étape 1 : Publier l'EXE self-contained**

```cmd
cd ArduSafeMonAlpaca
dotnet publish ArduSafeMonAlpaca/ArduSafeMonAlpaca.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Résultat attendu : dossier `ArduSafeMonAlpaca/bin/Release/net8.0/win-x64/publish/` contenant :
- `ArduSafeMonAlpaca.exe` (~60 MB, autonome, aucun .NET requis)
- `appsettings.json`

- [ ] **Étape 2 : Vérifier que l'EXE fonctionne seul**

```cmd
"ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\ArduSafeMonAlpaca.exe"
```

Résultat attendu : serveur démarre sur le port 11111.  
Ouvrir `http://localhost:11111/management/apiversions` → réponse JSON.  
Ctrl+C pour arrêter.

- [ ] **Étape 3 : Créer le script InnoSetup**

Créer `ArduSafeMonAlpacaSetup.iss` à la **racine du dépôt** :

```iss
[Setup]
AppName=ArduSafeMon Alpaca Safety Monitor
AppVersion=1.0
AppPublisher=dalex
DefaultDirName={autopf}\ArduSafeMonAlpaca
DefaultGroupName=ArduSafeMon
OutputDir=.\Installer
OutputBaseFilename=ArduSafeMonAlpaca_Setup_v1.0
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1

[Files]
; Serveur Alpaca (self-contained, aucun .NET requis)
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\ArduSafeMonAlpaca.exe"; \
  DestDir: "{app}"; Flags: ignoreversion

; Config — onlyifdoesntexist pour ne pas écraser les settings de l'utilisateur
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\appsettings.json"; \
  DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
; Raccourci dans le menu Démarrer
Name: "{group}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"
Name: "{group}\Désinstaller ArduSafeMon Alpaca"; Filename: "{uninstallexe}"

; Démarrage automatique avec Windows (répertoire Startup de l'utilisateur)
Name: "{userstartup}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"

[Run]
; Démarrer le serveur immédiatement après installation
Filename: "{app}\ArduSafeMonAlpaca.exe"; \
  Flags: nowait postinstall skipifsilent; \
  Description: "Démarrer ArduSafeMon Alpaca maintenant"

[UninstallRun]
; Arrêter le serveur avant désinstallation
Filename: "taskkill"; Parameters: "/F /IM ArduSafeMonAlpaca.exe"; \
  Flags: runhidden waituntilterminated

[Messages]
WelcomeLabel2=Ce programme va installer ArduSafeMon Alpaca Safety Monitor v1.0.%n%nCe serveur permet à NINA de surveiller la position du toit via un Arduino Uno.%n%nAucune plateforme ASCOM n'est requise.%n%nLe serveur démarrera automatiquement avec Windows.
```

- [ ] **Étape 4 : Compiler l'installateur**

Ouvrir `ArduSafeMonAlpacaSetup.iss` dans InnoSetup → **Build → Compile** (ou `Ctrl+F9`).  
Résultat attendu : `ArduSafeMonAlpaca_Setup_v1.0.exe` créé dans `Installer\`.

- [ ] **Étape 5 : Tester l'installateur**

Double-cliquer `Installer\ArduSafeMonAlpaca_Setup_v1.0.exe` → installer → vérifier :
- L'EXE est dans `C:\Program Files\ArduSafeMonAlpaca\`
- Un raccourci existe dans le dossier Démarrage automatique (`%AppData%\Microsoft\Windows\Start Menu\Programs\Startup\`)
- Le serveur démarre et `http://localhost:11111/management/apiversions` répond

- [ ] **Étape 6 : Commit final**

```cmd
git add ArduSafeMonAlpacaSetup.iss
git add ArduSafeMonAlpaca/
git commit -m "feat: add InnoSetup installer with auto-start on Windows startup"
```

---

## Notes de configuration post-installation

Une fois installé, pour changer le port COM ou activer la simulation :

1. Ouvrir `C:\Program Files\ArduSafeMonAlpaca\appsettings.json` avec Notepad
2. Modifier `ComPort`, `SimulationMode`, etc.
3. Redémarrer le serveur : Task Manager → trouver `ArduSafeMonAlpaca.exe` → Fin de tâche → relancer depuis le menu Démarrer

## Dans NINA

1. Equipment → Safety Monitor → sélectionner **"ArduSafeMon (Alpaca)"** dans la liste Alpaca
2. Connecter → le driver apparaît comme connecté
3. Déconnecter → propre, aucun crash
4. Si le port COM est incorrect → message d'erreur affiché dans NINA, pas de crash
