# ArduSafeMon ASCOM Driver v1.0 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Créer un driver ASCOM Safety Monitor en C# (.NET 4.6.2) qui communique avec un Arduino Uno via port série, avec thread de polling découplé, cache d'état thread-safe et mode simulation.

**Architecture:** Le driver expose `ISafetyMonitor` à NINA via COM. Un thread de polling interroge l'Arduino toutes les 2 secondes (`S#` → `safe#`/`notsafe#`) et met à jour un booléen en cache protégé par lock. En mode simulation, le port série n'est pas ouvert et `IsSafe` retourne un état fixe configuré dans le Setup.

**Tech Stack:** C# 7.3, .NET Framework 4.6.2, WinForms, ASCOM Platform 6.2 (`ASCOM.DeviceInterface.dll`, `ASCOM.Utilities.dll`), NUnit 3 (tests unitaires)

---

## Structure des fichiers

```
ArduSafeMon/                                  ← racine du projet VS
├── ArduSafeMon.sln
├── ArduSafeMon/                              ← projet driver (Class Library)
│   ├── ArduSafeMon.csproj
│   ├── Properties/
│   │   └── AssemblyInfo.cs                   ← GUID COM, version, COM visibility
│   ├── DriverProfile.cs                      ← lecture/écriture settings via ASCOM.Utilities.Profile
│   ├── ISerialPoller.cs                      ← interface pour injection/mock dans tests
│   ├── SerialPoller.cs                       ← thread de polling + parsing réponse Arduino
│   ├── Driver.cs                             ← classe principale ISafetyMonitor
│   ├── SetupDialogForm.cs                    ← WinForms dialog configuration
│   └── SetupDialogForm.Designer.cs
└── ArduSafeMon.Tests/                        ← projet NUnit (tests unitaires)
    ├── ArduSafeMon.Tests.csproj
    ├── SerialPollerParserTests.cs            ← teste le parsing safe#/notsafe#
    └── DriverSimulationTests.cs             ← teste le mode simulation
```

**Fichier Arduino modifié :** `ArduSafeMonV0_1/ArduSafeMonV0_1.ino`

---

## Prérequis

