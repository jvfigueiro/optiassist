# Startup Manager (Autostart & Scheduled Tasks)

**Module Identifier:** `StartupManager`  
**Type:** Decoupled Modular Add-on (`IOptiAssistAddon`)  
**DLL Location:** `Config\Addons\StartupManager\StartupManager.dll`  
**Documentation File:** `docs/modulos/startup-manager.md`  

---

## 1. Overview and Problem Solved

Software applications frequently configure automatic startup entries without explicit user awareness, degrading system boot performance and consuming background RAM and CPU resources. In Windows, autostart entries are scattered across multiple registry locations (HKCU and HKLM in both 32-bit and 64-bit views), file system Startup folders, and the Windows Task Scheduler.

The **Startup Manager** solves this problem by providing a unified, centralized inspector for all system entry points. It enables technicians to:
- Audit autostart applications across Registry keys (`Run`, `RunOnce`) and file system folders.
- Inspect and toggle Windows Scheduled Tasks triggered at boot or user logon.
- Filter native Microsoft/Windows entries to isolate third-party software.
- Safely disable or enable entries using Windows' official `StartupApproved` registry architecture (allowing full reversibility).
- Protect critical system services with risk warning confirmation dialogs.

---

## 2. Step-by-Step Usage Guide

### 2.1 Auditing Autostart Applications
1. In the sidebar, click **🚀 Startup Manager**.
2. Select the **Applications (Registry & Folders)** tab.
3. Review detected entries: Name, Executable Path, Location, and Status (*Enabled* or *Disabled*).
4. Use the search bar to locate specific programs.
5. To toggle an application:
   - Click the **Disable / Enable** button on the entry card.
   - The application changes its autostart state immediately without deleting registry values.

### 2.2 Auditing Scheduled Tasks
1. Select the **Scheduled Tasks** tab.
2. The list displays active tasks, their triggers, actions, and current status.
3. **Filter Native Entries**: Keep *"Hide native Microsoft / Windows entries"* checked to focus on third-party software.
4. If you uncheck the native filter, a risk warning dialog alerts you that modifying core Windows tasks can affect operating system stability.
5. To disable a task, select it and click **Disable Task**.

---

## 3. Internal Architecture and Implementation

### 3.1 Registry Inspection and `StartupApproved` Standard
Windows 8, 10, and 11 use the `StartupApproved\Run` registry key to disable entries without deleting the original `Run` command value:
- **Registry Keys Inspected**:
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` (64-bit view)
  - `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run` (32-bit view)
- **StartupApproved Mechanism**:
  - When enabled: Binary value starts with `02 00 00 00...`.
  - When disabled: Binary value starts with `03 00 00 00...`.
  OptiAssist reads and writes this binary format, preserving user configuration without corrupting paths.

### 3.2 Scheduled Tasks via COM Interop
Rather than relying on fragile parsing of `schtasks.exe /query`, Startup Manager connects directly to the Task Scheduler 2.0 COM API:
```csharp
Type taskServiceType = Type.GetTypeFromProgID("Schedule.Service");
dynamic taskService = Activator.CreateInstance(taskServiceType);
taskService.Connect();
dynamic rootFolder = taskService.GetFolder("\\");
```
This enables recursive enumeration, inspection of executable action paths, and state modifications (`task.Enabled = false`).

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * Windows Registry keys (`HKLM`, `HKCU`).
  * Startup folders (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`).
  * Task Scheduler COM hierarchy.
* **Outputs**:
  * Updated `StartupApproved` binary registry values.
  * Modified task states in Task Scheduler.

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Required to modify `HKLM` keys and system-level Scheduled Tasks.
* **Supported Windows Versions**: Windows 10 and Windows 11.
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **High Reversibility**: Disabling entries does not delete them; they can be re-enabled at any time.
* **System Task Warnings**: Unchecking the native filter displays an explicit warning dialog to prevent accidental modification of critical Windows services.

---

## 7. Error Handling and Logging

* Permission denied errors on protected tasks or registry keys are caught and reported to the log console without crashing.

---

## 8. Related Source Files

* [`src/Addons/StartupManager/StartupManagerAddon.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/StartupManager/StartupManagerAddon.cs): UI layout, controls, and search filtering.
* [`src/Addons/StartupManager/StartupEngine.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/StartupManager/StartupEngine.cs): Registry `StartupApproved` logic and Task Scheduler COM engine.
* [`src/Addons/StartupManager/StartupModels.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/StartupManager/StartupModels.cs): Data models for startup items and tasks.
