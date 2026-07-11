#Requires -Version 5.0
<#
DICOM Enabler / Modality Emulator - local environment initializer.

Every setting below can be overridden with an environment variable so this
script works across machines without editing it. Defaults match the
historical setup (DB name, table, install folder) but no password is
hardcoded - each machine's MySQL root password differs.

Env vars (all optional):
  DICOM_MYSQL_HOST         MySQL host              (default: localhost)
  DICOM_MYSQL_PORT         MySQL port              (default: 3306)
  DICOM_MYSQL_USER         MySQL user              (default: root)
  DICOM_MYSQL_PWD          MySQL password          (default: none - blank password is tried)
  DICOM_MYSQL_BIN          Full path to mysql.exe  (default: auto-detected)
  DICOM_SKIP_SERVICE_CHECK Set to "1" to skip the Windows-service check
                            (use this if MySQL runs outside a Windows service,
                            e.g. Docker, WSL, XAMPP)
  DICOM_INSTALL_DIR        Modality Emulator folder (default: auto-detected)
  DICOM_EXE_NAME           Emulator executable      (default: dicom_enabler.exe)
  DICOM_SET_MYSQL_ROOT_PWD Set to "1" to also run the documented
                            ALTER USER 'root'@'localhost' step (see schema.sql
                            comment). Off by default - changing another
                            login's password is not something this script
                            does silently.
  DICOM_MYSQL_NEW_PWD      New root password to set when DICOM_SET_MYSQL_ROOT_PWD=1
                            (default: inzin@123, matching the documented setup)
#>

$MySqlHost   = if ($env:DICOM_MYSQL_HOST) { $env:DICOM_MYSQL_HOST } else { 'localhost' }
$MySqlPort   = if ($env:DICOM_MYSQL_PORT) { $env:DICOM_MYSQL_PORT } else { '3306' }
$MySqlUser   = if ($env:DICOM_MYSQL_USER) { $env:DICOM_MYSQL_USER } else { 'root' }
$MySqlPwd    = $env:DICOM_MYSQL_PWD
$DbName      = 'plexus_mi2' # matches the database/tables baked into schema.sql - not independently configurable
$ExeName     = if ($env:DICOM_EXE_NAME)   { $env:DICOM_EXE_NAME }   else { 'dicom_enabler.exe' }
$SkipService = $env:DICOM_SKIP_SERVICE_CHECK -eq '1'

$script:HadFailure = $false

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message, [string]$Fix)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
    if ($Fix) { Write-Host "         Fix: $Fix" -ForegroundColor Yellow }
    $script:HadFailure = $true
}

# ---------------------------------------------------------------------------
# Step 1: MySQL client available
# ---------------------------------------------------------------------------
Write-Step "1/5 Checking for the MySQL client (mysql.exe)"

$mysqlExe = $null
if ($env:DICOM_MYSQL_BIN -and (Test-Path $env:DICOM_MYSQL_BIN)) {
    $mysqlExe = $env:DICOM_MYSQL_BIN
} else {
    $onPath = Get-Command mysql.exe -ErrorAction SilentlyContinue
    if ($onPath) {
        $mysqlExe = $onPath.Source
    } else {
        $candidate = Get-ChildItem -Path "$env:ProgramFiles\MySQL","${env:ProgramFiles(x86)}\MySQL" `
            -Filter mysql.exe -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($candidate) { $mysqlExe = $candidate }
    }
}

if ($mysqlExe) {
    Write-Ok "Found mysql.exe at $mysqlExe"
} else {
    Write-Fail "mysql.exe was not found on PATH or in the default MySQL install folders." `
        "Install MySQL Server (https://dev.mysql.com/downloads/mysql/), or add its 'bin' folder to PATH, or set the DICOM_MYSQL_BIN environment variable to the full path of mysql.exe."
}

# ---------------------------------------------------------------------------
# Step 2: MySQL Windows service running
# ---------------------------------------------------------------------------
if ($SkipService) {
    Write-Step "2/5 Checking MySQL service (skipped - DICOM_SKIP_SERVICE_CHECK=1)"
} else {
    Write-Step "2/5 Checking MySQL Windows service"
    $svc = Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*mysql*' -or $_.DisplayName -like '*mysql*' } | Select-Object -First 1

    if (-not $svc) {
        Write-Fail "No Windows service with 'mysql' in its name was found." `
            "If MySQL runs as a Windows service, verify it installed correctly. If you run MySQL another way (Docker, WSL, XAMPP), set DICOM_SKIP_SERVICE_CHECK=1 to skip this check."
    } elseif ($svc.Status -eq 'Running') {
        Write-Ok "Service '$($svc.Name)' is running."
    } else {
        Write-Host "  Service '$($svc.Name)' is $($svc.Status). Attempting to start it..."
        try {
            Start-Service -Name $svc.Name
            Write-Ok "Service '$($svc.Name)' started."
        } catch {
            Write-Fail "Could not start service '$($svc.Name)': $($_.Exception.Message)" `
                "Start it manually (services.msc or 'net start $($svc.Name)') - this usually requires an elevated (Run as Administrator) prompt."
        }
    }
}

# ---------------------------------------------------------------------------
# Step 3: MySQL connectivity + schema setup
# ---------------------------------------------------------------------------
Write-Step "3/5 Checking MySQL connectivity and creating database/table"

