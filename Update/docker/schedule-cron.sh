#!/usr/bin/env bash
#
# AxarDB Docker Update Scheduling Script (Linux Cron)
# Configures a daily cron job to check and update AxarDB Docker containers.
#

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UPDATE_SCRIPT="$SCRIPT_DIR/update.sh"
TIME_HOUR="03"
TIME_MIN="00"
CONTAINER_NAME="AxarDB"
COMPOSE_FILE=""
IMAGE_NAME=""
REMOVE=0

log() {
    local level="$1"
    shift
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [$level] $*"
}

usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Options:
  -t, --time HH:MM       Daily execution time in 24h format (default: 03:00)
  -c, --container NAME   Container name (default: AxarDB)
  -f, --file FILE        Path to docker-compose.yml (optional)
  -i, --image IMAGE      Docker image name (optional)
  -r, --remove           Remove scheduled update job
  -h, --help             Display this help message
EOF
    exit 1
}

if [[ $EUID -ne 0 ]]; then
    log "ERROR" "Root privileges are required to configure system cron. Run with sudo."
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t|--time)
            IFS=':' read -r TIME_HOUR TIME_MIN <<< "$2"
            shift 2
            ;;
        -c|--container)
            CONTAINER_NAME="$2"
            shift 2
            ;;
        -f|--file)
            COMPOSE_FILE="$2"
            shift 2
            ;;
        -i|--image)
            IMAGE_NAME="$2"
            shift 2
            ;;
        -r|--remove)
            REMOVE=1
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

CRON_FILE="/etc/cron.d/axardb-docker-update"

if [[ $REMOVE -eq 1 ]]; then
    log "INFO" "Removing AxarDB Docker update cron configuration..."
    rm -f "$CRON_FILE"
    log "INFO" "Cron job removed successfully."
    exit 0
fi

chmod +x "$UPDATE_SCRIPT"

ARGS=""
if [[ -n "$COMPOSE_FILE" ]]; then
    ARGS="--file $COMPOSE_FILE"
else
    ARGS="--container $CONTAINER_NAME"
    if [[ -n "$IMAGE_NAME" ]]; then
        ARGS="$ARGS --image $IMAGE_NAME"
    fi
fi

log "INFO" "Creating daily cron job at $TIME_HOUR:$TIME_MIN..."
cat > "$CRON_FILE" <<EOF
# AxarDB Docker daily automatic update job
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/sbin:/bin:/usr/sbin:/usr/bin

$TIME_MIN $TIME_HOUR * * * root $UPDATE_SCRIPT $ARGS >> /var/log/axardb-docker-update.log 2>&1
EOF

chmod 644 "$CRON_FILE"
log "INFO" "Docker update cron job registered at $CRON_FILE (Scheduled daily at $TIME_HOUR:$TIME_MIN)."
