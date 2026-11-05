# FAQ and Troubleshooting Guide: OptiAssist

This document provides answers to frequently asked questions, explanations of error codes, and workarounds for edge cases derived directly from the **OptiAssist** codebase and Windows platform behaviors.

---

## 1. Permissions, UAC, and Execution

### Q1: Why does OptiAssist always request Administrator privileges at startup?
**Cause**: The application manifest ([`src/app.manifest`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/app.manifest)) explicitly specifies:
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```
OptiAssist performs low-level maintenance operations that require elevated privileges:
- Modifying `HKLM` registry autostart and uninstall keys.
- Managing Windows Scheduled Tasks via `schtasks.exe` and Task Scheduler COM APIs.
- Cleaning locked system temporary files in `C:\Windows\Temp` and `SoftwareDistribution`.
- Querying privileged WMI hardware classes (`root\cimv2`, `Win32_EncryptableVolume`, `Win32_Tpm`).

**Without Elevation**: If UAC is declined or the user account lacks administrative rights, Windows halts initialization with Win32 error `0x2E4` (`ERROR_ELEVATION_REQUIRED`).

---

## 2. Compilation and File-Lock Issues

### Q2: Error `MSB3021 / MSB3027: Cannot copy file... The process cannot access the file because it is being used by another process` during `build.bat`.
**Cause**: 
1. A previously running instance of `OptiAssist.exe` is still active in the background.
2. The post-build target `CleanupLocalArtifacts` in `OptiAssist.csproj` attempts to overwrite plugin DLLs in `Config\Addons\<Module>\` while a debugger holds an active handle.

**Runtime Anti-Lock Protection**:
The module loader ([`AddonHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/AddonHelper.cs)) avoids runtime locking by loading byte arrays:
```csharp
byte[] rawBytes = File.ReadAllBytes(dllPath);
Assembly assembly = Assembly.Load(rawBytes);
```

**Build-Time Fix**:
- Terminate lingering processes:
  ```cmd
  taskkill /f /im OptiAssist.exe
  ```
- Or compile only the main host without re-triggering the addon targets:
  ```cmd
  MSBuild.exe OptiAssist.csproj /t:CoreBuild /p:Configuration=Release
  ```

---

## 3. Windows Native Tools & Consoles

### Q3: Clicking `gpedit.msc` reports that the file was not found.
**Cause**: The Local Group Policy Editor (`gpedit.msc`) is exclusive to **Windows Pro, Enterprise, and Education** editions. **Windows Home** and **Windows Single Language** editions do not include it.

**OptiAssist Behavior**:
The application intercepts the `Win32Exception` when launching `gpedit.msc`, logs an informational message in the console, and prevents any application crash.

**Workaround**:
On Windows Home editions, administrators can install the DISM packages for Group Policy or configure registry equivalents using Toolbox scripts.

---

### Q4: "Reboot to UEFI Firmware" fails or does not restart the computer.
**Cause**: The internal command executed is:
```cmd
shutdown.exe /r /fw /t 0
```
The `/fw` flag requires a native UEFI motherboard.

**Edge Cases**:
1. **Legacy BIOS / MBR / CSM Systems**: Older systems configured without native UEFI reject the parameter with error `0x5B3` (`System firmware interface reboot is not supported`).
2. **Virtual Machines**: Hypervisors configured in legacy BIOS mode will also reject the command.

**OptiAssist Behavior**:
The process exit code is evaluated, and an informational log message is written explaining that the motherboard does not support UEFI software reboot. The technician should use hardware hotkeys (F2, Del) instead.

---

### Q5: USB AutoRun does not automatically launch when plugging in the flash drive.
**Cause**: Starting with Windows 7 and strictly enforced in Windows 10 and 11, Microsoft disabled silent executable AutoRun on removable USB storage media to prevent malware propagation (e.g. Stuxnet, Conficker).

**AutoRun Behavior on Modern Windows**:
The `autorun.inf` file created by OptiAssist still performs key functions:
1. Displays the custom **OptiAssist icon** on the USB drive in Windows Explorer.
2. Applies the **custom drive label** (`OptiAssist - Maintenance & Post-Installation`).
3. Adds "Open OptiAssist" to the right-click Explorer context menu.

---

## 4. App Manager and Package Management

### Q6: App Manager reports that Winget is unavailable.
**Cause**: While `winget.exe` (Windows Package Manager) is included in Windows 11 and up-to-date Windows 10 installations, it may be absent in:
- Windows 10 LTSC / LTSB enterprise installations (which lack the Microsoft Store).
- Windows 10 builds older than version 1809.
- Systems where the `Microsoft.DesktopAppInstaller` package is missing or corrupted.

**OptiAssist Behavior**:
1. When checking for upgrades or installing packages, OptiAssist handles `Win32Exception` if `winget.exe` is absent from `PATH`.
2. If `EnableWingetFallback` is enabled in `settings.json`, OptiAssist prioritizes local installers in `Installers/`.
3. If no local installer exists and Winget is missing, an actionable warning directs the technician to update the Microsoft Store App Installer.

---

## 5. System Cleaner Edge Cases

### Q7: System Cleaner reports errors or skips files in `%TEMP%`.
**Cause**: Temporary files currently locked by running web browsers, background indexing services, or active antivirus scanners throw `IOException` or `UnauthorizedAccessException` when deletion is attempted.

**Code Handling**:
The `SystemCleaner` processes each file inside isolated `try/catch` blocks. If a file is locked, it is skipped safely while continuing with remaining files, calculating total freed bytes without interrupting the cleanup routine.

---

## 6. Toolbox and Script Execution

### Q8: Why does the Toolbox execute certain PowerShell commands using `-EncodedCommand` (Base64)?
**Cause**: Commands containing nested quotes, pipeline symbols, or special characters can be mangled by conventional command-line parsing.

**Architectural Benefit**:
Encoding commands as UTF-16LE Base64 strings provides:
1. **Immunity from quote escaping issues**.
2. **Faster execution** without loading user profiles (`-NoProfile`).
3. **Execution Policy Bypass** (`-ExecutionPolicy Bypass`) without altering global system policies permanently.

---

### Q9: Long-running Toolbox commands appear unresponsive.
**Cause**: Deep diagnostic operations (`sfc /scannow`, `dism /online /cleanup-image /restorehealth`) can take 10 to 20 minutes depending on disk speed and corruption volume.

**Monitoring**:
OptiAssist streams process standard output and standard error directly to the **Logs** tab in real time. Open the floating **Log Window** to observe line-by-line terminal output.
