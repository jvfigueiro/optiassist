# Chapter 02: App Manager & Post-Installation

The **App Manager** module automates the setup and maintenance of essential software packages after formatting or provisioning Windows workstations.

---

## 📦 Dual Installation Engine

OptiAssist features a hybrid installation pipeline:

```
[Target Program Selection]
           │
           ▼
 Does offline installer exist in "PostInstall/"?
        /      \
      YES       NO
      /          \
    ▼             ▼
Run Local      Is Winget enabled & connected?
Installer             /          \
(Silent)            YES           NO
                    /              \
                  ▼                 ▼
             Run Winget       Skip & Report
            Silent Install        in Logs
```

1. **Local Offline Engine**:
   * Scans the portable `PostInstall/` directory for executables (`.exe`, `.msi`, `.msixbundle`).
   * Executes installers using proven silent command-line arguments (e.g., `/S`, `/silent`, `/qn`).
   * Runs completely offline at full USB 3.0/SSD speed.
2. **Winget Repository Fallback Engine**:
   * If the installer file is not found in `PostInstall/` and the setting `EnableWingetFallback` is active, OptiAssist queries the official Microsoft Windows Package Manager (Winget).
   * Automatically executes `winget install --id <AppId> --silent --accept-package-agreements --accept-source-agreements`.

---

## ⚙️ Customizing Applications (`autoinstall.json`)

All programs displayed in the installer catalog are defined in `Config/autoinstall.json`.

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

### Visual App Catalog Editor:
Clicking **⚙️ Customize Automated Installation** unlocks an in-app visual editor where you can:
* **Add New Programs**: Specify Name, Type (`Local` vs. `Winget`), installer path or Package ID, and default selection state.
* **Reorder Programs**: Move items up or down with `▲` / `▼` buttons to enforce installation sequence (e.g., run runtimes before software).
* **Edit or Delete**: Modify package arguments or remove programs from the list.
* **Save Changes**: Persists modifications to `Config/autoinstall.json` immediately.
* **Reset to Defaults**: Restores factory-bundled catalog defaults.

---

## 🔄 Application Updates (Winget Scanner)

OptiAssist integrates with `winget upgrade` to detect outdated applications installed on the system:

1. **Background Discovery**: Automatically checks for outdated packages upon launching the App Manager tab.
2. **Selective Updates**: Check individual applications in the list and click **🔄 Update Selected**.
3. **Batch Upgrade**: Click **🚀 Update All** to run an automated unattended upgrade of all outdated packages.
4. **Console Monitoring**: Direct link to the **Logs** tab allows monitoring the download and installation progress in real time.
