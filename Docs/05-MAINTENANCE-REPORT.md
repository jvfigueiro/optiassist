# Chapter 05: Maintenance Service Report

The **Maintenance Report** module enables technicians and IT service providers to generate polished, standardized technical records of the services performed on a client's computer.

---

## 💼 Service Provider Mode Branding

When **Service Provider Mode** is enabled in Settings, the report header and footer dynamically brand themselves with your business details:
* **Customer / Company Name (`CustomerName`)**: Displayed prominently at the top header and footer of every generated report.
* **Contact Information (`ContactInfo`)**: Phone number, website, or email displayed below the company title.

---

## 📋 Report Structure & Data Flow

When generating a report, OptiAssist compiles the following structured sections:

```
=============================================================
		    {CustomerName}
		    {ContactInfo}
		   Maintenance Report                                                
=============================================================

SERVICE DETAILS
-------------------------------------------------------------
Client:		{clientname}
Order No.:	{ordernumber}
Date:		{dateandtime}

DEVICE SPECS
-------------------------------------------------------------
Device Name:	{devicename}
BIOS:		{biosvendor} {version} {date}
OS:		{osname}
MB:		{mbmanufacturer}{mbmodel}
CPU:     	{cpumodel}
RAM:		{ramcapacity}
GPU:		{gpumodel}
Main Storage:	{letter} {freespace} {capacity} {filesystem}
Secure Boot:	{securebootstate}
TPM:		{tpmstate}
BitLocker:	{bitlockerstate}
Serial Number:	{serialnumber}

SERVICES AND PROCEDURES PERFORMED
-------------------------------------------------------------
 [X] Disk diagnostic and SMART health check
 [X] Full driver and BIOS update
 [X] Essential application installation

OBSERVATIONS
-------------------------------------------------------------
Replaced thermal paste on CPU. Tested stress loads for 30 minutes;
temperatures remained below 68°C under full load.

=============================================================
		Report generated via OptiAssist
=============================================================
```

---

## 🛠️ Interactive Checklist of Performed Procedures

Technicians can check off the relevant procedures executed during the service:
* `[X]` Disk diagnostic and SMART health check
* `[X]` Preventive internal cleaning & thermal paste replacement
* `[X]` Clean Windows format and reinstallation
* `[X]` Full driver and BIOS update
* `[X]` Essential application installation
* `[X]` Deep temporary file cleanup
* `[X]` System integrity repair (DISM / SFC)
* `[X]` Network and printer setup

---

## 💾 Export & Copy Actions

* **📄 Generate Maintenance Report**: Automatically formats and saves the report as a `.txt` file to the directory configured in `settings.json` (defaults to `C:\MaintenanceReport.txt` or `Reports/<OrderNo>_<Date>.txt`).
* **📋 Copy Technical Report**: Copies the full formatted report to the Windows clipboard for instant pasting into WhatsApp, email, or ticketing systems.
* **📂 Open Reports Folder**: Opens the local `Reports/` directory in Windows Explorer.
