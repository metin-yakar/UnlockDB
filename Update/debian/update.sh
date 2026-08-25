#!/usr/bin/env bash
#
# AxarDB Automatic Release Update Script for Debian
# Downloads precompiled release binaries directly from GitHub Releases,
# safely updates the application files, and preserves all user database data.
#

set -euo pipefail

# Configuration defaults
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${SCRIPT_DIR}"
LOG_FILE="/var/log/axardb-update.log"
GITHUB_REPO="${AXARDB_REPO:-metin-yakar/AxarDB}"
SERVICE_NAME="axardb.service"
UPDATE_SERVICE="axardb-update.service"
UPDATE_TIMER="axardb-update.timer"
TEMP_DIR="/tmp/axardb_update"
CHECK_ONLY=false
FORCE_UPDATE=false

log() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo "$msg"
    if [ -w "$(dirname "$LOG_FILE")" ] 2>/dev/null || [ "$EUID" -eq 0 ]; then
        echo "$msg" >> "$LOG_FILE" 2>/dev/null || true
    fi
}

usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Options:
  -d, --dir DIR          Set AxarDB installation directory (default: auto-detected)
  -s, --service NAME     Set systemd service name (default: axardb.service)
  -c, --check            Check for update availability without applying changes
  -f, --force            Force update even if version is already up-to-date
  -h, --help             Display this help message
EOF
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -d|--dir)
            APP_DIR="$2"
            shift 2
            ;;
        -s|--service)
            SERVICE_NAME="$2"
            if [[ "$SERVICE_NAME" != *.service ]]; then
                SERVICE_NAME="${SERVICE_NAME}.service"
            fi
            shift 2
            ;;
        -c|--check)
            CHECK_ONLY=true
            shift
            ;;
        -f|--force)
            FORCE_UPDATE=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            log "ERROR: Unknown argument: $1"
            usage
            ;;
    esac
done

# Auto-detect installation directory if not explicitly provided
if [[ "$APP_DIR" == "$SCRIPT_DIR" ]]; then
    if [[ -f "$SCRIPT_DIR/AxarDB" || -f "$SCRIPT_DIR/AxarDB.dll" ]]; then
        APP_DIR="$SCRIPT_DIR"
    elif [[ -f "$(dirname "$SCRIPT_DIR")/AxarDB" || -f "$(dirname "$SCRIPT_DIR")/AxarDB.dll" ]]; then
        APP_DIR="$(cd "$(dirname "$SCRIPT_DIR")" && pwd)"
    elif [[ -d "/opt/axardb" ]]; then
        APP_DIR="/opt/axardb"
    fi
fi

APP_DIR="$(cd "$APP_DIR" && pwd)"
SCRIPT_PATH="$(cd "$SCRIPT_DIR" && pwd)/$(basename "${BASH_SOURCE[0]}")"
VERSION_FILE="${APP_DIR}/.version"
LATEST_DOWNLOAD_URL="https://github.com/${GITHUB_REPO}/releases/latest/download/AxarDB-debian.zip"
API_LATEST_URL="https://api.github.com/repos/${GITHUB_REPO}/releases/latest"

# ==============================================================================
# Auto Self-Registration: Daily (03:00) systemd timer configuration
# ==============================================================================
ensure_scheduled_task() {
    if ! command -v systemctl >/dev/null 2>&1; then
        return 0
    fi

    if [ "$EUID" -ne 0 ]; then
        return 0
    fi

    local timer_path="/etc/systemd/system/${UPDATE_TIMER}"
    local service_path="/etc/systemd/system/${UPDATE_SERVICE}"

    if [ -f "$timer_path" ] && systemctl is-active --quiet "$UPDATE_TIMER" 2>/dev/null; then
        return 0
    fi

    log "Configuring automated daily update timer (03:00)..."

    cat << EOF > "$service_path"
[Unit]
Description=AxarDB Daily Release Auto-Updater
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
WorkingDirectory=${APP_DIR}
ExecStart=/bin/bash ${SCRIPT_PATH} --dir ${APP_DIR} --service ${SERVICE_NAME}
StandardOutput=journal+console
StandardError=journal+console
EOF

    cat << EOF > "$timer_path"
[Unit]
Description=AxarDB Daily Auto-Update Timer (03:00)

[Timer]
OnCalendar=*-*-* 03:00:00
Persistent=true
Unit=${UPDATE_SERVICE}

[Install]
WantedBy=timers.target
EOF

    systemctl daemon-reload
    systemctl enable --now "$UPDATE_TIMER" 2>/dev/null || true
    log "Daily update timer successfully registered and activated (${UPDATE_TIMER} at 03:00)."
}

# 1. Ensure scheduled background task is registered when running as root
ensure_scheduled_task

log "=== AxarDB Release Update Task Started ==="
log "Working Directory: ${APP_DIR}"

# 2. Retrieve latest release tag from GitHub API
log "Fetching latest release info from ${API_LATEST_URL}..."
RELEASE_JSON=$(curl -sL -H "User-Agent: AxarDB-Updater" "${API_LATEST_URL}" || echo "")

LATEST_TAG=""
if [ -n "$RELEASE_JSON" ]; then
    LATEST_TAG=$(echo "$RELEASE_JSON" | grep -o '"tag_name": *"[^"]*"' | head -n 1 | cut -d'"' -f4 || echo "")
fi

if [ -z "$LATEST_TAG" ]; then
    log "WARNING: Could not parse tag from GitHub API, falling back to direct latest download."
    LATEST_TAG="latest"
