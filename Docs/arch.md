# OptiAssist Architecture and Modular System

**Document:** `docs/arch.md`  
**Platform:** Windows 10 / 11 (64-bit)  
**Framework:** .NET Framework 4.8 / WPF  
**License:** GNU General Public License v3.0  

---

## 1. Architectural Overview

OptiAssist is structured as a **Microkernel Host with Dynamic In-Memory Assembly Loading**. The central application (`OptiAssist.exe`) acts as the kernel, providing core services such as window chrome, asynchronous WMI/CIM hardware telemetry, theme and UI dispatching, thread-safe logging, and a zero-restart internationalization (i18n) engine.

Functional capabilities are divided into two distinct tiers:
1. **Host Native Views**: Integral views hardcoded into `MainWindow.xaml` (Dashboard, Windows Tools, Settings & Deployment, Logs & Terminal).
2. **Decoupled Add-on Modules**: Independent .NET Framework 4.8 Class Libraries located in `Config/Addons/<ModuleName>/<ModuleName>.dll` that implement the `IOptiAssistAddon` interface.

```
+-----------------------------------------------------------------------------------------+
|                               HOST KERNEL (OptiAssist.exe)                              |
|                                                                                         |
|  +--------------------+  +--------------------+  +-----------------+  +--------------+  |
|  | Settings & Config  |  | i18n Localization  |  | Thread-Safe Log |  | WMI Telemetry|  |
|  | (SettingsHelper)   |  | (LocalizationHelper|  | (MainWindow.Log)|  | (SystemInfo) |  |
|  +--------------------+  +--------------------+  +-----------------+  +--------------+  |
|                                                                                         |
|  +--------------------+  +--------------------+  +-----------------+  +--------------+  |
|  | Native View:       |  | Native View:       |  | Native View:    |  | Native View: |  |
|  | Dashboard          |  | Windows Tools      |  | Settings        |  | Terminal     |  |
|  +--------------------+  +--------------------+  +-----------------+  +--------------+  |
+--------------------------------------------^--------------------------------------------+
                                             |
                         Contracts: IOptiAssistAddon / IOptiAssistHost
                         In-Memory Anti-Lock Loading: Assembly.Load(byte[])
                                             |
        +------------------+-----------------+------------------+-----------------+
        |                  |                                    |                 |
        v                  v                                    v                 v
+----------------+ +----------------+                  +----------------+ +----------------+
| Toolbox (Core) | | App Manager    |                  | System Cleaner | | Startup / Rep. |
| (Toolbox.dll)  | | (AppManager.dll|                  | (Cleaner.dll)  | | (Addon DLLs)   |
+----------------+ +----------------+                  +----------------+ +----------------+
```

---

## 2. Mermaid Architecture Diagram

The diagram below illustrates the relationship between the Host Kernel, interfaces, and decoupled Add-ons:

```mermaid
graph TD
    subgraph Host ["OptiAssist Host (OptiAssist.exe)"]
        App["App.xaml.cs<br/>UAC Check, Exception Handlers"]
        MainWin["MainWindow.xaml / .cs<br/>Shell, Navigation, Dispatcher"]
        AddonH["AddonHelper.cs<br/>Discovery, Anti-Lock Loader, Lifecycle"]
        LocalH["LocalizationHelper.cs<br/>i18n Dictionaries & Hot-Reload"]
        SetH["SettingsHelper.cs<br/>Zero-Dependency JSON Engine"]
        SysH["SystemInfoHelper.cs<br/>Async WMI / CIM Telemetry"]
        DeployH["ShortcutsHelper.cs<br/>AppDeploymentHelper & WelcomeWizard"]
        LogWin["LogWindow.cs<br/>Detached Floating Terminal"]
    end

    subgraph Contracts ["Interface Contracts"]
        IHost["IOptiAssistHost<br/>Log, GetLocalizedString, Settings, Hardware"]
        IAddon["IOptiAssistAddon<br/>Id, Name, Initialize, CreateView, OnLanguageChanged"]
    end

    subgraph Addons ["Decoupled Add-on Modules (Config/Addons/)"]
        ModToolbox["Toolbox.dll<br/>Script Hub & Process Runner"]
        ModApp["AppManager.dll<br/>Winget & Offline Installer"]
        ModCleaner["SystemCleaner.dll<br/>Deep Cleaning & Dry-Run"]
        ModStartup["StartupManager.dll<br/>Registry & Task Scheduler"]
        ModReport["MaintenanceReport.dll<br/>Service Order Generator"]
    end

    App --> MainWin
    MainWin --> AddonH
    MainWin --> LocalH
    MainWin --> SetH
    MainWin --> SysH
    MainWin --> DeployH
    MainWin --> LogWin

    MainWin -.->|Implements| IHost
    AddonH -.->|Loads via byte array| Addons
    Addons -.->|Implement| IAddon
    Addons -.->|Consume Host Services| IHost
```

