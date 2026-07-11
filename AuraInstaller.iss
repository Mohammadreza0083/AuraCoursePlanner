; Script for AuraCourse Planner
[Setup]
AppName=AuraCourse Planner
AppVersion=1.2.0
DefaultDirName={autopf}\AuraCoursePlanner
DefaultGroupName=AuraCourse Planner
OutputDir=.\Installer
OutputBaseFilename=AuraSetup
Compression=lzma2
SolidCompression=yes

SetupIconFile=C:\Users\Mohammadreza\Desktop\AuraCoursePlanner\Assets\Icons\AuraCourse.ico 

[Files]
Source: ".\bin\publish\AuraCoursePlanner.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AuraCourse Planner"; Filename: "{app}\AuraCoursePlanner.exe"
Name: "{commondesktop}\AuraCourse Planner"; Filename: "{app}\AuraCoursePlanner.exe"; IconFilename: "{app}\AuraCoursePlanner.exe"
[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;