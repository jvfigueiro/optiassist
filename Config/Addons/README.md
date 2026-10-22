# 🧩 OptiAssist Add-ons & Modular DLL Plugins (`Config/Addons`)

This directory stores modular `.dll` plugins and extensions for **OptiAssist**.

---

## 1. How It Works

Each module lives in its own subdirectory inside `Config/Addons/<ModuleName>/` and consists of a compiled .NET library (`<ModuleName>.dll`) that implements the `OptiAssist.IOptiAssistAddon` interface:

```text
Config/
└── Addons/
    ├── SystemCleaner/
    │   └── SystemCleaner.dll
    ├── MaintenanceReport/
    │   └── MaintenanceReport.dll
    └── Toolbox/
        ├── Toolbox.dll
        ├── toolbox.json
        └── Scripts/
            ├── tweaks_interface.ps1
            ├── fix_printer_rpc.reg
            └── reset_network.bat
```

### Dynamic Discovery & Non-Locking Load
1. When OptiAssist launches (or when clicking **Reload Modules** in **Settings**), `OptiAssist.exe` scans `Config/Addons/` for all `.dll` assemblies.
2. It reads each assembly into memory using `Assembly.Load(File.ReadAllBytes(dllPath))` and resolves references back to `OptiAssist.exe` via `AppDomain.CurrentDomain.AssemblyResolve`.
3. Because the DLL is loaded directly from memory bytes, the file on disk is **never locked**. Users can overwrite, update, or remove modules while OptiAssist is running.

### First-Class Sidebar Navigation
Every enabled module automatically renders as a native navigation tab in the left sidebar (`pnlSidebarAddons`). Clicking a module dynamically displays its complete WPF view (`CreateView()`) in the main workspace.

### Settings Management
In **Settings → Modular Add-ons & Plugins**, technicians can:
- **➕ Add Module (.dll)**: Import an external `.dll` plugin; OptiAssist copies it into `Config/Addons/<ModuleName>/` and registers it immediately.
- **Active Checkbox**: Toggle whether a module is visible in the sidebar. Disabled module IDs persist in `Config/settings.json` under `DisabledAddons`.
- **🗑️ Remove**: Delete a module's DLL and remove it from the sidebar navigation.

---

## 2. Built-in Official Modules

### 🧹 System Cleaner (`Config/Addons/SystemCleaner/SystemCleaner.dll`)
Full-featured system cleaning and optimization engine inspired by **BleachBit**.
- **Architecture**: Extensible `BaseCleaner` object-oriented hierarchy.
- **Modes**: 100% non-destructive Dry-Run (`Scan()`) and safe Execution (`Clean()`).
- **Safety**: Strict `PathSanitizer` scope protector preventing accidental OS or user profile deletions; resilient skipping of locked files (`EBUSY` / `IOException`).
- **Scope**: 23 cleaners across 5 categories (Windows Core, Explorer Shell, Chromium multi-profile + SQLite `VACUUM;`, Windows Defender, and recursive Deep Scan for developer artifacts).

### 📋 Maintenance Report (`Config/Addons/MaintenanceReport/MaintenanceReport.dll`)
Professional service order and technical maintenance report generator.
- **Branding**: Integrates with Service Provider Mode (`CustomerName` and `ContactInfo`).
- **Data Capture**: Automatic hardware extraction, Client Name, Service Order number, and serial tag.
- **Checklist**: 8-point interactive maintenance procedure checklist.
- **Export**: Generates timestamped `.txt` files archived in `Reports/`, prompts for optional client PC copy, and supports 1-click clipboard export.

### 🧰 Toolbox (`Config/Addons/Toolbox/Toolbox.dll`)
Custom system command runner and unified optimization hub.
- **Command Engine**: Asynchronous execution of `cmd`, `PowerShell`, and direct `binary` executables with live line-by-line `stdout`/`stderr` streaming.
- **Process Management**: Safe process tree termination (`taskkill /T /F`) and exit-code reporting.
- **Categories**: Dynamic collapsible accordion cards with an in-app Category Manager (Add, Rename with cascading updates, Delete).
- **Optimization Presets**: Pre-loaded with 16+ essential system repair and tuning presets (SFC, DISM, Chkdsk, Network Reset, Network Printer RPC fix, Dual Boot RTC clock, CompactOS, WinSAT, etc.) replacing the legacy Optimization tab.
- **Options & Edit Mode**: Options drawer with New Toolbox, Restore Presets, Open JSON, Category Manager, and an Edit Mode toggle to lock or unlock editing capabilities.

---

## 3. Official Module Assembly Metadata

All official OptiAssist modules are compiled with standard assembly metadata:
- **Company**: João Figueiró
- **Product**: OptiAssist Modules
- **Copyright**: Copyright (C) 2026 João Figueiró. Licensed under GNU GPLv3.
- **Version**: 1.1.0.0
