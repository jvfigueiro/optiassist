# OptiAssist Documentation

Welcome to the official technical documentation for **OptiAssist**, the portable Windows optimization, maintenance, post-installation, and command execution workbench designed for IT support technicians, system administrators, and repair workshops.

---

## 📚 Documentation Chapters

| Chapter | Topic | Description |
|---|---|---|
| [**01. Dashboard & Hardware Inspection**](01-DASHBOARD.md) | System Overview | Hardware telemetry, WMI specs, Serial Tag copy, BitLocker, TPM, Secure Boot, and 3x3 Common Tasks. |
| [**02. App Manager & Post-Install**](02-APP-MANAGER.md) | Software Automation | Offline installer deployment (`Installers/`), Winget fallback, batch upgrades scanner, and `autoinstall.json`. |
| [**03. Toolbox & System Optimization Hub**](03-TOOLBOX.md) | Command Runner & Fixes | Custom command engine (CMD, PowerShell, Binary), category manager, options drawer, external log popup setting, and 16+ built-in presets. |
| [**04. Windows Administrative Tools**](04-WINDOWS-TOOLS.md) | Native Consoles | Quick access to 18 MMC management consoles (including services.msc), GPEDIT Home edition handling, and the portable `Maintenance/` folder. |
| [**05. Maintenance Service Report**](05-MAINTENANCE-REPORT.md) | Client Deliverables | Modular DLL add-on, in-module options drawer for contact info, hardware logging, 8-point checklist, and multi-destination report export. |
| [**06. Settings, Logs & Personalization**](06-SETTINGS-AND-LOCALIZATION.md) | Configuration & Theming | Central logs & floating LogWindow, dynamic zero-restart i18n, custom app logo, themes, accent colors, USB AutoRun, and 1-click install/uninstall. |
| [**07. Add-ons & Modular DLL Architecture**](07-ADDONS-AND-MODULES.md) | Extensibility & Plugins | Non-locking assembly loader (`Config/Addons`), BleachBit-style System Cleaner v2.0, Maintenance Report, and Toolbox DLL. |

---

## Transversal Engineering Guides & Module Documentation

In addition to the overview chapters above, comprehensive engineering guides and per-module specifications are available:

* [**System Architecture & Module Lifecycle**](architecture.md): Detailed Microkernel design, sequence diagrams, and anti-lock assembly loading.
* [**Developer Guide: How to Create a Module**](how-to-create-a-module.md): Step-by-step tutorial with complete code examples.
* [**Build, Packaging, and Distribution**](build-and-distribution.md): Build targets, self-deployment, and uninstallation mechanisms.
* [**Configuration, JSON Schemas, i18n, and Logging**](configuration-and-logs.md): Data dictionary and real-time logging pipelines.
* [**FAQ and Troubleshooting**](faq-and-troubleshooting.md): Code-based explanations of platform edge cases and error codes.
* [**Complete Architecture Map & Code Analysis**](analysis/00-map.md): Exhaustive codebase analysis and platform integrations.
* [**Individual Module Specifications Index**](modules/README.md): Detailed guides for all native and add-on modules.

---

## 🛠️ Core Principles of OptiAssist

1. **100% Portability First**: OptiAssist runs directly from a USB flash drive, external SSD, or network share without requiring an installation process. All configuration, languages, offline installers, scripts, and plugins reside alongside the executable.
2. **Native Windows Architecture**: Engineered with high-performance WPF (.NET Framework 4.8 / XAML), avoiding heavy web runtimes, Electron frameworks, or unnecessary dependencies (~125 KB standalone base executable).
3. **Elevated Privileges & 64-Bit System Access**: Compiled strictly in 64-bit mode (`Prefer32Bit = false`) and enforces Administrator execution (`requireAdministrator` manifest + startup check). This ensures direct, unredirected access to `System32`, the native Registry, and low-level WMI classes.
4. **Offline First with Winget Fallback**: Deploys software locally at full hardware speed without requiring internet connectivity, falling back gracefully to Microsoft's official Winget repository when an active connection exists.
5. **Real-time Diagnostic Logging & Pop-out Window**: Every operation streams its console output live to the central Logs console and the independent, floating `LogWindow`, providing full transparency and exportable audit trails.
6. **BleachBit-Inspired Cleaning Engine**: Distinguishes between non-destructive Dry-Run (`Scan()`) and safe Execution (`Clean()`), utilizing `PathSanitizer` scope protection and locked-file resilience.
7. **Modular Extensibility**: First-class plugin system utilizing standalone compiled DLLs loaded into memory without file-locking, allowing technicians to add, toggle, or remove features at runtime.
8. **Strict Software Legality**: Engineered exclusively with legitimate tools, native APIs, and verified procedures. 100% free of illicit activation software.

