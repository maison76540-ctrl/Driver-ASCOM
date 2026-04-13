[Setup]
AppName=ArduSafeMon ASCOM Safety Monitor
AppVersion=1.0
AppPublisher=dalex
DefaultDirName={autopf}\ASCOM\SafetyMonitor\ArduSafeMon
DefaultGroupName=ASCOM\ArduSafeMon
OutputDir=.\Installer
OutputBaseFilename=ArduSafeMon_Setup_v1.0
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1

[Files]
; DLL principale du driver
Source: "ArduSafeMon\bin\Release\ASCOM.ArduSafeMon.SafetyMonitor.dll"; \
  DestDir: "{app}"; Flags: ignoreversion

[Run]
; Enregistrement COM 64-bit
Filename: "{dotnet4064}\RegAsm.exe"; \
  Parameters: """{app}\ASCOM.ArduSafeMon.SafetyMonitor.dll"" /codebase"; \
  StatusMsg: "Enregistrement du driver ASCOM..."; \
  Flags: runhidden waituntilterminated

[UninstallRun]
; Désenregistrement COM 64-bit
Filename: "{dotnet4064}\RegAsm.exe"; \
  Parameters: """{app}\ASCOM.ArduSafeMon.SafetyMonitor.dll"" /unregister"; \
  Flags: runhidden waituntilterminated

[Messages]
WelcomeLabel2=Ce programme va installer le driver ASCOM ArduSafeMon Safety Monitor v1.0.%n%nCe driver permet à NINA de surveiller la position du toit via un Arduino Uno.%n%nASCOM Platform 6.2 ou supérieur doit être installé.
