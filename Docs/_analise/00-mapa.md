# Architecture and Code Analysis Report: OptiAssist

**Document:** `docs/_analise/00-mapa.md`  
**Date:** November 05, 2026  
**System Version:** 1.1.0  
**Software Author:** João Figueiró  
**License:** GNU General Public License v3.0 (GNU GPLv3)  

---

## 1. Technology Stack

### 1.1 Programming and Markup Languages
* **C# (C Sharp 7.3+)**: Core programming language of the application and all modular add-ons (.NET Class Libraries). All code is written for native compatibility with the standard .NET Framework compiler.
* **XAML (Extensible Application Markup Language)**: Used in the host application (`src/MainWindow.xaml`, `src/App.xaml`) for declarative layout definitions, high-contrast Dark and Light themes, dynamic resources, color brushes, and WPF control templates.
* **PowerShell 5.1 / Core**: Secondary script interpreter for complex maintenance automation, community utilities (Chris Titus WinUtil), network time protocol (NTP) synchronization, and internal Toolbox commands.
* **Batch / CMD (Windows Command Processor)**: Used in the build automation script (`build.bat`), uninstallation cleanup routines, and classic system repair utilities (`chkdsk`, `compact`, `sfc`, `dism`, `ping`).
* **JSON (JavaScript Object Notation)**: Primary serialization format for user preferences (`Config/settings.json`), dashboard common tasks (`Config/custom_shortcuts.json`), toolbox command catalogs (`Config/toolbox.json`), post-installation software catalogs (`Config/autoinstall.json`), and multi-language translation dictionaries (`Config/Languages/*.json`).

### 1.2 Runtime and Frameworks
* **Runtime**: Microsoft .NET Framework 4.8 (CLR 4.0.30319).
* **UI Framework**: Windows Presentation Foundation (WPF), leveraging native DirectX hardware rendering acceleration.
* **Zero External Dependencies (Zero Third-Party NuGets)**: OptiAssist was purposefully engineered without third-party libraries (no `Newtonsoft.Json`, `MahApps.Metro`, `Costura.Fody`). JSON parsing and writing, dynamic plugin lifecycle, modern UI components, and reactive styling are implemented entirely with native .NET Framework BCL assemblies and Windows Win32/COM platform APIs.

### 1.3 Core .NET Assemblies and Namespaces

| Assembly / Namespace | Project Purpose and Usage |
| :--- | :--- |
| `PresentationFramework.dll` / `PresentationCore.dll` / `WindowsBase.dll` | Core WPF graphics engine: windows, `Grid`, `StackPanel`, `Border`, `ScrollViewer`, `ItemsControl`, templates, animations, and data binding. |
| `System.Xaml.dll` | Compilation and parsing of declarative host XAML assets. |
| `System.Management.dll` | Communication with WMI (Windows Management Instrumentation) and CIM for hardware telemetry: CPU, GPU, RAM, Motherboard, BIOS, TPM, BitLocker, Windows activation status, and OS details. |
| `System.ServiceProcess.dll` | Windows service monitoring, starting, and stopping (e.g. `wuauserv` for Windows Update cache clearing, `DiagTrack` telemetry services). |
| `Microsoft.Win32.dll` | Native Windows Registry access (`Registry.LocalMachine`, `Registry.CurrentUser`) for autostart inspection (`Run`, `StartupApproved`), Control Panel uninstall registrations, Secure Boot posture, and OS version detection. |
| `System.Drawing.dll` / `System.Windows.Forms.dll` | Native Windows dialog wrappers: folder browsing (`FolderBrowserDialog`) and custom accent color selection (`ColorDialog`). |
| `System.Diagnostics.Process` | Process orchestration, execution of external binaries, asynchronous streaming of stdout/stderr to real-time console buffers, and PID monitoring. |

### 1.4 Win32 APIs, P/Invoke, and COM Integrations

* **P/Invoke (Shell32.dll)**:
  * `SHEmptyRecycleBin`: Empties the Recycle Bin across all drives without blocking dialog prompts (`SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND`).
* **P/Invoke (User32.dll)**:
  * `SendMessageTimeout`: Broadcasts system-wide notification (`WM_SETTINGCHANGE`, `HWND_BROADCAST`) to force Windows Explorer to refresh metric updates without rebooting.
* **Late-Bound COM Interop (ActiveX / ProgID)**:
  * `WScript.Shell` (ProgID): Programmatically creates `.lnk` shortcuts (TargetPath, WorkingDirectory, IconLocation, Description) for Desktop and Start Menu deployment.
  * `Schedule.Service` (ProgID): Connects to the Windows Task Scheduler 2.0 API, enabling recursive folder enumeration, boot/logon triggers, and enabling/disabling tasks in the Startup Manager.

