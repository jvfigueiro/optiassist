# Chapter 02: App Manager & Post-Installation

The **App Manager** module automates the setup, deployment, and ongoing maintenance of software packages on fresh Windows installations or serviced workstations. It eliminates manual, repetitive browser downloads by combining a lightning-fast offline installer engine with official online repository fallback.

---

## 📦 Dual Installation Engine

OptiAssist implements an intelligent hybrid installation pipeline:

```text
[Target Program Selected for Installation]
                   │
                   ▼
  Does offline installer exist in "Installers/"?
                 /      \
               YES       NO
               /          \
             ▼             ▼
      Run Local         Is Winget fallback enabled
      Installer         and internet connected?
      (Silent)                 /          \
                             YES           NO
                             /              \
                           ▼                 ▼
                      Run Winget        Skip item & report
                    Silent Install      in central Logs
```

### 1. Local Offline Engine
* Scans the portable `Installers/` directory for executables (`.exe`, `.msi`, `.msixbundle`).
* Executes installers using verified silent command-line arguments (e.g., `/S`, `/silent`, `/qn`, `/norestart`).
* Operates completely offline at maximum USB 3.0 / NVMe SSD read speeds, drastically cutting deployment time across multiple machines.

### 2. Winget Repository Fallback Engine
* When an installer is not present in `Installers/` and the setting `EnableWingetFallback` is active, OptiAssist queries the official Microsoft Windows Package Manager (Winget).
* Automatically invokes:
  ```cmd
  winget install --id <AppId> --silent --accept-package-agreements --accept-source-agreements
  ```
* Provides a seamless safety net for packages that change frequently or when storage on the USB drive is constrained.

---

## ⚙️ Customizing Applications (`Config/autoinstall.json`)

All applications listed in the automated catalog are persisted in `Config/autoinstall.json`.

### Schema Example:
```json
[
  {
    "Name": "Google Chrome",
    "Type": "Winget",
    "WingetId": "Google.Chrome",
    "LocalFileName": "ChromeSetup.exe",
    "PreSelected": true,
    "Category": "Browsers",
    "SilentArgs": "/silent /install"
  },
  {
    "Name": "7-Zip",
    "Type": "Local",
    "WingetId": "7zip.7zip",
    "LocalFileName": "7z2408-x64.exe",
    "PreSelected": true,
    "Category": "Utilities",
    "SilentArgs": "/S"
  }
]
```

### Visual App Catalog Editor
Clicking **`⚙️ Customize Automated Installation`** unlocks an in-app visual management dialog:
* **Add New Programs**: Specify visual Name, Type (`Local` vs. `Winget`), installer filename in `Installers/` or Winget Package ID, Category, and default selection state.
* **Reorder Packages**: Move items up or down with **`▲`** / **`▼`** controls to enforce strict execution sequences (e.g., install Visual C++ runtimes and .NET prerequisites before user applications).
* **Edit or Delete**: Fine-tune custom silent argument strings, update package IDs, or delete unnecessary programs.
* **Direct File Picking**: When choosing `Local`, a file picker lets you select any installer directly inside the `Installers/` folder.
* **Persist & Reset**: Changes are immediately saved to `Config/autoinstall.json`. A **Reset Defaults** option restores factory presets at any time.

### Footer Quick Actions
- **`Select Essentials`**: Quickly checks all items flagged as `PreSelected = true`.
- **`Deselect All`**: Clears all checkboxes with one click.
- **`📂 Installers Folder`**: Opens the local `Installers/` directory in Windows Explorer.
- **`🚀 Install Selected`**: Launches unattended silent installation of all checked applications sequentially, streaming live status to the central Logs console.
- **Integration with Dashboard**: Clicking **`🚀 Run Post-Install`** on the Dashboard switches to App Manager and automatically triggers `Install Selected` without extra clicks.

---

## 🔄 Application Updates (Winget Scanner)

OptiAssist integrates with `winget upgrade` to detect outdated packages on the target system:

1. **Background Discovery**: Automatically checks for outdated packages when opening the App Manager tab.
2. **Selective Upgrades**: Check individual applications in the list and click **`🔄 Update Selected`**.
3. **Batch Upgrade**: Click **`🚀 Update All`** to perform an automated, unattended upgrade of all outdated software.
4. **Progress & Console Monitoring**: Displays a real-time progress bar during upgrade execution and provides an integrated **`⬛ View Logs`** button to monitor download percentages and installation logs live.
