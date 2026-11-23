# Dashboard (`v1.2.S7`)

## 1. Overview & Problem Solved
Technicians servicing unfamiliar Windows machines need an immediate, trustworthy snapshot of system health, hardware specifications, OS activation/build, security posture, and quick-launch utilities without opening multiple Windows consoles. The **Dashboard** consolidates these into a single landing view.

## 2. How to Use (Step-by-Step)
1. Launch `OptiAssist.exe` (accept UAC elevation).
2. Inspect the **Health Hero Card**: review the dynamic status icon (green checkmark when 0 recommendations are pending, yellow/amber warning triangle for 1–2 recommendations, or red exclamation circle for 3+ recommendations), headline summary, and any integrated diagnostic recommendation strips (click the action button to jump/fix, or `✕` to dismiss for the session).
3. Use the top-right hero controls to toggle **Light / Dark Mode** or click **Install on PC** (visible in Portable Mode).
4. Review the **Hardware Info**, **Operating System**, and **Storage** cards (click **Copy** next to Serial Number to copy it to the clipboard).
5. Launch frequent workflows from **Common Tasks**, or click **Personalize Common Tasks...** to add, edit, reorder, or reset shortcuts.
6. Check the bottom status bar for **Firmware (UEFI/Legacy)**, **Secure Boot**, **TPM** (clickable -> `tpm.msc`), and **BitLocker** (clickable -> Device Encryption settings).

## 3. How It Works Internally
1. `MainWindow_Loaded` triggers `RefreshHardwareAsync()` (when `AutoRefreshHardware` is `true`) on a `ThreadPool` worker, guarded by a 30-second `DispatcherTimer` timeout.
2. `SystemInfoHelper.GetSystemInfo()` queries WMI/CIM (`Win32_Processor`, `Win32_BaseBoard`, `Win32_BIOS`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_DiskDrive`, `Win32_OperatingSystem`, `Win32_PnPEntity`, `SoftwareLicensingProduct`) and Registry keys (TPM, Secure Boot, Defender real-time protection, pending reboot).
3. `PlatformDiagnosticService.Instance.GetSnapshot(...)` evaluates hardware/driver findings and merges recommendations into `UpdateDashboardHealthHero(...)`, updating the 3-state Fluent UI status icon (`pathHealthIcon`) and recommendation strips based on the active recommendation count.
4. `RenderCommonTasks()` reads `Config\custom_shortcuts.json` via `CustomShortcutsHelper.Load()` and renders the 3-column `UniformGrid`.

## 4. Inputs, Outputs & Configuration
- **Inputs:** Live WMI/CIM telemetry, Registry security/OS state, `PlatformDiagnosticSnapshot`, `Config\custom_shortcuts.json`.
- **Outputs:** Visual Bento cards, clipboard serial number copy, launched target sections/commands.
- **Configuration:** `AutoRefreshHardware` in `Config\settings.json`; shortcuts in `Config\custom_shortcuts.json`.

## 5. Requirements
- **Admin:** Recommended/required (`requireAdministrator` manifest) for full WMI/Defender/BitLocker/TPM queries.
- **Windows Version:** Windows 10 (1809+) or Windows 11.
- **External Dependencies:** `Config\Data\pci.ids.min` and `microarch.json` for platform diagnostics.

## 6. Risks & Side Effects
- **Read-only telemetry** on load (non-destructive).
- Clicking a recommendation quick-fix (e.g., `ApplyPlatformQuickFix`) modifies specific Registry keys with automatic rollback snapshots recorded by `PlatformDiagnosticService`.

## 7. Logs & Error Handling
- Logs `[INFO] Hardware updated...`, `[SHORTCUTS]`, and `[CLIPBOARD]` entries to `MainWindow.Log`.
- WMI timeouts fall back to `"Unknown"` via `ApplyHardwareDetectionUnknownFallback()`.

## 8. Related Source Files
- `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`
- `src/SystemInfoHelper.cs`
- `src/Platform/PlatformDiagnosticService.cs`
- `src/FluentIcons.cs`, `src/OptiDialog.cs`
