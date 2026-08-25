# AxarDB Multi-Platform Update Suite

Automated, secure, and zero-data-loss update scripts for **AxarDB** across popular server environments.

---

## 📁 Directory Overview

| Directory | Environment | Description |
| :--- | :--- | :--- |
| [windows/](windows/README.md) | Windows Server / Windows 10/11 | PowerShell & Batch update scripts + Windows Task Scheduler automation. |
| [debian/](debian/README.md) | Debian GNU/Linux | Bash update scripts + systemd service & timer + cron automation. |
| [ubuntu/](ubuntu/README.md) | Ubuntu Server / Desktop | Bash update scripts + systemd service & timer + cron automation. |
| [docker/](docker/README.md) | Docker & Docker Compose | Container & Compose update scripts + host cron / task scheduler. |

---

## 🛡️ Critical Data Preservation Guarantee

AxarDB update scripts are built with strict safeguards to ensure that **database data, configuration, and logs are never deleted or overwritten during updates**:

1. **Database Documents & Collections (`Data/`, `data/`)**: All JSON collections and indexes (`idx_*.json`) remain intact.
2. **Bulk Storage (`Bulk/`, `bulk/`)**: High-performance JSONL static datasets are strictly preserved.
3. **Stored Views & Triggers (`Views/`, `views/`, `Triggers/`, `triggers/`)**: All server-side JavaScript queries and event triggers remain unchanged.
4. **Data Recovery Logs (`backup_queries/`)**: Fail-safe reverse queries are kept safe.
5. **Uploaded Assets (`uploads/`, `Uploads/`)**: All uploaded user files and assets remain untouched.
6. **System & Access Logs (`*logs/`, `*log/`, `logs/`)**: Request logs, error logs, debug logs, and audit logs are preserved.
7. **Configuration (`appsettings.json`)**: Host-specific configuration is never overwritten.
8. **Version File (`.version`)**: Internal version tracker is updated upon successful deployment.
9. **Docker Storage**: Host volume mounts (e.g. `./data:/app/data`) and named volumes persist across container recreations.

Only runtime binaries, assemblies, and public static assets (`wwwroot/`, `Docs/`) are updated.

---

## 🔄 Version Check Mechanism

Before initiating any download or restarting services, the scripts inspect the current local version and query the official release channels:

- **Standalone Binaries (Windows, Debian, Ubuntu)**:
  - Release Source: [AxarDB GitHub Releases](https://github.com/metin-yakar/AxarDB/releases)
  - Packages: Direct download of precompiled release archives (`AxarDB-debian.zip`, `AxarDB-windows.zip`).
  - If local version equals latest remote tag, the script exits immediately with zero downtime and does not restart the service.
- **Docker**:
  - Pulls image manifests from Docker Hub.
  - If the image digest has not changed, running containers are left untouched.

---

## ⏰ Daily Scheduled Updates

Each environment includes automated scheduling to run update checks once a day at a user-defined time (default: `03:00 AM`):

- **Debian / Ubuntu**: Running `update.sh` auto-registers `axardb-update.timer` (03:00) and `axardb-update.service`. `schedule-cron.sh` is also available for cron or timer configuration.
- **Windows**: Running `update.ps1` as Administrator auto-registers the `AxarDB-DailyUpdate` task at 03:00. `schedule-task.ps1` is also available for custom schedules.
- **Docker Host**: `schedule-cron.sh` (Linux) or `schedule-task.ps1` (Windows).

---

## 📖 Platform Quick Guides

- 🪟 **[Windows Guide](windows/README.md)**
- 🌀 **[Debian Guide](debian/README.md)**
- 🐧 **[Ubuntu Guide](ubuntu/README.md)**
- 🐳 **[Docker Guide](docker/README.md)**
