# OptiAssist Module Index

This directory contains individual, detailed technical documentation for each module and subsystem in **OptiAssist**.

Each document follows a standardized structure: technical overview and problem solved, step-by-step usage guide, internal architecture and Windows API flow, inputs/outputs/configurations, requirements and UAC privileges, risks and reversibility, error handling, and related source files in the repository.

---

## 🗂️ Host Native Modules (Core Executable)

These modules are built directly into `OptiAssist.exe` and provide the central application framework, hardware diagnostics, access to operating system administrative tools, and transversal services:

| Module | Documentation File | Functional Summary |
| :--- | :--- | :--- |
| **Dashboard & Common Tasks** | [dashboard.md](dashboard.md) | Landing screen with hardware diagnostics (WMI/CIM/Registry), TPM 2.0, Secure Boot, BitLocker, OS activation status, and a customizable Common Tasks shortcut grid (`custom_shortcuts.json`). |
| **Windows Tools** | [windows-tools.md](windows-tools.md) | Centralized hub for fast access to native Windows administrative consoles (`.msc`, `.cpl`, `sysnative` 64-bit redirection), technician folders, and elevated command prompts. |
| **Settings & Deployment** | [settings-deployment.md](settings-deployment.md) | Theme management (Light/Dark/Accent), language hot-reloading (i18n), floating window preferences, Add-on DLL manager, and self-installer (`AppDeploymentHelper`) with Standard, Portable, and USB AutoRun modes. |
| **Terminal & Logs** | [logs-terminal.md](logs-terminal.md) | Real-time diagnostic monitoring with thread-safe `Dispatcher` streaming, embedded console, and a detached floating window (`LogWindow`) with search, copy, and `.txt` export capabilities. |

---

## 🧩 Decoupled Add-on Modules (External DLLs)

These modules operate as independent .NET Framework 4.8 Class Libraries located in `Config\Addons\<ModuleName>\<ModuleName>.dll`. They implement the `IOptiAssistAddon` contract and are loaded into memory via raw byte arrays to prevent disk file-locking:

| Module | Documentation File | DLL Location | Functional Summary |
| :--- | :--- | :--- | :--- |
| **Toolbox** | [toolbox.md](toolbox.md) | `Config\Addons\Toolbox\Toolbox.dll` | Technician Swiss Army knife with dynamic categories, over 20 pre-configured repair and optimization presets, and full CRUD automation (`toolbox.json`). |
| **App Manager** | [app-manager.md](app-manager.md) | `Config\Addons\AppManager\AppManager.dll` | Dual-tab software manager: batch upgrades scanner via Winget, and automated post-installation of essential software with offline local installers and silent fallback. |
| **System Cleaner** | [system-cleaner.md](system-cleaner.md) | `Config\Addons\SystemCleaner\SystemCleaner.dll` | BleachBit-inspired deep disk cleaning engine with 13 specialized engines, preliminary *Dry-Run* analysis, file inspector, and vital path protection shielding. |
| **Startup Manager** | [startup-manager.md](startup-manager.md) | `Config\Addons\StartupManager\StartupManager.dll` | Auditor and manager for autostart entries across Windows Registry keys (HKCU/HKLM 32/64-bit with official `StartupApproved` support) and Task Scheduler via COM (`Schedule.Service`). |
| **Maintenance Report** | [maintenance-report.md](maintenance-report.md) | `Config\Addons\MaintenanceReport\MaintenanceReport.dll` | Technical service order and maintenance report generator in structured text, consolidating customer data, maintenance checklist, and audited hardware specs. |

---

## Related Engineering and Architecture Documents

* [**00-map.md (Architecture Map & Code Analysis)**](../analysis/00-map.md): Comprehensive analysis of the technology stack, zero-dependency philosophy, Win32/WMI/CIM/COM integration points, Windows 10 vs 11 compatibility matrix, and error handling.
* [**architecture.md (System Architecture & Module Lifecycle)**](../architecture.md): Deep dive into the Microkernel pattern, interface contracts (`IOptiAssistAddon` / `IOptiAssistHost`), and anti-lock assembly loading.
* [**how-to-create-a-module.md (Developer Guide)**](../how-to-create-a-module.md): Step-by-step developer tutorial for creating custom add-on modules.
* [**build-and-distribution.md (Build and Distribution)**](../build-and-distribution.md): Compilation commands, project targets, and deployment architecture.
* [**configuration-and-logs.md (Configuration & Logs)**](../configuration-and-logs.md): JSON schemas, multi-language localization, and the logging subsystem.
* [**faq-and-troubleshooting.md (Troubleshooting Guide)**](../faq-and-troubleshooting.md): Answers to common questions, error code explanations, and edge cases.
