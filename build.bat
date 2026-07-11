@echo off
echo Cleaning old publish output...
rmdir /s /q .\bin\publish 2>nul

echo Publishing project...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ./bin/publish

if %ERRORLEVEL% NEQ 0 (
    echo Publish failed! Aborting.
    pause
    exit /b 1
)

echo Building Installer...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" AuraInstaller.iss

echo Done! Setup file is in the Installer folder.
pause