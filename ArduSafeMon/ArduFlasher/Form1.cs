using System.Diagnostics;
using System.IO.Ports;

namespace ArduFlasher;

public partial class Form1 : Form
{
    // ── Palette de couleurs ───────────────────────────────────────────────
    static readonly Color BG        = Color.FromArgb(18,  18,  32);
    static readonly Color PANEL     = Color.FromArgb(28,  28,  48);
    static readonly Color ACCENT    = Color.FromArgb(80, 160, 255);
    static readonly Color ACCENT2   = Color.FromArgb(40, 200, 120);
    static readonly Color TEXT      = Color.FromArgb(220, 220, 240);
    static readonly Color TEXTDIM   = Color.FromArgb(140, 140, 170);
    static readonly Color BORDER    = Color.FromArgb(50,  50,  80);
    static readonly Color BTN_FLASH = Color.FromArgb(50, 160, 80);
    static readonly Color BTN_HOV   = Color.FromArgb(60, 190, 100);

    private static readonly string SketchRelPath =
        Path.Combine("..", "ArduSafeMonV0_1", "ArduSafeMonV0_1.ino");

    // ── Contrôles ─────────────────────────────────────────────────────────
    private ComboBox    cbPort    = new();
    private ComboBox    cbBoard   = new();
    private TextBox     txtSketch = new();
    private Button      btnBrowse = new();
    private Button      btnRefresh= new();
    private Button      btnFlash  = new();
    private RichTextBox rtLog     = new();
    private Label       lblStatus = new();
    private Panel       pnlHeader = new();
    private Panel       pnlBody   = new();
    private Panel       pnlLog    = new();
    private ProgressBar progress  = new();

    public Form1()
    {
        InitializeComponent();
        Text            = "ArduSafeMon — Flash Arduino";
        ClientSize      = new Size(580, 540);
        BackColor       = BG;
        ForeColor       = TEXT;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);

        BuildHeader();
        BuildBody();
        BuildLog();

