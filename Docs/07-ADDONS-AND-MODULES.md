# 🧩 Add-ons & Modular DLL Architecture (`Config/Addons`)

OptiAssist features a scalable **compiled DLL plugin architecture** that allows modules to integrate as first-class pages in the main application sidebar while keeping their entire UI and business logic isolated inside standalone `.dll` assemblies.

---

## 1. Architecture Overview

### Plugin Interface (`IOptiAssistAddon` & `IOptiAssistHost`)
Defined in `src/AddonHelper.cs` inside `OptiAssist.exe`:

- **`IOptiAssistHost`** (implemented by `MainWindow`):
  - `void Log(string message)` — Writes real-time execution logs to the OptiAssist **Logs** console.
  - `string GetLocalizedString(string key, string fallback)` — Retrieves localized strings from the active language dictionary (`Config/Languages/*.json`).
  - `string CurrentLanguage { get; }` — Returns the active language code (`en-US`, `pt-BR`, etc.).

- **`IOptiAssistAddon`** (implemented by each `.dll` plugin):
  - `string Id { get; }` — Unique module identifier (e.g., `"system-cleaner"`).
  - `string Name { get; }` — Localized display name shown in the sidebar and Settings manager.
  - `string Description { get; }` — Localized module description.
  - `string Version { get; }` — Semantic version (e.g., `"1.1.0"`).
  - `string Author { get; }` — Module author.
  - `string Icon { get; }` — Emoji/icon displayed on the sidebar button and in Settings.
  - `void Initialize(IOptiAssistHost host)` — Called when `OptiAssist.exe` loads the DLL.
  - `UIElement CreateView()` — Returns the complete WPF UI view hosted inside the main window when the user clicks the module in the sidebar.
  - `void OnLanguageChanged(string languageCode)` — Updates the module's UI labels dynamically whenever the user switches languages in Settings.

### Non-Locking DLL Loader
`AddonHelper.LoadDiscoveredAddons` reads each `.dll` in `Config/Addons/` using `Assembly.Load(File.ReadAllBytes(dllPath))` and resolves references to `OptiAssist.exe` via `AppDomain.CurrentDomain.AssemblyResolve`. Because the assembly is loaded from memory bytes, the `.dll` file on disk is **never locked**, allowing users to remove or update modules while `OptiAssist.exe` is running.

---

## 2. Managing Modules in Settings

Inside **Settings (`⚙️ Settings` → `🧩 Modular Add-ons & Plugins`)**, users can:
1. **➕ Add Module (`.dll`)**: Select any compiled OptiAssist `.dll` plugin; OptiAssist validates that it implements `IOptiAssistAddon`, copies it into `Config/Addons/<ModuleName>/<ModuleName>.dll`, and adds it to the sidebar immediately.
2. **Enable / Disable (`Active`)**: Toggle whether a module is shown in the sidebar navigation. Disabled module IDs are persisted in `Config/settings.json` (`DisabledAddons`).
3. **🗑️ Remove**: Deletes the module's `.dll` from `Config/Addons` and removes its entry from the sidebar.

---

## 3. Built-in Module: System Cleaner v2.0 (`Config/Addons/SystemCleaner/SystemCleaner.dll`)

Inspired by **BleachBit**, the **System Cleaner & Optimizer** (`src/Addons/SystemCleaner/SystemCleanerAddon.cs`) implements an extensible, object-oriented `BaseCleaner` architecture supporting non-destructive **Dry-Run (`Scan()`)** analysis and safe **Execution (`Clean()`)** with strict path sanitization and locked-file resilience.

### `BaseCleaner` Architecture
- **`BaseCleaner` (Abstract Base Class)**:
  - Metadata properties: `Id`, `CategoryId`, `Category`, `Name`, `Description`, `Icon`, `RequiresAdmin` (`bool`), `IsDangerous` (`bool`), `WarningMessage` (`string`), and `IsRecommended` (`bool`).
  - `ScanResult Scan(CleanerContext context)` — Performs a 100% non-destructive dry-run scan, returning `FoundItems` (`List<string>`), `EstimatedBytes` (`long`), and `SkippedOrLockedCount` (`int`).
  - `CleanResult Clean(CleanerContext context)` — Executes cleanup/maintenance, returning `FreedBytes` (`long`), `DeletedCount` (`int`), and `ErrorOrLockedCount` (`int`) while gracefully skipping locked (`EBUSY` / `IOException`) or permission-denied (`UnauthorizedAccessException`) files.
