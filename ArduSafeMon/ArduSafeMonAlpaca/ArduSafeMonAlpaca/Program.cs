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

// NINA sends PUT /connected with form body: Connected=True&ClientID=...
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
        // 0x0500 = InvalidOperation — NINA displays ex.Message to the user
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

// ── Start ────────────────────────────────────────────────────────────────────
Console.WriteLine($"ArduSafeMon Alpaca  ▶  http://localhost:{settings.AlpacaPort}");
Console.WriteLine($"COM port : {settings.ComPort}  |  Poll : {settings.PollIntervalMs} ms" +
                  (settings.SimulationMode
                      ? $"  |  SIMULATION ({(settings.SimulatedSafe ? "Safe" : "Unsafe")})"
                      : ""));
Console.WriteLine("Appuyez sur Ctrl+C pour arrêter.");

app.Run();
