@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

set "DICOM_INSTALL_DIR=%~dp0Modality-Emulator-3.1.5.0\"
set "DICOM_EXE_NAME=Modality-Emulator-3.1.5.0.msi"
set "DICOM_MYSQL_PWD=inzin@123"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Initializer.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Setup Complete!
) else (
    echo    Setup finished with failures - see messages above
)
echo ========================================
pause