### 1.5 Build and Deployment Tools
* **Build Engine**: Microsoft Build Engine (`MSBuild.exe` v4.8) via target `CoreBuild` or batch script `build.bat`.
* **Module Orchestration**: The main project `OptiAssist.csproj` contains the `CleanupLocalArtifacts` target, which sequentially compiles all add-on projects (`AppManager.csproj`, `SystemCleaner.csproj`, `MaintenanceReport.csproj`, `Toolbox.csproj`, `StartupManager.csproj`).
* **Native Installer (`AppDeploymentHelper`)**:
  * Does not require Inno Setup, NSIS, or external installers. The `OptiAssist.exe` binary includes full self-deployment logic for Portable mode, Standard installation to `%ProgramFiles%\OptiAssist`, Windows Control Panel registration (`SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist`), and USB `autorun.inf` generation.

---

## 2. Directory Layout and Responsibilities

```
OptiAssist/
├── .git/                                   # Git version control repository
├── Config/                                 # Persistent configuration and runtime data
│   ├── settings.json                       # Global application settings
│   ├── autoinstall.json                    # Post-install software catalog
│   ├── toolbox.json                        # Toolbox scripts and repair presets
│   ├── custom_shortcuts.json               # Dashboard common task shortcuts
│   ├── Languages/                          # Multi-language translation dictionaries
│   │   ├── en-US.json                      # English (US) dictionary
│   │   └── pt-BR.json                      # Portuguese (Brazil) dictionary
│   └── Addons/                             # Compiled decoupled add-on DLLs
│       ├── AppManager/AppManager.dll
│       ├── MaintenanceReport/MaintenanceReport.dll
│       ├── StartupManager/StartupManager.dll
│       ├── SystemCleaner/SystemCleaner.dll
│       └── Toolbox/Toolbox.dll
├── Docs/                                   # Full technical documentation
│   ├── arquitetura.md                      # Modular system architecture
│   ├── como-criar-um-modulo.md             # Developer guide for creating addons
│   ├── build-e-distribuicao.md             # Compilation and deployment guide
│   ├── configuracao-e-logs.md              # Configuration schemas, i18n, and logs
│   ├── faq-e-troubleshooting.md            # FAQ and edge-case troubleshooting
│   ├── modulos/                            # Individual module documentation
│   └── _analise/00-mapa.md                 # Complete architectural map
├── Maintenance/                            # Portable third-party diagnostic utilities
│   ├── CPU-Z/                              # Portable CPU-Z
│   └── CrystalDiskInfo/                    # Portable CrystalDiskInfo
├── Installers/                             # Offline installer storage repository
├── Reports/                                # Default maintenance report output directory
├── src/                                    # Host core source code
│   ├── app.manifest                        # Elevation manifest (requireAdministrator)
│   ├── App.xaml / App.xaml.cs              # Application entry point and CLI args
│   ├── MainWindow.xaml / MainWindow.xaml.cs# Host shell and navigation coordinator
│   ├── AddonHelper.cs                      # Dynamic module loader and contracts
│   ├── LocalizationHelper.cs               # Multi-language dictionary engine
│   ├── SettingsHelper.cs                   # Zero-dependency JSON parser/serializer
│   ├── ShortcutsHelper.cs                  # Deployment and shortcut helper
│   ├── SystemInfoHelper.cs                 # Hardware telemetry and WMI collector
│   ├── TweakHelper.cs                      # Registry tweaks and process helpers
│   └── LogWindow.cs                        # Detached floating log console
├── src/Addons/                             # Add-on Class Library projects
│   ├── AppManager/                         # AppManager.csproj & source files
│   ├── MaintenanceReport/                  # MaintenanceReport.csproj & source files
│   ├── StartupManager/                     # StartupManager.csproj & source files
│   ├── SystemCleaner/                      # SystemCleaner.csproj & source files
│   └── Toolbox/                            # Toolbox.csproj & source files
├── OptiAssist.csproj                       # Primary host project file
├── OptiAssist.sln                          # Master Visual Studio solution
├── build.bat                               # Automated build script
└── README.md                               # Root project overview and guide
```

---

## 3. Modular Architecture: Discovery, Lifecycle, and Contracts

OptiAssist uses a microkernel architecture where modules reside in independent assemblies:

### 3.1 Interface Contracts (`IOptiAssistAddon` & `IOptiAssistHost`)

- **`IOptiAssistAddon`**: Implemented by all plugin DLLs:
  - `Id`, `Name`, `Description`, `Version`, `Author`, `Icon`.
  - `Initialize(IOptiAssistHost host)`: Injects host services.
  - `CreateView()`: Returns the WPF `FrameworkElement`.
  - `OnLanguageChanged(string langCode)`: Triggers runtime string updates.
- **`IOptiAssistHost`**: Implemented by `MainWindow`:
  - `Log(string message)`: Dispatches messages to real-time console buffers.
  - `GetLocalizedString(string key, string defaultValue)`: Fetches localized strings.
  - `CurrentLanguage`, `Settings`, `Hardware`, `SaveSettings()`, `OpenLogWindow()`.

