using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;

namespace ArduFlasher;

public partial class Form1 : Form
{
    // Chemin du sketch .ino — relatif à l'EXE
    private static readonly string SketchRelPath =
        Path.Combine("..", "ArduSafeMonV0_1", "ArduSafeMonV0_1.ino");

    public Form1()
    {
        InitializeComponent();
        Text = "ArduSafeMon — Flash Arduino";
        ClientSize = new Size(520, 420);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        RefreshPorts();
        DetectArduinoCli();
    }

    // ── Contrôles ─────────────────────────────────────────────────────────
    private ComboBox cbPort    = new();
    private ComboBox cbBoard   = new();
    private TextBox  txtSketch = new();
    private Button   btnBrowse = new();
    private Button   btnRefresh= new();
    private Button   btnFlash  = new();
    private RichTextBox rtLog  = new();
    private Label    lblStatus = new();

    private void BuildUI()
    {
        int y = 16;

        // ─ Port COM ─
        AddLabel("Port COM (Arduino) :", 16, y);
        cbPort.Left = 180; cbPort.Top = y; cbPort.Width = 180;
        cbPort.DropDownStyle = ComboBoxStyle.DropDownList;
        btnRefresh.Text = "↺"; btnRefresh.Left = 368; btnRefresh.Top = y;
        btnRefresh.Width = 40; btnRefresh.Height = 23;
        btnRefresh.Click += (_, _) => RefreshPorts();
        Controls.AddRange(new Control[] { cbPort, btnRefresh });
        y += 36;

        // ─ Carte ─
        AddLabel("Carte Arduino :", 16, y);
        cbBoard.Left = 180; cbBoard.Top = y; cbBoard.Width = 250;
        cbBoard.DropDownStyle = ComboBoxStyle.DropDownList;
        cbBoard.Items.AddRange(new object[] {
            "arduino:avr:uno",
            "arduino:avr:nano",
            "arduino:avr:mega" });
        cbBoard.SelectedIndex = 0;
        Controls.Add(cbBoard);
        y += 36;

        // ─ Sketch ─
        AddLabel("Fichier .ino :", 16, y);
        txtSketch.Left = 180; txtSketch.Top = y; txtSketch.Width = 230;
        txtSketch.Text = ResolveSketchPath();
        btnBrowse.Text = "…"; btnBrowse.Left = 418; btnBrowse.Top = y;
        btnBrowse.Width = 40; btnBrowse.Height = 23;
        btnBrowse.Click += BrowseSketch;
        Controls.AddRange(new Control[] { txtSketch, btnBrowse });
        y += 36;

        // ─ Bouton Flash ─
        btnFlash.Text = "⚡  Flasher l'Arduino";
        btnFlash.Left = 16; btnFlash.Top = y;
        btnFlash.Width = 220; btnFlash.Height = 34;
        btnFlash.BackColor = Color.FromArgb(30, 120, 180);
        btnFlash.ForeColor = Color.White;
        btnFlash.FlatStyle = FlatStyle.Flat;
        btnFlash.Font = new Font("Arial", 10, FontStyle.Bold);
        btnFlash.Click += FlashArduino;
        Controls.Add(btnFlash);
        y += 48;

        // ─ Statut ─
        lblStatus.Left = 16; lblStatus.Top = y;
        lblStatus.Width = 480; lblStatus.Height = 20;
        lblStatus.ForeColor = Color.Gray;
        Controls.Add(lblStatus);
        y += 24;

        // ─ Log ─
        rtLog.Left = 16; rtLog.Top = y;
        rtLog.Width = 482; rtLog.Height = 160;
        rtLog.ReadOnly = true;
        rtLog.BackColor = Color.Black;
        rtLog.ForeColor = Color.LightGray;
        rtLog.Font = new Font("Consolas", 9);
        rtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        Controls.Add(rtLog);
    }

