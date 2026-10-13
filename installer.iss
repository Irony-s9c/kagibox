; KagiBox Inno Setup script
; Packs "dist\KagiBox" (KagiBox.exe = WinUI 3 app, self-contained, plus
; KagiBox-core.exe = the Python core with its "core-lib" folder) into a Windows installer.
; Build: ISCC.exe installer.iss

; MyAppVersion is read from changelog\VERSION (the single source of truth
; also used by the core and the UI project) so the installer filename, the
; app's own version display, and the release tag can't drift apart.
#define VersionFileHandle
#define MyAppVersion "0.0.0"
#if VersionFileHandle = FileOpen(AddBackslash(SourcePath) + "changelog\VERSION")
  #define MyAppVersion Trim(FileRead(VersionFileHandle))
  #expr FileClose(VersionFileHandle)
#endif

#define MyAppName "KagiBox"
#define MyAppPublisher "KagiBox Project"
#define MyAppExeName "KagiBox.exe"

[Setup]
; AppId must never change, otherwise updates install side by side.
AppId={{6E1B6E2E-2E5A-4B9C-9C7A-KAGIBOX000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=KagiBox-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=images\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Normal installs go to Program Files (admin). "/CURRENTUSER" on the command line installs for the
; current user only without admin rights (used by the install test in scripts\install-test.ps1).
PrivilegesRequiredOverridesAllowed=commandline
; The in-app updater (backend/api.py: download_and_apply_update) launches
; this installer with /SILENT, then quits KagiBox itself; these two settings
; are a second line of defense in case that self-quit is too slow and
; Setup.exe still finds KagiBox.exe holding its own files open.
CloseApplications=yes
RestartApplications=no
; No code-signing certificate is used for this personal-use build; see
; README.md for the SmartScreen "More info -> Run anyway" workaround.

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "dist\KagiBox\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; No "skipifsilent": after a silent in-app update KagiBox restarts by itself.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall
