; Burhan Nexus HMS - Client Setup (every other hospital computer)
; Build:  ISCC.exe /DPayloadDir=<folder> /DAppVersion=2.2.0 Client.iss
;   <folder>\client\BurhanNexus.HMS.Client.exe
;   <folder>\webview2\MicrosoftEdgeWebview2Setup.exe   (Microsoft's small WebView2 installer, used only if WebView2 is missing)
; Installs ONLY the desktop window. No database, no server, no hospital data on this computer:
; everything is on the central HMS server.

#ifndef AppVersion
  #define AppVersion "2.2.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\..\payload"
#endif

[Setup]
AppId={{9D3F2A10-7C4B-4E2A-8F61-C2B3D4E5F602}
AppName=Burhan Nexus HMS Client
AppVersion={#AppVersion}
AppVerName=Burhan Nexus HMS Client {#AppVersion}
AppPublisher=Burhan Nexus
DefaultDirName={autopf}\Burhan Nexus HMS\Client
DefaultGroupName=Burhan Nexus HMS
DisableProgramGroupPage=yes
OutputDir=..\..\out
OutputBaseFilename=BurhanNexus-HMS-Client-Setup-{#AppVersion}
SetupIconFile=..\..\tools\BurhanNexus.Launcher\app.ico
UninstallDisplayIcon={app}\BurhanNexus.HMS.Client.exe
UninstallDisplayName=Burhan Nexus HMS Client
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#PayloadDir}\client\BurhanNexus.HMS.Client.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsWebView2

[Icons]
Name: "{group}\Burhan Nexus HMS"; Filename: "{app}\BurhanNexus.HMS.Client.exe"; Comment: "Open Burhan Nexus HMS"
Name: "{autodesktop}\Burhan Nexus HMS"; Filename: "{app}\BurhanNexus.HMS.Client.exe"; Tasks: desktopicon
Name: "{group}\Uninstall Burhan Nexus HMS Client"; Filename: "{uninstallexe}"

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft Edge WebView2 (needs internet once)..."; Flags: waituntilterminated; Check: NeedsWebView2
Filename: "{app}\BurhanNexus.HMS.Client.exe"; Parameters: "--set-server ""{code:GetServer}"" --machine"; Flags: runhidden waituntilterminated
Filename: "{app}\BurhanNexus.HMS.Client.exe"; Description: "Open Burhan Nexus HMS now"; Flags: nowait postinstall skipifsilent

[Code]
var
  ServerPage: TInputQueryWizardPage;

function WebView2Version(): String;
var
  V: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0') then Result := V
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0') then Result := V;
end;

function NeedsWebView2(): Boolean;
begin
  Result := WebView2Version() = '';
end;

function ServiceExists(const Name: String): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query ' + Name, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

procedure InitializeWizard();
begin
  ServerPage := CreateInputQueryWizardPage(wpSelectDir, 'Central HMS server',
    'Which computer is the hospital''s HMS server?',
    'Enter the server computer''s IP address or name, e.g. 192.168.1.10 or SERVER-PC. ' +
    'Port 5000 is added automatically. (On the server: Start menu > Burhan Nexus HMS > Server tools > Server status shows the address.)');
  ServerPage.Add('Server address:', False);
  ServerPage.Values[0] := ExpandConstant('{param:SERVER|}');
  if (ServerPage.Values[0] = '') and ServiceExists('BurhanNexusHMS') then ServerPage.Values[0] := 'localhost';
end;

function GetServer(Param: String): String;
begin
  Result := Trim(ServerPage.Values[0]);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Code: Integer;
  Tmp: String;
begin
  Result := True;
  if CurPageID = ServerPage.ID then
  begin
    if GetServer('') = '' then
    begin
      SuppressibleMsgBox('Please enter the server address.', mbError, MB_OK, IDOK);
      Result := False;
      exit;
    end;
    { Test the address with the client program itself (extracted to a temporary folder). }
    ExtractTemporaryFile('BurhanNexus.HMS.Client.exe');
    Tmp := ExpandConstant('{tmp}\BurhanNexus.HMS.Client.exe');
    WizardForm.NextButton.Enabled := False;
    if not (Exec(Tmp, '--check-server "' + GetServer('') + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0)) then
      Result := SuppressibleMsgBox('The HMS server at "' + GetServer('') + '" did not answer.' + #13#10#13#10 +
        'Check that the server computer is on, the address is right and both computers are on the same network.' + #13#10#13#10 +
        'Continue anyway? (The address can be changed later in the program: right-click the title bar > Server settings.)',
        mbConfirmation, MB_YESNO, IDYES) = IDYES;
    WizardForm.NextButton.Enabled := True;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  WV: String;
begin
  WV := WebView2Version();
  if WV = '' then WV := 'not installed - will be installed (needs internet once)' else WV := 'installed (' + WV + ')';
  Result := MemoDirInfo + NewLine + NewLine +
    'HMS server:' + NewLine + Space + GetServer('') + NewLine + NewLine +
    'Microsoft Edge WebView2:' + NewLine + Space + WV + NewLine + NewLine +
    'Only the desktop program is installed - no database and no hospital data on this computer.' + NewLine + NewLine +
    MemoTasksInfo;
end;
