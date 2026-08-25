@echo off
REM AxarDB Windows Update Launcher
REM Runs the PowerShell update script with execution policy bypass

setlocal enabledelayedexpansion
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%update.ps1"

if not exist "%PS_SCRIPT%" (
    echo [ERROR] Update script not found: %PS_SCRIPT%
    exit /b 1
)

echo [INFO] Starting AxarDB Windows Update Runner...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*

set EXIT_CODE=%ERRORLEVEL%
if %EXIT_CODE% NEQ 0 (
    echo [ERROR] AxarDB Update failed with exit code %EXIT_CODE%
) else (
    echo [INFO] AxarDB Update finished successfully.
)

exit /b %EXIT_CODE%
