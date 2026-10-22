# Chapter 03: System Optimization & Repairs

The **Optimization** module houses a curated collection of system integrity repair tools, performance enhancers, and deep cleanup routines. Every utility executes asynchronously, streaming standard output and error messages live to the terminal console.

---

## 🚀 1. Community Tools

### Chris Titus Tool (WinUtil)
* **Description**: Launches Chris Titus Tech's renowned open-source Windows Utility.
* **Command Executed**: `powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://christitus.com/win | iex"`
* **Use Cases**: Advanced debloating of pre-installed UWP apps, comprehensive system tweaks, feature disabling, and DNS/network optimizations.

---

## 🛡️ 2. System Integrity & Repairs

### Check and Repair Windows Integrity via SFC
* **Description**: System File Checker scans all protected operating system files and replaces corrupt or damaged copies with cached good versions.
* **Command Executed**: `sfc.exe /scannow`
* **Use Cases**: Random system crashes, missing DLL error dialogs, or following malware remediation.

### Check and Repair Windows Integrity via DISM
* **Description**: Deployment Image Servicing and Management repairs the underlying Windows Component Store (`WinSxS`) by fetching healthy payload packages from Windows Update servers.
* **Command Executed**: `DISM.exe /Online /Cleanup-Image /RestoreHealth`
* **Use Cases**: When `sfc /scannow` reports corruptions that it cannot fix on its own, or as standard maintenance before major updates.

### Check Disk Integrity (CHKDSK C:)
* **Description**: Performs a non-invasive read-only check of the primary volume (`C:`) to detect logical file system errors or bad sector indicators.
* **Command Executed**: `chkdsk.exe C:`
* **Use Cases**: Slow disk reads/writes, sudden shutdowns, or blue screen events pointing to storage drivers.

### Fix Network Printer Issue (RPC 0x11b / 0x709)
* **Description**: Resolves network printer connection errors `0x0000011b` and `0x00000709` caused by Microsoft security patches altering RPC authentication levels.
* **Internal Mechanism**:
  1. Sets DWORD `RpcAuthnLevelPrivacyEnabled = 0` in `HKLM\SYSTEM\CurrentControlSet\Control\Print`.
  2. Restarts the Windows Print Spooler service (`Spooler`).
* **Use Cases**: When a computer cannot connect to or print from a shared network printer on a local Windows peer-to-peer network.

### Fix System Time for Dual Boot (UTC / Linux Clock)
* **Description**: Configures Windows to treat the hardware Real-Time Clock (RTC / BIOS) as Universal Coordinated Time (UTC) instead of Local Time.
* **Internal Mechanism**: Sets DWORD `RealTimeIsUniversal = 1` in `HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation`.
* **Use Cases**: Machines set up in dual-boot with Linux distributions, preventing the clock from skipping or showing incorrect hours when switching between operating systems.

### Install or Repair Microsoft Store
* **Description**: Reinitializes, resets the cache, and triggers reinstallation of the Microsoft Store infrastructure.
* **Command Executed**: `wsreset.exe -i`
* **Use Cases**: When the Microsoft Store fails to open, crashes on startup, or refuses to download/install apps.

### Network Reset
* **Description**: Flushes the local DNS cache and completely rebuilds the network stack and Winsock catalog.
* **Commands Executed**:
  1. `ipconfig.exe /flushdns` (flushes DNS resolver cache).
  2. `netsh.exe winsock reset` (restores Winsock catalog).
  3. `netsh.exe int ip reset` (rebuilds TCP/IP stack).
* **Use Cases**: "No Internet Connection" errors with valid IP, DNS resolving failures, or proxy hijackings caused by adware.

### WinSAT Benchmark
* **Description**: Evaluates and computes Windows Experience Index (WinSPR) hardware performance ratings.
* **Command Executed**: Queries existing scores; if re-run is requested, executes `winsat.exe formal -restart clean`.
* **Metrics Reported**: Overall Score, Processor (CPU), RAM Memory, Direct3D 9/10/11 Graphics, Desktop UI Graphics, and Primary Disk throughput.

---

## 🧹 3. Cleanup & Performance

### Clean Temporary Files
* **Description**: Deep purge of junk files and cache across user profiles and system volumes.
* **Targets Cleared**:
  1. User temporary folder (`%TEMP%`).
  2. Windows temporary folder (`C:\Windows\Temp`).
  3. Prefetch directory (`C:\Windows\Prefetch`).
  4. Windows Error Reporting queue (`WER\ReportQueue`).
  5. Empties the Recycle Bin on all mounted partitions using the native Win32 API (`SHEmptyRecycleBin`).
* **Use Cases**: Routine preventive cleanup, reclaiming multiple gigabytes of drive space, and speeding up system responsiveness.

### Clean Windows Update Cache
* **Description**: Clears downloaded installation packages and update remnants that linger after patching.
* **Internal Mechanism**:
  1. Stops the Windows Update service (`wuauserv`).
  2. Deletes cached files in `C:\Windows\SoftwareDistribution\Download`.
  3. Restarts `wuauserv`.
* **Use Cases**: Resolving Windows Update stuck in downloading loops, installation error codes, and reclaiming gigabytes of disk space.

### Optimize Visual Effects
* **Description**: Configures visual effects for maximum performance and eliminates unnecessary desktop animation delays.
* **Internal Mechanism**:
  1. Sets DWORD `VisualFXSetting = 2` (Best Performance) in `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects`.
  2. Disables window minimize/maximize animations (`MinAnimate = 0` in `HKCU\Control Panel\Desktop\WindowMetrics`).
  3. Reduces menu show delay from 400ms down to 50ms (`MenuShowDelay = 50` in `HKCU\Control Panel\Desktop`).
  4. Broadcasts `WM_SETTINGCHANGE` to refresh the desktop shell immediately.
* **Use Cases**: Entry-level laptops, virtual machines, or giving maximum responsiveness to any workstation.

### Disable Telemetry and Optimize System Services
* **Description**: Stops background diagnostic collectors and telemetry tracking to reduce unneeded CPU, RAM, and disk activity.
* **Internal Mechanism**:
  1. Disables service `DiagTrack` (*Connected User Experiences and Telemetry*).
  2. Disables service `dmwappushservice` (*WAP Push Message Routing Service*).
  3. Disables service `WerSvc` (*Windows Error Reporting Service*).
  4. Sets DWORD `AllowTelemetry = 0` in `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection`.
* **Use Cases**: Enhancing user privacy and stopping unnecessary background disk thrashing on standard HDDs and SSDs.

### Compact Windows OS (Compact OS)
* **Description**: Applies intelligent, hardware-accelerated filesystem compression to Windows system binaries and DLLs.
* **Command Executed**: `compact.exe /CompactOS:always`
* **Space Saved**: Typically frees between 1.5 GB and 3.0 GB of storage on drive `C:` without any observable impact on execution speed.
* **Use Cases**: Machines with smaller primary storage (eMMC 32GB/64GB or SSDs of 120GB/240GB).

### Open Scripts Folder
* **Description**: Opens the local `Scripts/` directory in Windows Explorer.
* **Supported Script Formats**:
  * `.ps1` (PowerShell scripts run with `-ExecutionPolicy Bypass`).
  * `.bat` / `.cmd` (Command Prompt batch scripts).
  * `.reg` (Registry import files applied silently via `regedit.exe /s`).
* **Use Cases**: Running custom proprietary batch scripts or applying custom registry fixes on client machines.
