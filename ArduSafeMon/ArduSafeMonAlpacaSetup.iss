[Setup]
AppName=ArduSafeMon Alpaca Safety Monitor
AppVersion=1.0
AppPublisher=dalex
DefaultDirName={autopf}\ArduSafeMonAlpaca
DefaultGroupName=ArduSafeMon
OutputDir=.\Installer
OutputBaseFilename=ArduSafeMonAlpaca_Setup_v1.0
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1

[Files]
; Serveur Alpaca (self-contained, aucun .NET requis)
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\ArduSafeMonAlpaca.exe"; \
  DestDir: "{app}"; Flags: ignoreversion

; Config — onlyifdoesntexist pour ne pas écraser les settings de l'utilisateur
Source: "ArduSafeMonAlpaca\ArduSafeMonAlpaca\bin\Release\net8.0\win-x64\publish\appsettings.json"; \
  DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
; Raccourci dans le menu Démarrer
Name: "{group}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"
Name: "{group}\Désinstaller ArduSafeMon Alpaca"; Filename: "{uninstallexe}"

; Démarrage automatique avec Windows (répertoire Startup de l'utilisateur)
Name: "{userstartup}\ArduSafeMon Alpaca"; Filename: "{app}\ArduSafeMonAlpaca.exe"

[Run]
; Démarrer le serveur immédiatement après installation
Filename: "{app}\ArduSafeMonAlpaca.exe"; \
  Flags: nowait postinstall skipifsilent; \
  Description: "Démarrer ArduSafeMon Alpaca maintenant"

[UninstallRun]
; Arrêter le serveur avant désinstallation
Filename: "taskkill"; Parameters: "/F /IM ArduSafeMonAlpaca.exe"; \
  Flags: runhidden waituntilterminated

[Messages]
WelcomeLabel2=Ce programme va installer ArduSafeMon Alpaca Safety Monitor v1.0.%n%nCe serveur permet à NINA de surveiller la position du toit via un Arduino Uno.%n%nAucune plateforme ASCOM n'est requise.%n%nLe serveur démarrera automatiquement avec Windows.
