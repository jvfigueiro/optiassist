# OptiAssist Technical Documentation (`v1.2.S7`)

Welcome to the official technical documentation for **OptiAssist (`1.2.0.7` / `1.2.S7`)** — a portable Windows 10 and Windows 11 technician workbench engineered with WPF and .NET Framework 4.8.

---

## 1. Engineering & Architecture Guides

| Guide | Description |
| :--- | :--- |
| [System Architecture & Module Lifecycle](arch.md) | Microkernel host architecture, Mermaid diagrams, and anti-lock assembly/dependency preloading. |
| [Architecture & Codebase Map](_analysis/00-map.md) | Complete stack inventory, folder responsibilities, Windows integrations, and persistence analysis. |
| [How to Create an OptiAssist Module](create-module.md) | Step-by-step developer guide with a minimal `IOptiAssistAddon` / `IOptiAssistHost` implementation. |
| [Build, Packaging & Deployment Guide](build.md) | MSBuild pipeline, runtime vs. development layout, `.7z` packaging, `Verify-ReleasePackage.ps1`, and `AppDeploymentHelper`. |
| [Configuration, Localization & Logging](logs-and-settings.md) | `SettingsHelper`, `CustomShortcutsHelper`, `LocalizationHelper`, and external Windows Terminal log streaming. |
| [FAQ & Troubleshooting](faq-and-troubleshooting.md) | Real error-handling paths, `LibreHardwareMonitorLib` preload, `PawnIO`, VirtIO storage guards, and edge cases. |

---

## 2. Module Specifications (`Docs/modules/`)

- **[Module Documentation Index](modules/README.md)**
  - [Dashboard](modules/dashboard.md)
  - [Windows Tools — Admin Consoles, OptiBoost & Compatibility Checker](modules/windows-tools.md)
  - [Terminal & Logs](modules/logs-terminal.md)
  - [Toolbox (`toolbox`)](modules/toolbox.md)
  - [App Manager (`app-manager`)](modules/app-manager.md)
  - [System Cleaner (`system-cleaner`)](modules/system-cleaner.md)
  - [Startup Manager (`startup-manager`)](modules/startup-manager.md)
  - [Hardware Monitor (`hardware-monitor` / `PCAssist`)](modules/hardware-monitor.md)
  - [Maintenance Report (`maintenance-report`)](modules/maintenance-report.md)
  - [Settings & Deployment](modules/settings-deployment.md)
