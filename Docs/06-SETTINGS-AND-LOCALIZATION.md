# Chapter 06: Settings & Localization Architecture

The **Settings** module handles runtime application preferences, UI personalization, portable USB configuration, and the multi-language localization engine.

---

## ⚙️ Configuration File (`Config/settings.json`)

All configuration is stored portably in `Config/settings.json`. Preferences update and persist automatically as soon as the user alters an option in the UI.

### Settings Parameters Reference:

| Key | Type | Default | Description |
|---|---|---|---|
| `CustomerName` | String | `"OptiAssist"` | Custom company/technician branding name. |
| `ContactInfo` | String | `"https://github.com/jvfigueiro/optiassist"` | Contact email, phone number, or URL. |
| `Language` | String | `"en-US"` | Active UI language code (`en-US`, `pt-BR`, etc.). |
| `ReportSavePath` | String | `@"C:\MaintenanceReport.txt"` | Default file path for exported maintenance reports. |
| `AutoRefreshHardware` | Boolean | `true` | Automatically run WMI hardware discovery on startup. |
| `EnableWingetFallback` | Boolean | `true` | Use Winget repository when an installer is missing offline. |
| `Theme` | String | `"Dark"` | Active UI visual mode (`"Dark"` or `"Light"`). |
| `AccentColor` | String | `"#0EA5E9"` | Primary accent and button color in hexadecimal format. |
| `ServiceProviderMode` | Boolean | `false` | Enables custom branding, Service Order inputs, and reports. |
| `EnableMaintenanceReport` | Boolean | `false` | Shows or hides the Maintenance Report tab in the sidebar. |

---

## 💿 Portable USB AutoRun Integration

Technicians can deploy OptiAssist directly onto bootable or maintenance USB pen drives. The **Create USB AutoRun** button generates or updates `autorun.inf` directly at the drive's root:

```ini
[AutoRun]
open={relative_exe_path}
action=Executar o OptiAssist
icon={relative_exe_path},0
```

* **Preserves Volume Label**: Does not overwrite or modify the existing USB volume name.
* **AutoPlay Action**: Configures Windows AutoPlay to display the action `"Executar o OptiAssist"`.
* **Application Icon**: Sets the drive icon in Windows Explorer to the OptiAssist application icon directly from the executable.
* **Attribute Handling**: Safely clears any existing read-only or hidden flags on existing `autorun.inf` files before updating.

---

## 🌐 Dynamic Multi-Language Localization Engine

OptiAssist features a fully dynamic, zero-restart localization system:

1. **File-based Dictionary Architecture**:
   * Language files reside in `Config/Languages/<locale>.json` (e.g., `en-US.json`, `pt-BR.json`).
   * Adding a new language simply requires dropping a new `<locale>.json` file into `Config/Languages/`.
2. **Automatic Language Discovery**:
   * On startup and in the Settings panel, OptiAssist scans `Config/Languages/` and dynamically generates language selection buttons.
   * Reads the `LanguageName` property (e.g., `"Português (Brasil)"`, `"English (United States)"`) to display the native language name.
3. **Instant Runtime Switching**:
   * Clicking any language button immediately re-renders every tab, title, subtitle, card, button, tooltip, placeholder, and dialog message across the application without requiring a restart.
   * Actively highlights the selected language button using the accent color.

---

## 🎨 Personalization & Themes

### Dark and Light Modes:
* **Dark Mode (Default)**: Deep slate backgrounds (`#0F172A`, `#1E293B`) optimized for low-eye-strain usage during workshop servicing.
* **Light Mode**: Crisp, high-contrast white and soft-gray backgrounds (`#F8FAFC`, `#FFFFFF`) for brightly lit environments.

### Accent Color Customization:
* Users can specify any custom hexadecimal color code (e.g., `#0EA5E9`, `#10B981`, `#8B5CF6`).
* Integrated Windows Color Picker allows visual color selection.
* Quick preset buttons provide one-click selection of verified accessible color themes (Blue, Green, Purple, Orange, Red, Yellow, Pink).
* When applied, the accent color dynamically re-calculates hover and pressed border shades in real time.
