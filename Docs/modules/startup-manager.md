# Startup Manager (`startup-manager`) (`v1.2.S7`)

## 1. Overview & Problem Solved
Excessive autostart entries slow down Windows boot and consume background RAM. **Startup Manager** gives technicians unified visibility and non-destructive enable/disable/delete control over Registry Run keys, Startup folders, and Task Scheduler logon tasks.

## 2. How to Use (Step-by-Step)
1. Click **Startup Manager** in the sidebar.
2. Inspect discovered startup items grouped by **Registry** (`HKCU`/`HKLM` `Run` & `RunOnce`), **Startup Folders**, and **Scheduled Tasks**.
3. Toggle items **Enable / Disable** (non-destructive via Windows `StartupApproved` binary flags) or click **Delete** to permanently remove an entry.

## 3. How It Works Internally
1. `StartupEngine` enumerates `Run`/`RunOnce` (32/64-bit) Registry hives and reads `Explorer\StartupApproved\Run` / `StartupFolder` binary status bytes.
2. Enumerates User and Common Startup shell folders.
3. Connects to the Windows Task Scheduler 2.0 COM API (`Schedule.Service`) to list and toggle non-Microsoft logon/boot scheduled tasks.

## 4. Inputs, Outputs & Configuration
- **Inputs:** Windows Registry `Run`/`RunOnce`/`StartupApproved`, Startup folders, `Schedule.Service` COM.
- **Outputs:** Updated enabled/disabled states or removed startup entries.

## 5. Requirements
- **Admin:** Required to modify `HKLM` Run keys, Common Startup folder, and elevated Scheduled Tasks.
- **Windows Version:** Windows 10 / 11.
- **External Dependencies:** None.

## 6. Risks & Side Effects
- Disabling via `StartupApproved` is fully reversible; deleting an entry permanently removes its Registry value, `.lnk` shortcut, or Scheduled Task.

## 7. Logs & Error Handling
- Logs all enable/disable/delete operations and permission errors via `IOptiAssistHost.Log` and `OptiDialog`.

## 8. Related Source Files
- `src/Addons/StartupManager/StartupManagerAddon.cs`, `StartupEngine.cs`, `StartupModels.cs`
