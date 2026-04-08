#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "publish"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "Output"
#endif

#define AppExePath AddBackslash(PublishDir) + "TgPcAgent.App.exe"
#define AppName GetStringFileInfo(AppExePath, "ProductName")
#define AppVersion GetStringFileInfo(AppExePath, "ProductVersion")
#define AppPublisher "Somni"
#define AppExeName "TgPcAgent.App.exe"
#define AppMutex "Local\TgPcAgent.Singleton.v1"
#define UserDataDir "{localappdata}\TgPcAgent"

#ifndef OutputBaseFilename
  #define OutputBaseFilename "TgPcAgent-Setup"
#endif

[Setup]
AppId={{7F7758B3-2954-4336-B3DB-9712FC860417}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://tgpcagent-cloud.vercel.app
AppSupportURL=https://tgpcagent-cloud.vercel.app
AppUpdatesURL=https://tgpcagent-cloud.vercel.app
DefaultDirName={autopf}\TgPcAgent
DefaultGroupName=TgPcAgent
DisableProgramGroupPage=yes
AllowNoIcons=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
AppMutex={#AppMutex}
CloseApplications=force
CloseApplicationsFilter=TgPcAgent.App.exe
RestartApplications=yes
SetupLogging=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\TgPcAgent"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\TgPcAgent"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch TgPcAgent"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExeName}"; Flags: nowait skipifnotsilent

[Code]
var
  RemoveUserData: Boolean;
  UserDataPromptShown: Boolean;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM TgPcAgent.App.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  Result := True;
end;

procedure InitializeWizard();
begin
  RemoveUserData := False;
  UserDataPromptShown := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataPath: string;
  Response: Integer;
begin
  UserDataPath := ExpandConstant('{#UserDataDir}');

  if (CurUninstallStep = usUninstall) and (not UserDataPromptShown) then
  begin
    UserDataPromptShown := True;

    if DirExists(UserDataPath) then
    begin
      Response := SuppressibleMsgBox(
        'A settings and logs folder was found:'#13#10 +
        UserDataPath + #13#10#13#10 +
        'Delete it together with the application?'#13#10#13#10 +
        'Yes  - remove the app and the saved settings.'#13#10 +
        'No   - remove only the app and keep the settings.'#13#10 +
        'Cancel - abort uninstall.',
        mbConfirmation,
        MB_YESNOCANCEL,
        IDNO);

      if Response = IDCANCEL then
      begin
        Abort;
      end;

      RemoveUserData := Response = IDYES;
    end;
  end;

  if (CurUninstallStep = usPostUninstall) and RemoveUserData and DirExists(UserDataPath) then
  begin
    DelTree(UserDataPath, True, True, True);
  end;
end;
