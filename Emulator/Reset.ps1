#Requires -Version 5.0
<#
DICOM Enabler / Modality Emulator - backup and reset.

Returns the machine to a clean state so Initializer.bat and the Modality Emulator can be run
again from scratch. Before anything is removed it backs up, into
C:\ProgramData\CARE DICOM Enabler\Backups\<yyyy-MM-dd_HH-mm-ss>\ :
  plexus_mi2.sql   mysqldump of the plexus_mi2 database: structure of every table and the stored
                   procedures; data of care_config and dcm_servers, care_sync_upload rows not
                   SUCCESS, care_worklist rows with a scan_status other than COMPLETED or
                   CANCELLED, and the
                   care_worklist / care_service_request(_device) / care_patient rows those point to
  logs\            <install folder>\logs
  SCP\             scp_folder from care_config (blank = <install folder>\SCP)
  FailedSCP\       failed_scp_folder from care_config (blank = <install folder>\FailedSCP)
  reset-run.log    transcript of this run
then empties those folders and drops plexus_mi2. A folder is only emptied when its own backup
succeeded, and the database is only dropped when the dump succeeded. The Backups folder is
readable and deletable by Administrators and SYSTEM only.

Everything that could stop the run half-way (admin rights, MySQL client, MySQL password,
install folder, free disk space) is checked before any change is made.

Env vars (all optional):
  DICOM_MYSQL_HOST    MySQL host              (default: localhost)
  DICOM_MYSQL_PORT    MySQL port              (default: 3306)
  DICOM_MYSQL_USER    MySQL user              (default: root)
  DICOM_MYSQL_PWD     MySQL password          (default: 'care' is tried first; if it is not
                       accepted the password is prompted for, masked with *)
  DICOM_MYSQL_BIN     Full path to mysql.exe  (default: auto-detected; mysqldump.exe is
                       expected in the same folder)

The install folder is the parent of this script's folder: the build ships Reset.bat and
Reset.ps1 in <install folder>\scripts\.
#>

$MySqlHost  = if ($env:DICOM_MYSQL_HOST) { $env:DICOM_MYSQL_HOST } else { 'localhost' }
$MySqlPort  = if ($env:DICOM_MYSQL_PORT) { $env:DICOM_MYSQL_PORT } else { '3306' }
$MySqlUser  = if ($env:DICOM_MYSQL_USER) { $env:DICOM_MYSQL_USER } else { 'root' }
$DbName     = 'plexus_mi2' # matches schema.sql - not independently configurable
$DefaultPwd = 'care'
$MaxPasswordAttempts = 3

# Names the app installs the services under (uctrl_ServerManager.cs)
$ServiceNames = @('Care MWL SCP Service', 'Care Store SCP Service', 'Care Store SCU Service')
# The app's exe (AssemblyName); older builds used the Plexus_DICOM_Enabler name
$AppProcessNames = @('CARE_DICOM_Enabler', 'Plexus_DICOM_Enabler')

$BackupRoot = Join-Path $env:ProgramData 'CARE DICOM Enabler\Backups'
$RunStamp   = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$BackupDir  = Join-Path $BackupRoot $RunStamp
$TranscriptTemp = Join-Path $env:TEMP "care-reset-$RunStamp.log"

$script:HadFailure = $false
$script:Results = New-Object System.Collections.Generic.List[object]
$script:OptionFile = $null
$script:BackupDirCreated = $false

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  [INFO] $Message"
}

function Write-Skip {
    param([string]$Message)
    Write-Host "  [SKIPPED] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message, [string]$Fix)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
    if ($Fix) { Write-Host "         Fix: $Fix" -ForegroundColor Yellow }
    $script:HadFailure = $true
}

