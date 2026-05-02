@echo off
REM Quick build script for DuctSupportAddin
REM For full options, use build.ps1 with PowerShell

echo ================================
echo DuctSupportAddin Build
echo ================================
echo.

cd /d "%~dp0"

echo Building Release configuration...
dotnet build DuctSupportAddin.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ================================
echo Build successful!
echo ================================
echo.
echo Output: bin\Release\
echo.
echo To create installer, run:
echo   powershell -ExecutionPolicy Bypass -File build.ps1 -Release -Installer
