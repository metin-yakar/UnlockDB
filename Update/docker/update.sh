#!/usr/bin/env bash
#
# AxarDB Docker Automatic Release Update Script
# Updates AxarDB Docker containers (Docker Compose & Standalone) to the latest Docker Hub image
# while guaranteeing all database data and mounted volumes remain completely preserved.
#

set -euo pipefail

CONTAINER_NAME="AxarDB"
IMAGE_NAME=""
COMPOSE_FILE=""
FORCE_UPDATE=0
CHECK_ONLY=0

log() {
    local level="$1"
    shift
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [$level] $*"
}

usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Options:
  -c, --container NAME   Container name (default: AxarDB)
  -i, --image IMAGE      Docker image tag (e.g. metinyakar/axardb:latest)
  -f, --file FILE        Path to docker-compose.yml (optional)
  -k, --check            Check if a new image is available without restarting
  -F, --force            Force container recreation even if image is unchanged
  -h, --help             Display this help message
EOF
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -c|--container)
            CONTAINER_NAME="$2"
            shift 2
            ;;
        -i|--image)
            IMAGE_NAME="$2"
            shift 2
            ;;
        -f|--file)
            COMPOSE_FILE="$2"
            shift 2
            ;;
        -k|--check)
            CHECK_ONLY=1
            shift
            ;;
        -F|--force)
            FORCE_UPDATE=1
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            log "ERROR" "Unknown argument: $1"
            usage
            ;;
    esac
done

if ! command -v docker &>/dev/null; then
    log "ERROR" "Docker is not installed or not in PATH."
    exit 1
fi

# Detect Docker Compose file if not specified
if [[ -z "$COMPOSE_FILE" ]]; then
    if [[ -f "./docker-compose.yml" ]]; then
        COMPOSE_FILE="./docker-compose.yml"
    elif [[ -f "../docker-compose.yml" ]]; then
        COMPOSE_FILE="../docker-compose.yml"
    fi
fi

# Strategy 1: Docker Compose
if [[ -n "$COMPOSE_FILE" && -f "$COMPOSE_FILE" ]]; then
    log "INFO" "Using Docker Compose configuration: $COMPOSE_FILE"

    # Select compose command
    COMPOSE_CMD="docker compose"
    if ! docker compose version &>/dev/null; then
        if command -v docker-compose &>/dev/null; then
            COMPOSE_CMD="docker-compose"
        else
            log "ERROR" "Neither 'docker compose' nor 'docker-compose' found."
            exit 1
        fi
    fi

    log "INFO" "Checking and pulling latest images for Compose service..."
    PULL_OUTPUT=$($COMPOSE_CMD -f "$COMPOSE_FILE" pull 2>&1)
    echo "$PULL_OUTPUT"

    if [[ $CHECK_ONLY -eq 1 ]]; then
        log "INFO" "Check-only completed for Docker Compose."
        exit 0
    fi

    log "INFO" "Recreating containers with latest image (data volumes preserved)..."
    $COMPOSE_CMD -f "$COMPOSE_FILE" up -d --remove-orphans
    log "INFO" "AxarDB Docker Compose update completed successfully."
    exit 0
fi

# Strategy 2: Standalone Container
log "INFO" "Checking standalone Docker container: $CONTAINER_NAME"

CONTAINER_EXISTS=0
if docker ps -a --format '{{.Names}}' | grep -Eq "^${CONTAINER_NAME}$"; then
    CONTAINER_EXISTS=1
fi

if [[ $CONTAINER_EXISTS -eq 0 ]]; then
    log "WARN" "Container '$CONTAINER_NAME' is not currently running or created."
    if [[ -z "$IMAGE_NAME" ]]; then
        IMAGE_NAME="axardb:latest"
    fi
fi

# Inspect existing container if running
if [[ $CONTAINER_EXISTS -eq 1 ]]; then
    CURRENT_IMAGE_ID=$(docker inspect --format '{{.Image}}' "$CONTAINER_NAME" 2>/dev/null || echo "")
    if [[ -z "$IMAGE_NAME" ]]; then
        IMAGE_NAME=$(docker inspect --format '{{.Config.Image}}' "$CONTAINER_NAME" 2>/dev/null || echo "axardb:latest")
    fi
else
    CURRENT_IMAGE_ID=""
fi

log "INFO" "Target Docker Image: $IMAGE_NAME"
log "INFO" "Pulling latest image from registry..."
docker pull "$IMAGE_NAME"

NEW_IMAGE_ID=$(docker inspect --format '{{.Id}}' "$IMAGE_NAME" 2>/dev/null || echo "")

if [[ "$CURRENT_IMAGE_ID" == "$NEW_IMAGE_ID" && $FORCE_UPDATE -eq 0 && $CONTAINER_EXISTS -eq 1 ]]; then
    log "INFO" "Container '$CONTAINER_NAME' is already running the latest image ($NEW_IMAGE_ID). No update needed."
    exit 0
fi

if [[ $CHECK_ONLY -eq 1 ]]; then
    log "INFO" "A newer image is available for container '$CONTAINER_NAME'."
    exit 0
fi

if [[ $CONTAINER_EXISTS -eq 1 ]]; then
    log "INFO" "Stopping and recreating container '$CONTAINER_NAME' (preserving volumes)..."

    # Extract mount configurations and ports
    MOUNTS=$(docker inspect --format '{{range .Mounts}}-v {{.Source}}:{{.Destination}} {{end}}' "$CONTAINER_NAME")
    PORTS=$(docker inspect --format '{{range $p, $conf := .NetworkSettings.Ports}}{{range $conf}}-p {{.HostIp}}:{{.HostPort}}:{{$p}} {{end}}{{end}}' "$CONTAINER_NAME")
    
    # If no custom ports detected, default to 5000:5000
    if [[ -z "$PORTS" || "$PORTS" =~ ^[[:space:]]*$ ]]; then
        PORTS="-p 5000:5000"
    fi
    # If no custom mounts detected, default to $(pwd)/data:/app/data
    if [[ -z "$MOUNTS" || "$MOUNTS" =~ ^[[:space:]]*$ ]]; then
        mkdir -p "$(pwd)/data"
        MOUNTS="-v $(pwd)/data:/app/data"
    fi

    docker stop "$CONTAINER_NAME"
    docker rm "$CONTAINER_NAME"

    # Launch new container with preserved volumes
    log "INFO" "Starting updated container with mounts: $MOUNTS"
    eval "docker run -d --name \"$CONTAINER_NAME\" --restart unless-stopped $PORTS $MOUNTS \"$IMAGE_NAME\""
else
    mkdir -p "$(pwd)/data"
    log "INFO" "Creating fresh container '$CONTAINER_NAME' with persistent data mount..."
    docker run -d --name "$CONTAINER_NAME" --restart unless-stopped -p 5000:5000 -v "$(pwd)/data:/app/data" "$IMAGE_NAME"
fi

log "INFO" "AxarDB Docker container '$CONTAINER_NAME' updated successfully without data loss."
