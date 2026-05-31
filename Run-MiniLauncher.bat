@echo off
setlocal
set "APP_DIR=%~dp0MiniLauncher-Portable"
set "APP_EXE=%APP_DIR%\MiniLauncher.exe"

if exist "%APP_EXE%" (
  start "" "%APP_EXE%"
  exit /b 0
)

echo MiniLauncher.exe was not found.
echo Build it first from the MiniLauncher folder:
echo dotnet publish -c Release -r win-x64 --self-contained true
pause
exit /b 1
