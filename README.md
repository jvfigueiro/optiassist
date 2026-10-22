# OptiAssist - Portable Optimization and Maintenance Assistant (Windows 10 / 11)

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(64--bit)-blue.svg)](https://microsoft.com/windows)
[![Architecture](https://img.shields.io/badge/Architecture-Native%20WPF%20(.NET%204.8)-0078D4.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GNU%20GPLv3-green.svg)](LICENSE)
[![Status](https://img.shields.io/badge/Portability-100%25%20Zero--Install-success.svg)](#-core-principles--portability)

**OptiAssist** is a native, portable, high-performance **WPF (.NET Framework 4.8 / XAML)** workbench designed specifically for computer technicians, IT support specialists, and repair workshops. It centralizes and standardizes hardware diagnostics, unattended post-formatting software installations, BleachBit-inspired system cleaning, custom command execution (Toolbox), native administrative consoles, and professional service maintenance reports into a single, cohesive, zero-bloat portable environment.

📖 **Full Technical Documentation**: Detailed architectural and usage guides for every button and module are available in the [**Docs/**](Docs/README.md) directory.

---

## 🌟 Key Highlights & Capabilities

- ⚡ **100% Portable & Zero-Install**: Runs directly from a USB flash drive, external SSD, or network share. No Electron, no WebView2, and no third-party runtime bloat (~125 KB base executable + standalone plugin DLLs).
- 🛡️ **Strict Administrator Privilege Enforcement**: Runs in 64-bit native mode (`Prefer32Bit = false`) with an elevation manifest. Verifies administrator rights on startup and alerts the user if elevation is denied, ensuring direct, unredirected access to `System32`, the Windows Registry, and WMI.
- ⚖️ **100% Clean & Legal**: Strictly free of unauthorized activation tools or illicit utilities. Engineered for legitimate corporate and workshop compliance.
- 🖥️ **Asynchronous Hardware & OS Telemetry**: Queries WMI in background threads to present CPU, RAM, GPU, Motherboard, BIOS, primary storage, serial number (with a 1-click clipboard copy button), and real-time security badges (**BitLocker**, **TPM 2.0**, **Secure Boot**) with clickable shortcuts.
- 📦 **Dual-Engine Post-Install App Deployment**: Deploys essential software packages offline from `PostInstall/` at maximum USB 3.0/SSD speed, with automatic silent fallback to Microsoft's official **Winget** repository when connected to the internet. Features a visual in-app catalog editor (`Config/autoinstall.json`) and batch updates scanner.
- 🧰 **Toolbox Command & Optimization Hub**: Replaces static optimization buttons with an extensible command execution engine supporting `cmd`, `PowerShell`, and standalone `binary` executables. Features collapsible category accordions, dynamic Category Management (add, rename, delete), live streaming of standard output/error, process termination, and 16+ pre-configured system repair presets (SFC, DISM, Chkdsk, Network Printer RPC fix, Dual Boot RTC clock, CompactOS, WinSAT, etc.).
- 🧹 **BleachBit-Inspired System Cleaner v2.0 (`SystemCleaner.dll`)**: Modular `BaseCleaner` architecture supporting non-destructive **Dry-Run (`Scan()`)** and safe **Execution (`Clean()`)**. Features `PathSanitizer` scope protection, locked-file resilience (`EBUSY` skipping), and 23 cleaning routines across 5 categories (Windows Core, Explorer Shell, Chromium multi-profile + SQLite `VACUUM;`, Windows Defender, and recursive Deep Scan for developer artifacts like `node_modules`, `venv`, and `__pycache__`).
- 📋 **Professional Maintenance Service Report (`MaintenanceReport.dll`)**: Generates standardized technical service orders with customer info, auto-captured hardware specs, and an 8-point procedure checklist. Saves reports to `Reports/`, optionally prompts to save a client copy, and supports 1-click clipboard copying.
- 📊 **Centralized Logs & Independent Floating LogWindow**: Real-time timestamped event stream with keyword search, clipboard copy, and `.txt` export. Includes an independent, floating `LogWindow` that auto-opens during command runs, synchronizes with Dark/Light themes, and can be set to open by default.
- 🧩 **Modular Non-Locking Add-ons Architecture**: Standalone DLL plugins reside in `Config/Addons/<Module>/<Module>.dll` and integrate seamlessly as native sidebar navigation pages. Assemblies are loaded directly from memory bytes, preventing disk file locking and allowing on-the-fly updates, additions, or removals via **Settings**.
- 🌐 **Dynamic Zero-Restart Localization (i18n)**: Instant in-memory switching between **English (US - `en-US`)** and **Portuguese (Brazil - `pt-BR`)** with 100% key parity across all 441 localization keys. New languages can be added simply by dropping a JSON file into `Config/Languages/`.
- 🎨 **Fluent Personalization & USB AutoRun**: High-contrast Dark and Light modes, custom hex accent color selection with Windows Color Picker and 7 curated presets, Service Provider business branding, and 1-click `autorun.inf` generation for bootable USB drives.

---

## 🚀 Application Layout & Architecture

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [Title] OptiAssist                          [Header Telemetry] Motherboard | CPU | RAM │
├───────────────┬────────────────────────────────────────────────────────────────────────┤
│ 🏠 Dashboard  │                                                                        │
│ 📦 App Manager│                                                                        │
│ 🧰 Toolbox    │                          WORKSPACE AREA                                │
│ 🧹 Cleaner    │                                                                        │
│ 📋 Report     │                  (Dynamic Tab Content / Add-on View)                   │
│ 🛠️ Win Tools  │                                                                        │
│ 📄 Logs       │                                                                        │
│ ⚙️ Settings   │                                                                        │
│ ℹ️ About      │                                                                        │
│ ───────────── │                                                                        │
│ 🌓 Dark/Light │                                                                        │
├───────────────┴────────────────────────────────────────────────────────────────────────┤
│ 🔒 BitLocker: Active  |  🛡️ TPM: 2.0  |  ⚡ Secure Boot: Active   │  Branding / Company │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

1. **Top Header Bar**:
   - Left: Prominent application branding.
   - Right: Real-time hardware telemetry:
     - Line 1: Motherboard Manufacturer and Model.
     - Line 2: Processor (CPU) Model • Total Physical RAM.
2. **Left Navigation Sidebar**:
   - Quick-access tabs: **Dashboard**, **App Manager**, **Toolbox**, **Windows Tools**, **Logs**, **Settings**, and **About**.
   - Dynamic Add-on Navigation: Installed and enabled add-ons (`SystemCleaner`, `MaintenanceReport`, etc.) seamlessly appear as first-class sidebar navigation items with native styling and icons.
   - Bottom Toggle: 1-click switch between **Light Mode** and **Dark Mode**.
3. **Workspace Area**:
   - Fluid, DPI-aware container rendering selected tools, catalog editors, and plugin views.
4. **Persistent Bottom Status Bar**:
   - Real-time hardware security posture:
     - **BitLocker**: Clickable shortcut opening Windows Device Encryption or Control Panel.
     - **TPM**: Clickable shortcut launching native TPM Management Console (`tpm.msc`).
     - **Secure Boot**: Displays active/inactive state from the Windows Registry.
   - Right Side: Custom company or technician branding configured in Settings.

---

## 🖥️ Detailed Module Breakdown

### 1. Dashboard (Hardware & System Overview)
- **Hardware Telemetry Card**: Queries `Win32_Processor`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_BaseBoard`, `Win32_BIOS`, and `Win32_LogicalDisk` asynchronously.
- **Serial Number / Service Tag**: Displays the BIOS/Chassis serial tag with a dedicated **`📋 Copy`** button for instant clipboard access.
- **Operating System Card**: Displays OS edition, release version, internal build number, architecture, and NetBIOS workstation name.
- **Common Tasks (3x3 Grid)**:
  1. **🚀 Run Post-Install**: Switches to App Manager and immediately launches automated installation of all pre-selected applications.
  2. **📦 App Manager**: Opens application and installer management.
  3. **📁 Maintenance Folder**: Opens the portable `Maintenance/` directory in Windows Explorer.
  4. **🚀 Chris Titus WinUtil**: Launches the community PowerShell debloater and optimizer (`irm https://christitus.com/win | iex`).
  5. **🧠 Memory Diagnostic**: Schedules Windows Memory Diagnostic (`mdsched.exe`).
  6. **🖥️ Device Manager**: Opens native device console (`devmgmt.msc`).
  7. **⚙️ System Properties**: Opens legacy system properties dialog (`sysdm.cpl`).
  8. **🧰 Toolbox**: Quick jump to the custom command runner and system repair hub.
  9. **⚡ Reboot to UEFI**: Reboots the machine directly into the motherboard BIOS/UEFI setup (`shutdown.exe /r /fw /t 0`).

### 2. App Manager (Automated Software Deployment)
- **Hybrid Deployment Engine**:
  - **Local Offline Engine**: Checks `PostInstall/` for offline installers (`.exe`, `.msi`, `.msixbundle`) and runs them silently without internet overhead.
  - **Winget Fallback Engine**: If an installer is missing and fallback is enabled, executes silent installation via Microsoft's official Windows Package Manager (`winget install --id <AppId> --silent --accept-package-agreements --accept-source-agreements`).
- **Visual Catalog Editor (`Config/autoinstall.json`)**:
  - Add, edit, or remove software items directly from the interface.
  - Toggle between **Local** and **Winget** installation modes.
  - Reorder items (`▲` / `▼`) to enforce strict installation sequences (e.g., runtimes before heavy applications).
  - Configure custom silent argument strings per installer.
- **Winget Updates Scanner**:
  - Detects installed applications with available upgrades via `winget upgrade`.
  - Live progress bar during downloads and installations.
  - Single-click **`🔄 Update Selected`** or **`🚀 Update All`**.
  - Integrated **`⬛ View Logs`** button to track execution output in real time.

### 3. Toolbox Hub (`Config/Addons/Toolbox/Toolbox.dll`)
- **Custom System Command Engine**:
  - Execute commands via `cmd`, `PowerShell`, or direct `binary` executables.
  - Asynchronous execution on detached worker threads; zero UI freezing.
  - Real-time line-by-line `stdout` and `stderr` streaming directly into the central Logs console and the pop-out LogWindow.
  - Accurate exit-code detection with visual success and error indicators.
  - Process tree termination (`taskkill /T /F`) to halt long-running tasks safely.
  - Robust exception handling for UAC elevation cancellation (Win32 1223) and missing binaries (Win32 2/3 / exit 9009).
- **Collapsible Category Accordions & Category Manager**:
  - Commands are dynamically grouped into expandable category sections (`Expander`).
  - Integrated Category Manager modal: Add new categories, Rename existing categories (with automatic cascading update across all member commands), and Delete categories.
- **Pre-Configured System Repair & Optimization Presets**:
  - **System Integrity**: SFC (`sfc /scannow`), DISM (`DISM /Online /Cleanup-Image /RestoreHealth`), Disk Check (`chkdsk C:`).
  - **Network & Connectivity**: Full Network Reset (`flushdns`, `winsock reset`, `int ip reset`), Network Printer RPC 0x11b/0x709 registry fix, TCP Port Listener inspection.
  - **Windows Performance & Tuning**: Dual Boot UTC Clock fix (`RealTimeIsUniversal`), CompactOS filesystem compression (`compact.exe /CompactOS:always`), WinSAT Benchmark, Microsoft Store Repair (`wsreset -i`), Visual Effects optimization, and Telemetry service disabling.
  - **Custom Scripts**: Integrated with `Config/Addons/Toolbox/Scripts/` (`tweaks_interface.ps1`, `fix_printer_rpc.reg`, `reset_network.bat`).
- **Options Drawer & Edit Mode**:
  - **➕ New Toolbox**: Modal dialog with field validation, shell picker, and execution settings.
  - **🔄 Restore Presets**: Restores factory default commands and categories.
  - **📂 Open toolbox.json**: Opens the raw configuration file in the default text editor.
  - **✏️ Enable Edit Mode**: Toggles the visibility of "Edit" buttons on command cards, locking down the interface to prevent accidental modifications or unlocking it for customization.
  - Automatic External LogWindow trigger on command execution.

### 4. System Cleaner & Optimizer (`Config/Addons/SystemCleaner/SystemCleaner.dll`)
- **BleachBit-Inspired Architecture**:
  - Object-oriented `BaseCleaner` hierarchy separating non-destructive **Dry-Run (`Scan()`)** from safe **Execution (`Clean()`)**.
  - `PathSanitizer` scope protector preventing catastrophic accidental deletions of OS roots, user profiles, or system volumes.
  - Resilient handling of locked files (`EBUSY` / `IOException`) and access-denied paths without crashing.
- **23 Cleaners across 5 Categories**:
  1. **Windows Core (`win_core`)**: User Temp (`%TEMP%`), Windows System Temp (`C:\Windows\Temp`), Windows Update cache (`SoftwareDistribution`), Memory Dumps (`*.dmp`, `MEMORY.DMP`), Windows Error Reporting (WER), Prefetch (`C:\Windows\Prefetch`), Empty Recycle Bin (`SHEmptyRecycleBin`), Windows Clipboard, DNS Resolver Cache (`DnsFlushResolverCache`), MUICache, and System Event/Setup Logs (`wevtutil cl`).
  2. **Windows Explorer & Shell (`explorer_shell`)**: Thumbnail Cache (`thumbcache_*.db`), Run Dialog MRU History, Recent Documents & Jump Lists, and Shellbags & Search History.
  3. **Chromium Browsers (`chromium`)**: Supports Google Chrome and Microsoft Edge across all profiles (`Default`, `Profile 1`, etc.). Running process check (`chrome.exe`, `msedge.exe`), Cache, Crashpad reports, Local AI Models (Gemini Nano), SQLite Database `VACUUM;` (defragmentation without data corruption), Cookies, Browsing/Download History, Autofill Form Data, Site Data (LocalStorage/IndexedDB), and Saved Sessions.
  4. **Windows Defender (`defender`)**: Scan Logs & Support Temp, Quarantine files.
  5. **Deep Scan (`deep_scan`)**: Configurable root path (defaults to `C:\Users\<user>`) and recursion depth limit. Orphaned metadata & editor swap files (`.DS_Store`, `Thumbs.db`, `*.swp`, `*.swo`, `*.tmp`), Node.js/Angular cache (`node_modules`, `.angular`), and Python bytecode/virtual environments (`__pycache__`, `venv`, `.venv`).
- **Telemetry Summary Bar**: Displays selected cleaners count, total scanned items, estimated reclaimable space, bytes freed, and skipped/locked item count.

### 5. Maintenance Service Report (`Config/Addons/MaintenanceReport/MaintenanceReport.dll`)
- **Service Provider Branding**: Headers and footers dynamically incorporate your company name (`CustomerName`) and contact information (`ContactInfo`) when Service Provider Mode is enabled.
- **Comprehensive Hardware & Service Capture**: Records Client Name, Service Order (SO) number, timestamp, auto-detected Serial Number / Service Tag, Motherboard, CPU, RAM, GPU, Storage, BIOS, OS, Secure Boot, TPM, and BitLocker states.
- **8-Point Interactive Procedure Checklist**:
  - `[X]` Disk diagnostic and SMART health check
  - `[X]` Preventive internal cleaning & thermal paste replacement
  - `[X]` Clean Windows format and reinstallation
  - `[X]` Full driver and BIOS update
  - `[X]` Essential application installation
  - `[X]` Deep temporary file cleanup
  - `[X]` System integrity repair (DISM / SFC)
  - `[X]` Network and printer setup
- **Safe Export & Distribution**:
  - Always archives reports in `Reports/<OrderNo>_<Date>.txt`.
  - Prompts whether to save an extra local copy on the client workstation (`ReportSavePath`).
  - **`📋 Copy Technical Report`** copies the full formatted report to the Windows clipboard for instant sharing via email, WhatsApp, or ticketing portals.

### 6. Windows Administrative Tools
- **17 Native Management Consoles**: Single-click access to Device Manager, User Accounts (`netplwiz`), Disk Management, Classic Control Panel, System Information (`msinfo32`), About Windows (`winver`), MSCONFIG, System Properties (`sysdm.cpl`), Network Connections (`ncpa.cpl`), Windows Features, Registry Editor, Task Manager, Resource Monitor, Memory Diagnostic, Reliability Monitor, Event Viewer, and GPEDIT Group Policy Editor (with edition detection for Windows Home).
- **Maintenance Tools Folder**: Quick shortcut to the portable `Maintenance/` directory to run portable tools like CPU-Z, CrystalDiskInfo, HWMonitor, or offline scanners.

### 7. Centralized Logs & Floating LogWindow
- **In-App Logs Tab**: Real-time timestamped event stream with text filtering, clipboard copy, export to `.txt`, clear logs, and automatic scroll-to-bottom.
- **Independent Floating `LogWindow` (`LogWindow.cs`)**:
  - Standalone pop-out window for multi-monitor setups or floating over other windows.
  - Auto-opens upon executing any Toolbox command or repair action.
  - Fully synchronized with Dark and Light themes.
  - Dedicated search filter, clear, copy, and export capabilities.
  - Configurable in Settings: **"Open external logs window by default"** (`OpenExternalLogsByDefault`).

### 8. Settings & Personalization
- **Service Provider Mode**: Custom technician branding, contact info, and toggle to show/hide the Maintenance Report tab.
- **Dynamic Localization Engine (i18n)**:
  - English (US - `en-US`) and Portuguese (Brazil - `pt-BR`) dictionaries in `Config/Languages/`.
  - 100% key parity (441 keys matched with 0 diff).
  - Instant zero-restart translation across every element in memory.
  - Extensible: drop any `<locale>.json` (e.g. `es-ES.json`) into the folder to add language support instantly.
- **Fluent Styling & Accent Colors**:
  - Dark Mode (`#0F172A`) & Light Mode (`#FFFFFF` / `#F8FAFC`).
  - Custom Hex color code input or Windows Color Dialog picker.
  - 7 quick preset colors: Blue (Default), Green, Purple, Orange, Red, Yellow, and Pink.
- **Portable USB AutoRun**: Generates or updates `autorun.inf` at the drive root with relative application path and icon while preserving existing volume metadata.
- **Add-ons Manager**: View installed `.dll` plugins, enable/disable them on the fly, import new plugin DLLs, or remove modules.

---

## 📁 Portable Directory Structure

```text
📁 OptiAssist/
│
├── OptiAssist.exe                   <-- Standalone 64-bit executable (~125 KB)
├── OptiAssist.csproj                <-- MSBuild project file for main executable
├── build.bat                        <-- 1-click compiler script (Main app + All Add-ons)
├── README.md                        <-- Master overview and documentation
│
├── 📁 Config/                       <-- Portable configuration & modules
│   ├── settings.json                <-- User preferences, branding, themes, active add-ons
│   ├── autoinstall.json             <-- Automated software installation catalog (Local/Winget)
│   ├── 📁 Languages/                <-- Dynamic i18n translation dictionaries
│   │   ├── en-US.json               <-- English (United States) [441 keys]
│   │   └── pt-BR.json               <-- Português (Brasil) [441 keys]
│   └── 📁 Addons/                   <-- Standalone compiled plugin assemblies
│       ├── README.md                <-- Add-ons developer guide
│       ├── 📁 SystemCleaner/
│       │   └── SystemCleaner.dll    <-- BleachBit-style system cleaner & optimizer
│       ├── 📁 MaintenanceReport/
│       │   └── MaintenanceReport.dll<-- Technical service order & report generator
│       └── 📁 Toolbox/
│           ├── Toolbox.dll          <-- Custom command runner & optimization hub
│           ├── toolbox.json         <-- User commands & repair presets database
│           └── 📁 Scripts/          <-- Automation helper scripts (.ps1, .reg, .bat)
│               ├── tweaks_interface.ps1
│               ├── fix_printer_rpc.reg
│               └── reset_network.bat
│
├── 📁 PostInstall/                  <-- Offline software installers (.exe, .msi, .msix)
│   └── README.txt
│
├── 📁 Maintenance/                  <-- Portable third-party tools (CPU-Z, CrystalDiskInfo)
│   └── README.txt
│
├── 📁 Reports/                      <-- Generated maintenance service reports (.txt)
│   └── README.txt
│
├── 📁 Docs/                         <-- In-depth technical documentation chapters
│   ├── README.md                    <-- Documentation index & principles
│   ├── 01-DASHBOARD.md              <-- Hardware telemetry, WMI queries, security badges
│   ├── 02-APP-MANAGER.md            <-- Offline installer deployment, Winget engine
│   ├── 03-TOOLBOX.md                <-- Command runner, categories, repair presets
│   ├── 04-WINDOWS-TOOLS.md          <-- 17 administrative consoles, GPEDIT handling
│   ├── 05-MAINTENANCE-REPORT.md     <-- Service orders, checklist, report exports
│   ├── 06-SETTINGS-AND-LOCALIZATION.md <-- Dynamic i18n, themes, logs, USB AutoRun
│   └── 07-ADDONS-AND-MODULES.md     <-- Plugin architecture, BaseCleaner, specs
│
└── 📁 src/                          <-- Full modular source code (.NET 4.8 / WPF)
    ├── App.xaml / App.xaml.cs       <-- Application entry, elevation check, themes
    ├── MainWindow.xaml / .cs        <-- Main UI grid, navigation, status bar
    ├── LogWindow.cs                 <-- Pop-out independent floating log window
    ├── AddonHelper.cs               <-- Non-locking assembly loader & contracts
    ├── SystemInfoHelper.cs          <-- WMI hardware, OS, and security queries
    ├── InstallerHelper.cs           <-- Offline installer & Winget upgrade runner
    ├── ShortcutsHelper.cs           <-- Windows administrative tool launchers
    ├── SettingsHelper.cs            <-- settings.json serialization & persistence
    ├── LocalizationHelper.cs        <-- Dynamic i18n JSON translation engine
    └── 📁 Addons/                   <-- Standalone add-on project sources
        ├── 📁 SystemCleaner/        <-- SystemCleaner.csproj & BaseCleaner classes
        ├── 📁 MaintenanceReport/    <-- MaintenanceReport.csproj & ReportAddon
        └── 📁 Toolbox/              <-- Toolbox.csproj, Storage, Runner, Addon
```

---

## 🛠️ Building from Source

OptiAssist compiles using standard Microsoft Build Engine (**MSBuild**) included with Windows and the .NET Framework 4.8 SDK. No external package restore or internet connection is required:

1. Open a Command Prompt or PowerShell terminal inside the repository root.
2. Run the automated build script:
   ```cmd
   build.bat
   ```
3. The script compiles:
   - `OptiAssist.exe` (main application)
   - `Config/Addons/SystemCleaner/SystemCleaner.dll`
   - `Config/Addons/MaintenanceReport/MaintenanceReport.dll`
   - `Config/Addons/Toolbox/Toolbox.dll`

---

## 📄 License & Attribution

- **License**: GNU General Public License v3.0 ([GNU GPLv3](LICENSE)).
- **Author**: João Figueiró
- **Copyright**: Copyright (C) 2026 João Figueiró.
- **Third-Party Inspiration**: BleachBit (cleaning engine architecture), Chris Titus WinUtil (community PowerShell optimization script).