- Visual Studio 2019+ (Community suffisant)
- ASCOM Platform 6.2 installé → [https://github.com/ASCOMInitiative/ASCOMPlatform/releases](https://github.com/ASCOMInitiative/ASCOMPlatform/releases)
- Arduino IDE (pour flasher le firmware)
- NuGet : `NUnit 3.13`, `NUnit3TestAdapter 4.x`

---

## Task 1 : Firmware Arduino mis à jour

**Fichiers :**
- Modifier : `ArduSafeMonV0_1/ArduSafeMonV0_1.ino`

- [ ] **Étape 1.1 : Remplacer le contenu du fichier .ino**

```cpp
// ArduSafeMon v1.0
// Pin 8 : HIGH = toit ouvert = safe | LOW = toit fermé = unsafe
// Protocole : reçoit "S#" → répond "safe#" ou "notsafe#"

bool pinState = false;

void setup() {
  pinMode(8, INPUT);
  Serial.begin(9600);
  Serial.flush();
  Serial.print("notsafe#");  // état initial sécuritaire au démarrage
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('#');
    if (cmd == "S") {
      pinState = digitalRead(8);
      if (pinState == HIGH) {
        Serial.print("safe#");     // toit ouvert → observation possible
      } else {
        Serial.print("notsafe#");  // toit fermé → observation impossible
      }
    }
  }
}
```

- [ ] **Étape 1.2 : Flasher sur l'Arduino**

  Ouvrir `ArduSafeMonV0_1.ino` dans Arduino IDE → Vérifier → Téléverser.
  Ouvrir le Moniteur Série (9600 baud) → taper `S#` → vérifier la réponse `safe#` ou `notsafe#` selon la position de la pin 8.

- [ ] **Étape 1.3 : Committer**

```bash
git add ArduSafeMonV0_1/ArduSafeMonV0_1.ino
git commit -m "fix: remove blocking delay(10000) from Arduino firmware"
```

---

## Task 2 : Scaffold du projet Visual Studio

**Fichiers à créer :**
- `ArduSafeMon/ArduSafeMon.sln`
- `ArduSafeMon/ArduSafeMon/ArduSafeMon.csproj`
- `ArduSafeMon/ArduSafeMon/Properties/AssemblyInfo.cs`

- [ ] **Étape 2.1 : Créer la solution et le projet Class Library**

  Dans Visual Studio :
  1. Fichier → Nouveau → Projet
  2. Choisir **Bibliothèque de classes (.NET Framework)**
  3. Nom : `ArduSafeMon`, Framework : **.NET Framework 4.6.2**
  4. Emplacement : `C:\Users\dalex\Desktop\Driver ASCOM pour Capteur de toit\ArduSafeMon\`

- [ ] **Étape 2.2 : Configurer le .csproj pour COM**

  Double-cliquer sur le projet → Propriétés → onglet **Build** :
  - Cocher **Enregistrer pour COM Interop** (Register for COM Interop)
  - Platform Target : **x86** (obligatoire pour ASCOM 32-bit)

  Propriétés → onglet **Application** :
  - Nom de l'assembly : `ASCOM.ArduSafeMon.SafetyMonitor`
  - Espace de noms par défaut : `ASCOM.ArduSafeMon`

- [ ] **Étape 2.3 : Ajouter les références ASCOM**

  Clic droit sur Références → Ajouter une référence → Parcourir → sélectionner :
  - `C:\Program Files (x86)\Common Files\ASCOM\Interface\ASCOM.DeviceInterface.dll`
  - `C:\Program Files (x86)\Common Files\ASCOM\Utilities\ASCOM.Utilities.dll`

  Si le chemin est différent, chercher ces DLL dans `C:\Program Files (x86)\Common Files\ASCOM\`.

- [ ] **Étape 2.4 : Écrire `Properties/AssemblyInfo.cs`**

  Remplacer le contenu généré par :

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ASCOM SafetyMonitor Driver for ArduSafeMon")]
[assembly: AssemblyDescription("ASCOM Safety Monitor driver for Arduino roof sensor")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ASCOM.ArduSafeMon.SafetyMonitor")]
[assembly: AssemblyCopyright("")]
[assembly: ComVisible(false)]
// IMPORTANT : ce GUID identifie la typelib COM — ne JAMAIS le changer après déploiement
[assembly: Guid("5A3B7C91-2D4E-4F8A-B6C0-3E1D9F2A5B7C")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

  > **Note :** Générer un nouveau GUID unique via Visual Studio → Outils → Créer un GUID, ou `[System.Guid]::NewGuid()` en PowerShell. Utiliser le même GUID pour tout le projet.

- [ ] **Étape 2.5 : Ajouter le projet de tests NUnit**

  1. Clic droit sur la solution → Ajouter → Nouveau projet
  2. Choisir **Bibliothèque de classes (.NET Framework)**, nom : `ArduSafeMon.Tests`, Framework 4.6.2
  3. Via NuGet (clic droit sur `ArduSafeMon.Tests` → Gérer les packages NuGet) :
     - Installer `NUnit` version 3.13.3
     - Installer `NUnit3TestAdapter` version 4.5.0
  4. Ajouter une référence projet : `ArduSafeMon.Tests` → Références → Ajouter → Projets → `ArduSafeMon`

- [ ] **Étape 2.6 : Committer**

```bash
git add ArduSafeMon/
git commit -m "feat: scaffold Visual Studio solution with ASCOM references and NUnit test project"
```

---

## Task 3 : DriverProfile — gestion des paramètres

**Fichiers à créer :**
- `ArduSafeMon/ArduSafeMon/DriverProfile.cs`

- [ ] **Étape 3.1 : Créer `DriverProfile.cs`**

```csharp
using System;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Gère la lecture et l'écriture des paramètres du driver
    /// dans le registre ASCOM (ASCOM.Utilities.Profile).
    /// </summary>
    internal class DriverProfile
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
                PollIntervalMs = int.Parse(
                    profile.GetValue(DriverId, PollIntervalKey, string.Empty,
                        PollIntervalDefault.ToString()));
                SimulationMode = bool.Parse(
                    profile.GetValue(DriverId, SimulationModeKey, string.Empty,
                        SimulationModeDefault.ToString()));
                SimulatedSafe = bool.Parse(
                    profile.GetValue(DriverId, SimulatedSafeKey, string.Empty,
                        SimulatedSafeDefault.ToString()));
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
```

- [ ] **Étape 3.2 : Committer**

```bash
git add ArduSafeMon/ArduSafeMon/DriverProfile.cs
git commit -m "feat: add DriverProfile for ASCOM registry settings"
```

---

## Task 4 : ISerialPoller + SerialPoller — thread de polling

**Fichiers à créer :**
- `ArduSafeMon/ArduSafeMon/ISerialPoller.cs`
- `ArduSafeMon/ArduSafeMon/SerialPoller.cs`
- `ArduSafeMon/ArduSafeMon.Tests/SerialPollerParserTests.cs`

- [ ] **Étape 4.1 : Créer `ISerialPoller.cs`** (interface pour injection de dépendance)

```csharp
using System;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Abstraction du polling série, permet de mocker dans les tests.
    /// </summary>
    internal interface ISerialPoller : IDisposable
    {
        /// <summary>Dernier état lu sur l'Arduino (thread-safe).</summary>
        bool IsSafe { get; }

        /// <summary>Ouvre le port série et démarre le thread de polling.</summary>
        void Start();

        /// <summary>Arrête le thread de polling et ferme le port série.</summary>
        void Stop();
    }
}
```

- [ ] **Étape 4.2 : Écrire le test de parsing AVANT l'implémentation**

  Dans `ArduSafeMon.Tests/SerialPollerParserTests.cs` :

```csharp
using NUnit.Framework;
using ASCOM.ArduSafeMon;

