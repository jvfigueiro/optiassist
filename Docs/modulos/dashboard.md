# Dashboard & Common Tasks

**Module Identifier:** `dashboard`  
**Type:** Host Native View (`MainWindow`)  
**Documentation File:** `docs/modulos/dashboard.md`  

---

## 1. Overview and Problem Solved

When starting a maintenance session or diagnostic review, IT technicians must quickly identify the system's physical characteristics (processor model, installed and usable RAM, motherboard model, exact Windows build and revision, storage drives, TPM availability, Secure Boot state, and BitLocker encryption status). Gathering these details manually on Windows usually requires opening several disjointed native tools (`dxdiag`, `msinfo32`, `winver`, `tpm.msc`, `diskmgmt.msc`, `PowerShell`).

The **Dashboard** solves this problem by consolidating a complete hardware and security audit directly onto OptiAssist's landing screen, running asynchronously in the background without UI lag. Additionally, it provides the **Common Tasks** section: a customizable grid of quick-action shortcuts for daily technician operations (opening technician directories, launching memory tests, rebooting to UEFI, opening modules, and running community tools like Chris Titus WinUtil).

---

## 2. Step-by-Step Usage Guide

1. **Instant Hardware Telemetry**:
   - Open OptiAssist. The initial screen displayed is the **Dashboard**.
   - Review diagnostic telemetry cards:
     - **Operating System**: Edition name, exact build number with UBR revision, 64-bit architecture, Windows installation date, and genuine activation status (*Activated* or *Not Activated*).
     - **Hardware Specifications**: Processor model (core and thread count), total physical RAM, primary display adapter (GPU), motherboard manufacturer and board model, BIOS version and date, and hardware Serial Number / Service Tag (with a 1-click clipboard copy button).
     - **Security Posture**: TPM status (e.g. *Available (2.0)*), Secure Boot (*Active* or *Inactive*), and BitLocker protection on system drive `C:`.
2. **Manual Hardware Refresh**:
   - If hardware or security settings changed (e.g. BitLocker enabled or RAM module swapped), click **Dashboard** in the sidebar. The system re-audits hardware in the background.
3. **Executing Common Tasks**:
   - Click any button in the grid (e.g. `🧰 Toolbox`, `📦 App Manager`, `📁 Maintenance Folder`, `📂 Installers Folder`, `🧠 Memory Diagnostic`, `🖥️ Device Manager`, `⚙️ System Properties`).
4. **Customizing Common Tasks**:
   - Click **⚙️ Customize** to add, edit, reorder, or remove quick shortcuts.

---

## 3. Internal Architecture and Implementation

### 3.1 Asynchronous WMI and Registry Telemetry
1. On startup (`MainWindow_Loaded`), if `AutoRefreshHardware` is enabled in `settings.json`, `RefreshHardwareAsync()` executes in a background thread.
2. `SystemInfoHelper.GetHardwareSummaryAsync()` queries WMI/CIM:
   - `Win32_Processor`: Name, NumberOfCores, NumberOfLogicalProcessors.
   - `Win32_PhysicalMemory`: Aggregates Capacity across all slots into gigabytes.
   - `Win32_VideoController`: Primary GPU description.
   - `Win32_BaseBoard` & `Win32_BIOS`: Manufacturer, Product, BIOS Version, SerialNumber.
   - `Win32_Tpm` & `Win32_PnPEntity`: TPM detection.
   - `Root\CIMV2\Security\MicrosoftVolumeEncryption`: BitLocker status for drive `C:`.
3. Registry queries verify Secure Boot (`HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State`) and OS installation date (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\InstallDate`).
4. Extracted metrics populate `HardwareSummary` and update UI controls via `Dispatcher.BeginInvoke()`.

### 3.2 Dynamic Common Tasks Rendering
1. The host loads shortcuts from `Config/custom_shortcuts.json` via `CustomShortcutsHelper.Load()`.
2. `RenderCommonTasks()` populates `UniformGrid x:Name="ugCommonTasks"`, styling buttons dynamically (`BtnAccent`, `BtnDanger`, `BtnDefault`).
3. Clicking a button triggers `ExecuteCommonTask(item, btn)`. External processes or scripts (such as Chris Titus WinUtil) execute in the background via `RunShortcutCommandInBackground`, temporarily disabling the button (`⏳...`) and redirecting stdout/stderr to the console log.

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * `Config/custom_shortcuts.json`: Shortcut items (`Id`, `Title`, `Icon`, `TargetType`, `TargetValue`, `Arguments`).
  * WMI and Registry platform queries.
* **Outputs**:
  * Visual diagnostic cards on the dashboard.
  * Serial number copied to the clipboard.
  * Spawned Windows tools and scripts.
* **Configurations in `Config/settings.json`**:
  * `"AutoRefreshHardware": true`: Triggers automatic background hardware refresh.

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Required for low-level WMI classes (TPM, BitLocker, product activation, `HKLM` keys).
* **Supported Windows Versions**: Windows 10 and Windows 11 (64-bit recommended).
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **Hardware Queries**: 100% safe and non-destructive read-only operations.
* **Reboot to UEFI**: Reboots the machine immediately after user confirmation dialog.

---

## 7. Error Handling and Logging

* WMI timeouts or permission exceptions are caught and displayed as "Unavailable" without crashing the application.
* Failed shortcut commands write detailed error messages to `MainWindow.Log`.

---

## 8. Related Source Files

* [`src/MainWindow.xaml`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml): Dashboard layout, telemetry cards, and Common Tasks grid.
* [`src/MainWindow.xaml.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml.cs): Hardware refresh coordinator and shortcut handlers.
* [`src/SystemInfoHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/SystemInfoHelper.cs): Core WMI queries and `HardwareSummary` data models.
* [`src/SettingsHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/SettingsHelper.cs): `CustomShortcutsHelper` and shortcut item serialization.
