<#
.SYNOPSIS
    AxarDB Docker Automatic Update Script for Windows Hosts.
.DESCRIPTION
    Checks Docker Hub for new AxarDB images, pulls latest layers, and recreates
    containers with existing volume mounts and data fully preserved.
.PARAMETER ContainerName
    Name of the Docker container (default: AxarDB).
.PARAMETER ImageName
    Docker image name and tag (default: metinyakar/axardb:latest or autodetected).
.PARAMETER ComposeFile
    Path to docker-compose.yml file.
.PARAMETER CheckOnly
    Check if newer image exists without restarting container.
.PARAMETER Force
    Recreate container even if image digest is unchanged.
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "AxarDB",

    [Parameter(Mandatory = $false)]
    [string]$ImageName = "",

    [Parameter(Mandatory = $false)]
    [string]$ComposeFile = "",

    [Parameter(Mandatory = $false)]
    [switch]$CheckOnly,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param ([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Log "Docker executable not found in PATH." "ERROR"
    exit 1
}

# Auto-detect docker-compose file
if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
    if (Test-Path ".\docker-compose.yml") {
        $ComposeFile = ".\docker-compose.yml"
    } elseif (Test-Path "..\docker-compose.yml") {
        $ComposeFile = "..\docker-compose.yml"
    }
}

if (-not [string]::IsNullOrWhiteSpace($ComposeFile) -and (Test-Path $ComposeFile)) {
    Write-Log "Using Docker Compose: $ComposeFile"
    Write-Log "Pulling latest service images..."
    docker compose -f $ComposeFile pull

    if ($CheckOnly) {
        Write-Log "Check-only finished for Docker Compose." "INFO"
        exit 0
    }

    Write-Log "Recreating containers with preserved volumes..."
    docker compose -f $ComposeFile up -d --remove-orphans
    Write-Log "AxarDB Docker Compose update completed successfully." "INFO"
    exit 0
}

# Standalone container mode
Write-Log "Checking standalone container: $ContainerName"
$containerInspect = docker ps -a --filter "name=^/${ContainerName}$" --format "{{.ID}}"

$containerExists = ($containerInspect.Length -gt 0)
$currentImageId = ""

if ($containerExists) {
    $currentImageId = (docker inspect --format '{{.Image}}' $ContainerName).Trim()
    if ([string]::IsNullOrWhiteSpace($ImageName)) {
        $ImageName = (docker inspect --format '{{.Config.Image}}' $ContainerName).Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($ImageName)) {
    $ImageName = "axardb:latest"
}

Write-Log "Target Docker image: $ImageName"
Write-Log "Pulling latest image from registry..."
docker pull $ImageName

$newImageId = ""
try {
    $newImageId = (docker inspect --format '{{.Id}}' $ImageName).Trim()
} catch {}

if ($containerExists -and ($currentImageId -eq $newImageId) -and (-not $Force)) {
    Write-Log "Container '$ContainerName' is already running the latest image. No update needed." "INFO"
    exit 0
}

if ($CheckOnly) {
    Write-Log "New image is available for '$ContainerName'." "INFO"
    exit 0
}

if ($containerExists) {
    Write-Log "Stopping container '$ContainerName'..."
    docker stop $ContainerName | Out-Null

    # Read mounts and ports
    $mountsRaw = docker inspect --format '{{range .Mounts}}-v {{.Source}}:{{.Destination}} {{end}}' $ContainerName
    $portsRaw = docker inspect --format '{{range $p, $conf := .NetworkSettings.Ports}}{{range $conf}}-p {{.HostIp}}:{{.HostPort}}:{{$p}} {{end}}{{end}}' $ContainerName

    if ([string]::IsNullOrWhiteSpace($portsRaw)) {
        $portsRaw = "-p 5000:5000"
    }

    if ([string]::IsNullOrWhiteSpace($mountsRaw)) {
        $localData = Join-Path (Get-Location).Path "data"
        if (-not (Test-Path $localData)) { New-Item -ItemType Directory -Path $localData -Force | Out-Null }
        $mountsRaw = "-v `"${localData}:/app/data`""
    }

    Write-Log "Removing old container structure (volumes preserved on host)..."
    docker rm $ContainerName | Out-Null

    Write-Log "Starting updated container '$ContainerName'..."
    $runArgs = "run -d --name `"$ContainerName`" --restart unless-stopped $portsRaw $mountsRaw `"$ImageName`""
    Invoke-Expression "docker $runArgs"
} else {
    $localData = Join-Path (Get-Location).Path "data"
    if (-not (Test-Path $localData)) { New-Item -ItemType Directory -Path $localData -Force | Out-Null }
    Write-Log "Creating container '$ContainerName'..."
    docker run -d --name $ContainerName --restart unless-stopped -p 5000:5000 -v "${localData}:/app/data" $ImageName
}

Write-Log "AxarDB Docker container '$ContainerName' updated successfully without data loss." "INFO"