if (-not $mysqlExe) {
    Write-Fail "Skipped - mysql.exe not available (see step 1)." $null
} else {
    $mysqlArgs = @('-h', $MySqlHost, '-P', $MySqlPort, '-u', $MySqlUser)
    if ($MySqlPwd) { $mysqlArgs += "-p$MySqlPwd" }

    $pingArgs = $mysqlArgs + @('-e', 'SELECT 1;')
    & $mysqlExe @pingArgs *> $null
    if ($LASTEXITCODE -ne 0) {
        $pwdHint = if ($MySqlPwd) { "the password in DICOM_MYSQL_PWD" } else { "a blank password (DICOM_MYSQL_PWD is not set)" }
        Write-Fail "Could not connect to MySQL at ${MySqlHost}:${MySqlPort} as user '$MySqlUser' using $pwdHint." `
            "Verify MySQL is running and the credentials are correct, then set DICOM_MYSQL_USER / DICOM_MYSQL_PWD / DICOM_MYSQL_HOST / DICOM_MYSQL_PORT as needed."
    } else {
        Write-Ok "Connected to MySQL at ${MySqlHost}:${MySqlPort} as '$MySqlUser'."

        $schemaPath = Join-Path $PSScriptRoot 'schema.sql'
        if (-not (Test-Path $schemaPath)) {
            Write-Fail "schema.sql was not found next to Initializer.ps1 ($schemaPath)." "Restore Emulator\schema.sql from source control."
        } else {
            Get-Content -Path $schemaPath -Raw | & $mysqlExe @mysqlArgs
            if ($LASTEXITCODE -ne 0) {
                Write-Fail "Failed applying schema.sql (database/tables/stored procedures) to '$DbName'." "Ensure user '$MySqlUser' has CREATE/DROP privileges on '$DbName', then re-run this script."
            } else {
                Write-Ok "Database '$DbName' is ready: dcm_servers, patient, study, series, instance, userdetails tables and the push_pat_data / push_patdicom_details / updatestatus / updatestatus_ascno procedures."
            }
        }

        if ($env:DICOM_SET_MYSQL_ROOT_PWD -eq '1') {
            $newPwd = if ($env:DICOM_MYSQL_NEW_PWD) { $env:DICOM_MYSQL_NEW_PWD } else { 'inzin@123' }
            $alterSql = "ALTER USER 'root'@'localhost' IDENTIFIED BY '$newPwd'; FLUSH PRIVILEGES;"
            & $mysqlExe @mysqlArgs -e $alterSql
            if ($LASTEXITCODE -ne 0) {
                Write-Fail "Failed to set the root@localhost password." "Run manually in a mysql shell: ALTER USER 'root'@'localhost' IDENTIFIED BY '<password>'; FLUSH PRIVILEGES;"
            } else {
                Write-Ok "root@localhost password set (DICOM_SET_MYSQL_ROOT_PWD=1). Remember to update DICOM_MYSQL_PWD for future runs."
            }
        } else {
            Write-Host "  Skipped setting root@localhost's password (opt in with DICOM_SET_MYSQL_ROOT_PWD=1 to run the documented ALTER USER step)." -ForegroundColor DarkGray
        }
    }
}

# ---------------------------------------------------------------------------
# Step 4: Locate the Modality Emulator install
# ---------------------------------------------------------------------------
Write-Step "4/5 Locating the Modality Emulator install"

function Find-EmulatorInstallDir {
    param([string]$ExeName)

    if ($env:DICOM_INSTALL_DIR -and (Test-Path (Join-Path $env:DICOM_INSTALL_DIR $ExeName))) {
        return $env:DICOM_INSTALL_DIR
    }

    $uninstallRoots = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    $entry = Get-ItemProperty -Path $uninstallRoots -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like '*Modality*Emulator*' -or $_.DisplayName -like '*PlexusDICOM*' -or $_.DisplayName -like '*DICOM Enabler*' } |
        Select-Object -First 1
    if ($entry -and $entry.InstallLocation -and (Test-Path (Join-Path $entry.InstallLocation $ExeName))) {
        return $entry.InstallLocation
    }

    $commonPaths = @(
        'C:\PlexusDICOM',
        (Join-Path $env:ProgramFiles 'PlexusDICOM'),
        (Join-Path ${env:ProgramFiles(x86)} 'PlexusDICOM')
    )
    foreach ($p in $commonPaths) {
        if ($p -and (Test-Path (Join-Path $p $ExeName))) { return $p }
    }

    $found = Get-ChildItem -Path $env:ProgramFiles, ${env:ProgramFiles(x86)} -Filter $ExeName -Recurse -Depth 3 -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($found) { return $found.DirectoryName }

    return $null
}

$installDir = Find-EmulatorInstallDir -ExeName $ExeName
if ($installDir) {
    Write-Ok "Found $ExeName in $installDir"
} else {
    Write-Fail "Could not find $ExeName anywhere (checked DICOM_INSTALL_DIR, registry, Program Files, and C:\PlexusDICOM)." `
        "Install the Modality Emulator first: unzip and run Modality-Emulator-3.1.5.0.msi from this folder. If it's installed somewhere unusual, set DICOM_INSTALL_DIR to that folder."
}

# ---------------------------------------------------------------------------
# Step 5: Launch the emulator
# ---------------------------------------------------------------------------
Write-Step "5/5 Launching the Modality Emulator"

if ($installDir) {
    try {
        Start-Process -FilePath (Join-Path $installDir $ExeName) -WorkingDirectory $installDir
        Write-Ok "Launched $ExeName from $installDir"
    } catch {
        Write-Fail "Failed to launch $ExeName from ${installDir}: $($_.Exception.Message)" $null
    }
} else {
    Write-Fail "Skipped - install location not found (see step 4)." $null
}

Write-Host ""
if ($script:HadFailure) {
    Write-Host "======================================================" -ForegroundColor Yellow
    Write-Host " Setup finished with failures - see [FAIL] lines above" -ForegroundColor Yellow
    Write-Host "======================================================" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "========================" -ForegroundColor Green
    Write-Host " Setup complete!" -ForegroundColor Green
    Write-Host "========================" -ForegroundColor Green
    exit 0
}
