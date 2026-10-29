#Requires -Version 5.0
<#
DICOM Enabler / Modality Emulator - local environment initializer.

Every setting below can be overridden with an environment variable so this
script works across machines without editing it. Defaults match the
historical setup (DB name, table, install folder) but no password is
hardcoded - each machine's MySQL root password differs.

Must be run as Administrator: the backups in C:\ProgramData\CARE DICOM Enabler\Backups\ are
readable by Administrators and SYSTEM only. After connecting to MySQL:
  - plexus_mi2 exists: it is backed up in full (every table and row) to a new
    Backups\<yyyy-MM-dd_HH-mm-ss>\plexus_mi2.sql, then schema.sql / care_tables.sql are run on
    it. It is never dropped. If the backup fails nothing is changed.
  - plexus_mi2 does not exist and Backups\ has backups (made by Reset.bat or by an earlier run):
    the user can pick one to restore. The database is restored from its plexus_mi2.sql, then
    schema.sql / care_tables.sql are run, then its SCP and FailedSCP folders are copied back
    without overwriting or deleting anything already there.
  - Otherwise: a fresh setup from schema.sql / care_tables.sql.
Nothing in Backups\ is ever deleted.

Env vars (all optional):
  DICOM_MYSQL_HOST         MySQL host              (default: localhost)
  DICOM_MYSQL_PORT         MySQL port              (default: 3306)
  DICOM_MYSQL_USER         MySQL user              (default: root)
  DICOM_MYSQL_PWD          MySQL password          (default: 'care' is tried first; if it is not
                            accepted the password is prompted for, masked with *)
  DICOM_MYSQL_BIN          Full path to mysql.exe  (default: auto-detected; mysqldump.exe,
                            needed to back up an existing database, is expected in the
                            same folder or on PATH)
  DICOM_INSTALL_DIR        DICOM Enabler install folder, used for the default SCP / FailedSCP
                            folders when restoring a backup (default: the folder of the
                            installed Care MWL SCP / Store SCP / Store SCU service, else the
                            folder above this script when it contains CARE_DICOM_Enabler.exe)
  DICOM_SKIP_SERVICE_CHECK Set to "1" to skip the Windows-service check
                            (use this if MySQL runs outside a Windows service,
                            e.g. Docker, WSL, XAMPP)
  DICOM_MYSQL_NEW_PWD      Password root@localhost is changed to as the final step,
                            after all SQL files are applied
                            (default: care)
#>

$MySqlHost   = if ($env:DICOM_MYSQL_HOST) { $env:DICOM_MYSQL_HOST } else { 'localhost' }
$MySqlPort   = if ($env:DICOM_MYSQL_PORT) { $env:DICOM_MYSQL_PORT } else { '3306' }
$MySqlUser   = if ($env:DICOM_MYSQL_USER) { $env:DICOM_MYSQL_USER } else { 'root' }
$DbName      = 'plexus_mi2' # matches the database/tables baked into schema.sql - not independently configurable
$SkipService = $env:DICOM_SKIP_SERVICE_CHECK -eq '1'
$DefaultPwd  = 'care'
$MaxPasswordAttempts = 3
$NewPwd      = if ($env:DICOM_MYSQL_NEW_PWD) { $env:DICOM_MYSQL_NEW_PWD } else { 'care' }

# Same folder Reset.bat backs up to
$BackupRoot  = Join-Path $env:ProgramData 'CARE DICOM Enabler\Backups'
$BackupDir   = Join-Path $BackupRoot (Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')
# Names the app installs the services under (uctrl_ServerManager.cs)
$ServiceNames = @('Care MWL SCP Service', 'Care Store SCP Service', 'Care Store SCU Service')

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

function Write-Info {
    param([string]$Message)
    Write-Host "  [INFO] $Message"
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Format-Size {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return '{0:N2} GB' -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return '{0:N1} MB' -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return '{0:N0} KB' -f ($Bytes / 1KB) }
    return "$Bytes bytes"
}

# Runs one SQL statement; returns @{ ExitCode; Output } with tab-separated rows and no header.
function Invoke-MySql {
    param([string]$Sql)
    $allArgs = $mysqlArgs + @('-N', '-B', '-e', $Sql)
    $output = & $mysqlExe @allArgs 2>&1 | ForEach-Object { "$_" }
    return @{ ExitCode = $LASTEXITCODE; Output = @($output) }
}

# Backup folders that can be restored (they contain plexus_mi2.sql), newest first
function Get-Backups {
    if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) { return @() }
    return @(Get-ChildItem -LiteralPath $BackupRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}$' -and (Test-Path -LiteralPath (Join-Path $_.FullName "$DbName.sql") -PathType Leaf) } |
        Sort-Object Name -Descending)
}

# Lists the backups and asks which one to restore. Returns the chosen folder, or $null for a fresh setup.
function Select-Backup {
    param([object[]]$Backups)
    Write-Host "  Backups found in $BackupRoot :"
    for ($i = 0; $i -lt $Backups.Count; $i++) {
        $contents = @('database') + @('SCP', 'FailedSCP' | Where-Object { Test-Path -LiteralPath (Join-Path $Backups[$i].FullName $_) -PathType Container })
        Write-Host ("    {0}) {1}  [{2}]" -f ($i + 1), $Backups[$i].Name, ($contents -join ', ')) -NoNewline
        if ($i -eq 0) { Write-Host "  (latest)" -ForegroundColor Green } else { Write-Host "" }
    }
    Write-Host ""
    while ($true) {
        if ($Backups.Count -eq 1) {
            $answer = (Read-Host "  Restore the database and SCP / FailedSCP folders from this backup? [Y/n]").Trim()
            if ($answer -eq '' -or $answer -ieq 'y' -or $answer -ieq 'yes') { return $Backups[0] }
            if ($answer -ieq 'n' -or $answer -ieq 'no') { return $null }
        } else {
            $answer = (Read-Host "  Restore the database and SCP / FailedSCP folders from a backup? Enter a number (Enter = 1), or N for a fresh setup").Trim()
            if ($answer -eq '') { return $Backups[0] }
            if ($answer -ieq 'n' -or $answer -ieq 'no') { return $null }
            $number = 0
            if ([int]::TryParse($answer, [ref]$number) -and $number -ge 1 -and $number -le $Backups.Count) { return $Backups[$number - 1] }
        }
        Write-Host "  Please answer with one of the options shown." -ForegroundColor Yellow
    }
}

