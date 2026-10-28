#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif
#ifndef AppPublishDir
  #define AppPublishDir "dist\windows-x64"
#endif
#ifndef MaintenancePublishDir
  #define MaintenancePublishDir "dist\maintenance-windows-x64"
#endif

[Setup]
AppId={{6712D2EC-8CDA-4B02-8D4A-4E61C3FE26C7}
AppName=Jerry
AppVersion={#AppVersion}
AppVerName=Jerry {#AppVersion}
AppPublisher=nRudzy
AppPublisherURL=https://github.com/nRudzy/Jerry
AppSupportURL=https://github.com/nRudzy/Jerry/issues
AppUpdatesURL=https://github.com/nRudzy/Jerry/releases
DefaultDirName={localappdata}\Programs\Jerry
DefaultGroupName=Jerry
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
SourceDir=..
OutputDir=dist
OutputBaseFilename=Jerry-Setup-{#AppVersion}
SetupIconFile=assets\branding\jerry.ico
UninstallDisplayIcon={app}\Jerry.exe
UninstallDisplayName=Jerry
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes
DisableDirPage=auto
DirExistsWarning=no
AllowNetworkDrive=no
AllowUNCPath=no
AllowRootDirectory=no
UsePreviousTasks=no
CloseApplications=no
RestartApplications=no
SetupMutex=Local\Jerry.Installer
SetupLogging=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Démarrer Jerry avec Windows"; GroupDescription: "Au quotidien :"; Flags: unchecked
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Files]
; A standalone native .exe is extracted before any installed file is replaced.
Source: "{#MaintenancePublishDir}\Jerry.Maintenance.exe"; Flags: dontcopy
Source: "{#AppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Jerry"; Filename: "{app}\Jerry.exe"; WorkingDir: "{app}"; AppUserModelID: "nRudzy.Jerry"
Name: "{autodesktop}\Jerry"; Filename: "{app}\Jerry.exe"; WorkingDir: "{app}"; AppUserModelID: "nRudzy.Jerry"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\Jerry.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\Jerry.exe"; Flags: uninsdeletekey

[Code]
const
  WaitObject0 = 0;
  WaitAbandoned = $80;

var
  MaintenanceGate: THandle;
  MaintenanceGateOwned: Boolean;
  InstanceReservation: THandle;
  InstanceReservationOwned: Boolean;
  StartupTaskInitialized: Boolean;
  StartupStatusFailed: Boolean;
  ConfigurationFailed: Boolean;

function JerryCreateMutex(SecurityAttributes: LongWord; InitialOwner: BOOL; Name: String): THandle;
  external 'CreateMutexW@kernel32.dll stdcall';
function JerryWaitForSingleObject(Handle: THandle; Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function JerryReleaseMutex(Handle: THandle): BOOL;
  external 'ReleaseMutex@kernel32.dll stdcall';
function JerryCloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

procedure ReleaseMaintenanceGate;
begin
  if MaintenanceGateOwned then begin
    JerryReleaseMutex(MaintenanceGate);
    MaintenanceGateOwned := False;
  end;
  if MaintenanceGate <> 0 then begin
    JerryCloseHandle(MaintenanceGate);
    MaintenanceGate := 0;
  end;
end;

function AcquireMaintenanceGate: Boolean;
var
  WaitResult: LongWord;
begin
  Result := MaintenanceGateOwned;
  if Result then Exit;
  MaintenanceGate := JerryCreateMutex(0, False, 'Local\Jerry.CursorStudio.Maintenance');
  if MaintenanceGate = 0 then Exit;
  WaitResult := JerryWaitForSingleObject(MaintenanceGate, 0);
  MaintenanceGateOwned := (WaitResult = WaitObject0) or (WaitResult = WaitAbandoned);
  Result := MaintenanceGateOwned;
  if not Result then ReleaseMaintenanceGate;
end;

procedure ReleaseInstanceReservation;
begin
  if InstanceReservationOwned then begin
    JerryReleaseMutex(InstanceReservation);
    InstanceReservationOwned := False;
  end;
  if InstanceReservation <> 0 then begin
    JerryCloseHandle(InstanceReservation);
    InstanceReservation := 0;
  end;
end;

function AcquireInstanceReservation: Boolean;
var
  WaitResult: LongWord;
begin
  Result := InstanceReservationOwned;
  if Result then Exit;
  { Older Jerry versions know this mutex but do not know the maintenance gate. }
  InstanceReservation := JerryCreateMutex(0, False, 'Local\Jerry.CursorStudio');
  if InstanceReservation = 0 then Exit;
  WaitResult := JerryWaitForSingleObject(InstanceReservation, 0);
  InstanceReservationOwned := (WaitResult = WaitObject0) or (WaitResult = WaitAbandoned);
  Result := InstanceReservationOwned;
  if not Result then ReleaseInstanceReservation;
end;

function ExplicitStartupSelection: Boolean;
var
  I: Integer;
  Arg, MergedTasks: String;
begin
  Result := False;
  for I := 1 to ParamCount do begin
    Arg := Uppercase(ParamStr(I));
    if Pos('/TASKS=', Arg) = 1 then begin
      Result := True;
      Exit;
    end;
    if Pos('/MERGETASKS=', Arg) = 1 then begin
      MergedTasks := Copy(Arg, Length('/MERGETASKS=') + 1, Length(Arg));
      StringChangeEx(MergedTasks, ' ', '', True);
      StringChangeEx(MergedTasks, '"', '', True);
      MergedTasks := ',' + MergedTasks + ',';
      if (Pos(',STARTUP,', MergedTasks) > 0) or
         (Pos(',!STARTUP,', MergedTasks) > 0) then begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  ExitCode: Integer;
begin
  { Inno populates TasksList on entry, including silent installations. }
  if (CurPageID <> wpSelectTasks) or StartupTaskInitialized then Exit;
  StartupTaskInitialized := True;
  if ExplicitStartupSelection then Exit;
  ExtractTemporaryFile('Jerry.Maintenance.exe');
  if Exec(ExpandConstant('{tmp}\Jerry.Maintenance.exe'), '--startup-status', '',
          SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    if ExitCode = 20 then WizardSelectTasks('startup')
    else if ExitCode = 0 then WizardSelectTasks('!startup')
    else StartupStatusFailed := True;
  end else StartupStatusFailed := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if StartupStatusFailed then begin
    Result := 'Impossible de lire le choix de démarrage de Jerry. Vérifiez vos préférences puis relancez l''installation.';
    Exit;
  end;
  if not AcquireMaintenanceGate then begin
    Result := 'Une maintenance de Jerry est déjà en cours. Réessayez après sa fermeture.';
    Exit;
  end;
  try
    ExtractTemporaryFile('Jerry.Maintenance.exe');
    if not Exec(ExpandConstant('{tmp}\Jerry.Maintenance.exe'), '--prepare-update', '',
                SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Impossible de préparer Jerry. Fermez Jerry depuis son icône puis réessayez.'
    else if ExitCode <> 0 then
      Result := 'Jerry utilise encore une capture, un éditeur ou une opération en cours, ou cette version demande une fermeture manuelle. Terminez votre travail et quittez Jerry puis réessayez.'
    else if not AcquireInstanceReservation then
      Result := 'Jerry a été relancé pendant la préparation. Quittez Jerry puis réessayez.';
  except
    Result := 'Impossible de préparer Jerry. Fermez Jerry puis relancez l''installation.';
  end;
  if Result <> '' then begin
    ReleaseInstanceReservation;
    ReleaseMaintenanceGate;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Mode: String;
  ExitCode: Integer;
begin
  if CurStep <> ssPostInstall then Exit;
  { The full payload is now copied. The helper claims this mutex itself while
    saving startup, so release the reservation before invoking it. The newer
    app remains blocked by MaintenanceGate until Setup terminates. }
  ReleaseInstanceReservation;
  Mode := 'disable';
  if WizardIsTaskSelected('startup') then Mode := 'enable';
  ConfigurationFailed := not Exec(ExpandConstant('{app}\Jerry.Maintenance.exe'),
    '--configure-startup=' + Mode, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not ConfigurationFailed then ConfigurationFailed := ExitCode <> 0;
  if ConfigurationFailed then
    SuppressibleMsgBox('Jerry a été copié, mais son choix de démarrage n''a pas pu être enregistré. Relancez l''installation.', mbError, MB_OK, IDOK);
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ConfigurationFailed then Result := 1;
end;

procedure DeinitializeSetup;
begin
  ReleaseInstanceReservation;
  ReleaseMaintenanceGate;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
  Prepared: Boolean;
begin
  { Never prepare, quit or change settings before the user's confirmation. }
  if CurUninstallStep <> usUninstall then Exit;
  Prepared := AcquireMaintenanceGate;
  if Prepared then begin
    Prepared := Exec(ExpandConstant('{app}\Jerry.Maintenance.exe'), '--prepare-update', '',
                     SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if Prepared then Prepared := ExitCode = 0;
  end;
  if Prepared then begin
    Prepared := Exec(ExpandConstant('{app}\Jerry.Maintenance.exe'), '--configure-startup=disable', '',
                     SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if Prepared then Prepared := ExitCode = 0;
  end;
  { Reserve after configuration, before deleting anything. Legacy apps do not
    honor MaintenanceGate and must also be excluded throughout removal. }
  if Prepared then Prepared := AcquireInstanceReservation;
  if not Prepared then begin
    ReleaseInstanceReservation;
    ReleaseMaintenanceGate;
    SuppressibleMsgBox('La désinstallation est arrêtée pour conserver votre travail. Terminez les opérations en cours, quittez Jerry et vérifiez que ses préférences sont accessibles.', mbError, MB_OK, IDOK);
    Abort;
  end;
end;

procedure DeinitializeUninstall;
begin
  ReleaseInstanceReservation;
  ReleaseMaintenanceGate;
end;
