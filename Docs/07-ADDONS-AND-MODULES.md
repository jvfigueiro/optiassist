# Chapter 07: Add-ons & Modular Plugins Architecture

OptiAssist includes an extensible, modular architecture designed for high scalability and flexibility. Technicians and developers can package, enable, or disable independent feature modules ("plug-ins") stored portably inside `Config/Addons/`.

---

## 🧩 Architectural Overview

The Add-ons system allows OptiAssist to extend beyond its core capabilities without requiring monolithic code modifications or recompilations.

### Core Add-on Characteristics:
1. **Portable Discovery**: Each plug-in is located in its own directory inside `Config/Addons/<ModuleName>/`.
2. **Declarative Manifest (`addon.json`)**: Describes the module's identity, version, author, icon, category, and default activation state.
3. **Seamless In-Window UI**: Modules render directly within the main OptiAssist window via the **Add-ons** (`rbNavAddons`) navigation panel.
4. **Independent Toggle & Persistence**: Modules can be individually enabled or disabled in the **Manage Add-ons** view; disabled modules are persisted in `Config/settings.json` under `DisabledAddons`.
5. **Real-Time Hot Reload**: New modules dropped into `Config/Addons` are discovered immediately by clicking **Reload Add-ons** (`🔄 Reload Add-ons`) without restarting the application.

---

## 🧹 Built-in Module: System Cleaner & Optimizer

The initial flagship module included with OptiAssist is the **System Cleaner & Optimizer** (`Config/Addons/SystemCleaner/`), which performs deep system garbage collection, cache purging, crash dump cleanup, and privacy trace removal.

### Cleaning Categories & Supported Targets:

| Category | Item Name | Cleaning Target & Description | Recommended |
|---|---|---|:---:|
| **Temporary Files** | **User Temporary Files** | Deletes application cache files, unreferenced installers, and temp directories in `%TEMP%` and `%LOCALAPPDATA%\Temp`. | ✅ Yes |
| **Temporary Files** | **Windows System Temp** | Deletes Windows system-wide temp files in `C:\Windows\Temp`. | ✅ Yes |
| **System Caches** | **Windows Update Cache** | Purges downloaded and staged update packages in `C:\Windows\SoftwareDistribution\Download`. | ✅ Yes |
| **Explorer & UI** | **Thumbnail Cache** | Deletes Explorer thumbnail databases (`thumbcache_*.db`, `iconcache_*.db`) in `%LOCALAPPDATA%\Microsoft\Windows\Explorer`. | ✅ Yes |
| **Storage** | **Empty Recycle Bin** | Permanently empties recycle bins across all attached drives using native Win32 `SHEmptyRecycleBin`. | ✅ Yes |
| **System Dumps** | **Memory Dumps & BSOD Reports** | Removes crash dumps (`MEMORY.DMP`), minidumps (`C:\Windows\Minidump`), and kernel crash reports. | ✅ Yes |
| **Security** | **Windows Defender Temp & Scans** | Clears detection history caches, operational support logs, and engine cache in `C:\ProgramData\Microsoft\Windows Defender`. | ✅ Yes |
| **Network** | **DNS Resolver Cache** | Flushes the local DNS client cache (`DnsFlushResolverCache` API and `ipconfig /flushdns`) to resolve stale host resolutions. | ✅ Yes |
| **System Logs** | **Windows Error Reporting (WER)** | Clears archived crash reports, queues, and telemetry logs in `ProgramData\Microsoft\Windows\WER`. | ✅ Yes |
| **System Caches** | **Delivery Optimization Cache** | Purges peer-to-peer Windows Update delivery cache files in `SoftwareDistribution\DeliveryOptimization`. | ✅ Yes |
| **Privacy & Registry** | **Windows Clipboard** | Flushes text, images, and data stored in active memory clipboard buffers. | ⚪ Optional |
| **Privacy & Registry** | **Explorer Shellbags** | Cleans registry folder view settings, window positions, and explorer browsing history (`Bags` and `BagMRU`). | ⚪ Optional |
| **Privacy & Registry** | **MUICache** | Clears the registry cache of application names and executable strings from `MuiCache`. | ⚪ Optional |
| **System Caches** | **Windows Prefetch** | Deletes execution prefetch files in `C:\Windows\Prefetch` to rebuild application load tables. | ⚪ Optional |
| **System Logs** | **System Event Logs** | Clears native Windows Event Logs (`Application`, `System`, `Security`, `Setup`) via Win32 EventLog API and `wevtutil`. | ⚪ Optional |

---

## 🛠️ System Cleaner Operations

1. **🔍 Scan & Analyze**:
   * Scans all selected categories asynchronously without locking the UI.
   * Accurately calculates file count and cleanable disk space (KB, MB, GB).
   * Displays individual size badges for each category and a global summary.

2. **🧹 Clean Selected**:
   * Safely deletes unlocked files and resets registries.
   * In-use files are skipped gracefully without throwing unhandled exceptions.
   * Reports total disk space freed and logs operations in real time.

3. **Selection Shortcuts**:
   * **Select All**: Marks all available categories.
   * **Deselect All**: Unchecks all categories.
   * **Recommended**: Marks safe, non-invasive daily clean targets (excludes deep registry and prefetch caches).

---

## 📦 How to Create Custom Add-ons

To develop a new module:
1. Create a subdirectory inside `Config/Addons/` (e.g., `Config/Addons/HardwareStress/`).
2. Add an `addon.json` file defining module properties:
   ```json
   {
     "id": "hardware-stress",
     "name": "Hardware Stress Tester",
     "description": "Performs CPU, memory, and disk stress testing routines.",
     "version": "1.0.0",
     "author": "IT Workshop",
     "icon": "⚡",
     "category": "Diagnostics",
     "enabled": true
   }
   ```
3. Open OptiAssist and navigate to **🧩 Add-ons** > **⚙️ Manage Add-ons** > **🔄 Reload Add-ons**.
