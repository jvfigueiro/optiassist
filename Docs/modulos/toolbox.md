# Toolbox (Command Runner & System Optimization Hub)

**Module Identifier:** `Toolbox`  
**Type:** Decoupled Modular Add-on (`IOptiAssistAddon`)  
**DLL Location:** `Config\Addons\Toolbox\Toolbox.dll`  
**Documentation File:** `docs/modulos/toolbox.md`  

---

## 1. Overview and Problem Solved

System repair and optimization often require running numerous specific command-line utilities (`sfc`, `dism`, `chkdsk`, `wsreset`, `ipconfig`, registry tweaks, and network printer RPC fixes). Memorizing complex syntax, handling nested quotation escaping, and remembering command-line switches across multiple machines is inefficient.

The **Toolbox** solves this by providing an extensible, customizable command execution workbench:
- Categorized accordions for system repairs, network fixes, Windows tweaks, and diagnostics.
- Multi-engine execution support: native `cmd.exe`, `powershell.exe`, and standalone portable binaries.
- Real-time stdout and stderr output streaming directly to the embedded console or floating `LogWindow`.
- In-app visual category and command editor: add, edit, reorder, or delete custom repair scripts without editing JSON files.
- Process control: terminate long-running processes on demand.

---

## 2. Step-by-Step Usage Guide

1. In the sidebar, click **🧰 Toolbox**.
2. Browse categorized accordions:
   - **System Repair**: SFC Scannow, DISM Component Store Cleanup, CHKDSK Scan, Print Spooler Reset, Network Printer RPC Fix, Windows Store Reset (WSReset).
   - **Performance & Disk**: CompactOS, WinSAT Benchmark, Clear Font Cache.
   - **Network & Diagnostics**: Flush DNS, Network Adapter Reset, Ping Diagnostics.
3. Click the **Run** button next to any action.
4. The button displays a spinning/busy state (`⏳ Running...`), the command executes asynchronously, and output streams to the console.
5. To stop a hanging process, click **🛑 Stop**.
6. **Customizing Actions**:
   - Click **Manage Categories** to add, rename, or delete categories.
   - Click **Add Command** to create a custom script, specify its shell type (`powershell`, `cmd`, `binary`), and set required administrative privileges.

---

## 3. Internal Architecture and Implementation

### 3.1 Command Runner Engine (`ToolboxProcessRunner`)
When an action is triggered, `ToolboxProcessRunner.RunAsync` coordinates execution:
1. Validates administrative privileges via `IsCurrentProcessElevated()`.
2. Encodes complex PowerShell commands as UTF-16LE Base64 strings:
   ```cmd
   powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand <Base64String>
   ```
3. Configures process redirection for `StandardOutput` and `StandardError`.
4. Dispatches line-by-line output events to `_host.Log`.

### 3.2 Persistent Catalog (`toolbox.json`)
The catalog is managed by `ToolboxStorageManager` in `Config/toolbox.json`, defining categories, commands, shells, and argument strings.

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**: `Config/toolbox.json` catalog and user selections.
* **Outputs**: Executed repair routines and real-time logs.
* **Configurations**: Handled via [`ToolboxStorageManager.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/Toolbox/ToolboxStorageManager.cs).

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Required for system-level repair commands (SFC, DISM, CHKDSK, Service resets).
* **Supported Windows Versions**: Windows 10 and Windows 11.
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **System Modifications**: Certain optimization commands alter registry settings or clear component caches.
* **Review Before Running**: Technicians should review commands before running unfamiliar scripts.

---

## 7. Error Handling and Logging

* Subprocess exit codes and error streams are captured and displayed in red/accent in the logs.
* Process cancellation terminates the active process tree cleanly.

---

## 8. Related Source Files

* [`src/Addons/Toolbox/ToolboxAddon.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/Toolbox/ToolboxAddon.cs): Add-on view, controls, and category accordions.
* [`src/Addons/Toolbox/ToolboxProcessRunner.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/Toolbox/ToolboxProcessRunner.cs): Process execution engine and output streaming.
* [`src/Addons/Toolbox/ToolboxStorageManager.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/Toolbox/ToolboxStorageManager.cs): JSON catalog manager.
* [`src/Addons/Toolbox/ToolboxModels.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/Toolbox/ToolboxModels.cs): Data models for commands and categories.
