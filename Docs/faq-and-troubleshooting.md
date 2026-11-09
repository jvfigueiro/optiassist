# Frequently Asked Questions and Troubleshooting Guide

**Document:** `docs/faq-and-troubleshooting.md`  
**Platform:** Windows 10 / 11 (64-bit)  

---

## 1. Frequently Asked Questions (FAQ)

### Q1: Why does OptiAssist require Administrator privileges to launch?
**A:** OptiAssist performs low-level operating system inspections and maintenance tasks that are restricted by Windows User Account Control (UAC). These include:
- WMI hardware and security queries (`Win32_Tpm`, `Win32_EncryptableVolume`, `SoftwareLicensingProduct`).
- System-wide autostart inspection across machine-level registry hives (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` and `StartupApproved`).
- Purging system-wide temporary files (`C:\Windows\Temp`, Windows Update download caches, and Prefetch).
- Accessing the Windows Task Scheduler COM service (`Schedule.Service`).

### Q2: Can OptiAssist run without any internet connection?
**A:** **Yes.** OptiAssist is engineered around an **Offline-First Zero-Dependencies** philosophy:
- All core views, telemetry collectors, and add-on modules operate 100% offline.
- The App Manager installs applications from local `.exe` and `.msi` installers stored in the `Installers/` folder.
- Internet connectivity is only required if you choose to download packages via Microsoft Winget or run remote community scripts (e.g., Chris Titus WinUtil).

### Q3: How do I deploy OptiAssist to a technician USB flash drive?
**A:** You have two options:
1. **Direct Copy**: Extract the `OptiAssist-v1.1.0.0.7z` archive directly onto your flash drive and run `OptiAssist.exe`. Configuration files and plugins are stored locally within the `Config/` directory.
2. **Built-in Installer**: In OptiAssist, go to **Settings > Install OptiAssist**, select **Portable Installation (USB Flash Drive)**, choose your USB drive letter, and check **Create USB AutoRun (autorun.inf)**.

### Q4: Can I develop my own custom modules?
**A:** **Yes.** OptiAssist uses a decoupled Microkernel architecture. You can create a Class Library in C# targeting .NET Framework 4.8 that implements `IOptiAssistAddon` and drop the compiled `.dll` into `Config/Addons/<YourModule>/`. See [`create-module.md`](create-module.md) for a complete tutorial.

---

## 2. Troubleshooting and Real Code Edge Cases

### Issue 1: "Administrator permission was refused or cancelled"
* **Symptom**: When launching OptiAssist, a warning dialog appears stating administrator permission was cancelled, and the application closes.
* **Underlying Cause**: In `src/App.xaml.cs`, if the user is not running as Administrator, the application attempts to elevate via `ProcessStartInfo.Verb = "runas"`. If the user clicks "No" on the Windows UAC dialog, Windows throws a `Win32Exception` with native error code `1223` (`ERROR_CANCELLED`).
* **Resolution**: Re-launch `OptiAssist.exe` and click **Yes** when prompted by Windows User Account Control.

---

### Issue 2: "Reboot to UEFI Firmware is not supported on this machine"
* **Symptom**: Clicking **Reboot to UEFI** in Windows Tools or Dashboard Common Tasks displays an error message stating firmware reboot is unsupported.
* **Underlying Cause**: In `src/ShortcutsHelper.cs`, the reboot action calls:
  ```cmd
  shutdown.exe /r /fw /t 0
  ```
  On older machines installed with Legacy BIOS (MBR partitioning) rather than modern UEFI (GPT partitioning), Windows rejects the `/fw` switch with error code `0x5B3` (`ERROR_NOT_SUPPORTED`).
* **Resolution**: The hardware or partition table does not support software-triggered UEFI entry. Reboot the computer manually and press the motherboard firmware hotkey (e.g. `Del`, `F2`, `F12`, or `Esc`) during boot.

---

### Issue 3: Some temporary files remain after running System Cleaner
* **Symptom**: The System Cleaner finishes, but some files still remain in `%TEMP%` or `C:\Windows\Temp`.
* **Underlying Cause**: In `src/Addons/SystemCleaner/SystemCleanerAddon.cs`, files currently in use by active Windows services or running applications (e.g. active system telemetry, open browsers, background updaters) are locked by the Windows kernel. The cleaner catches `IOException` and `UnauthorizedAccessException` per file to avoid crashing.
* **Resolution**: This is normal and expected Windows behavior. Close all open applications (especially browsers) before running the cleaner. To clean locked system logs, run the cleaner immediately following a clean system reboot.

---

### Issue 4: "Winget is not available on this system"
* **Symptom**: The App Manager displays a notice that Winget is unavailable, or updates scanning fails.
* **Underlying Cause**: In `src/Addons/AppManager/InstallerHelper.cs`, the application queries `winget.exe`. On certain Windows 10 editions (such as LTSC, LTSB, or installations with Microsoft Store removed), Winget is not pre-installed.
* **Resolution**:
  1. Place offline `.exe` or `.msi` installers directly into the local `Installers/` folder. OptiAssist installs local files without needing Winget.
  2. Alternatively, install the *App Installer* package from Microsoft or download the standalone Winget MSIX release from GitHub.

---

### Issue 5: Missing Executable Warning (`⚠️ Not found`) in Startup Manager
* **Symptom**: An entry in the Startup Programs tab displays a red `⚠️ Not found` badge.
* **Underlying Cause**: In `src/Addons/StartupManager/StartupEngine.cs`, the engine tests `File.Exists(targetPath)`. If a program was uninstalled but its uninstaller failed to remove its autostart entry from the registry (`Run` key) or Startup folder, it creates an orphan reference.
* **Resolution**: Click **Disable** or **Edit** to safely disable or clean up the orphan reference.

---

### Issue 6: Scheduled Tasks display an access denied warning
* **Symptom**: Unchecking "Hide native Microsoft / Windows entries" in Scheduled Tasks prompts a confirmation dialog.
* **Underlying Cause**: Windows Task Scheduler includes protected system tasks (under `\Microsoft\Windows\SystemData` and `\Microsoft\Windows\WaaSMedic`) that restrict read access even from local administrator accounts. The engine uses COM late-binding (`Schedule.Service`) and catches COM exceptions to prevent enumeration halts.
* **Resolution**: Acknowledge the warning to inspect tasks. Third-party updaters (Google, Adobe, Brave) can be safely disabled; avoid modifying tasks under root `\Microsoft\Windows\` unless performing specific troubleshooting.

---

### Issue 7: Updating or replacing an Add-on DLL fails with "File in use"
* **Symptom**: Recompiling a custom module DLL results in a build lock error in external IDEs.
* **Underlying Cause**: OptiAssist uses an **Anti-Lock Loading Engine** in `src/AddonHelper.cs`:
  ```csharp
  byte[] rawBytes = File.ReadAllBytes(sourceToLoad);
  Assembly asm = Assembly.Load(rawBytes);
  ```
  The host executable does *not* lock DLLs on disk. However, if another background inspection tool or anti-virus holds an open handle, file replacement may temporarily fail.
* **Resolution**: Ensure no third-party file locker or debugger has an active process lock on the DLL file.
