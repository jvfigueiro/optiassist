# OptiAssist

Portable optimization, diagnostic, and post-installation technical assistant for Windows 10 and 11.

[![Version](https://img.shields.io/badge/version-1.1.0.0-blue.svg)](https://github.com/jvfigueiro/optiassist/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(64--bit)-0078D4.svg)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/license-GNU%20GPLv3-green.svg)](LICENSE)
[![Architecture](https://img.shields.io/badge/architecture-WPF%20%7C%20.NET%204.8-512BD4.svg)](https://dotnet.microsoft.com/)

OptiAssist is a native C#/WPF (.NET Framework 4.8) technician workbench engineered for IT professionals, system administrators, and computer repair workshops. It consolidates asynchronous hardware diagnostics, automated post-installation software deployment, deep system cleaning routines, a script and repair hub, native Windows administrative consoles, and service order report generation into a single, cohesive portable environment with zero external dependencies.

---

## Key Features

| Module | Summary Description | Documentation |
| :--- | :--- | :---: |
| **Dashboard** | Real-time asynchronous hardware telemetry (CPU, total RAM, GPU, motherboard, BIOS, Serial/Service Tag with 1-click copy), OS activation status, security posture (BitLocker, TPM 2.0, Secure Boot), and customizable Common Tasks shortcuts. | [Read Guide](Docs/modules/dashboard.md) |
| **App Manager** | Batch unattended software deployment via offline local installers (`Installers/`) or official Microsoft Winget repository fallback, with an installed updates scanner and visual catalog editor. | [Read Guide](Docs/modules/app-manager.md) |
| **Toolbox** | Script and repair hub supporting PowerShell (with Base64 encoding), Command Prompt, and portable executables with dynamic categories, real-time stdout/stderr streaming, and process termination. | [Read Guide](Docs/modules/toolbox.md) |
| **System Cleaner** | Modular deep disk cleaner with non-destructive Dry-Run analysis, protected system path validation (`PathSanitizer`), locked-file resilience, SQLite database compaction, and developer cache cleanup. | [Read Guide](Docs/modules/system-cleaner.md) |
| **Startup Manager** | Unified autostart inspector: Windows Registry (`Run`/`RunOnce` with non-destructive `StartupApproved` binary control), Startup folders, and Task Scheduler entries via COM (`Schedule.Service`). | [Read Guide](Docs/modules/startup-manager.md) |
| **Windows Tools** | Fast-launch control panel for native administrative consoles (`sysdm.cpl`, `services.msc`, `regedit`, `cleanmgr`, `dfrgui`), technician storage folders, and direct UEFI firmware reboot. | [Read Guide](Docs/modules/windows-tools.md) |
| **Maintenance Report** | Technical service order and report generator with client details, machine specifications, an 8-point checklist of performed procedures, and structured `.txt` export. | [Read Guide](Docs/modules/maintenance-report.md) |
| **PC Assist** | Real-time hardware telemetry monitor (CPU, RAM, GPU, Disks), deep notebook battery health and degradation analysis (ACPI wear level, autonomy, powercfg report), and quick bench diagnostics (CPU, RAM bandwidth, disk I/O, and thermal stress). | [Read Guide](Docs/modules/pc-assist.md) |
| **Settings & Deployment** | UI personalization (Light/Dark themes, accent color, top bar logo), zero-restart language hot-reloading (i18n), Always on Top window pinning, module manager, and built-in deployment engine. | [Read Guide](Docs/modules/settings-deployment.md) |
| **Logs & Terminal** | Thread-safe real-time diagnostic terminal with an independent floating pop-out window (`LogWindow`), keyword search, clipboard copying, and `.txt` log export. | [Read Guide](Docs/modules/logs-terminal.md) |

---

## System Requirements

- **Operating System**: Windows 10 (version 1809 or higher) or Windows 11 (64-bit recommended).
- **Runtime**: Microsoft .NET Framework 4.8 (pre-installed natively on modern Windows 10 and 11 installations).
- **Privileges**: Administrator rights (`requireAdministrator` in application manifest) required for low-level WMI hardware inspection, service manipulation, registry autostart management, and temporary file cleaning.
- **Connectivity**: Optional. The application operates 100% offline; internet access is required only for Winget downloads or remote community scripts.

---

## Installation and Deployment

OptiAssist requires no third-party installers and runs immediately from removable flash drives or local directories:

1. **Portable Mode (USB / External Storage)**: Extract the `OptiAssist-v1.1.0.0.7z` archive to any directory or USB drive and run `OptiAssist.exe`. Configuration files and plugins reside entirely within `Config/`.
2. **Standard Installation (Local System)**: Open **Settings > Install OptiAssist** (or use the first-run Welcome Wizard). The application copies files to `%ProgramFiles%\OptiAssist`, creates Start Menu and Desktop shortcuts, and registers in Windows Control Panel (*Programs and Features*).
3. **USB AutoRun**: From the deployment dialog, generate an `autorun.inf` at the root of a USB flash drive to assign the custom application icon and drive label in Windows Explorer.
4. **Uninstallation**: Run `OptiAssist.exe --uninstall` or use the Windows Control Panel entry to cleanly remove files, shortcuts, and registry entries.

For automated compilation and packaging details, refer to the [Build and Deployment Guide](Docs/build.md).

---

## Getting Started

1. **Launch**: Right-click `OptiAssist.exe` and select "Run as administrator" (or accept the UAC elevation prompt).
2. **Inspect Telemetry**: Review processor model, physical RAM, GPU, motherboard, BIOS, and the security indicators (BitLocker, TPM 2.0, Secure Boot) on the Dashboard.
3. **Execute System Maintenance**: In **Toolbox**, run system integrity repair presets (SFC, DISM) or network troubleshooting scripts.
4. **Deploy Software**: In **App Manager**, select required software suites and click **Install Selected** to install offline packages or fall back to Winget.
5. **Generate Service Record**: In **Maintenance Report**, enter customer details, check off performed maintenance steps, and export the technical report.

---

## Modular Architecture Overview

OptiAssist implements a **Microkernel Host with Dynamic In-Memory Assembly Loading**:
- **Host Kernel (`OptiAssist.exe`)**: Coordinates navigation, persistent settings, theming, thread-safe logging, and native views.
- **Anti-Lock Assembly Loading**: Plugins are read into memory byte arrays (`File.ReadAllBytes` + `Assembly.Load`), ensuring add-on DLLs on disk are never locked and can be recompiled, replaced, or deleted at runtime.
- **Lazy Initialization**: Add-on views are created via `IOptiAssistAddon.CreateView()` only on first tab selection and cached in memory.
- **Unified Localization**: Per-module JSON dictionaries are merged dynamically at startup, supporting zero-restart hot-reloading.

For architectural flowcharts and sequence lifecycles, refer to the [Architecture Documentation](Docs/arch.md).

---

## Technologies Used

- **Language**: C# 7.3+ and XAML (WPF).
- **Framework**: Microsoft .NET Framework 4.8.
- **System Integrations**: Windows Management Instrumentation (WMI / CIM via `System.Management`), native Windows Registry (`Microsoft.Win32`), Windows Package Manager (`winget.exe`), Windows Task Scheduler 2.0 COM interface (`Schedule.Service`), Win32 Shell APIs (`Shell32.SHEmptyRecycleBin`, `User32.SendMessageTimeout`), and asynchronous PowerShell/CMD subprocesses.
- **External Dependencies**: **Zero**. JSON parsing, i18n dictionaries, COM shortcut creation, and logging pipelines are implemented entirely with native .NET Framework BCL assemblies and Windows platform APIs.

---

## Project Structure

```text
OptiAssist/
|-- OptiAssist.csproj              # Main WPF (.NET 4.8) project file
|-- OptiAssist.sln                 # Visual Studio Solution
|-- build.bat                      # Automated build script
|-- LICENSE                        # GNU General Public License v3.0
|-- README.md                      # Consolidated project guide
|-- src/                           # Host core source code
|   |-- app.manifest               # Elevation manifest (requireAdministrator)
|   |-- MainWindow.xaml / .cs      # Shell window and navigation coordinator
|   |-- AddonHelper.cs             # Add-on discovery engine, contracts, and lifecycle
|   |-- LocalizationHelper.cs      # Multi-language translation engine (i18n)
|   |-- SettingsHelper.cs          # Zero-dependency JSON settings engine
|   |-- ShortcutsHelper.cs         # AppDeploymentHelper, WelcomeWizard, and launchers
|   |-- SystemInfoHelper.cs        # Asynchronous WMI/CIM hardware telemetry
|   |-- TweakHelper.cs             # Registry and process optimization utilities
|   `-- LogWindow.cs               # Detached floating log console
|-- src/Addons/                    # Decoupled plugin Class Library projects
|   |-- AppManager/                # Offline installer and Winget manager
|   |-- MaintenanceReport/         # Service order and checklist generator
|   |-- StartupManager/            # Startup items and Task Scheduler inspector
|   |-- SystemCleaner/             # Deep cleaner engine and dry-run scanner
|   `-- Toolbox/                   # Script runner and automation hub
|-- Config/                        # Configurations, catalogs, and translations
|   |-- settings.json              # Global user preferences
|   |-- custom_shortcuts.json      # Customizable Dashboard common tasks
|   |-- Languages/                 # Multi-language dictionaries (en-US, pt-BR)
|   `-- Addons/                    # Compiled plugin DLL binaries and local catalogs
|-- Maintenance/                   # Auxiliary portable diagnostic utilities
|-- Installers/                    # Offline technician installer repository
`-- Docs/                          # Complete technical documentation
```

---

## How to Contribute and Create Modules

Extending OptiAssist does not require modifying the host executable. Custom modules can be developed as independent Class Libraries (.NET Framework 4.8) implementing the `IOptiAssistAddon` interface:

1. Create a Class Library project referencing `OptiAssist.exe` with `<Private>False</Private>`.
2. Implement `IOptiAssistAddon` and construct your user interface programmatically inside `CreateView()`.
3. Add companion localization files (`en-US.json`, `pt-BR.json`).
4. Compile the output DLL into `Config/Addons/<YourModule>/<YourModule>.dll`.

Read the comprehensive step-by-step developer tutorial in [How to Create a Module in OptiAssist](Docs/create-module.md).

---

## Disclaimer

OptiAssist executes administrative operations that modify system configurations, including Windows Registry hives, background services, scheduled tasks, and temporary system files.

This tool is intended strictly for IT professionals, system administrators, and computer maintenance technicians. Always review selected scripts, automated actions, and cleaning routines before executing them on production systems. The authors and contributors assume no liability for data loss or system instability resulting from improper use.

---

## License

OptiAssist is free and open-source software licensed under the **GNU General Public License v3.0 (GNU GPLv3)**. You are free to run, study, share, and modify the application under the terms of this license.

For complete license terms, refer to the [LICENSE](LICENSE) file or access the built-in License view within the application (*Settings > License*).

---

## Technical Documentation Index (`Docs/`)

For in-depth technical specifications, refer to the dedicated documentation files:

- [System Architecture and Modular Lifecycle](Docs/arch.md)
- [Developer Guide: Creating Add-on Modules](Docs/create-module.md)
- [Build, Packaging, and Deployment Guide](Docs/build.md)
- [Configuration, Localization, and Logging Subsystems](Docs/logs-and-settings.md)
- [Frequently Asked Questions and Troubleshooting Guide](Docs/faq-and-troubleshooting.md)
- [Architecture Map and Code Analysis](Docs/_analysis/00-map.md)
- [Module Documentation Index](Docs/modules/README.md)
  - [Dashboard & Hardware Telemetry](Docs/modules/dashboard.md)
  - [App Manager & Software Deployment](Docs/modules/app-manager.md)
  - [Toolbox & Automation Hub](Docs/modules/toolbox.md)
  - [System Cleaner & Deep Disk Cleanup](Docs/modules/system-cleaner.md)
  - [Startup Manager & Scheduled Tasks](Docs/modules/startup-manager.md)
  - [Windows Tools & Native Consoles](Docs/modules/windows-tools.md)
  - [Maintenance Report & Service Orders](Docs/modules/maintenance-report.md)
  - [PC Assist Hardware Monitor & Bench](Docs/modules/pc-assist.md)
  - [Settings & Native Deployment](Docs/modules/settings-deployment.md)
  - [Logs & Diagnostic Terminal](Docs/modules/logs-terminal.md)
