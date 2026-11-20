; Burhan Nexus HMS - Server Setup (central server computer)
; Build:  ISCC.exe /DPayloadDir=<folder> /DAppVersion=2.2.0 Server.iss
;   <folder>\server   = published HMS server package (BurhanNexus.HMS.Web.exe, deploy\windows\*.ps1, tools\print-agent ...)
;   <folder>\client   = BurhanNexus.HMS.Client.exe (desktop window for this computer)
;   <folder>\prereqs  = postgresql-16.x-windows-x64-binaries.zip + vc_redist.x64.exe
; Hospital data (database, keys, backups, radiology files, logs) is kept OUTSIDE the program folder
; (default C:\ProgramData\BurhanNexusHMS) and is never removed by the uninstaller.
; This version installs only on a computer WITHOUT an existing Burhan Nexus HMS (upgrades are a later, approved stage).

#ifndef AppVersion
  #define AppVersion "2.2.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\..\payload"
#endif

[Setup]
AppId={{6B0E7C61-2F4D-4F7A-9C5E-B1A2D3E4F501}
AppName=Burhan Nexus HMS Server
AppVersion={#AppVersion}
AppVerName=Burhan Nexus HMS Server {#AppVersion}
AppPublisher=Burhan Nexus
DefaultDirName={autopf}\Burhan Nexus HMS\Server
DefaultGroupName=Burhan Nexus HMS
DisableProgramGroupPage=yes
OutputDir=..\..\out
OutputBaseFilename=BurhanNexus-HMS-Server-Setup-{#AppVersion}
SetupIconFile=..\..\tools\BurhanNexus.Launcher\app.ico
UninstallDisplayIcon={app}\client\BurhanNexus.HMS.Client.exe
UninstallDisplayName=Burhan Nexus HMS Server
Compression=lzma2/normal
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
WizardStyle=modern
SetupLogging=yes
CloseApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#PayloadDir}\server\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\client\BurhanNexus.HMS.Client.exe"; DestDir: "{app}\client"; Flags: ignoreversion
Source: "server-tools\*.cmd"; DestDir: "{app}\server-tools"; Flags: ignoreversion
Source: "{#PayloadDir}\prereqs\*"; DestDir: "{tmp}\prereqs"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Burhan Nexus HMS"; Filename: "{app}\client\BurhanNexus.HMS.Client.exe"; Parameters: "--server http://localhost:{code:GetPort}"; Comment: "Open Burhan Nexus HMS"
Name: "{autodesktop}\Burhan Nexus HMS"; Filename: "{app}\client\BurhanNexus.HMS.Client.exe"; Parameters: "--server http://localhost:{code:GetPort}"; Tasks: desktopicon
Name: "{group}\Server tools\Server status"; Filename: "{app}\server-tools\HMS-Server-Status.cmd"
Name: "{group}\Server tools\Restart HMS server"; Filename: "{app}\server-tools\Restart-HMS-Server.cmd"
Name: "{group}\Server tools\Stop HMS server"; Filename: "{app}\server-tools\Stop-HMS-Server.cmd"
Name: "{group}\Server tools\Start HMS server"; Filename: "{app}\server-tools\Start-HMS-Server.cmd"
Name: "{group}\Server tools\Backups folder"; Filename: "{code:GetDataRoot}\backups"
Name: "{group}\Server tools\Installation log"; Filename: "{code:GetDataRoot}\logs\install-log.txt"
Name: "{group}\Uninstall Burhan Nexus HMS Server"; Filename: "{uninstallexe}"

[UninstallRun]
; Removes only the HMS Windows service and its firewall rule. Database, PostgreSQL and all hospital data stay.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\deploy\windows\Uninstall-Server.ps1"" -Port {code:GetPort}"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveHmsService"

[Code]
var
  DataPage: TInputDirWizardPage;
  DbPage: TInputQueryWizardPage;
  InstallOk: Boolean;

function ServiceExists(const Name: String): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query ' + Name, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if ServiceExists('BurhanNexusHMS') or DirExists('C:\BurhanNexusHMS') then
  begin
    SuppressibleMsgBox(
      'An existing Burhan Nexus HMS installation was found on this computer' + #13#10 +
      '(Windows service "BurhanNexusHMS" or folder C:\BurhanNexusHMS).' + #13#10#13#10 +
      'This Setup installs only on a computer without HMS. Upgrading an existing installation' + #13#10 +
      'is a separate step that always starts with a verified backup.' + #13#10#13#10 +
      'Nothing has been changed. Your existing installation and data are untouched.',
      mbInformation, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure InitializeWizard();
begin
  DataPage := CreateInputDirPage(wpSelectDir, 'Hospital data folder',
    'Where should the hospital data be kept?',
    'Database, backups, radiology files, keys and logs are kept here - outside the program folder - and are never deleted by uninstalling.',
    False, '');
  DataPage.Add('Data folder:');
  DataPage.Values[0] := ExpandConstant('{param:DATADIR|}');
  if DataPage.Values[0] = '' then DataPage.Values[0] := ExpandConstant('{commonappdata}\BurhanNexusHMS');

  DbPage := CreateInputQueryPage(DataPage.ID, 'Database and network',
    'PostgreSQL administrator password and server port',
    'PostgreSQL is installed automatically. Choose its administrator (postgres) password and keep it safe. ' +
    'Other hospital computers connect to this server on the port below.');
  DbPage.Add('PostgreSQL administrator password:', True);
  DbPage.Add('Server port (default 5000):', False);
  DbPage.Values[0] := ExpandConstant('{param:PGPASSWORD|BCGH}');
  DbPage.Values[1] := ExpandConstant('{param:PORT|5000}');
end;

function GetDataRoot(Param: String): String;
begin
  if DataPage <> nil then Result := RemoveBackslashUnlessRoot(DataPage.Values[0])
  else Result := ExpandConstant('{commonappdata}\BurhanNexusHMS');
end;

function GetPort(Param: String): String;
begin
  if DbPage <> nil then Result := Trim(DbPage.Values[1]) else Result := '5000';
  if Result = '' then Result := '5000';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  P: Integer;
begin
  Result := True;
  if (DbPage <> nil) and (CurPageID = DbPage.ID) then
  begin
    P := StrToIntDef(Trim(DbPage.Values[1]), 0);
    if (P < 1024) or (P > 65535) then
    begin
      SuppressibleMsgBox('Enter a port between 1024 and 65535 (default 5000).', mbError, MB_OK, IDOK);
      Result := False;
    end
    else if Length(DbPage.Values[0]) < 4 then
    begin
      SuppressibleMsgBox('The PostgreSQL password must have at least 4 characters.', mbError, MB_OK, IDOK);
      Result := False;
    end
    else if Pos('"', DbPage.Values[0]) > 0 then
    begin
      SuppressibleMsgBox('Please do not use the " character in the password.', mbError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;

procedure RunServerInstall();
var
  Code: Integer;
  Params, Log: String;
begin
  Log := GetDataRoot('') + '\logs\install-log.txt';
  ForceDirectories(GetDataRoot('') + '\logs');
  DeleteFile(GetDataRoot('') + '\logs\INSTALL-FAILED.txt');
  WizardForm.StatusLabel.Caption := 'Installing PostgreSQL, the database and the HMS service (2-10 minutes)...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}') + '\deploy\windows\Easy-Install.ps1"' +
    ' -InstallDir "' + ExpandConstant('{app}') + '"' +
    ' -DataRoot "' + GetDataRoot('') + '"' +
    ' -PgDataDir "' + GetDataRoot('') + '\pgdata"' +
    ' -PrereqDir "' + ExpandConstant('{tmp}') + '\prereqs"' +
    ' -PostgresPassword "' + DbPage.Values[0] + '"' +
    ' -Port ' + GetPort('') +
    ' -LogFile "' + Log + '" -NoBrowser';
  InstallOk := Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  WizardForm.ProgressGauge.Style := npbstNormal;
  if InstallOk then
    Exec(ExpandConstant('{app}\client\BurhanNexus.HMS.Client.exe'), '--set-server http://localhost:' + GetPort('') + ' --machine', '', SW_HIDE, ewWaitUntilTerminated, Code)
  else
  begin
    SaveStringToFile(GetDataRoot('') + '\logs\INSTALL-FAILED.txt', 'Server Setup failed (exit code ' + IntToStr(Code) + '). See install-log.txt in this folder.', False);
    SuppressibleMsgBox('The server installation did not finish (code ' + IntToStr(Code) + ').' + #13#10#13#10 +
      'Please send this file to Burhan Nexus:' + #13#10 + Log, mbError, MB_OK, IDOK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then RunServerInstall();
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine +
    'Hospital data folder:' + NewLine + Space + GetDataRoot('') + NewLine + NewLine +
    'Server address for other computers:' + NewLine + Space + 'http://<this computer''s IP>:' + GetPort('') + NewLine + NewLine +
    MemoTasksInfo;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and not InstallOk then
    WizardForm.FinishedLabel.Caption := 'Setup copied the files but the server installation did not finish. ' +
      'See ' + GetDataRoot('') + '\logs\install-log.txt and send it to Burhan Nexus.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    SuppressibleMsgBox('Burhan Nexus HMS Server was removed.' + #13#10#13#10 +
      'The database (PostgreSQL) and all hospital data were KEPT, e.g. in C:\ProgramData\BurhanNexusHMS.', mbInformation, MB_OK, IDOK);
end;
