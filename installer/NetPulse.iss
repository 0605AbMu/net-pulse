; NetPulse Inno Setup Script
; Supports both clean installation and seamless in-place updates.

#ifndef MyAppName
#define MyAppName "NetPulse"
#endif

#ifndef MyAppVersion
#define MyAppVersion "1.1.0"
#endif

#ifndef MyAppPublisher
#define MyAppPublisher "0605AbMu"
#endif

#ifndef MyAppURL
#define MyAppURL "https://github.com/0605AbMu/net-pulse"
#endif

#ifndef MyAppExeName
#define MyAppExeName "NetPulse.exe"
#endif

#ifndef SourceDir
#define SourceDir "..\publish"
#endif

#ifndef OutputDir
#define OutputDir "output"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "NetPulse-Setup-v" + MyAppVersion
#endif

#ifndef SetupIcon
#define SetupIcon "..\NetPulse\Assets\app.ico"
#endif

[Setup]
; AppId identifies this application. Do not change this across updates!
AppId={{8B8A0423-B245-4DC3-8DA3-E6E45E20F751}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Update settings: Use previous installation path automatically
UsePreviousAppDir=yes
DisableDirPage=auto

; Privileges: Required admin for network diagnostics & repair operations
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

; Output settings
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#SetupIcon}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Process handling during update: safely close running app
CloseApplications=yes
CloseApplicationsFilter=*{#MyAppExeName}*
RestartApplications=no

; Uninstaller configuration
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.CreateDesktopIcon=Create a &desktop shortcut
english.CreateStartMenuIcon=Create a &Start Menu shortcut
russian.CreateDesktopIcon=Создать ярлык на &Рабочем столе
russian.CreateStartMenuIcon=Создать ярлык в меню «&Пуск»

[Tasks]
Name: "startmenuicon"; Description: "{cm:CreateStartMenuIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy all published files into the app destination directory
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch application option after install/update (skipped during silent installs)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Helper function to check if this is an upgrade/update over an existing installation
function IsUpgrade(): Boolean;
var
  UninstallKey: String;
begin
  UninstallKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1';
  Result := RegKeyExists(HKLM, UninstallKey) or RegKeyExists(HKCU, UninstallKey);
end;

// O'rnatish muvaffaqiyatli yakunlanganda Sentry ga o'rnatish metrikasini yuborish
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Params: String;
  ExePath: String;
begin
  if CurStep = ssPostInstall then
  begin
    ExePath := ExpandConstant('{app}\{#MyAppExeName}');
    if FileExists(ExePath) then
    begin
      Params := '--track-install --no-elevate';
      if IsUpgrade() then
        Params := Params + ' --is-upgrade';
      // Fon rejimida (SW_HIDE) NetPulse ni ishga tushirib metrika yuboramiz va tugashini kutamiz
      Exec(ExePath, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;

