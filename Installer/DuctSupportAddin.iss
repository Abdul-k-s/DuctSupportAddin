; DuctSupportAddin Installer Script for Inno Setup 6
; https://jrsoftware.org/isinfo.php

#define MyAppName "AUS Duct Support Add-in"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AUS"
#define MyAppURL "https://github.com/yourusername/DuctSupportAddin"
#define MyAppExeName "DuctSupportAddin.dll"

[Setup]
AppId={{B1C2D3E4-F5A6-7890-BCDE-F12345678901}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2025\DuctSupportAddin
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
InfoBeforeFile=InstallerInfo.txt
OutputDir=Output
OutputBaseFilename=DuctSupportAddin-Setup-{#MyAppVersion}
; SetupIconFile is optional - uncomment if you have an icon
; SetupIconFile=..\Resources\duct-support.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayName={#MyAppName} for Revit 2025

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main DLL and dependencies
Source: "..\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Addin manifest (goes to Addins folder root)
Source: "..\DuctSupportAddin.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion

; Revit Families
Source: "..\Families\RfaFiles\*.rfa"; DestDir: "{app}\Families\RfaFiles"; Flags: ignoreversion

; Resources
Source: "..\Resources\*.ico"; DestDir: "{app}\Resources"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\Resources\*.png"; DestDir: "{app}\Resources"; Flags: ignoreversion skipifsourcedoesntexist

; Localization
Source: "..\Localization\*.json"; DestDir: "{app}\Localization"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Nothing to run after install - Revit loads the add-in automatically

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\DuctSupportAddin.addin"

[Code]
function InitializeSetup(): Boolean;
var
  RevitPath: String;
begin
  Result := True;
  
  // Check if Revit 2025 addins folder exists
  RevitPath := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\2025');
  if not DirExists(RevitPath) then
  begin
    if MsgBox('Revit 2025 addins folder not found.' + #13#10 + #13#10 +
              'The installer will create the folder:' + #13#10 +
              RevitPath + #13#10 + #13#10 +
              'Do you want to continue?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end
    else
    begin
      ForceDirectories(RevitPath);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up config and logs if user wants
    if MsgBox('Do you want to remove configuration and log files?' + #13#10 +
              '(Located in %APPDATA%\AUS\DuctSupportAddin)', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\AUS\DuctSupportAddin'), True, True, True);
    end;
  end;
end;
