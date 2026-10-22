# Chapter 03: Toolbox & System Optimization Hub

The **Toolbox** module (`Config/Addons/Toolbox/Toolbox.dll`) serves as OptiAssist's extensible command runner and unified system optimization hub. It replaces legacy static optimization buttons with a dynamic, categorized execution engine capable of running custom batch files, PowerShell scripts, Command Prompt strings, and standalone binary executables.

All previous optimization routines and tweaks (SFC, DISM, Chkdsk, Network Printer RPC fix, Dual Boot RTC clock, CompactOS, WinSAT, etc.) are pre-configured as native presets inside Toolbox, allowing technicians to use, modify, or expand them as needed.

---

## 🏗️ Architecture & Data Model

Toolbox follows a decoupled layered architecture separating persistent data storage, process execution, and WPF presentation:

```text
┌────────────────────────────────────────────────────────┐
│                      ToolboxAddon                      │
│        (WPF View, Accordion Cards & Options Drawer)     │
└───────────────┬────────────────────────┬───────────────┘
                │                        │
                ▼                        ▼
    ┌──────────────────────┐  ┌──────────────────────┐
    │ ToolboxStorageManager│  │ ToolboxProcessRunner │
    │   (JSON Persistence) │  │  (Async Worker Thread│
    │   Config/Addons/     │  │   stdout/stderr      │
    │   Toolbox/           │  │   Process Tree Kill) │
    │   toolbox.json       │  └──────────┬───────────┘
    └──────────────────────┘             │
                                         ▼
                               ┌───────────────────┐
                               │ Central Logs &    │
                               │ External LogWindow│
                               └───────────────────┘
```

### 1. Data Schema (`ToolboxModels.cs`)
Each command item is represented by `ToolboxCommandItem`:

| Field | Type | Description |
|---|---|---|
| `Id` | String | Unique GUID or alphanumeric identifier. |
| `Title` | String | User-facing display title (e.g., `"Verificar integridade do disco C:"`). |
| `Description` | String | Optional subtitle or contextual explanation. |
| `Command` | String | Command line string or script to execute (e.g., `"chkdsk.exe C:"`). |
| `Category` | String | Category grouping string (e.g., `"System Integrity"`, `"Network"`, `"Performance"`). |
| `Shell` | String | Execution shell environment: `"cmd"`, `"powershell"`, or `"binary"`. |
| `WaitForExit` | Boolean | Whether execution waits synchronously for termination or runs in the background. |
| `CaptureOutput` | Boolean | Whether `stdout` and `stderr` streams are captured and routed to the logs. |
| `OpenExternalLogs` | Boolean | Whether to automatically pop out the external Logs window upon executing this action. |
| `TimeoutSeconds` | Integer | Maximum execution time in seconds (0 = unlimited). |

### 2. Process Execution Engine (`ToolboxProcessRunner.cs`)
- **Detached Background Threads**: Commands run on asynchronous worker threads via `Task.Run()`, ensuring the primary OptiAssist window remains 100% fluid and responsive.
- **Real-Time Streaming**: Standard output (`stdout`) and standard error (`stderr`) are captured line-by-line using asynchronous read events and piped directly to the central Logs console and the pop-out `LogWindow`.
- **Process Tree Termination**: If a user cancels a running task, `ToolboxProcessRunner` halts the entire child process hierarchy safely using `taskkill /T /F`.
- **Exit-Code & Error Detection**:
  - Exit code `0`: Reported as a clean success.
  - Non-zero exit code: Reported with the exact code number and highlighted in the logs.
  - UAC Elevation Cancelled (`Win32Exception` 1223): Handled gracefully without throwing an unhandled exception.
  - Missing Executable (`Win32Exception` 2/3 or exit code 9009): Clearly alerts the user that the binary or script was not found.

---

## 🎨 User Interface & Workflow

### 1. Clean, Native Card Design
To blend seamlessly into the application, action cards keep raw commands and implementation details hidden by default. Each card presents:
* Title and descriptive subtitle.
* Action buttons:
  - **`▶ Run`**: Instantly executes the command and automatically triggers the pop-out **LogWindow** so technicians can watch real-time console progress.
  - **`✏️ Edit`**: Opens the editing modal (visible when Edit Mode is active).

### 2. Collapsible Category Accordions
Commands are automatically grouped by their `Category` field into styled collapsible cards (`Expander`). Users can expand or collapse categories to keep their workspace organized.

