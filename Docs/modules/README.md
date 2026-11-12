# OptiAssist Module Index

This directory contains individual, detailed technical documentation for each module and subsystem in **OptiAssist**.

Each document adheres to a standardized specification structure:
1. **Overview and Problem Solved**: The operational context and technician pain point addressed.
2. **Step-by-Step Usage Guide**: Comprehensive instructions from a bench technician's perspective.
3. **Internal Architecture & Technical Execution Flow**: Execution lifecycles, Windows APIs, WMI/CIM tables, registry hives, and subprocess management.
4. **Inputs, Outputs, and Configurations**: Referenced JSON files, arguments, generated artifacts, and UI outputs.
5. **Requirements**: Elevation privileges, Windows version compatibility, and runtime dependencies.
6. **Risks and Side Effects**: System modifications, persistence, and reversibility.
7. **Logging and Error Handling**: Exception suppression, logging traces, and user alerts.
8. **Related Source Files**: Direct code references within the repository.

---

## Host Native Modules (Core Executable)

These modules are built directly into `OptiAssist.exe` and provide the central application framework, hardware diagnostics, access to operating system administrative tools, and transversal services:

| Module | Documentation File | Type | Functional Summary |
| :--- | :--- | :--- | :--- |
| **Dashboard & Hardware Telemetry** | [dashboard.md](dashboard.md) | Host Native | Landing screen with hardware diagnostics (WMI/CIM/Registry), TPM 2.0, Secure Boot, BitLocker, OS activation status, and customizable Common Tasks shortcuts (`custom_shortcuts.json`). |
| **Windows Tools** | [windows-tools.md](windows-tools.md) | Host Native | Fast-launch control panel for native Windows administrative consoles (`.msc`, `.cpl`, `sysnative` redirection), technician directories, and direct UEFI reboot. |
| **Settings & Deployment** | [settings-deployment.md](settings-deployment.md) | Host Native | Theme management (Light/Dark/Accent), language hot-reloading (i18n), Always on Top window control, Add-on DLL manager, Welcome Wizard (`freshstart`), and self-contained installer (`AppDeploymentHelper`). |
| **Logs & Diagnostic Terminal** | [logs-terminal.md](logs-terminal.md) | Host Native | Real-time diagnostic monitoring with thread-safe `Dispatcher` streaming, embedded console, and a detached floating window (`LogWindow`) with search, copy, and `.txt` export capabilities. |

---

## Decoupled Add-on Modules (External DLLs)

These modules operate as independent .NET Framework 4.8 Class Libraries located in `Config\Addons\<ModuleName>\<ModuleName>.dll`. They implement the `IOptiAssistAddon` contract and are loaded into memory via raw byte arrays to prevent disk file-locking:

| Module | Documentation File | DLL Location | Functional Summary |
| :--- | :--- | :--- | :--- |
| **Toolbox** | [toolbox.md](toolbox.md) | `Config\Addons\Toolbox\Toolbox.dll` | Technician automation hub with dynamic categories, over 20 pre-configured repair and optimization presets, Base64 PowerShell execution, and process termination support (Core protected). |
| **App Manager** | [app-manager.md](app-manager.md) | `Config\Addons\AppManager\AppManager.dll` | Dual-tab software workbench: batch upgrades scanner via Winget, and automated post-installation of essential software with offline local installers and silent Winget fallback. |
| **System Cleaner** | [system-cleaner.md](system-cleaner.md) | `Config\Addons\SystemCleaner\SystemCleaner.dll` | Modular deep disk cleaning engine inspired by BleachBit: preliminary non-destructive *Dry-Run* analysis, file inspector, SQLite VACUUM compaction, and `PathSanitizer` safety shielding. |
| **Startup Manager** | [startup-manager.md](startup-manager.md) | `Config\Addons\StartupManager\StartupManager.dll` | Auditor and manager for autostart entries across Windows Registry keys (`Run`/`RunOnce` with non-destructive `StartupApproved` binary control), Startup folders, and Task Scheduler via COM (`Schedule.Service`). |
| **Maintenance Report** | [maintenance-report.md](maintenance-report.md) | `Config\Addons\MaintenanceReport\MaintenanceReport.dll` | Technical service order and maintenance report generator in structured plain text, consolidating customer data, an 8-step maintenance checklist, and audited hardware specs. |
| **Hardware Monitor** | [hardware-monitor.md](hardware-monitor.md) | `Config\Addons\PCAssist\PCAssist.dll` | Real-time hardware telemetry monitor (CPU, RAM, GPU, Disks), deep notebook battery health and degradation analysis (ACPI wear level, autonomy, powercfg report), and quick bench diagnostics (CPU, RAM bandwidth, disk I/O, and thermal stress). |

---

## Architectural and Engineering References

* [**00-map.md (Architecture Map & Code Analysis)**](../_analysis/00-map.md): Comprehensive analysis of the technology stack, zero-dependency philosophy, Win32/WMI/CIM/COM integration points, Windows 10 vs 11 compatibility matrix, and error handling.
* [**architecture.md (System Architecture & Module Lifecycle)**](../architecture.md): Deep dive into the Microkernel pattern, interface contracts (`IOptiAssistAddon` / `IOptiAssistHost`), and anti-lock assembly loading.
* [**how-to-create-a-module.md (Developer Guide)**](../how-to-create-a-module.md): Step-by-step developer tutorial for creating custom add-on modules.
* [**build-and-distribution.md (Build and Distribution)**](../build-and-distribution.md): Compilation commands, project targets, and deployment architecture.
* [**configuration-and-logs.md (Configuration & Logs)**](../configuration-and-logs.md): JSON schemas, multi-language localization, and the logging subsystem.
* [**faq-and-troubleshooting.md (Troubleshooting Guide)**](../faq-and-troubleshooting.md): Answers to common questions, error code explanations, and edge cases.
