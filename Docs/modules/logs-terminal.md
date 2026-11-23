# Terminal & Logs (`v1.2.S7`)

## 1. Overview & Problem Solved
Long-running maintenance commands (DISM, SFC, Winget, OptiBoost batches, and Toolbox scripts) produce continuous output that should remain observable in a native command-line window without cluttering or blocking the main GUI. **Terminal** provides real-time external log tailing plus an optional floating `LogWindow`.

## 2. How to Use (Step-by-Step)
1. Click **>_ Terminal** in the sidebar at any time.
2. Windows Terminal (`wt.exe`, or PowerShell/CMD fallback) opens in a separate window streaming the live session log without navigating you away from your active tab.
3. Optional: in **Settings**, enable **Open external Logs window by default** to automatically open the terminal when batch tasks start.

## 3. How It Works Internally
1. `MainWindow.Log(string message)` marshals to the UI thread via `Dispatcher.Invoke`, appends to `txtConsoleLog`, writes UTF-8 lines to `%TEMP%\OptiAssist_Terminal_<pid>.log` under `_logFileLock`, and forwards to `_externalLogWindow` if open.
2. `MainWindow.OpenExternalLogWindow()` generates `%TEMP%\OptiAssist_Terminal_<pid>.ps1` containing `Get-Content -LiteralPath '<logPath>' -Wait -Encoding UTF8` and launches `%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe` with `-File "<scriptPath>"` (preventing `wt.exe` from interpreting `;` as tab delimiters), falling back to `powershell.exe` or `cmd.exe`.

## 4. Inputs, Outputs & Configuration
- **Inputs:** `MainWindow.Log(...)` calls from the host and all Add-on `IOptiAssistHost.Log(...)` calls.
- **Outputs:** `%TEMP%\OptiAssist_Terminal_<pid>.log`, `%TEMP%\OptiAssist_Terminal_<pid>.ps1`, or user-exported `.txt` logs via `SaveFileDialog`.
- **Configuration:** `OpenExternalLogsByDefault` in `Config\settings.json`.

## 5. Requirements
- **Admin:** Inherited from `OptiAssist.exe`.
- **Windows Version:** Windows 10 / 11 (`wt.exe` used when installed; automatic fallback to `powershell.exe` / `cmd.exe`).
- **External Dependencies:** None.

## 6. Risks & Side Effects
- Read-only log viewing; creates temporary `.log` and `.ps1` files in `%TEMP%`.

## 7. Logs & Error Handling
- `OpenExternalLogWindow()` catches launch exceptions and displays a warning via `OptiDialog.Show(...)`.

## 8. Related Source Files
- `src/MainWindow.xaml.cs` (`OpenExternalLogWindow`, `Log`, `EnsureSessionLogFile`)
- `src/LogWindow.cs`
