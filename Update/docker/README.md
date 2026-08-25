# AxarDB Docker Update Suite

Automated and safe update utilities for AxarDB instances running in Docker containers.

---

## 🔒 Data Protection Guarantee

AxarDB Docker instances use volume mounts (e.g. `-v ./data:/app/data` or named volumes) to persist all database state on the host:
- `data/` (all collection documents and index files)
- `Logging/` (all log files)

When updates occur, containers are gracefully recreated using the new image **without removing mounted volumes**. Your data and configuration remain 100% intact.

---

## 🚀 Quick Start

### 1. Manual Update on Linux/macOS
```bash
chmod +x ./update.sh

# Standalone container update (Default container: AxarDB)
./update.sh

# Custom container name or image
./update.sh --container MyAxarDB --image metinyakar/axardb:latest

# Docker Compose update
./update.sh --file /path/to/docker-compose.yml

# Check only without restarting
./update.sh --check
```

### 2. Manual Update on Windows (PowerShell)
```powershell
# Standalone container update
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1

# Docker Compose update
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1 -ComposeFile "..\docker-compose.yml"
```

### 3. Schedule Daily Automatic Update Check

#### Linux (Cron)
```bash
chmod +x ./schedule-cron.sh

# Schedule daily check at 03:00 AM
sudo ./schedule-cron.sh --time 03:00 --container AxarDB

# Schedule for Docker Compose
sudo ./schedule-cron.sh --time 03:00 --file /path/to/docker-compose.yml

# Remove scheduled cron
sudo ./schedule-cron.sh --remove
```

#### Windows (Task Scheduler)
Run PowerShell as Administrator:
```powershell
# Schedule daily check at 03:00 AM
.\schedule-task.ps1 -DailyTime "03:00" -ContainerName "AxarDB"

# Remove scheduled task
.\schedule-task.ps1 -Remove
```

---

## ⚙️ Options Reference

| Option | Flag | Default | Description |
| :--- | :--- | :--- | :--- |
| Container Name | `-c, --container` | `AxarDB` | Docker container name |
| Image Name | `-i, --image` | Auto-detected / `axardb:latest` | Docker image tag |
| Compose File | `-f, --file` | Auto-detected `docker-compose.yml` | Path to docker-compose file |
| Check Only | `-k, --check` | `false` | Check if new image exists without restarting |
| Force Update | `-F, --force` | `false` | Recreates container even if image hash is unchanged |
| Scheduled Time | `-t, --time` | `03:00` | Daily execution time for cron/scheduler |