function Add-Result {
    param([string]$Step, [string]$Status, [string]$Detail)
    $script:Results.Add([pscustomobject]@{ Step = $Step; Status = $Status; Detail = $Detail })
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
# MySQL helpers. The password goes in a temporary option file rather than on the command
# line, so it never shows in the process list or in mysql's "insecure password" warning.
# ---------------------------------------------------------------------------
function Set-MySqlPassword {
    param([string]$Password)
    if (-not $script:OptionFile) {
        $script:OptionFile = Join-Path $env:TEMP ("care-reset-" + [guid]::NewGuid().ToString('N') + ".cnf")
    }
    $escaped = $Password.Replace('\', '\\').Replace('"', '\"')
    $content = "[client]`r`npassword=`"$escaped`"`r`n"
    [IO.File]::WriteAllText($script:OptionFile, $content, (New-Object System.Text.UTF8Encoding $false))
}

function Remove-MySqlPassword {
    if ($script:OptionFile -and (Test-Path $script:OptionFile)) {
        Remove-Item -Path $script:OptionFile -Force -ErrorAction SilentlyContinue
    }
}

function Get-MySqlArgs {
    # --defaults-extra-file must be the first option
    return @("--defaults-extra-file=$script:OptionFile", '-h', $MySqlHost, '-P', $MySqlPort, '-u', $MySqlUser)
}

# Runs one SQL statement; returns @{ ExitCode; Output } with tab-separated, unescaped rows and no header.
function Invoke-MySql {
    param([string]$Sql)
    # -r: batch mode otherwise escapes '\' in values, returning C:\x as C:\\x
    $allArgs = (Get-MySqlArgs) + @('-N', '-B', '-r', '-e', $Sql)
    $output = & $script:MySqlExe @allArgs 2>&1 | ForEach-Object { "$_" }
    return @{ ExitCode = $LASTEXITCODE; Output = @($output) }
}

function Get-FolderStats {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return @{ Files = 0; Bytes = 0 } }
    $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue)
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    return @{ Files = $files.Count; Bytes = [long]$(if ($bytes) { $bytes } else { 0 }) }
}

function Format-Size {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return '{0:N2} GB' -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return '{0:N1} MB' -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return '{0:N0} KB' -f ($Bytes / 1KB) }
    return "$Bytes bytes"
}

function Get-NormalizedPath {
    param([string]$Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

# A configured folder is emptied by this script, so refuse anything that would wipe more than
# DICOM Enabler data: a drive root, the install folder itself, a system folder, or a folder
# that contains the backups.
function Get-UnsafeFolderReason {
    param([string]$Path)
    $full = Get-NormalizedPath $Path
    if ($full.Length -le 2 -or $full -eq [IO.Path]::GetPathRoot($full).TrimEnd('\')) { return 'it is a drive root' }
    if ($full -ieq (Get-NormalizedPath $script:InstallDir)) { return 'it is the install folder itself' }
    $backupFull = Get-NormalizedPath $BackupRoot
    if ($backupFull -ieq $full -or $backupFull.StartsWith($full + '\', [StringComparison]::OrdinalIgnoreCase)) {
        return 'it contains the backup folder'
    }
    $systemFolders = @($env:SystemRoot, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData, $env:USERPROFILE, $env:PUBLIC) |
        Where-Object { $_ } | ForEach-Object { Get-NormalizedPath $_ }
    if ($systemFolders -icontains $full) { return 'it is a system folder' }
    return $null
}

# Copies a folder into the backup and checks every file arrived. Returns $true on success.
function Backup-Folder {
    param([string]$Name, [string]$Source)
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        Write-Skip "$Source does not exist - nothing to back up."
        Add-Result "Back up $Name" 'SKIPPED' 'folder does not exist'
        return $true
    }
    $target = Join-Path $BackupDir $Name
    $sourceStats = Get-FolderStats $Source
    # /E all sub-folders, /R:1 /W:1 do not hang on a locked file; data, attributes and timestamps
    # are copied but not permissions, so the copies inherit the admin-only backup permissions.
    $robocopyOutput = & robocopy.exe $Source $target /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NP /NFL /NDL /NJH 2>&1 | ForEach-Object { "$_" }
    $robocopyCode = $LASTEXITCODE
    $targetStats = Get-FolderStats $target
    # robocopy exit codes 0-7 are success; 8 and above mean at least one file failed
    if ($robocopyCode -ge 8 -or $targetStats.Files -lt $sourceStats.Files) {
        $missing = $sourceStats.Files - $targetStats.Files
        Write-Fail "$missing of $($sourceStats.Files) file(s) could not be copied from $Source (robocopy exit code $robocopyCode)." `
            "Check that no program has these files open and that $Source is readable, then re-run Reset.bat. Nothing in this folder will be deleted."
        $robocopyOutput | Where-Object { $_ -match 'ERROR' } | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkYellow }
        Add-Result "Back up $Name" 'FAIL' "$missing file(s) not copied"
        return $false
    }
    Write-Ok "Copied $($targetStats.Files) file(s), $(Format-Size $targetStats.Bytes), from $Source"
    Add-Result "Back up $Name" 'OK' "$($targetStats.Files) file(s)"
    return $true
}

