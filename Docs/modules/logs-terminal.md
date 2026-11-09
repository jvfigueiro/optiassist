# Logs & Diagnostic Terminal

**Module Identifier:** `terminal`  
**Module Type:** Host Native View (`MainWindow`)  
**Documentation Path:** `docs/modules/logs-terminal.md`  

---

## 1. Overview and Problem Solved

When automating batch operations (installing software suites, executing disk cleanups, running DISM/SFC system repairs, or applying registry scripts), technicians need complete real-time visibility into the execution output of background subprocesses. Traditional GUI utilities often hide console output behind generic progress bars, leaving technicians unaware of silent errors, download stalls, or non-zero exit codes.

The **Logs & Diagnostic Terminal** provides a thread-safe, centralized logging and output streaming console embedded directly within the OptiAssist interface. It features real-time autoscrolling, keyword search filtering, one-click clipboard copying, `.txt` file export, and an independent floating pop-out window (`LogWindow`) that can be detached and positioned on a secondary monitor while navigating other modules.

---

## 2. Step-by-Step Usage Guide

1. Navigate to **Logs** on the left sidebar.
2. **Review Real-Time Diagnostic Output**:
   - The terminal displays timestamped logs from hardware telemetry audits, module loader events, executed PowerShell/CMD commands, Winget package downloads, and file deletion routines.
3. **Search the Log Stream**:
   - Type in the **Search...** box at the top of the terminal. Matching lines are highlighted, allowing technicians to quickly find error codes or specific package names.
4. **Copy Output**:
   - Click **Copy All** to place the entire log buffer onto the Windows clipboard.
5. **Export to Plain Text**:
   - Click **Export Log**: A native `SaveFileDialog` prompts for a destination path, saving the complete session log as a `.txt` file for ticketing systems or customer records.
6. **Clear the Buffer**:
   - Click **Clear** to reset the console buffer for a new diagnostic session.
7. **Use the Detached Pop-Out Window**:
   - Click the **Pop-Out Window** button (or configure `"OpenExternalLogsByDefault": true` in Settings).
   - A dedicated `LogWindow` opens independently of the main window. Technicians can move this window to a second monitor and continue using the Dashboard, Toolbox, or App Manager while watching live output streams.

---

## 3. Internal Architecture and Technical Execution Flow

```
+-----------------------------------------------------------------------------------------+
|                  Any Module, Subprocess, or Thread Calls: MainWindow.Log()              |
+-----------------------------------------------------------------------------------------+
                                             |
                                             v
                      +---------------------------------------------+
                      | Is caller executing on a background thread? |
                      +---------------------------------------------+
                                     /             \
                             Yes    /               \   No
                                   v                 v
                 +-----------------------+     +-----------------------+
                 | Dispatcher.BeginInvoke|     | Direct UI Execution   |
                 +-----------------------+     +-----------------------+
                                   \                 /
                                    \               /
                                     v             v
                      +---------------------------------------------+
                      | Timestamp Prepended: [HH:mm:ss] <message>   |
                      | Appends to StringBuilder in-memory buffer   |
                      +---------------------------------------------+
                                             |
        +------------------------------------+------------------------------------+
        |                                                                         |
        v                                                                         v
+-------------------------------+                                 +-------------------------------+
| Embedded Console (txtConsoleLog)                                | Floating Window (_logWindow)  |
| - Appends line to TextBox     |                                 | - If open: appends line to    |
| - txtConsoleLog.ScrollToEnd() |                                 |   external LogWindow TextBox  |
+-------------------------------+                                 | - Auto-scrolls external view  |
                                                                  +-------------------------------+
```

### Buffer Management:
The logging pipeline streams text directly to the native WPF `TextBox txtConsoleLog`. When messages originate from background worker threads, `Dispatcher.Invoke` marshals the call onto the UI thread to guarantee safe, crash-free UI updates. Clearing the console (`BtnClearLog_Click`) flushes both the embedded console and the detached floating window.

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * Formatted log strings emitted by Host routines, Add-on modules (`IOptiAssistHost.Log`), and redirected subprocess streams (`OutputDataReceived`, `ErrorDataReceived`).
* **Outputs**:
  * Real-time text display in the embedded terminal and floating `LogWindow`.
  * Plain text files exported via `SaveFileDialog`.
  * Text content copied to Windows clipboard.
* **Configurations in `Config/settings.json`**:
  * `"OpenExternalLogsByDefault": true`: Automatically spawns the floating `LogWindow` whenever the Logs tab is clicked.

---

## 5. Requirements

* **Administrative Privileges**: Inherited from Host.
* **Windows Version**: Windows 10 and Windows 11 (64-bit).
* **External Dependencies**: Zero. Uses native WPF controls and standard .NET string builders.

---

## 6. Risks and Side Effects

* **System Impact**: Zero. The logging subsystem is strictly read-only and in-memory.
* **Reversibility**: Clearing the log empties the active buffer without altering persistent files.

---

## 7. Logging and Error Handling

* The `MainWindow.Log()` method is designed never to throw exceptions back to calling threads; any Dispatcher failure is suppressed to prevent diagnostic logging from crashing critical maintenance tasks.
* File export operations handle write-access errors cleanly via standard `try-catch` blocks.

---

## 8. Related Source Files

* [`src/MainWindow.xaml`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml): Embedded terminal UI elements (`txtConsoleLog`, action buttons).
* [`src/MainWindow.xaml.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml.cs): Central `Log(string message)` coordinator and buffer management.
* [`src/LogWindow.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/LogWindow.cs): Independent floating WPF window with search, copy, export, and auto-scroll capabilities.
