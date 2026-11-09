# Build, Packaging, and Deployment Guide

**Document:** `docs/build.md`  
**Platform:** Windows 10 / 11 (64-bit)  
**Compiler:** Microsoft Build Engine (`MSBuild.exe` v4.8)  

---

## 1. Environment Prerequisites

To compile OptiAssist and all decoupled add-on modules from source, the development environment requires:

- **Operating System**: Windows 10 (version 1809 or later) or Windows 11 (64-bit).
- **Runtime**: Microsoft .NET Framework 4.8 (native on modern Windows 10 and 11 installations).
- **Build Tools**: Microsoft Build Engine (`MSBuild.exe` v4.0.30319 64-bit), located natively in:
  ```text
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
  ```
  Or Visual Studio 2019/2022 Build Tools.
- **Archiver (for packaging)**: 7-Zip CLI (`7z.exe`) or NanaZip (`7z.exe`).
- **External Dependencies**: Zero. No NuGet packages or package restores are required.

---

## 2. Compilation

### 2.1 One-Click Automated Build (`build.bat`)
Run the bundled batch script from the repository root:
```cmd
build.bat
```
The script locates the 64-bit `MSBuild.exe` and executes:
```cmd
"%MSBUILD%" OptiAssist.csproj /p:Configuration=Release /v:minimal
```

### 2.2 Cascading Add-on Build Pipeline
The primary project file (`OptiAssist.csproj`) contains an automated post-build target (`CleanupLocalArtifacts`):
```xml
<Target Name="CleanupLocalArtifacts" AfterTargets="Build">
  <Delete Files="mscorlib.dll;normidna.nlp;normnfc.nlp;normnfd.nlp;normnfkc.nlp;normnfkd.nlp" />
  <MSBuild Projects="src\Addons\AppManager\AppManager.csproj" Properties="Configuration=$(Configuration);Platform=$(Platform)" />
  <MSBuild Projects="src\Addons\SystemCleaner\SystemCleaner.csproj" Properties="Configuration=$(Configuration);Platform=$(Platform)" />
  <MSBuild Projects="src\Addons\MaintenanceReport\MaintenanceReport.csproj" Properties="Configuration=$(Configuration);Platform=$(Platform)" />
  <MSBuild Projects="src\Addons\Toolbox\Toolbox.csproj" Properties="Configuration=$(Configuration);Platform=$(Platform)" />
  <MSBuild Projects="src\Addons\StartupManager\StartupManager.csproj" Properties="Configuration=$(Configuration);Platform=$(Platform)" />
</Target>
```

#### Build Execution Order:
1. **Host Compilation**: Compiles `OptiAssist.exe` into the repository root.
2. **NGen Cleanup**: Removes redundant local framework artifacts (`mscorlib.dll`, `.nlp` normalization tables).
3. **Sequential Add-on Compilation**: Compiles each add-on Class Library in order. Each add-on outputs directly to its target folder:
   - `Config/Addons/AppManager/AppManager.dll`
   - `Config/Addons/SystemCleaner/SystemCleaner.dll`
   - `Config/Addons/MaintenanceReport/MaintenanceReport.dll`
   - `Config/Addons/Toolbox/Toolbox.dll`
   - `Config/Addons/StartupManager/StartupManager.dll`

### 2.3 Partial Core Compilation (`CoreBuild`)
To compile only the main host executable without recompiling the add-on modules:
```cmd
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" OptiAssist.csproj /t:CoreBuild /p:Configuration=Release
```

---

## 3. Packaging into `.7z` Portable Distribution

To generate the official standalone distribution archive (`OptiAssist-v1.1.0.0.7z` ~504 KB):

### 3.1 Distribution Folder Layout
Assemble the release files into a clean staging folder:
```text
OptiAssist/
|-- OptiAssist.exe                 # Compiled host executable (v1.1.0.0)
|-- LICENSE                        # GNU General Public License v3.0
|-- README.md                      # Project documentation
|-- Config/                        # Configuration files and plugins
|   |-- settings.json              # Fresh user preferences (freshstart: "true")
|   |-- custom_shortcuts.json      # Customizable Dashboard common tasks
|   |-- Languages/                 # Dynamic multi-language dictionaries
|   |   |-- en-US.json
|   |   `-- pt-BR.json
|   `-- Addons/                    # Compiled decoupled add-on DLLs & catalogs
|       |-- AppManager/
|       |-- MaintenanceReport/
|       |-- StartupManager/
|       |-- SystemCleaner/
|       `-- Toolbox/
|-- Maintenance/                   # Auxiliary portable diagnostic utilities
|   `-- README.txt
|-- Installers/                    # Local technician installer repository
|   `-- README.txt
`-- Reports/                       # Default maintenance report export directory
    `-- README.txt
```

### 3.2 Compression Command
Compress with maximum LZMA2 solid compression:
```powershell
7z a -t7z -mx=9 "OptiAssist-v1.1.0.0.7z" ".\release_stage\OptiAssist"
```

---

## 4. Native Installer Subsystem (`AppDeploymentHelper`)

Unlike tools that depend on external setup builders (Inno Setup, NSIS, WiX), OptiAssist includes an autonomous deployment engine built directly into `OptiAssist.exe`.

### 4.1 Standard Installation (Local PC)
Accessible via **Settings > Install OptiAssist** or the first-run Welcome Wizard:
1. Copies all files and directories into `%ProgramFiles%\OptiAssist`.
2. Creates `.lnk` shortcuts using COM `WScript.Shell`:
   - Desktop: `%PUBLIC%\Desktop\OptiAssist.lnk`
   - Start Menu: `%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\OptiAssist.lnk`
3. Registers the application in Windows Control Panel (*Programs and Features*):
   - Key: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist`
   - Values: `DisplayName`, `DisplayVersion`, `Publisher`, `InstallLocation`, `DisplayIcon`, and `UninstallString`.

### 4.2 Portable USB Deployment
1. Allows the technician to select any USB drive or external partition.
2. Deploys the application and selected modules without touching the host registry.
3. Optionally writes an `autorun.inf` file to the drive root:
   ```ini
   [AutoRun]
   open=OptiAssist\OptiAssist.exe
   icon=OptiAssist\OptiAssist.exe,0
   label=OptiAssist
   ```

### 4.3 Silent Uninstallation
To cleanly uninstall OptiAssist from the local PC:
```cmd
"%ProgramFiles%\OptiAssist\OptiAssist.exe" --uninstall
```
The uninstaller removes shortcuts, deletes the uninstall registry key, and deletes `%ProgramFiles%\OptiAssist`.