namespace ArduSafeMon.Tests
{
    [TestFixture]
    public class SerialPollerParserTests
    {
        [Test]
        public void ParseResponse_SafeHash_ReturnsTrue()
        {
            bool result = SerialPoller.ParseResponse("safe");
            Assert.That(result, Is.True);
        }

        [Test]
        public void ParseResponse_NotsafeHash_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("notsafe");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_EmptyString_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_UnknownString_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("garbage");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_CaseInsensitive_Safe_ReturnsTrue()
        {
            bool result = SerialPoller.ParseResponse("SAFE");
            Assert.That(result, Is.True);
        }
    }
}
```

- [ ] **Étape 4.3 : Lancer les tests — vérifier qu'ils échouent**

  Visual Studio → Test → Exécuter tous les tests.
  Attendu : ÉCHEC — `SerialPoller.ParseResponse` n'existe pas encore.

- [ ] **Étape 4.4 : Créer `SerialPoller.cs`**

```csharp
using System;
using System.IO.Ports;
using System.Threading;
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
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "#"
            };
            _serialPort.Open();

            // Vider le buffer de démarrage de l'Arduino
            System.Threading.Thread.Sleep(500);
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
            _cts?.Cancel();
            _pollThread?.Join(2000);

            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();

            _serialPort?.Dispose();
            _serialPort = null;
            _logger?.LogMessage("SerialPoller", "Stopped");
        }

        public void Dispose() => Stop();

        // ── Logique de polling ───────────────────────────────────────────

        private void PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    _logger?.LogMessage("SerialPoller", $"Error: {ex.Message} → unsafe");
                    lock (_lock) { _isSafe = false; }

                    // Attendre avant de retenter (évite une boucle d'erreurs rapide)
                    WaitOrCancel(5000, token);
                    TryReconnect(token);
                    continue;
                }

                WaitOrCancel(_pollIntervalMs, token);
            }
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
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };
                    _serialPort.Open();
                    System.Threading.Thread.Sleep(500);
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
```

  > **Note :** Ajouter `using System.Threading.Tasks;` en haut du fichier.

- [ ] **Étape 4.5 : Lancer les tests — vérifier qu'ils passent**

  Visual Studio → Test → Exécuter tous les tests.
  Attendu : 5 tests PASS.

- [ ] **Étape 4.6 : Committer**

```bash
git add ArduSafeMon/ArduSafeMon/ISerialPoller.cs
git add ArduSafeMon/ArduSafeMon/SerialPoller.cs
git add ArduSafeMon/ArduSafeMon.Tests/SerialPollerParserTests.cs
git commit -m "feat: add SerialPoller with background polling thread and response parsing"
```

---

## Task 5 : Driver.cs — classe principale ASCOM

**Fichiers à créer :**
- `ArduSafeMon/ArduSafeMon/Driver.cs`
- `ArduSafeMon/ArduSafeMon.Tests/DriverSimulationTests.cs`

- [ ] **Étape 5.1 : Écrire les tests du mode simulation AVANT l'implémentation**

  Dans `ArduSafeMon.Tests/DriverSimulationTests.cs` :

```csharp
using NUnit.Framework;
using ASCOM.ArduSafeMon;

