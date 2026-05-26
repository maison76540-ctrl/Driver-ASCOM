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

    /// <summary>
    /// Si true, inverse la logique du capteur :
    /// "safe" Arduino → unsafe, "notsafe" Arduino → safe.
    /// Utile si le câblage du capteur est en logique inverse.
    /// </summary>
    public bool InvertSensor { get; set; } = false;
}
