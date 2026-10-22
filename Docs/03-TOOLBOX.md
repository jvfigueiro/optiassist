# Chapter 03: Toolbox & System Optimization Hub

The **Toolbox** module (`Config/Addons/Toolbox/Toolbox.dll`) serves as OptiAssist's extensible command runner and unified system optimization hub. It replaces legacy static optimization buttons with a dynamic, categorized execution engine capable of running custom batch files, PowerShell scripts, Command Prompt strings, and standalone binary executables.

All previous optimization routines and tweaks (SFC, DISM, Chkdsk, Network Printer RPC fix, Dual Boot RTC clock, CompactOS, WinSAT, etc.) are pre-configured as native presets inside Toolbox, allowing technicians to use, modify, or expand them as needed.

---

## 🏗️ Architecture & Data Model

Toolbox follows a decoupled layered architecture separating persistent data storage, process execution, and WPF presentation:

```text
┌────────────────────────────────────────────────────────┐
│                      ToolboxAddon                      │
│        (WPF View, Accordion Cards & Options Drawer)     │
└───────────────┬────────────────────────┬───────────────┘
                │                        │
                ▼                        ▼
    ┌──────────────────────┐  ┌──────────────────────┐
    │ ToolboxStorageManager│  │ ToolboxProcessRunner │
    │   (JSON Persistence) │  │  (Async Worker Thread│
    │   Config/Addons/     │  │   stdout/stderr      │
    │   Toolbox/           │  │   Process Tree Kill) │
    │   toolbox.json       │  └──────────┬───────────┘
    └──────────────────────┘             │
                                         ▼
                               ┌───────────────────┐
                               │ Central Logs &    │
                               │ External LogWindow│
                               └───────────────────┘
```

### 1. Data Schema (`ToolboxModels.cs`)
Each command item is represented by `ToolboxCommandItem`:

| Field | Type | Description |
|---|---|---|
| `Id` | String | Unique GUID or alphanumeric identifier. |
| `Title` | String | User-facing display title (e.g., `"Verificar integridade do disco C:"`). |
| `Description` | String | Optional subtitle or contextual explanation. |
| `Command` | String | Command line string or script to execute (e.g., `"chkdsk.exe C:"`). |
| `Category` | String | Category grouping string (e.g., `"System Integrity"`, `"Network"`, `"Performance"`). |
| `Shell` | String | Execution shell environment: `"cmd"`, `"powershell"`, or `"binary"`. |
| `WaitForExit` | Boolean | Whether execution waits synchronously for termination or runs in the background. |
| `CaptureOutput` | Boolean | Whether `stdout` and `stderr` streams are captured and routed to the logs. |
| `OpenExternalLogs` | Boolean | Whether to automatically pop out the external Logs window upon executing this action. |
| `TimeoutSeconds` | Integer | Maximum execution time in seconds (0 = unlimited). |

### 2. Process Execution Engine (`ToolboxProcessRunner.cs`)
- **Detached Background Threads**: Commands run on asynchronous worker threads via `Task.Run()`, ensuring the primary OptiAssist window remains 100% fluid and responsive.
- **Real-Time Streaming**: Standard output (`stdout`) and standard error (`stderr`) are captured line-by-line using asynchronous read events and piped directly to the central Logs console and the pop-out `LogWindow`.
- **Process Tree Termination**: If a user cancels a running task, `ToolboxProcessRunner` halts the entire child process hierarchy safely using `taskkill /T /F`.
- **Exit-Code & Error Detection**:
  - Exit code `0`: Reported as a clean success.
  - Non-zero exit code: Reported with the exact code number and highlighted in the logs.
  - UAC Elevation Cancelled (`Win32Exception` 1223): Handled gracefully without throwing an unhandled exception.
  - Missing Executable (`Win32Exception` 2/3 or exit code 9009): Clearly alerts the user that the binary or script was not found.

---

## 🎨 User Interface & Workflow

### 1. Clean, Native Card Design
To blend seamlessly into the application, action cards keep raw commands and implementation details hidden by default. Each card presents:
* Title and descriptive subtitle.
* Action buttons:
  - **`▶ Run`**: Instantly executes the command and automatically triggers the pop-out **LogWindow** so technicians can watch real-time console progress.
  - **`✏️ Edit`**: Opens the editing modal (visible when Edit Mode is active).

### 2. Collapsible Category Accordions
Commands are automatically grouped by their `Category` field into styled collapsible cards (`Expander`). Users can expand or collapse categories to keep their workspace organized.