        RefreshPorts();
        DetectArduinoCli();
    }

    // ── Header ────────────────────────────────────────────────────────────
    private void BuildHeader()
    {
        pnlHeader.Dock      = DockStyle.Top;
        pnlHeader.Height    = 72;
        pnlHeader.BackColor = PANEL;
        pnlHeader.Paint    += (s, e) =>
        {
            var g = e.Graphics;
            // Bande de couleur en bas du header
            g.FillRectangle(new SolidBrush(ACCENT), 0, pnlHeader.Height - 3, pnlHeader.Width, 3);
        };

        // Icône Arduino (texte stylisé)
        var lblIcon = new Label
        {
            Text      = "⚡",
            Font      = new Font("Segoe UI", 28),
            ForeColor = ACCENT,
            Left = 20, Top = 10, AutoSize = true
        };

        var lblTitle = new Label
        {
            Text      = "ArduSafeMon Flasher",
            Font      = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = TEXT,
            Left = 70, Top = 10, AutoSize = true
        };

        var lblSub = new Label
        {
            Text      = "Mise à jour du firmware Arduino",
            Font      = new Font("Segoe UI", 9),
            ForeColor = TEXTDIM,
            Left = 72, Top = 44, AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblSub });
        Controls.Add(pnlHeader);
    }

    // ── Corps du formulaire ───────────────────────────────────────────────
    private void BuildBody()
    {
        pnlBody.Top       = 72;
        pnlBody.Left      = 0;
        pnlBody.Width     = 580;
        pnlBody.Height    = 230;
        pnlBody.BackColor = BG;

        int y = 20;

        // ─ Port COM ─
        AddSectionLabel(pnlBody, "PORT SÉRIE", 24, y - 4);
        y += 20;

        cbPort.Left        = 24;   cbPort.Top    = y;
        cbPort.Width       = 240;  cbPort.Height = 30;
        cbPort.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(cbPort);

        btnRefresh.Left      = 272;  btnRefresh.Top   = y;
        btnRefresh.Width     = 44;   btnRefresh.Height = 28;
        btnRefresh.Text      = "↺";
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.BackColor = PANEL;
        btnRefresh.ForeColor = ACCENT;
        btnRefresh.Font      = new Font("Segoe UI", 13);
        btnRefresh.FlatAppearance.BorderColor = BORDER;
        btnRefresh.Click    += (_, _) => RefreshPorts();
        btnRefresh.Cursor    = Cursors.Hand;

        pnlBody.Controls.AddRange(new Control[] { cbPort, btnRefresh });
        y += 44;

        // ─ Carte ─
        AddSectionLabel(pnlBody, "CARTE ARDUINO", 24, y - 4);
        y += 20;

        cbBoard.Left         = 24;  cbBoard.Top   = y;
        cbBoard.Width        = 292; cbBoard.Height = 30;
        cbBoard.DropDownStyle = ComboBoxStyle.DropDownList;
        cbBoard.Items.AddRange(new object[] {
            "arduino:megaavr:nona4809            (Arduino Nano Every)",
            "arduino:avr:nano:cpu=atmega328      (Arduino Nano — nouveau bootloader)",
            "arduino:avr:nano:cpu=atmega328old   (Arduino Nano — ancien bootloader / clone CH340)",
            "arduino:avr:uno                     (Arduino Uno / Uno R3)",
            "arduino:avr:mega:cpu=atmega2560     (Arduino Mega 2560)" });
        cbBoard.SelectedIndex = 0;
        StyleCombo(cbBoard);
        pnlBody.Controls.Add(cbBoard);
        y += 44;

        // ─ Sketch ─
        AddSectionLabel(pnlBody, "FICHIER SKETCH (.ino)", 24, y - 4);
        y += 20;

        txtSketch.Left      = 24;  txtSketch.Top    = y;
        txtSketch.Width     = 430; txtSketch.Height = 28;
        txtSketch.Text      = ResolveSketchPath();
        txtSketch.BackColor = PANEL;
        txtSketch.ForeColor = TEXT;
        txtSketch.BorderStyle = BorderStyle.FixedSingle;

        btnBrowse.Left      = 462; btnBrowse.Top  = y;
        btnBrowse.Width     = 90;  btnBrowse.Height = 28;
        btnBrowse.Text      = "Parcourir…";
        btnBrowse.FlatStyle = FlatStyle.Flat;
        btnBrowse.BackColor = PANEL;
        btnBrowse.ForeColor = ACCENT;
        btnBrowse.FlatAppearance.BorderColor = BORDER;
        btnBrowse.Click += BrowseSketch;
        btnBrowse.Cursor = Cursors.Hand;

        pnlBody.Controls.AddRange(new Control[] { txtSketch, btnBrowse });

        Controls.Add(pnlBody);
    }

    private void BuildLog()
    {
        // ─ Bouton Flash ─
        btnFlash.Top        = 308;
        btnFlash.Left       = 24;
        btnFlash.Width      = 534;
        btnFlash.Height     = 44;
        btnFlash.Text       = "⚡   FLASHER L'ARDUINO";
        btnFlash.Font       = new Font("Segoe UI", 12, FontStyle.Bold);
        btnFlash.BackColor  = BTN_FLASH;
        btnFlash.ForeColor  = Color.White;
        btnFlash.FlatStyle  = FlatStyle.Flat;
        btnFlash.FlatAppearance.BorderSize = 0;
        btnFlash.Cursor     = Cursors.Hand;
        btnFlash.Click     += FlashArduino;
        btnFlash.MouseEnter += (_, _) => btnFlash.BackColor = BTN_HOV;
        btnFlash.MouseLeave += (_, _) => btnFlash.BackColor = BTN_FLASH;

        // ─ Barre de progression ─
        progress.Top     = 358;
        progress.Left    = 24;
        progress.Width   = 534;
        progress.Height  = 6;
        progress.Style   = ProgressBarStyle.Marquee;
        progress.Visible = false;
        progress.MarqueeAnimationSpeed = 30;

        // ─ Statut ─
        lblStatus.Top       = 368;
        lblStatus.Left      = 24;
        lblStatus.Width     = 534;
        lblStatus.Height    = 22;
        lblStatus.ForeColor = TEXTDIM;
        lblStatus.Text      = "Prêt.";
        lblStatus.Font      = new Font("Segoe UI", 9, FontStyle.Italic);

        // ─ Panel log ─
        pnlLog.Top       = 396;
        pnlLog.Left      = 24;
        pnlLog.Width     = 534;
        pnlLog.Height    = 128;
        pnlLog.BackColor = Color.Black;
        pnlLog.BorderStyle = BorderStyle.FixedSingle;

        rtLog.Dock      = DockStyle.Fill;
        rtLog.ReadOnly  = true;
        rtLog.BackColor = Color.FromArgb(10, 10, 20);
        rtLog.ForeColor = Color.LightGray;
        rtLog.Font      = new Font("Consolas", 8.5f);
        rtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        rtLog.BorderStyle = BorderStyle.None;

        pnlLog.Controls.Add(rtLog);

        Controls.AddRange(new Control[] { btnFlash, progress, lblStatus, pnlLog });
    }

    // ── Helpers de style ──────────────────────────────────────────────────
    private void StyleCombo(ComboBox cb)
    {
        cb.BackColor  = PANEL;
        cb.ForeColor  = TEXT;
        cb.FlatStyle  = FlatStyle.Flat;
    }

    private void AddSectionLabel(Control parent, string text, int x, int y)
    {
        var lbl = new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = ACCENT,
            Left = x, Top = y, AutoSize = true
        };
        parent.Controls.Add(lbl);
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

        if (cbPort.Items.Count == 0)
            Log("Aucun port COM détecté. Branchez l'Arduino.", Color.Orange);
    }

    private string ResolveSketchPath()
    {
        string exeDir = AppContext.BaseDirectory;
        string full   = Path.GetFullPath(Path.Combine(exeDir, SketchRelPath));
        return File.Exists(full) ? full : "";
    }

    private void DetectArduinoCli()
    {
        string? path = FindArduinoCli();
        if (path == null)
        {
            Log("⚠  arduino-cli non trouvé.", Color.Orange);
            Log("   → Installez Arduino IDE 2.x : https://www.arduino.cc/en/software", Color.Orange);
        }
        else
        {
            Log($"✔  arduino-cli : {path}", ACCENT2);
        }
    }

    private static string? FindArduinoCli()
    {
        string[] candidates = {
            "arduino-cli",
            // Arduino IDE 2.x installé dans Program Files
            @"C:\Program Files\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe",
            // Arduino IDE 2.x installé dans AppData\Local\Programs (installation utilisateur)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe"),
            // arduino-cli standalone dans AppData\Local\Arduino15
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Arduino15\arduino-cli.exe",
            // Arduino IDE 1.x
            @"C:\Program Files (x86)\Arduino\arduino-cli.exe"
        };
        foreach (string c in candidates)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(c, "version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
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
            Title  = "Sélectionner le sketch Arduino",
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
            MessageBox.Show(
                "Fichier .ino introuvable.\nUtilisez le bouton Parcourir pour le localiser.",
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

        // FQBN : prendre seulement la première partie avant l'espace
        string fqbn   = cbBoard.SelectedItem!.ToString()!.Split(' ')[0];
        string port   = cbPort.SelectedItem.ToString()!;
        string sketch = Path.GetDirectoryName(txtSketch.Text)!;
        bool   isNanoEvery = fqbn.StartsWith("arduino:megaavr");

        btnFlash.Enabled = false;
        progress.Visible = true;
        rtLog.Clear();

        // ─ Étape 1 : compilation ─
        SetStatus("⚙  Compilation en cours…", ACCENT);
        Log("─── Compilation ─────────────────────────", TEXTDIM);
        bool ok = await RunCliAsync(cli, $"compile --fqbn {fqbn} \"{sketch}\"");

        if (!ok)
        {
            SetStatus("❌  Erreur de compilation.", Color.Tomato);
            progress.Visible = false;
            btnFlash.Enabled = true;
            return;
        }

        // ─ Étape 1b : libérer le port (arrêt ArduSafeMon si nécessaire) ─
        bool serverWasRunning = false;
        if (isNanoEvery)
        {
            SetStatus("🔄  Vérification du port…", ACCENT);
            Log("─── Libération du port ───────────────────", TEXTDIM);

            // Vérifier si le port est occupé (ex: ArduSafeMonAlpaca.exe le tient ouvert)
            bool portBusy = IsPortBusy(port);
            if (portBusy)
            {
                Log($"⚠  Port {port} occupé — arrêt du serveur ArduSafeMon…", Color.Orange);
                serverWasRunning = StopArduSafeMon();
                await Task.Delay(1500); // laisser le port se libérer
                Log("✔  Serveur arrêté.", Color.LightGray);
            }
            else
            {
                Log($"✔  Port {port} disponible.", Color.LightGray);
            }

            // 1200-baud touch pour déclencher le bootloader sans bouton RESET
            SetStatus("🔄  Activation du bootloader (1200 baud touch)…", ACCENT);
            Log("─── Bootloader touch ─────────────────────", TEXTDIM);
            try
            {
                using var sp = new SerialPort(port, 1200);
                sp.Open();
                await Task.Delay(200);
                sp.Close();
                Log($"✔  Port {port} ouvert/fermé à 1200 baud.", Color.LightGray);
            }
            catch (Exception ex)
            {
                Log($"⚠  1200-baud touch échoué : {ex.Message}", Color.Orange);
            }
            // Attendre que le bootloader soit prêt (~2 s)
            Log("   Attente du bootloader…", Color.LightGray);
            await Task.Delay(2000);
        }

        // ─ Étape 2 : upload ─
        SetStatus("⬆  Upload en cours…", ACCENT);
        Log("─── Upload ───────────────────────────────", TEXTDIM);
        ok = await RunCliAsync(cli, $"upload -p {port} --fqbn {fqbn} \"{sketch}\"");

        progress.Visible = false;
        btnFlash.Enabled = true;

        if (ok)
        {
            SetStatus("✅  Flash réussi ! L'Arduino est à jour.", ACCENT2);
            Log("─── Terminé ──────────────────────────────", TEXTDIM);
            Log("✔  Firmware flashé avec succès.", ACCENT2);
        }
        else
        {
            SetStatus("❌  Erreur lors de l'upload.", Color.Tomato);
        }

        // Relancer le serveur ArduSafeMon s'il tournait avant le flash
        if (serverWasRunning)
        {
            Log("─── Redémarrage du serveur ───────────────", TEXTDIM);
            await Task.Delay(1000);
            StartArduSafeMon();
            Log("✔  ArduSafeMonAlpaca relancé.", ACCENT2);
        }
    }

    // ── Gestion du serveur ArduSafeMon ───────────────────────────────────────
    private static bool IsPortBusy(string port)
    {
        try
        {
            using var sp = new SerialPort(port, 9600);
            sp.Open();
            sp.Close();
            return false; // port libre
        }
        catch
        {
            return true; // port occupé
        }
    }

    private static bool StopArduSafeMon()
    {
        var procs = Process.GetProcessesByName("ArduSafeMonAlpaca");
        foreach (var p in procs)
        {
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        }
        return procs.Length > 0;
    }

    private static void StartArduSafeMon()
    {
        // Chercher l'exe dans le même dossier que ArduFlasher
        string[] candidates = {
            Path.Combine(AppContext.BaseDirectory, "ArduSafeMonAlpaca.exe"),
            Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory) ?? ".", "ArduSafeMonAlpaca.exe"),
        };
        foreach (string c in candidates)
        {
            if (File.Exists(c))
            {
                Process.Start(new ProcessStartInfo(c) { UseShellExecute = true });
                return;
            }
        }
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
        lblStatus.Font      = new Font("Segoe UI", 9,
            text.StartsWith("✅") ? FontStyle.Bold : FontStyle.Italic);
    }
}
