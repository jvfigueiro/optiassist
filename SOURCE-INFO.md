# Third-Party Source Code & Provenance Information — OptiAssist

This document records the upstream origin, version/commit identifiers, retrieval context, modifications, build-time vs. runtime separation, and corresponding source-code locations for third-party components used by OptiAssist.

---

## 1. LibreHardwareMonitorLib & Transitive Dependencies

- **Upstream Repository:** https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- **Package / Version Distributed:** `LibreHardwareMonitorLib` `0.9.6.0` (`0.9.6+3d331e3370efb858411f19511373eff65a218701`, target `net472`)
- **Upstream Commit:** `3d331e3370efb858411f19511373eff65a218701`
- **Retrieval Date:** 2026-11-21
- **Modifications:** None. Binaries are distributed unmodified.
- **Build-Time vs. Runtime Separation:**
  - **Build-time reference directory (development only):** `lib/LibreHardwareMonitor/*.dll` (referenced by `src/Addons/PCAssist/PCAssist.csproj`; not required in final runtime release archives).
  - **Runtime distribution directory (required in release packages):** `Config/Addons/PCAssist/*.dll` (copied automatically during build alongside `PCAssist.dll` and loaded at runtime by `AddonHelper`).
- **Corresponding Source Code (MPL-2.0 Section 3.2 / 3.3):**
  - `LibreHardwareMonitorLib` `0.9.6.0` (`MPL-2.0`, commit `3d331e3370efb858411f19511373eff65a218701`):
    - GitHub: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701
  - `BlackSharp.Core` `1.0.7` (`MPL-2.0`, commit `c70b735c6cec123ee8a046ac4a0bc6c606f52cf0`):
    - GitHub: https://github.com/Blacktempel/BlackSharp/tree/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0
    - NuGet: https://www.nuget.org/packages/BlackSharp.Core/1.0.7
  - `DiskInfoToolkit` `1.1.2` (`MPL-2.0`, commit `25319eae5781e75bcf141e844ceab2afe94d40ea`):
    - GitHub: https://github.com/Blacktempel/DiskInfoToolkit/tree/25319eae5781e75bcf141e844ceab2afe94d40ea
    - NuGet: https://www.nuget.org/packages/DiskInfoToolkit/1.1.2
  - `RAMSPDToolkit-NDD` `1.4.2` (`MPL-2.0`, commit `3b47b960e0830fef344624ad5e389675d5f0a1ce`):
    - GitHub: https://github.com/Blacktempel/RAMSPDToolkit/tree/3b47b960e0830fef344624ad5e389675d5f0a1ce
    - NuGet: https://www.nuget.org/packages/RAMSPDToolkit-NDD/1.4.2
  - `HidSharp` `2.6.4.0` (`Apache-2.0`):
    - NuGet: https://www.nuget.org/packages/HidSharp/2.6.4
  - Microsoft .NET BCL packages (`System.Buffers 4.6.1`, `System.Memory 4.6.3`, `System.Numerics.Vectors 4.6.1`, `System.Runtime.CompilerServices.Unsafe 6.1.2`, `MIT`):
    - GitHub: https://github.com/dotnet/maintenance-packages

---

## 2. BleachBit CleanerML Definitions

- **Upstream Repository:** https://github.com/bleachbit/bleachbit
- **Directory Upstream:** `cleaners/` (`CleanerML` XML rules + `Makefile` + `Makefile.bat`)
- **Import Date in OptiAssist:** 2026-11-10 (commit `4b29b781b9109e23c22643119c3e78e99fdb7c93`)
- **Upstream Commit:** Not recorded at time of initial import; headers carry `Copyright (c) 2008-2026 Andrew Ziem.` and `SPDX-License-Identifier: GPL-3.0-or-later`.
- **Modifications:**
  - Non-Windows cleaners (Linux/Unix/macOS-only definitions) were removed, leaving 34 upstream CleanerML XML files (`Config/Addons/SystemCleaner/cleaners/*.xml`) + 3 original OptiAssist XML files (`adobe_cache.xml`, `onedrive.xml`, `spotify.xml`).
  - `Config/Addons/SystemCleaner/cleaners/deepscan.xml` was extended with `node_modules`, `venv`, and `pycache` options (marked in header per GPL-3.0 Section 5a).
  - Note: `Makefile` and `Makefile.bat` in `cleaners/` are development/linting scripts and are not required in runtime release packages.
- **Corresponding Source Code (GPL-3.0-or-later):** Included directly in `Config/Addons/SystemCleaner/cleaners/` in this repository.

---

## 3. PCI IDs Database (`pci.ids.min`)

- **Upstream Project:** The PCI ID Project — https://pci-ids.ucw.cz/ (Mirror: https://github.com/pciutils/pciids)
- **Maintainers / Copyright:** Martin Mares, Albert Pool, and contributors
- **License Selected:** `BSD-3-Clause`
- **Retrieval / Curation Date:** 2026-11-18 (commit `695e522`)
- **Modifications:** Reduced to a curated runtime subset (`Config/Data/pci.ids.min`, 212 lines) covering major AMD/ATI, NVIDIA, Intel, SiS, VIA, Broadcom, and Ralink/MediaTek PCI IDs used by `OptiAssist.Platform.PciIdsDatabase`.
- **Upstream Source:** https://github.com/pciutils/pciids/blob/master/pci.ids

---

## 4. Microsoft Fluent UI System Icons

- **Upstream Repository:** https://github.com/microsoft/fluentui-system-icons
- **License:** `MIT`
- **Retrieval Date:** 2026-11-21
- **Modifications:** Selected 24×24 SVG `d` path strings embedded in `src/FluentIcons.cs` (compiled into `OptiAssist.exe`).

---

## 5. Chris Titus Tech WinUtil (External Reference — Not Redistributed)

- **Upstream Repository:** https://github.com/ChrisTitusTech/winutil
- **License:** `MIT`
- **Distribution Status:** **Not redistributed.** No WinUtil source code or binaries are bundled in OptiAssist.
- **Usage & Justification for MIT Attribution:**
  - External on-demand launch via `irm https://christitus.com/win | iex` in `toolbox.json`, `ToolboxStorageManager.cs`, `ShortcutsHelper.cs`, and `MainWindow.xaml.cs`.
  - Preset concepts and declarative catalog structures adapted in `Config/Addons/Toolbox/toolbox.json` (commit `ae51cb5`) and `Config/Debloater/apps_ctt.json`, for which MIT attribution is preserved in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## 6. Baboo Temporary Files Script (Pending Side-by-Side Verification)

- **Reference URL:** https://www.baboo.com.br/tutorial-windows/script-do-baboo-para-apagar-arquivos-temporarios/
- **Status:** Original `.bat` file is not present in the repository. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md#6-baboo-temporary-files-script--technical--legal-limitation-record) for the documented limitation and independent reimplementation requirement prior to public distribution.
