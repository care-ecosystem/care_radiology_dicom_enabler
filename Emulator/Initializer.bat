@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

set "DICOM_INSTALL_DIR=%~dp0Modality-Emulator-3.1.5.0\"
set "DICOM_EXE_NAME=Modality-Emulator-3.1.5.0.msi"

set "DEFAULT_DICOM_MYSQL_PWD=care"

echo.
set /p DICOM_MYSQL_PWD=Enter MySQL password [Press Enter for default: %DEFAULT_DICOM_MYSQL_PWD%]: 

if "%DICOM_MYSQL_PWD%"=="" set "DICOM_MYSQL_PWD=%DEFAULT_DICOM_MYSQL_PWD%"

echo Using MySQL password: %DICOM_MYSQL_PWD%
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