---

## 3. Module Discovery, Anti-Lock Loading, and Lifecycle

### 3.1 Step-by-Step Loading Pipeline

```mermaid
sequenceDiagram
    autonumber
    participant Host as MainWindow / AddonHelper
    participant Disk as Filesystem (Config/Addons)
    participant Memory as CLR AppDomain
    participant Addon as IOptiAssistAddon Instance
    participant UI as Sidebar / AddonHost Panel

    Host->>Disk: Scan for *.dll in Config/Addons and subdirectories
    Disk-->>Host: List of candidate DLL file paths
    loop For each DLL found
        Host->>Disk: File.ReadAllBytes(dllPath)
        Disk-->>Host: byte[] rawBytes
        Host->>Memory: Assembly.Load(rawBytes)
        Note over Host,Memory: Leaves the DLL file unlocked on disk (Anti-Lock)
        Memory-->>Host: Assembly instance
        Host->>Memory: Scan ExportedTypes for IOptiAssistAddon
        Host->>Addon: Activator.CreateInstance(type)
        Host->>Addon: Initialize(IOptiAssistHost host)
        Host->>Host: Extract AssemblyInfo metadata (Title, Company, Version)
        Host->>Host: Register AddonEntry into ObservableCollection
    end
    Host->>UI: RebuildSidebarAddonsMenu() creates RadioButtons
    Note over UI: View is NOT created yet (Lazy Loading)
    opt User clicks on Addon in Sidebar
        UI->>Host: NavButton_Click(RadioButton)
        Host->>Addon: CreateView() (invoked once and cached)
        Addon-->>Host: FrameworkElement (UI Tree)
        Host->>UI: panelAddonHost.Children.Add(view)
    end
    opt User changes language in Settings
        Host->>Addon: OnLanguageChanged(langCode)
        Addon-->>UI: Refreshes localized strings in active view
    end
```

### 3.2 Anti-Lock Loading Mechanism
Standard .NET assembly loading via `Assembly.LoadFrom(path)` acquires an exclusive operating system file lock on the `.dll` file, preventing updates or deletions until the host process terminates. 

OptiAssist resolves this constraint by reading the raw bytes into memory first:
```csharp
byte[] rawBytes = File.ReadAllBytes(sourceToLoad);
Assembly asm = Assembly.Load(rawBytes);
```
This enables technicians and developers to recompile, overwrite, or delete add-on modules on disk while OptiAssist is actively running.

### 3.3 Circular Dependency Resolution
Because add-on DLLs reference `OptiAssist.exe` for the `IOptiAssistAddon` and `IOptiAssistHost` interfaces, `AddonHelper` hooks into `AppDomain.CurrentDomain.AssemblyResolve`. When an add-on requests the host assembly, the handler immediately returns the running host instance:
```csharp
private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
{
    Assembly hostAsm = typeof(AddonHelper).Assembly;
    if (args != null && !string.IsNullOrEmpty(args.Name))
    {
        if (args.Name.StartsWith("OptiAssist,", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.Name, hostAsm.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return hostAsm;
        }
    }
    return null;
}
```

### 3.4 Lazy View Initialization
Add-on views are not created during application boot. `CreateView()` is deferred until the technician selects the module's tab in the sidebar for the first time via `entry.GetOrCreateView()`. Once created, the resulting `FrameworkElement` is cached in `AddonEntry.CachedView` for high performance during subsequent tab switches.

---

## 4. Sorting and Module Hierarchy

Discovered modules are sorted in the navigation sidebar according to `AddonHelper.GetModuleSortOrder(addon.Id)`:

| Priority Order | Module Identifier | Module Name | Core Status |
| :---: | :--- | :--- | :---: |
| **1** | `toolbox` | Toolbox | **Core Module** (`CanModify = false`, cannot be disabled or removed) |
| **2** | `app-manager` | App Manager | Optional Add-on (`CanModify = true`) |
| **3** | `system-cleaner` | System Cleaner | Optional Add-on (`CanModify = true`) |
| **4** | `startup-manager` | Startup Manager | Optional Add-on (`CanModify = true`) |
| **5** | `maintenance-report`| Maintenance Report | Optional Add-on (`CanModify = true`) |
| **50** | *custom* | External 3rd-party modules | Custom Add-on (`CanModify = true`) |
