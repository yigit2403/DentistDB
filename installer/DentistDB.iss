; Inno Setup script for DentistDB — builds a single DentistDB-Setup-<version>.exe.
;
; Compile with build-installer.ps1 (which publishes the app first and passes the
; defines below) or by hand:
;   ISCC.exe /DAppVersion=0.4.0 /DPublishDir="..\publish\DentistDB" DentistDB.iss
;
; The published app must be SELF-CONTAINED (win-x64) so the target PC needs no
; .NET runtime. The heavy lifting (data folders, PINs, Windows service, firewall,
; optional Tailscale) is done by Install-DentistDB.ps1 -ConfigureOnly, which is
; shipped inside the app folder.

#ifndef AppVersion
  #define AppVersion "0.4.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish\DentistDB"
#endif
#define AppName "DentistDB"
#define ServiceName "DentistDB"
#define AppPort "5000"

[Setup]
AppId={{1D9E6AFA-AAD9-49CD-A320-C2E71656E6E6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=DentistDB
DefaultDirName={commonpf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#PublishDir}\..
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; An existing install folder is expected on upgrade; PrepareToInstall cleans it itself.
DirExistsWarning=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\DentistDB.exe
SetupLogging=yes

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
tr.TailscaleTask=Telefon ve uzaktan erişim için Tailscale kur ve ayarla (tarayıcıda giriş ister)
en.TailscaleTask=Set up Tailscale for phone and remote access (opens a browser sign-in)
tr.OpenApp=DentistDB'yi tarayıcıda aç
en.OpenApp=Open DentistDB in the browser
tr.ShowInfo=Kurulum bilgilerini göster (adres ve PIN)
en.ShowInfo=Show setup details (address and PIN)
tr.Configuring=Servis, ayarlar ve güvenlik duvarı yapılandırılıyor...
en.Configuring=Configuring the service, settings and firewall...
tr.ConfiguringTs=Tailscale ayarlanıyor (tarayıcıda oturum açın)...
en.ConfiguringTs=Setting up Tailscale (sign in via the browser)...
tr.WipeDataTask=Mevcut hasta verilerini SİL ve sıfırdan başla (deneme verilerini temizlemek için; yedekler kalır)
en.WipeDataTask=DELETE existing patient data and start fresh (clears trial data; backups are kept)
tr.WipeDataConfirm=DİKKAT: %1 altındaki veritabanı, görüntüler ve anahtarlar kalıcı olarak silinecek. Yedek klasörü korunur.%n%nDevam edilsin mi?
en.WipeDataConfirm=WARNING: the database, images and keys under %1 will be permanently deleted. The backups folder is kept.%n%nContinue?
tr.PortTitle=Ağ Portu
en.PortTitle=Network Port
tr.PortSubtitle=Uygulamanın yerel ağda dinleyeceği portu seçin
en.PortSubtitle=Choose the port the application listens on
tr.PortDesc=Diğer bilgisayarlar ve telefon bu porta bağlanır (örn. http://klinik-pc:5000). Başka bir program bu portu kullanmıyorsa varsayılanı bırakın.
en.PortDesc=Other computers and the phone connect to this port (e.g. http://clinic-pc:5000). Keep the default unless another program already uses it.
tr.PortLabel=Port (1024–65535):
en.PortLabel=Port (1024–65535):
tr.PortInvalid=Port 1024 ile 65535 arasında bir sayı olmalıdır.
en.PortInvalid=The port must be a number between 1024 and 65535.

[Tasks]
Name: "tailscale"; Description: "{cm:TailscaleTask}"; Flags: unchecked
Name: "wipedata"; Description: "{cm:WipeDataTask}"; Flags: unchecked

[Files]
; The whole self-contained publish output, minus the environment files the
; configure step manages itself (so an upgrade never overwrites saved PINs).
; build-installer.ps1 copies Install-DentistDB.ps1 into the publish folder, so the
; wildcard below ships it inside {app} where the [Run] step calls it.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; \
  Excludes: "appsettings.Development.json,appsettings.Production.json"

[INI]
; A Start-menu shortcut target that opens the local app in the default browser.
Filename: "{app}\DentistDB.url"; Section: "InternetShortcut"; Key: "URL"; String: "http://localhost:{code:GetPort}"

[Icons]
Name: "{group}\DentistDB (Klinik)"; Filename: "{app}\DentistDB.url"
Name: "{group}\Kurulum Bilgileri"; Filename: "{app}\KURULUM-BILGILERI.txt"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"

[Run]
; 1) Configure without Tailscale (default), or 2) with Tailscale when the task is ticked.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Install-DentistDB.ps1"" -ConfigureOnly -InstallPath ""{app}"" -DataRoot ""{commonappdata}\DentistDB"" -Port {code:GetPort} -SkipTailscale -SummaryFile ""{app}\KURULUM-BILGILERI.txt"""; \
  StatusMsg: "{cm:Configuring}"; Flags: runhidden waituntilterminated; Tasks: not tailscale
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Install-DentistDB.ps1"" -ConfigureOnly -InstallPath ""{app}"" -DataRoot ""{commonappdata}\DentistDB"" -Port {code:GetPort} -SummaryFile ""{app}\KURULUM-BILGILERI.txt"""; \
  StatusMsg: "{cm:ConfiguringTs}"; Flags: waituntilterminated; Tasks: tailscale
; Post-install: show the details file and open the app.
Filename: "notepad.exe"; Parameters: """{app}\KURULUM-BILGILERI.txt"""; \
  Description: "{cm:ShowInfo}"; Flags: postinstall nowait skipifsilent runasoriginaluser
Filename: "http://localhost:{code:GetPort}"; \
  Description: "{cm:OpenApp}"; Flags: postinstall shellexec nowait skipifsilent runasoriginaluser

[UninstallRun]
; Stop and remove the service and firewall rules; leave the data folder untouched.
Filename: "powershell.exe"; RunOnceId: "RemoveService"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Stop-Service -Name '{#ServiceName}' -ErrorAction SilentlyContinue; sc.exe delete '{#ServiceName}' | Out-Null; Get-NetFirewallRule -DisplayName 'DentistDB*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule"""; \
  Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files; Name: "{app}\appsettings.Production.json"
Type: files; Name: "{app}\KURULUM-BILGILERI.txt"
Type: files; Name: "{app}\DentistDB.url"
Type: dirifempty; Name: "{app}"

[Code]
var
  PortPage: TInputQueryWizardPage;

// Port previously configured in {app}\appsettings.Production.json ("Url": "http://0.0.0.0:5000"),
// or the default when there is no earlier install.
function PreviousPort: String;
var
  Text, Marker: AnsiString;
  S: String;
  P, E: Integer;
begin
  Result := '{#AppPort}';
  try
    if not LoadStringFromFile(ExpandConstant('{app}\appsettings.Production.json'), Text) then
      Exit;
  except
    Exit;
  end;
  S := String(Text);
  Marker := '"Url"';
  P := Pos(String(Marker), S);
  if P = 0 then Exit;
  S := Copy(S, P, Length(S));            // from "Url" onwards
  P := Pos('http://', S);
  if P = 0 then Exit;
  S := Copy(S, P + 7, Length(S));        // host:port"...
  P := Pos(':', S);
  if P = 0 then Exit;
  S := Copy(S, P + 1, Length(S));        // port"...
  E := 1;
  while (E <= Length(S)) and (S[E] >= '0') and (S[E] <= '9') do E := E + 1;
  if E > 1 then Result := Copy(S, 1, E - 1);
end;

procedure InitializeWizard;
begin
  PortPage := CreateInputQueryPage(wpSelectTasks,
    CustomMessage('PortTitle'), CustomMessage('PortSubtitle'), CustomMessage('PortDesc'));
  PortPage.Add(CustomMessage('PortLabel'), False);
  PortPage.Values[0] := '{#AppPort}';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // The install folder is known by now, so an earlier install's port can be offered as the default.
  if (CurPageID = PortPage.ID) and (PortPage.Values[0] = '{#AppPort}') then
    PortPage.Values[0] := PreviousPort();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;
  if CurPageID = PortPage.ID then
  begin
    Port := StrToIntDef(Trim(PortPage.Values[0]), 0);
    if (Port < 1024) or (Port > 65535) then
    begin
      MsgBox(CustomMessage('PortInvalid'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// Used by [Run] and [INI] via {code:GetPort}.
function GetPort(Param: String): String;
begin
  Result := Trim(PortPage.Values[0]);
end;

// Stop a running service before files are copied, so the self-contained exe/dlls
// are not locked during an upgrade.
procedure StopServiceIfRunning;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
end;

// If the install folder already holds a previous DentistDB, empty it so files that
// no longer exist in the new version (old DLLs, removed assets) do not linger.
// The configured appsettings.Production.json (PINs, paths) is kept. Only folders
// that really contain DentistDB are touched, so a wrong install path is not wiped.
procedure CleanPreviousInstall;
var
  App, Settings, Saved: String;
begin
  App := ExpandConstant('{app}');
  if not (FileExists(App + '\DentistDB.dll') or FileExists(App + '\DentistDB.exe')) then
    Exit;

  Settings := App + '\appsettings.Production.json';
  Saved := ExpandConstant('{tmp}') + '\appsettings.Production.json';
  if FileExists(Settings) then
    CopyFile(Settings, Saved, False);

  Log('Cleaning previous install in ' + App);
  DelTree(App + '\*', False, True, True);

  if FileExists(Saved) then
  begin
    ForceDirectories(App);
    CopyFile(Saved, Settings, False);
  end;
end;

// Optional "start fresh": remove the database, images and keys under the data root.
// Backups are deliberately left alone. Asks once more before doing anything.
procedure WipeDataIfRequested;
var
  DataRoot: String;
begin
  if not WizardIsTaskSelected('wipedata') then
    Exit;
  DataRoot := ExpandConstant('{commonappdata}\DentistDB');
  if not DirExists(DataRoot) then
    Exit;
  if MsgBox(FmtMessage(CustomMessage('WipeDataConfirm'), [DataRoot]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then
    Exit;

  Log('Wiping data under ' + DataRoot);
  DelTree(DataRoot + '\data\*', False, True, True);
  DelTree(DataRoot + '\scans\*', False, True, True);
  DelTree(DataRoot + '\keys\*', False, True, True);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopServiceIfRunning();   // must come first: the service holds the database and the exe
  WipeDataIfRequested();
  CleanPreviousInstall();
  Result := '';
end;