namespace ArduSafeMon.Tests
{
    /// <summary>
    /// Teste la logique du driver en mode simulation
    /// (sans port série, sans ASCOM Platform installé).
    /// </summary>
    [TestFixture]
    public class DriverSimulationTests
    {
        [Test]
        public void SimulationMode_SimulatedSafeTrue_IsSafeReturnsTrue()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            driver.Connect();
            Assert.That(driver.IsSafe, Is.True);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_SimulatedSafeFalse_IsSafeReturnsFalse()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: false);
            driver.Connect();
            Assert.That(driver.IsSafe, Is.False);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_ConnectSucceeds_WithoutSerialPort()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.DoesNotThrow(() => driver.Connect());
            Assert.That(driver.Connected, Is.True);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_DisconnectSetsConnectedFalse()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            driver.Connect();
            driver.Disconnect();
            Assert.That(driver.Connected, Is.False);
        }

        [Test]
        public void Name_ReturnsArduSafeMon()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.That(driver.Name, Is.EqualTo("ArduSafeMon"));
        }

        [Test]
        public void InterfaceVersion_Returns2()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.That(driver.InterfaceVersion, Is.EqualTo((short)2));
        }
    }
}
```

- [ ] **Étape 5.2 : Lancer les tests — vérifier qu'ils échouent**

  Attendu : ÉCHEC — `SafetyMonitor` n'existe pas encore.

- [ ] **Étape 5.3 : Créer `Driver.cs`**

```csharp
using System;
using System.Collections;
using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Driver ASCOM Safety Monitor pour capteur de position de toit Arduino.
    /// ProgID : ASCOM.ArduSafeMon.SafetyMonitor
    /// </summary>
    [ComVisible(true)]
    [Guid("5A3B7C91-2D4E-4F8A-B6C0-3E1D9F2A5B7C")]
    [ProgId("ASCOM.ArduSafeMon.SafetyMonitor")]
    [ServedClassName("ArduSafeMon Safety Monitor")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SafetyMonitor : ISafetyMonitor, IDisposable
    {
        private const string DriverId = "ASCOM.ArduSafeMon.SafetyMonitor";
        private const string DriverDescription = "ArduSafeMon Safety Monitor";

        private readonly TraceLogger _logger;
        private readonly DriverProfile _profile;
        private ISerialPoller _poller;
        private bool _connected = false;

        // Paramètres overrides pour les tests (injection directe)
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
                if (!_connected)
                    throw new InvalidOperationException("Driver non connecté.");

                bool simMode = _testMode ? _testSimulationMode : _profile.SimulationMode;
                if (simMode)
                {
                    bool simSafe = _testMode ? _testSimulatedSafe : _profile.SimulatedSafe;
                    _logger?.LogMessage("IsSafe", $"[SIMULATION] → {simSafe}");
                    return simSafe;
                }

                return _poller?.IsSafe ?? false;
            }
        }

        public string Name => "ArduSafeMon";
        public string Description => "Arduino Roof Safety Monitor";
        public string DriverInfo => "ArduSafeMon v1.0 - ASCOM Safety Monitor for roof sensor";
        public string DriverVersion => "1.0";
        public short InterfaceVersion => 2;

        public ArrayList SupportedActions => new ArrayList();

        public string Action(string ActionName, string ActionParameters)
            => throw new ActionNotImplementedException("Action " + ActionName);

        public void CommandBlind(string Command, bool Raw = false)
            => throw new MethodNotImplementedException("CommandBlind");

        public bool CommandBool(string Command, bool Raw = false)
            => throw new MethodNotImplementedException("CommandBool");

        public string CommandString(string Command, bool Raw = false)
            => throw new MethodNotImplementedException("CommandString");

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
            Disconnect();
            _logger?.Dispose();
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

            _poller?.Stop();
            _poller?.Dispose();
            _poller = null;
            _connected = false;
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
```

- [ ] **Étape 5.4 : Lancer les tests — vérifier qu'ils passent**

  Visual Studio → Test → Exécuter tous les tests.
  Attendu : 6 tests `DriverSimulationTests` PASS + 5 tests `SerialPollerParserTests` PASS = **11 tests PASS**.

- [ ] **Étape 5.5 : Committer**

```bash
git add ArduSafeMon/ArduSafeMon/Driver.cs
git add ArduSafeMon/ArduSafeMon.Tests/DriverSimulationTests.cs
git commit -m "feat: implement SafetyMonitor driver with simulation mode and COM registration"
```

---

## Task 6 : SetupDialogForm — interface de configuration

**Fichiers à créer :**
- `ArduSafeMon/ArduSafeMon/SetupDialogForm.cs`
- `ArduSafeMon/ArduSafeMon/SetupDialogForm.Designer.cs`

- [ ] **Étape 6.1 : Créer `SetupDialogForm.cs`**

```csharp
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
```

- [ ] **Étape 6.2 : Créer `SetupDialogForm.Designer.cs`**

```csharp
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
```

- [ ] **Étape 6.3 : Vérifier que le projet compile sans erreur**

  Visual Studio → Générer → Générer la solution.
  Attendu : `0 erreur(s)`, éventuellement des avertissements mineurs.

- [ ] **Étape 6.4 : Committer**

```bash
git add ArduSafeMon/ArduSafeMon/SetupDialogForm.cs
git add ArduSafeMon/ArduSafeMon/SetupDialogForm.Designer.cs
git commit -m "feat: add SetupDialogForm with COM port, poll interval and simulation controls"
```

---

## Task 7 : Build, enregistrement COM et test avec NINA

- [ ] **Étape 7.1 : Compiler en mode Release (x86)**

  Visual Studio → Configuration : **Release**, Platform : **x86** → Générer → Générer la solution.
  Vérifier que `bin\x86\Release\ASCOM.ArduSafeMon.SafetyMonitor.dll` est créé.

- [ ] **Étape 7.2 : Enregistrer le driver COM**

  Ouvrir une **invite de commande en mode Administrateur** :

```cmd
cd "C:\Users\dalex\Desktop\Driver ASCOM pour Capteur de toit\ArduSafeMon\ArduSafeMon\bin\x86\Release"
"%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" ASCOM.ArduSafeMon.SafetyMonitor.dll /codebase /tlb
```

  Attendu : `Types registered successfully`

- [ ] **Étape 7.3 : Vérifier l'enregistrement dans ASCOM Diagnostics**

  Démarrer → ASCOM Platform → ASCOM Diagnostics.
  Vérifier que `ArduSafeMon Safety Monitor` apparaît dans la liste des Safety Monitors.

- [ ] **Étape 7.4 : Test mode simulation dans NINA**

  1. Ouvrir NINA → Options → Equipement → Safety Monitor
  2. Sélectionner `ArduSafeMon Safety Monitor`
  3. Cliquer **Setup** → cocher **Mode simulation** → sélectionner **Safe** → OK
  4. Cliquer **Connect** → vérifier que l'état affiché est **Safe** ✅
  5. Cliquer **Setup** → sélectionner **Unsafe** → OK → **Reconnecter**
  6. Vérifier que l'état est **Unsafe** ✅

- [ ] **Étape 7.5 : Test mode normal (Arduino branché)**

  1. Flasher le firmware mis à jour sur l'Arduino (Task 1)
  2. Brancher l'Arduino, noter le port COM (Gestionnaire de périphériques)
  3. Dans NINA → Setup → **décocher** Mode simulation → sélectionner le bon COM → OK
  4. Connect → vérifier l'état cohérent avec la position physique de la pin 8
  5. Laisser tourner 5 minutes → aucune oscillation erratique ✅

- [ ] **Étape 7.6 : Test de déconnexion Arduino**

  1. Arduino connecté et driver en mode normal
  2. Débrancher l'USB de l'Arduino
  3. NINA doit passer en **Unsafe** dans les 5 secondes ✅
  4. Rebrancher l'Arduino → reconnexion automatique et retour à l'état correct ✅

- [ ] **Étape 7.7 : Commit final**

```bash
git add ArduSafeMon/
git commit -m "feat: complete ArduSafeMon ASCOM driver v1.0 - ready for testing"
```

- [ ] **Étape 7.8 : Push sur GitHub**

```bash
git push origin feature/ardusafemon-v1-ascom-driver
```

---

## Récapitulatif des tests

| # | Test | Mode | Résultat attendu |
|---|---|---|---|
| 1 | Arduino répond à `S#` | Firmware | `safe#` ou `notsafe#` immédiat |
| 2 | `ParseResponse("safe")` | Unitaire | `true` |
| 3 | `ParseResponse("notsafe")` | Unitaire | `false` |
| 4 | Simulation Safe → `IsSafe` | Unitaire | `true` |
| 5 | Simulation Unsafe → `IsSafe` | Unitaire | `false` |
| 6 | Connect sans Arduino (simulation) | Unitaire | Réussit |
| 7 | NINA affiche Safe (simulation) | Intégration | ✅ |
| 8 | NINA affiche Unsafe (simulation) | Intégration | ✅ |
| 9 | Stabilité 5 min (normal) | Intégration | Pas d'oscillation |
| 10 | Déconnexion Arduino → Unsafe | Intégration | < 5 secondes |