function Clear-Folder {
    param([string]$Name, [string]$Path)
    # Keep going past a locked file; the leftover check below reports anything still there
    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    $left = @(Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue)
    if ($left.Count -gt 0) {
        Write-Fail "$Name : $($left.Count) item(s) could not be deleted from $Path (first: $($left[0].FullName))." `
            "Check that no program has these files open, then delete them by hand. They are already in the backup."
        Add-Result "Clear $Name" 'FAIL' "$($left.Count) item(s) left"
    } else {
        Write-Ok "$Name cleared ($Path)"
        Add-Result "Clear $Name" 'OK' $Path
    }
}

function Write-Summary {
    Write-Host ""
    Write-Host "=================== Summary ===================" -ForegroundColor Cyan
    foreach ($r in $script:Results) {
        $colour = switch ($r.Status) { 'OK' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
        Write-Host ("  {0,-9} {1,-28} {2}" -f "[$($r.Status)]", $r.Step, $r.Detail) -ForegroundColor $colour
    }
    Write-Host ""
    if ($script:BackupDirCreated) {
        Write-Host "  Backup folder : $BackupDir" -ForegroundColor White
        Write-Host "                  (Administrators only - open it from an elevated Explorer or prompt)"
    } else {
        Write-Host "  Backup folder : not created - nothing was changed"
    }
}

# Saves the transcript into the backup folder; copied rather than moved so the file picks up
# the backup folder's admin-only permissions.
function Save-Transcript {
    try { Stop-Transcript | Out-Null } catch { }
    if ($script:BackupDirCreated -and (Test-Path $TranscriptTemp)) {
        Copy-Item -Path $TranscriptTemp -Destination (Join-Path $BackupDir 'reset-run.log') -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $TranscriptTemp -Force -ErrorAction SilentlyContinue
    }
}

function Exit-Reset {
    param([int]$Code)
    Remove-MySqlPassword
    Save-Transcript
    exit $Code
}

try { Start-Transcript -Path $TranscriptTemp -Force | Out-Null } catch { }

try {
# ===========================================================================
# Step 0: Pre-checks - nothing is changed until every one of these passes
# ===========================================================================
Write-Step "0/10 Pre-checks (nothing is changed yet)"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    # First check and stops here: every later step needs admin rights
    Write-Host ""
    Write-Host " This script must be run as Administrator." -ForegroundColor Red
    Write-Host " Right-click Reset.bat and choose 'Run as administrator'. Nothing was changed." -ForegroundColor Red
    Exit-Reset 1
}
Write-Ok "Running as Administrator."

# MySQL client tools
$script:MySqlExe = $null
if ($env:DICOM_MYSQL_BIN -and (Test-Path $env:DICOM_MYSQL_BIN)) {
    $script:MySqlExe = $env:DICOM_MYSQL_BIN
} else {
    $onPath = Get-Command mysql.exe -ErrorAction SilentlyContinue
    if ($onPath) {
        $script:MySqlExe = $onPath.Source
    } else {
        $candidate = Get-ChildItem -Path "$env:ProgramFiles\MySQL","${env:ProgramFiles(x86)}\MySQL" `
            -Filter mysql.exe -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($candidate) { $script:MySqlExe = $candidate }
    }
}
$mysqlDumpExe = $null
if ($script:MySqlExe) {
    $besideMysql = Join-Path (Split-Path $script:MySqlExe -Parent) 'mysqldump.exe'
    if (Test-Path $besideMysql) {
        $mysqlDumpExe = $besideMysql
    } else {
        $dumpOnPath = Get-Command mysqldump.exe -ErrorAction SilentlyContinue
        if ($dumpOnPath) { $mysqlDumpExe = $dumpOnPath.Source }
    }
}
if (-not $script:MySqlExe) {
    Write-Fail "mysql.exe was not found on PATH or in the default MySQL install folders." `
        "Add MySQL's 'bin' folder to PATH, or set DICOM_MYSQL_BIN to the full path of mysql.exe."
} elseif (-not $mysqlDumpExe) {
    Write-Fail "mysqldump.exe was not found next to $script:MySqlExe or on PATH." `
        "Install the MySQL client tools, or add the folder containing mysqldump.exe to PATH."
} else {
    Write-Ok "Found mysql.exe and mysqldump.exe in $(Split-Path $mysqlDumpExe -Parent)"
}

