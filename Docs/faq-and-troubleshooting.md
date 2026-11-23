# FAQ & Troubleshooting (`v1.2.S7`)

This guide documents edge cases, defensive guards, and error-handling behavior observed in the OptiAssist codebase.

---

### 1. Hardware Monitor (`PCAssist`) fails to load `LibreHardwareMonitorLib` on physical PCs
- **Symptom:** `FileNotFoundException` for `LibreHardwareMonitorLib` or one of its dependencies when moving binaries across machines.
- **Root Cause & Built-in Fix:**
  - `PCAssist.dll` is loaded via `Assembly.Load(byte[])`, while `LoadFrom` on sibling DLLs can fail due to mismatched load contexts or Windows Mark-of-the-Web (`Zone.Identifier`) blocks on downloaded files.
  - `AddonHelper.PreloadSiblingDependencies` and `PCAssistAddon`'s static constructor preload all 9 dependency DLLs (`System.Runtime.CompilerServices.Unsafe`, `System.Buffers`, `System.Numerics.Vectors`, `System.Memory`, `BlackSharp.Core`, `HidSharp`, `DiskInfoToolkit`, `RAMSPDToolkit-NDD`, `LibreHardwareMonitorLib`) via `File.ReadAllBytes` + `Assembly.Load(byte[])` into the exact same load context.
  - Ensure all 9 DLLs remain co-located directly inside `Config\Addons\PCAssist\`.

### 2. Sensors tab shows limited temperatures or missing motherboard/CPU readings
- **Cause:** `LibreHardwareMonitorLib` requires the optional **PawnIO** kernel driver for low-level MSR/SMBus/Super-I/O register access.
- **Solution:** In **Hardware Monitor > Sensors**, click the **Install PawnIO Driver** button to download and run the official installer from `github.com/namazso/PawnIO.Setup`.

### 3. Virtual machine crashes (`StackOverflowException`) when opening Hardware Monitor
- **Cause:** `DiskInfoToolkit` inside `LibreHardwareMonitorLib 0.9.6` can stack-overflow when probing virtualized SCSI controllers (QEMU/VirtIO/VMware).
- **Built-in Guard:** `LibreHardwareEngine.HasVirtualizedStorageDisk()` checks `Win32_DiskDrive` / `Win32_ComputerSystem` and disables `Computer.IsStorageEnabled` when virtualized disks are detected, keeping CPU/RAM/GPU/Network sensors safe.

### 4. Windows Terminal (`wt.exe`) opens multiple broken tabs
- **Cause:** `wt.exe` treats semicolons (`;`) on its command line as new-tab/split commands.
- **Built-in Fix:** `MainWindow.OpenExternalLogWindow()` writes a dedicated `%TEMP%\OptiAssist_Terminal_<pid>.ps1` script and launches `wt.exe` with `-File "<scriptPath>"`, avoiding semicolons on the command line.

### 5. `gpedit.msc` is unavailable on Windows Home / Single Language
- **Behavior:** `ShortcutsHelper.OpenGpedit()` checks for `%SystemRoot%\System32\gpedit.msc` (using `sysnative` if running under WOW64) and shows an informational `OptiDialog` explaining that Local Group Policy Editor is exclusive to Pro, Enterprise, and Education editions.

### 6. System Cleaner skips locked files or refuses a target path
- **Behavior:**
  - `PathSanitizer` blocks critical system paths (`\Windows\System32`, `\Windows\SysWOW64`, `\Windows\WinSxS`, Visual C++ runtime / WebView2 folders).
  - Locked files in use by running processes are safely skipped and reported in the cleanup log (`locked/skipped`). Use the optional backup checkbox when prompted before **Run Cleaner**.

### 7. Hardware detection stalls on Dashboard startup
- **Built-in Guard:** `MainWindow.RefreshHardwareAsync()` attaches a 30-second `DispatcherTimer` timeout (`_hardwareDetectTimeoutTimer`). If WMI hangs on a malfunctioning device, `ApplyHardwareDetectionUnknownFallback()` populates `"Unknown"` so the UI remains responsive.
