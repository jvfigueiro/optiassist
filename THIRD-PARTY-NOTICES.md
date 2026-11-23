# Third-Party Notices and Licensing Inventory — OptiAssist

**Project License:** GNU General Public License v3.0 or later (`SPDX-License-Identifier: GPL-3.0-or-later`)  
**Copyright:** Copyright (C) 2026 João Figueiró  
**Full License Text:** [LICENSE](LICENSE)

OptiAssist incorporates or depends on third-party software and data components listed below. Each third-party component retains its original copyright, author attribution, and license terms. Nothing in OptiAssist's GPL-3.0-or-later license removes or replaces the original license notices of third-party components.

---

## 1. LibreHardwareMonitorLib (Hardware Monitor Module)

- **Component:** `LibreHardwareMonitorLib`
- **Copyright / Author:** LibreHardwareMonitor Contributors
- **Repository:** https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- **Version Used:** `0.9.6.0` (`0.9.6+3d331e3370efb858411f19511373eff65a218701`)
- **Files Incorporated:**
  - `lib/LibreHardwareMonitor/LibreHardwareMonitorLib.dll`
  - `Config/Addons/PCAssist/LibreHardwareMonitorLib.dll`
- **Original License:** Mozilla Public License 2.0 (`MPL-2.0`) — [THIRD-PARTY/MPL-2.0.txt](THIRD-PARTY/MPL-2.0.txt)
- **Usage:** Used unmodified as a separate compiled dependency referenced by `PCAssist.dll` (`src/Addons/PCAssist/Core/LibreHardwareEngine.cs`) for reading hardware sensors (temperature, voltage, frequency, load, power, fan speed).
- **Modifications:** None.
- **Applicable Obligations & MPL-2.0 Section 3.3:**
  - Under **MPL-2.0 Section 3.3**, MPL-2.0 covered files may be distributed as part of a Larger Work under GNU GPL-3.0-or-later, provided the MPL-2.0 notices are preserved and the Corresponding Source Code for the MPL-2.0 files remains available under MPL-2.0.
  - Integration code written for OptiAssist (`src/Addons/PCAssist/PCAssistAddon.cs`, `src/Addons/PCAssist/Core/LibreHardwareEngine.cs`) remains under `GPL-3.0-or-later`.
- **Source Code Location:** Upstream GitHub repository at tag `v0.9.6` / commit `3d331e3370efb858411f19511373eff65a218701` (see [SOURCE-INFO.md](SOURCE-INFO.md)).

### 1.1 Transitive Dependencies of LibreHardwareMonitorLib

| Package / Binary | Version | Copyright / Author | License | Files |
| :--- | :--- | :--- | :--- | :--- |
| `BlackSharp.Core` | `1.0.7` (`+c70b735...`) | Florian K. | `MPL-2.0` ([THIRD-PARTY/MPL-2.0.txt](THIRD-PARTY/MPL-2.0.txt)) | `lib/LibreHardwareMonitor/BlackSharp.Core.dll`, `Config/Addons/PCAssist/BlackSharp.Core.dll` |
| `DiskInfoToolkit` | `1.1.2` (`+25319ea...`) | Florian K. (parts derived from CrystalDiskInfo) | `MPL-2.0` ([THIRD-PARTY/MPL-2.0.txt](THIRD-PARTY/MPL-2.0.txt)) | `lib/LibreHardwareMonitor/DiskInfoToolkit.dll`, `Config/Addons/PCAssist/DiskInfoToolkit.dll` |
| `RAMSPDToolkit-NDD` | `1.4.2` (`+3b47b96...`) | Florian K. | `MPL-2.0` ([THIRD-PARTY/MPL-2.0.txt](THIRD-PARTY/MPL-2.0.txt)) | `lib/LibreHardwareMonitor/RAMSPDToolkit-NDD.dll`, `Config/Addons/PCAssist/RAMSPDToolkit-NDD.dll` |
| `HidSharp` | `2.6.4.0` | Copyright © 2010-2025 James F. Bellinger (`Illusory Studios LLC`) | `Apache-2.0` ([THIRD-PARTY/Apache-2.0.txt](THIRD-PARTY/Apache-2.0.txt)) | `lib/LibreHardwareMonitor/HidSharp.dll`, `Config/Addons/PCAssist/HidSharp.dll` |
| `System.Buffers` | `4.6.1` | © Microsoft Corporation. All rights reserved. | `MIT` ([THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)) | `lib/LibreHardwareMonitor/System.Buffers.dll`, `Config/Addons/PCAssist/System.Buffers.dll` |
| `System.Memory` | `4.6.3` | © Microsoft Corporation. All rights reserved. | `MIT` ([THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)) | `lib/LibreHardwareMonitor/System.Memory.dll`, `Config/Addons/PCAssist/System.Memory.dll` |
| `System.Numerics.Vectors` | `4.6.1` | © Microsoft Corporation. All rights reserved. | `MIT` ([THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)) | `lib/LibreHardwareMonitor/System.Numerics.Vectors.dll`, `Config/Addons/PCAssist/System.Numerics.Vectors.dll` |
| `System.Runtime.CompilerServices.Unsafe` | `6.1.2` | © Microsoft Corporation. All rights reserved. | `MIT` ([THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)) | `lib/LibreHardwareMonitor/System.Runtime.CompilerServices.Unsafe.dll`, `Config/Addons/PCAssist/System.Runtime.CompilerServices.Unsafe.dll` |

