; Frociart's per-user application payload. Built by build-light-setup.ps1.
; Inno Setup provides file/shortcut ownership and the standard uninstaller.
; Never add recursive deletion of the application directory or user data.
#ifndef AppPayload
  #error AppPayload is required
#endif
#ifndef DocumentIcon
  #error DocumentIcon is required
#endif
#ifndef VendorLicense
  #error VendorLicense is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef ApplicationHash
  #error ApplicationHash is required
#endif
#ifndef DocumentHash
  #error DocumentHash is required
#endif
#ifndef ApplicationId
  #define ApplicationId "Frociart.User"
#endif
#ifndef ProductLabel
  #define ProductLabel "Frociart"
#endif
#ifndef InstallRoot
  #define InstallRoot "{localappdata}\Programs\Frociart"
#endif
#ifndef StartLink
  #define StartLink "{userprograms}\Frociart"
#endif
#ifndef RegistryClasses
  #define RegistryClasses "Software\Classes"
#endif
#ifndef RegistryApplication
  #define RegistryApplication "Software\Frociart"
#endif
#ifndef RegistryRegistered
  #define RegistryRegistered "Software\RegisteredApplications"
#endif
#ifndef RegisteredName
  #define RegisteredName "Frociart"
#endif

[Setup]
AppId={#ApplicationId}
AppName={#ProductLabel}
AppVersion=v1
AppVerName={#ProductLabel} v1
AppPublisher=mamt104
AppPublisherURL=https://frociart.pages.dev/
VersionInfoVersion=1.82.0.0
VersionInfoCopyright=Frociart — mamt104
DefaultDirName={#InstallRoot}
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableStartupPrompt=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupArchitecture=x64
MinVersion=10.0
WizardStyle=modern dynamic
SetupIconFile={#DocumentIcon}
UninstallDisplayIcon={app}\Frociart.exe
UninstallDisplayName={#ProductLabel} v1
Uninstallable=yes
CreateUninstallRegKey=yes
ChangesAssociations=yes
CloseApplications=no
RestartApplications=no
UninstallRestartComputer=no
AlwaysRestart=no
Compression=lzma2
SolidCompression=yes
TimeStampsInUTC=yes
OutputDir={#OutputDir}
OutputBaseFilename=Frociart.ApplicationPayload

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "{#AppPayload}"; DestDir: "{app}"; DestName: "Frociart.exe"; Flags: ignoreversion notimestamp
Source: "{#DocumentIcon}"; DestDir: "{app}"; DestName: "Frociart.Document.ico"; Flags: ignoreversion notimestamp
Source: "{#VendorLicense}"; DestDir: "{app}"; DestName: "Inno-Setup-LICENSE.txt"; Flags: ignoreversion notimestamp

[Icons]
Name: "{#StartLink}"; Filename: "{app}\Frociart.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Frociart.exe"; Comment: "Crea, modifica e prova i tuoi diagrammi di flusso."; Flags: uninsneveruninstall

[Messages]
ConfirmUninstall=Vuoi rimuovere %1 dal computer?%n%nI progetti, le impostazioni e il componente Microsoft .NET verranno conservati.

[Registry]
; Association removal is guarded by ownership in CurUninstallStepChanged.
; Never touch Windows UserChoice or another application's default value.
Root: HKCU64; Subkey: "{#RegistryClasses}\Frociart.Diagram"; ValueType: string; ValueName: ""; ValueData: "Progetto Frociart"
Root: HKCU64; Subkey: "{#RegistryClasses}\Frociart.Diagram\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Frociart.Document.ico"",0"
Root: HKCU64; Subkey: "{#RegistryClasses}\Frociart.Diagram\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Frociart.exe"" --open ""%1"""
Root: HKCU64; Subkey: "{#RegistryClasses}\.frociart"; ValueType: string; ValueName: ""; ValueData: "Frociart.Diagram"; Check: CanSetDefault
Root: HKCU64; Subkey: "{#RegistryClasses}\.frociart"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-frociart"; Check: CanSetDefault
Root: HKCU64; Subkey: "{#RegistryClasses}\.frociart\OpenWithProgids"; ValueType: string; ValueName: "Frociart.Diagram"; ValueData: ""
Root: HKCU64; Subkey: "{#RegistryClasses}\Applications\Frociart.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Frociart"
Root: HKCU64; Subkey: "{#RegistryClasses}\Applications\Frociart.exe\SupportedTypes"; ValueType: string; ValueName: ".frociart"; ValueData: ""
Root: HKCU64; Subkey: "{#RegistryClasses}\Applications\Frociart.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Frociart.exe"" --open ""%1"""
Root: HKCU64; Subkey: "{#RegistryClasses}\frociart"; ValueType: string; ValueName: ""; ValueData: "URL:Frociart Project"
Root: HKCU64; Subkey: "{#RegistryClasses}\frociart"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU64; Subkey: "{#RegistryClasses}\frociart\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Frociart.Document.ico"",0"
Root: HKCU64; Subkey: "{#RegistryClasses}\frociart\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Frociart.exe"" --open-link ""%1"""
Root: HKCU64; Subkey: "{#RegistryApplication}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Frociart"
Root: HKCU64; Subkey: "{#RegistryApplication}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Crea e apri diagrammi di flusso sul computer."
Root: HKCU64; Subkey: "{#RegistryApplication}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".frociart"; ValueData: "Frociart.Diagram"
Root: HKCU64; Subkey: "{#RegistryApplication}\Capabilities\URLAssociations"; ValueType: string; ValueName: "frociart"; ValueData: "frociart"
Root: HKCU64; Subkey: "{#RegistryRegistered}"; ValueType: string; ValueName: "{#RegisteredName}"; ValueData: "{#RegistryApplication}\Capabilities"

[Code]
const
  ReparsePoint = $400;
  InvalidAttributes = $FFFFFFFF;
function GetAttributes(Name: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function OpenExclusive(Name: String; Access, Share: LongWord; Security: NativeInt;
  Creation, Attributes: LongWord; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function CloseFileHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function SameValue(Key, Name, Expected: String): Boolean;
var Value: String;
begin
  Result := RegQueryStringValue(HKCU64, Key, Name, Value) and
    (CompareText(Value, Expected) = 0);
end;

function FileCommand(Protocol: Boolean): String;
begin
  if Protocol then
    Result := '"' + ExpandConstant('{app}\Frociart.exe') + '" --open-link "%1"'
  else
    Result := '"' + ExpandConstant('{app}\Frociart.exe') + '" --open "%1"';
end;

function CanSetDefault: Boolean;
var Value: String;
begin
  Result := not RegQueryStringValue(HKCU64, '{#RegistryClasses}\.frociart', '', Value)
    or (Value = '') or (CompareText(Value, 'Frociart.Diagram') = 0);
end;

function IsReparse(Name: String): Boolean;
var Attributes: LongWord;
begin
  Attributes := GetAttributes(Name);
  Result := (Attributes <> InvalidAttributes) and ((Attributes and ReparsePoint) <> 0);
end;

function LinkFile: String;
begin
  Result := ExpandConstant('{#StartLink}') + '.lnk';
end;

function FileAvailable(Name: String): Boolean;
var Handle: THandle;
begin
  if not FileExists(Name) then begin Result := True; exit; end;
  Handle := OpenExclusive(Name, $C0000000, 0, 0, 3, $80, 0);
  Result := Handle <> THandle(-1);
  if Result then CloseFileHandle(Handle);
end;

function OwnedLink: Boolean;
var Hash: String; Shell, Link: Variant;
begin
  Result := False;
  if not FileExists(LinkFile) or IsReparse(LinkFile) then exit;
  Result := RegQueryStringValue(HKCU64, '{#RegistryApplication}\Installation', 'StartLinkSha256', Hash)
    and (CompareText(GetSHA256OfFile(LinkFile), Hash) = 0);
  if Result then exit;
  { Windows can update tracking data without changing a shortcut's target.
    Read the native shell object; no script or external process is executed. }
  try
    Shell := CreateOleObject('WScript.Shell');
    Link := Shell.CreateShortcut(LinkFile);
    Result := (CompareText(Link.TargetPath, ExpandConstant('{app}\Frociart.exe')) = 0)
      and (Link.Arguments = '')
      and (CompareText(RemoveBackslashUnlessRoot(Link.WorkingDirectory), RemoveBackslashUnlessRoot(ExpandConstant('{app}'))) = 0);
  except
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Root: String;
begin
  Root := ExpandConstant('{#InstallRoot}');
  Result := '';
  if CompareText(RemoveBackslashUnlessRoot(WizardDirValue), RemoveBackslashUnlessRoot(Root)) <> 0 then
    Result := 'La cartella di installazione non è valida. Riapri il setup di Frociart.'
  else if IsReparse(Root) or IsReparse(ExtractFileDir(Root)) or
    IsReparse(AddBackslash(Root) + 'Frociart.exe') or
    IsReparse(AddBackslash(Root) + 'Frociart.Document.ico') or
    IsReparse(AddBackslash(Root) + 'Inno-Setup-LICENSE.txt') or
    IsReparse(LinkFile) then
    Result := 'La destinazione contiene un collegamento a un’altra posizione. Nessuna modifica applicata.'
  else if FileExists(LinkFile) and not OwnedLink then
    Result := 'Il collegamento Frociart nel menu Start è stato creato o modificato altrove. Rinominalo prima di riprovare: non è stato sostituito.'
  else if not FileAvailable(AddBackslash(Root) + 'Frociart.exe') or
    not FileAvailable(AddBackslash(Root) + 'Frociart.Document.ico') or
    not FileAvailable(AddBackslash(Root) + 'Inno-Setup-LICENSE.txt') then
    Result := 'Chiudi Frociart e verifica che la cartella del programma sia accessibile, poi riprova. Nessuna modifica applicata.';
  if Result = '' then begin
    ExtractTemporaryFile('Frociart.exe');
    ExtractTemporaryFile('Frociart.Document.ico');
    if (CompareText(GetSHA256OfFile(ExpandConstant('{tmp}\Frociart.exe')), '{#ApplicationHash}') <> 0) or
      (CompareText(GetSHA256OfFile(ExpandConstant('{tmp}\Frociart.Document.ico')), '{#DocumentHash}') <> 0) then
      Result := 'Il pacchetto non corrisponde ai file previsti. Nessuna modifica applicata.';
  end;
end;

function InitializeUninstall: Boolean;
var Root: String;
begin
  Root := ExpandConstant('{#InstallRoot}');
  Result := (CompareText(RemoveBackslashUnlessRoot(ExpandConstant('{app}')), RemoveBackslashUnlessRoot(Root)) = 0)
    and not IsReparse(Root) and not IsReparse(ExtractFileDir(Root))
    and not IsReparse(AddBackslash(Root) + 'Frociart.exe')
    and not IsReparse(AddBackslash(Root) + 'Frociart.Document.ico')
    and not IsReparse(AddBackslash(Root) + 'Inno-Setup-LICENSE.txt')
    and FileAvailable(AddBackslash(Root) + 'Frociart.exe')
    and FileAvailable(AddBackslash(Root) + 'Frociart.Document.ico')
    and FileAvailable(AddBackslash(Root) + 'Inno-Setup-LICENSE.txt');
  if not Result then
    SuppressibleMsgBox('Chiudi Frociart e verifica che la cartella del programma sia accessibile, poi riprova. Nessun progetto è stato rimosso.', mbError, MB_OK, IDOK);
end;

procedure CurStepChanged(Step: TSetupStep);
begin
  if Step <> ssPostInstall then exit;
  if not FileExists(LinkFile) then
    RaiseException('Il collegamento del programma non è stato creato. Riprova il setup.');
  if not RegWriteStringValue(HKCU64, '{#RegistryApplication}\Installation', 'StartLinkSha256', GetSHA256OfFile(LinkFile))
    or not RegWriteStringValue(HKCU64, '{#RegistryApplication}\Installation', 'Engine', 'Frociart.InnoSetup.1')
    or not RegWriteStringValue(HKCU64, '{#RegistryApplication}\Installation', 'InstallLocation', ExpandConstant('{app}')) then
    RaiseException('Non riesco a confermare la configurazione del programma. Riprova il setup.');
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
var Classes, ApplicationKey, RegisteredKey: String;
begin
  if Step <> usUninstall then exit;
  Classes := '{#RegistryClasses}';
  ApplicationKey := '{#RegistryApplication}';
  RegisteredKey := '{#RegistryRegistered}';
  if OwnedLink then DeleteFile(LinkFile);
  if SameValue(ApplicationKey + '\Installation', 'Engine', 'Frociart.InnoSetup.1') and
    SameValue(ApplicationKey + '\Installation', 'InstallLocation', ExpandConstant('{app}')) then
    RegDeleteKeyIncludingSubkeys(HKCU64, ApplicationKey + '\Installation');
  if SameValue(Classes + '\Frociart.Diagram\shell\open\command', '', FileCommand(False)) then begin
    if SameValue(Classes + '\.frociart', '', 'Frociart.Diagram') then
      RegDeleteValue(HKCU64, Classes + '\.frociart', '');
    if SameValue(Classes + '\.frociart', 'Content Type', 'application/x-frociart') then
      RegDeleteValue(HKCU64, Classes + '\.frociart', 'Content Type');
    if SameValue(Classes + '\.frociart\OpenWithProgids', 'Frociart.Diagram', '') then
      RegDeleteValue(HKCU64, Classes + '\.frociart\OpenWithProgids', 'Frociart.Diagram');
    RegDeleteKeyIncludingSubkeys(HKCU64, Classes + '\Frociart.Diagram');
    if SameValue(RegisteredKey, '{#RegisteredName}', ApplicationKey + '\Capabilities') then
      RegDeleteValue(HKCU64, RegisteredKey, '{#RegisteredName}');
    RegDeleteKeyIncludingSubkeys(HKCU64, ApplicationKey + '\Capabilities');
  end;
  if SameValue(Classes + '\frociart\shell\open\command', '', FileCommand(True)) then
    RegDeleteKeyIncludingSubkeys(HKCU64, Classes + '\frociart');
  if SameValue(Classes + '\Applications\Frociart.exe\shell\open\command', '', FileCommand(False)) then
    RegDeleteKeyIncludingSubkeys(HKCU64, Classes + '\Applications\Frociart.exe');
end;
