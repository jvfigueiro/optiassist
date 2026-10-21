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

## 3. Built-in Module: System Cleaner (`Config/Addons/SystemCleaner/SystemCleaner.dll`)

All code, UI construction, Win32 API calls, and cleaning logic for the **System Cleaner & Optimizer** reside inside `src/Addons/SystemCleaner/SystemCleanerAddon.cs` (`SystemCleaner.csproj`), which compiles directly to:

```text
Config/Addons/SystemCleaner/SystemCleaner.dll
```

### Supported Cleanup Categories (15 Items)

| ID | Category | Target Location / Mechanism | Recommended by Default |
| :--- | :--- | :--- | :---: |
| `user_temp` | **User Temporary Files** | `%TEMP%` (`Path.GetTempPath()`) | ✅ Yes |
| `system_temp` | **Windows System Temp** | `C:\Windows\Temp` | ✅ Yes |
| `win_update` | **Windows Update Cache** | `C:\Windows\SoftwareDistribution\Download` | ✅ Yes |
| `thumbnails` | **Thumbnail Cache** | `%LocalAppData%\Microsoft\Windows\Explorer\thumbcache_*.db` & `iconcache_*.db` | ✅ Yes |
| `recycle_bin` | **Empty Recycle Bin** | `shell32.dll` (`SHQueryRecycleBin` / `SHEmptyRecycleBin`) across all drives | ✅ Yes |
| `defender_temp` | **Windows Defender Temp & Scans** | `C:\ProgramData\Microsoft\Windows Defender\Scans\History\Service` & `Support` | ✅ Yes |
| `dns_cache` | **DNS Resolver Cache** | `dnsapi.dll` (`DnsFlushResolverCache`) / `ipconfig /flushdns` | ✅ Yes |
| `error_reports` | **Windows Error Reporting (WER)** | `%ProgramData%\Microsoft\Windows\WER` & `%LocalAppData%\Microsoft\Windows\WER` | ✅ Yes |
| `delivery_opt` | **Delivery Optimization Cache** | `C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache` | ✅ Yes |
| `clipboard` | **Windows Clipboard** | `System.Windows.Clipboard.Clear()` (STA Thread) | ✅ Yes |
| `memory_dumps` | **Memory Dumps & BSOD Reports** | `C:\Windows\MEMORY.DMP`, `C:\Windows\Minidump`, `LiveKernelReports` | ⚪ Optional |
| `mui_cache` | **MUICache** | `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache` | ⚪ Optional |
| `prefetch` | **Windows Prefetch** | `C:\Windows\Prefetch` | ⚪ Optional |
| `shellbags` | **Explorer Shellbags** | `BagMRU` & `Bags` registry keys under `Shell` and `ShellNoRoam` | ⚪ Optional |
| `system_logs` | **System Event Logs** | `wevtutil cl` for `Application`, `System`, `Setup`, and `Security` | ⚪ Optional |
