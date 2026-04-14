using System;
using System.IO.Ports;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Interroge l'Arduino toutes les N millisecondes via le port série.
    /// Le thread est seul propriétaire du SerialPort — il l'ouvre ET le ferme lui-même.
    /// Stop() annule le token et attend la fin du thread (max 1,5 s) pour éviter
    /// tout accès concurrent au port après la déconnexion.
    /// [HandleProcessCorruptedStateExceptions] garantit que même une
    /// AccessViolationException native ne peut pas faire crasher NINA.
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
            _pollThread = new Thread(() => PollLoopSafe(_cts.Token))
            {
                IsBackground = true,
                Name = "ArduSafeMon.Poller"
            };
            _pollThread.Start();
        }

        /// <summary>
        /// Annule le token et attend la fin du thread (max 1 500 ms).
        /// ReadTimeout = 300 ms → le thread se termine en ≤ 400 ms après Cancel.
        /// Bloquer 400 ms max sur le thread COM de NINA est acceptable.
        /// </summary>
        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _pollThread?.Join(1500); } catch { }
        }

        public void Dispose() => Stop();

        // ── Wrapper qui attrape même les exceptions de corruption d'état ──────

        /// <summary>
        /// Enveloppe HandleProcessCorruptedStateExceptions autour de PollLoop
        /// afin qu'une AccessViolationException native ne puisse jamais
        /// remonter dans le processus NINA et le faire crasher.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private void PollLoopSafe(CancellationToken token)
        {
            try
            {
                PollLoop(token);
            }
            catch
            {
                // Absorbe toute exception y compris les corrupted-state exceptions
            }
            finally
            {
                _isSafe = false;
            }
        }

        // ── Logique de polling ───────────────────────────────────────────────

        private void PollLoop(CancellationToken token)
        {
            SerialPort port = null;
            try
            {
                port = new SerialPort(_portName, 9600)
                {
                    ReadTimeout  = 300,   // court → le thread répond vite à Cancel
                    WriteTimeout = 300
                };
                port.Open();
                Thread.Sleep(200);
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

                        WaitOrCancel(5000, token);
                        if (token.IsCancellationRequested) break;

                        port = TryReopen(port, token);
                        if (port == null) break;
                        continue;
                    }

                    WaitOrCancel(_pollIntervalMs, token);
                }
            }
            catch { }
            finally
            {
                _isSafe = false;
                try { port?.Close(); }   catch { }
                try { port?.Dispose(); } catch { }
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private SerialPort TryReopen(SerialPort oldPort, CancellationToken token)
        {
            try { oldPort?.Close(); }   catch { }
            try { oldPort?.Dispose(); } catch { }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var p = new SerialPort(_portName, 9600)
                    {
                        ReadTimeout  = 300,
                        WriteTimeout = 300
                    };
                    p.Open();
                    Thread.Sleep(200);
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
