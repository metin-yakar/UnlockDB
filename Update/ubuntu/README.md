# AxarDB Ubuntu Update Suite

Automated and safe update utilities for AxarDB instances on Ubuntu servers.

---

## 🔒 Data Protection Guarantee

During updates, the update script strictly protects and preserves your existing database files:
- `Data/` / `data/` (all collection documents and index files)
- `Bulk/` / `bulk/` (all JSONL static storage files)
- `Views/` / `views/` (stored server-side queries)
- `Triggers/` / `triggers/` (configured event triggers)
- `backup_queries/` (fail-safe recovery logs)
- `uploads/` / `Uploads/` (uploaded user assets)
- `*logs*` (all system, request, and error logs)
- `appsettings.json` (user modified configuration)

Only application binaries and web assets (`wwwroot/`, `Docs/`) are updated.

---

## 🚀 Quick Start

### 1. Manual Update & Auto-Timer Setup
Running `update.sh` downloads the latest precompiled release bundle (`AxarDB-debian.zip`), safely syncs files with `rsync`, and automatically registers a daily systemd timer (`axardb-update.timer` at 03:00):
```bash
# Make executable and run
chmod +x ./update.sh
sudo ./update.sh

# Check if an update is available without applying it
./update.sh --check

# Force update even if version is current
sudo ./update.sh --force

# Custom installation directory and service name
sudo ./update.sh --dir /opt/axardb --service axardb.service
```

### 2. Manual Schedule Configuration (Cron or Systemd Timer)
```bash
chmod +x ./schedule-cron.sh

# Option A: Install daily cron job at 03:00 AM
sudo ./schedule-cron.sh --time 03:00 --method cron --dir /opt/axardb

# Option B: Install and enable systemd timer
sudo ./schedule-cron.sh --time 03:00 --method timer --dir /opt/axardb

# Remove scheduled task
sudo ./schedule-cron.sh --remove
```

### 3. Log Output
All automated update attempts are logged to:
```bash
tail -f /var/log/axardb-update.log
```

---

## ⚙️ Options Reference

| Option | Flag | Default | Description |
| :--- | :--- | :--- | :--- |
| Installation Directory | `-d, --dir` | Auto-detected / `/opt/axardb` | Directory containing AxarDB installation |
| Systemd Service | `-s, --service` | `axardb.service` | Systemd service unit name |
| Check Only | `-c, --check` | `false` | Checks version availability without downloading |
| Force Update | `-f, --force` | `false` | Reinstalls binaries even if version is up-to-date |
| Execution Time | `-t, --time` | `03:00` | Daily execution time for cron/timer in 24h format |
| Scheduling Method | `-m, --method`| `cron` | Method: `cron` or `timer` |
