@echo off
echo Publishing project...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./bin/publish

echo Building Installer...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" AuraInstaller.iss

echo Done! Setup file is in the Installer folder.
pause