# MySQL password: the default first, then ask. Verified here so no later step can fail on it.
$passwordOk = $false
if ($script:MySqlExe) {
    $candidates = New-Object System.Collections.Generic.List[string]
    if ($env:DICOM_MYSQL_PWD) { $candidates.Add($env:DICOM_MYSQL_PWD) } else { $candidates.Add($DefaultPwd) }
    $attempt = 0
    $serverUnreachable = $false
    while (-not $passwordOk -and -not $serverUnreachable) {
        if ($candidates.Count -gt 0) {
            $tryPwd = $candidates[0]; $candidates.RemoveAt(0)
        } else {
            if ($attempt -ge $MaxPasswordAttempts) { break }
            $attempt++
            $tryPwd = Read-MaskedInput "  Enter MySQL password for '$MySqlUser' (attempt $attempt of $MaxPasswordAttempts): "
        }
        Set-MySqlPassword $tryPwd
        $ping = Invoke-MySql 'SELECT 1;'
        if ($ping.ExitCode -eq 0) {
            $passwordOk = $true
        } elseif (($ping.Output -join ' ') -match 'ERROR 1045') {
            if ($attempt -eq 0) {
                $which = if ($env:DICOM_MYSQL_PWD) { 'The password in DICOM_MYSQL_PWD' } else { "The default password '$DefaultPwd'" }
                Write-Info "$which was not accepted - please enter the MySQL password."
            } else {
                Write-Host "  Password not accepted." -ForegroundColor Yellow
            }
        } else {
            $serverUnreachable = $true
            Write-Fail "Could not connect to MySQL at ${MySqlHost}:${MySqlPort}: $($ping.Output | Select-Object -First 1)" `
                "Make sure the MySQL service is running (services.msc), or set DICOM_MYSQL_HOST / DICOM_MYSQL_PORT."
        }
    }
    if ($passwordOk) {
        Write-Ok "Connected to MySQL at ${MySqlHost}:${MySqlPort} as '$MySqlUser'."
    } elseif (-not $serverUnreachable) {
        Write-Fail "MySQL did not accept the password after $MaxPasswordAttempts attempt(s)." `
            "Find the current password for '$MySqlUser' (Initializer.bat sets it to '$DefaultPwd') and re-run Reset.bat."
    }
}

# Database, and the SCP folder settings stored in it
$dbExists = $false
$configuredFolders = @{}
$dbBytes = 0
if ($passwordOk) {
    $dbCheck = Invoke-MySql "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = '$DbName';"
    $dbExists = $dbCheck.ExitCode -eq 0 -and ($dbCheck.Output | Select-Object -First 1) -eq '1'
    if ($dbExists) {
        $sizeQuery = Invoke-MySql "SELECT COALESCE(SUM(data_length + index_length), 0) FROM information_schema.tables WHERE table_schema = '$DbName';"
        if ($sizeQuery.ExitCode -eq 0) { $dbBytes = [long]($sizeQuery.Output | Select-Object -First 1) }
        Write-Ok "Database '$DbName' found ($(Format-Size $dbBytes))."
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
    } else {
        Write-Info "Database '$DbName' does not exist - nothing to dump or drop."
    }
}

# Install folder: the build ships this script in <install folder>\scripts\
$script:InstallDir = Split-Path $PSScriptRoot -Parent
$appExeFound = $AppProcessNames | Where-Object { Test-Path -LiteralPath (Join-Path $script:InstallDir "$_.exe") -PathType Leaf }
if (-not $appExeFound) {
    Write-Fail "Could not find CARE_DICOM_Enabler.exe in $script:InstallDir." `
        "Run Reset.bat from the scripts folder of the DICOM Enabler build, then re-run it."
    $script:InstallDir = $null
} else {
    Write-Ok "Install folder: $script:InstallDir"
}

# Folders to back up and clear
$folders = @()
if ($script:InstallDir) {
    $folders = @(
        @{ Name = 'logs';      Path = (Join-Path $script:InstallDir 'logs') },
        @{ Name = 'SCP';       Path = $configuredFolders['scp_folder'] },
        @{ Name = 'FailedSCP'; Path = $configuredFolders['failed_scp_folder'] }
    )
    foreach ($f in $folders) {
        $default = Join-Path $script:InstallDir $f.Name
        if (-not $f.Path) { $f.Path = $default }
        # A malformed care_config value (e.g. a ':' after the drive letter) makes the path calls throw:
        # use the default folder under the install folder instead, as for a blank setting
        try {
            if (-not [IO.Path]::IsPathRooted($f.Path)) { $f.Path = Join-Path $script:InstallDir $f.Path }
            # No trailing '\' - robocopy would read "C:\x\" as an escaped quote
            $f.Path = Get-NormalizedPath $f.Path
        } catch {
            Write-Info "$($f.Name) folder is set to '$($f.Path)', which is not a valid folder path - using $default instead."
            $f.Path = Get-NormalizedPath $default
        }
        $reason = Get-UnsafeFolderReason $f.Path
        if ($reason) {
            Write-Fail "$($f.Name) folder is set to $($f.Path), which will not be cleared because $reason." `
                "Point $($f.Name) at a dedicated folder in the Configuration tab, then re-run Reset.bat."
        }
    }
}

