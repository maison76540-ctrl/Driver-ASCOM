using System;
using System.IO.Ports;
using System.Threading;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Gère la communication série avec l'Arduino.
    /// Le port est ouvert lors de Connect() et fermé lors de Stop().
    /// IsSafe interroge le port de façon synchrone avec cache temporel.
    /// Toutes les exceptions sont absorbées — rien ne peut crasher NINA.
    /// </summary>
    internal class SerialPoller : ISerialPoller
    {
        private readonly string _portName;
        private readonly int _pollIntervalMs;

        private readonly object _lock = new object();
        private SerialPort _port;
        private bool _isSafe = false;
        private DateTime _lastPoll = DateTime.MinValue;
        private bool _stopped = true;

        public SerialPoller(string portName, int pollIntervalMs, TraceLogger logger)
        {
            _portName = portName;
            _pollIntervalMs = pollIntervalMs;
        }

        public bool IsSafe
        {
            get
            {
                lock (_lock)
                {
                    if (_stopped) return false;

                    bool intervalElapsed =
                        (DateTime.Now - _lastPoll).TotalMilliseconds >= _pollIntervalMs;

                    if (intervalElapsed)
                        RefreshSafe();

                    return _isSafe;
                }
            }
        }

        /// <summary>
        /// Ouvre le port et vérifie que l'Arduino répond.
        /// Lance une exception descriptive si le port est introuvable ou si
        /// l'Arduino ne répond pas — NINA affiche ce message à l'utilisateur.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                _stopped = false;
                _isSafe = false;
                _lastPoll = DateTime.MinValue;
                OpenPort(); // valide immédiatement — lève une exception si erreur
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                _isSafe = false;
                ClosePort();
            }
        }

        public void Dispose() => Stop();

        // ── Accès série ──────────────────────────────────────────────────────

        private void RefreshSafe()
        {
            _lastPoll = DateTime.Now;
            try
            {
                if (_port == null || !_port.IsOpen)
                    OpenPort();

                _port.DiscardInBuffer();
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

        /// <summary>
        /// Ouvre le port. Lance une exception avec message lisible si échec.
        /// Cette exception remonte dans Connect() → NINA l'affiche.
        /// </summary>
        private void OpenPort()
        {
            ClosePort();

            // Vérifie que le port existe dans la liste système
            bool portExists = false;
            try
            {
                string[] ports = SerialPort.GetPortNames();
                foreach (string p in ports)
                {
                    if (string.Equals(p, _portName, StringComparison.OrdinalIgnoreCase))
                    {
                        portExists = true;
                        break;
                    }
                }
            }
            catch { }

            if (!portExists)
                throw new Exception(
                    $"Port série '{_portName}' introuvable. " +
                    "Vérifiez que l'Arduino est branché et configurez le bon port " +
                    "dans Setup.");

            try
            {
                _port = new SerialPort(_portName, 9600)
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
                throw new Exception(
                    $"Impossible d'ouvrir le port '{_portName}' : {ex.Message}");
            }
        }

        private void ClosePort()
        {
            try { _port?.Close(); }   catch { }
            try { _port?.Dispose(); } catch { }
            _port = null;
        }

        /// <summary>"safe" → true, tout autre valeur → false.</summary>
        internal static bool ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return false;
            return string.Equals(response.Trim(), "safe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
