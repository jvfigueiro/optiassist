# Windows Tools — Admin Consoles, OptiBoost & Compatibility Checker (`v1.2.S7`)

## 1. Overview & Problem Solved
Windows administrative consoles are scattered across `System32`, Control Panel, and Settings, while manual Windows optimization ("debloating") is error-prone without hardware awareness or rollback. **Windows Tools** unifies native consoles, the 7-profile **OptiBoost** engine, and **Compatibility Checker** in one tabbed workspace.

## 2. How to Use (Step-by-Step)
1. Select **Windows Tools** in the sidebar.
2. **Windows Administrative Tools tab:** click any of the 23 native tools (`Device Manager`, `Disk Management`, `MSCONFIG`, `Services`, `Event Viewer`, `GPEDIT`, etc.) or **Maintenance Tools** (Portable Mode).
3. **OptiBoost tab:**
   - Review the **Auto-Detected Profile** strip (`Gaming PC`, `Workstation`, `Hybrid`, `Conventional Desktop`, `Laptop`, `Gaming Laptop`, or `Ultraportable`), or click another profile button.
   - Filter by category or quick search; toggle **Restore Point** if desired (auto-checked for Medium/High risk tweaks).
   - Click **Dry-Run** to preview exact Registry/Service/AppX operations, **Apply Selected** to execute with snapshot backup, **Revert Last Session** (or per-tweak **Revert**) to undo changes, or **Report Summary** to copy a structured summary.
4. **Compatibility Checker tab:** inspect **Hardware Specs** (`CPU`, `GPU`, `RAM`, `Storage Type`, `Firmware` with `Compatible` / `Unknown or Not Tested` / `Not Compatible` badges), **Windows 11 Compatibility** (shown when hardware is Layer B/C), **Detected Hardware Quirks & Known Driver Issues**, or click **Windows Support Lifecycle**.

## 3. How It Works Internally
- `WindowsToolsTabsController` (`src/Debloater/WindowsToolsTabsBuilder.cs`) hosts the `OptiBoost` and `Compatibility Checker` views inside `ContentControl`s.
- `DebloaterCatalogLoader` loads 7 modular JSON catalogs from `Config\Debloater\*.json` (`power`, `appearance_ui`, `services`, `features_capabilities`, `policies_gpo`, `security_best_effort`, `apps_ctt`).
- `DebloaterEngine` gates tweaks by OS build/edition/hardware (`ssd_only`, `no_ir_camera`, `compact_os_eligible`, etc.), snapshots pre-change Registry/Service values under `Config\Debloater\backups\`, and executes changes asynchronously.
- `PlatformDiagnosticService` combines `PlatformProfiler`, `MicroarchDatabase`, `PciIdsDatabase`, `Win11CompatAdvisor`, `ProfileRecommender`, and `KnownIssuesEngine`.

## 4. Inputs, Outputs & Configuration
- **Inputs:** `Config\Debloater\*.json`, `Config\Data\microarch.json`, `pci.ids.min`, `known_issues.json`.
- **Outputs:** Pre-change JSON snapshots and `last_debloat_report.txt` in `Config\Debloater\backups\`; persisted `DebloaterProfileOverride` in `Config\settings.json`.

## 5. Requirements
- **Admin:** Required for HKLM Registry writes, services, power plans, and System Restore Points.
- **Windows Version:** Windows 10 (1809+) or Windows 11.
- **External Dependencies:** None (native .NET / WMI / Registry / `vssadmin` / PowerShell).

## 6. Risks & Side Effects
- Modifies Windows Registry (`HKCU`/`HKLM`), services, and AppX packages.
- High-risk tweaks (e.g., Smart App Control) enforce `doubleConfirm`. Registry/Service tweaks are reversible via **Revert** / **Revert Last Session** snapshots; AppX removals are marked `revert: manual`.

## 7. Logs & Error Handling
- Streams progress to `MainWindow.OpenLogWindow()` / `HostLog`.
- `ShortcutsHelper.OpenGpedit()` safely detects missing `gpedit.msc` on Windows Home editions and displays an informational `OptiDialog`.

## 8. Related Source Files
- `src/Debloater/WindowsToolsTabsBuilder.cs`, `DebloaterCatalog.cs`, `DebloaterEngine.cs`, `SystemRestoreHelper.cs`
- `src/Platform/*.cs`
- `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`, `src/ShortcutsHelper.cs`
