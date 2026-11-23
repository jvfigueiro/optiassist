# OptiAssist Architecture & Codebase Map (`v1.2.S7`)

This document is the comprehensive architectural map of **OptiAssist (`1.2.0.7` / `1.2.S7`)** derived from static inspection of the repository source code, project files, configuration catalogs, and build/test tooling.

---

## 1. Stack, Runtime, Dependencies & Build Tooling

- **Primary Language & UI Framework:** C# 5 (strictly compatible with the .NET Framework 4.8 C# compiler) and **WPF** (`PresentationFramework`, `PresentationCore`, `WindowsBase`, `System.Xaml`).
- **Target Runtime:** Microsoft **.NET Framework 4.8** (`v4.8`, `AnyCPU`, `<Prefer32Bit>false</Prefer32Bit>`), executing natively on Windows 10 (1809+) and Windows 11 with UAC elevation (`requireAdministrator` in `src/app.manifest`).
- **Host & Core Assemblies:** `System`, `System.Core`, `System.Data`, `System.Drawing`, `System.Management` (WMI/CIM), `System.ServiceProcess`, `System.Windows.Forms` (`ColorDialog`), `Microsoft.CSharp`.
- **Third-Party Dependencies (see [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) & [SOURCE-INFO.md](../../SOURCE-INFO.md)):**
  - **`LibreHardwareMonitorLib` `0.9.6.0` (`MPL-2.0`)** + transitive dependencies (`BlackSharp.Core 1.0.7` `MPL-2.0`, `DiskInfoToolkit 1.1.2` `MPL-2.0`, `RAMSPDToolkit-NDD 1.4.2` `MPL-2.0`, `HidSharp 2.6.4.0` `Apache-2.0`, and Microsoft BCL packages `System.Buffers 4.6.1`, `System.Memory 4.6.3`, `System.Numerics.Vectors 4.6.1`, `System.Runtime.CompilerServices.Unsafe 6.1.2` `MIT`): staged in `lib/LibreHardwareMonitor/` at build time and deployed to `Config/Addons/PCAssist/` for live hardware sensor telemetry.
  - **BleachBit CleanerML XML definitions (`GPL-3.0-or-later`):** declarative cleaning rules in `Config/Addons/SystemCleaner/cleaners/*.xml` parsed by OptiAssist's native C# `CleanerMlEngine`.
  - **PCI IDs (`Config/Data/pci.ids.min`, `BSD-3-Clause`):** curated vendor/device lookup database consumed by `OptiAssist.Platform.PciIdsDatabase`.
  - **Microsoft Fluent UI System Icons (`MIT`):** 24×24 SVG `<Path>` geometries embedded in `src/FluentIcons.cs`.
  - **Optional Runtime External Tools (Not Bundled):** `winget.exe` (Windows Package Manager), `PawnIO_setup.exe` (downloaded on-demand from `namazso/PawnIO.Setup`), and Chris Titus Tech `WinUtil` (invoked remotely via `irm https://christitus.com/win | iex`).
- **Build, Packaging & Validation Tooling:**
  - `MSBuild.exe` (`v4.0.30319` 64-bit) invoked via `build.bat` or directly on `OptiAssist.csproj` (which cascades builds to all 6 Add-on `.csproj` files via the `CleanupLocalArtifacts` target).
  - `csc.exe` for standalone C# test suites (`tests/PlatformDiagnosticTests.cs`, `tests/DebloaterEndToEndTests.cs`, `tests/DeploymentComplianceTests.cs`).
  - `7z.exe` or Windows built-in `tar.exe` (`bsdtar --format=7zip`) for portable `.7z` packaging, validated by `tests/Verify-ReleasePackage.ps1`.
  - Built-in autonomous installer/deployer (`AppDeploymentHelper` in `src/ShortcutsHelper.cs`) — no external setup builder (Inno/NSIS/WiX) is used.

---

## 2. Folder Structure & Responsibilities