### 1.2 PawnIO Kernel Driver (External Optional Download)

- **Component:** `PawnIO` (`PawnIO_setup.exe`)
- **Repository:** https://github.com/namazso/PawnIO.Setup
- **Distribution Status:** **Not bundled or redistributed** with OptiAssist. Downloaded on-demand from the official GitHub release URL only when explicitly requested by the user in `Hardware Monitor > Sensors`.

---

## 2. BleachBit CleanerML Definitions (System Cleaner Module)

- **Component:** BleachBit CleanerML XML definitions & validation Makefiles
- **Copyright / Author:** Copyright (c) 2008-2026 Andrew Ziem (and Tobias B. Besemer for `Makefile.bat`)
- **Repository:** https://github.com/bleachbit/bleachbit
- **Version Used:** Upstream `master` snapshot imported on 2026-11-10 (commit `4b29b78` in OptiAssist history; see [SOURCE-INFO.md](SOURCE-INFO.md))
- **Files Incorporated:**
  - 34 CleanerML `.xml` files in `Config/Addons/SystemCleaner/cleaners/` carrying `SPDX-License-Identifier: GPL-3.0-or-later` and `Copyright (c) 2008-2026 Andrew Ziem.`
  - `Config/Addons/SystemCleaner/cleaners/Makefile` and `Makefile.bat`
- **Original License:** `GNU GPL-3.0-or-later` — [LICENSE](LICENSE)
- **Usage:** Declarative cleaning target definitions parsed at runtime by OptiAssist's independent C# engine (`CleanerMlEngine`, `CleanerBackupManager`, `SystemCleanerAddon.cs`), which remains authored by João Figueiró under `GPL-3.0-or-later`.
- **Modifications:**
  - Filtered the upstream cleaner set to 37 Windows-relevant definitions.
  - Modified `Config/Addons/SystemCleaner/cleaners/deepscan.xml` to add developer cleanup options (`node_modules`, `venv`, `pycache`); marked with a prominent modification notice per GPL-3.0 Section 5(a) while preserving original copyright.
  - Note: `adobe_cache.xml`, `onedrive.xml`, and `spotify.xml` are original OptiAssist CleanerML definitions authored under `GPL-3.0-or-later`.
- **Obligations:** Preserve copyright and `SPDX-License-Identifier: GPL-3.0-or-later` headers, document modifications, and keep XML files + C# source available under GPL-3.0-or-later.

---

## 3. PCI IDs Database (Platform Profiler / OptiBoost / Compatibility Checker)

