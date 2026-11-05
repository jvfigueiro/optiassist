# Settings & Deployment (Settings)

**Module Identifier:** `settings`  
**Type:** Host Native View (`MainWindow`)  
**Documentation File:** `docs/modulos/settings-deployment.md`  

---

## 1. Overview and Problem Solved

A professional maintenance workbench must be adaptable to different technician environments and client preferences. Technicians require fine-grained control over UI themes, accent colors, localization languages, window behaviors, and modular plugin activations, as well as the ability to deploy the workbench permanently onto client machines or portable technician drives.

The **Settings & Deployment** module centralizes:
- Visual personalizations: Light/Dark themes and custom accent colors.
- Zero-restart multi-language localization (i18n).
- Behavior toggles: *Always on Top*, *Open External Logs by Default*, and *Auto Refresh Hardware*.
- Independent Add-on management: dynamically enable or disable external DLL modules.
- Built-in installation and uninstallation subsystem (`AppDeploymentHelper`).

---

## 2. Step-by-Step Usage Guide

### 2.1 Interface Customization
1. In the sidebar, click **⚙️ Settings**.
2. **Theme**: Choose between **Light** and **Dark** modes.
3. **Accent Color**: Select from preset palettes or click **Custom Color...** to pick any hexadecimal color.
4. **Language**: Choose between **English (US)** and **Português (Brasil)**. All UI strings update instantly.

### 2.2 Operational Settings
- **Always on Top**: Check to keep the application window above all other windows during benchmarking and diagnostics.
- **Open External Logs by Default**: Check to automatically launch the floating log console upon opening OptiAssist.
- **Enable Maintenance Report**: Check to display the Report generator tab in the sidebar.

### 2.3 Managing Add-ons and Plugins
- Review the **🧩 Add-ons & Modular Plugins** section.
- Use the checkboxes to enable or disable specific modules. Disabled modules are excluded from the sidebar navigation.

### 2.4 Deploying OptiAssist (`AppDeploymentHelper`)
- Click **Install OptiAssist** to open the deployment dialog:
  - Review GNU GPLv3 terms.
  - Select **Standard Installation** (copies to `%ProgramFiles%\OptiAssist`, creates Start/Desktop shortcuts, and registers in Control Panel) or **Portable Installation** (copies to any directory or USB drive).
  - Click **💿 Create USB AutoRun** to write `autorun.inf` to the root of a technician flash drive.
- Click **Uninstall OptiAssist** to cleanly remove all files, shortcuts, and registry entries.

---

## 3. Internal Architecture and Implementation

### 3.1 Theme Engine
Switching themes invokes `ApplyTheme(themeName)`, which redefines dynamic resource brushes (`BrushBgPrimary`, `BrushBgCard`, `BrushTextPrimary`, etc.) in `Application.Current.Resources`.

### 3.2 Dynamic Language Hot-Reloading
Switching languages triggers `MainWindow.SetLanguage(langCode)`:
1. `LocalizationHelper.LoadLanguage(langCode)` updates the active dictionary.
2. The host updates all native text blocks and buttons.
3. The host notifies active modules via `addon.OnLanguageChanged(langCode)`.

### 3.3 Self-Installation Subsystem
[`AppDeploymentHelper`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/ShortcutsHelper.cs#L342) manages:
- Recursive file copying via `CopyDirectoryRecursive`.
- Shortcut creation via COM reflection (`WScript.Shell`).
- Windows Registry uninstall key registration in `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist`.
- Delayed self-deletion via `cmd.exe /c ping 127.0.0.1 -n 3 >nul & rd /s /q "<dir>"`.

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**: User selections in settings forms.
* **Outputs**: Updated `Config/settings.json`, system shortcuts, and registry keys.
* **Configurations**: Handled via [`SettingsHelper`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/SettingsHelper.cs).

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Required for Standard installation, uninstallation, and `HKLM` registry modifications.
* **Supported Windows Versions**: Windows 10 and Windows 11.
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **Safe Customization**: All settings changes are fully reversible.
* **Clean Uninstallation**: Removes all shortcuts and registry keys cleanly.

---

## 7. Error Handling and Logging

* All configuration saves and deployment actions stream status and error logs to `MainWindow.Log`.

---

## 8. Related Source Files

* [`src/MainWindow.xaml`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml): Settings view layout.
* [`src/MainWindow.xaml.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml.cs): Settings coordinator, theming, and i18n triggers.
* [`src/SettingsHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/SettingsHelper.cs): JSON serialization for `AppSettings`.
* [`src/ShortcutsHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/ShortcutsHelper.cs): `AppDeploymentHelper` implementation.
