#ifndef AppVersion
  #define AppVersion "0.9.0-preview.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\distribution"
#endif

[Setup]
AppId={{B18F9C8D-98CA-4A95-96D7-65E4E54FC9AA}
AppName=RMPP 红枫叶定位打印
AppVersion={#AppVersion}
AppPublisher=fly-ha
AppPublisherURL=https://github.com/fly-ha/rmpp
AppSupportURL=https://github.com/fly-ha/rmpp/issues
DefaultDirName={localappdata}\Programs\RMPP
DefaultGroupName=RMPP 红枫叶定位打印
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=rmpp-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\Rmpp.Desktop.exe
SetupLogging=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "{#SourcePath}\Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RMPP 红枫叶定位打印"; Filename: "{app}\Rmpp.Desktop.exe"
Name: "{autodesktop}\RMPP 红枫叶定位打印"; Filename: "{app}\Rmpp.Desktop.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.rmpp"; ValueType: string; ValueName: ""; ValueData: "RMPP.Template"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\RMPP.Template"; ValueType: string; ValueName: ""; ValueData: "RMPP 模板"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\RMPP.Template\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Rmpp.Desktop.exe,0"
Root: HKCU; Subkey: "Software\Classes\RMPP.Template\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Rmpp.Desktop.exe"" ""%1"""

[Run]
Filename: "{app}\Rmpp.Desktop.exe"; Description: "启动 RMPP"; Flags: nowait postinstall skipifsilent
