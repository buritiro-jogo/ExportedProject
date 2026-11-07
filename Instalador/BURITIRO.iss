; Instalador do jogo BURITIRO (Inno Setup 6). Gerado pelo GERAR_INSTALADOR.ps1, que passa /DPasta=<build> /DVersao=<data> /DSaida=<pasta> /DExe=<nome.exe>.
#ifndef Pasta
  #define Pasta "..\..\Builds\Windows"
#endif
#ifndef Versao
  #define Versao "1.0"
#endif
#ifndef Saida
  #define Saida "..\..\Instaladores"
#endif
#ifndef Exe
  #define Exe "Buritiro.exe"
#endif

[Setup]
AppId={{10E6595F-55F1-41EC-9015-453FDF43541B}
AppName=BURITIRO
AppVersion={#Versao}
AppPublisher=BURITIRO
DefaultDirName={autopf}\BURITIRO
DefaultGroupName=BURITIRO
DisableProgramGroupPage=yes
OutputDir={#Saida}
OutputBaseFilename=BURITIRO_Instalador_{#Versao}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
LZMAUseSeparateProcess=yes
#ifndef SemDisco
; build com mais de 2 GB: o instalador sai em .exe + arquivos .bin (mantenha todos na mesma pasta)
DiskSpanning=yes
DiskSliceSize=max
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
UninstallDisplayIcon={app}\{#Exe}
ShowLanguageDialog=no

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "atalho"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
Source: "{#Pasta}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "teste_exe\*,*.log,log*.txt,*.build.txt,*.assets.txt,*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*"

[Icons]
Name: "{group}\BURITIRO"; Filename: "{app}\{#Exe}"
Name: "{group}\Desinstalar BURITIRO"; Filename: "{uninstallexe}"
Name: "{autodesktop}\BURITIRO"; Filename: "{app}\{#Exe}"; Tasks: atalho

[Run]
; libera o jogo no Firewall do Windows (o online só precisa de saída; evita o aviso na primeira vez)
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""BURITIRO"" dir=out action=allow program=""{app}\{#Exe}"" enable=yes"; Flags: runhidden; Check: IsAdminInstallMode
Filename: "{app}\{#Exe}"; Description: "Jogar BURITIRO agora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""BURITIRO"""; Flags: runhidden; RunOnceId: "fw"