# Free space on the backup drive
if ($folders.Count -gt 0) {
    $neededBytes = $dbBytes
    foreach ($f in $folders) {
        $f.Stats = Get-FolderStats $f.Path
        $neededBytes += $f.Stats.Bytes
    }
    $neededBytes = [long]($neededBytes * 1.1) + 50MB
    $backupDrive = Get-PSDrive -Name ($BackupRoot.Substring(0, 1)) -ErrorAction SilentlyContinue
    if ($backupDrive -and $backupDrive.Free -lt $neededBytes) {
        Write-Fail "Not enough free space on $($backupDrive.Name): for the backup - need about $(Format-Size $neededBytes), $(Format-Size $backupDrive.Free) free." `
            "Free up space on $($backupDrive.Name): (for example, delete old backups in $BackupRoot), then re-run Reset.bat."
    } else {
        Write-Ok "Enough free space for the backup (about $(Format-Size $neededBytes) needed)."
    }
}

if ($script:HadFailure) {
    Add-Result 'Pre-checks' 'FAIL' 'see [FAIL] lines above'
    Write-Summary
    Write-Host ""
    Write-Host " Reset did not start - fix the [FAIL] lines above and re-run Reset.bat" -ForegroundColor Yellow
    Exit-Reset 1
}
Add-Result 'Pre-checks' 'OK' ''

# ===========================================================================
# Step 1: Confirm
# ===========================================================================
Write-Step "1/10 Confirm"
Write-Host "  This will back up and then DELETE:"
if ($dbExists) { Write-Host "    - MySQL database '$DbName' on ${MySqlHost}:${MySqlPort}" }
foreach ($f in $folders) {
    Write-Host ("    - {0,-10} {1}  ({2} file(s), {3})" -f $f.Name, $f.Path, $f.Stats.Files, (Format-Size $f.Stats.Bytes))
}
Write-Host "  It will also close the DICOM Enabler app if it is open, and stop the services: $($ServiceNames -join ', ')"
Write-Host "  Backup will be saved to: $BackupDir"
Write-Host ""
$answer = Read-Host "  Type RESET to continue (anything else cancels)"
if ($answer -cne 'RESET') {
    Write-Host ""
    Write-Host " Cancelled - nothing was changed." -ForegroundColor Yellow
    Exit-Reset 0
}
Add-Result 'Confirm' 'OK' ''

# ===========================================================================
# Step 2: Stop the app and services - the app keeps its log files open
# ===========================================================================
Write-Step "2/10 Stopping the DICOM Enabler app and services"
$servicesStopped = $true
$appProcesses = @(Get-Process -Name $AppProcessNames -ErrorAction SilentlyContinue)
if ($appProcesses.Count -eq 0) {
    Write-Ok "DICOM Enabler app is not running."
} else {
    Write-Host "  DICOM Enabler app is running - stopping it..."
    foreach ($proc in $appProcesses) {
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            if (-not $proc.WaitForExit(30000)) { throw "it did not exit within 30 seconds" }
            Write-Ok "$($proc.ProcessName).exe (PID $($proc.Id)) stopped."
        } catch {
            $servicesStopped = $false
            Write-Fail "Could not stop $($proc.ProcessName).exe (PID $($proc.Id)): $($_.Exception.Message)" `
                "End it from Task Manager, then re-run Reset.bat."
        }
    }
}
foreach ($name in $ServiceNames) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Info "'$name' is not installed."
        continue
    }
    if ($svc.Status -eq 'Stopped') {
        Write-Ok "'$name' is already stopped."
        continue
    }
    try {
        Stop-Service -Name $name -Force -ErrorAction Stop
        $svc.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        Write-Ok "'$name' stopped."
    } catch {
        $servicesStopped = $false
        Write-Fail "Could not stop '$name': $($_.Exception.Message)" `
            "Stop it from services.msc (or 'sc stop `"$name`"'), then re-run Reset.bat."
    }
}
if ($servicesStopped) {
    Add-Result 'Stop app and services' 'OK' ''
} else {
    Write-Info "The backup still runs, but nothing will be deleted while the app or a service is running."
    Add-Result 'Stop app and services' 'FAIL' 'clear and drop steps will be skipped'
}

