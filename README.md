# OptiAssist - Portable Optimization and Maintenance Assistant (Windows 10 / 11)

**OptiAssist** is a native portable **WPF (.NET Framework 4.8 / XAML)** application designed for computer technicians, IT support professionals, and maintenance workshops. Its purpose is to streamline and standardize post-formatting tasks, quick hardware diagnostics, system optimization, network/printer repairs, and the generation of technical maintenance reports.

📖 **Full Documentation**: Comprehensive technical guides detailing every button and feature are available in the [**Docs/**](Docs/README.md) directory.

---

## 🚀 Architecture and Layout Structure

The user interface was built following a modern, responsive layout that eliminates manual fixed positioning and floating margins:

1. **Main Window**:
   - Title: **`OptiAssist`**
   - `Width="1000"`, `Height="620"`, `MinWidth="960"`, `MinHeight="560"`.
   - `WindowStartupLocation="CenterScreen"`.
   - Compatible with DPI Scaling (100%, 125%, 150%) and standard display resolutions (1366x768, 1080p, 2K, 4K).
   - Compiled in native 64-bit mode (`Prefer32Bit = false`), ensuring direct access to `System32` utilities without SysWOW64 redirection.
   - Text elements use `TextTrimming="CharacterEllipsis"` to prevent visual clipping.

2. **Main Window Grid (3 Rows)**:
   - **Row 0 (Top Header Bar)**: `Height="Auto" MinHeight="52"`:
     - **Top Left**: Prominent application title **`OptiAssist`**.
     - **Top Right**: Clean, essential hardware specifications:
       - **Line 1**: `[Motherboard Manufacturer and Model]`
       - **Line 2**: `[Processor] • [Total RAM Memory]`
   - **Row 1 (Workspace)**: Dynamic `Height="*"`:
     - **Column 0 (Sidebar)**: Fixed side navigation (`Width="200"`) with 7 direct navigation options and a **bottom toggle for Light Mode / Dark Mode**.
     - **Column 1 (Content Area)**: Wide full-screen area for dynamic tab content.
   - **Row 2 (Bottom Status Bar)**: Fixed `Height="28"` at the window base, persistent across all tabs:
     - **Left Side**: `BitLocker | TPM | Secure Boot` with dynamic color badges.
     - **Right Side**: Custom branding: **`Custom Application Name`** (configured in Settings).

3. **Persistent Bottom Status Bar**:
   - Real-time display: **BitLocker | TPM | Secure Boot**.
   - **Dynamic Color Indicators**:
     - **Green**: Feature active / available (e.g. `Active`, `Available (2.0)`).
     - **Red**: Feature inactive / disabled (e.g. `Inactive`).
     - **Gray / Neutral**: `Unavailable / Not supported` when the feature is not present or not supported by the hardware or Windows edition.

---

## 🖥️ Modules and Features

- **Dashboard** (**Welcome to OptiAssist!**):
   - Subtitle: *The ultimate tool to keep your system in order*.
   - **2-Block Grid Layout per Row**:
     - **Hardware Information (Left Block)**: Specifications laid out strictly one item per line:
       1. Processor (CPU name and core/thread count)
       2. RAM Memory (total and available)
       3. Graphics Card (GPU)
       4. Motherboard (manufacturer and model)
       5. BIOS (version, vendor, and release date)
       6. Storage (primary drive and free space)
       7. **Equipment Serial Number** with a dedicated **`📋 Copy`** button.
     - **Operating System (Right Block)**: Windows edition, version, OS build, architecture, and **Device Name** aligned at the exact same height as the Serial Number in the adjacent block.
   - **Common Tasks (Bottom Block)**: Quick grid with 8 one-click essential shortcuts (App Manager, Maintenance Folder, Chris Titus WinUtil, Massgrave MAS, Memory Diagnostic, Device Manager, System Properties, and Reboot to UEFI).
- **App Manager**:
   - **Application Updates (Always Visible)**: Permanent top panel with update scanning via Winget. Displays a real-time progress bar during updates and a centered **`⬛ View Logs`** button to jump directly to the Logs tab.
     - **Update Actions**: **`🚀 Update All`** on the left (default style) and **`🔄 Update Selected`** highlighted on the right with the accent color (`BrushAccent`).
   - **Automated Application Installation (Integrated Card)**: Program list organized inside a styled container card, featuring footer actions: **`Select Essentials`**, **`Deselect All`**, **`⚙️ Customize Automated Installation`**, **`📂 Installers Folder`**, and **`🚀 Install Selected`**.
   - **Customize Automated Application Installation (Config\autoinstall.json)**:
     - Manage installable software directly from the interface:
       1. **Program Name**: Visual display name in the list.
       2. **Installation Type**: Dynamic toggle between **`Local`** (with file picker for `.exe`/`.msi` installers in `PostInstall\`) and **`Winget`** (with official package ID field).
       3. **Automate**: Checkbox to designate whether the program is pre-selected by default.
       4. **Per-Item List Actions**: Move up (`▲`), Move down (`▼`), Edit item (`✏️`), and Remove (`🗑️`).
       5. Optional silent arguments for custom installers.
   - **Intelligent Fallback**: If `Local` mode is chosen but the installer is not found, it automatically falls back to Winget when enabled.
- **Optimization**:
   - **Community Tools**: Direct access to community utilities: **Chris Titus Tool (WinUtil)** and **Massgrave Activation (MAS)**.
   - **System Integrity & Repairs (Left Column)**: Dedicated repair utilities:
     - **Check & Repair Windows Integrity via SFC** (`sfc /scannow`)
     - **Check & Repair Windows Integrity via DISM** (`Dism /Online /Cleanup-Image /RestoreHealth`)
     - **Check Disk Integrity (CHKDSK C:)**
     - **Fix Network Printer RPC Issue** (RPC 0x11b)
     - **Fix System Clock for Dual Boot**
     - **Install or Repair Microsoft Store** (`wsreset -i`)
     - **Network Reset**
     - **WinSAT Benchmark** with immediate display of CPU, RAM, Disk, and Graphics scores.
   - **Cleanup & Performance (Right Column)**:
     - **Clean Temporary Files** (`%TEMP%`, Prefetch, WER, and Recycle Bin)
     - **Clean Windows Update Cache**
     - **Optimize Visual Effects**
     - **Disable Telemetry & Optimize System Services**
     - **Compact Windows Installation** (`compact.exe /CompactOS:always`)
     - **Open Scripts Folder**
- **Windows Tools**:
   - **Administrative Tools**: Centralized 3-column grid for: GPEDIT Tool (Pro/Enterprise), Memory Diagnostic, Reliability Monitor, Event Viewer, User Accounts (Netplwiz), MSCONFIG Tool, Device Manager, Disk Management, Classic Control Panel, System Information, About Windows, Network Connections, Windows Features, Registry Editor, Task Manager, and Resource Monitor.
   - **Maintenance Tools Folder**: Direct button located immediately below Administrative Tools to open the `Maintenance\` directory.
- **Maintenance Report**:
   - **Safe Generation Flow**: Reports are **always generated and archived in the application's `Reports\` directory**. The application then prompts whether to save an extra local copy on the user's computer (at the path defined in Settings).
   - **Standardized Technical Layout**:
     - Header: `{CustomerName}` and `{ContactInfo}` followed by `Maintenance Report`.
     - **Service Details**: Client name, Order Number, and Date/Time of execution.
     - **Device Specs**: Device Name, BIOS, Operating System, Motherboard, CPU, RAM Memory, GPU, Main Storage, Secure Boot, TPM, BitLocker, and Serial Number.
     - **Services and Procedures Performed**: Selected checklist items formatted as `[X] Procedure`.
     - **Observations**: Technical assessment notes or `"Nothing Reported"` when empty.
     - Footer: `Report generated via OptiAssist`.
- **Settings & Personalization**:
   - **Service Provider Mode**:
     - Checkbox to toggle professional technician mode.
     - When enabled, exposes customization fields:
       1. **Custom Application Name** (with tooltip: *"Customize the application according to the name you prefer."*).
       2. **Contact Information** (with tooltip: *"Provide an email, phone number, or social media link of your choice"*).
       3. **Enable Maintenance Report**: Checkbox to show or hide the "Maintenance Report" tab in the sidebar.
   - **Dynamic Multi-Language Support (i18n)**:
     - Native support for **English (US - `en-US`)** and **Portuguese (Brazil - `pt-BR`)**, selectable with 1 click in Settings.
     - **Real-Time Translation**: The entire application updates in memory immediately without requiring a restart.
     - **Fully Scalable**: All strings reside in flat JSON files inside `Config\Languages\` (`en-US.json`, `pt-BR.json`). To add a new language (e.g. Spanish `es-ES.json`), simply place the corresponding file in the folder without recompilation.
   - **General Preferences**: Default Report Path (`C:\MaintenanceReport.txt`), selected language, and Winget fallback settings.
   - **Restore Defaults**: One-click button to restore all settings to default values.
   - **Theme & Accent Customization**:
     - **Theme Switching (Light Mode & Dark Mode)**: 100% consistent across all screens via DynamicResources.
     - Button and accent color selection via **Hexadecimal color code** or **Windows Color Dialog Picker**.
     - **Suggested Color Presets**: Blue (Default), Light Green, Purple, Orange, Red, Yellow, and Pink.
     - Live dynamic preview in real time.
- **About**:
   - Dedicated screen accessible via sidebar.
   - Full version details (`OptiAssist v1.0.0`), copyright (`Copyright (C) 2026 João Figueiró`), and **GNU General Public License v3 (GPLv3)** terms.
   - Quick access links: **GitHub Repository** and **Official Website**.

---

## 📁 Folder Structure

```text
📁 OptiAssist/
│
├── OptiAssist.exe             <-- Compiled portable executable (~125 KB)
├── OptiAssist.csproj          <-- MSBuild project file for WPF compilation
├── build.bat                  <-- 1-click fast build script
├── README.md                  <-- Overview and quick reference
├── 📁 Docs/                   <-- Comprehensive technical documentation chapters
│   ├── README.md              (Documentation index and principles)
│   ├── 01-DASHBOARD.md        (Hardware, WMI discovery, serial number, security)
│   ├── 02-APP-MANAGER.md      (Post-install automation, offline & Winget engines)
│   ├── 03-OPTIMIZATION.md     (Deep dive into all 16 repair & cleanup tweaks)
│   ├── 04-WINDOWS-TOOLS.md    (Native administrative consoles and portable tools)
│   ├── 05-MAINTENANCE-REPORT.md (Service Provider Mode and technical reports)
│   └── 06-SETTINGS-AND-LOCALIZATION.md (Dynamic i18n, themes, and persistence)
│
├── 📁 Config/
│   ├── settings.json          <-- App preferences, contact info, modules, and accent color
│   ├── autoinstall.json       <-- Customizable post-install software catalog (Local/Winget)
│   └── 📁 Languages/          <-- Multi-language translation dictionaries
│       ├── en-US.json         <-- English (US)
│       └── pt-BR.json         <-- Portuguese (Brazil)
│
├── 📁 PostInstall/            <-- Offline program setups
│   ├── README.txt
│   ├── ChromeSetup.exe
│   ├── 7ZipSetup.exe
│   └── OfficeSetup.exe
│
├── 📁 Maintenance/            <-- Portable diagnostic and repair utilities
│   └── README.txt
│
├── 📁 Scripts/                <-- Custom automation scripts (.ps1, .bat, .reg)
│   ├── tweaks_interface.ps1   (UI animation and responsiveness optimizations)
│   ├── fix_printer_rpc.reg    (Network printer 0x0000011b error fix)
│   └── reset_network.bat      (Winsock, TCP/IP, and DNS flush reset)
│
├── 📁 Reports/                <-- Generated maintenance reports and service order history
│   └── README.txt
│
└── 📁 src/                    <-- Full modular WPF source code
    ├── app.ico                (Multi-resolution application icon)
    ├── app.png                (High-definition logo asset)
    ├── app.manifest           (Administrator privileges and DPI awareness manifest)
    ├── App.xaml               (XAML styles, themes, and dynamic dark Fluent resources)
    ├── App.xaml.cs            (Application startup and global exception handling)
    ├── MainWindow.xaml        (Rigid grid layout of the primary window)
    ├── MainWindow.xaml.cs     (UI logic, control handlers, and color picker)
    ├── SystemInfoHelper.cs    (WMI queries: OS, hardware, serial number, BIOS, WinSAT)
    ├── InstallerHelper.cs     (Offline installation manager and Winget engine)
    ├── TweakHelper.cs         (Cleanup routines, DISM, SFC, Chkdsk, and system fixes)
    ├── ShortcutsHelper.cs     (Direct shortcuts to native and portable tools)
    ├── ReportHelper.cs        (Technical maintenance report generator and exporter)
    ├── SettingsHelper.cs      (Reader and writer for settings.json)
    └── LocalizationHelper.cs  (i18n internationalization manager and JSON parser)
```

---

## 🛠️ How to Rebuild

To modify the XAML or C# code in the `src\` folder, run:
👉 **`build.bat`**

The standard Windows MSBuild compiler will build `OptiAssist.csproj`, producing the single standalone executable `OptiAssist.exe`.
