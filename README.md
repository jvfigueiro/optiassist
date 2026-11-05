# OptiAssist

**Portable optimization, diagnostic, and post-installation technical assistant for Windows 10 and 11.**

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/jvfigueiro/optiassist)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(64--bit)-0078D4.svg)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/license-GNU%20GPLv3-green.svg)](LICENSE)
[![Architecture](https://img.shields.io/badge/architecture-WPF%20%7C%20.NET%204.8-512BD4.svg)](https://dotnet.microsoft.com/)

**OptiAssist** is a native C#/WPF (.NET Framework 4.8) technician workbench engineered for IT professionals, support technicians, and maintenance workshops. The application consolidates real-time hardware diagnostics, unattended post-installation software deployment with offline catalogs and Winget fallback, deep system cleaning routines, a custom command and repair script hub (Toolbox), native Windows administration shortcuts, and technical service order generation into a single, cohesive portable environment without external dependencies.

---

## 🚀 Key Features

| Module | Summary Description | Documentation |
| :--- | :--- | :---: |
| 🏠 **Dashboard** | Asynchronous hardware telemetry (CPU, RAM, GPU, Motherboard, BIOS, Serial/Service Tag with 1-click copy), security badges (BitLocker, TPM 2.0, Secure Boot), and customizable quick-action shortcuts. | [Read Guide](Docs/modulos/dashboard.md) |
| 📦 **App Manager** | Batch unattended software deployment via offline installers (`Installers/`) or official Microsoft Winget repository, with a visual catalog editor (`autoinstall.json`). | [Read Guide](Docs/modulos/app-manager.md) |
| 🧰 **Toolbox** | Script and command execution hub (`PowerShell`, `CMD`, portable binaries) with dynamic categorization, real-time stdout/stderr streaming, and Base64 command encoding. | [Read Guide](Docs/modulos/toolbox.md) |
| 🧹 **System Cleaner** | BleachBit-inspired safe cleaner with Dry-Run (*Scan*) support, critical path protection (*PathSanitizer*), locked-file resilience, and deep scans for developer artifacts (`node_modules`, `venv`, etc.). | [Read Guide](Docs/modulos/system-cleaner.md) |
| 🚀 **Startup Manager** | Unified autostart inspector: Windows Registry (`Run`/`RunOnce` with `StartupApproved`), user/system Startup folders, and Scheduled Tasks (`schtasks`), with danger warning dialogs. | [Read Guide](Docs/modulos/startup-manager.md) |
| 🛠️ **Windows Tools** | Fast-launch hub for native administrative consoles (`sysdm.cpl`, `services.msc`, `compmgmt.msc`, `regedit`, `cleanmgr`, direct UEFI reboot, etc.). | [Read Guide](Docs/modulos/windows-tools.md) |
| 📋 **Maintenance Report** | Technical service order and report generator with client/technician info, an 8-step maintenance checklist, and structured `.txt` export. | [Read Guide](Docs/modulos/maintenance-report.md) |
| ⚙️ **Settings & Deployment** | Theme personalization (Light/Dark/Accent colors), zero-restart language hot-reloading (i18n), Always-on-top, module toggles, and built-in installer/uninstaller. | [Read Guide](Docs/modulos/settings-deployment.md) |
| 📄 **Logs & Terminal** | Thread-safe real-time diagnostic terminal with an independent floating pop-out window (`LogWindow`), text search, clipboard copying, and `.txt` log export. | [Read Guide](Docs/modulos/logs-terminal.md) |
| ℹ️ **About** | Application version information, GNU GPLv3 license dialog, community credits (WinUtil / Chris Titus Tech), and dynamic `AssemblyInfo` audit of loaded plugins. | [Read Guide](Docs/modulos/about.md) |

---

## 💻 System Requirements

- **Operating System**: Windows 10 (version 1809 or higher) or Windows 11 (64-bit recommended).
- **Runtime**: Microsoft .NET Framework 4.8 (pre-installed natively on modern Windows 10/11 installations).
- **Privileges**: Requires **Administrator** rights (`requireAdministrator`) for low-level WMI queries, system services, and Windows temporary folder maintenance.
- **Connectivity**: Optional. 100% functional offline; internet access is required only for Winget downloads or remote scripts.

---

## 📥 Installation and Usage

OptiAssist is designed around a **Zero External Dependencies** philosophy: it does not require third-party installers and can run immediately from a flash drive or local directory.

### Deployment Modes:
1. **Portable Mode (USB / External SSD)**: Extract or copy the OptiAssist directory to your removable drive and launch `OptiAssist.exe`. Configuration files and plugins reside entirely within the app directory.
2. **Standard Installation on System**: Open OptiAssist, go to **Settings**, and click **Install OptiAssist**. The application creates Start Menu and Desktop shortcuts, registers in Windows Control Panel (*Programs and Features*), and copies files to `%ProgramFiles%\OptiAssist`.
3. **USB AutoRun**: From the installer or settings dialog, generate an `autorun.inf` at the root of your USB drive to apply the custom application icon and label in Windows Explorer.

For in-depth compilation, packaging, and silent uninstallation details, refer to the [**Build and Distribution Guide**](Docs/build-e-distribuicao.md).

---

## ⚡ Getting Started

1. **Launch the Application**: Right-click `OptiAssist.exe` and select "Run as administrator" (or accept the UAC prompt).
2. **Review Telemetry**: Check motherboard specs, processor model, RAM capacity, and the security posture badges (BitLocker, TPM 2.0, Secure Boot) in the bottom bar.
3. **Deploy Post-Install Software**: Navigate to **App Manager**, select required packages, and click **Install Selected**. If offline installers exist in `Installers/`, deployment is instant; otherwise, Winget handles silent background downloading.
4. **Perform System Maintenance**: In **Toolbox**, trigger system repairs such as SFC, DISM, and print spooler resets.
5. **Generate Service Report**: In **Report**, fill in customer details, verify performed steps on the checklist, and save the maintenance record.

---

## 🏗️ Modular Architecture Overview

OptiAssist implements a **Microkernel pattern with Dynamic Module Injection (Add-ons)**:

```
┌─────────────────────────────────────────────────────────────┐
│                 Host Kernel (OptiAssist.exe)                │
│  ┌───────────────────┐ ┌────────────────┐ ┌──────────────┐  │
│  │ Settings & Config │ │ i18n Engine    │ │ Log Pipeline │  │
│  └───────────────────┘ └────────────────┘ └──────────────┘  │
└──────────────────────────────▲──────────────────────────────┘
                               │ Contracts: IOptiAssistAddon / IOptiAssistHost
                               │ Anti-Lock in-memory loading (byte[])
┌──────────────┬───────────────┴──────────────┬───────────────┐
│              │                              │               │
▼              ▼                              ▼               ▼
AppManager     SystemCleaner                  Toolbox         StartupManager
(Addon DLL)    (Addon DLL)                    (Addon DLL)     (Addon DLL)
```

- **Binary Decoupling**: The central host executable manages navigation, persistence, theming, and logs.
- **Non-Locking Add-on Loader**: Modules are loaded from in-memory byte arrays (`File.ReadAllBytes` + `Assembly.Load`), allowing plugins to be updated, replaced, or recompiled on disk without locking running processes.
- **Unified Localization**: Multi-language JSON dictionaries (`Config/Languages/`) with zero-restart runtime hot-reloading.

To explore architecture diagrams and component sequence flows, see the [**Architecture Documentation**](Docs/arquitetura.md).

---

## 🛠️ Technology Stack

- **Language**: C# (compatible with C# 7.3+).
- **User Interface**: Windows Presentation Foundation (WPF) / Native XAML.
- **Base Framework**: Microsoft .NET Framework 4.8.
- **System Integrations**: Windows Management Instrumentation (WMI / CIM via `System.Management`), native Win32 APIs, Windows Registry (`Microsoft.Win32`), Windows Package Manager (`winget.exe`), Windows Task Scheduler (`schtasks.exe`), and asynchronous PowerShell/CMD subprocesses.
- **External Dependencies**: **Zero**. JSON parsing, i18n engines, COM shortcut creation, and logging pipelines are implemented entirely natively.

---

## 📂 Repository Structure

```text
OptiAssist/
├── OptiAssist.csproj              # Main WPF (.NET 4.8) project file
├── OptiAssist.sln                 # Visual Studio Solution
├── build.bat                      # Automated build script
├── src/                           # Host core source code
│   ├── app.manifest               # Elevation manifest (requireAdministrator)
│   ├── MainWindow.xaml / .cs      # Main shell and navigation coordinator
│   ├── AddonHelper.cs             # Add-on contracts and discovery engine
│   ├── LocalizationHelper.cs      # Multi-language translation engine (i18n)
│   ├── SettingsHelper.cs          # Zero-dependency JSON settings parser/serializer
│   ├── ShortcutsHelper.cs         # AppDeploymentHelper (installer, shortcuts, autorun)
│   ├── SystemInfoHelper.cs        # Asynchronous WMI telemetry (HardwareSummary)
│   ├── TweakHelper.cs             # Registry and process optimization utilities
│   └── LogWindow.cs               # Detached floating log window
├── src/Addons/                    # Decoupled plugin projects
│   ├── AppManager/                # Installer and Winget management
│   ├── MaintenanceReport/         # Technical report and checklist generator
│   ├── StartupManager/            # Startup items and task scheduler inspector
│   ├── SystemCleaner/             # Deep cleaner engine and dry-run scanner
│   └── Toolbox/                   # Script runner and automation hub
├── Config/                        # Configurations, catalogs, and translations
│   ├── settings.json              # Global user preferences
│   ├── autoinstall.json           # Software installation catalog
│   ├── toolbox.json               # Configured repair and optimization scripts
│   ├── custom_shortcuts.json      # Customizable Dashboard common tasks
│   ├── Languages/                 # Multi-language dictionaries (en-US, pt-BR)
│   └── Addons/                    # Compiled plugin DLL binaries
├── Maintenance/                   # Auxiliary portable diagnostics
└── Docs/                          # Complete technical documentation
```

---

## 🧩 How to Contribute and Create Modules

Extending OptiAssist does not require modifying the host executable. New features can be created as independent Class Libraries (.NET 4.8) implementing the `IOptiAssistAddon` interface:

1. Create a Class Library project referencing `OptiAssist.exe` with `<Private>False</Private>`.
2. Implement `IOptiAssistAddon` and construct your user interface inside `CreateView()`.
3. Add custom translation keys to the language JSON files.
4. Compile the output DLL into `Config/Addons/<YourModule>/<YourModule>.dll`.

Read the comprehensive step-by-step developer guide in [**How to Create a Module in OptiAssist**](Docs/como-criar-um-modulo.md).

---

## ⚠️ Disclaimer

> [!WARNING]
> **OptiAssist** executes administrative operations that directly alter operating system configurations, including Windows Registry keys, background services, scheduled tasks, and system temporary files.
> 
> This tool is intended for **support technicians, system administrators, and advanced users**. Always review selected scripts and automation tasks before executing operations. The developers and contributors assume no liability for data loss or system instability resulting from improper use.

---

## ⚖️ License

This project is free software distributed under the terms of the **GNU General Public License v3.0 (GNU GPLv3)**. For more details, refer to the [LICENSE](LICENSE) file or access the built-in license dialog (*Settings* -> *License*).

---

## 📚 Technical Documentation Index (`Docs/`)

For detailed technical references, refer to the dedicated documentation files:

- 🗺️ [**Architecture Map and Code Analysis**](Docs/_analise/00-mapa.md)
- 🏛️ [**System Architecture and Module Lifecycle**](Docs/arquitetura.md)
- 🛠️ [**Developer Guide: How to Create a Module**](Docs/como-criar-um-modulo.md)
- 📦 [**Build, Packaging, and Distribution**](Docs/build-e-distribuicao.md)
- ⚙️ [**Configuration, JSON Schemas, i18n, and Logging**](Docs/configuracao-e-logs.md)
- ❓ [**FAQ and Troubleshooting Guide**](Docs/faq-e-troubleshooting.md)
- 📑 [**Module Index and Detailed Documentation**](Docs/modulos/README.md)
  - 🏠 [Dashboard](Docs/modulos/dashboard.md)
  - 📦 [App Manager](Docs/modulos/app-manager.md)
  - 🧰 [Toolbox](Docs/modulos/toolbox.md)
  - 🧹 [System Cleaner](Docs/modulos/system-cleaner.md)
  - 🚀 [Startup Manager](Docs/modulos/startup-manager.md)
  - 🛠️ [Windows Tools](Docs/modulos/windows-tools.md)
  - 📋 [Maintenance Report](Docs/modulos/maintenance-report.md)
  - ⚙️ [Settings & Deployment](Docs/modulos/settings-deployment.md)
  - 📄 [Logs & Terminal](Docs/modulos/logs-terminal.md)
  - ℹ️ [About](Docs/modulos/about.md)
