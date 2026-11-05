# App Manager (Automated Software Installation & Updates)

**Module Identifier:** `AppManager`  
**Type:** Decoupled Modular Add-on (`IOptiAssistAddon`)  
**DLL Location:** `Config\Addons\AppManager\AppManager.dll`  
**Documentation File:** `docs/modulos/app-manager.md`  

---

## 1. Overview and Problem Solved

After formatting or repairing a Windows installation, setting up essential software (web browsers, archivers, PDF viewers, runtime libraries, remote support tools) is traditionally a slow, repetitive, and manual process. Technicians often download separate installers, click through endless setup wizards, and manually uncheck bundled adware.

The **App Manager** solves this problem by providing a dual-engine automated software manager:
1. **Application Updates (Winget)**: Automatically detects installed applications with newer versions available in Microsoft's official repository and updates them in batch or individually in 100% silent mode.
2. **Post-Installation Deployment**: Installs a curated list of essential software unattended. It uses offline installers from `Installers/` for maximum USB 3.0/SSD deployment speed and automatically falls back to silent Winget downloads when an installer is absent.
3. **In-App Visual Catalog Editor**: Allows technicians to add custom software, toggle between Local and Winget install types, configure custom silent arguments, and reorder installation priority without manually editing JSON files.

---

## 2. Step-by-Step Usage Guide

### 2.1 Checking and Updating Installed Applications
1. In the sidebar, click **📦 App Manager**.
2. Select the **🔄 Application Updates** tab.
3. Click **🔄 Check for Updates**. The application runs a background scan using Winget and populates the table with:
   - Application Name and ID.
   - Currently Installed Version.
   - Latest Available Version in the repository.
4. Mark the items you want to update (or click **Select All**).
5. Click **🔄 Update Selected** or **🚀 Update All**. The updates run silently in the background while streaming progress to the real-time log.

### 2.2 Unattended Post-Installation Deployment
1. Select the **📦 Post-Installation** tab.
2. Review the list of pre-configured applications (Google Chrome, 7-Zip, VLC Media Player, AnyDesk, Visual C++ Runtimes, etc.).
3. Choose your selection:
   - **Select Essentials**: Checks recommended standard tools.
   - **Select All / Deselect All**: Bulk toggles all checkboxes.
4. Click **🚀 Install Selected Applications**.
5. The execution engine processes items sequentially:
   - If a corresponding installer exists in the `Installers/` folder, it executes locally with silent switches (`/S`, `/SILENT`, `/qn`).
   - If missing locally and `EnableWingetFallback` is enabled in `settings.json`, it triggers `winget install --id <Id> --silent --accept-package-agreements --accept-source-agreements`.

### 2.3 Customizing the Catalog (`autoinstall.json`)
1. In the Post-Installation tab, click **⚙️ Customize Catalog**.
2. To add a software:
   - Enter the application name and select the type (**Local Installer** or **Winget**).
   - If Local, click **Browse...** to pick the setup file from `Installers/`.
   - If Winget, enter the official Package ID (e.g. `Mozilla.Firefox`).
   - Define custom silent arguments (e.g. `/VERYSILENT /NORESTART`).
   - Click **Add Application**.
3. Use the **▲ Move Up** and **▼ Move Down** buttons to prioritize prerequisites (e.g. Runtimes installed before dependent apps).
4. Click **💾 Save Catalog** to persist changes to `Config/autoinstall.json`.

---

## 3. Internal Architecture and Implementation

### 3.1 Upgrade Detection Flow
1. `CheckWingetUpgradesAsync()` runs in a background thread via `ThreadPool.QueueUserWorkItem`.
2. It launches `winget.exe upgrade` with standard output and error redirection.
3. The parser processes table lines, matching columns (`Name`, `Id`, `Version`, `Available`) and discarding header lines.
4. Extracted entries populate an `ObservableCollection<WingetUpgradeItem>` bound to the UI.

### 3.2 Post-Install Execution Flow
1. `BtnInstallSelected_Click` filters apps with `Selected == true`.
2. For each selected application, `InstallerHelper.InstallAppAsync()` evaluates:
   - **Local Mode**: Checks if `Path.Combine(baseDir, "Installers", app.LocalInstallerFile)` exists. If found, starts `Process.Start(psi)` with `app.SilentArgs`.
   - **Winget Fallback Mode**: If the local file is not found, switches to `winget.exe install --id {app.WingetId} --silent --accept-package-agreements --accept-source-agreements`.
3. Process stdout and stderr stream asynchronously to the central host log (`_host.Log`).

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * `Config/autoinstall.json`: Master software catalog.
  * Local installers directory (`Installers/`).
  * Winget repository metadata.
* **Outputs**:
  * Silently installed software packages.
  * Real-time logs of setup processes.
* **Configurations in `Config/settings.json`**:
  * `"EnableWingetFallback": true`: Toggles Winget fallback when local installers are missing.

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Required. Silent software installations write to `%ProgramFiles%`, `%ProgramData%`, and `HKLM`.
* **Supported Windows Versions**: Windows 10 (1809+) and Windows 11.
* **External Tool**: `winget.exe` (included natively in modern Windows via App Installer). If absent, App Manager operates in Local Installer mode.

---

## 6. Risks and Reversibility

* **System Alterations**: Installs third-party software on the machine.
* **Reversibility**: All installed applications can be uninstalled via Windows Control Panel or Winget (`winget uninstall <Id>`).

---

## 7. Error Handling and Logging

* Corrupted installer files or non-zero exit codes are logged to `MainWindow.Log`.
* If `winget.exe` is absent from `PATH`, the `Win32Exception` is safely caught and fallback logic is engaged without crashing the UI.

---

## 8. Related Source Files

* [`src/Addons/AppManager/AppManagerAddon.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/AppManager/AppManagerAddon.cs): Add-on view, controls, and event coordinators.
* [`src/Addons/AppManager/InstallerHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/AppManager/InstallerHelper.cs): JSON catalog manager, process execution, and Winget parsing.
