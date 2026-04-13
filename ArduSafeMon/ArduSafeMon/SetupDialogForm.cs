using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace ASCOM.ArduSafeMon
{
    public partial class SetupDialogForm : Form
    {
        private readonly DriverProfile _profile;

        public SetupDialogForm(DriverProfile profile)
        {
            _profile = profile;
            InitializeComponent();
            LoadFromProfile();
        }

        private void LoadFromProfile()
        {
            // Port COM
            comboBoxComPort.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
                comboBoxComPort.Items.Add(port);

            if (comboBoxComPort.Items.Contains(_profile.ComPort))
                comboBoxComPort.SelectedItem = _profile.ComPort;
            else if (comboBoxComPort.Items.Count > 0)
                comboBoxComPort.SelectedIndex = 0;

            // Intervalle de polling
            numericPollInterval.Value = Math.Max(500, Math.Min(10000, _profile.PollIntervalMs));

            // Mode simulation
            checkBoxSimulation.Checked = _profile.SimulationMode;
            radioButtonSafe.Checked = _profile.SimulatedSafe;
            radioButtonUnsafe.Checked = !_profile.SimulatedSafe;

            UpdateSimulationControls();
        }

        private void SaveToProfile()
        {
            _profile.ComPort = comboBoxComPort.SelectedItem?.ToString() ?? "COM3";
            _profile.PollIntervalMs = (int)numericPollInterval.Value;
            _profile.SimulationMode = checkBoxSimulation.Checked;
            _profile.SimulatedSafe = radioButtonSafe.Checked;
        }

        private void UpdateSimulationControls()
        {
            bool simEnabled = checkBoxSimulation.Checked;
            radioButtonSafe.Enabled = simEnabled;
            radioButtonUnsafe.Enabled = simEnabled;
            comboBoxComPort.Enabled = !simEnabled;
            labelComPort.Enabled = !simEnabled;
        }

        private void checkBoxSimulation_CheckedChanged(object sender, EventArgs e)
            => UpdateSimulationControls();

        private void buttonOK_Click(object sender, EventArgs e)
        {
            SaveToProfile();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
