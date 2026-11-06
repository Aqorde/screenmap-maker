#ifndef PublishDir
  #define PublishDir "..\releases\0.5.12rc\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\releases"
#endif
#ifdef VerificationBuild
  #define ProductId "aqorde.ScreenmapMaker.InstallerTest"
  #define Association ".screenmap-installtest"
  #define ProgId "aqorde.ScreenmapMaker.InstallTestDocument"
  #define DesktopName "aqorde ScreenMap Maker Installer Test"
  #define SetupName "aqorde-ScreenMapMaker-0.5.12rc-installer-test"
#else
  #define ProductId "aqorde.ScreenmapMaker"
  #define Association ".screenmap"
  #define ProgId "aqorde.ScreenmapMaker.Document"
  #define DesktopName "aqorde ScreenMap Maker"
  #define SetupName "aqorde-ScreenMapMaker-0.5.12rc-setup-win-x64"
#endif

[Setup]
AppId={#ProductId}
AppName=aqorde ScreenMap Maker
AppVersion=0.5.12rc (RC)
AppVerName=aqorde ScreenMap Maker 0.5.12rc (RC)
AppPublisher=Alexander Andreev (aqorde)
AppCopyright=Copyright (c) 2026 Alexander Andreev (aqorde)
DefaultDirName={localappdata}\Programs\aqorde\ScreenmapMaker
#ifdef VerificationBuild
DefaultGroupName=aqorde ScreenMap Maker Installer Test
#else
DefaultGroupName=aqorde ScreenMap Maker
#endif
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ChangesAssociations=yes
UninstallDisplayIcon={app}\ScreenmapMaker.exe
SetupIconFile=..\assets\icons\ScreenmapMaker.ico
LicenseFile=..\LICENSE.txt
OutputDir={#OutputDir}
OutputBaseFilename={#SetupName}
VersionInfoVersion=0.5.12.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
LanguageDetectionMethod=none
UsePreviousLanguage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
russian.Associate=Связать файлы {#Association} с aqorde ScreenMap Maker
english.Associate=Associate {#Association} files with aqorde ScreenMap Maker
russian.DesktopIcon=Создать ярлык на рабочем столе
english.DesktopIcon=Create a desktop shortcut

[Tasks]
Name: "associate"; Description: "{cm:Associate}"
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "config\*,installed-mode"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed-mode"; DestDir: "{app}"; Flags: ignoreversion; Languages: english
Source: "installed-mode.ru"; DestDir: "{app}"; DestName: "installed-mode"; Flags: ignoreversion; Languages: russian

[InstallDelete]
Type: files; Name: "{autodesktop}\{#DesktopName}.lnk"; Tasks: desktopicon; Check: IsPreviousDesktopShortcut('')
Type: files; Name: "{autodesktop}\{#DesktopName} v0.5.8b.lnk"; Tasks: desktopicon; Check: IsPreviousDesktopShortcut(' v0.5.8b')
Type: files; Name: "{autodesktop}\{#DesktopName} v0.5.9b.lnk"; Tasks: desktopicon; Check: IsPreviousDesktopShortcut(' v0.5.9b')
Type: files; Name: "{autodesktop}\{#DesktopName} v0.5.10b.lnk"; Tasks: desktopicon; Check: IsPreviousDesktopShortcut(' v0.5.10b')
Type: files; Name: "{autodesktop}\{#DesktopName} v0.5.11rc.lnk"; Tasks: desktopicon; Check: IsPreviousDesktopShortcut(' v0.5.11rc')

[Icons]
Name: "{group}\aqorde ScreenMap Maker"; Filename: "{app}\ScreenmapMaker.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#DesktopName} v0.5.12rc"; Filename: "{app}\ScreenmapMaker.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueData: "aqorde ScreenMap scene"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueData: """{app}\screenmap.ico"",0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueData: """{app}\ScreenmapMaker.exe"" ""%1"""; Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#Association}\OpenWithProgids"; ValueType: none; Flags: uninsdeletekeyifempty; Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#Association}\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate

[Run]
Filename: "{app}\ScreenmapMaker.exe"; Description: "{cm:LaunchProgram,aqorde ScreenMap Maker}"; Flags: nowait postinstall skipifsilent

[Code]
function IsPreviousDesktopShortcut(const VersionSuffix: String): Boolean;
var ShortcutPath: String;
    Shell, Shortcut: Variant;
begin
  Result := False;
  ShortcutPath := ExpandConstant('{autodesktop}\{#DesktopName}') + VersionSuffix + '.lnk';
  if not FileExists(ShortcutPath) then
    Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(ShortcutPath);
    Result := CompareText(Shortcut.TargetPath, ExpandConstant('{app}\ScreenmapMaker.exe')) = 0;
  except
    Log('Could not inspect the old desktop shortcut. It was kept.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Current: String;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('associate') then
  begin
    if (not RegQueryStringValue(HKCU, 'Software\Classes\{#Association}', '', Current)) or (Current = '') then
      RegWriteStringValue(HKCU, 'Software\Classes\{#Association}', '', '{#ProgId}');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Current: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Classes\{#Association}', '', Current) and (Current = '{#ProgId}') then
      RegDeleteValue(HKCU, 'Software\Classes\{#Association}', '');
  end;
  if CurUninstallStep = usPostUninstall then
    RegDeleteKeyIfEmpty(HKCU, 'Software\Classes\{#Association}');
end;
