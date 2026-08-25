<#
.SYNOPSIS
    AxarDB Windows Scheduled Task Installer.
.DESCRIPTION
    Registers a daily scheduled task in Windows Task Scheduler to check and apply AxarDB updates.
.PARAMETER TaskName
    Name of the scheduled task (default: AxarDB-DailyUpdate).
.PARAMETER DailyTime
    Daily execution time in 24-hour format HH:mm (default: 03:00).
.PARAMETER InstallDir
    Installation directory of AxarDB.
.PARAMETER ServiceName
    Service name to restart after update (default: AxarDB).
.PARAMETER Remove
    Removes the existing scheduled task.
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$TaskName = "AxarDB-DailyUpdate",

    [Parameter(Mandatory = $false)]
    [string]$DailyTime = "03:00",

    [Parameter(Mandatory = $false)]
    [string]$InstallDir = "",

    [Parameter(Mandatory = $false)]
    [string]$ServiceName = "AxarDB",

    [Parameter(Mandatory = $false)]
    [switch]$Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param ([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Check Administrative Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Log "Administrative privileges are required to register Windows Scheduled Tasks. Please run PowerShell as Administrator." "ERROR"
    exit 1
}

if ($Remove) {
    Write-Log "Unregistering scheduled task '$TaskName'..."
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Log "Scheduled task '$TaskName' removed successfully." "INFO"
    exit 0
}

# Resolve Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$updateScript = Join-Path $scriptDir "update.ps1"

if (-not (Test-Path $updateScript)) {
    Write-Log "Update script not found at '$updateScript'" "ERROR"
    exit 1
}

# Build Arguments
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$updateScript`""
if (-not [string]::IsNullOrWhiteSpace($InstallDir)) {
    $arguments += " -InstallDir `"$InstallDir`""
}
if (-not [string]::IsNullOrWhiteSpace($ServiceName)) {
    $arguments += " -ServiceName `"$ServiceName`""
}

Write-Log "Configuring daily scheduled task '$TaskName' to run at $DailyTime..."

# Create Scheduled Task Action, Trigger, Settings, Principal
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Daily -At ([datetime]::ParseExact($DailyTime, "HH:mm", $null))
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

# Register or Update Task
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null

Write-Log "Scheduled task '$TaskName' registered successfully to execute daily at $DailyTime." "INFO"