### 3.2 Dynamic Loading without File-Locking (Anti-Lock)
1. **Directory Scan**: Scans `Config\Addons\*\*.dll`.
2. **Byte Array Loading**: Uses `File.ReadAllBytes(dllPath)` followed by `Assembly.Load(rawBytes)`. This leaves the DLL unlocked on disk, allowing live replacements.
3. **Assembly Resolution Hook**: Handles `AppDomain.CurrentDomain.AssemblyResolve` to bind external DLLs back to host interfaces.
4. **Lazy View Initialization**: Invokes `CreateView()` only on first tab selection, caching the resulting `FrameworkElement`.

---

## 4. Module Inventory

| Module Name | Type | Key Source Files | Dependencies | Admin Required? |
| :--- | :--- | :--- | :--- | :---: |
| **Dashboard** | Native Host Tab | `src/MainWindow.xaml.cs`, `src/SystemInfoHelper.cs` | WMI, Registry, `custom_shortcuts.json` | Yes |
| **App Manager** | Decoupled Add-on | `src/Addons/AppManager/*` | Winget, `autoinstall.json`, Local Installers | Yes |
| **Toolbox** | Decoupled Add-on | `src/Addons/Toolbox/*` | PowerShell, CMD, `toolbox.json` | Yes |
| **System Cleaner**| Decoupled Add-on | `src/Addons/SystemCleaner/*` | Shell32, Chromium SQLite, Event Logs | Yes |
| **Startup Manager**| Decoupled Add-on| `src/Addons/StartupManager/*`| Registry (`StartupApproved`), Task Scheduler COM | Yes |
| **Windows Tools** | Native Host Tab | `src/MainWindow.xaml.cs`, `src/ShortcutsHelper.cs` | Win32 Process, Native `.msc`/`.cpl` consoles | Yes |
| **Maintenance Report**| Decoupled Add-on| `src/Addons/MaintenanceReport/*`| HardwareSummary, Local File System | No (inherits Yes) |
| **Settings & Deployment**| Native Host Tab| `src/MainWindow.xaml.cs`, `src/ShortcutsHelper.cs`| `settings.json`, COM WScript.Shell, Registry | Yes |
| **Logs Terminal** | Native Host Tab | `src/MainWindow.xaml.cs`, `src/LogWindow.cs` | Dispatcher, Windows Clipboard | No (inherits Yes) |
| **About** | Native Host Tab | `src/MainWindow.xaml.cs` | `AddonHelper.LoadedAddons`, AssemblyInfo | No |

---

## 5. Windows Platform Integrations and Win 10 vs 11 Differences

1. **WMI and CIM Telemetry**:
   - `root\cimv2` (`Win32_Processor`, `Win32_BaseBoard`, `Win32_BIOS`, `Win32_PhysicalMemory`).
   - TPM 2.0: Evaluated via `Win32_PnPEntity` (SecurityDevices PNPClass) and `Win32_Tpm`.
   - BitLocker: Evaluated via `Root\CIMV2\Security\MicrosoftVolumeEncryption`. On Windows 11 Home, queries safely detect OEM "Device Encryption".
2. **Winget Package Manager**:
   - Included natively on Windows 11 and modern Windows 10 (via Microsoft Store App Installer).
   - Absent on early Windows 10 LTSC/LTSB builds; handled via local installer fallback.
3. **UEFI Software Reboot (`shutdown /r /fw /t 0`)**:
   - Supported on native UEFI systems. Rejection by legacy BIOS/MBR configurations is caught with error `0x5B3`.
4. **USB AutoRun Restrictions**:
   - Silent code execution via `autorun.inf` is blocked on Windows 10/11. The file successfully supplies the custom drive icon and explorer label.

---

## 6. Configuration, Logging, and Error Handling

* **Zero-Dependency JSON**: Robust custom parsing in [`SettingsHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/SettingsHelper.cs).
* **Multi-Language Engine**: Central dictionary in [`LocalizationHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/LocalizationHelper.cs), dynamic add-on merging, and zero-restart hot-reloading.
* **Thread-Safe Logging**: All background threads safely marshal messages to UI buffers via `Dispatcher.Invoke`. Floating `LogWindow` synchronizes in real time and exports to `.txt`.

---

## 7. Limitations and Environment-Dependent Aspects

1. **Firmware Reboot Support**: Depends on motherboard UEFI capability.
2. **Winget Availability**: Dependent on Windows Store App Installer presence.
3. **Virtual Machine Telemetry**: Generic strings (`"None"`, `"To be filled by O.E.M."`) are sanitized to `"Not specified"`.
4. **AppLocker / Domain SRP Policies**: Strict corporate environments may restrict execution of unsigned PowerShell commands outside Windows directories.
