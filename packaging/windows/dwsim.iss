; Inno Setup script for the DWSIM open, cross-platform edition (GPL).
;
; Adapted from the Patreon edition installer. What the Patreon edition needs and this one does not:
; COM/regasm registration, the WinForms DWSIM.exe and the Azure/TCP solver servers and the Excel
; add-in, and the "become a Patron" closing message - all removed. The license is the GPL.
;
; It packages a self-contained publish folder (the .NET 10 runtime is inside it) into a per-user
; installer with Start Menu and desktop shortcuts, .dwxml/.dwxmz/.dwcsd2/.dwrsd2 associations, and
; preservation of the extenders, unitops and ppacks folders across a reinstall.
;
; Driven from CI with defines:
;   ISCC /DMyAppVersion=10.2.0 /DMyArch=x64 /DSourceDir=...\publish\win-x64 /DOutputDir=...\artifacts dwsim.iss
;
; Needs Inno Setup 6.3+ for the "x64compatible" architecture identifier.

#define AppName       "DWSIM"
#define AppPublisher  "Daniel Medeiros"
#define AppURL        "https://dwsim.org"
#define AppExeName    "DWSIM.UI.Desktop.Avalonia.exe"

#ifndef MyAppVersion
  #define MyAppVersion "10.0.0"
#endif
#ifndef MyArch
  #define MyArch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

#if MyArch == "arm64"
  #define ArchAllowed "arm64"
#else
  #define ArchAllowed "x64compatible"
#endif