---

## 📂 Portable Directory Structure

```text
📁 OptiAssist/
│
├── OptiAssist.exe                   # Standalone compiled 64-bit executable (~125 KB)
├── OptiAssist.csproj                # MSBuild project file for main executable
├── build.bat                        # 1-click batch compiler (Main app + All Add-ons)
├── README.md                        # Master overview and quick reference
│
├── 📁 Config/                       # Configuration, translation dictionaries & plugins
│   ├── settings.json                # User preferences, branding, themes, active add-ons
│   ├── autoinstall.json             # Automated software installation catalog (Local/Winget)
│   ├── custom_logo.png              # Custom application logo image (optional)
│   ├── 📁 Languages/                # Dynamic multi-language JSON dictionaries
│   │   ├── en-US.json               # English (United States) [472 keys]
│   │   └── pt-BR.json               # Português (Brasil) [472 keys]
│   └── 📁 Addons/                   # Modular plugin assemblies
│       ├── README.md                # Add-on developer guide
│       ├── 📁 SystemCleaner/
│       │   └── SystemCleaner.dll    # BleachBit-style system cleaner & optimizer
│       ├── 📁 MaintenanceReport/
│       │   └── MaintenanceReport.dll# Service order & maintenance report generator
│       └── 📁 Toolbox/
│           ├── Toolbox.dll          # Custom command runner & optimization hub
│           ├── toolbox.json         # User commands & repair presets database
│           └── 📁 Scripts/          # Automation helper scripts (.ps1, .reg, .bat)
│               ├── tweaks_interface.ps1
│               ├── fix_printer_rpc.reg
│               └── reset_network.bat
│
├── 📁 Installers/                   # Offline software installers (.exe, .msi, etc.)
│   └── README.txt
│
├── 📁 Maintenance/                  # Portable third-party tools (CPU-Z, CrystalDiskInfo)
│   └── README.txt
│
├── 📁 Reports/                      # Saved maintenance service reports (.txt)
│   └── README.txt
│
├── 📁 Docs/                         # Complete technical documentation chapters
│   ├── README.md                    # Documentation index & principles
│   ├── 01-DASHBOARD.md              # Hardware telemetry, WMI queries, security badges
│   ├── 02-APP-MANAGER.md            # Offline installer deployment, Winget engine
│   ├── 03-TOOLBOX.md                # Command runner, categories, repair presets
│   ├── 04-WINDOWS-TOOLS.md          # 17 administrative consoles, GPEDIT handling
│   ├── 05-MAINTENANCE-REPORT.md     # Service orders, checklist, report exports
│   ├── 06-SETTINGS-AND-LOCALIZATION.md # Dynamic i18n, themes, logs, USB AutoRun
│   └── 07-ADDONS-AND-MODULES.md     # Plugin architecture, BaseCleaner, specs
│
└── 📁 src/                          # Full modular WPF source code (.NET 4.8 / WPF)
    ├── App.xaml / App.xaml.cs       # Application entry, elevation check, themes
    ├── MainWindow.xaml / .cs        # Main UI grid, navigation, status bar
    ├── LogWindow.cs                 # Pop-out independent floating log window
    ├── AddonHelper.cs               # Non-locking assembly loader & contracts
    ├── SystemInfoHelper.cs          # WMI hardware, OS, and security queries
    ├── InstallerHelper.cs           # Offline installer & Winget upgrade runner
    ├── ShortcutsHelper.cs           # Windows administrative tool launchers
    ├── SettingsHelper.cs            # settings.json serialization & persistence
    ├── LocalizationHelper.cs        # Dynamic i18n JSON translation engine
    └── 📁 Addons/                   # Standalone add-on project sources
        ├── 📁 SystemCleaner/        # SystemCleaner.csproj & BaseCleaner classes
        ├── 📁 MaintenanceReport/    # MaintenanceReport.csproj & ReportAddon
        └── 📁 Toolbox/              # Toolbox.csproj, Storage, Runner, Addon
```
