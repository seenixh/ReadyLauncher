; Ready Launcher's installer (Inno Setup 6). Made by release.ps1, which passes the version and the files:
;   ISCC /DAppVersion=2.4.0 /DSourceExe=..\bin\ReadyLauncher.exe /O<folder> /F<file name> installer\ReadyLauncher.iss
; It installs for the current Windows account only (no administrator needed), in %LOCALAPPDATA%\Programs\Ready Launcher,
; with a Start menu shortcut (and a desktop one if wanted). The icons, notes and settings stay in %APPDATA%\ReadyLauncher,
; where every non-portable copy keeps them, so uninstalling leaves them; "Start with Windows" is Ready Launcher's own
; setting, and uninstalling removes its startup entry if it starts this copy.
; /DTestBuild makes a copy for testing the installer itself: another name and ID, its startup entry in a test key, and
; no check for a running Ready Launcher (/DTestMutex puts that check back).

#ifndef AppVersion
  #error Pass the version: /DAppVersion=2.4.0
#endif
#ifndef SourceExe
  #define SourceExe "..\bin\ReadyLauncher.exe"
#endif

#ifdef TestBuild
  #define AppId "{{4F1C2E0A-9B7D-4C1E-8A55-0D3E6B2F7A91}"
  #define AppName "Ready Launcher Test"
  #define RunKey "Software\ReadyLauncherInstallerTest\Run"
#else
  #define AppId "{{28B8C198-7081-4A52-AAB8-7B5DE8F74E03}"
  #define AppName "Ready Launcher"
  #define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#endif

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=seenixh
AppPublisherURL=https://github.com/seenixh/ReadyLauncher
AppSupportURL=https://github.com/seenixh/ReadyLauncher/issues
AppUpdatesURL=https://github.com/seenixh/ReadyLauncher/releases
AppCopyright=seenixh
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
; For this Windows account only: {autopf} is %LOCALAPPDATA%\Programs, and there's no administrator prompt.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=yes
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\ReadyLauncher.exe
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
#if !defined(TestBuild) || defined(TestMutex)
; Ready Launcher runs one copy at a time: Setup and the uninstaller ask to close a running one first.
AppMutex=Local\ReadyLauncher.SingleInstance
#endif
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Put a shortcut on the desktop"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\ReadyLauncher.exe"; Comment: "An always-on-top app launcher (Ctrl + Alt + L opens or hides it)"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\ReadyLauncher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ReadyLauncher.exe"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; What Check for updates leaves after swapping in a new version (it goes the next time Ready Launcher starts).
Type: files; Name: "{app}\ReadyLauncher.exe.old"

[Code]
// Uninstalling: the startup entry goes too, if it starts this copy (one that starts another copy is left alone).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, '{#RunKey}', 'ReadyLauncher', Command) then
      if Pos(Lowercase(ExpandConstant('{app}\ReadyLauncher.exe')), Lowercase(Command)) > 0 then
        RegDeleteValue(HKCU, '{#RunKey}', 'ReadyLauncher');
end;