### 3. Options Popover Drawer
Clicking the **`⚙️ Options`** button in the header opens an actions menu:
* **`➕ New Toolbox`**: Opens a form to create a new custom command with mandatory field validation.
* **`🔄 Restore Presets`**: Restores factory-default system repair commands and categories.
* **`📂 Open toolbox.json`**: Opens the raw JSON database in the system text editor for power users.
* **`✏️ Enable Edit Mode` / `Disable Edit Mode`**: Toggles visibility of the **Edit** button on action cards. When disabled, the interface is locked down to prevent accidental edits.
* **`🗂️ Manage Categories`**: Opens the Category Manager dialog.

### 4. Category Manager
Technicians can manage categories without touching raw JSON files:
* **Add Category**: Enter a new category name.
* **Rename Category**: Renames an existing category and automatically cascades the update across all member commands.
* **Delete Category**: Removes an empty category or prompts for confirmation before reassigning or deleting items.

---

## 🛠️ Built-in System Optimization & Repair Presets

OptiAssist comes pre-loaded with **40 verified maintenance, repair, and performance presets** organized into 7 dynamic categories, fully integrating the best-in-class optimization, debloat, and repair routines from the community (including the Chris Titus Tech WinUtil project):

### 🌟 Category: Ferramentas da Comunidade (Community Tools)
1. **Chris Titus WinUtil (Ferramenta de Otimização)**:
   - Command: `powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://christitus.com/win | iex"` (Shell: `powershell`)
   - Launches the popular open-source Chris Titus Tech Windows Utility for rapid system adjustments, debloating, and feature configuration.

### 🛡️ Category: Integridade e Reparos (System Integrity & Repairs)
2. **Verificar e Reparar Arquivos do Sistema (SFC /scannow)**:
   - Command: `sfc /scannow` (Shell: `cmd`)
   - Scans and replaces corrupted, altered, or missing protected Windows system binaries with healthy cached copies.
3. **Reparar Imagem do Windows (DISM RestoreHealth)**:
   - Command: `DISM /Online /Cleanup-Image /RestoreHealth` (Shell: `cmd`)
   - Repairs the Windows Component Store (`WinSxS`) by fetching healthy files from Windows Update.
4. **Reparação Completa do Sistema (DISM + SFC)**:
   - Command: `DISM /Online /Cleanup-Image /RestoreHealth && sfc /scannow` (Shell: `cmd`)
   - Sequentially executes DISM component repair followed by SFC system file check for maximum repair efficacy.
5. **Verificar Integridade do Disco (CHKDSK C:)**:
   - Command: `chkdsk C:` (Shell: `cmd`)
   - Performs a non-destructive read-only check of drive `C:` to detect filesystem inconsistencies.
6. **Corrigir Erro de Impressora em Rede (RPC 0x11b / 0x709)**:
   - Command: `reg add "HKLM\SYSTEM\CurrentControlSet\Control\Print" /v RpcAuthnLevelPrivacyEnabled /t REG_DWORD /d 0 /f && net stop Spooler && net start Spooler` (Shell: `cmd`)
   - Restores network printer connectivity across Windows workstations affected by security update changes.
7. **Corrigir Relógio do Windows em Dual Boot (UTC / Linux)**:
   - Command: `reg add "HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation" /v RealTimeIsUniversal /t REG_DWORD /d 1 /f` (Shell: `cmd`)
   - Sets Windows hardware clock to UTC (`RealTimeIsUniversal = 1`) to eliminate time desynchronization when dual-booting with Linux.
8. **Instalar ou Reparar Microsoft Store (wsreset -i)**:
   - Command: `wsreset.exe -i` (Shell: `binary`)
   - Reinitializes Microsoft Store components, manifests, and cache to resolve store launching and app installation failures.
9. **Reparar e Registrar Novamente o WinGet (Package Manager)**:
   - Command: `powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe ..."` (Shell: `powershell`)
   - Re-registers the `Microsoft.DesktopAppInstaller` package family to recover a damaged or missing `winget` command line.
10. **Redefinir e Reparar o Windows Update**:
    - Command: `powershell -NoProfile -ExecutionPolicy Bypass -Command "Stop-Service wuauserv, bits...; Rename-Item SoftwareDistribution\Download; netsh winsock reset; netsh winhttp reset proxy; Start-Service..."` (Shell: `powershell`)
    - Stops update services, cleans queue databases (`qmgr*.dat`), resets download caches, resets Winsock and WinHTTP proxy, and re-triggers update detection.

