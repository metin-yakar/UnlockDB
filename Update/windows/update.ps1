<#
.SYNOPSIS
    AxarDB Automatic Release Update Script for Windows.
.DESCRIPTION
    Downloads precompiled release binaries directly from GitHub Releases,
    safely updates application binaries, and preserves all user database data.
.PARAMETER InstallDir
    Target installation directory of AxarDB. Defaults to auto-detected path.
.PARAMETER ServiceName
    Windows Service name if AxarDB runs as a service (default: AxarDB).
.PARAMETER CheckOnly
    Only check if a new version is available without applying updates.
.PARAMETER Force
    Force update even if the current version matches the latest release.
.PARAMETER RepoOwner
    GitHub repository owner (default: metin-yakar).
.PARAMETER RepoName
    GitHub repository name (default: AxarDB).
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$InstallDir = "",

    [Parameter(Mandatory = $false)]
    [string]$ServiceName = "AxarDB",

    [Parameter(Mandatory = $false)]
    [switch]$CheckOnly,

    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [string]$RepoOwner = "metin-yakar",

    [Parameter(Mandatory = $false)]
    [string]$RepoName = "AxarDB"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param ([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Resolve Installation Directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    if ((Test-Path (Join-Path $scriptDir "AxarDB.exe")) -or (Test-Path (Join-Path $scriptDir "AxarDB.dll"))) {
        $InstallDir = $scriptDir
    } elseif ((Test-Path (Join-Path (Split-Path -Parent $scriptDir) "AxarDB.exe")) -or (Test-Path (Join-Path (Split-Path -Parent $scriptDir) "AxarDB.dll"))) {
        $InstallDir = Split-Path -Parent $scriptDir
    } elseif ((Test-Path ".\AxarDB.exe") -or (Test-Path ".\AxarDB.dll")) {
        $InstallDir = (Get-Item ".").FullName
    } elseif (Test-Path "C:\Program Files\AxarDB\AxarDB.exe") {
        $InstallDir = "C:\Program Files\AxarDB"
    } else {
        $InstallDir = (Get-Item ".").FullName
    }
}

$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
Write-Log "Target installation directory: $InstallDir"

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Version tracking file (.version)
$versionFile = Join-Path $InstallDir ".version"
$legacyVersionFile = Join-Path $InstallDir "version.txt"
$currentVersion = "none"

if (Test-Path $versionFile) {
    $currentVersion = (Get-Content $versionFile -Raw).Trim()
} elseif (Test-Path $legacyVersionFile) {
    $currentVersion = (Get-Content $legacyVersionFile -Raw).Trim()
} elseif (Test-Path (Join-Path $InstallDir "AxarDB.dll")) {
    try {
        $fileVer = (Get-Item (Join-Path $InstallDir "AxarDB.dll")).VersionInfo.FileVersion
        if (![string]::IsNullOrWhiteSpace($fileVer)) {
            $currentVersion = "v" + $fileVer
        }
    } catch {
        $currentVersion = "unknown"
    }
}

Write-Log "Current installed version: $currentVersion"

# ==============================================================================
# Auto Self-Registration: Daily (03:00) Windows Scheduled Task
# ==============================================================================
function Ensure-ScheduledTask {
    param ([string]$TaskName = "AxarDB-DailyUpdate", [string]$Time = "03:00")
    try {
        $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if (-not $isAdmin) {
            return
        }

        $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
        if ($null -ne $existingTask) {
            return
        }

        Write-Log "Configuring automated daily update scheduled task ($Time)..."
        $scriptPath = Join-Path $scriptDir "update.ps1"
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -InstallDir `"$InstallDir`" -ServiceName `"$ServiceName`""

        $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
        $trigger = New-ScheduledTaskTrigger -Daily -At ([datetime]::ParseExact($Time, "HH:mm", $null))
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew
        $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

        Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
        Write-Log "Daily scheduled task '$TaskName' registered successfully ($Time)."
    } catch {
        Write-Log "Notice: Could not auto-register scheduled task: $($_.Exception.Message)" "WARN"
    }
}

Ensure-ScheduledTask

# Query GitHub Releases API for Latest Release Tag
$apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
$latestDownloadUrl = "https://github.com/$RepoOwner/$RepoName/releases/latest/download/AxarDB-windows.zip"
Write-Log "Checking for updates from: $apiUrl"

$headers = @{
    "User-Agent" = "AxarDB-Updater-Windows"
    "Accept"     = "application/vnd.github.v3+json"
}

$latestVersion = "latest"
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
    $releaseInfo = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get -TimeoutSec 30
    if ($null -ne $releaseInfo.tag_name) {
        $latestVersion = $releaseInfo.tag_name
    }
} catch {
    Write-Log "Warning: Could not fetch tag from GitHub API ($($_.Exception.Message)). Falling back to direct download." "WARN"
}

Write-Log "Latest available release: $latestVersion"

# Check if Update is Required
if ($CheckOnly) {
    if ($currentVersion -eq $latestVersion -and $latestVersion -ne "latest") {
        Write-Log "AxarDB is currently up-to-date ($currentVersion)." "INFO"
    } else {
        Write-Log "Update is available: $currentVersion -> $latestVersion" "INFO"
    }
    exit 0
}

if ($currentVersion -eq $latestVersion -and $latestVersion -ne "latest" -and -not $Force) {
    Write-Log "AxarDB is already up-to-date ($currentVersion). No action required." "INFO"
    exit 0
}

Write-Log "Starting update procedure: $currentVersion -> $latestVersion" "INFO"

# Prepare Temporary Staging Workspace
$tempId = [Guid]::NewGuid().ToString("N")
$stagingBase = Join-Path ([System.IO.Path]::GetTempPath()) "axardb_update_$tempId"
$zipPath = Join-Path $stagingBase "AxarDB-windows.zip"
$extractDir = Join-Path $stagingBase "extracted"

New-Item -ItemType Directory -Path $stagingBase -Force | Out-Null
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

$serviceWasRunning = $false
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

try {
    # Download Precompiled Windows Release Archive
    Write-Log "Downloading latest package: $latestDownloadUrl..."
    Invoke-WebRequest -Uri $latestDownloadUrl -OutFile $zipPath -Headers $headers -UseBasicParsing

    if (-not (Test-Path $zipPath) -or (Get-Item $zipPath).Length -eq 0) {
        throw "Downloaded package is empty or failed to download."
    }

    Write-Log "Extracting release package..."
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    # Resolve inner root if archive contains 'windows/' folder
    $sourceDir = $extractDir
    if (Test-Path (Join-Path $extractDir "windows")) {
        $sourceDir = Join-Path $extractDir "windows"
    }

    # Stop AxarDB Service / Processes if Running
    if ($null -ne $service) {
        if ($service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) {
            Write-Log "Stopping Windows service '$ServiceName'..."
            Stop-Service -Name $ServiceName -Force
            $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
            $serviceWasRunning = $true
            Write-Log "Service '$ServiceName' stopped successfully."
        }
    } else {
        $runningProcesses = Get-Process -Name "AxarDB" -ErrorAction SilentlyContinue
        if ($runningProcesses) {
            Write-Log "Stopping running AxarDB processes..."
            $runningProcesses | Stop-Process -Force
            Start-Sleep -Seconds 2
        }
    }

    # CRITICAL: Safe Copy Routine (Strict Data Preservation)
    # Protected directories and files:
    # - Data/ (Database collections and documents)
    # - Bulk/ (JSONL Bulk store tables)
    # - Views/ (User stored query scripts)
    # - Triggers/ (User trigger event scripts)
    # - backup_queries/ (Query backups)
    # - uploads/ (Uploaded user assets)
    # - *logs/ / logs/ (All log directories)
    # - appsettings.json (Configuration)
    # - .version (Internal version tracking)
    $protectedFolders = @("Data", "data", "Bulk", "bulk", "Views", "views", "Triggers", "triggers", "backup_queries", "uploads", "Uploads")

    Write-Log "Syncing application binaries -> $InstallDir (Preserving user data, configs, and logs)..."

    $itemsToCopy = Get-ChildItem -Path $sourceDir
    foreach ($item in $itemsToCopy) {
        $destPath = Join-Path $InstallDir $item.Name

        if ($item.PSIsContainer) {
            # Skip replacing protected user data directories
            if ($protectedFolders -contains $item.Name -or $item.Name -like "*log*" -or $item.Name -like "*logs*") {
                if (Test-Path $destPath) {
                    Write-Log "Preserving existing data directory: $($item.Name)" "INFO"
                    continue
                }
            }
            # Copy non-protected directories (such as wwwroot, Docs)
            Copy-Item -Path $item.FullName -Destination $destPath -Recurse -Force
        } else {
            # Files: Protect user modified appsettings.json
            if ($item.Name -eq "appsettings.json" -and (Test-Path $destPath)) {
                Write-Log "Preserving existing appsettings.json configuration file." "INFO"
                continue
            }
            if ($item.Name -eq ".version") {
                continue
            }
            Copy-Item -Path $item.FullName -Destination $destPath -Force
        }
    }

    # Record New Version
    Set-Content -Path $versionFile -Value $latestVersion -Encoding UTF8
    Write-Log "Version file updated to $latestVersion."

    # Restart Service if Applicable
    if ($serviceWasRunning -and $null -ne $service) {
        Write-Log "Restarting Windows service '$ServiceName'..."
        Start-Service -Name $ServiceName
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
        Write-Log "SUCCESS: Windows service '$ServiceName' started successfully."
    }

    Write-Log "SUCCESS: AxarDB update to $latestVersion completed successfully without data loss." "INFO"
}
catch {
    Write-Log "ERROR: Update failed: $($_.Exception.Message)" "ERROR"
    if ($serviceWasRunning -and $null -ne $service) {
        Write-Log "Attempting to restart service '$ServiceName'..." "WARN"
        Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    }
    exit 1
}
finally {
    # Cleanup temporary workspace
    if (Test-Path $stagingBase) {
        Remove-Item -Path $stagingBase -Recurse -Force -ErrorAction SilentlyContinue
    }
}
