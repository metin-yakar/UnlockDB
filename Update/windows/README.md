# AxarDB Windows Update Suite

Automated and safe update scripts for AxarDB instances running on Windows servers.

---

## 🔒 Data Protection Guarantee

During updates, the update scripts **never** touch, overwrite, or delete your existing database data:
- `Data/` / `data/` (all collections and index files)
- `Bulk/` / `bulk/` (all JSONL static storage files)
- `Views/` / `views/` (stored server-side queries)
- `Triggers/` / `triggers/` (configured event triggers)
- `backup_queries/` (fail-safe recovery logs)
- `uploads/` / `Uploads/` (uploaded user assets)
- `*logs*` (all system, request, and error logs)
- `appsettings.json` (user modified configuration)

Only application binaries (`.exe`, `.dll`, runtime files) and static assets (`wwwroot/`, `Docs/`) are updated.

---

## 🚀 Quick Start

### 1. Manual Update via PowerShell & Auto-Task Setup
Running `update.ps1` downloads the latest precompiled release bundle (`AxarDB-windows.zip`), safely extracts and copies binaries, and automatically configures a daily scheduled task at 03:00 when run as Administrator:
```powershell
# Run update from the windows directory
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1

# Check if an update is available without applying it
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1 -CheckOnly

# Force update even if version is current
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1 -Force

# Specify custom installation path and service name
powershell.exe -ExecutionPolicy Bypass -File .\update.ps1 -InstallDir "C:\AxarDB" -ServiceName "AxarDB"
```

### 2. Manual Update via Batch Launcher
```cmd
update.bat
```

### 3. Manual Schedule Configuration via Task Scheduler
Run PowerShell as Administrator:
```powershell
# Schedule daily check at 03:00 AM
.\schedule-task.ps1 -DailyTime "03:00"

# Schedule daily check for custom directory
.\schedule-task.ps1 -DailyTime "04:30" -InstallDir "C:\AxarDB" -ServiceName "AxarDB"

# Remove scheduled task
.\schedule-task.ps1 -Remove
```

---

## ⚙️ Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-InstallDir` | String | Auto-detected | Path to the AxarDB installation folder |
| `-ServiceName` | String | `AxarDB` | Name of the Windows Service |
| `-CheckOnly` | Switch | `False` | Checks version availability without downloading |
| `-Force` | Switch | `False` | Forces reinstall even if version matches |
| `-DailyTime` | String | `03:00` | Scheduled time in 24-hour format (for `schedule-task.ps1`) |
