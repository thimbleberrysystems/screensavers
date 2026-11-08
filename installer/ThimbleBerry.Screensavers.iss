; Inno Setup script for ThimbleBerry Screensavers.
; Build with scripts\build-installer.ps1 (it publishes the apps and passes the defines below).

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.1.0.0"
#endif
#ifndef StagingDir
  #define StagingDir "..\artifacts\installer-staging"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "ThimbleBerry Screensavers"
#define Publisher "Thimble Berry"
#define WatcherExe "ThimbleBerry.IdleWatcher.exe"

[Setup]
AppId={{7D3C2A61-9B5E-4E8A-A7F1-3C6B2E9D4F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/thimbleberrysystems/screensavers
; Per-user install: no admin rights needed.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\ThimbleBerry\Screensavers
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=ThimbleBerry-Screensavers-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#WatcherExe}
LicenseFile=..\LICENSE
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "setdefault"; Description: "Make ThimbleBerry my Windows screensaver"
Name: "watcher"; Description: "Start the screensaver even while Caffeine, PowerToys Awake or similar tools keep the PC awake (runs a small tray app at sign-in)"

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Windows screensaver selection (per user).
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{app}\ThimbleBerry.scr"; Tasks: setdefault; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveActive"; ValueData: "1"; Tasks: setdefault
; Idle watcher at sign-in.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ThimbleBerryIdleWatcher"; ValueData: """{app}\{#WatcherExe}"""; Tasks: watcher; Flags: uninsdeletevalue

[Run]
; Save the idle time chosen in the wizard and apply it to Windows' timer.
Filename: "{app}\ThimbleBerry.scr"; Parameters: "--idle-minutes {code:GetIdleMinutes}"; Flags: runhidden waituntilterminated
Filename: "{app}\{#WatcherExe}"; Tasks: watcher; Flags: nowait runasoriginaluser
Filename: "{app}\ThimbleBerry.scr"; Parameters: "/c"; Description: "Choose a scene and settings now"; Flags: postinstall nowait skipifsilent unchecked

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#WatcherExe}"; Flags: runhidden; RunOnceId: "KillWatcher"
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im ThimbleBerry.scr"; Flags: runhidden; RunOnceId: "KillSaver"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  IdlePage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  IdlePage := CreateInputQueryPage(wpSelectTasks,
    'Idle time',
    'When should the screensaver start?',
    'Number of minutes with no keyboard or mouse activity before the screensaver starts (1 to 240). You can change this later in Settings.');
  IdlePage.Add('Minutes:', False);
  IdlePage.Values[0] := '10';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Minutes: Integer;
begin
  Result := True;
  if CurPageID = IdlePage.ID then
  begin
    Minutes := StrToIntDef(Trim(IdlePage.Values[0]), -1);
    if (Minutes < 1) or (Minutes > 240) then
    begin
      MsgBox('Please enter a number of minutes from 1 to 240.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function GetIdleMinutes(Param: String): String;
begin
  Result := IntToStr(StrToIntDef(Trim(IdlePage.Values[0]), 10));
end;

{ Close a running watcher or screensaver so their files can be replaced. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#WatcherExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im ThimbleBerry.scr', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

{ When uninstalling, stop Windows pointing at a screensaver that no longer exists. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Control Panel\Desktop', 'SCRNSAVE.EXE', Current) then
      if Pos('thimbleberry.scr', Lowercase(Current)) > 0 then
        RegDeleteValue(HKCU, 'Control Panel\Desktop', 'SCRNSAVE.EXE');
  end;
end;
