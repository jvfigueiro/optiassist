# Chapter 06: Settings, Logs & Personalization

The **Settings** module manages application preferences, UI theming, dynamic multi-language translation, portable USB deployment, and the central logging system.

---

## ⚙️ Configuration Persistence (`Config/settings.json`)

All configuration parameters are stored portably in `Config/settings.json`. Preferences are automatically serialized to disk as soon as any option is altered in the user interface.

### Complete Settings Parameters Reference:

| Key | Type | Default | Description |
|---|---|---|---|
| `CustomerName` | String | `"OptiAssist"` | Custom company or technician branding title. |
| `ContactInfo` | String | `"https://github.com/jvfigueiro/optiassist"` | Support email, website, phone number, or social media link. |
| `Language` | String | `"en-US"` | Active UI language code (`en-US`, `pt-BR`, etc.). |
| `ReportSavePath` | String | `@"C:\MaintenanceReport.txt"` | Default target path for exported local client maintenance reports. |
| `AutoRefreshHardware` | Boolean | `true` | Asynchronously executes WMI hardware discovery on application startup. |
| `EnableWingetFallback` | Boolean | `true` | Allows Winget repository fallback when an offline installer is missing. |
| `Theme` | String | `"Dark"` | Active visual theme mode (`"Dark"` or `"Light"`). |
| `AccentColor` | String | `"#0EA5E9"` | Primary accent and button color in 6-digit hexadecimal format. |
| `ServiceProviderMode` | Boolean | `false` | Enables custom branding, Service Order inputs, and technical reports. |
| `EnableMaintenanceReport` | Boolean | `false` | Controls whether the Maintenance Report tab is rendered in the sidebar. |
| `OpenExternalLogsByDefault` | Boolean | `false` | Automatically opens the floating `LogWindow` whenever OptiAssist launches. |
| `CustomLogoPath` | String | `""` | Relative path to custom application logo image stored in `Config/`. |
| `DisabledAddons` | Array | `[]` | List of module IDs (`string[]`) that are currently disabled by the user. |

---

## 🛡️ Strict Administrator Elevation Enforcement

OptiAssist is engineered specifically for deep operating system maintenance, requiring elevated administrative privileges. 

1. **Application Manifest**: Embedded with `requestedExecutionLevel level="requireAdministrator"`.
2. **Runtime Verification**: Upon startup (`App.xaml.cs`), `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)` verifies elevated status.
3. **Safety Protection**: If elevation is not granted (e.g., invoked via an un-elevated shell or restricted user account), OptiAssist displays a localized error notification informing the user that OptiAssist runs exclusively as Administrator, and safely terminates execution.

---

## 📊 Centralized Logging & Floating `LogWindow`

Every operation—from package installation and custom command execution to BleachBit cleaning and system integrity checks—pipes its output directly to the logging engine.

### 1. In-App Logs Tab
- **Live Output Stream**: Asynchronous, thread-safe message ingestion with millisecond timestamps.
- **Search & Filter**: Real-time text filtering box to quickly find error codes, file paths, or warnings.
- **`📋 Copy All`**: Copies the complete console buffer to the clipboard.
- **`💾 Save to File`**: Exports the log stream to a `.txt` file for audit or troubleshooting records.
- **`🗑️ Clear`**: Clears the current console view.
- **Auto-Scroll**: Keeps the newest lines in view automatically.

### 2. Independent Floating `LogWindow` (`LogWindow.cs`)
- **Pop-out Execution**: Clicking the **`🪟 Pop-out Logs`** button opens a detached, floating window that stays on top or can be placed on a secondary monitor.
- **Auto-Open on Execution**: Whenever any command in **Toolbox** is run, the external `LogWindow` automatically appears to give immediate feedback.
- **Theme Synchronization**: The window dynamically listens to theme changes, rendering in Dark Mode (`#0F172A`) or Light Mode (`#FFFFFF`) with synchronized borders and typography.
- **Dedicated Toolbar**: Independent search box, clear button, copy button, and save/export button.
- **Startup Preference**: When **"Open external logs window by default"** is enabled in Settings, the pop-out window launches alongside the main window.

