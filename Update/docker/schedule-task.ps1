<#
.SYNOPSIS
    AxarDB Docker Update Scheduling Script (Windows Task Scheduler).
.DESCRIPTION
    Registers a daily scheduled task in Windows Task Scheduler to check and update AxarDB Docker containers.
.PARAMETER TaskName
    Name of the scheduled task (default: AxarDB-DockerDailyUpdate).
.PARAMETER DailyTime
    Daily execution time in 24-hour format HH:mm (default: 03:00).
.PARAMETER ContainerName
    Name of the Docker container (default: AxarDB).
.PARAMETER ComposeFile
    Path to docker-compose.yml file.
.PARAMETER Remove
    Removes the existing scheduled task.
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$TaskName = "AxarDB-DockerDailyUpdate",

    [Parameter(Mandatory = $false)]
    [string]$DailyTime = "03:00",

    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "AxarDB",

    [Parameter(Mandatory = $false)]
    [string]$ComposeFile = "",

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

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Log "Administrative privileges required. Please run PowerShell as Administrator." "ERROR"
    exit 1
}

if ($Remove) {
    Write-Log "Unregistering scheduled task '$TaskName'..."
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Log "Scheduled task '$TaskName' removed successfully." "INFO"
    exit 0
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$updateScript = Join-Path $scriptDir "update.ps1"

if (-not (Test-Path $updateScript)) {
    Write-Log "Update script not found at '$updateScript'" "ERROR"
    exit 1
}

$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$updateScript`""
if (-not [string]::IsNullOrWhiteSpace($ComposeFile)) {
    $arguments += " -ComposeFile `"$ComposeFile`""
} else {
    $arguments += " -ContainerName `"$ContainerName`""
}

Write-Log "Configuring daily Docker update scheduled task '$TaskName' at $DailyTime..."

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Daily -At ([datetime]::ParseExact($DailyTime, "HH:mm", $null))
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null

Write-Log "Scheduled task '$TaskName' registered successfully to run daily at $DailyTime." "INFO"
