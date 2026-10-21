# Chapter 04: Windows Administrative Tools

The **Windows Tools** module provides single-click access to the core native management consoles, diagnostics utilities, and local troubleshooting folders.

---

## 🛠️ Administrative Consoles & Native Utilities

| Tool Name | Executable / Command | Purpose |
|---|---|---|
| **Device Manager** | `devmgmt.msc` | Hardware driver diagnostics, driver updates, and device status verification. |
| **User Accounts** | `netplwiz.exe` | Classic user management, automatic login setup, and account groups. |
| **Disk Management** | `diskmgmt.msc` | Drive partitioning, volume resizing, drive letter assignments, and initialization. |
| **Classic Control Panel** | `control.exe` | Full legacy Windows Control Panel containing applets omitted from Modern Settings. |
| **System Information** | `msinfo32.exe` | In-depth hardware and software environment reporting (IRQ, DMA, loaded drivers). |
| **About Windows** | `winver.exe` | Displays operating system edition, licensed user, and build revision. |
| **MSCONFIG Tool** | `msconfig.exe` | Boot configuration, Safe Mode selection, and startup diagnostics. |
| **System Properties** | `sysdm.cpl` | Computer name, domain/workgroup joining, and advanced environment variables. |
| **Network Connections** | `ncpa.cpl` | Classic network adapter properties, IPv4/IPv6 configuration, and DNS settings. |
| **Windows Features** | `optionalfeatures.exe` | Enable or disable native features (Hyper-V, WSL, SMB 1.0, .NET 3.5). |
| **Registry Editor** | `regedit.exe` | Direct Windows Registry viewer and editor. |
| **Task Manager** | `taskmgr.exe` | Process monitoring, performance graphs, startup apps, and service control. |
| **Resource Monitor** | `resmon.exe` | Real-time breakdown of CPU threads, disk I/O per file, and active network connections. |
| **Memory Diagnostic** | `mdsched.exe` | Schedules a reboot memory test to detect faulty RAM modules. |
| **Reliability Monitor** | `perfmon /rel` | Historical timeline chart of software crashes, Windows updates, and hardware errors. |
| **Event Viewer** | `eventvwr.msc` | System, Application, and Security event logs for troubleshooting critical faults. |
| **GPEDIT Tool** | `gpedit.msc` | Group Policy Editor (available natively in Windows Pro, Enterprise, and Education). |

> [!NOTE]
> If a technician attempts to launch `gpedit.msc` on a Windows Home edition machine, OptiAssist gracefully detects the edition limitation and presents a clear informational dialog rather than a cryptic crash.

---

## 📂 Maintenance Tools Folder (Portable)

Clicking **Maintenance Tools Folder (Portable)** directly opens the local `Maintenance/` directory located on the portable drive. 

Technicians can place their favorite standalone portable utilities here, such as:
* Portable hardware monitors (e.g., CPU-Z, GPU-Z, HWMonitor)
* Drive health tools (e.g., CrystalDiskInfo)
* Antivirus / antimalware offline scanners
* Backup and disk cloning utilities
