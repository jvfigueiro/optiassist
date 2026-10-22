# Chapter 07: Add-ons & Modular DLL Architecture

OptiAssist features a high-performance **compiled DLL plugin architecture** that allows modules to integrate as first-class pages in the main application sidebar while keeping their entire UI and business logic isolated inside standalone `.dll` assemblies.

---

## 1. Architecture Overview

### Plugin Contracts (`IOptiAssistAddon` & `IOptiAssistHost`)
Defined in `src/AddonHelper.cs` inside `OptiAssist.exe`:

#### `IOptiAssistHost` (Implemented by `MainWindow`):
- `void Log(string message)` — Pushes real-time messages to the central console and pop-out `LogWindow`.
- `string GetLocalizedString(string key, string defaultValue)` — Retrieves localized strings from the active dictionary.
- `string CurrentLanguage { get; }` — Returns the active language code (`en-US`, `pt-BR`, etc.).
- `AppSettings Settings { get; }` — Grants access to global application settings.
- `HardwareSummary Hardware { get; }` — Grants access to the machine's detected WMI hardware specs.
- `void SaveSettings()` — Persists modifications to `Config/settings.json`.
- `void OpenLogWindow()` — Automatically surfaces the floating `LogWindow` during long-running tasks.

#### `IOptiAssistAddon` (Implemented by each `.dll` plugin):
- `string Id { get; }` — Unique module identifier (e.g., `"system-cleaner"`, `"maintenance-report"`, `"toolbox"`).
- `string Name { get; }` — Localized display name shown in the sidebar and Settings manager.
- `string Description { get; }` — Localized module description.
- `string Version { get; }` — Semantic version string (e.g., `"1.1.0.0"`).
- `string Author { get; }` — Module author (e.g., `"João Figueiró"`).
- `string Icon { get; }` — Emoji/symbol rendered on the sidebar button and in Settings.
- `void Initialize(IOptiAssistHost host)` — Lifecycle hook invoked when `OptiAssist.exe` loads the DLL.
- `FrameworkElement CreateView()` — Returns the complete WPF UI view rendered inside the main workspace when selected.
- `void OnLanguageChanged(string langCode)` — Updates UI strings dynamically whenever the language changes.

### Non-Locking DLL Loader
`AddonHelper.LoadDiscoveredAddons` reads each `.dll` in `Config/Addons/` using `Assembly.Load(File.ReadAllBytes(dllPath))` and resolves references back to `OptiAssist.exe` via `AppDomain.CurrentDomain.AssemblyResolve`.

Because the assembly is loaded from memory bytes, the `.dll` file on disk is **never locked**. Users can overwrite, update, replace, or remove modules while `OptiAssist.exe` is running without triggering Windows file-lock errors.

---

## 2. Managing Modules in Settings

Inside **Settings (`⚙️ Settings` → `🧩 Modular Add-ons & Plugins`)**, users can manage extensions visually:
1. **➕ Add Module (`.dll`)**: Select any compiled OptiAssist `.dll` plugin file. OptiAssist validates that it implements `IOptiAssistAddon`, copies it into `Config/Addons/<ModuleName>/<ModuleName>.dll`, and adds it to the sidebar immediately.
2. **Active Checkbox (Enable / Disable)**: Toggles whether a module appears in the sidebar navigation. Disabled module IDs are persisted in `Config/settings.json` under `DisabledAddons`.
3. **🗑️ Remove**: Deletes the module's DLL from `Config/Addons` and purges its entry from the sidebar.
4. **Reload Modules**: Rescans `Config/Addons` to detect any manual filesystem additions or removals.

---

## 3. Official Assembly Metadata

All official OptiAssist modules are compiled with standard assembly attributes:
- **Title**: `OptiAssist Module - <ModuleName>`
- **Description**: Module description
- **Company**: João Figueiró
- **Product**: OptiAssist Modules
- **Copyright**: Copyright (C) 2026 João Figueiró. Licensed under GNU GPLv3.
- **Assembly Version**: `1.1.0.0`
- **File Version**: `1.1.0.0`

---

## 4. Built-in Module: System Cleaner v2.0 (`SystemCleaner.dll`)

Inspired by **BleachBit**, the **System Cleaner & Optimizer** (`src/Addons/SystemCleaner/`) implements an extensible, object-oriented `BaseCleaner` architecture supporting non-destructive **Dry-Run (`Scan()`)** analysis and safe **Execution (`Clean()`)** with strict path sanitization and locked-file resilience.