### 🌐 Category: Rede e Conectividade (Network & Connectivity)
11. **Limpar Cache DNS (Flush DNS)**:
    - Command: `ipconfig /flushdns` (Shell: `cmd`)
    - Purges the local DNS resolver cache to resolve stale hostname and IP mappings.
12. **Redefinir Catálogo Winsock e Pilha TCP/IP**:
    - Command: `netsh winsock reset && netsh int ip reset` (Shell: `cmd`)
    - Resets Winsock catalog and TCP/IP stack to resolve connectivity corruption, proxy anomalies, or stubborn socket errors.
13. **Redefinição Completa de Conectividade e DHCP**:
    - Command: `ipconfig /release && ipconfig /flushdns && netsh winsock reset && netsh int ip reset && ipconfig /renew` (Shell: `cmd`)
    - Complete release, stack reinitialization, DNS purge, and DHCP lease renewal.
14. **Listar Portas TCP Abertas (Listening)**:
    - Command: `Get-NetTCPConnection -State Listen | Select-Object LocalAddress, LocalPort, OwningProcess | Sort-Object LocalPort | Format-Table -AutoSize` (Shell: `powershell`)
    - Audits all active local listening TCP ports and their owning process identifiers (PID).

### ⚡ Category: Tweaks Essenciais (Essential Tweaks)
15. **Desativar Recursos de Consumidor e Sugestões (Consumer Features)**:
    - Command: `reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f` (Shell: `cmd`)
    - Blocks automatic installation of sponsored Microsoft Store apps, game promotions, and suggested apps.
16. **Desativar Otimização de Entrega (Delivery Optimization)**:
    - Command: `reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization" /v DODownloadMode /t REG_DWORD /d 0 /f` (Shell: `cmd`)
    - Disables P2P peer upload of Windows Updates to other computers on the local network or internet (`DODownloadMode = 0`).
17. **Desativar Criptografia BitLocker (Unidade C:)**:
    - Command: `powershell -NoProfile -ExecutionPolicy Bypass -Command "Disable-BitLocker -MountPoint $Env:SystemDrive"` (Shell: `powershell`)
    - Safely decrypts drive `C:` and removes BitLocker encryption overhead.
18. **Ativar "Finalizar Tarefa" no Clique Direito da Barra de Tarefas**:
    - Command: `reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings" /v TaskbarEndTask /t REG_DWORD /d 1 /f` (Shell: `cmd`)
    - Enables the native Windows 11 feature to terminate hanging processes directly from their taskbar jump icon.
19. **Desativar Hibernação do Sistema (Liberar hiberfil.sys)**:
    - Command: `powercfg.exe /hibernate off && reg add ... HibernateEnabled /d 0 /f` (Shell: `cmd`)
    - Turns off hibernation, deletes the massive `hiberfil.sys` file, and frees gigabytes of root storage.
20. **Desativar Telemetria Completa e Coleta de Dados do Windows**:
    - Command: Comprehensive PowerShell script applying privacy policies across AdvertisingInfo, Speech, TIPC, Personalization, SIUF, Windows System, Disabling `diagtrack` and `wermgr`, and setting `POWERSHELL_TELEMETRY_OPTOUT=1`.
21. **Remover Widgets da Barra de Tarefas (WebExperience)**:
    - Command: PowerShell removal of `Microsoft.WidgetsPlatformRuntime` and `MicrosoftWindows.Client.WebExperience` packages.
    - Frees memory and removes news/weather bloat from the taskbar.
22. **Desativar Execução WPBT (Bloatware de BIOS/OEM)**:
    - Command: `reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager" /v DisableWpbtExecution /t REG_DWORD /d 1 /f` (Shell: `cmd`)
    - Blocks OEMs from silently injecting background services and bloatware from UEFI ACPI WPBT tables into Windows on boot.
23. **Otimizar Serviços em Segundo Plano e Limite SvcHost**:
    - Command: Sets unneeded services (`CscService`, `SharedAccess`, `DiagTrack`) to Disabled/Manual and calculates `SvcHostSplitThresholdInKB` to match total physical RAM.
24. **Desativar Histórico de Atividades do Usuário**:
    - Command: `reg add ... /v PublishUserActivities /d 0 /f && reg add ... /v UploadUserActivities /d 0 /f` (Shell: `cmd`)
    - Disables local activity tracking and cloud telemetry sync for user actions and timeline history.

### 🚀 Category: Tweaks Avançados (Advanced Tweaks)
25. **Desativar Aplicativos em Segundo Plano (Background Apps)**:
    - Command: `reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" /v GlobalUserDisabled /t REG_DWORD /d 1 /f` (Shell: `cmd`)
    - Globally stops Universal Windows Platform (UWP) apps from running in the background when not in use.
