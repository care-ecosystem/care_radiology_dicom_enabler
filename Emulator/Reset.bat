@echo off
echo ========================================
echo    DICOM Enabler - Backup and Reset
echo ========================================

REM Backs up the logs, SCP and FailedSCP folders and the plexus_mi2 database to
REM C:\ProgramData\CARE DICOM Enabler\Backups\<date-time>, then clears them so
REM Initializer.bat can be run on a clean machine. Run as Administrator.
REM The MySQL password 'care' is tried first; Reset.ps1 asks for it if that fails.
REM Set DICOM_MYSQL_PWD before running this file to use a different password.

echo.

REM 'net session' only succeeds in an elevated prompt
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    powershell -NoProfile -Command "Write-Host ' This script must be run as Administrator.' -ForegroundColor Red; Write-Host ' Right-click Reset.bat and choose ''Run as administrator''.' -ForegroundColor Red; Write-Host ' Nothing was changed.' -ForegroundColor Red"
    echo ========================================
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reset.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Reset script finished
) else (
    echo    Reset finished with failures - see messages above
)
echo ========================================
pause
