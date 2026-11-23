# Settings & Deployment (`v1.2.S7`)

## 1. Overview & Problem Solved
Technicians need to brand OptiAssist for their workshop, switch Light/Dark themes and languages without restarting, manage modular `.dll` add-ons, and deploy OptiAssist either portably on USB drives or as an installed application in `%ProgramFiles%\OptiAssist`.

## 2. How to Use (Step-by-Step)
1. Click **Settings** in the sidebar.
2. Select **Application Language** (`en-US` or `pt-BR`), toggle **Light / Dark Mode**, pick an **Accent Color**, set a **Custom application name**, or **Change App Logo**.
3. Under **Installed Modules & Plugins**, enable/disable, reorder (`▲`/`▼`), add (`.dll`), or remove add-on modules.
4. Click **Install OptiAssist** (or use **OptiAssist Installer** on first run / **Install on PC** on the Dashboard) to choose **Install on PC** or **Portable Mode** (enables all modules and closes the installer immediately).

## 3. How It Works Internally
1. `SettingsHelper.Save(...)` persists `Config\settings.json`; `MainWindow.ApplyTheme(...)` updates dynamic `Application.Current.Resources` brushes; `MainWindow.ApplyLanguage(...)` hot-reloads dictionaries and calls `FluentIcons.UpgradeVisualTree(this)`.
2. `AppDeploymentHelper.DeployApplicationFiles(...)` (`src/ShortcutsHelper.cs`) copies `OptiAssist.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `SOURCE-INFO.md`, `THIRD-PARTY\`, `Config\Data\` (including `Config\Data\OptiBoost\`), `Config\Languages\`, and selected `Config\Addons\` into `%ProgramFiles%\OptiAssist` (creating Start Menu / Desktop `.lnk` shortcuts and Control Panel `Uninstall` Registry entry) or a portable target directory.

## 4. Inputs, Outputs & Configuration
- **Inputs:** User preferences, custom logo image, `.dll` add-on files.
- **Outputs:** `Config\settings.json`, `Config\custom_logo.*`, `%ProgramFiles%\OptiAssist\`, shortcuts, `HKLM\...\Uninstall\OptiAssist`, optional `autorun.inf`.

## 5. Requirements
- **Admin:** Required for `%ProgramFiles%\OptiAssist` installation and `HKLM` Uninstall registration.
- **Windows Version:** Windows 10 / 11.
- **External Dependencies:** None.

## 6. Risks & Side Effects
- Installing on PC writes to `%ProgramFiles%\OptiAssist`, Public Desktop, Start Menu, and `HKLM` Uninstall key; fully reversible via **Uninstall OptiAssist** (`--uninstall`).

## 7. Logs & Error Handling
- Logs `[CONFIG]`, `[LANGUAGE]`, `[THEME]`, `[ADDON]`, `[INSTALL]`, and `[UNINSTALL]` events; surfaces validation errors via `OptiDialog`.

## 8. Related Source Files
- `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`
- `src/SettingsHelper.cs`, `src/LocalizationHelper.cs`, `src/AddonHelper.cs`, `src/ShortcutsHelper.cs` (`AppDeploymentHelper`)
