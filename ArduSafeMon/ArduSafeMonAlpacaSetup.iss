[Setup]
AppName=ArduSafeMon Alpaca Safety Monitor
AppVersion=1.0
AppPublisher=dalex
AppPublisherURL=https://github.com/maison76540-ctrl/Driver-ASCOM
DefaultDirName={autopf}\ArduSafeMonAlpaca
DefaultGroupName=ArduSafeMon
OutputDir=.\Installer
OutputBaseFilename=ArduSafeMon_Setup_v1.0
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1

[Files]
; Serveur Alpaca (self-contained, aucun .NET requis)
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\ArduSafeMonAlpaca.exe"; \
  DestDir: "{app}"; Flags: ignoreversion

; Config — onlyifdoesntexist pour ne pas ecraser les settings de l'utilisateur
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\appsettings.json"; \
  DestDir: "{app}"; Flags: onlyifdoesntexist

; ArduFlasher — outil de mise a jour du firmware Arduino
Source: "ArduFlasher\bin\Release\net10.0-windows\win-x64\publish\ArduFlasher.exe"; \
  DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Menu Demarrer
Name: "{group}\ArduSafeMon Alpaca (serveur)"; Filename: "{app}\ArduSafeMonAlpaca.exe"
Name: "{group}\ArduFlasher (mise a jour firmware)"; Filename: "{app}\ArduFlasher.exe"
Name: "{group}\Desinstaller ArduSafeMon"; Filename: "{uninstallexe}"

; Raccourci bureau pour le serveur
Name: "{commondesktop}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"

; Demarrage automatique avec Windows (serveur uniquement)
Name: "{commonstartup}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"

[Run]
; Demarrer le serveur immediatement apres installation
Filename: "{app}\ArduSafeMonAlpaca.exe"; \
  Flags: nowait postinstall skipifsilent; \
  Description: "Demarrer ArduSafeMon Alpaca maintenant"

; Proposer d'ouvrir ArduFlasher pour flasher l'Arduino
Filename: "{app}\ArduFlasher.exe"; \
  Flags: nowait postinstall skipifsilent unchecked; \
  Description: "Ouvrir ArduFlasher pour mettre a jour le firmware Arduino"

[UninstallRun]
; Arreter le serveur avant desinstallation
Filename: "taskkill"; Parameters: "/F /IM ArduSafeMonAlpaca.exe"; \
  Flags: runhidden waituntilterminated; RunOnceId: "StopServer"

[Messages]
WelcomeLabel2=Ce programme va installer ArduSafeMon Alpaca Safety Monitor v1.0.%n%nContenu du package :%n  - ArduSafeMon Alpaca : serveur ASCOM Alpaca pour NINA%n  - ArduFlasher : outil de mise a jour du firmware Arduino%n%nAucune plateforme ASCOM ni .NET n'est requise.%n%nLe serveur demarrera automatiquement avec Windows.
