using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ASCOM;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Driver ASCOM Safety Monitor pour capteur de position de toit Arduino.
    /// ProgID : ASCOM.ArduSafeMon.SafetyMonitor
    /// </summary>
    [ComVisible(true)]
    [Guid("0EA229E6-6F0C-4B9D-BAAC-A4447204D708")]
    [ProgId("ASCOM.ArduSafeMon.SafetyMonitor")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SafetyMonitor : ISafetyMonitor, IDisposable
    {
        private const string DriverId = "ASCOM.ArduSafeMon.SafetyMonitor";
        private const string DriverDescription = "ArduSafeMon Safety Monitor";

        private readonly TraceLogger _logger;
        private readonly DriverProfile _profile;
        private ISerialPoller _poller;
        private bool _connected = false;

        // Paramètres pour le constructeur de test (injection directe)
        private readonly bool _testMode;
        private readonly bool _testSimulationMode;
        private readonly bool _testSimulatedSafe;

        // ── Constructeur ASCOM normal (utilisé par COM / NINA) ──────────
        public SafetyMonitor()
        {
            _logger = new TraceLogger("", "ArduSafeMon");
            _logger.Enabled = false;
            _profile = new DriverProfile();
            _profile.Load();
            _testMode = false;
        }

        // ── Constructeur pour tests unitaires (pas de COM, pas de registre) ──
        internal SafetyMonitor(bool simulationMode, bool simulatedSafe)
        {
            _testMode = true;
            _testSimulationMode = simulationMode;
            _testSimulatedSafe = simulatedSafe;
            _profile = null;
            _logger = null;
        }

        // ── Propriétés ISafetyMonitor ────────────────────────────────────

        public bool Connected
        {
            get => _connected;
            set
            {
                if (value) Connect();
                else Disconnect();
            }
        }

        public bool IsSafe
        {
            get
            {
                // Retourner false (unsafe) si non connecté — ne jamais lever d'exception
                // NINA peut appeler IsSafe sur un timer en parallèle pendant la déconnexion
                if (!_connected)
                    return false;

                try
                {
                    bool simMode = _testMode ? _testSimulationMode : _profile.SimulationMode;
                    if (simMode)
                    {
                        bool simSafe = _testMode ? _testSimulatedSafe : _profile.SimulatedSafe;
                        _logger?.LogMessage("IsSafe", $"[SIMULATION] → {simSafe}");
                        return simSafe;
                    }

                    return _poller?.IsSafe ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }

        public string Name => "ArduSafeMon";
        public string Description => "Arduino Roof Safety Monitor";
        public string DriverInfo => "ArduSafeMon v1.0 - ASCOM Safety Monitor for roof sensor";
        public string DriverVersion => "1.0";
        public short InterfaceVersion => 2;

        public ArrayList SupportedActions => new ArrayList();

        public string Action(string ActionName, string ActionParameters)
            => throw new ASCOM.ActionNotImplementedException("Action " + ActionName);

        public void CommandBlind(string Command, bool Raw = false)
            => throw new ASCOM.MethodNotImplementedException("CommandBlind");

        public bool CommandBool(string Command, bool Raw = false)
            => throw new ASCOM.MethodNotImplementedException("CommandBool");

        public string CommandString(string Command, bool Raw = false)
            => throw new ASCOM.MethodNotImplementedException("CommandString");

        public void SetupDialog()
        {
            if (_testMode) return;

            using (var form = new SetupDialogForm(_profile))
            {
                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _profile.Save();
                }
            }
        }

        public void Dispose()
        {
            try { Disconnect(); } catch { }
            try { _logger?.Dispose(); } catch { }
        }

        // ── Connexion / Déconnexion ──────────────────────────────────────

        internal void Connect()
        {
            if (_connected) return;

            bool simMode = _testMode ? _testSimulationMode : _profile?.SimulationMode ?? false;

            if (!simMode)
            {
                string port = _testMode ? "COM1" : _profile.ComPort;
                int interval = _testMode ? 2000 : _profile.PollIntervalMs;

                _poller = new SerialPoller(port, interval, _logger);
                _poller.Start();
            }

            _connected = true;
            _logger?.LogMessage("Connect", $"Connected (simulation={simMode})");
        }

        internal void Disconnect()
        {
            if (!_connected) return;

            // Marquer déconnecté IMMÉDIATEMENT — NINA peut continuer sans bloquer
            _connected = false;

            // Capturer le poller et le nettoyer en arrière-plan
            // pour ne jamais bloquer le thread COM de NINA
            var pollerToStop = _poller;
            _poller = null;

            if (pollerToStop != null)
            {
                Task.Run(() =>
                {
                    try { pollerToStop.Stop(); } catch { }
                    try { pollerToStop.Dispose(); } catch { }
                });
            }

            _logger?.LogMessage("Disconnect", "Disconnected");
        }

        // ── Enregistrement COM (ASCOM) ────────────────────────────────────

        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            using (var profile = new Profile())
            {
                profile.DeviceType = "SafetyMonitor";
                profile.Register(DriverId, DriverDescription);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            using (var profile = new Profile())
            {
                profile.DeviceType = "SafetyMonitor";
                profile.Unregister(DriverId);
            }
        }
    }
}
