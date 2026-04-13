using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Interroge l'Arduino toutes les N millisecondes via le port série.
    /// Le thread est seul propriétaire du SerialPort — il l'ouvre ET le ferme lui-même.
    /// Stop() se contente d'annuler le token, sans toucher au port.
    /// </summary>
    internal class SerialPoller : ISerialPoller
    {
        private readonly string _portName;
        private readonly int _pollIntervalMs;

        private Thread _pollThread;
        private CancellationTokenSource _cts;

        private volatile bool _isSafe = false;

        public SerialPoller(string portName, int pollIntervalMs, TraceLogger logger)
        {
            _portName = portName;
            _pollIntervalMs = pollIntervalMs;
            // TraceLogger non utilisé — objet COM dangereux depuis un thread background
        }

        /// <summary>Dernier état connu (thread-safe via volatile).</summary>
        public bool IsSafe => _isSafe;

        /// <summary>Démarre le thread de polling — le thread ouvre lui-même le port.</summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _pollThread = new Thread(() => PollLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "ArduSafeMon.Poller"
            };
            _pollThread.Start();
        }

        /// <summary>
        /// Annule le token — le thread sortira de lui-même au prochain timeout (max 1s)
        /// et fermera le port dans son finally. Stop() ne bloque jamais NINA.
        /// </summary>
        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            // NE PAS toucher au SerialPort ici — le thread le ferme dans son finally
        }

        public void Dispose() => Stop();

        // ── Logique de polling ───────────────────────────────────────────

        private void PollLoop(CancellationToken token)
        {
            SerialPort port = null;
            try
            {
                // Le thread ouvre son propre port
                port = new SerialPort(_portName, 9600)
                {
                    ReadTimeout  = 1000,
                    WriteTimeout = 1000
                };
                port.Open();
                Thread.Sleep(500);
                port.DiscardInBuffer();

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        port.Write("S#");
                        string response = port.ReadTo("#");
                        _isSafe = ParseResponse(response);
                    }
                    catch
                    {
                        _isSafe = false;
                        if (token.IsCancellationRequested) break;

                        // Attendre puis retenter de rouvrir le port
                        WaitOrCancel(5000, token);
                        if (token.IsCancellationRequested) break;

                        port = TryReopen(port, token);
                        if (port == null) break;
                        continue;
                    }

                    WaitOrCancel(_pollIntervalMs, token);
                }
            }
            catch { /* Sécurité absolue : rien ne peut crasher NINA */ }
            finally
            {
                // Le thread ferme toujours son propre port — jamais depuis Stop()
                _isSafe = false;
                try { port?.Close(); } catch { }
                try { port?.Dispose(); } catch { }
            }
        }

        private SerialPort TryReopen(SerialPort oldPort, CancellationToken token)
        {
            try { oldPort?.Close(); } catch { }
            try { oldPort?.Dispose(); } catch { }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var p = new SerialPort(_portName, 9600)
                    {
                        ReadTimeout  = 1000,
                        WriteTimeout = 1000
                    };
                    p.Open();
                    Thread.Sleep(500);
                    p.DiscardInBuffer();
                    return p;
                }
                catch
                {
                    WaitOrCancel(5000, token);
                }
            }
            return null;
        }

        private static void WaitOrCancel(int ms, CancellationToken token)
        {
            try { Task.Delay(ms, token).Wait(); }
            catch { }
        }

        /// <summary>"safe" → true, tout autre valeur → false.</summary>
        internal static bool ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return false;
            return string.Equals(response.Trim(), "safe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
