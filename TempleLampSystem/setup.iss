#define MyAppName "點燈管理系統"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "鎰翔科技"
#define MyAppExeName "TempleLampSystem.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer
OutputBaseFilename=TempleLampSystem_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
; 預設語言：繁體中文
ShowLanguageDialog=no

[Languages]
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"

[Tasks]
Name: "desktopicon"; Description: "在桌面建立捷徑"; GroupDescription: "額外圖示："; Flags: unchecked

[Files]
; 主程式與必要 DLL（排除除錯符號 .pdb）
Source: "bin\publish\win-x64\TempleLampSystem.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\publish\win-x64\*.dll";               DestDir: "{app}"; Flags: ignoreversion
; 使用乾淨的設定範本（不含開發者的 Supabase 帳號）
Source: "appsettings.dist.json"; DestDir: "{app}"; DestName: "appsettings.json"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";                          Filename: "{app}\{#MyAppExeName}"
Name: "{group}\解除安裝 {#MyAppName}";                 Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";                    Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即啟動 {#MyAppName}"; Flags: nowait postinstall skipifsilent
