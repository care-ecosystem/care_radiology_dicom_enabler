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
  DICOM_MYSQL_PWD          MySQL password          (default: prompted for, masked with *;
                            pressing Enter at the prompt uses 'care')
  DICOM_MYSQL_BIN          Full path to mysql.exe  (default: auto-detected)
  DICOM_SKIP_SERVICE_CHECK Set to "1" to skip the Windows-service check
                            (use this if MySQL runs outside a Windows service,
                            e.g. Docker, WSL, XAMPP)
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
$DbName      = 'plexus_mi2' # matches the database/tables baked into schema.sql - not independently configurable
$SkipService = $env:DICOM_SKIP_SERVICE_CHECK -eq '1'
$DefaultPwd  = 'care'

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

function Read-MaskedInput {
    param([string]$Prompt)

    Write-Host $Prompt -NoNewline

    if ([Console]::IsInputRedirected -or -not [Environment]::UserInteractive) {
        $line = [Console]::ReadLine()
        Write-Host ""
        return $line
    }

    $buffer = New-Object System.Text.StringBuilder

    try {
        while ($true) {
            $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

            if ($key.VirtualKeyCode -eq 13) { Write-Host ""; break }          # Enter

            if ($key.VirtualKeyCode -eq 8) {                                   # Backspace
                if ($buffer.Length -gt 0) {
                    [void]$buffer.Remove($buffer.Length - 1, 1)
                    Write-Host "`b `b" -NoNewline
                }
                continue
            }

            # Ignore keys with no printable character (arrows, function keys, ...)
            if ($key.Character -and [int]$key.Character -ge 32) {
                [void]$buffer.Append($key.Character)
                Write-Host "*" -NoNewline
            }
        }
    } catch {
        # Host does not implement ReadKey: fall back to a plain line read.
        Write-Host ""
        return [Console]::ReadLine()
    }

    return $buffer.ToString()
}

# ---------------------------------------------------------------------------
# MySQL password: taken from the environment when set, prompted for otherwise
# ---------------------------------------------------------------------------
if ($env:DICOM_MYSQL_PWD) {
    $MySqlPwd = $env:DICOM_MYSQL_PWD
} else {
    $MySqlPwd = Read-MaskedInput "Enter MySQL password [Press Enter for default: $DefaultPwd]: "
    if (-not $MySqlPwd) { $MySqlPwd = $DefaultPwd }
}

# ---------------------------------------------------------------------------
# Step 1: MySQL client available
# ---------------------------------------------------------------------------
Write-Step "1/3 Checking for the MySQL client (mysql.exe)"

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
    Write-Step "2/3 Checking MySQL service (skipped - DICOM_SKIP_SERVICE_CHECK=1)"
} else {
    Write-Step "2/3 Checking MySQL Windows service"
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
Write-Step "3/3 Checking MySQL connectivity and creating database/table"

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
