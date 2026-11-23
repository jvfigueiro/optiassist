# System Architecture — OptiAssist (`v1.2.S7`)

OptiAssist is a **WPF (.NET Framework 4.8) Microkernel Host** that coordinates hardware telemetry, Windows diagnostics, and a decoupled plugin ecosystem of dynamically loaded Add-on DLLs.

---

## 1. Modular Architecture Overview

```mermaid
flowchart TB
    Host["OptiAssist.exe (WPF Host Kernel)"]
    Host --> UI["MainWindow + OptiDialog + FluentIcons (SVG)"]
    Host --> Core["Core Services: SettingsHelper, LocalizationHelper, SystemInfoHelper"]
    Host --> Platform["Platform Engine: PlatformProfiler, PciIdsDatabase, Win11CompatAdvisor, KnownIssuesEngine"]
    Host --> Debloat["Windows Tools: OptiBoost (DebloaterEngine) + Compatibility Checker"]
    Host --> AddonMgr["AddonHelper (Anti-Lock Loader & Dependency Preloader)"]

    AddonMgr -->|"Assembly.Load(byte[])"| AM["AppManager.dll"]
    AddonMgr -->|"Assembly.Load(byte[])"| SC["SystemCleaner.dll"]
    AddonMgr -->|"Assembly.Load(byte[])"| TB["Toolbox.dll"]
    AddonMgr -->|"Assembly.Load(byte[])"| SM["StartupManager.dll"]
    AddonMgr -->|"Assembly.Load(byte[])"| MR["MaintenanceReport.dll"]
    AddonMgr -->|"Preload 9 Sibling DLLs + Load"| PC["PCAssist.dll (Hardware Monitor)"]
    PC --> LHM["LibreHardwareMonitorLib.dll (MPL-2.0)"]
```

### Core Architectural Principles
1. **Zero File-Lock Assembly Loading:** Both Add-on DLLs and their co-located dependency DLLs are read into memory via `File.ReadAllBytes(...)` and loaded with `Assembly.Load(byte[])`. Files on disk are never locked, enabling runtime hot-replacement or removal.
2. **Contract Decoupling:** Add-ons reference `OptiAssist.exe` with `<Private>False</Private>` and communicate solely through `IOptiAssistAddon` and `IOptiAssistHost`.
3. **Lazy UI Initialization:** Add-on views (`CreateView()`) are constructed only when the user clicks the corresponding sidebar button, then cached in `AddonEntry.CachedView`.
4. **Unified Vector Iconography:** `FluentIcons.cs` and `FluentContentConverter` dynamically convert icon tokens across both core XAML and all 6 Add-on DLLs into theme-aware Microsoft Fluent UI SVG `<Path>` vectors.

---

## 2. Module Discovery & Loading Flow

```mermaid
sequenceDiagram
    participant App as MainWindow_Loaded
    participant Helper as AddonHelper
    participant Resolver as PreloadSiblingDependencies
    participant DLL as Addon DLL (IOptiAssistAddon)

    App->>Helper: LoadDiscoveredAddons(host, disabledIds)
    loop Each folder in Config\Addons\<Module>\
        Helper->>Helper: Skip known dependency DLLs
        Helper->>Resolver: Preload sibling dependency DLLs (byte[])
        Resolver-->>Helper: Cached in _resolvedDepCache
        Helper->>DLL: Assembly.Load(File.ReadAllBytes(addonDll))
        Helper->>DLL: Activator.CreateInstance(IOptiAssistAddon)
        Helper->>DLL: Initialize(host)
        Helper->>Helper: Merge Config\Addons\<Module>\<lang>.json
    end
    App->>App: RebuildSidebarAddonsMenu() sorted by ModuleOrder
    Note over App,DLL: User clicks module in sidebar
    App->>DLL: GetOrCreateView() -> CreateView() (lazy, cached)
```

---

## 3. Related Documentation
- [Architecture & Codebase Map](_analysis/00-map.md)
- [Module Documentation Index](modules/README.md)
- [How to Create a Module](create-module.md)
- [Build & Distribution Guide](build.md)
