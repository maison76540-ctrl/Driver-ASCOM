using System;
using System.IO.Ports;
using System.Threading;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Interroge l'Arduino de façon synchrone lors de chaque appel à IsSafe.
    /// Pas de thread background — élimine tous les problèmes de crash liés
    /// aux threads non gérés dans le processus NINA (.NET 10).
    /// Le résultat est mis en cache : le port série n'est interrogé qu'une fois
    /// par intervalle de polling (défaut 2 s). NINA appelle IsSafe toutes les
    /// quelques secondes ; bloquer 300 ms max est parfaitement acceptable.
    /// </summary>
    internal class SerialPoller : ISerialPoller
    {
        private readonly string _portName;
        private readonly int _pollIntervalMs;

        private readonly object _lock = new object();
        private SerialPort _port;
        private bool _isSafe = false;
        private DateTime _lastPoll = DateTime.MinValue;
        private bool _stopped = false;

        public SerialPoller(string portName, int pollIntervalMs, TraceLogger logger)
        {
            _portName = portName;
            _pollIntervalMs = pollIntervalMs;
        }

        /// <summary>
        /// Retourne l'état de sécurité.
        /// Si l'intervalle de polling est écoulé, interroge le port série
        /// de façon synchrone (bloque max 300 ms) puis met à jour le cache.
        /// </summary>
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
        /// Prépare le poller (rien à démarrer — pas de thread).
        /// Réinitialise l'état arrêté si on reconnecte.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                _stopped = false;
                _isSafe = false;
                _lastPoll = DateTime.MinValue;
            }
        }

        /// <summary>Ferme le port et marque le poller comme arrêté.</summary>
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

        // ── Accès série synchrone ────────────────────────────────────────────

        /// <summary>
        /// Envoie "S#" et lit la réponse. Appelé depuis IsSafe sous lock.
        /// Toutes les exceptions sont absorbées : en cas d'erreur _isSafe = false.
        /// </summary>
        private void RefreshSafe()
        {
            _lastPoll = DateTime.Now;
            try
            {
                EnsurePortOpen();
                _port.Write("S#");
                string response = _port.ReadTo("#");
                _isSafe = ParseResponse(response);
            }
            catch
            {
                _isSafe = false;
                // Ferme le port pour forcer une réouverture au prochain appel
                ClosePort();
            }
        }

        /// <summary>Ouvre le port s'il n'est pas déjà ouvert.</summary>
        private void EnsurePortOpen()
        {
            if (_port != null && _port.IsOpen) return;

            ClosePort();
            _port = new SerialPort(_portName, 9600)
            {
                ReadTimeout  = 300,
                WriteTimeout = 300
            };
            _port.Open();
            Thread.Sleep(200);      // laisse l'Arduino se stabiliser
            _port.DiscardInBuffer();
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
