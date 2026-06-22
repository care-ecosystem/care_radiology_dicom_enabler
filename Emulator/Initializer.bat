@echo off
echo ========================================
echo    DICOM Enabler - Auto Setup Script
echo ========================================

:: Step 1: Check if MySQL is running
echo [1/4] Checking MySQL service...
sc query MySQL80 | find "RUNNING" >nul 2>&1
if errorlevel 1 (
    echo Starting MySQL service...
    net start MySQL80
) else (
    echo MySQL is already running.
)

:: Step 2: Create DB and Table
echo [2/4] Setting up database...
mysql -u root -pinzin@123 -e "CREATE DATABASE IF NOT EXISTS plexus_mi2;"
mysql -u root -pinzin@123 plexus_mi2 -e "CREATE TABLE IF NOT EXISTS dcm_servers (pk INT AUTO_INCREMENT PRIMARY KEY, name VARCHAR(255), aetitle VARCHAR(255), hostaddress VARCHAR(255), portnumber INT, description TEXT);"

echo [3/4] Database setup complete.

:: Step 3: Launch DICOM Enabler
echo [4/4] Launching DICOM Enabler...
cd /d "C:\PlexusDICOM\"
start "" "dicom_enabler.exe"

echo ========================================
echo    Setup Complete!
echo ========================================
pause