26. **Desativar e Remover Recursos de IA do Windows (Copilot / Recall)**:
    - Command: Powershell routine removing Copilot Appx and WinGet packages, hiding AI components page (`hide:aicomponents`), disabling Notepad AI, disabling `Recall` optional feature, and stopping `WSAIFabricSvc`.
27. **Desativar Sensor de Armazenamento Automático (Storage Sense)**:
    - Command: `reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy" /v 01 /t REG_DWORD /d 0 /f` (Shell: `cmd`)
    - Prevents background deletion of files from user temporary and Downloads directories.
28. **Desativar Armazenamento Reservado (Liberar 7-10 GB)**:
    - Command: `DISM /Online /Set-ReservedStorageState /State:Disabled` (Shell: `cmd`)
    - Disables Windows Reserved Storage, immediately returning 7 to 10 GB of disk space back to the user partition.
29. **Debloat do Navegador Brave (Desativar Crypto, VPN e Leo AI)**:
    - Command: Injects Brave Software Enterprise policies disabling crypto rewards/wallet, built-in VPN, Leo AI chat, news feed, Tor tabs, and telemetry stats ping.
30. **Debloat do Microsoft Edge (Desativar Shopping, Telemetria e Popups)**:
    - Command: Injects Microsoft Edge Enterprise policies disabling shopping assistant, first-run prompts, personalization reporting, diagnostic telemetry, Edge collections, and desktop shortcut generation.

### 🎨 Category: Preferências e Interface (Preferences & UI)
31. **Otimizar Efeitos Visuais para Máximo Desempenho**:
    - Command: Powershell setting `MinAnimate = 0`, `MenuShowDelay = 50ms`, and `VisualFXSetting = 2`.
    - Eliminates menu and window lag for instant desktop response.
32. **Desativar Busca do Bing na Barra de Pesquisa do Windows**:
    - Command: `reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Search" /v BingSearchEnabled /t REG_DWORD /d 0 /f && reg add "HKCU\Software\Policies\Microsoft\Windows\Explorer" /v DisableSearchBoxSuggestions /t REG_DWORD /d 1 /f` (Shell: `cmd`)
    - Restricts Windows Search to local files and installed applications, removing Bing web queries and cloud advertisements.
33. **Desativar Aceleração do Mouse (Movimento 1:1 Preciso)**:
    - Command: `reg add "HKCU\Control Panel\Mouse" /v MouseSpeed /d "0" /f ...` (Shell: `cmd`)
    - Disables pointer acceleration curves for true 1:1 hardware linear mouse movement.

### 🏎️ Category: Otimização e Desempenho (Performance & Optimization)
34. **Limpar Pastas de Arquivos Temporários (%TEMP% e Temp)**:
    - Command: `cmd.exe /c "del /q /f /s \"%TEMP%\*.*\" 2>nul & del /q /f /s \"C:\Windows\Temp\*.*\" 2>nul ..."` (Shell: `cmd`)
    - Safely flushes temporary build, installer, and session files.
35. **Limpar Cache de Download do Windows Update**:
    - Command: Stops `wuauserv` & `bits`, deletes `%windir%\SoftwareDistribution\Download`, and restarts services.
36. **Desativar Serviços de Telemetria (DiagTrack / dmwappushservice)**:
    - Command: Disables and terminates `DiagTrack` and `dmwappushservice` services and sets `AllowTelemetry = 0`.
37. **Consultar Estado de Compactação do Sistema (CompactOS)**:
    - Command: `compact.exe /CompactOS:query` (Shell: `binary`)
    - Audits whether Windows binaries are currently compressed on disk.
38. **Compactar Binários do Windows (CompactOS)**:
    - Command: `compact.exe /CompactOS:always` (Shell: `binary`)
    - Compresses Windows system binaries using transparent hardware decompression, reclaiming 1.5 to 3.0 GB on drive `C:`.
39. **Executar Benchmark de Desempenho do Sistema (WinSAT Formal)**:
    - Command: `winsat.exe formal` (Shell: `binary`)
    - Evaluates system capabilities (CPU, RAM, Direct3D 3D graphics, sequential and random disk I/O).
40. **Listar Planos de Energia Ativos (PowerCfg)**:
    - Command: `powercfg.exe /list` (Shell: `binary`)
    - Lists all installed Windows power plans and flags the currently active profile.