# ===========================================================================
# Step 3: Backup folder (admin-only)
# ===========================================================================
Write-Step "3/10 Creating backup folder"
try {
    New-Item -ItemType Directory -Path $BackupRoot -Force -ErrorAction Stop | Out-Null
    # Replace the whole permission list: no inherited entries, Full Control for Administrators
    # and SYSTEM only. Set on the Backups root so every run's folder inherits it.
    $admins = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $system = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($admins)
    foreach ($sid in @($admins, $system)) {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    Set-Acl -LiteralPath $BackupRoot -AclObject $acl -ErrorAction Stop

    $check = Get-Acl -LiteralPath $BackupRoot
    $others = @($check.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier]) |
        Where-Object { -not ($_.IdentityReference.Equals($admins) -or $_.IdentityReference.Equals($system)) })
    if ($others.Count -gt 0) { throw "permissions on $BackupRoot still allow: $(($others | ForEach-Object { $_.IdentityReference.Value }) -join ', ')" }

    New-Item -ItemType Directory -Path $BackupDir -ErrorAction Stop | Out-Null
    $script:BackupDirCreated = $true
    Write-Ok "Created $BackupDir (Administrators and SYSTEM only)."
    Add-Result 'Create backup folder' 'OK' ''
} catch {
    Write-Fail "Could not create the backup folder: $($_.Exception.Message)" `
        "Check that $env:ProgramData is writable by Administrators, then re-run Reset.bat. Nothing was deleted."
    Add-Result 'Create backup folder' 'FAIL' 'reset stopped, nothing deleted'
    Write-Summary
    Exit-Reset 1
}

# ===========================================================================
# Step 4: Dump MySQL
# ===========================================================================
Write-Step "4/10 Dumping MySQL database $DbName"
$dumpOk = $false
if (-not $dbExists) {
    Write-Skip "Database '$DbName' does not exist."
    Add-Result 'Dump MySQL' 'SKIPPED' 'database does not exist'
} else {
    $dumpFile = Join-Path $BackupDir "$DbName.sql"
    # Only the data still needed is kept: care_config and dcm_servers in full, uploads not yet SUCCESS,
    # worklist rows whose scan was started but not COMPLETED or CANCELLED, and every row those point to.
    $uploadWhere = "status <> 'SUCCESS'"
    $worklistWhere = "(scan_status IS NOT NULL AND scan_status NOT IN ('COMPLETED', 'CANCELLED')) OR pk IN (SELECT worklist_pk FROM care_sync_upload WHERE $uploadWhere)"
    $serviceRequestPks = "SELECT service_request_pk FROM care_worklist WHERE $worklistWhere"
    # --where applies to every table in a run, so each filter is its own run and the parts are joined
    # into one file. The first part has the structure of every table plus the routines and triggers;
    # its CREATE DATABASE / USE makes the joined file restore after the database is dropped.
    $dataOnly = @('--no-create-info', '--skip-triggers')
    $dumpParts = @(
        @{ Name = 'structure';                   Args = @('--no-data', '--routines', '--triggers', '--databases', $DbName) },
        @{ Name = 'care_config, dcm_servers';    Args = $dataOnly + @($DbName, 'care_config', 'dcm_servers') },
        @{ Name = 'care_service_request';        Args = $dataOnly + @("--where=pk IN ($serviceRequestPks)", $DbName, 'care_service_request') },
        @{ Name = 'care_service_request_device'; Args = $dataOnly + @("--where=service_request_pk IN ($serviceRequestPks)", $DbName, 'care_service_request_device') },
        @{ Name = 'care_patient';                Args = $dataOnly + @("--where=pk IN (SELECT patient_pk FROM care_worklist WHERE $worklistWhere)", $DbName, 'care_patient') },
        @{ Name = 'care_worklist';               Args = $dataOnly + @("--where=$worklistWhere", $DbName, 'care_worklist') },
        @{ Name = 'care_sync_upload';            Args = $dataOnly + @("--where=$uploadWhere", $DbName, 'care_sync_upload') }
    )
    $partFiles = @()
    $failedPart = $null
    for ($i = 0; $i -lt $dumpParts.Count; $i++) {
        $partFile = Join-Path $BackupDir ("$DbName.part{0}.sql" -f ($i + 1))
        $partFiles += $partFile
        # --result-file avoids PowerShell 5.1's '>' redirect, which writes UTF-16 that mysql cannot import;
        # --set-gtid-purged=OFF leaves out SET @@GLOBAL.GTID_PURGED, which fails the restore on a server that already has GTIDs;
        # --skip-lock-tables lets a --where subquery read tables outside the run (the services are already stopped).
        $dumpArgs = (Get-MySqlArgs) + @('--single-transaction', '--skip-lock-tables', '--set-gtid-purged=OFF', "--result-file=$partFile") + $dumpParts[$i].Args
        $dumpOutput = & $mysqlDumpExe @dumpArgs 2>&1 | ForEach-Object { "$_" }
        $dumpCode = $LASTEXITCODE
        $tail = if (Test-Path $partFile) { (Get-Content -LiteralPath $partFile -Tail 3 -ErrorAction SilentlyContinue) -join ' ' } else { '' }
        if ($dumpCode -ne 0 -or $tail -notmatch 'Dump completed') {
            $failedPart = $dumpParts[$i].Name
            break
        }
    }
    if (-not $failedPart) {
        # Joined as .partial and renamed once complete: Initializer.bat offers every backup with a
        # plexus_mi2.sql for restore, so a half-written one must never have that name.
        $joinedFile = "$dumpFile.partial"
        try {
            $out = [System.IO.File]::Create($joinedFile)
            try {
                foreach ($partFile in $partFiles) {
                    $in = [System.IO.File]::OpenRead($partFile)
                    try { $in.CopyTo($out) } finally { $in.Dispose() }
                }
            } finally { $out.Dispose() }
            Move-Item -LiteralPath $joinedFile -Destination $dumpFile -ErrorAction Stop
        } catch {
            $failedPart = 'joining the parts'
            $dumpCode = 0
            $dumpOutput = @("$($_.Exception.Message)")
        }
    }
    if (-not $failedPart) {
        Remove-Item -LiteralPath $partFiles -Force -ErrorAction SilentlyContinue
        $dumpOk = $true
        Write-Ok "Saved $DbName.sql ($(Format-Size (Get-Item -LiteralPath $dumpFile).Length))"
        Add-Result 'Dump MySQL' 'OK' "$DbName.sql"
    } else {
        Write-Fail "mysqldump failed on $failedPart (exit code $dumpCode) or the dump is incomplete." `
            "Check the message below, fix it and re-run Reset.bat. The database will not be dropped."
        $dumpOutput | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkYellow }
        Add-Result 'Dump MySQL' 'FAIL' 'database will not be dropped'
    }
}