[Setup]
AppId={{B6C2D7E0-1A4F-4C3D-9B6D-DWSIM00000002}
AppName={#AppName}
AppVersion={#MyAppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={localappdata}\Programs\DWSIM
DefaultGroupName=DWSIM
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
LicenseFile=..\..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=DWSIM-{#MyAppVersion}-win-{#MyArch}-setup
SetupIconFile=..\..\ui\DWSIM.UI.Desktop.Avalonia\DWSIM_ico.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright (c) {#AppPublisher}
UninstallDisplayName={#AppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
ChangesAssociations=yes
CloseApplications=yes
AllowNoIcons=yes

[Languages]
Name: "english";  MessagesFile: "compiler:Default.isl"
Name: "ptbr";     MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "french";   MessagesFile: "compiler:Languages\French.isl"
Name: "german";   MessagesFile: "compiler:Languages\German.isl"
Name: "spanish";  MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian";  MessagesFile: "compiler:Languages\Italian.isl"
Name: "russian";  MessagesFile: "compiler:Languages\Russian.isl"
Name: "polish";   MessagesFile: "compiler:Languages\Polish.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean";   MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon";    Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatefiles"; Description: "Associate .dwxml/.dwxmz/.dwcsd2/.dwrsd2 files with DWSIM"; GroupDescription: "File associations:"

[Dirs]
Name: "{app}\extenders"
Name: "{app}\unitops"
Name: "{app}\ppacks"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DWSIM";                       Filename: "{app}\{#AppExeName}"
Name: "{group}\DWSIM Website";               Filename: "{#AppURL}"
Name: "{group}\{cm:UninstallProgram,DWSIM}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DWSIM";                 Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; HKA resolves to HKCU for a per-user install. The icon comes from the executable itself.
Root: HKA; Subkey: "Software\Classes\.dwxml";                                        ValueType: string; ValueData: "DWSIM.SimulationXML";     Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXML";                          ValueType: string; ValueData: "DWSIM XML Simulation File"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXML\DefaultIcon";              ValueType: string; ValueData: "{app}\{#AppExeName},0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXML\shell\open\command";       ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.dwxmz";                                        ValueType: string; ValueData: "DWSIM.SimulationXMLZIP";  Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXMLZIP";                       ValueType: string; ValueData: "DWSIM Compressed XML Simulation File"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXMLZIP\DefaultIcon";           ValueType: string; ValueData: "{app}\{#AppExeName},0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.SimulationXMLZIP\shell\open\command";    ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.dwcsd2";                                       ValueType: string; ValueData: "DWSIM.CompoundCreatorCase"; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.CompoundCreatorCase";                    ValueType: string; ValueData: "DWSIM Compound Creator Case"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.CompoundCreatorCase\DefaultIcon";        ValueType: string; ValueData: "{app}\{#AppExeName},0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.CompoundCreatorCase\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.dwrsd2";                                       ValueType: string; ValueData: "DWSIM.DataRegressionCase"; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.DataRegressionCase";                     ValueType: string; ValueData: "DWSIM Data Regression Case"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.DataRegressionCase\DefaultIcon";         ValueType: string; ValueData: "{app}\{#AppExeName},0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\DWSIM.DataRegressionCase\shell\open\command";  ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associatefiles

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,DWSIM}"; Flags: nowait postinstall skipifsilent

[Messages]
english.FinishedLabel=DWSIM has been installed on your computer.%n%nDWSIM is free and open source under the GNU General Public License. If you use it, a citation is appreciated: a link to https://dwsim.org and, if you like, an email to daniel@dwsim.org about your use case.%n%nThank you for using DWSIM!
english.FinishedLabelNoIcons=DWSIM has been installed on your computer.%n%nDWSIM is free and open source under the GNU General Public License. If you use it, a citation is appreciated: a link to https://dwsim.org and, if you like, an email to daniel@dwsim.org about your use case.%n%nThank you for using DWSIM!
ptbr.FinishedLabel=DWSIM foi instalado em seu computador.%n%nO DWSIM e livre e de codigo aberto sob a Licenca Publica Geral GNU. Se voce o utilizar, uma citacao e bem-vinda: um link para https://dwsim.org e, se quiser, um email para daniel@dwsim.org sobre o seu uso.%n%nObrigado por usar o DWSIM!
ptbr.FinishedLabelNoIcons=DWSIM foi instalado em seu computador.%n%nO DWSIM e livre e de codigo aberto sob a Licenca Publica Geral GNU. Se voce o utilizar, uma citacao e bem-vinda: um link para https://dwsim.org e, se quiser, um email para daniel@dwsim.org sobre o seu uso.%n%nObrigado por usar o DWSIM!

[Code]
//---------------------------------------------------------------------------
// Preserve the user's extenders, unitops and ppacks folders across a reinstall
//---------------------------------------------------------------------------

procedure MovePluginsToBackup(BackupRoot: string);
var
  Subs: array of string;
  i: Integer;
begin
  SetArrayLength(Subs, 3);
  Subs[0] := 'extenders';
  Subs[1] := 'unitops';
  Subs[2] := 'ppacks';
  ForceDirectories(BackupRoot);
  for i := 0 to High(Subs) do
    if DirExists(ExpandConstant('{app}\' + Subs[i])) then
      RenameFile(ExpandConstant('{app}\' + Subs[i]), BackupRoot + '\' + Subs[i]);
end;

procedure RestorePluginsFromBackup(BackupRoot: string);
var
  Subs: array of string;
  i: Integer;
begin
  SetArrayLength(Subs, 3);
  Subs[0] := 'extenders';
  Subs[1] := 'unitops';
  Subs[2] := 'ppacks';
  for i := 0 to High(Subs) do
    if DirExists(BackupRoot + '\' + Subs[i]) then
    begin
      ForceDirectories(ExpandConstant('{app}'));
      RenameFile(BackupRoot + '\' + Subs[i], ExpandConstant('{app}\' + Subs[i]));
    end;
  DelTree(BackupRoot, True, True, True);
end;

function ShouldAskPreserve: Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\extenders')) or
            DirExists(ExpandConstant('{app}\unitops'))   or
            DirExists(ExpandConstant('{app}\ppacks'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  BackupRoot: string;
  Preserve: Boolean;
begin
  Result := '';
  if ShouldAskPreserve then
  begin
    Preserve := MsgBox('An existing DWSIM installation was found in:' + #13#10 +
                       ExpandConstant('{app}') + #13#10 + #13#10 +
                       'Keep user-added DLLs in the extenders, unitops and ppacks folders?' + #13#10 + #13#10 +
                       'Yes = preserve user plugins (recommended)' + #13#10 +
                       'No  = wipe everything for a clean install',
                       mbConfirmation, MB_YESNO) = IDYES;

    BackupRoot := ExpandConstant('{app}\..\DWSIM_install_backup');
    DelTree(BackupRoot, True, True, True);
    if Preserve then
      MovePluginsToBackup(BackupRoot);

    DelTree(ExpandConstant('{app}'), True, True, True);
    ForceDirectories(ExpandConstant('{app}'));

    if Preserve then
      RestorePluginsFromBackup(BackupRoot)
    else
      DelTree(BackupRoot, True, True, True);
  end;
end;

//---------------------------------------------------------------------------
// Uninstall: offer to keep the plugin folders
//---------------------------------------------------------------------------

function InitializeUninstall: Boolean;
var
  BackupRoot: string;
begin
  Result := True;
  if DirExists(ExpandConstant('{app}\extenders')) or
     DirExists(ExpandConstant('{app}\unitops'))   or
     DirExists(ExpandConstant('{app}\ppacks')) then
  begin
    if MsgBox('Keep user-added DLLs in the extenders, unitops and ppacks folders?' + #13#10 + #13#10 +
              'Yes = preserve those folders' + #13#10 +
              'No  = remove everything in ' + ExpandConstant('{app}'),
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      BackupRoot := ExpandConstant('{app}\..\DWSIM_uninstall_backup');
      DelTree(BackupRoot, True, True, True);
      MovePluginsToBackup(BackupRoot);
      RegWriteStringValue(HKCU, 'Software\DWSIM\Uninstall', 'PluginBackup', BackupRoot);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  BackupRoot: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\DWSIM\Uninstall', 'PluginBackup', BackupRoot) then
    begin
      ForceDirectories(ExpandConstant('{app}'));
      RestorePluginsFromBackup(BackupRoot);
      RegDeleteValue(HKCU, 'Software\DWSIM\Uninstall', 'PluginBackup');
      RegDeleteKeyIfEmpty(HKCU, 'Software\DWSIM\Uninstall');
      RegDeleteKeyIfEmpty(HKCU, 'Software\DWSIM');
    end;
  end;
end;