# Creates this run's backup folder. Like Reset.bat, the Backups root is set to Full Control for
# Administrators and SYSTEM only, with no inherited entries, so every backup folder inherits that.
function New-BackupDir {
    New-Item -ItemType Directory -Path $BackupRoot -Force -ErrorAction Stop | Out-Null
    $admins = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $system = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($admins)
    foreach ($sid in @($admins, $system)) {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    Set-Acl -LiteralPath $BackupRoot -AclObject $acl -ErrorAction Stop
    New-Item -ItemType Directory -Path $BackupDir -ErrorAction Stop | Out-Null
}

# Full dump of the existing database (every table and row, routines, triggers), taken before
# schema.sql / care_tables.sql run on it. Returns $true on success.
function Backup-Database {
    if (-not $mysqlDumpExe) {
        Write-Fail "mysqldump.exe was not found next to $mysqlExe or on PATH, so '$DbName' cannot be backed up. Nothing was changed." `
            "Install the MySQL client tools, or add the folder containing mysqldump.exe to PATH, then re-run Initializer.bat."
        return $false
    }
    try {
        New-BackupDir
    } catch {
        Write-Fail "Could not create the backup folder: $($_.Exception.Message). Nothing was changed." `
            "Check that $env:ProgramData is writable by Administrators, then re-run Initializer.bat."
        return $false
    }
    $dumpFile = Join-Path $BackupDir "$DbName.sql"
    # Written as .partial and renamed once complete, so a failed dump is never offered for restore.
    # --databases adds CREATE DATABASE / USE so the dump restores after the database is dropped;
    # --result-file avoids PowerShell 5.1's '>' redirect, which writes UTF-16 that mysql cannot import;
    # --set-gtid-purged=OFF leaves out SET @@GLOBAL.GTID_PURGED, which fails the restore on a server that already has GTIDs.
    $partialFile = "$dumpFile.partial"
    $dumpArgs = $mysqlArgs + @('--single-transaction', '--routines', '--triggers', '--set-gtid-purged=OFF', "--result-file=$partialFile", '--databases', $DbName)
    $dumpOutput = & $mysqlDumpExe @dumpArgs 2>&1 | ForEach-Object { "$_" }
    $dumpCode = $LASTEXITCODE
    $tail = if (Test-Path -LiteralPath $partialFile) { (Get-Content -LiteralPath $partialFile -Tail 3 -ErrorAction SilentlyContinue) -join ' ' } else { '' }
    if ($dumpCode -eq 0 -and $tail -match 'Dump completed') {
        try {
            Move-Item -LiteralPath $partialFile -Destination $dumpFile -ErrorAction Stop
        } catch {
            Write-Fail "The backup was taken but could not be renamed to $dumpFile : $($_.Exception.Message). Nothing was changed." `
                "Rename $partialFile to $DbName.sql by hand if you need it, then re-run Initializer.bat."
            return $false
        }
        Write-Ok "Saved a full backup of '$DbName' ($(Format-Size (Get-Item -LiteralPath $dumpFile).Length)) to $dumpFile"
        Write-Info "If the update goes wrong, restore it from an elevated Command Prompt: mysql -u root -p < `"$dumpFile`""
        return $true
    }
    Write-Fail "mysqldump failed (exit code $dumpCode) or the backup is incomplete. Nothing was changed." `
        "Check the message below, fix it and re-run Initializer.bat."
    $dumpOutput | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkYellow }
    return $false
}

# Restores a backup's plexus_mi2.sql. On failure the partly restored database is dropped so
# Initializer.bat can be re-run. The backup itself is only read. Returns $true on success.
function Restore-Database {
    param([System.IO.DirectoryInfo]$Backup)
    $dumpFile = Join-Path $Backup.FullName "$DbName.sql"
    # mysql reads the file itself with source, so nothing is re-encoded by PowerShell on the way.
    # Forward slashes: '\' would start a client command (e.g. \P). No quotes: source takes the rest
    # of the line as the file name, spaces included, and keeps quotes as part of the name.
    $restoreArgs = $mysqlArgs + @('-e', "source $($dumpFile.Replace('\', '/'))")
    # From MySQL 9.4 the client's --commands option, which allows client commands such as source, is
    # off by default, and source is then sent to the server as SQL (ERROR 1064). Older clients do not
    # know the option and refuse to start with it, so it is only passed when the client lists it.
    $helpText = (& $mysqlExe --help 2>$null) -join "`n"
    if ($helpText -match '--commands\b') { $restoreArgs = @('--commands') + $restoreArgs }
    $restoreOutput = @(& $mysqlExe @restoreArgs 2>&1 | ForEach-Object { "$_" })
    $restoreCode = $LASTEXITCODE
    $errors = @($restoreOutput | Where-Object { $_ -match '^ERROR' })
    if ($restoreCode -eq 0 -and $errors.Count -eq 0) {
        Write-Ok "Database '$DbName' restored from $dumpFile"
        return $true
    }
    Write-Fail "Restoring $dumpFile failed (exit code $restoreCode)." `
        "Check the message below, fix it and re-run Initializer.bat. The backup was not changed."
    @($errors + $restoreOutput) | Select-Object -Unique -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkYellow }
    $drop = Invoke-MySql "DROP DATABASE IF EXISTS $DbName;"
    if ($drop.ExitCode -eq 0) {
        Write-Info "The partly restored database '$DbName' was dropped, so Initializer.bat can be re-run."
    } else {
        Write-Fail "Could not drop the partly restored database '$DbName': $($drop.Output | Select-Object -First 1)" `
            "Run 'DROP DATABASE $DbName;' in a mysql shell before re-running Initializer.bat, or it will be treated as an existing database."
    }
    return $false
}

# Install folder, for the default SCP / FailedSCP folders: DICOM_INSTALL_DIR, then the installed
# services, then the folder above this script (bin\<Config>\scripts\..) when it holds the exe.
function Get-InstallDir {
    if ($env:DICOM_INSTALL_DIR) { return $env:DICOM_INSTALL_DIR }
    foreach ($name in $ServiceNames) {
        $svcInfo = Get-CimInstance -ClassName Win32_Service -Filter "Name='$name'" -ErrorAction SilentlyContinue
        if ($svcInfo -and $svcInfo.PathName) {
            $exePath = if ($svcInfo.PathName -match '^"([^"]+)"') { $Matches[1] } else { ($svcInfo.PathName -split ' ')[0] }
            return (Split-Path $exePath -Parent)
        }
    }
    $parent = Split-Path $PSScriptRoot -Parent
    if ($parent -and (Test-Path -LiteralPath (Join-Path $parent 'CARE_DICOM_Enabler.exe'))) { return $parent }
    return $null
}

# Copies every file from a backup folder into $Target, keeping its sub-folders. Nothing in $Target
# is overwritten or deleted: a restored file whose name is taken is saved as <name>_restored<.ext>
# (then _restored_2, _restored_3, ...).
function Restore-Folder {
    param([string]$Name, [string]$Source, [string]$Target)
    $sourceFull = [IO.Path]::GetFullPath($Source).TrimEnd('\')
    $files = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File -Force -ErrorAction SilentlyContinue)
    $copied = 0
    $renamed = 0
    $failed = New-Object System.Collections.Generic.List[string]
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($sourceFull.Length + 1)
        $dest = Join-Path $Target $relative
        $destDir = Split-Path $dest -Parent
        try {
            New-Item -ItemType Directory -Path $destDir -Force -ErrorAction Stop | Out-Null
            if (Test-Path -LiteralPath $dest) {
                $base = [IO.Path]::GetFileNameWithoutExtension($file.Name)
                $ext = [IO.Path]::GetExtension($file.Name)
                $n = 1
                do {
                    $suffix = if ($n -eq 1) { '_restored' } else { "_restored_$n" }
                    $dest = Join-Path $destDir "$base$suffix$ext"
                    $n++
                } while (Test-Path -LiteralPath $dest)
                $renamed++
            }
            # overwrite = false: throws rather than replace a file that appeared since the check above
            [IO.File]::Copy($file.FullName, $dest, $false)
            $copied++
        } catch {
            $failed.Add("$relative ($($_.Exception.Message))")
        }
    }
    if ($failed.Count -gt 0) {
        Write-Fail "$Name : $($failed.Count) of $($files.Count) file(s) could not be copied to $Target." `
            "Copy them by hand from $sourceFull - they are still in the backup."
        $failed | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkYellow }
    }
    $renamedNote = if ($renamed -gt 0) { " ($renamed saved with a _restored name because the name was already taken)" } else { '' }
    Write-Ok "$Name : copied $copied file(s) to $Target$renamedNote"
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

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    # The backups are Administrators-only: without admin rights they would be silently ignored
    Write-Host " This script must be run as Administrator." -ForegroundColor Red
    Write-Host " Right-click Initializer.bat and choose 'Run as administrator'. Nothing was changed." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
# Step 1: MySQL client available
# ---------------------------------------------------------------------------
Write-Step "1/7 Checking for the MySQL client (mysql.exe)"

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

# mysqldump.exe is only needed to back up an existing database (step 4), so it is not checked here
$mysqlDumpExe = $null
if ($mysqlExe) {
    $besideMysql = Join-Path (Split-Path $mysqlExe -Parent) 'mysqldump.exe'
    if (Test-Path $besideMysql) {
        $mysqlDumpExe = $besideMysql
    } else {
        $dumpOnPath = Get-Command mysqldump.exe -ErrorAction SilentlyContinue
        if ($dumpOnPath) { $mysqlDumpExe = $dumpOnPath.Source }
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
    Write-Step "2/7 Checking MySQL service (skipped - DICOM_SKIP_SERVICE_CHECK=1)"
} else {
    Write-Step "2/7 Checking MySQL Windows service"
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
# Step 3: MySQL connectivity, existing database and backups
# ---------------------------------------------------------------------------
Write-Step "3/7 Checking MySQL connectivity, existing database and backups"

$connected = $false
$dbExists = $false
$restoreFrom = $null
if (-not $mysqlExe) {
    Write-Fail "Skipped - mysql.exe not available (see step 1)." $null
} else {
    # MySQL password: DICOM_MYSQL_PWD, or 'care', first; asked for only when that is not accepted
    $passwordOk = $false
    $serverUnreachable = $false
    $attempt = 0
    $MySqlPwd = if ($env:DICOM_MYSQL_PWD) { $env:DICOM_MYSQL_PWD } else { $DefaultPwd }
    while ($true) {
        $mysqlArgs = @('-h', $MySqlHost, '-P', $MySqlPort, '-u', $MySqlUser)
        if ($MySqlPwd) { $mysqlArgs += "-p$MySqlPwd" }
        $ping = Invoke-MySql 'SELECT 1;'
        if ($ping.ExitCode -eq 0) { $passwordOk = $true; break }
        if (($ping.Output -join ' ') -notmatch 'ERROR 1045') {
            $serverUnreachable = $true
            break
        }
        if ($attempt -eq 0) {
            $which = if ($env:DICOM_MYSQL_PWD) { 'The password in DICOM_MYSQL_PWD' } else { "The default password '$DefaultPwd'" }
            Write-Info "$which was not accepted - please enter the MySQL password."
        } else {
            Write-Host "  Password not accepted." -ForegroundColor Yellow
        }
        if ($attempt -ge $MaxPasswordAttempts) { break }
        $attempt++
        $MySqlPwd = Read-MaskedInput "  Enter MySQL password for '$MySqlUser' (attempt $attempt of $MaxPasswordAttempts): "
    }

    if ($serverUnreachable) {
        Write-Fail "Could not connect to MySQL at ${MySqlHost}:${MySqlPort}: $($ping.Output | Where-Object { $_ -match 'ERROR' } | Select-Object -First 1)" `
            "Verify MySQL is running, then set DICOM_MYSQL_HOST / DICOM_MYSQL_PORT / DICOM_MYSQL_USER as needed."
    } elseif (-not $passwordOk) {
        Write-Fail "MySQL did not accept the password for '$MySqlUser' after $MaxPasswordAttempts attempt(s)." `
            "Find the current MySQL password for '$MySqlUser' and re-run Initializer.bat, or set DICOM_MYSQL_PWD."
    } else {
        Write-Ok "Connected to MySQL at ${MySqlHost}:${MySqlPort} as '$MySqlUser'."

        $dbCheck = Invoke-MySql "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = '$DbName';"
        if ($dbCheck.ExitCode -ne 0) {
            Write-Fail "Could not check whether database '$DbName' exists: $($dbCheck.Output | Select-Object -First 1)" `
                "Ensure user '$MySqlUser' can read information_schema, then re-run this script."
        } else {
            $connected = $true
            $dbExists = ($dbCheck.Output | Select-Object -First 1) -eq '1'
            if ($dbExists) {
                Write-Warn "Database '$DbName' already exists. It will be backed up in full and then updated - it is not dropped."
            } else {
                Write-Ok "Database '$DbName' does not exist yet."
                $backups = Get-Backups
                if ($backups.Count -eq 0) {
                    Write-Info "No backups in $BackupRoot - doing a fresh setup."
                } else {
                    $restoreFrom = Select-Backup $backups
                    if (-not $restoreFrom) { Write-Info "No backup chosen - doing a fresh setup." }
                }
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Step 4: Back up the existing database, or restore the chosen backup
# ---------------------------------------------------------------------------
# $dbReady: schema.sql / care_tables.sql may run. False when the backup or the restore failed,
# so nothing more is changed (the root password included, so a re-run uses the same one).
$dbReady = $false
$restoreOk = $false
if (-not $connected) {
    Write-Step "4/7 Backing up or restoring the database (skipped - not connected, see step 3)"
} elseif ($dbExists) {
    Write-Step "4/7 Backing up the existing database $DbName"
    $dbReady = Backup-Database
} elseif ($restoreFrom) {
    Write-Step "4/7 Restoring database $DbName from backup $($restoreFrom.Name)"
    $restoreOk = Restore-Database $restoreFrom
    $dbReady = $restoreOk
} else {
    Write-Step "4/7 Backing up or restoring the database (skipped - fresh setup)"
    $dbReady = $true
}

# ---------------------------------------------------------------------------
# Step 5: Create / update tables. Both files only add what is missing (IF NOT EXISTS,
# INSERT IGNORE), so an existing or restored database keeps its tables and rows.
# ---------------------------------------------------------------------------
if (-not $dbReady) {
    Write-Step "5/7 Creating/updating database tables (skipped - see step $(if ($connected) { 4 } else { 3 }))"
} else {
    Write-Step "5/7 Creating/updating database tables"
    $schemaPath = Join-Path $PSScriptRoot 'schema.sql'
    if (-not (Test-Path $schemaPath)) {
        Write-Fail "schema.sql was not found next to Initializer.ps1 ($schemaPath)." "Restore Emulator\schema.sql from source control."
    } else {
        Get-Content -Path $schemaPath -Raw | & $mysqlExe @mysqlArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Failed applying schema.sql (database/tables/stored procedures) to '$DbName'." "Ensure user '$MySqlUser' has CREATE/DROP privileges on '$DbName', then re-run this script."
        } else {
            Write-Ok "Database '$DbName' is ready: dcm_servers, patient, study, series, instance, userdetails tables and the push_pat_data / push_patdicom_details / updatestatus / updatestatus_ascno procedures."

            $careTablesPath = Join-Path $PSScriptRoot 'care_tables.sql'
            if (-not (Test-Path $careTablesPath)) {
                Write-Fail "care_tables.sql was not found next to Initializer.ps1 ($careTablesPath)." "Restore Emulator\care_tables.sql from source control."
            } else {
                Get-Content -Path $careTablesPath -Raw | & $mysqlExe @mysqlArgs
                if ($LASTEXITCODE -ne 0) {
                    Write-Fail "Failed applying care_tables.sql (care_service_request / care_patient / care_worklist / care_sync_upload / care_config) to '$DbName'." "Ensure user '$MySqlUser' has CREATE and REFERENCES privileges on '$DbName', then re-run this script."
                } else {
                    Write-Ok "CARE tables are ready: care_service_request, care_patient, care_worklist, care_sync_upload, care_config."
                }
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Step 6: Restore the SCP / FailedSCP folders of the chosen backup, merged into what is there
# ---------------------------------------------------------------------------
if (-not $restoreFrom) {
    Write-Step "6/7 Restoring SCP / FailedSCP folders (skipped - no backup restored)"
} elseif (-not $restoreOk) {
    Write-Step "6/7 Restoring SCP / FailedSCP folders (skipped - the database restore failed, see step 4)"
} else {
    Write-Step "6/7 Restoring SCP / FailedSCP folders from backup $($restoreFrom.Name)"
    # Target folders: as configured in the restored care_config, else the defaults under the install folder
    $configuredFolders = @{}
    $configQuery = Invoke-MySql "SELECT config_key, config_value FROM $DbName.care_config WHERE config_key IN ('scp_folder', 'failed_scp_folder');"
    if ($configQuery.ExitCode -eq 0) {
        foreach ($row in $configQuery.Output) {
            $parts = $row -split "`t", 2
            if ($parts.Count -eq 2 -and $parts[1] -and $parts[1] -ne 'NULL' -and $parts[1].Trim()) {
                $configuredFolders[$parts[0]] = $parts[1].Trim()
            }
        }
    } else {
        Write-Info "Could not read care_config ($($configQuery.Output | Select-Object -First 1)) - using the default SCP folders."
    }
    $installDir = Get-InstallDir
    foreach ($f in @(@{ Name = 'SCP'; Key = 'scp_folder' }, @{ Name = 'FailedSCP'; Key = 'failed_scp_folder' })) {
        $source = Join-Path $restoreFrom.FullName $f.Name
        if (-not (Test-Path -LiteralPath $source -PathType Container)) {
            Write-Info "$($f.Name) is not in this backup - nothing to restore."
            continue
        }
        $target = $configuredFolders[$f.Key]
        if (-not $target) { $target = $f.Name }
        if (-not [IO.Path]::IsPathRooted($target)) {
            if (-not $installDir) {
                Write-Fail "$($f.Name) was not restored: could not find the DICOM Enabler install folder for the default $($f.Name) folder." `
                    "Copy the files in $source by hand into the $($f.Name) folder under the install folder, without overwriting files already there. Next time, set DICOM_INSTALL_DIR to the folder containing CARE_DICOM_Enabler.exe before running Initializer.bat."
                continue
            }
            $target = Join-Path $installDir $target
        }
        Restore-Folder $f.Name $source ([IO.Path]::GetFullPath($target).TrimEnd('\'))
    }
    Write-Info "The backup in $($restoreFrom.FullName) was not changed."
}

# ---------------------------------------------------------------------------
# Step 7: root password, set to 'care' (DICOM_MYSQL_NEW_PWD) whatever it was. Runs last: every
# mysql call above authenticates with the password accepted in step 3, so changing it any earlier
# would lock out the remaining steps.
# ---------------------------------------------------------------------------
if (-not $dbReady) {
    Write-Step "7/7 Setting the MySQL root password (skipped - see step $(if ($connected) { 4 } else { 3 }))"
} else {
    Write-Step "7/7 Setting the MySQL root password"
    $alterSql = "ALTER USER 'root'@'localhost' IDENTIFIED BY '$NewPwd'; FLUSH PRIVILEGES;"
    & $mysqlExe @mysqlArgs -e $alterSql
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Failed to set the root@localhost password." "Run manually in a mysql shell: ALTER USER 'root'@'localhost' IDENTIFIED BY '<password>'; FLUSH PRIVILEGES;"
    } else {
        Write-Ok "root@localhost password set to '$NewPwd'. Future runs use it without asking."
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