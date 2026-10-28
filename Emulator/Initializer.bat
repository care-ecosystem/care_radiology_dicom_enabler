@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

REM The MySQL password 'care' is tried first; Initializer.ps1 asks for it (masked
REM with *) only if that fails, and sets it to 'care' at the end.
REM Set DICOM_MYSQL_PWD before running this file to use a different password.
REM Run as Administrator: an existing plexus_mi2 is backed up, and backups made by
REM Reset.bat can be restored, from C:\ProgramData\CARE DICOM Enabler\Backups.

echo.

REM 'net session' only succeeds in an elevated prompt
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    powershell -NoProfile -Command "Write-Host ' This script must be run as Administrator.' -ForegroundColor Red; Write-Host ' Right-click Initializer.bat and choose ''Run as administrator''.' -ForegroundColor Red; Write-Host ' Nothing was changed.' -ForegroundColor Red"
    echo ========================================
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Initializer.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Setup Complete!
) else (
    echo    Setup finished with failures - see messages above
)
echo ========================================
pause