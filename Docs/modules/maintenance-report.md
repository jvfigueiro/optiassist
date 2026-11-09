# Maintenance Report & Service Orders

**Module Identifier:** `maintenance-report`  
**Module Type:** Decoupled Add-on DLL (`Config/Addons/MaintenanceReport/MaintenanceReport.dll`)  
**Documentation Path:** `docs/modules/maintenance-report.md`  

![Maintenance Report Interface](../images/maintenance-report.png)

---

## 1. Overview and Problem Solved

Computer maintenance workshops, freelance technicians, and corporate IT departments must generate professional service records and work order summaries for clients and internal asset documentation. Creating these documents manually in Word or Notepad is repetitive, prone to omitted machine specifications, and disconnected from the diagnostics workbench.

The **Maintenance Report** module solves this problem by automatically combining customer metadata, a standardized 8-step technical procedure checklist, free-form technician observations, and the host's audited hardware telemetry into a structured plain-text report (`.txt`) ready for customer handoff or digital archiving.

---

## 2. Step-by-Step Usage Guide

1. Navigate to **Maintenance Report** on the sidebar.
2. **Fill Service Order Metadata**:
   - **Service Order (SO) Number**: Enter your internal tracking or invoice number (e.g., `SO-2026-1042`).
   - **Client Name**: Enter the customer or department name (e.g., `Acme Corp / John Doe`).
   - **Serial Number / Service Tag**: Automatically populated with the hardware serial number discovered by the Dashboard. Can be edited manually if needed.
3. **Select Performed Procedures (Checklist)**:
   - Check off all procedures performed during the maintenance session:
     - [x] Disk diagnostic and SMART health check
     - [x] Clean Windows format and reinstallation
     - [x] Essential application installation
     - [x] System integrity repair (DISM / SFC)
     - [x] Preventive internal cleaning & thermal paste replacement
     - [x] Full driver and BIOS update
     - [x] Deep temporary file cleanup
     - [x] Network and printer setup
4. **Enter Technical Report & Observations**:
   - Type specific technical notes, recommendations, bench findings, or component upgrade suggestions in the multiline text box.
5. **Generate and Export**:
   - **Default Report File Path**: Choose the output file path (defaults to `C:\MaintenanceReport.txt` or configured path in `Reports/`).
   - Click **Generate Maintenance Report**: Writes the complete document to disk and opens it in the default text viewer (Notepad).
   - Click **Copy Technical Report**: Instantly copies the full formatted text report to the Windows clipboard for pasting into ticketing systems or emails.
   - Click **Open Reports Folder**: Opens the reports destination directory in Windows Explorer.

---

## 3. Internal Architecture and Technical Execution Flow

```
+-----------------------------------------------------------------------------------------+
|                        Technician Clicks "Generate Maintenance Report"                  |
+-----------------------------------------------------------------------------------------+
                                             |
                                             v
                      +---------------------------------------------+
                      | MaintenanceReportAddon.GenerateReport()     |
                      +---------------------------------------------+
                                             |
                                             v
                      +---------------------------------------------+
                      | Gather Host Data:                           |
                      | - IOptiAssistHost.Hardware (HardwareSummary)|
                      | - IOptiAssistHost.Settings (Company/Contact)|
                      +---------------------------------------------+
                                             |
                                             v
                      +---------------------------------------------+
                      | Build Formatted StringBuilder:              |
                      | 1. Header & Organization Information        |
                      | 2. Work Order Metadata (SO #, Client, Date) |
                      | 3. Hardware Telemetry & Windows Build Audit |
                      | 4. Performed Procedures (8-step Checklist)  |
                      | 5. Technician Observations & Diagnosis      |
                      | 6. Technician & Customer Signature Lines    |
                      +---------------------------------------------+
                                             |
                                             v
                      +---------------------------------------------+
                      | File.WriteAllText(destinationPath, text)    |
                      | Process.Start("notepad.exe", path)          |
                      | Host.Log("[REPORT] Successfully generated") |
                      +---------------------------------------------+
```

### Sample Generated Report Output:
```text
================================================================================
                         TECHNICAL MAINTENANCE REPORT
================================================================================
Technician / Company: OptiAssist
Contact / Support:    https://github.com/jvfigueiro/optiassist
Generated On:         11/09/2026 08:35:00

--------------------------------------------------------------------------------
1. SERVICE ORDER INFORMATION
--------------------------------------------------------------------------------
Service Order (SO):   SO-2026-1042
Client / Department:  Acme Corp / John Doe
Machine Serial / Tag: System Serial Number

--------------------------------------------------------------------------------
2. SYSTEM HARDWARE & OPERATING SYSTEM SPECIFICATIONS
--------------------------------------------------------------------------------
Operating System:     Microsoft Windows 11 Pro (Build 10.0.26300.9818)
Processor:            13th Gen Intel Core i5-13400 (10 Cores / 16 Threads)
Total RAM:            31.8 GB RAM
Graphics (GPU):       NVIDIA GeForce RTX 2070
Motherboard:          ASUSTeK COMPUTER INC. PRIME H610M-E D4
BIOS Version:         American Megatrends Inc. v4004 (03/06/2026)
Security Posture:     TPM Available (2.0) | Secure Boot: Active | BitLocker: Inactive

--------------------------------------------------------------------------------
3. PERFORMED TECHNICAL PROCEDURES
--------------------------------------------------------------------------------
 [X] Disk diagnostic and SMART health check
 [X] Clean Windows format and reinstallation
 [X] Essential application installation
 [X] System integrity repair (DISM / SFC)
 [X] Full driver and BIOS update
 [X] Deep temporary file cleanup

--------------------------------------------------------------------------------
4. TECHNICAL OBSERVATIONS & DIAGNOSIS
--------------------------------------------------------------------------------
System was cleaned of temporary files and thermal paste was reapplied.
All drivers updated to latest vendor releases. Windows fully updated.

================================================================================
Technician Signature: _______________________   Client: _______________________
================================================================================
```

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * User inputs: Service order number, client name, serial number override, procedure checkboxes, and observation notes.
  * Host inputs: `HardwareSummary` and company preferences from `settings.json`.
* **Outputs**:
  * Formatted `.txt` report file saved to the specified filesystem path.
  * Text content copied to Windows clipboard.
* **Configurations in `Config/settings.json`**:
  * `"CustomerName"`: Custom technician or company name displayed in report headers.
  * `"ContactInfo"`: Support URL, telephone, or email address.
  * `"ReportSavePath"`: Default target filesystem path (e.g., `C:\MaintenanceReport.txt`).

---

## 5. Requirements

* **Administrative Privileges**: Inherited from Host. Writing to root `C:\` requires administrator elevation.
* **Windows Version**: Windows 10 and Windows 11 (64-bit).
* **External Dependencies**: Zero. Relies entirely on .NET Framework file I/O and clipboard APIs.

---

## 6. Risks and Side Effects

* **Filesystem Changes**: Overwrites the specified output `.txt` file if it already exists.
* **Reversibility**: Non-destructive reporting operation.

---

## 7. Logging and Error Handling

* Unauthorized path write exceptions (e.g., trying to write to a write-protected USB drive or invalid directory) are caught gracefully, displaying an informational alert prompting the technician to select a writable path.
* Successful report generation and clipboard copy events are recorded in `IOptiAssistHost.Log`.

---

## 8. Related Source Files

* [`src/Addons/MaintenanceReport/MaintenanceReportAddon.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/Addons/MaintenanceReport/MaintenanceReportAddon.cs): Complete UI, form binding, report generation string builder, and clipboard exporter.
