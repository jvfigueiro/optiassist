# Configuration, Localization, and Logging Subsystems

**Document:** `docs/logs-and-settings.md`  
**Platform:** Windows 10 / 11 (64-bit)  

---

## 1. Configuration Subsystem (`SettingsHelper.cs`)

OptiAssist implements a lightweight, zero-dependency JSON parser and serializer specifically designed to read and write configuration files without external NuGet libraries (such as `Newtonsoft.Json`).

### 1.1 Master Configuration Schema (`Config/settings.json`)
The primary configuration file resides at `Config/settings.json`:

```json
{
  "CustomerName": "OptiAssist",
  "ContactInfo": "https://github.com/jvfigueiro/optiassist",
  "Language": "en-US",
  "ReportSavePath": "C:\\MaintenanceReport.txt",
  "AutoRefreshHardware": true,
  "EnableWingetFallback": true,
  "Theme": "Light",
  "AccentColor": "#0EA5E9",
  "ServiceProviderMode": false,
  "EnableMaintenanceReport": false,
  "OpenExternalLogsByDefault": false,
  "AlwaysOnTop": false,
  "CustomLogoPath": "",
  "freshstart": "true",
  "DisabledAddons": ""
}
```

### Field Definitions:

| Property | Type | Default | Purpose |
| :--- | :---: | :---: | :--- |
| `CustomerName` | `string` | `"OptiAssist"` | Company or technician name rendered in maintenance report headers. |
| `ContactInfo` | `string` | `"https://..."` | Support URL, phone number, or email address for customer reports. |
| `Language` | `string` | `"en-US"` | Active localization code (`en-US`, `pt-BR`). |
| `ReportSavePath` | `string` | `"C:\\MaintenanceReport.txt"` | Default output file path for generated maintenance reports. |
| `AutoRefreshHardware` | `bool` | `true` | Automatically triggers background hardware audit on startup and tab change. |
| `EnableWingetFallback` | `bool` | `true` | Allows App Manager to fall back to Microsoft Winget if an offline installer is missing. |
| `Theme` | `string` | `"Light"` | UI color theme: `"Light"` or `"Dark"`. |
| `AccentColor` | `string` | `"#0EA5E9"` | Hex color string for UI buttons, progress bars, and highlights. |
| `ServiceProviderMode` | `bool` | `false` | Enables service provider branding features. |
| `EnableMaintenanceReport`| `bool` | `false` | Controls default reporting checkboxes. |
| `OpenExternalLogsByDefault`| `bool` | `false` | Automatically opens the floating `LogWindow` when switching to Logs. |
| `AlwaysOnTop` | `bool` | `false` | Keeps the main application window pinned above other windows. |
| `CustomLogoPath` | `string` | `""` | Path to a custom PNG image to display on the top bar header. |
| `freshstart` | `string` | `"true"` | When `"true"`, triggers the first-run Welcome Wizard on launch. Sets to `"false"` once completed. |
| `DisabledAddons` | `string` | `""` | Comma-separated list of disabled add-on IDs (e.g. `"app-manager,system-cleaner"`). |

### 1.2 Auxiliary JSON Catalogs
- `Config/custom_shortcuts.json`: Defines custom shortcut items for the Dashboard Common Tasks grid (`Id`, `Title`, `Icon`, `TargetType`, `TargetValue`, `Arguments`, `ButtonVariant`).
- `Config/Addons/Toolbox/toolbox.json`: Catalog of maintenance tools, categories, commands, and execution types.
- `Config/Addons/AppManager/autoinstall.json`: Catalog of applications for automated post-installation deployment.

---

## 2. Multi-Language Localization Engine (`LocalizationHelper.cs`)

OptiAssist features a high-performance, zero-restart internationalization engine.

### 2.1 File Organization and Discovery
- **Global Dictionaries**: Stored in `Config/Languages/<langCode>.json` (e.g. `en-US.json`, `pt-BR.json`).
- **Module Dictionaries**: Stored inside each add-on directory in `Config/Addons/<ModuleName>/<langCode>.json`.

### 2.2 Dynamic Merging and Fallback Resolution
During startup and add-on discovery, `LocalizationHelper.LoadAddonLanguageFiles()` automatically merges all per-module dictionaries into the global dictionary in memory.

When a localized string is requested via `LocalizationHelper.Get(key, defaultValue)`:
1. The engine checks if the key exists in the active language dictionary (`CurrentLanguage`).
2. If missing, it checks the fallback dictionary (`en-US`).
3. If still missing, it returns the provided `defaultValue`.

### 2.3 Language Metadata
Translation JSON files include metadata at their root:
```json
{
  "_LanguageTitle": "English (US)",
  "_LanguageAuthor": "João Figueiró",
  ...
}
```
The Settings language dropdown reads these properties directly from the disk files, ensuring new community translations appear with native names and author credits without modifying code.

### 2.4 Zero-Restart Hot-Reloading
When the technician selects a new language in Settings:
1. `LocalizationHelper.LoadLanguage(newLangCode)` reloads dictionary keys into memory.
2. `MainWindow.UpdateAllLocalizations()` updates all UI labels, button texts, and tooltips on native views.
3. The host notifies all loaded add-ons by invoking `IOptiAssistAddon.OnLanguageChanged(newLangCode)`.

---

## 3. Logging Subsystem (`MainWindow.Log` & `LogWindow.cs`)

OptiAssist maintains a thread-safe, centralized logging pipeline for all hardware audits, script executions, subprocess streaming, and error traces.

### 3.1 Central Thread-Safe Pipeline
All logs converge into `MainWindow.Log(string message)`:
```csharp
public void Log(string message)
{
    if (!Dispatcher.CheckAccess())
    {
        Dispatcher.Invoke(new Action(() => Log(message)));
        return;
    }

    txtConsoleLog.AppendText(message + Environment.NewLine);
    txtConsoleLog.CaretIndex = txtConsoleLog.Text.Length;
    txtConsoleLog.ScrollToEnd();

    if (_externalLogWindow != null)
    {
        _externalLogWindow.AppendLog(message);
    }
}
```

### 3.2 Detached Floating Window (`LogWindow.cs`)
Technicians can detach the terminal by clicking **Pop-Out Window**:
- Operates as an independent WPF window (`LogWindow`).
- Can be dragged to a secondary monitor during long-running tasks.
- Supports real-time search filtering, one-click clipboard copying, and `.txt` file export.
