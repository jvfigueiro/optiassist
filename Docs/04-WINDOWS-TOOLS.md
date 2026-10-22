# Chapter 04: Windows Administrative Tools

The **Windows Tools** module provides single-click, elevated access to core native management consoles, diagnostics utilities, and portable troubleshooting folders. It eliminates the need to remember arcane `.msc` or `.cpl` execution strings during on-site support.

---

## 🛠️ Administrative Consoles & Native Utilities

All native utilities are launched with inherited 64-bit elevated privileges:

| Tool Name | Executable / Command | Purpose |
|---|---|---|
| **Device Manager** | `devmgmt.msc` | Hardware driver diagnostics, driver updates, and hardware failure identification. |
| **User Accounts** | `netplwiz.exe` | Classic user management, automatic login setup, and account privilege configuration. |
| **Disk Management** | `diskmgmt.msc` | Drive partitioning, volume resizing, drive letter assignments, and disk initialization. |
| **Classic Control Panel** | `control.exe` | Legacy Windows Control Panel containing system applets omitted from the Modern Settings app. |
| **System Information** | `msinfo32.exe` | In-depth hardware and software environment reporting (IRQs, DMA, loaded drivers, Secure Boot state). |
| **About Windows** | `winver.exe` | Displays Windows edition, licensed owner, and exact build revision number. |
| **MSCONFIG Tool** | `msconfig.exe` | Boot configuration, Safe Mode selection, and startup diagnostics. |
| **System Properties** | `sysdm.cpl` | Computer name, domain/workgroup joining, and advanced environment variables. |
| **Network Connections** | `ncpa.cpl` | Classic network adapter properties, IPv4/IPv6 static assignment, and DNS server configuration. |
| **Windows Features** | `optionalfeatures.exe` | Enable or disable native features (Hyper-V, WSL, SMB 1.0, .NET 3.5). |
| **Registry Editor** | `regedit.exe` | Direct Windows Registry viewer and editor with elevated access. |
| **Task Manager** | `taskmgr.exe` | Process monitoring, performance graphs, startup apps, and service control. |
| **Resource Monitor** | `resmon.exe` | Real-time breakdown of CPU threads, disk I/O per file, and active network connections. |
| **Memory Diagnostic** | `mdsched.exe` | Schedules a reboot memory test to detect faulty RAM modules. |
| **Reliability Monitor** | `perfmon /rel` | Historical timeline chart of software crashes, Windows updates, and hardware errors. |
| **Event Viewer** | `eventvwr.msc` | System, Application, and Security event logs for troubleshooting critical faults. |
| **GPEDIT Tool** | `gpedit.msc` | Group Policy Editor (available natively in Windows Pro, Enterprise, and Education). |

> [!NOTE]
> If a technician attempts to launch `gpedit.msc` on a Windows Home edition machine, OptiAssist gracefully detects the edition limitation and presents a localized informational prompt rather than throwing a missing file exception.

---

## 📂 Portable Maintenance Tools Folder

Clicking **`📁 Maintenance Folder`** opens the portable `Maintenance/` directory located on the root of your portable drive or USB stick.

Technicians can place their favorite standalone portable utilities here, such as:
* **Drive Health Diagnostics**: CrystalDiskInfo, Victoria SSD/HDD, SeaTools.
* **Hardware Sensors & Telemetry**: CPU-Z, GPU-Z, HWMonitor, HWiNFO.
* **Malware Remediation**: Standalone offline malware scanners, AdwCleaner, Kaspersky Virus Removal Tool.
* **Backup & Imaging**: Portable disk cloning and imaging utilities.
