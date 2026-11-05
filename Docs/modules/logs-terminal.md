# Logs Terminal & Floating Log Window

**Module Identifier:** `logs`  
**Type:** Host Native View (`MainWindow`) & Floating Window (`LogWindow`)  
**Documentation File:** `docs/modules/logs-terminal.md`  

---

## 1. Overview and Problem Solved

Automated maintenance utilities that execute silent commands without visible feedback often leave technicians uncertain about whether a process has succeeded, stalled, or crashed (e.g. running `sfc /scannow`, `dism`, or `winget install`).

The **Logs Terminal** solves this by providing complete transparency:
- Real-time timestamped event stream capturing host operations, module activities, and external subprocess stdout/stderr streams.
- Dual visual modes: an **embedded console** inside the main window, and a **detached, floating `LogWindow`** designed for multi-monitor setups.
- Export and auditing tools: fast keyword search, one-click clipboard copying, and `.txt` file export.

---

## 2. Step-by-Step Usage Guide

### 2.1 Using the Embedded Console
1. In the sidebar, click **📄 Logs**.
2. Review the timestamped event stream (`[HH:mm:ss] Message`).
3. Click **📋 Copy All** to copy the full buffer to the Windows Clipboard.
4. Click **🗑️ Clear Logs** to flush the buffer.
5. Click **💾 Export to .txt** to save a timestamped text log for customer records.

### 2.2 Using the Floating Log Window (`LogWindow`)
1. Click **🪟 Detach / Floating Window** in the Logs header (or enable *"Open External Logs By Default"* in Settings).
2. The independent window can be resized, minimized, or moved to a secondary monitor.
3. Output synchronizes live with the main console.

---

## 3. Internal Architecture and Implementation

### 3.1 Central Host Logging Pipeline
All components and add-ons invoke `_host.Log(msg)`:

```csharp
public void Log(string message)
{
    if (!Dispatcher.CheckAccess())
    {
        Dispatcher.Invoke(new Action(() => Log(message)));
        return;
    }

    string stamped = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);
    txtConsoleLog.AppendText(stamped + Environment.NewLine);
    txtConsoleLog.ScrollToEnd();

    if (_externalLogWindow != null && _externalLogWindow.IsLoaded)
    {
        _externalLogWindow.AppendLog(stamped);
    }
}
```

### 3.2 Asynchronous Subprocess Stream Redirection
When launching external processes:

```csharp
ProcessStartInfo psi = new ProcessStartInfo(fileName, args)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("[CMD] " + e.Data); };
proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("[ERROR] " + e.Data); };
```

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**: Internal application events and subprocess stdout/stderr.
* **Outputs**: Formatted console text, clipboard buffers, and `.txt` log files.
* **Configurations in `Config/settings.json`**:
  * `"OpenExternalLogsByDefault": true`: Launches the floating log window automatically on startup.

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Inherited from the host; logging requires no special permissions.
* **Supported Windows Versions**: Windows 10 and Windows 11.
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **Safe Operation**: 100% read-only diagnostic telemetry.

---

## 7. Error Handling and Logging

* Large log buffers are managed within memory limits; the Clear action frees memory immediately.
* Clipboard access exceptions are caught safely if another application holds a clipboard lock.

---

## 8. Related Source Files

* [`src/MainWindow.xaml`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml): Embedded console layout.
* [`src/MainWindow.xaml.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml.cs): Central `Log(string message)` pipeline.
* [`src/LogWindow.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/LogWindow.cs): Detached floating log window implementation.
