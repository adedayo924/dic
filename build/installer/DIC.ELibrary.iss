; ---------------------------------------------------------------------------
; DIC E-Library - per-machine installer
;
; Installed layout:
;   {app}                                      Application (Program Files)
;   {commonappdata}\DIC\ELibrary\Data          Shared library data (library.db + books\)
;   {commonappdata}\DIC\ELibrary\appsettings.json
;                                              Machine config pinning Library:DataDirectory
;                                              (see ELibraryComposition.BuildHost)
;
; The data directory is shared by every user on the machine and granted Modify
; rights to the local Users group so students can open books and record loans.
;
; Build:
;   ISCC.exe build\installer\DIC.ELibrary.iss
; ---------------------------------------------------------------------------

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef AppName
  #define AppName "DIC E-Library"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\src\DIC.ELibrary.App\bin\Release\net8.0-windows10.0.19041.0\publish\win-x64"
#endif
#ifndef PayloadDir
  #define PayloadDir AddBackslash(GetEnv("LOCALAPPDATA")) + "Temp\opencode\dic-payload"
#endif

[Setup]
AppId={{7B1E7A5D-3C9F-4A2E-8C6B-9D4E2F1A0B31}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DIC
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\dist
OutputBaseFilename=DIC.ELibrary-Setup-{#AppVersion}
SetupIconFile=..\..\assets\dic_logo.ico
UninstallDisplayIcon={app}\DIC.ELibrary.App.exe
UninstallDisplayName={#AppName}
Compression=none
SolidCompression=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
ChangesAssociations=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; --- application (already Authenticode-signed before packaging) ---
Source: "{#PublishDir}\DIC.ELibrary.App.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

; --- shared, pre-seeded library data ---
Source: "{#PayloadDir}\library.db"; DestDir: "{commonappdata}\DIC\ELibrary\Data"; Flags: ignoreversion
Source: "{#PayloadDir}\books\*"; DestDir: "{commonappdata}\DIC\ELibrary\Data\books"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\DIC.ELibrary.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DIC.ELibrary.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DIC.ELibrary.App.exe"; Description: "Launch {#AppName}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent unchecked

[Code]
// Pins the shared data directory so every account on this machine uses the
// seeded library instead of creating a private one under %LOCALAPPDATA%.
procedure WriteMachineConfig();
var
  ConfigDir, ConfigFile, DataDir, DataDirJson, Json: String;
  Lines: TStringList;
begin
  ConfigDir := ExpandConstant('{commonappdata}\DIC\ELibrary');
  DataDir := ExpandConstant('{commonappdata}\DIC\ELibrary\Data');
  ForceDirectories(ConfigDir);

  // An administrator may have pinned other settings already: never overwrite.
  ConfigFile := ConfigDir + '\appsettings.json';
  if not FileExists(ConfigFile) then
  begin
    // JSON strings escape backslashes.
    DataDirJson := DataDir;
    StringChangeEx(DataDirJson, '\', '\\', False);

    Json := '{' + #13#10
      + '  "Library": {' + #13#10
      + '    "DataDirectory": "' + DataDirJson + '"' + #13#10
      + '  }' + #13#10
      + '}';

    Lines := TStringList.Create;
    try
      Lines.Text := Json;
      Lines.SaveToFile(ConfigFile);
    finally
      Lines.Free;
    end;
  end;
end;

// Students need write access to the shared database and book folders so they
// can read books and record loans. Grant it to the built-in Users group by SID
// so the script works on non-English Windows too.
procedure GrantDataPermissions();
var
  Root, Args: String;
  ResultCode: Integer;
begin
  Root := ExpandConstant('{commonappdata}\DIC\ELibrary');
  Args := '"' + Root + '" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q';
  if not Exec(ExpandConstant('{sys}\icacls.exe'), Args, '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode) then
  begin
    Log('icacls could not be executed; permissions may need to be granted manually.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteMachineConfig();
    GrantDataPermissions();
  end;
end;
