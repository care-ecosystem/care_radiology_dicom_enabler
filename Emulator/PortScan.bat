@echo off
echo ========================================
echo    DICOM Enabler - Open Port Report
echo ========================================

REM Lists the open TCP ports on this machine, finds the DICOM AE title on each
REM port that runs a DICOM service, and writes a report to
REM C:\ProgramData\CARE DICOM Enabler\PortReports\PortReport_<date-time>.csv
REM On a rerun it compares with the previous report and shows the new ports.
REM Run as Administrator so every process and service name can be read.
REM Options are passed on to PortScan.ps1, e.g.  PortScan.bat -IncludeUdp
REM
REM After the report it keeps listening and shows connections and DICOM AE titles live
REM (the sender, the AE title it asked for, IPs, ports, Worklist/Storage) until you press q.
REM Run a worklist query or test send on the modality meanwhile. Needs Administrator.
REM   PortScan.bat -NoListen        scan and report only
REM   PortScan.bat -DicomOnly       while listening, hide plain connection lines
REM   PortScan.bat -SearchConfig    instead: search config files and the registry for AE title / port settings

echo.

net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    powershell -NoProfile -Command "Write-Host ' Not running as Administrator - some process names may be blank.' -ForegroundColor Yellow"
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PortScan.ps1" %*

echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo    Port report finished
) else (
    echo    Port report failed - see messages above
)
echo ========================================
pause