- **`PathSanitizer` (Strict Scope Protection)**:
  - Blocks accidental deletion of root drives (`C:\`), `C:\Windows` (except explicitly whitelisted temp/prefetch/update subpaths), `Program Files`, user profile roots, and the OptiAssist installation directory.
  - Excludes reparse points/symlinks (`FileAttributes.ReparsePoint`) and critical system folders during recursive Deep Scans.

### Supported Cleanup Modules (23 Cleaners Across 5 Categories)

#### 🪟 Category 1: Windows Core (`win_core`)
| ID | Name | Target Location / Mechanism | Admin | Caution |
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

#### 🗂️ Category 2: Windows Explorer & Shell (`explorer_shell`)
| ID | Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `thumbnails` | **Thumbnail Cache (`thumbcache_*.db`)** | `%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db` | No | No |
| `run_mru` | **Run Dialog MRU History** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU` | No | No |
| `recent_docs` | **Recent Documents & Jump Lists** | `%APPDATA%\Microsoft\Windows\Recent`, `AutomaticDestinations`, `CustomDestinations`, `RecentDocs` | No | No |
| `shellbags` | **Explorer Shellbags & Search History** | `BagMRU`, `Bags`, and `WordWheelQuery` registry keys | No | ⚠️ Yes |

#### 🌐 Category 3: Chromium Browsers — Google Chrome & Microsoft Edge (`chromium`)
Automatically discovers all profiles (`Default`, `Profile 1`, `Profile 2`, `Guest Profile`, `System Profile`) in `%LOCALAPPDATA%\Google\Chrome\User Data` and `%LOCALAPPDATA%\Microsoft\Edge\User Data`, and detects running `chrome.exe` / `msedge.exe` processes before accessing locked databases.

| ID | Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `chromium_cache` | **Network, GPU & Code Cache** | `Cache\Cache_Data`, `Code Cache`, `GPUCache`, `Service Worker`, `ShaderCache` | No | No |
| `chromium_crash_reports` | **Crashpad & Crash Reports** | `Crashpad\reports`, `Crashpad\completed`, `Crashpad\pending` | No | No |
| `chromium_ai_models` | **Local AI Models (Gemini Nano)** | `OptGuideOnDeviceModel`, `optimization_guide_model_store` | No | No |
| `chromium_vacuum` | **SQLite Database Vacuum** | Executes native `VACUUM;` via `winsqlite3.dll` on `History`, `Web Data`, `Favicons`, `Top Sites`, `Cookies` | No | No |
| `chromium_cookies` | **Browser Cookies** | `Cookies`, `Network\Cookies` across all profiles | No | ⚠️ Yes |
| `chromium_history` | **Browsing & Download History** | `History`, `Visited Links`, `Top Sites` across all profiles | No | ⚠️ Yes |
| `chromium_form_data` | **Autofill Form Data** | `Web Data` across all profiles | No | ⚠️ Yes |
| `chromium_site_data` | **Site Data (LocalStorage & IndexedDB)** | `Local Storage\leveldb`, `IndexedDB`, `Session Storage`, `File System` | No | ⚠️ Yes |
| `chromium_sessions` | **Saved Sessions & Open Tabs** | `Sessions`, `Current Session`, `Current Tabs`, `Last Session` | No | ⚠️ Yes |

#### 🛡️ Category 4: Windows Defender (`defender`)
| ID | Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `defender_temp` | **Scan Logs & Support Temp** | `C:\ProgramData\Microsoft\Windows Defender\Scans\History` & `Support` | 🛡️ Yes | No |
| `defender_quarantine` | **Quarantine Files** | `C:\ProgramData\Microsoft\Windows Defender\Quarantine` | 🛡️ Yes | ⚠️ Yes |

#### 🔎 Category 5: Deep Scan — Developer & Orphaned Files (`deep_scan`)
Supports configurable root directory (`DeepScanRootPath`, defaulting to `C:\Users\<user>`) and recursion depth limit (`DeepScanMaxDepth`) while skipping critical OS and hidden `.git` directories.

| ID | Name | Target Location / Mechanism | Admin | Caution |
| :--- | :--- | :--- | :---: | :---: |
| `deep_junk_files` | **Orphaned Metadata & Editor Swap Files** | `.DS_Store`, `Thumbs.db`, `*.swp`, `*.swo`, `*.tmp` | No | No |
| `deep_node_modules` | **Node.js & Angular Cache** | `node_modules`, `.angular` directories | No | ⚠️ Yes |
| `deep_python_cache` | **Python Bytecode & VirtualEnvs** | `__pycache__`, `venv`, `.venv` directories | No | ⚠️ Yes |

### Automated Unit Tests & Dry-Run Simulation Script
Run `Tests/Run-SystemCleanerTests.ps1` in PowerShell to execute unit tests for `PathSanitizer`, sandboxed `DeepScan` (`Scan()`, `Clean()`, and `EBUSY` locked-file skipping), SQLite `VACUUM;` defragmentation, and a live non-destructive dry-run report across all modules:
```powershell
.\Tests\Run-SystemCleanerTests.ps1
```

---

## 4. Built-in Module: Maintenance Report (`Config/Addons/MaintenanceReport/MaintenanceReport.dll`)

All code, UI construction, and report generation logic (`MaintenanceRecord` and `ReportHelper`) for the **Maintenance Service Record** reside inside `src/Addons/MaintenanceReport/MaintenanceReportAddon.cs` (`MaintenanceReport.csproj`), which compiles directly to:

```text
Config/Addons/MaintenanceReport/MaintenanceReport.dll
```

### Features
- **Service & Customer Details**: Captures Service Order (SO) number, Client Name, and auto-fills the machine's detected Serial Number / Service Tag via `IOptiAssistHost.Hardware`.
- **Service Provider Branding**: Reads `CustomerName` and `ContactInfo` from `IOptiAssistHost.Settings` when Service Provider Mode is enabled.
- **8-Point Maintenance Checklist**: Disk SMART check, internal cleaning & thermal paste, clean Windows format, driver/BIOS update, essential apps installation, temporary file cleanup, DISM/SFC repair, and network/printer setup.
- **Technical Observations & Report Generation**: Issues timestamped `.txt` reports into `Reports/`, optionally saves a copy on the customer's machine (`ReportSavePath`), or copies the full formatted report directly to the Windows Clipboard.
