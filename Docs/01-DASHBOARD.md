# Chapter 01: Dashboard & Hardware Inspection

The **Dashboard** is the default landing screen of OptiAssist. It provides an instantaneous, comprehensive overview of the machine's hardware architecture, operating system configuration, and firmware security posture, along with a balanced 3x3 grid of essential everyday tasks.

---

## 🖥️ Top Header Summary

The top right corner of the application window displays real-time hardware telemetry persistent across view switches:
* **Motherboard Model**: Manufacturer and model board designation (e.g., `ASUSTeK COMPUTER INC. PRIME B550M-A`).
* **Processor (CPU)**: Processor model and frequency (e.g., `AMD Ryzen 5 5600X 6-Core Processor`).
* **Total RAM**: Combined physical memory installed across all slots (e.g., `32.0 GB`).

---

## 🧠 Hardware Information Card

OptiAssist queries the Windows Management Instrumentation (WMI) subsystem asynchronously upon launch to prevent any UI freezing:

| Field | WMI Class / Source | Description |
|---|---|---|
| **Processor** | `Win32_Processor` | CPU Name, physical core count, and logical thread count. |
| **RAM Memory** | `Win32_PhysicalMemory` | Total capacity in gigabytes and detected memory modules/slots. |
| **Graphics Card** | `Win32_VideoController` | Primary and secondary display adapter models (e.g., `NVIDIA GeForce RTX 3070`). |
| **Motherboard** | `Win32_BaseBoard` | Motherboard manufacturer and board model name. |
| **BIOS Firmware** | `Win32_BIOS` | BIOS vendor, version string, and release date. |
| **Primary Storage** | `Win32_LogicalDisk` | System drive letter (`C:`), free space, total size, and filesystem (`NTFS`). |
| **Serial Number** | `Win32_BIOS` / `Win32_ComputerSystemProduct` | Hardware Serial Number / Service Tag. Includes a one-click **`📋 Copy`** button. |

> [!TIP]
> Clicking the **`📋 Copy`** button beside the serial number instantly copies the motherboard/machine serial tag directly to the Windows clipboard, streamlining warranty lookups, vendor support portal inquiries, and maintenance ticket logging.

---

## 🖥️ Operating System Card

* **Operating System**: Windows product name, edition, and architecture (e.g., `Windows 11 Pro 64-bit`).
* **Version & Build**: Windows feature version and internal build number (e.g., `Version 23H2 (Build 22631.3007)`).
* **Device Name**: Active NetBIOS / hostname of the workstation.

---

## 🔒 Security Status Badges (Persistent Status Bar)

At the bottom of the window, OptiAssist continuously checks and displays the machine's security posture. Each badge features dynamic color indication (Green for active/available, Red for inactive/disabled, Gray for unsupported) and interactive click actions:

1. **BitLocker Drive Encryption** *(Clickable)*:
   * Inspects `root\CIMV2\Security\MicrosoftVolumeEncryption` -> `Win32_EncryptableVolume`.
   * States: `Active (Encrypted)`, `Inactive (Decrypted)`, or `Unavailable`.
   * **Click action**: Opens Windows Device Encryption Settings (`ms-settings:deviceencryption`) or BitLocker Control Panel (`control /name Microsoft.BitLockerDriveEncryption`).
2. **Trusted Platform Module (TPM)** *(Clickable)*:
   * Inspects `root\CIMV2\Security\MicrosoftTpm` -> `Win32_Tpm`.
   * States: `Available (v2.0)`, `Available (v1.2)`, or `Not supported / Disabled`.
   * **Click action**: Opens native TPM Management Console (`tpm.msc`).
3. **Secure Boot**:
   * Inspects the registry value `UEFISecureBootEnabled` at `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State`.
   * States: `Active` or `Inactive`.

---

## ⚡ Common Tasks (3x3 Grid)

The bottom section of the Dashboard features direct shortcuts to the 9 most frequent technician operations arranged in a balanced 3x3 grid:

1. **🚀 Run Post-Install**: Switches directly to the **App Manager** view and immediately launches the automated silent installation of all checked/pre-selected software applications.
2. **📦 App Manager**: Opens application and installer management to review, select, or customize packages.
3. **📁 Maintenance Folder**: Opens the portable `Maintenance/` folder containing standalone tools (e.g., CPU-Z, CrystalDiskInfo).
4. **🚀 Chris Titus WinUtil**: Launches the community PowerShell optimization script (`irm https://christitus.com/win | iex`).
5. **🧠 Memory Diagnostic**: Schedules Windows Memory Diagnostic (`mdsched.exe`) for next boot.
6. **🖥️ Device Manager**: Opens the native device console (`devmgmt.msc`) to inspect driver states.
7. **⚙️ System Properties**: Opens legacy system properties dialog (`sysdm.cpl`).
8. **🧰 Toolbox**: Jumps directly to the **Toolbox** command runner and system repair presets hub.
9. **⚡ Reboot to UEFI**: Reboots the machine directly into the motherboard BIOS/UEFI firmware without needing to mash `DEL` or `F2` on restart (via `shutdown.exe /r /fw /t 0`).
