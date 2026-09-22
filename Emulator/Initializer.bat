@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

REM The MySQL password is prompted for by Initializer.ps1, which masks it with *.
REM Set DICOM_MYSQL_PWD before running this file to skip that prompt.

echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Initializer.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Setup Complete!
) else (
    echo    Setup finished with failures - see messages above
)
echo ========================================
pause