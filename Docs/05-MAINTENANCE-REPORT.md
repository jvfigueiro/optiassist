# Chapter 05: Maintenance Service Report

The **Maintenance Report** module (`Config/Addons/MaintenanceReport/MaintenanceReport.dll`) enables technicians, repair shops, and IT service providers to generate standardized, professional technical records of the services performed on a client's computer.

Compiled as an independent, non-locking modular DLL add-on, it integrates seamlessly into the OptiAssist sidebar and reads global hardware telemetry directly through the host interface (`IOptiAssistHost`).

---

## 💼 In-Module Options & Contact Info Branding

The module includes a dedicated **`⚙️ Options`** drawer accessible right from the report header:
* **Company / Technician Name (`CustomerName`)**: Configured under Settings (`Customize Application`) and displayed prominently in the top header and footer of every generated report.
* **Contact Information (`ContactInfo`)**: Phone number, website, or email entered in the **Options** card and displayed directly below the company title.
* **Streamlined Design**: Moving Contact Information directly into Maintenance Report keeps global application Settings clean and uncluttered, ensuring contact details are maintained right where they are used.

---

## 📋 Report Structure & Data Flow

When generating a report, OptiAssist compiles the following structured document:

```text
=============================================================
                    ACME TECH REPAIRS
               support@acmetech.com • (555) 0199
                      Maintenance Report                                                
=============================================================

SERVICE DETAILS
-------------------------------------------------------------
Client:        John Doe
Order No.:     OS-2026-1042
Date:          22/10/2026 14:35:10

DEVICE SPECS
-------------------------------------------------------------
Device Name:   DESKTOP-PC01
BIOS:          American Megatrends Inc. 2.10 15/01/2024
OS:            Windows 11 Pro 64-bit (Build 22631)
MB:            ASUSTeK COMPUTER INC. PRIME B550M-A
CPU:           AMD Ryzen 5 5600X 6-Core Processor
RAM:           32.0 GB
GPU:           NVIDIA GeForce RTX 3070
Main Storage:  C: (420.5 GB free of 931.5 GB, NTFS)
Secure Boot:   Active
TPM:           Available (v2.0)
BitLocker:     Active
Serial Number: SN98421094821

SERVICES AND PROCEDURES PERFORMED
-------------------------------------------------------------
 [X] Disk diagnostic and SMART health check
 [X] Preventive internal cleaning & thermal paste replacement
 [X] Full driver and BIOS update
 [X] Essential application installation
 [X] Deep temporary file cleanup
 [X] System integrity repair (DISM / SFC)

OBSERVATIONS
-------------------------------------------------------------
Replaced thermal paste on CPU. Tested stress loads for 30 minutes;
temperatures remained stable below 68°C under full synthetic load.

=============================================================
             Report generated via ACME TECH REPAIRS
=============================================================
```

---

## 🛠️ Interactive Checklist of Performed Procedures

The module features an 8-point interactive checklist of standard service workshop operations:
* `[X]` **Disk diagnostic and SMART health check**: Verification of NVMe/SSD wear levels and HDD sector integrity.
* `[X]` **Preventive internal cleaning & thermal paste replacement**: Dust removal, fan lubrication, and thermal compound re-application.
* `[X]` **Clean Windows format and reinstallation**: Fresh OS installation with driver provisioning.
* `[X]` **Full driver and BIOS update**: Flashing motherboard firmware and installing certified vendor chipset/GPU drivers.
* `[X]` **Essential application installation**: Automated software deployment via App Manager.
* `[X]` **Deep temporary file cleanup**: Cache, temp, and junk purge via System Cleaner.
* `[X]` **System integrity repair (DISM / SFC)**: Verification of component store and protected OS binaries.
* `[X]` **Network and printer setup**: Configuration of local IP, DNS, and network printer shares.

---

## 💾 Multi-Destination Export Pipeline

OptiAssist ensures reports are never lost or misplaced:

1. **Automatic Portable Archiving (`Reports/`)**:
   * Reports are always saved automatically into the portable `Reports/` directory located on the root of the OptiAssist drive:
     `Reports/MaintenanceReport_<OrderNo>_<Timestamp>.txt`
2. **Client Machine Copy Prompt**:
   * Upon saving, the user is prompted whether to save an extra local copy on the client's computer at the path defined in `settings.json` (`ReportSavePath`, defaulting to `C:\MaintenanceReport.txt`).
3. **`📋 Copy Technical Report` Button**:
   * Copies the full formatted report to the Windows clipboard with a single click, ready for immediate pasting into ticketing systems, client WhatsApp chats, or invoices.
4. **`📂 Open Reports Folder` Button**:
   * Opens the local `Reports/` directory in Windows Explorer for rapid file management.
