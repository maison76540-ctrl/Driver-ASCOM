using System.IO.Ports;
using System.Text;
using System.Text.Json;
using ArduSafeMonAlpaca;

// ── Configuration ────────────────────────────────────────────────────────────

// Fichier de config dans %ProgramData%\ArduSafeMonAlpaca\ pour permettre
// l'ecriture sans droits admin (evite l'erreur "Access denied" sur Program Files)
static string GetConfigPath()
{
    string dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ArduSafeMonAlpaca");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "appsettings.json");
}

string configPath = GetConfigPath();

// Si pas encore de config dans ProgramData, copier celle de l'appli (defaults)
if (!File.Exists(configPath))
{
    string src = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (File.Exists(src)) File.Copy(src, configPath);
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var settings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<SafetyMonitorDevice>();
builder.Services.AddHostedService<DiscoveryService>();

builder.WebHost.UseUrls($"http://0.0.0.0:{settings.AlpacaPort}");

var app = builder.Build();

// ── Page de configuration ─────────────────────────────────────────────────────

// Route standard Alpaca appelée par NINA quand on clique "Setup"
app.MapGet("/setup/v1/safetymonitor/0/setup", (HttpContext ctx) =>
    Results.Redirect("/setup"));

app.MapGet("/setup", (AppSettings s) =>
{
    string[] ports;
    try { ports = SerialPort.GetPortNames(); }
    catch { ports = Array.Empty<string>(); }

    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang='fr'><head><meta charset='UTF-8'>");
    sb.AppendLine("<title>ArduSafeMon - Configuration</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("body{font-family:Arial,sans-serif;max-width:480px;margin:40px auto;padding:0 20px;background:#1a1a2e;color:#eee}");
    sb.AppendLine("h1{color:#e94560}");
    sb.AppendLine("label{display:block;margin-top:16px;font-weight:bold}");
    sb.AppendLine("select,input{width:100%;padding:8px;margin-top:4px;border-radius:4px;border:1px solid #444;background:#16213e;color:#eee;font-size:1em}");
    sb.AppendLine(".row{display:flex;align-items:center;gap:10px;margin-top:16px}");
    sb.AppendLine(".row input{width:auto}");
    sb.AppendLine("button{margin-top:24px;width:100%;padding:12px;background:#e94560;color:white;border:none;border-radius:4px;font-size:1em;cursor:pointer}");
    sb.AppendLine(".ok{margin-top:12px;padding:10px;border-radius:4px;background:#1a6b3a}");
    sb.AppendLine(".err{margin-top:12px;padding:10px;border-radius:4px;background:#6b1a1a}");
    sb.AppendLine("</style></head><body>");
    sb.AppendLine("<h1>ArduSafeMon Alpaca</h1>");
    sb.AppendLine("<form method='post' action='/setup'>");

    // Port série
    sb.AppendLine("<label>Port serie (Arduino)</label>");
    sb.AppendLine("<select name='ComPort'>");
    foreach (var p in ports)
    {
        string sel = string.Equals(p, s.ComPort, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
        sb.AppendLine($"<option value='{p}'{sel}>{p}</option>");
    }
    if (!ports.Any(p => string.Equals(p, s.ComPort, StringComparison.OrdinalIgnoreCase)))
    {
        sb.AppendLine($"<option value='{s.ComPort}' selected>{s.ComPort} (non detecte)</option>");
    }
    sb.AppendLine("</select>");

    // Intervalle
    sb.AppendLine("<label>Intervalle de polling (ms)</label>");
    sb.AppendLine($"<input type='number' name='PollIntervalMs' value='{s.PollIntervalMs}' min='500' max='30000'>");

    // Simulation mode
    string simChecked = s.SimulationMode ? " checked" : "";
    sb.AppendLine("<div class='row'>");
    sb.AppendLine($"<input type='checkbox' name='SimulationMode' id='sim'{simChecked}>");
    sb.AppendLine("<label for='sim' style='margin:0'>Mode simulation (sans Arduino)</label>");
    sb.AppendLine("</div>");

    // Simulated safe
    string safeChecked = s.SimulatedSafe ? " checked" : "";
    sb.AppendLine("<div class='row'>");
    sb.AppendLine($"<input type='checkbox' name='SimulatedSafe' id='safe'{safeChecked}>");
    sb.AppendLine("<label for='safe' style='margin:0'>Etat simule : Safe</label>");
    sb.AppendLine("</div>");

    // Invert sensor
    string invertChecked = s.InvertSensor ? " checked" : "";
    sb.AppendLine("<div class='row'>");
    sb.AppendLine($"<input type='checkbox' name='InvertSensor' id='inv'{invertChecked}>");
    sb.AppendLine("<label for='inv' style='margin:0'>Inverser le capteur (si toit ferm&#233; = safe)</label>");
    sb.AppendLine("</div>");

    sb.AppendLine("<button type='submit'>Enregistrer</button>");
    sb.AppendLine("</form></body></html>");

    return Results.Content(sb.ToString(), "text/html; charset=utf-8");
});

app.MapPost("/setup", async (HttpRequest req, AppSettings s) =>
{
    var form = await req.ReadFormAsync();

    string comPort = form["ComPort"].FirstOrDefault() ?? s.ComPort;
    if (!int.TryParse(form["PollIntervalMs"].FirstOrDefault(), out int pollMs) || pollMs < 500)
        pollMs = s.PollIntervalMs;
    bool simMode    = form.ContainsKey("SimulationMode");
    bool simSafe    = form.ContainsKey("SimulatedSafe");
    bool invertSensor = form.ContainsKey("InvertSensor");

    // Mise à jour en mémoire
    s.ComPort        = comPort;
    s.PollIntervalMs = pollMs;
    s.SimulationMode = simMode;
    s.SimulatedSafe  = simSafe;
    s.InvertSensor   = invertSensor;

    // Persistance dans %ProgramData%\ArduSafeMonAlpaca\appsettings.json
    try
    {
        string appSettingsPath = GetConfigPath();
        var json = new
        {
            AlpacaPort     = s.AlpacaPort,
            ComPort        = comPort,
            PollIntervalMs = pollMs,
            SimulationMode = simMode,
            SimulatedSafe  = simSafe,
            InvertSensor   = invertSensor,
            Logging        = new { LogLevel = new { Default = "Information" } }
        };
        await File.WriteAllTextAsync(appSettingsPath,
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch (Exception ex)
    {
        return Results.Text($"Erreur : {ex.Message}", statusCode: 500);
    }

    // Redirige vers la page setup avec message OK
    return Results.Redirect("/setup?saved=1");
});

// ── Management API ───────────────────────────────────────────────────────────

app.MapGet("/management/apiversions", () =>
    Results.Json(AlpacaResult.Ok(new[] { 1 })));

app.MapGet("/management/v1/description", () =>
    Results.Json(AlpacaResult.Ok(new
    {
        ServerName          = "ArduSafeMon Alpaca Server",
        Manufacturer        = "dalex",
        ManufacturerVersion = "1.0",
        Location            = ""
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

app.MapPut($"{P}/connected", async (HttpRequest req, SafetyMonitorDevice dev) =>
{
    var form = await req.ReadFormAsync();
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

// ── Demarrage ────────────────────────────────────────────────────────────────
Console.WriteLine($"ArduSafeMon Alpaca  ->  http://localhost:{settings.AlpacaPort}");
Console.WriteLine($"Configuration       ->  http://localhost:{settings.AlpacaPort}/setup");
Console.WriteLine($"COM port : {settings.ComPort}  |  Poll : {settings.PollIntervalMs} ms" +
                  (settings.SimulationMode
                      ? $"  |  SIMULATION ({(settings.SimulatedSafe ? "Safe" : "Unsafe")})"
                      : ""));
Console.WriteLine("Appuyez sur Ctrl+C pour arreter.");

app.Run();