### 3. Options Popover Drawer
Clicking the **`⚙️ Options`** button in the header opens an actions menu:
* **`➕ New Toolbox`**: Opens a form to create a new custom command with mandatory field validation.
* **`🔄 Restore Presets`**: Restores factory-default system repair commands and categories.
* **`📂 Open toolbox.json`**: Opens the raw JSON database in the system text editor for power users.
* **`✏️ Enable Edit Mode` / `Disable Edit Mode`**: Toggles visibility of the **Edit** button on action cards. When disabled, the interface is locked down to prevent accidental edits.
* **`🗂️ Manage Categories`**: Opens the Category Manager dialog.

### 4. Category Manager
Technicians can manage categories without touching raw JSON files:
* **Add Category**: Enter a new category name.
* **Rename Category**: Renames an existing category and automatically cascades the update across all member commands.
* **Delete Category**: Removes an empty category or prompts for confirmation before reassigning or deleting items.

---

## 🛠️ Built-in System Optimization & Repair Presets

OptiAssist comes pre-loaded with over 16 verified maintenance, repair, and performance presets:

### 🛡️ Category: System Integrity & Repairs
1. **Check & Repair Windows Integrity (SFC)**:
   - Command: `sfc.exe /scannow` (Shell: `cmd`)
   - Replaces corrupted, altered, or missing system DLLs with healthy cached copies.
2. **Check & Repair Windows Component Store (DISM)**:
   - Command: `DISM.exe /Online /Cleanup-Image /RestoreHealth` (Shell: `cmd`)
   - Repairs the Windows Component Store (`WinSxS`) by downloading payload packages from Windows Update.
3. **Check Disk Integrity (CHKDSK C:)**:
   - Command: `chkdsk.exe C:` (Shell: `cmd`)
   - Performs a non-invasive read-only check of drive `C:` to detect file system inconsistencies and bad sectors.
4. **Repair Microsoft Store Infrastructure**:
   - Command: `wsreset.exe -i` (Shell: `cmd`)
   - Resets store cache and reinitializes package registrations when the Store crashes or fails to install apps.

### 🌐 Category: Network & Printing
5. **Full Network Stack Reset**:
   - Command: `ipconfig.exe /flushdns && netsh.exe winsock reset && netsh.exe int ip reset` (Shell: `cmd`)
   - Flushes DNS cache, resets Winsock catalog, and rebuilds the TCP/IP stack to resolve connectivity dropouts.
6. **Fix Network Printer RPC Issue (0x11b / 0x709)**:
   - Mechanism: Sets DWORD `RpcAuthnLevelPrivacyEnabled = 0` in `HKLM\SYSTEM\CurrentControlSet\Control\Print` and restarts the `Spooler` service.
   - Resolves network printer connection errors across Windows peer networks caused by security patches.
7. **Inspect Listening TCP Ports**:
   - Command: `Get-NetTCPConnection -State Listen | Select-Object LocalAddress,LocalPort,OwningProcess | Format-Table -AutoSize` (Shell: `powershell`)
   - Displays all active network listening sockets and owning process IDs.

### ⚡ Category: Windows Performance & Tuning
8. **Fix System Time for Dual Boot (UTC / Linux Clock)**:
   - Mechanism: Sets DWORD `RealTimeIsUniversal = 1` in `HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation`.
   - Prevents Windows and Linux from desynchronizing the BIOS hardware clock.
9. **Compact Windows OS (Compact OS)**:
   - Command: `compact.exe /CompactOS:always` (Shell: `cmd`)
   - Compresses Windows system binaries using hardware-accelerated filesystem compression, reclaiming 1.5 to 3.0 GB on drive `C:`.
10. **Query Compact OS State**:
    - Command: `compact.exe /CompactOS:query` (Shell: `cmd`)
    - Reports whether filesystem compression is active on the current volume.
11. **WinSAT Hardware Benchmark**:
    - Command: `winsat.exe formal -restart clean` (Shell: `cmd`)
    - Benchmarks CPU, RAM, Disk, and Direct3D performance ratings.
12. **Optimize Visual Effects for Performance**:
    - Mechanism: Sets `VisualFXSetting = 2`, disables window minimize animations (`MinAnimate = 0`), and sets `MenuShowDelay = 50`.
13. **Disable Telemetry & Diagnostic Services**:
    - Mechanism: Disables `DiagTrack`, `dmwappushservice`, and `WerSvc` background telemetry services.
14. **List Active Power Schemes**:
    - Command: `powercfg.exe /list` (Shell: `cmd`)
    - Displays installed and active Windows power management profiles.

### 📁 Category: Scripts & Tools Folder
15. **Open Custom Scripts Folder**:
    - Command: Opens `Config/Addons/Toolbox/Scripts/` containing `tweaks_interface.ps1`, `fix_printer_rpc.reg`, and `reset_network.bat`.
16. **Launch Chris Titus WinUtil**:
    - Command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "irm https://christitus.com/win | iex"` (Shell: `powershell`).

