@echo off
setlocal enabledelayedexpansion
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================
echo.

:: Step 1: Get MySQL root password
echo [1/5] MySQL Configuration
set /p MYSQL_PASSWORD=Enter MySQL root password:
if "!MYSQL_PASSWORD!"=="" (
    echo ERROR: Password cannot be empty.
    exit /b 1
)
echo.

:: Step 2: Detect and start MySQL service
echo [2/5] Checking MySQL service...
set MYSQL_SERVICE=
for %%s in (MySQL80 MySQL81 MySQL90 MySQL MySQL57) do (
    sc query %%s >nul 2>&1
    if !errorlevel! equ 0 (
        set MYSQL_SERVICE=%%s
        goto :mysql_found
    )
)
:mysql_found
if "!MYSQL_SERVICE!"=="" (
    echo ERROR: No MySQL service found. Please install MySQL or ensure the service is registered.
    exit /b 1
)
echo Found MySQL service: !MYSQL_SERVICE!

sc query !MYSQL_SERVICE! | find "RUNNING" >nul 2>&1
if errorlevel 1 (
    echo Starting MySQL service...
    net start !MYSQL_SERVICE!
    if errorlevel 1 (
        echo ERROR: Failed to start MySQL service. Please check permissions and try running as Administrator.
        exit /b 1
    )
    echo MySQL service started successfully.
) else (
    echo MySQL is already running.
)
echo.

:: Step 3: Create database with proper charset/collation
echo [3/5] Setting up database...
mysql -u root -p!MYSQL_PASSWORD! -e "CREATE DATABASE IF NOT EXISTS plexus_mi2 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
if errorlevel 1 (
    echo ERROR: Failed to create database. Please check MySQL credentials and try again.
    exit /b 1
)

mysql -u root -p!MYSQL_PASSWORD! plexus_mi2 -e "CREATE TABLE IF NOT EXISTS dcm_servers (pk INT AUTO_INCREMENT PRIMARY KEY, name VARCHAR(255), aetitle VARCHAR(255), hostaddress VARCHAR(255), portnumber INT, description TEXT);"
if errorlevel 1 (
    echo ERROR: Failed to create table. Database setup failed.
    exit /b 1
)
echo Database setup complete.
echo.

:: Step 4: Locate installation directory
echo [4/5] Locating DICOM Enabler...
set INSTALL_DIR=%~dp0
if exist "!INSTALL_DIR!dicom_enabler.exe" (
    set EXE_PATH=!INSTALL_DIR!dicom_enabler.exe
) else if exist "C:\PlexusDICOM\dicom_enabler.exe" (
    set EXE_PATH=C:\PlexusDICOM\dicom_enabler.exe
) else (
    echo DICOM Enabler executable not found in current directory.
    set /p CUSTOM_PATH=Enter full path to dicom_enabler.exe:
    if exist "!CUSTOM_PATH!" (
        set EXE_PATH=!CUSTOM_PATH!
    ) else (
        echo ERROR: Executable not found at specified path: !CUSTOM_PATH!
        exit /b 1
    )
)
echo Found executable: !EXE_PATH!
echo.

:: Step 5: Launch DICOM Enabler
echo [5/5] Launching DICOM Enabler...
for %%F in ("!EXE_PATH!") do set WORK_DIR=%%~dpF
cd /d "!WORK_DIR!"
if errorlevel 1 (
    echo ERROR: Failed to change to executable directory: !WORK_DIR!
    exit /b 1
)

start "" "!EXE_PATH!"
if errorlevel 1 (
    echo ERROR: Failed to launch DICOM Enabler.
    exit /b 1
)

echo.
echo ========================================
echo    Setup Complete!
echo ========================================
echo DICOM Enabler is now running.
echo.
pause