---

## 🌐 Dynamic Zero-Restart Localization (i18n)

OptiAssist implements a zero-restart internationalization architecture:

1. **Flat JSON Dictionaries**:
   * Stored in `Config/Languages/<locale>.json` (`en-US.json`, `pt-BR.json`).
   * Exactly 472 keys per file, with 100% key parity between English and Brazilian Portuguese.
2. **Instant In-Memory Translation**:
   * Changing the language in Settings instantly updates every title, tab, button, card, tooltip, and dialog in memory without requiring an application restart.
3. **Extensibility**:
   * Technicians can add support for new languages (e.g., Spanish `es-ES.json` or German `de-DE.json`) simply by adding the file to `Config/Languages/`. The application automatically discovers it on startup and generates a corresponding language selection button.

---

## 🎨 Themes, Branding & Personalization

### Fluent Themes
* **Dark Mode (Default)**: Deep slate palette (`#0F172A`, `#1E293B`, `#334155`) engineered for low eye strain in dimly lit workshops.
* **Light Mode**: High-contrast, clean modern interface (`#F8FAFC`, `#FFFFFF`, `#E2E8F0`) for brightly lit retail or office environments.

### Custom Branding & Logo
* **Custom Application Name**: Allows setting a personalized business name (e.g. your repair shop name) that seamlessly replaces "OptiAssist" across the top bar.
* **Change App Logo**: Clicking **`🖼️ Change App Logo`** allows selecting an image (`.png`, `.jpg`, `.ico`, `.bmp`) that is automatically copied to `Config/custom_logo.<ext>` and loaded into both the top header and About screen.

### Custom Accent Colors
* **Hex Input**: Enter any custom 6-digit hex color code (e.g., `#0EA5E9`, `#10B981`, `#8B5CF6`).
* **Color Dialog**: Visual Windows Color Picker for intuitive color selection.
* **Preset Palette**: 7 curated presets (Blue, Light Green, Purple, Orange, Red, Yellow, Pink).
* **Dynamic Shade Computation**: OptiAssist automatically computes matching hover, pressed, and focus shades for borders and buttons in real time.

---

## 📦 Additional Settings & System Deployment

Located under the **Configurações Adicionais (Additional Settings)** card:

### 1. Portable USB AutoRun Integration
The **`💿 Create USB AutoRun`** button configures the portable drive for immediate execution upon insertion into a computer:
```ini
[AutoRun]
open={relative_exe_path}
action=Executar o OptiAssist
icon={relative_exe_path},0
```
* **Preserves Drive Metadata**: Does not alter or wipe the existing drive label.
* **Native Windows Icon**: Links the USB drive icon in Windows Explorer directly to the embedded high-resolution OptiAssist icon.
* **Safe Attributes**: Automatically strips read-only or hidden flags on existing `autorun.inf` files before updating.

### 2. Install OptiAssist (`InstallOptiAssist`)
Installs the full OptiAssist application suite to the host computer:
* Copies the application, configuration, modules, and icons to `C:\Program Files\OptiAssist`.
* Generates native Windows shell shortcuts (`.lnk`) on the **Common Desktop** and **Start Menu**.
* Registers OptiAssist in the Windows Registry under `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist` with full metadata (Publisher, DisplayVersion, DisplayIcon, and UninstallString).

### 3. Uninstall OptiAssist (`UninstallOptiAssist`)
Safely removes OptiAssist from the system:
* Removes Desktop and Start Menu `.lnk` shortcuts.
* Deletes the uninstallation registry key from `HKLM`.
* Cleans up the installation directory in `Program Files`.
* **Native Windows Settings Integration**: Supports uninstallation via Windows "Installed Apps" / Control Panel using the command line flag `OptiAssist.exe --uninstall`.
