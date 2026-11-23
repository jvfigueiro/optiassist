# OptiAssist Module Documentation Index (`v1.2.S7`)

This directory contains technical and operational documentation for every core section and modular add-on in OptiAssist (`1.2.0.7` / `1.2.S7`).

---

## Core Host Modules (`OptiAssist.exe`)

| Module | Summary | Documentation |
| :--- | :--- | :---: |
| **Dashboard** | Health score ring (%), integrated diagnostic recommendations, 2x2 Bento hardware/OS/storage telemetry, and customizable Common Tasks | [dashboard.md](dashboard.md) |
| **Windows Tools** | Native administrative consoles, 7-profile **OptiBoost** engine with snapshots/rollback, and **Compatibility Checker** | [windows-tools.md](windows-tools.md) |
| **Terminal & Logs** | Native external Windows Terminal (`wt.exe`) / PowerShell / CMD log streamer and floating `LogWindow` | [logs-terminal.md](logs-terminal.md) |
| **Settings & Deployment** | Light/Dark theme, accent color, custom logo, i18n hot-reload, Add-on manager, and **OptiAssist Installer** (PC / Portable Mode) | [settings-deployment.md](settings-deployment.md) |

---

## Modular Add-on DLLs (`Config\Addons\<Module>\`)

| Add-on ID | Display Name | Summary | Documentation |
| :--- | :--- | :--- | :---: |
| `toolbox` | **Toolbox** | Script & command runner (PowerShell, CMD, executables), repair presets, and WinUtil launcher | [toolbox.md](toolbox.md) |
| `app-manager` | **App Manager** | 3-tab software suite: **Batch Installer**, **App Updates** (Winget + Store), and **Manage Apps** | [app-manager.md](app-manager.md) |
| `system-cleaner` | **System Cleaner** | BleachBit CleanerML XML dry-run scanner/cleaner, `PathSanitizer`, SQLite vacuum, and backup manager | [system-cleaner.md](system-cleaner.md) |
| `startup-manager` | **Startup Manager** | Registry `Run`/`RunOnce` + `StartupApproved`, Startup folders, and COM Task Scheduler inspector | [startup-manager.md](startup-manager.md) |
| `hardware-monitor` | **Hardware Monitor** | System summary, `LibreHardwareMonitorLib` **Sensors (Beta)** + PawnIO, Battery, Bench, and Device Inventory | [hardware-monitor.md](hardware-monitor.md) |
| `maintenance-report` | **Maintenance Report** | Customer service order & 8-step checklist generator with `.txt` export | [maintenance-report.md](maintenance-report.md) |