    private void AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Left = x, Top = y + 4, Width = 160, AutoSize = true };
        Controls.Add(lbl);
    }

    // ── Logique ───────────────────────────────────────────────────────────

    private void RefreshPorts()
    {
        string? sel = cbPort.SelectedItem?.ToString();
        cbPort.Items.Clear();
        foreach (string p in SerialPort.GetPortNames().OrderBy(x => x))
            cbPort.Items.Add(p);
        if (sel != null && cbPort.Items.Contains(sel))
            cbPort.SelectedItem = sel;
        else if (cbPort.Items.Count > 0)
            cbPort.SelectedIndex = 0;
    }

    private string ResolveSketchPath()
    {
        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        string full = Path.GetFullPath(Path.Combine(exeDir, SketchRelPath));
        return File.Exists(full) ? full : "";
    }

    private void DetectArduinoCli()
    {
        string? path = FindArduinoCli();
        if (path == null)
        {
            Log("arduino-cli non trouvé.", Color.Orange);
            Log("Téléchargez-le sur https://arduino.github.io/arduino-cli/", Color.Orange);
            Log("Ou installez Arduino IDE 2.x qui l'inclut.", Color.Orange);
        }
        else
        {
            Log($"arduino-cli trouvé : {path}", Color.LightGreen);
        }
    }

    private static string? FindArduinoCli()
    {
        // Cherche dans PATH et dans les emplacements courants d'Arduino IDE
        string[] candidates = {
            "arduino-cli",
            @"C:\Program Files\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Arduino15\arduino-cli.exe",
            @"C:\Program Files (x86)\Arduino\arduino-cli.exe"
        };

        foreach (string c in candidates)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(c, "version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(2000);
                if (p?.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }

    private void BrowseSketch(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Sélectionner le sketch Arduino",
            Filter = "Sketches Arduino (*.ino)|*.ino",
            FileName = txtSketch.Text
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtSketch.Text = dlg.FileName;
    }

    private async void FlashArduino(object? sender, EventArgs e)
    {
        if (cbPort.SelectedItem == null)
        {
            MessageBox.Show("Sélectionnez un port COM.", "Port manquant",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(txtSketch.Text))
        {
            MessageBox.Show("Fichier .ino introuvable.\nUtilisez le bouton … pour le localiser.",
                "Sketch manquant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? cli = FindArduinoCli();
        if (cli == null)
        {
            MessageBox.Show(
                "arduino-cli introuvable.\n\n" +
                "Installez Arduino IDE 2.x depuis https://www.arduino.cc/en/software\n" +
                "puis relancez cette application.",
                "arduino-cli manquant", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string port   = cbPort.SelectedItem.ToString()!;
        string board  = cbBoard.SelectedItem?.ToString() ?? "arduino:avr:uno";
        string sketch = Path.GetDirectoryName(txtSketch.Text)!;

        btnFlash.Enabled = false;
        rtLog.Clear();
        SetStatus("Compilation en cours…", Color.CornflowerBlue);

        // Compile
        bool ok = await RunCliAsync(cli, $"compile --fqbn {board} \"{sketch}\"");
        if (!ok)
        {
            SetStatus("❌ Erreur de compilation.", Color.Tomato);
            btnFlash.Enabled = true;
            return;
        }

        SetStatus("Upload en cours…", Color.CornflowerBlue);

        // Upload
        ok = await RunCliAsync(cli, $"upload -p {port} --fqbn {board} \"{sketch}\"");

        SetStatus(ok ? "✅ Flash réussi !" : "❌ Erreur d'upload.", ok ? Color.LightGreen : Color.Tomato);
        btnFlash.Enabled = true;
    }

    private Task<bool> RunCliAsync(string cli, string args)
    {
        var tcs = new TaskCompletionSource<bool>();
        var psi = new ProcessStartInfo(cli, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        p.OutputDataReceived += (_, e) => { if (e.Data != null) LogSafe(e.Data, Color.LightGray); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data != null) LogSafe(e.Data, Color.LightYellow); };
        p.Exited += (_, _) => tcs.SetResult(p.ExitCode == 0);

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return tcs.Task;
    }

    private void LogSafe(string text, Color color)
    {
        if (InvokeRequired) { Invoke(() => Log(text, color)); return; }
        Log(text, color);
    }

    private void Log(string text, Color color)
    {
        rtLog.SelectionStart  = rtLog.TextLength;
        rtLog.SelectionLength = 0;
        rtLog.SelectionColor  = color;
        rtLog.AppendText(text + "\n");
        rtLog.ScrollToCaret();
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text, color)); return; }
        lblStatus.Text      = text;
        lblStatus.ForeColor = color;
    }
}