# ===========================================================================
# Steps 5-7: Back up folders - all run even if an earlier one failed
# ===========================================================================
$stepNo = 5
foreach ($f in $folders) {
    Write-Step "$stepNo/10 Backing up $($f.Name) folder"
    $f.BackedUp = Backup-Folder $f.Name $f.Path
    $stepNo++
}

# ===========================================================================
# Step 8: Clear folders - each only when its own backup succeeded
# ===========================================================================
Write-Step "8/10 Clearing folders"
foreach ($f in $folders) {
    if (-not $servicesStopped) {
        Write-Skip "$($f.Name) not cleared - the app or a service could not be stopped (step 2)."
        Add-Result "Clear $($f.Name)" 'SKIPPED' 'app or service still running'
    } elseif (-not $f.BackedUp) {
        Write-Skip "$($f.Name) not cleared - its backup failed."
        Add-Result "Clear $($f.Name)" 'SKIPPED' 'backup failed'
    } elseif (-not (Test-Path -LiteralPath $f.Path -PathType Container)) {
        Write-Skip "$($f.Name) does not exist."
        Add-Result "Clear $($f.Name)" 'SKIPPED' 'folder does not exist'
    } else {
        Clear-Folder $f.Name $f.Path
    }
}

# ===========================================================================
# Step 9: Drop database - only when the dump succeeded
# ===========================================================================
Write-Step "9/10 Dropping database $DbName"
if (-not $dbExists) {
    Write-Skip "Database '$DbName' does not exist."
    Add-Result 'Drop database' 'SKIPPED' 'database does not exist'
} elseif (-not $servicesStopped) {
    Write-Skip "Not dropped - the app or a service could not be stopped (step 2)."
    Add-Result 'Drop database' 'SKIPPED' 'app or service still running'
} elseif (-not $dumpOk) {
    Write-Skip "Not dropped - the dump failed (step 4)."
    Add-Result 'Drop database' 'SKIPPED' 'dump failed'
} else {
    $drop = Invoke-MySql "DROP DATABASE $DbName;"
    if ($drop.ExitCode -eq 0) {
        Write-Ok "Database '$DbName' dropped."
        Add-Result 'Drop database' 'OK' ''
    } else {
        Write-Fail "Could not drop '$DbName': $($drop.Output | Select-Object -First 1)" `
            "Make sure nothing else is connected to the database, then run in a mysql shell: DROP DATABASE $DbName;"
        Add-Result 'Drop database' 'FAIL' ''
    }
}

# ===========================================================================
# Step 10: Summary
# ===========================================================================
Write-Step "10/10 Done"
Write-Summary
Write-Host ""
if ($script:HadFailure) {
    Write-Host "======================================================" -ForegroundColor Yellow
    Write-Host " Reset finished with failures - see [FAIL] lines above" -ForegroundColor Yellow
    Write-Host "======================================================" -ForegroundColor Yellow
    Exit-Reset 1
} else {
    Write-Host "  Next: run Initializer.bat (it offers to restore this backup), then start the services from the DICOM Enabler app."
    Write-Host ""
    Write-Host "========================" -ForegroundColor Green
    Write-Host " Reset complete!" -ForegroundColor Green
    Write-Host "========================" -ForegroundColor Green
    Exit-Reset 0
}
} finally {
    # Also reached on Ctrl+C: never leave the password file behind
    Remove-MySqlPassword
}
