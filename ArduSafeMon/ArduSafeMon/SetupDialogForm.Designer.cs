namespace ASCOM.ArduSafeMon
{
    partial class SetupDialogForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelComPort = new System.Windows.Forms.Label();
            this.comboBoxComPort = new System.Windows.Forms.ComboBox();
            this.labelPollInterval = new System.Windows.Forms.Label();
            this.numericPollInterval = new System.Windows.Forms.NumericUpDown();
            this.labelMs = new System.Windows.Forms.Label();
            this.checkBoxSimulation = new System.Windows.Forms.CheckBox();
            this.radioButtonSafe = new System.Windows.Forms.RadioButton();
            this.radioButtonUnsafe = new System.Windows.Forms.RadioButton();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBoxSimulation = new System.Windows.Forms.GroupBox();

            ((System.ComponentModel.ISupportInitialize)(this.numericPollInterval)).BeginInit();
            this.groupBoxSimulation.SuspendLayout();
            this.SuspendLayout();

            // labelComPort
            this.labelComPort.AutoSize = true;
            this.labelComPort.Location = new System.Drawing.Point(12, 20);
            this.labelComPort.Text = "Port COM :";

            // comboBoxComPort
            this.comboBoxComPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxComPort.Location = new System.Drawing.Point(110, 17);
            this.comboBoxComPort.Size = new System.Drawing.Size(100, 21);

            // labelPollInterval
            this.labelPollInterval.AutoSize = true;
            this.labelPollInterval.Location = new System.Drawing.Point(12, 52);
            this.labelPollInterval.Text = "Intervalle :";

            // numericPollInterval
            this.numericPollInterval.Location = new System.Drawing.Point(110, 50);
            this.numericPollInterval.Minimum = 500;
            this.numericPollInterval.Maximum = 10000;
            this.numericPollInterval.Increment = 500;
            this.numericPollInterval.Value = 2000;
            this.numericPollInterval.Size = new System.Drawing.Size(80, 20);

            // labelMs
            this.labelMs.AutoSize = true;
            this.labelMs.Location = new System.Drawing.Point(196, 52);
            this.labelMs.Text = "ms";

            // groupBoxSimulation
            this.groupBoxSimulation.Location = new System.Drawing.Point(12, 80);
            this.groupBoxSimulation.Size = new System.Drawing.Size(300, 80);
            this.groupBoxSimulation.Text = "";

            // checkBoxSimulation
            this.checkBoxSimulation.AutoSize = true;
            this.checkBoxSimulation.Location = new System.Drawing.Point(6, 18);
            this.checkBoxSimulation.Text = "Mode simulation";
            this.checkBoxSimulation.CheckedChanged += new System.EventHandler(this.checkBoxSimulation_CheckedChanged);

            // radioButtonSafe
            this.radioButtonSafe.AutoSize = true;
            this.radioButtonSafe.Location = new System.Drawing.Point(20, 44);
            this.radioButtonSafe.Text = "Safe (toit ouvert)";
            this.radioButtonSafe.Enabled = false;

            // radioButtonUnsafe
            this.radioButtonUnsafe.AutoSize = true;
            this.radioButtonUnsafe.Location = new System.Drawing.Point(160, 44);
            this.radioButtonUnsafe.Text = "Unsafe (toit fermé)";
            this.radioButtonUnsafe.Enabled = false;

            this.groupBoxSimulation.Controls.Add(this.checkBoxSimulation);
            this.groupBoxSimulation.Controls.Add(this.radioButtonSafe);
            this.groupBoxSimulation.Controls.Add(this.radioButtonUnsafe);

            // buttonOK
            this.buttonOK.Location = new System.Drawing.Point(160, 175);
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.Text = "OK";
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);

            // buttonCancel
            this.buttonCancel.Location = new System.Drawing.Point(245, 175);
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.Text = "Annuler";
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);

            // SetupDialogForm
            this.ClientSize = new System.Drawing.Size(334, 210);
            this.Controls.Add(this.labelComPort);
            this.Controls.Add(this.comboBoxComPort);
            this.Controls.Add(this.labelPollInterval);
            this.Controls.Add(this.numericPollInterval);
            this.Controls.Add(this.labelMs);
            this.Controls.Add(this.groupBoxSimulation);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ArduSafeMon — Configuration";

            ((System.ComponentModel.ISupportInitialize)(this.numericPollInterval)).EndInit();
            this.groupBoxSimulation.ResumeLayout(false);
            this.groupBoxSimulation.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelComPort;
        private System.Windows.Forms.ComboBox comboBoxComPort;
        private System.Windows.Forms.Label labelPollInterval;
        private System.Windows.Forms.NumericUpDown numericPollInterval;
        private System.Windows.Forms.Label labelMs;
        private System.Windows.Forms.CheckBox checkBoxSimulation;
        private System.Windows.Forms.RadioButton radioButtonSafe;
        private System.Windows.Forms.RadioButton radioButtonUnsafe;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.GroupBox groupBoxSimulation;
    }
}
