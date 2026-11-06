<div align="center">
  <h1>OptiAssist</h1>
  <p><strong>Portable optimization, diagnostic, and post-installation technical assistant for Windows 10 and 11.</strong></p>

  <p>
    <a href="https://github.com/jvfigueiro/optiassist"><img src="https://img.shields.io/badge/version-1.1.0-blue.svg" alt="Version" /></a>
    <a href="https://microsoft.com/windows"><img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(64--bit)-0078D4.svg" alt="Platform" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-GNU%20GPLv3-green.svg" alt="License" /></a>
    <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/architecture-WPF%20%7C%20.NET%204.8-512BD4.svg" alt="Architecture" /></a>
  </p>

  <p>
    OptiAssist is a native C#/WPF (.NET Framework 4.8) technician workbench engineered for IT professionals, system administrators, and computer maintenance workshops. It consolidates asynchronous hardware diagnostics, automated post-installation application deployment with offline catalogs and Winget fallback, deep system cleaning routines, a custom command and repair script hub (Toolbox), native Windows administration consoles, and technical service order generation into a single, cohesive portable environment without external dependencies.
  </p>

  <br />

  <img src="Docs/images/dashboard.png" alt="OptiAssist Dashboard" width="900" />
</div>

---

## Key Features

- **Asynchronous System Telemetry & Dashboard**: Real-time hardware inspection (CPU, physical RAM, GPU, motherboard, BIOS, serial number/service tag with one-click clipboard copy), Windows installation date, activation status, security posture audit (BitLocker, TPM 2.0, Secure Boot), and customizable Common Tasks shortcuts.
- **Automated App Manager**: Unattended batch deployment supporting local offline installers (`Installers/`) with automated silent switches, official Microsoft Winget repository fallback, catalog editing (`autoinstall.json`), and installed application updates scanning.
- **Script & Automation Toolbox**: Technician script execution engine supporting PowerShell, Command Prompt (CMD), and portable executables with dynamic categories, real-time output streaming, search filtering, and process termination.
- **Deep System Cleaner**: Modular cleaning engine inspired by BleachBit with non-destructive Dry-Run analysis, protected system path validation (PathSanitizer), locked-file resilience, and targeted developer cache cleanup (`node_modules`, `venv`, `__pycache__`).
- **Startup & Task Manager**: Unified startup inspector covering Windows Registry (`Run`/`RunOnce` and `StartupApproved`), user and system Startup folders, and Task Scheduler entries via native COM (`Schedule.Service`), including search filtering and safety safeguards for native OS entries.
- **Native Windows Administrative Consoles**: Quick-launch hub for core Windows management consoles (`sysdm.cpl`, `services.msc`, `compmgmt.msc`, `regedit`, `cleanmgr`, `dfrgui`), technician maintenance directory, and direct firmware reboot (UEFI).
- **Technical Maintenance Reports**: Structured service order generation incorporating customer and machine details, an 8-point checklist of performed technical procedures, hardware specifications, and one-click export to plain text (`.txt`).
- **Zero-Dependency Architecture & Portability**: Operates standalone without third-party installers, runtimes beyond standard .NET Framework 4.8, or external NuGet dependencies. Features optional USB flash drive deployment, AutoRun generation, and local installation options.

---

## Important Notice

OptiAssist executes low-level administrative operations that directly interact with system configurations, Windows Registry hives, background services, scheduled tasks, and storage volumes.

This software is strictly intended for IT professionals, computer maintenance technicians, and advanced users. The authors and contributors assume no liability for system instability, data loss, or configuration issues resulting from the use or misuse of this software. Always review selected actions, automated scripts, and cleaning routines before executing them on production systems.

---

## Documentation

Comprehensive guides, module references, developer tutorials, and architecture specifications are available in the official documentation repository:

- [OptiAssist Technical Documentation Hub](https://github.com/jvfigueiro/optiassist/tree/main/Docs)

### Quick Links
- [System Architecture & Module Lifecycle](Docs/architecture.md)
- [Module Guides Index](Docs/modules/README.md)
- [Developer Guide: Creating Add-on Modules](Docs/how-to-create-a-module.md)
- [Build, Packaging, and Distribution](Docs/build-and-distribution.md)
- [Configuration, JSON Schemas, and Logging](Docs/configuration-and-logs.md)
- [FAQ and Troubleshooting Guide](Docs/faq-and-troubleshooting.md)

---

## System Requirements

- **Operating System**: Windows 10 (version 1809 or higher) or Windows 11 (64-bit recommended).
- **Framework**: Microsoft .NET Framework 4.8 (native on modern Windows 10 and 11 installations).
- **Privileges**: Administrator privileges (enforced via application manifest for hardware telemetry, registry modifications, service control, and system cleaning).
- **Network Connectivity**: Optional. The application operates 100% offline; an active connection is only required for Microsoft Winget package downloads or remote scripts.

---

## License

OptiAssist is free and open-source software licensed under the **GNU General Public License v3.0 (GNU GPLv3)**. You are free to run, study, share, and modify the application under the terms of this license.

For complete license terms, refer to the [LICENSE](LICENSE) file or access the built-in License view within the application (*Settings* > *License*).
