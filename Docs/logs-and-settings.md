# Configuration, Localization & Logging (`v1.2.S7`)

---

## 1. Global Configuration (`SettingsHelper`)

- **File:** `Config\settings.json` (auto-created with factory defaults on first run if absent).
- **Serializer:** Custom zero-dependency JSON parser/writer (`SettingsHelper.cs`).
- **Key Properties (`AppSettings`):**
  - `CustomerName`, `ContactInfo`, `CustomLogoPath` — custom header branding.
  - `Language` (`en-US` default, or `pt-BR`), `Theme` (`Light` default, or `Dark`), `AccentColor` (`#0EA5E9` default).
  - `AutoRefreshHardware` (`true`), `EnableWingetFallback` (`true`), `OpenExternalLogsByDefault` (`false`), `AlwaysOnTop` (`false`).
  - `freshstart` (`"true"` by default; triggers the **OptiAssist Installer** wizard on first run, transitioning to `"false"` once Portable Mode or PC installation is chosen).
  - `DisabledAddons` (comma-separated IDs, default `"maintenance-report"`).
  - `ModuleOrder` (default: `toolbox,app-manager,system-cleaner,startup-manager,hardware-monitor,maintenance-report`).
  - `DebloaterProfileOverride` (persisted OptiBoost hardware profile selection).

---

## 2. Common Tasks Persistence (`CustomShortcutsHelper`)

- **File:** `Config\custom_shortcuts.json`
- Stores Dashboard quick-action buttons (`Id`, `Title`, `Icon`, `TargetType` = `Section` or `Custom`, `TargetValue`, `Arguments`), customizable via **Personalize Common Tasks...** in `MainWindow.xaml.cs`.

---

## 3. Localization (`LocalizationHelper`)

- **Core Dictionaries:** `Config\Languages\en-US.json` and `Config\Languages\pt-BR.json`.
- **Module Dictionaries:** `Config\Addons\<ModuleName>\<langCode>.json`, merged dynamically by `AddonHelper.LoadAddonLocalization`.
- **Hot-Reload:** Changing the language in **Settings** calls `MainWindow.ApplyLanguage(langCode)`, reloads dictionaries, refreshes all core XAML elements, invokes `IOptiAssistAddon.OnLanguageChanged(langCode)` on all loaded modules, and re-runs `FluentIcons.UpgradeVisualTree(this)`.

---

## 4. Logging & External Terminal (`MainWindow.xaml.cs` & `LogWindow.cs`)

- **Thread-Safe Pipeline:** `MainWindow.Log(string message)` marshals updates via `Dispatcher.Invoke` to:
  1. The internal log buffer (`txtConsoleLog`).
  2. A per-process session log file `%TEMP%\OptiAssist_Terminal_<pid>.log` (UTF-8, protected by `_logFileLock`).
  3. Any open floating `LogWindow` instance.
- **Native External Terminal (`OpenExternalLogWindow`):**
  - Clicking **Terminal** in the sidebar writes `%TEMP%\OptiAssist_Terminal_<pid>.ps1` (running `Get-Content -LiteralPath ... -Wait -Encoding UTF8`) and launches Windows Terminal (`%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe`) with `-File` (preventing `wt.exe` from splitting `;` into broken tabs), falling back to `powershell.exe` or `cmd.exe` if Windows Terminal is absent.
