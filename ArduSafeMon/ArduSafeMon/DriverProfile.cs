using System;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Gère la lecture et l'écriture des paramètres du driver
    /// dans le registre ASCOM (ASCOM.Utilities.Profile).
    /// </summary>
    public class DriverProfile
    {
        private const string DriverId = "ASCOM.ArduSafeMon.SafetyMonitor";
        private const string ComPortKey = "ComPort";
        private const string PollIntervalKey = "PollInterval";
        private const string SimulationModeKey = "SimulationMode";
        private const string SimulatedSafeKey = "SimulatedSafe";

        private const string ComPortDefault = "COM3";
        private const int PollIntervalDefault = 2000;
        private const bool SimulationModeDefault = false;
        private const bool SimulatedSafeDefault = true;

        public string ComPort { get; set; } = ComPortDefault;
        public int PollIntervalMs { get; set; } = PollIntervalDefault;
        public bool SimulationMode { get; set; } = SimulationModeDefault;
        public bool SimulatedSafe { get; set; } = SimulatedSafeDefault;

        /// <summary>Charge les paramètres depuis le registre ASCOM.</summary>
        public void Load()
        {
            using (var profile = new Profile())
            {
                profile.DeviceType = "SafetyMonitor";

                ComPort = profile.GetValue(DriverId, ComPortKey, string.Empty, ComPortDefault);

                string pollStr = profile.GetValue(DriverId, PollIntervalKey, string.Empty, PollIntervalDefault.ToString());
                PollIntervalMs = int.TryParse(pollStr, out int poll) ? Math.Max(500, poll) : PollIntervalDefault;

                string simModeStr = profile.GetValue(DriverId, SimulationModeKey, string.Empty, SimulationModeDefault.ToString());
                SimulationMode = bool.TryParse(simModeStr, out bool simMode) ? simMode : SimulationModeDefault;

                string simSafeStr = profile.GetValue(DriverId, SimulatedSafeKey, string.Empty, SimulatedSafeDefault.ToString());
                SimulatedSafe = bool.TryParse(simSafeStr, out bool simSafe) ? simSafe : SimulatedSafeDefault;
            }
        }

        /// <summary>Sauvegarde les paramètres dans le registre ASCOM.</summary>
        public void Save()
        {
            using (var profile = new Profile())
            {
                profile.DeviceType = "SafetyMonitor";
                profile.WriteValue(DriverId, ComPortKey, ComPort);
                profile.WriteValue(DriverId, PollIntervalKey, PollIntervalMs.ToString());
                profile.WriteValue(DriverId, SimulationModeKey, SimulationMode.ToString());
                profile.WriteValue(DriverId, SimulatedSafeKey, SimulatedSafe.ToString());
            }
        }
    }
}
