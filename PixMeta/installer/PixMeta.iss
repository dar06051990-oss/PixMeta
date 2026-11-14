#define MyAppName "PixMeta"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PixMeta"
#define MyAppExeName "PixMeta.exe"

[Setup]
; Keep AppId unchanged for upgrades. Per-user install requires no admin rights.
AppId={{E6F232C1-7459-4DC5-9574-F9FDB5419E61}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName=PixMeta {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.0.0.0
DefaultDirName={localappdata}\Programs\PixMeta
DefaultGroupName=PixMeta
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\PixMeta.ico
SetupIconFile=..\assets\PixMeta.ico
OutputDir=..\dist
OutputBaseFilename=PixMeta-Setup-v1.0.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.AssociationTask=Show PixMeta in "Open with" for supported image types
russian.AssociationTask=Добавить PixMeta в меню «Открыть с помощью» для поддерживаемых форматов изображений
english.IntegrationGroup=Windows file integration
russian.IntegrationGroup=Интеграция с Windows
english.RunPixMeta=Launch PixMeta
russian.RunPixMeta=Запустить PixMeta
english.MakeDefault=Choose PixMeta as the default image viewer in Windows Settings
russian.MakeDefault=Выбрать PixMeta просмотрщиком изображений по умолчанию в параметрах Windows
english.UninstallPixMeta=Uninstall PixMeta
russian.UninstallPixMeta=Удалить PixMeta

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "associations"; Description: "{cm:AssociationTask}"; GroupDescription: "{cm:IntegrationGroup}"; Flags: checkedonce

[Files]
; dotnet publish --self-contained true --single-file creates a Windows EXE + optional sidecars.
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Approved PixMeta icon is also installed as a standalone ICO for shortcuts and associations.
Source: "..\assets\PixMeta.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\PixMeta"; Filename: "{app}\PixMeta.exe"; WorkingDir: "{app}"; IconFilename: "{app}\PixMeta.ico"
Name: "{group}\{cm:UninstallPixMeta}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PixMeta"; Filename: "{app}\PixMeta.exe"; WorkingDir: "{app}"; IconFilename: "{app}\PixMeta.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\PixMeta.Image"; ValueType: string; ValueName: ""; ValueData: "PixMeta image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PixMeta.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PixMeta.ico"
Root: HKCU; Subkey: "Software\Classes\PixMeta.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PixMeta.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "PixMeta"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PixMeta.exe"" ""%1"""
Root: HKCU; Subkey: "Software\PixMeta\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "PixMeta"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PixMeta\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "PixMeta — image viewer with AI generation metadata"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "PixMeta"; ValueData: "Software\PixMeta\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".png"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.png\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".jpg"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.jpg\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".jpeg"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".webp"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".gif"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.gif\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".bmp"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.bmp\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".tif"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.tif\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".tiff"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avif"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".avif"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.avif\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heic"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".heic"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.heic\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations
Root: HKCU; Subkey: "Software\PixMeta\Capabilities\FileAssociations"; ValueType: string; ValueName: ".apng"; ValueData: "PixMeta.Image"
Root: HKCU; Subkey: "Software\Classes\Applications\PixMeta.exe\SupportedTypes"; ValueType: string; ValueName: ".apng"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.apng\OpenWithProgids"; ValueType: string; ValueName: "PixMeta.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associations

[Run]
Filename: "{app}\PixMeta.exe"; Description: "{cm:RunPixMeta}"; Flags: nowait postinstall skipifsilent
; Windows 10/11 protect UserChoice. This opens Default Apps; it does not force a default handler.
Filename: "{sys}\explorer.exe"; Parameters: "ms-settings:defaultapps"; Description: "{cm:MakeDefault}"; Flags: nowait postinstall skipifsilent unchecked
