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
    private string _lastRaw = "";
    private string _lastError = "";
    private DateTime _lastPoll = DateTime.MinValue;

    public SafetyMonitorDevice(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>True si Connect() a réussi et Disconnect() n'a pas encore été appelé.</summary>
    public bool Connected { get { lock (_lock) return _connected; } }

    /// <summary>Retourne la dernière réponse brute reçue de l'Arduino (diagnostic).</summary>
    public string GetRawResponse()
    {
        lock (_lock)
        {
            if (!_connected || _settings.SimulationMode)
                return _settings.SimulationMode ? "(simulation)" : "(non connecté)";
            // Forcer un poll immédiat
            _lastPoll = DateTime.MinValue;
            RefreshSafe();
            return _lastRaw;
        }
    }

    /// <summary>Retourne le dernier message d'erreur série (vide si aucune erreur).</summary>
    public string GetLastError() { lock (_lock) return _lastError; }

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
    /// </summary>
    public void Connect()
    {
        lock (_lock)
        {
            if (_connected) return;

            if (!_settings.SimulationMode)
                OpenPort();

            _connected = true;
            _lastPoll = DateTime.MinValue;
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
            _lastRaw = response.Trim();
            bool raw = ParseResponse(response);
            _isSafe = _settings.InvertSensor ? !raw : raw;
        }
        catch (Exception ex)
        {
            _lastError = $"[{DateTime.Now:HH:mm:ss}] {ex.GetType().Name}: {ex.Message}";
            _isSafe = false;
            ClosePort();
        }
    }

    private void OpenPort()
    {
        ClosePort();

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
            Thread.Sleep(2000);   // Nano Every (USB natif) nécessite plus de temps que Nano classique
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
    /// "safe" (insensible à la casse, espaces ignorés) → true. Tout autre valeur → false.
    /// </summary>
    internal static bool ParseResponse(string? response)
    {
        if (string.IsNullOrEmpty(response)) return false;
        return string.Equals(response.Trim(), "safe", StringComparison.OrdinalIgnoreCase);
    }
}
