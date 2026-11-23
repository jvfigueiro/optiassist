# Hardware Monitor (`hardware-monitor` / `PCAssist`) (`v1.2.S7`)

## 1. Overview & Problem Solved
Field technicians need live thermal/voltage/clock sensors, accurate physical storage bus identification, full PnP device inventory, notebook battery wear diagnostics, and quick CPU/RAM/Disk stress/bench checks without carrying multiple third-party executables.

## 2. How to Use (Step-by-Step)
1. Click **Hardware Monitor** in the sidebar.
2. **System Summary tab:** inspect motherboard, CPU, RAM DIMMs, GPU, physical disks (NVMe/SATA/USB, SSD vs. HDD), and network adapters.
3. **Sensors (Beta) tab:** monitor live Current / Min / Max temperatures, voltages, clocks, fans, and power draw; filter by keyword, click **Reset Min/Max**, **Export Report**, or **Install PawnIO Driver** if low-level sensors require the kernel driver.
4. **Device Inventory tab:** browse the categorized PnP hardware tree and error codes.
5. **Battery Health tab** *(shown on notebooks only)*: inspect design vs. full-charge capacity, wear level %, cycle count, and generate `powercfg /batteryreport`.
6. **Bench & Stress tab:** run quick CPU, memory bandwidth, or disk I/O benchmarks.

## 3. How It Works Internally
1. `PCAssistAddon`'s static constructor and `AddonHelper.PreloadSiblingDependencies` preload `LibreHardwareMonitorLib.dll` (`0.9.6.0`, `MPL-2.0`) and its 8 co-located dependency DLLs from `Config\Addons\PCAssist\` via `Assembly.Load(byte[])`.
2. `LibreHardwareEngine` wraps `LibreHardwareMonitor.Hardware.Computer` with a `HasVirtualizedStorageDisk()` guard (disabling `IsStorageEnabled` on VirtIO/QEMU/VMware virtual SCSI disks to avoid `DiskInfoToolkit` stack overflows) and optional on-demand **PawnIO** download (`https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe`).
3. `HardwareMonitorService`, `DeviceInventoryService`, `DisplayInfoService`, `BatteryDiagnosticService`, and `BenchEngineService` collect WMI/CIM, PnP, EDID, ACPI battery, and benchmark metrics.

## 4. Inputs, Outputs & Configuration
- **Inputs:** `LibreHardwareMonitorLib` hardware probes, WMI/CIM (`MSFT_PhysicalDisk`, `Win32_PnPEntity`, `BatteryStaticData`), optional PawnIO driver.
- **Outputs:** Real-time UI telemetry, exported sensor `.txt` reports, optional HTML battery report.

## 5. Requirements
- **Admin:** Required (`requireAdministrator`) for MSR/SMBus/ACPI/SMART queries and PawnIO installation.
- **Windows Version:** Windows 10 (1809+) or Windows 11.
- **External Dependencies:** `LibreHardwareMonitorLib.dll` (`MPL-2.0`) + 8 dependency DLLs in `Config\Addons\PCAssist\`.

## 6. Risks & Side Effects
- Read-only monitoring; PawnIO installation (optional, user-triggered) installs a signed kernel driver (`PawnIO.sys`).

## 7. Logs & Error Handling
- Logs `[LHM]` initialization state (`PawnIO`, `Storage`), `[PAWNIO]` download/install progress, and probe exceptions to `IOptiAssistHost.Log`.

## 8. Related Source Files
- `src/Addons/PCAssist/PCAssistAddon.cs`, `PCAssist.csproj`
- `src/Addons/PCAssist/Core/*.cs` (`LibreHardwareEngine.cs`, `HardwareMonitorService.cs`, `DeviceInventoryService.cs`, `BatteryDiagnosticService.cs`, `BenchEngineService.cs`, `DisplayInfoService.cs`, `NativeMethods.cs`)