- **Component:** The PCI ID Repository (`pci.ids`)
- **Copyright / Authors:** Copyright (c) Martin Mares, Albert Pool, and contributors
- **Origin URL:** https://pci-ids.ucw.cz/ | https://github.com/pciutils/pciids
- **Files Incorporated:** `Config/Data/pci.ids.min` (curated subset consumed by `src/Platform/PciIdsDatabase.cs`)
- **Original Dual License:** `BSD-3-Clause` OR `GPL-2.0-or-later`
- **License Selected for OptiAssist:** **`BSD-3-Clause`** — [THIRD-PARTY/BSD-3-Clause.txt](THIRD-PARTY/BSD-3-Clause.txt)
- **Modifications:** Curated subset containing GPU, chipset, and legacy hardware vendor/device IDs relevant to OptiAssist hardware profiling and known-issue detection. Original header and copyright notice preserved at the top of `Config/Data/pci.ids.min`.
- **Obligations:** Preserve original copyright notice (Martin Mares, Albert Pool, and contributors) and distribute with the BSD-3-Clause license text. `pci.ids.min` remains under `BSD-3-Clause` and is not reclassified as GPL-3.0.

---

## 4. Microsoft Fluent UI System Icons (User Interface)

- **Component:** Fluent UI System Icons (SVG Path Geometries)
- **Copyright / Author:** Copyright (c) Microsoft Corporation
- **Repository:** https://github.com/microsoft/fluentui-system-icons
- **Files Incorporated:** Embedded 24×24 SVG `<Path>` geometry constants in `src/FluentIcons.cs`
- **Original License:** `MIT` — [THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)
- **Modifications:** Converted selected 24×24 regular/filled SVG `d` path strings into C# `const string` fields for WPF vector rendering.
- **Obligations:** Preserve copyright notice and MIT permission notice in documentation.

---

## 5. Chris Titus Tech WinUtil (External Reference — Not Redistributed)

- **Component:** Chris Titus Tech's Windows Utility (`WinUtil`)
- **Copyright / Author:** Copyright (c) Chris Titus Tech
- **Repository:** https://github.com/ChrisTitusTech/winutil
- **Original License:** `MIT` — [THIRD-PARTY/MIT.txt](THIRD-PARTY/MIT.txt)
- **Distribution Status:** **Not redistributed.** No WinUtil source code (`.ps1`/`.psm1`) or binaries are bundled in OptiAssist.
- **Usage & Justification for MIT Attribution:**
  - **External Execution:** Remote launcher preset (`irm https://christitus.com/win | iex`) in `Config/Addons/Toolbox/toolbox.json`, `src/Addons/Toolbox/ToolboxStorageManager.cs`, `src/ShortcutsHelper.cs`, and `src/MainWindow.xaml.cs`.
  - **Preset Concepts / Catalog References:** Curated preset ideas and declarative catalog structures adapted in `Config/Addons/Toolbox/toolbox.json` (commit `ae51cb5`) and `Config/Debloater/apps_ctt.json`.
- **Obligations:** Preserve MIT attribution for adapted preset concepts and structures.

---

## 6. Baboo Temporary Files Script — Attribution & Technical Reference

- **Author / Credit:** Aurélio Baboo (BABOO)
- **Reference Article:** https://www.baboo.com.br/tutorial-windows/script-do-baboo-para-apagar-arquivos-temporarios/
- **Download Reference:** https://www.baboo.com.br/script/atual
- **Usage & Attribution:**
  - Credited as technical reference for Windows temporary-file identification and cleanup routines (`%TEMP%`, `C:\Windows\Temp`, `C:\Windows\Prefetch`, `WER\ReportQueue`, Recycle Bin, and `SoftwareDistribution\Download` / `wuauserv` reset) originally adapted during the development of OptiAssist's cleanup utilities (`TweakHelper.CleanTempFiles` and `TweakHelper.CleanWindowsUpdateCache` in `src/TweakHelper.cs`, lines 42–114), alongside BleachBit CleanerML definitions in `SystemCleanerAddon.cs`.
- **Technical Inspection & Licensing Status:**
  - `TweakHelper.cs` uses standard .NET Framework / Win32 APIs (`Path.GetTempPath()`, `Environment.SpecialFolder.Windows`, `Environment.SpecialFolder.CommonApplicationData`, `Shell32.SHEmptyRecycleBin`, `System.ServiceProcess.ServiceController`).
  - No original Baboo Batch script file is present in the repository for side-by-side comparison, and the reference page describes the script as open-source without specifying an explicit OSI-approved license for redistribution/modification.
  - **Pending Action Prior to Public Distribution:** Obtain express written authorization from the author or perform a clean, independent reimplementation of `TweakHelper.CleanTempFiles` and `TweakHelper.CleanWindowsUpdateCache`.