### Architecture Highlights
- **`BaseCleaner` (Abstract Base Class)**:
  - Properties: `Id`, `CategoryId`, `Category`, `Name`, `Description`, `Icon`, `RequiresAdmin` (`bool`), `IsDangerous` (`bool`), `WarningMessage` (`string`), and `IsRecommended` (`bool`).
  - `ScanResult Scan(CleanerContext context)` — Performs a 100% non-destructive dry-run scan, returning `FoundItems` (`List<string>`), `EstimatedBytes` (`long`), and `SkippedOrLockedCount` (`int`).
  - `CleanResult Clean(CleanerContext context)` — Executes cleanup/maintenance, returning `FreedBytes` (`long`), `DeletedCount` (`int`), and `ErrorOrLockedCount` (`int`) while gracefully skipping locked (`EBUSY` / `IOException`) or permission-denied (`UnauthorizedAccessException`) files.
- **`PathSanitizer` (Strict Scope Protection)**:
  - Blocks accidental deletion of root drives (`C:\`), `C:\Windows` (except explicitly whitelisted temp/prefetch/update subpaths), `Program Files`, user profile roots, and the OptiAssist application directory.
  - Excludes reparse points/symlinks (`FileAttributes.ReparsePoint`) and critical system folders during recursive Deep Scans.

### 23 Cleaners across 5 Categories

#### 🪟 1. Windows Core (`win_core`)
| ID | Cleaner Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `user_temp` | **User Temporary Files (`%TEMP%`)** | `%TEMP%` and `%LOCALAPPDATA%\Temp` | No | No |
| `system_temp` | **Windows System Temp** | `C:\Windows\Temp` | 🛡️ Yes | No |
| `win_update` | **Windows Update & Delivery Cache** | `C:\Windows\SoftwareDistribution\Download` & `DeliveryOptimization` | 🛡️ Yes | No |
| `memory_dumps` | **Memory Dumps (`*.dmp`, `MEMORY.DMP`)** | `C:\Windows\MEMORY.DMP`, `Minidump`, `LiveKernelReports`, `CrashDumps` | 🛡️ Yes | No |
| `error_reports` | **Windows Error Reporting (WER)** | `%ProgramData%\Microsoft\Windows\WER` & `%LOCALAPPDATA%\Microsoft\Windows\WER` | No | No |
| `prefetch` | **Windows Prefetch** | `C:\Windows\Prefetch\*.pf` | 🛡️ Yes | No |
| `recycle_bin` | **Empty Recycle Bin** | Win32 `SHQueryRecycleBin` & `SHEmptyRecycleBin` across all drives | No | No |
| `clipboard` | **Windows Clipboard** | STA thread `System.Windows.Clipboard.Clear()` | No | No |
| `dns_cache` | **DNS Resolver Cache** | `dnsapi.dll` (`DnsFlushResolverCache`) + `ipconfig /flushdns` fallback | No | No |
| `mui_cache` | **Registry MUICache** | `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache` | No | No |
| `system_logs` | **System Event & Setup Logs** | `EventLog` / `wevtutil cl` (`Application`, `System`, `Setup`, `Security`) + `CBS` & `Panther` logs | 🛡️ Yes | ⚠️ Yes |

#### 🗂️ 2. Windows Explorer & Shell (`explorer_shell`)
| ID | Cleaner Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `thumbnails` | **Thumbnail Cache (`thumbcache_*.db`)** | `%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db` | No | No |
| `run_mru` | **Run Dialog MRU History** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU` | No | No |
| `recent_docs` | **Recent Documents & Jump Lists** | `%APPDATA%\Microsoft\Windows\Recent`, `AutomaticDestinations`, `CustomDestinations`, `RecentDocs` | No | No |
| `shellbags` | **Explorer Shellbags & Search History** | `BagMRU`, `Bags`, and `WordWheelQuery` registry keys | No | ⚠️ Yes |

#### 🌐 3. Chromium Browsers (`chromium`)
Discovers all profiles (`Default`, `Profile 1`, etc.) in Google Chrome and Microsoft Edge `%LOCALAPPDATA%` paths, and checks running `chrome.exe` / `msedge.exe` processes before accessing locked databases.

| ID | Cleaner Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `chromium_cache` | **Network, GPU & Code Cache** | `Cache\Cache_Data`, `Code Cache`, `GPUCache`, `Service Worker`, `ShaderCache` | No | No |
| `chromium_crash_reports` | **Crashpad & Crash Reports** | `Crashpad\reports`, `Crashpad\completed`, `Crashpad\pending` | No | No |
| `chromium_ai_models` | **Local AI Models (Gemini Nano)** | `OptGuideOnDeviceModel`, `optimization_guide_model_store` | No | No |
| `chromium_vacuum` | **SQLite Database Vacuum** | Executes native `VACUUM;` via `winsqlite3.dll` on `History`, `Web Data`, `Favicons`, `Cookies` | No | No |
| `chromium_cookies` | **Browser Cookies** | `Cookies`, `Network\Cookies` across all profiles | No | ⚠️ Yes |
| `chromium_history` | **Browsing & Download History** | `History`, `Visited Links`, `Top Sites` across all profiles | No | ⚠️ Yes |
| `chromium_form_data` | **Autofill Form Data** | `Web Data` across all profiles | No | ⚠️ Yes |
| `chromium_site_data` | **Site Data (LocalStorage & IndexedDB)** | `Local Storage\leveldb`, `IndexedDB`, `Session Storage`, `File System` | No | ⚠️ Yes |
| `chromium_sessions` | **Saved Sessions & Open Tabs** | `Sessions`, `Current Session`, `Current Tabs`, `Last Session` | No | ⚠️ Yes |

#### 🛡️ 4. Windows Defender (`defender`)
| ID | Cleaner Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `defender_temp` | **Scan Logs & Support Temp** | `C:\ProgramData\Microsoft\Windows Defender\Scans\History` & `Support` | 🛡️ Yes | No |
| `defender_quarantine` | **Quarantine Files** | `C:\ProgramData\Microsoft\Windows Defender\Quarantine` | 🛡️ Yes | ⚠️ Yes |

#### 🔎 5. Deep Scan — Developer & Orphaned Files (`deep_scan`)
Configurable root path (`DeepScanRootPath`, defaulting to `C:\Users\<user>`) and recursion depth limit (`DeepScanMaxDepth`) while excluding critical OS folders and `.git` repositories.

| ID | Cleaner Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `deep_junk_files` | **Orphaned Metadata & Editor Swap Files** | `.DS_Store`, `Thumbs.db`, `*.swp`, `*.swo`, `*.tmp` | No | No |
| `deep_node_modules` | **Node.js & Angular Cache** | `node_modules`, `.angular` directories | No | ⚠️ Yes |
| `deep_python_cache` | **Python Bytecode & VirtualEnvs** | `__pycache__`, `venv`, `.venv` directories | No | ⚠️ Yes |

---

## 5. Built-in Module: Maintenance Report (`MaintenanceReport.dll`)

Compiled from `src/Addons/MaintenanceReport/`, this module isolates the service order generator into:
```text
Config/Addons/MaintenanceReport/MaintenanceReport.dll
```
- **Service & Customer Details**: Captures Service Order (SO) number, Client Name, and machine serial number via `IOptiAssistHost.Hardware`.
- **Branding**: Reads `CustomerName` and `ContactInfo` from `IOptiAssistHost.Settings`.
- **8-Point Checklist**: SMART check, thermal paste, format, drivers, essential apps, temp cleanup, DISM/SFC, and network/printer setup.
- **Multi-Destination Export**: Saves `.txt` files to `Reports/`, client PC (`ReportSavePath`), and the Windows Clipboard.

---

## 6. Built-in Module: Toolbox (`Toolbox.dll`)

Compiled from `src/Addons/Toolbox/`, this module implements OptiAssist's custom command runner and unified optimization hub:
```text
Config/Addons/Toolbox/
├── Toolbox.dll
├── toolbox.json
└── Scripts/
    ├── tweaks_interface.ps1
    ├── fix_printer_rpc.reg
    └── reset_network.bat
```
- **Execution Engine**: Asynchronous execution of `cmd`, `PowerShell`, and standalone `binary` executables with live line-by-line streaming to `IOptiAssistHost.Log` and the pop-out `LogWindow`.
- **Category Manager**: Integrated modal to add, rename (with cascading updates across commands), and delete categories.
- **Bundled Optimization Presets**: Includes 16+ verified system repair and performance presets replacing the legacy static Optimization tab.
- **Options & Edit Mode**: Options drawer containing New Toolbox, Restore Presets, Open JSON, Category Manager, and an Edit Mode toggle to lock down or customize action cards.