fi

log "Target release tag: ${LATEST_TAG}"

# 3. Check currently installed version
CURRENT_TAG=""
if [ -f "$VERSION_FILE" ]; then
    CURRENT_TAG=$(cat "$VERSION_FILE" | tr -d '[:space:]')
elif [ -f "${APP_DIR}/version.txt" ]; then
    CURRENT_TAG=$(cat "${APP_DIR}/version.txt" | tr -d '[:space:]')
fi

log "Current installed version: ${CURRENT_TAG:-none}"

if [ "$CHECK_ONLY" = true ]; then
    if [ "$CURRENT_TAG" = "$LATEST_TAG" ] && [ "$LATEST_TAG" != "latest" ]; then
        log "AxarDB is currently up-to-date (${CURRENT_TAG})."
    else
        log "Update available: '${CURRENT_TAG:-none}' -> '${LATEST_TAG}'."
    fi
    log "=== AxarDB Check Finished ==="
    exit 0
fi

# If already up-to-date and not forced: do nothing, do NOT restart service
if [ "$CURRENT_TAG" = "$LATEST_TAG" ] && [ "$LATEST_TAG" != "latest" ] && [ "$FORCE_UPDATE" = false ]; then
    log "AxarDB is already up-to-date (Version: ${CURRENT_TAG}). No action needed. Service not restarted."
    log "=== AxarDB Release Update Task Finished ==="
    exit 0
fi

log "New version detected or forced update requested: '${CURRENT_TAG:-none}' -> '${LATEST_TAG}'..."

# Ensure prerequisite tools
for cmd in curl unzip rsync; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        if [ "$EUID" -eq 0 ] && command -v apt-get >/dev/null 2>&1; then
            log "Installing missing prerequisite '$cmd'..."
            apt-get update -qq && apt-get install -y -qq "$cmd"
        else
            log "ERROR: Prerequisite tool '$cmd' is required but not installed."
            exit 1
        fi
    fi
done

# 4. Prepare temporary directory and download release bundle
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

log "Downloading latest package: ${LATEST_DOWNLOAD_URL}..."
curl -sL -H "User-Agent: AxarDB-Updater" "$LATEST_DOWNLOAD_URL" -o "$TEMP_DIR/AxarDB-debian.zip"

if [ ! -s "$TEMP_DIR/AxarDB-debian.zip" ]; then
    log "ERROR: Downloaded package is empty or failed to download."
    rm -rf "$TEMP_DIR"
    exit 1
fi

log "Extracting release package..."
unzip -q -o "$TEMP_DIR/AxarDB-debian.zip" -d "$TEMP_DIR/extracted"

SOURCE_DIR="$TEMP_DIR/extracted/debian"
if [ ! -d "$SOURCE_DIR" ]; then
    SOURCE_DIR="$TEMP_DIR/extracted"
fi

# 5. Safely sync application binaries
# PRESERVED DIRECTORIES AND FILES:
# - Data/ / data/ (Database collections and documents)
# - Bulk/ / bulk/ (JSONL Bulk store tables)
# - Views/ / views/ (User stored query scripts)
# - Triggers/ / triggers/ (User trigger event scripts)
# - backup_queries/ (Query recovery backups)
# - uploads/ / Uploads/ (Uploaded user assets)
# - *logs/ / *log/ / logs/ (All log directories)
# - appsettings.json (Host-specific configuration)
# - .version (Internal version tracking)
log "Syncing application binaries -> ${APP_DIR} (Preserving Data, Bulk, Views, Triggers, backup_queries, uploads, logs, and appsettings.json)..."

rsync -av --exclude='Data/' \
          --exclude='data/' \
          --exclude='Bulk/' \
          --exclude='bulk/' \
          --exclude='Views/' \
          --exclude='views/' \
          --exclude='Triggers/' \
          --exclude='triggers/' \
          --exclude='backup_queries/' \
          --exclude='uploads/' \
          --exclude='Uploads/' \
          --exclude='*logs/' \
          --exclude='*log/' \
          --exclude='logs/' \
          --exclude='appsettings.json' \
          --exclude='.version' \
          "$SOURCE_DIR/" "$APP_DIR/"

# 6. Set permissions and update version file
if [ -f "$APP_DIR/AxarDB" ]; then
    chmod +x "$APP_DIR/AxarDB"
fi
echo "$LATEST_TAG" > "$VERSION_FILE"

# 7. Restart AxarDB service ONLY when an update has been applied
if command -v systemctl >/dev/null 2>&1; then
    if systemctl list-unit-files "$SERVICE_NAME" >/dev/null 2>&1; then
        log "Update applied. Restarting service: ${SERVICE_NAME}..."
        systemctl restart "$SERVICE_NAME" || true

        # 8. Verify service health
        sleep 2
        if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
            log "SUCCESS: AxarDB updated to ${LATEST_TAG} and service is active."
        else
            log "WARNING: AxarDB service failed to restart or is inactive. Inspecting journalctl logs..."
            journalctl -u "$SERVICE_NAME" -n 20 --no-pager 2>/dev/null | tee -a "$LOG_FILE" || true
        fi
    else
        log "SUCCESS: AxarDB updated to ${LATEST_TAG} (Service '${SERVICE_NAME}' not registered in systemd)."
    fi
else
    log "SUCCESS: AxarDB updated to ${LATEST_TAG}."
fi

# 9. Cleanup temporary files
rm -rf "$TEMP_DIR"

log "=== AxarDB Release Update Task Finished ==="
