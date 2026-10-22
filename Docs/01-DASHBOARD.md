# Chapter 01: Dashboard & Hardware Inspection

The **Dashboard** is the default landing screen of OptiAssist. It provides an instantaneous, comprehensive overview of the machine's hardware architecture, operating system configuration, and firmware security status.

---

## 🖥️ Top Header Summary

The top right corner of the application window displays real-time hardware telemetry:
* **Motherboard Model**: Manufacturer and model board designation (e.g., `ASUSTeK COMPUTER INC. PRIME B550M-A`).
* **Processor (CPU)**: Processor model and clocked frequency (e.g., `AMD Ryzen 5 5600X 6-Core Processor`).
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
| **Serial Number** | `Win32_BIOS` / `Win32_ComputerSystemProduct` | Hardware Serial Number / Service Tag. Includes a one-click **Copy** button. |

> [!TIP]
> Clicking the **📋 Copy** button beside the serial number instantly copies the motherboard/machine serial directly to the clipboard, streamlining warranty lookups and support ticket logging.

---

## 🖥️ Operating System Card

* **Operating System**: Windows product name, edition, and architecture (e.g., `Windows 11 Pro 64-bit`).
* **Version & Build**: Windows feature version and internal build number (e.g., `Version 23H2 (Build 22631.3007)`).
* **Device Name**: Active NetBIOS / hostname of the workstation.

---

## 🔒 Security Status Badges (Status Bar)

At the bottom of the dashboard, OptiAssist continuously checks and displays the machine's security posture (each item can be clicked for direct navigation):

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

## ⚡ Common Tasks & Quick Actions

The bottom section of the Dashboard features direct shortcuts to the most frequent operations arranged in a balanced 3x3 grid:
* **🚀 Run Post-Install**: Switches directly to the **App Manager** view and immediately launches the automated installation of all checked/pre-selected software applications.
* **📦 App Manager**: Quick jump to batch application and installer management.
* **📁 Maintenance Folder**: Opens the portable `Maintenance/` folder containing standalone tools.
* **🚀 Chris Titus WinUtil**: Launches the community PowerShell optimization script.
* **🧠 Memory Diagnostic**: Schedules Windows Memory Diagnostic (`mdsched.exe`).
* **🖥️ Device Manager**: Opens native device console (`devmgmt.msc`).
* **⚙️ System Properties**: Opens system properties dialog (`sysdm.cpl`).
* **⚡ Reboot to UEFI**: Reboots the machine directly into the motherboard BIOS/UEFI firmware without needing to mash `DEL` or `F2` on restart (via `shutdown.exe /r /fw /t 0`).
