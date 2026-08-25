#!/usr/bin/env bash
#
# AxarDB Scheduled Task Configuration Script for Ubuntu
# Configures daily automated update checks via cron or systemd timer.
#

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UPDATE_SCRIPT="$SCRIPT_DIR/update.sh"
TIME_HOUR="03"
TIME_MIN="00"
METHOD="cron"
INSTALL_DIR="/opt/axardb"
SERVICE_NAME="axardb.service"
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
  -m, --method METHOD    Scheduling method: 'cron' or 'timer' (default: cron)
  -d, --dir DIR          Target AxarDB installation directory (default: /opt/axardb)
  -s, --service NAME     Systemd service name (default: axardb.service)
  -r, --remove           Remove scheduled update job
  -h, --help             Display this help message
EOF
    exit 1
}

# Require root
if [[ $EUID -ne 0 ]]; then
    log "ERROR" "Root privileges are required to configure system cron/timers. Run with sudo."
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t|--time)
            IFS=':' read -r TIME_HOUR TIME_MIN <<< "$2"
            shift 2
            ;;
        -m|--method)
            METHOD="$2"
            shift 2
            ;;
        -d|--dir)
            INSTALL_DIR="$2"
            shift 2
            ;;
        -s|--service)
            SERVICE_NAME="$2"
            if [[ "$SERVICE_NAME" != *.service ]]; then
                SERVICE_NAME="${SERVICE_NAME}.service"
            fi
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

CRON_FILE="/etc/cron.d/axardb-update"
SERVICE_DEST="/etc/systemd/system/axardb-update.service"
TIMER_DEST="/etc/systemd/system/axardb-update.timer"

if [[ $REMOVE -eq 1 ]]; then
    log "INFO" "Removing AxarDB scheduled update configurations..."
    rm -f "$CRON_FILE"
    if systemctl is-enabled --quiet axardb-update.timer 2>/dev/null; then
        systemctl stop axardb-update.timer || true
        systemctl disable axardb-update.timer || true
    fi
    rm -f "$SERVICE_DEST" "$TIMER_DEST"
    systemctl daemon-reload || true
    log "INFO" "Scheduled update tasks removed successfully."
    exit 0
fi

chmod +x "$UPDATE_SCRIPT"

if [[ "$METHOD" == "cron" ]]; then
    log "INFO" "Configuring daily cron job at $TIME_HOUR:$TIME_MIN..."
    cat > "$CRON_FILE" <<EOF
# AxarDB daily automatic update job
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/sbin:/bin:/usr/sbin:/usr/bin

$TIME_MIN $TIME_HOUR * * * root $UPDATE_SCRIPT --dir "$INSTALL_DIR" --service "$SERVICE_NAME" >> /var/log/axardb-update.log 2>&1
EOF
    chmod 644 "$CRON_FILE"
    log "INFO" "Cron job created at $CRON_FILE. Scheduled daily at $TIME_HOUR:$TIME_MIN."
elif [[ "$METHOD" == "timer" ]]; then
    log "INFO" "Configuring systemd update service and timer at $TIME_HOUR:$TIME_MIN..."

    cat > "$SERVICE_DEST" <<EOF
[Unit]
Description=AxarDB Daily Release Auto-Updater
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
WorkingDirectory=$INSTALL_DIR
ExecStart=/bin/bash $UPDATE_SCRIPT --dir "$INSTALL_DIR" --service "$SERVICE_NAME"
StandardOutput=journal+console
StandardError=journal+console
EOF

    cat > "$TIMER_DEST" <<EOF
[Unit]
Description=AxarDB Daily Auto-Update Timer (03:00)

[Timer]
OnCalendar=*-*-* $TIME_HOUR:$TIME_MIN:00
Persistent=true
Unit=axardb-update.service

[Install]
WantedBy=timers.target
EOF

    systemctl daemon-reload
    systemctl enable --now axardb-update.timer
    log "INFO" "systemd timer enabled. Active timers:"
    systemctl list-timers --all | grep axardb || true
else
    log "ERROR" "Invalid method: $METHOD. Must be 'cron' or 'timer'."
    exit 1
fi
