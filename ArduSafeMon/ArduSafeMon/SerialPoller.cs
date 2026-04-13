using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Interroge l'Arduino toutes les N millisecondes via le port série.
    /// Maintient un état en cache thread-safe.
    /// </summary>
    internal class SerialPoller : ISerialPoller
    {
        private readonly string _portName;
        private readonly int _pollIntervalMs;
        private readonly TraceLogger _logger;

        private SerialPort _serialPort;
        private Thread _pollThread;
        private CancellationTokenSource _cts;

        private bool _isSafe = false;
        private readonly object _lock = new object();

        public SerialPoller(string portName, int pollIntervalMs, TraceLogger logger)
        {
            _portName = portName;
            _pollIntervalMs = pollIntervalMs;
            _logger = logger;
        }

        /// <summary>Dernier état connu (thread-safe).</summary>
        public bool IsSafe
        {
            get { lock (_lock) { return _isSafe; } }
        }

        /// <summary>Ouvre le port série et démarre le thread de polling.</summary>
        public void Start()
        {
            _serialPort = new SerialPort(_portName, 9600)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 1000,
                NewLine      = "#"   // protocole Arduino : délimiteur '#'
            };
            _serialPort.Open();

            // Vider le buffer de démarrage de l'Arduino (reset USB)
            Thread.Sleep(500);
            _serialPort.DiscardInBuffer();

            _cts = new CancellationTokenSource();
            _pollThread = new Thread(() => PollLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "ArduSafeMon.Poller"
            };
            _pollThread.Start();
            _logger?.LogMessage("SerialPoller", $"Started on {_portName}, interval={_pollIntervalMs}ms");
        }

        /// <summary>Arrête le thread et ferme le port série.</summary>
        public void Stop()
        {
            // 1. Signaler l'annulation
            _cts?.Cancel();

            // 2. Fermer le port AVANT le Join pour débloquer immédiatement
            //    tout ReadTo/Write en attente dans le thread
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                    _serialPort.Close();
            }
            catch { }

            // 3. Attendre la fin du thread (il sortira rapidement car le port est fermé)
            try { _pollThread?.Join(3000); } catch { }

            // 4. Libérer les ressources
            try { _serialPort?.Dispose(); } catch { }
            _serialPort = null;
            _logger?.LogMessage("SerialPoller", "Stopped");
        }

        public void Dispose() => Stop();

        // ── Logique de polling ───────────────────────────────────────────

        private void PollLoop(CancellationToken token)
        {
            // Enveloppe externe : aucune exception ne peut sortir du thread
            // Une exception non rattrapée dans un thread background crash le processus hôte (NINA)
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        PollOnce();
                    }
                    catch (Exception ex)
                    {
                        // Si le token est annulé, c'est un arrêt normal — on sort
                        if (token.IsCancellationRequested) return;

                        _logger?.LogMessage("SerialPoller", $"Error: {ex.Message} → unsafe");
                        lock (_lock) { _isSafe = false; }

                        WaitOrCancel(5000, token);
                        if (token.IsCancellationRequested) return;
                        TryReconnect(token);
                        continue;
                    }

                    WaitOrCancel(_pollIntervalMs, token);
                }
            }
            catch { /* Sécurité finale : absorber toute exception pour ne jamais crasher NINA */ }
        }

        private void PollOnce()
        {
            _serialPort.Write("S#");
            string response = _serialPort.ReadTo("#");
            bool newState = ParseResponse(response);
            lock (_lock) { _isSafe = newState; }
            _logger?.LogMessage("SerialPoller", $"Response: '{response}' → IsSafe={newState}");
        }

        private void TryReconnect(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                        _serialPort.Close();

                    _serialPort?.Dispose();
                    _serialPort = new SerialPort(_portName, 9600)
                    {
                        ReadTimeout  = 1000,
                        WriteTimeout = 1000,
                        NewLine      = "#"   // protocole Arduino : délimiteur '#'
                    };
                    _serialPort.Open();
                    Thread.Sleep(500);
                    _serialPort.DiscardInBuffer();
                    _logger?.LogMessage("SerialPoller", "Reconnected successfully");
                    return;
                }
                catch
                {
                    _logger?.LogMessage("SerialPoller", "Reconnect failed, retrying in 5s...");
                    WaitOrCancel(5000, token);
                }
            }
        }

        private static void WaitOrCancel(int ms, CancellationToken token)
        {
            try { Task.Delay(ms, token).Wait(); }
            catch { /* token annulé : normal */ }
        }

        // ── Méthode de parsing : interne + testable ──────────────────────

        /// <summary>
        /// Interprète la réponse de l'Arduino (sans le '#' final).
        /// "safe" → true, tout autre valeur → false.
        /// </summary>
        internal static bool ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return false;

            return string.Equals(response.Trim(), "safe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
