# OptiAssist Documentation

Welcome to the official technical documentation for **OptiAssist**, the portable Windows optimization, maintenance, and post-installation workbench designed for IT support technicians, system administrators, and repair workshops.

---

## 📚 Documentation Chapters

| Chapter | Topic | Description |
|---|---|---|
| [**01. Dashboard & Hardware Inspection**](01-DASHBOARD.md) | System Overview | Hardware discovery, WMI specs, Serial Tag, BitLocker, TPM, and UEFI reboot. |
| [**02. App Manager & Post-Install**](02-APP-MANAGER.md) | Software Automation | Offline installer deployment, Winget fallback, batch upgrades, and `autoinstall.json`. |
| [**03. System Optimization & Repairs**](03-OPTIMIZATION.md) | Tweaks & Fixes | Deep dive into all 16 cleanup, repair, and performance tuning utilities. |
| [**04. Windows Administrative Tools**](04-WINDOWS-TOOLS.md) | Native Consoles | Quick access to MMC management consoles, GPEDIT support, and portable tools. |
| [**05. Maintenance Service Report**](05-MAINTENANCE-REPORT.md) | Client Deliverables | Service Provider Mode, hardware logging, procedure checklist, and report exports. |
| [**06. Settings & Localization**](06-SETTINGS-AND-LOCALIZATION.md) | Configuration | Dynamic JSON multi-language engine, dark/light themes, accent colors, and persistence. |

---

## 🛠️ Core Principles of OptiAssist

1. **Portability First**: OptiAssist runs directly from a USB flash drive or external SSD without installation. All configurations, language files, offline installers, and scripts reside alongside the executable.
2. **Native Windows Architecture**: Built with modern WPF (.NET Framework 4.8 / XAML), avoiding heavy web runtimes or third-party bloat.
3. **Elevated Privileges**: Requires Administrator rights (`requireAdministrator`) to perform low-level registry adjustments, service management, and WMI hardware queries.
4. **Offline First with Winget Fallback**: Deploys software locally without internet whenever possible, falling back gracefully to Microsoft's official Winget repository when connected.
5. **Real-time Diagnostic Logging**: Every operation streams its console output to the dedicated Logs panel, providing full transparency and exportable audit trails.

---

## 📂 Portable Directory Structure

```plaintext
OptiAssist/
├── OptiAssist.exe               # Main compiled executable
├── OptiAssist.exe.manifest      # Elevation manifest (requireAdministrator)
├── Config/
│   ├── settings.json            # Application settings (Theme, Language, Modes)
│   ├── autoinstall.json         # Automated software installation catalog
│   └── Languages/
│       ├── en-US.json           # English (US) dictionary
│       └── pt-BR.json           # Portuguese (Brazil) dictionary
├── PostInstall/                 # Offline installers directory (.exe, .msi, etc.)
├── Maintenance/                 # Portable third-party tools folder
├── Reports/                     # Saved maintenance service reports (.txt)
├── Scripts/                     # Custom maintenance scripts (.bat, .ps1, .reg)
└── Docs/                        # Full technical documentation
```
