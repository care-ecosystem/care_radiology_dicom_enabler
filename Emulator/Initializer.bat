@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Initializer.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Setup Complete!
) else (
    echo    Setup finished with failures - see messages above
)
echo ========================================
pause