| Path | Responsibility |
| :--- | :--- |
| `OptiAssist.csproj`, `build.bat`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `SOURCE-INFO.md`, `THIRD-PARTY/` | Main MSBuild project, build launcher, GPL-3.0 license, third-party notices, source provenance, and full third-party license texts. |
| `src/` | Host executable (`OptiAssist.exe`) source: `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `AddonHelper.cs`, `SettingsHelper.cs`, `LocalizationHelper.cs`, `SystemInfoHelper.cs`, `TweakHelper.cs`, `ShortcutsHelper.cs` (`ModernChromeHelper` + `AppDeploymentHelper`), `LogWindow.cs`, `OptiDialog.cs`, `FluentIcons.cs`, `AssemblyInfo.cs`, `app.manifest`. |
| `src/Platform/` | Hardware profiling & compatibility engine: `PlatformInfo.cs`, `MicroarchDatabase.cs`, `PciIdsDatabase.cs`, `PlatformProfiler.cs`, `ProfileRecommender.cs`, `Win11CompatAdvisor.cs`, `KnownIssuesEngine.cs`, `PlatformDiagnosticService.cs`. |
| `src/Debloater/` | OptiBoost declarative optimization engine & Windows Tools sub-tabs UI: `DebloaterCatalog.cs`, `DebloaterEngine.cs`, `SystemRestoreHelper.cs`, `WindowsToolsTabsBuilder.cs`. |
| `src/Addons/<Module>/` | Source code for the 6 decoupled Class Library add-on DLLs: `AppManager`, `MaintenanceReport`, `PCAssist`, `StartupManager`, `SystemCleaner`, `Toolbox`. |
| `lib/LibreHardwareMonitor/` | Build-time reference copies of `LibreHardwareMonitorLib.dll` and its 8 transitive dependency DLLs. |
| `Config/` | Runtime configuration root: `settings.json`, `custom_shortcuts.json`, `Languages/*.json`, `Data/` (`microarch.json`, `pci.ids.min`, `known_issues.json`), `Debloater/*.json`, and `Addons/<Module>/` (compiled DLLs, per-module `en-US.json`/`pt-BR.json`, catalogs, and CleanerML XMLs). |
| `Installers/`, `Maintenance/` | Optional portable technician directories (auto-created in Portable Mode if absent; excluded from mandatory local PC installs). |
| `tests/` | Automated QA & compliance verification suites (`PlatformDiagnosticTests.cs`, `DebloaterEndToEndTests.cs`, `DeploymentComplianceTests.cs`, `Verify-ReleasePackage.ps1`). |
| `Docs/` | Technical architecture, module guides, build/release docs, and troubleshooting documentation. |

---

## 3. Modular Plugin Architecture

### 3.1 Contracts (`src/AddonHelper.cs`)
- **`IOptiAssistAddon`**: Implemented by every external add-on DLL (`Id`, `Name`, `Description`, `Version`, `Author`, `Icon`, `Initialize(IOptiAssistHost host)`, `FrameworkElement CreateView()`, `OnLanguageChanged(string langCode)`).
- **`IOptiAssistHost`**: Exposed by `MainWindow` to every add-on (`Log`, `GetLocalizedString`, `CurrentLanguage`, `Settings`, `Hardware`, `Platform`, `PlatformDiagnostic`, `IsPortableMode`, `SaveSettings`, `OpenLogWindow`).

### 3.2 Discovery, Dependency Preloading & Anti-Lock Loading
1. `AddonHelper.LoadDiscoveredAddons(...)` scans `Config\Addons\<ModuleName>\` (supporting both `<ModuleName>.dll` and nested `*.dll` while skipping known third-party dependency names via `IsKnownDependencyDllName`).
2. Before loading a module DLL, `AddonHelper.PreloadSiblingDependencies(...)` topologically preloads any co-located dependency DLLs (`System.Runtime.CompilerServices.Unsafe` -> `System.Buffers` -> `System.Numerics.Vectors` -> `System.Memory` -> `BlackSharp.Core` -> `HidSharp` -> `DiskInfoToolkit` -> `RAMSPDToolkit-NDD` -> `LibreHardwareMonitorLib`) via `File.ReadAllBytes` + `Assembly.Load(byte[])` into the default load context and caches them in `_resolvedDepCache`. This prevents `LoadFrom` context mismatches and Windows Mark-of-the-Web (`Zone.Identifier`) blocks on physical hardware.
3. The add-on DLL itself is also loaded via `File.ReadAllBytes` + `Assembly.Load(byte[])`, ensuring zero file locks on disk so modules can be updated or removed at runtime.
4. `AppDomain.CurrentDomain.AssemblyResolve` hooks `OnAddonAssemblyResolve` to return cached assemblies when .NET requests dependencies.
5. `AddonHelper.LoadAddonLocalization(...)` merges `Config\Addons\<ModuleName>\<langCode>.json` into `LocalizationHelper`, and reads compiled `AssemblyInfo` attributes (`Title`, `Description`, `Company`, `Product`, `Copyright`, `Version`) into `AddonEntry`.

### 3.3 Lifecycle & UI Consumption
- **Startup (`MainWindow_Loaded`):** Discovers add-ons, sorts them by `ModuleOrder` (`toolbox`, `app-manager`, `system-cleaner`, `startup-manager`, `hardware-monitor`, `maintenance-report`), and builds sidebar `RadioButton` items (`BtnNav` style) for enabled add-ons.
- **Lazy View Creation (`NavButton_Click`):** `entry.GetOrCreateView()` invokes `IOptiAssistAddon.CreateView()` only on first selection (showing `pnlAddonLoadingOverlay` while constructing) and caches the `FrameworkElement` in `AddonEntry.CachedView`.
- **Vector Icon Rendering (`FluentIcons.cs`):** `App.xaml` templates (`BtnNav`, `BtnDefault`, `BtnAccent`, `BtnDanger`, `BtnRowAction`) use `FluentContentConverter`, and `FluentIcons.UpgradeVisualTree(...)` upgrades standalone leading-emoji `TextBlock`s across both core views and dynamically loaded add-on views.

---

## 4. Complete Module & Feature Inventory

| Module / Feature | Purpose | Primary Files | Internal Dependencies | Admin Required |
| :--- | :--- | :--- | :--- | :--- |
| **Dashboard** | Health score ring (%), integrated diagnostic recommendations, 2x2 Bento hardware/OS/storage cards, customizable Common Tasks | `src/MainWindow.xaml(.cs)`, `src/SystemInfoHelper.cs`, `src/Platform/*` | `LocalizationHelper`, `SettingsHelper`, `CustomShortcutsHelper`, `PlatformDiagnosticService` | Yes (manifest) |
| **Windows Tools (`OptiBoost` & `Compatibility Checker`)** | Native admin consoles, 7-profile declarative OptiBoost engine with snapshots/rollback, Hardware Specs & Known Issues | `src/Debloater/*.cs`, `Config/Debloater/*.json`, `src/Platform/*.cs` | `PlatformDiagnosticService`, `SystemRestoreHelper`, `OptiDialog` | Yes |
| **Terminal (External Logs)** | Real-time diagnostic log streaming in Windows Terminal (`wt.exe`) / PowerShell / CMD + `LogWindow` | `src/MainWindow.xaml.cs`, `src/LogWindow.cs` | `LocalizationHelper`, `ModernChromeHelper` | Inherited |
| **Toolbox** (`toolbox`) | Script & command runner (PowerShell Base64, CMD, executables), preset repair hub, WinUtil launcher | `src/Addons/Toolbox/*.cs`, `Config/Addons/Toolbox/toolbox.json` | `IOptiAssistHost`, `OptiDialog` | Yes |
| **App Manager** (`app-manager`) | 3-tab software suite: Batch Installer (7-Zip/Chrome/Office default), App Updates (Winget + Store), Manage Apps | `src/Addons/AppManager/*.cs`, `Config/Addons/AppManager/autoinstall.json` | `IOptiAssistHost`, `ShortcutsHelper`, `OptiDialog` | Yes |
| **System Cleaner** (`system-cleaner`) | BleachBit CleanerML XML scanner/cleaner, `PathSanitizer`, SQLite vacuum, backup manager | `src/Addons/SystemCleaner/*.cs`, `Config/Addons/SystemCleaner/cleaners/*.xml` | `IOptiAssistHost`, `winsqlite3.dll`, `OptiDialog` | Yes |
| **Startup Manager** (`startup-manager`) | Registry `Run`/`RunOnce` + `StartupApproved`, Startup folders, COM Task Scheduler manager | `src/Addons/StartupManager/*.cs` | `IOptiAssistHost`, `Schedule.Service` COM | Yes |
| **Hardware Monitor** (`hardware-monitor`) | System summary, `LibreHardwareMonitorLib` Sensors (Beta) + optional PawnIO, Battery, Bench, Device Inventory | `src/Addons/PCAssist/*.cs`, `src/Addons/PCAssist/Core/*.cs` | `IOptiAssistHost`, `LibreHardwareMonitorLib.dll` + 8 DLLs | Yes |
| **Maintenance Report** (`maintenance-report`) | Service order & 8-step technician checklist report generator (`.txt` export) | `src/Addons/MaintenanceReport/*.cs` | `IOptiAssistHost`, `OptiDialog` | No (optional) |
| **Settings & Installer** | Theme/accent/logo customization, i18n hot-reload, Add-on manager, `AppDeploymentHelper` (PC install / Portable Mode) | `src/MainWindow.xaml(.cs)`, `src/SettingsHelper.cs`, `src/ShortcutsHelper.cs` | `AddonHelper`, `LocalizationHelper`, `OptiDialog` | Yes (PC install) |

---

## 5. Windows Integration Points & OS Differences

- **WMI / CIM (`System.Management`):** `Win32_Processor`, `Win32_ComputerSystem`, `Win32_BaseBoard`, `Win32_BIOS`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_DiskDrive`, `Win32_OperatingSystem`, `Win32_PnPEntity`, `MSFT_PhysicalDisk`, `SoftwareLicensingProduct`.
- **Windows Registry (`Microsoft.Win32`):** OS build/edition (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`), Secure Boot/UEFI, Defender status, Startup `Run`/`RunOnce` and `StartupApproved`, OptiBoost declarative tweaks/GPOs, Uninstall registration (`HKLM\...\Uninstall\OptiAssist`).
- **Services & Scheduled Tasks:** `System.ServiceProcess.ServiceController` (`wuauserv`, etc.), COM `Schedule.Service` for Task Scheduler enumeration/toggling.
- **Win32 & Shell APIs:** `dwmapi.dll` (`DwmSetWindowAttribute` rounded corners), `user32.dll` (`WM_GETMINMAXINFO`, `SendMessageTimeout`), `shell32.dll` (`ExtractIconEx`, `SHEmptyRecycleBin`), `winsqlite3.dll` (SQLite `VACUUM`), `WScript.Shell` COM (`.lnk` shortcuts), `ms-settings:` URIs, `winget.exe`, `wt.exe`.
- **Windows 10 vs. Windows 11 Behavior:**
  - `DebloaterEngine` gates tweaks by `minBuild` / `maxBuild` (e.g., 17763 Win10 1809, 19044 Win10 LTSC 2021, 22621/22631 Win11 22H2/23H2, 26100 Win11 24H2) and edition (`editions`).
  - `Win11CompatAdvisor` evaluates SSE4.2, CMPXCHG16B, UEFI, RAM, GPU, and storage to classify hardware into Layer A/B/C, hiding the Win11 compatibility warning banner when hardware is Layer A compatible.
  - `OpenExternalLogWindow` uses Windows Terminal (`wt.exe`, native on Win11 / optional on Win10) via a generated `.ps1` file (avoiding `;` tab-splitting), falling back to `powershell.exe` or `cmd.exe`.

---

## 6. Configuration, Logging, Persistence & Error Handling

- **Configuration (`SettingsHelper`):** Zero-dependency custom JSON parser/serializer persisting `Config\settings.json`, `Config\custom_shortcuts.json`, `Config\Addons\SystemCleaner\settings.json`, `Config\Addons\Toolbox\toolbox.json`, and `Config\Addons\AppManager\autoinstall.json`.
- **Localization (`LocalizationHelper`):** Merges `Config\Languages\<lang>.json` with per-module `Config\Addons\<Module>\<lang>.json`; supports zero-restart hot-reloading.
- **Logging:** Thread-safe `Dispatcher.Invoke` logging to `MainWindow.txtConsoleLog`, `%TEMP%\OptiAssist_Terminal_<pid>.log` (tailed by external Windows Terminal), and optional `LogWindow`.
- **Persistence & Rollback:** OptiBoost saves pre-change JSON snapshots under `Config\Debloater\backups\`; System Cleaner optionally backs up cleaned items via `CleanerBackupManager`.
- **Error Handling:** `App.xaml.cs` catches `DispatcherUnhandledException` and `AppDomain.UnhandledException` via `OptiDialog`; `MainWindow` guards WMI detection with a 30-second `DispatcherTimer` fallback; `LibreHardwareEngine` guards `DiskInfoToolkit` against `StackOverflowException` on virtualized VirtIO/QEMU/VMware SCSI disks (`HasVirtualizedStorageDisk()`).

---

## 7. Items Not Determinable From Static Code Alone

- **Runtime behavior on specific physical OEM hardware:** Exact sensor availability and PawnIO kernel driver behavior vary per motherboard Super I/O, GPU, and firmware implementation.
- **Upstream origin of `TweakHelper.CleanTempFiles` / `CleanWindowsUpdateCache`:** No original third-party Batch file is present in the repository for side-by-side comparison (documented in `THIRD-PARTY-NOTICES.md`).
- **Environment-specific Winget / Store availability:** Behavior on air-gapped networks or stripped Windows LTSC builds without Microsoft Store / `winget.exe` depends on runtime environment state.
