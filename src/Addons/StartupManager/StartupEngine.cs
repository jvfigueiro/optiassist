using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace OptiAssist.Addons.StartupManager
{
    /// <summary>
    /// Engine for scanning, modifying, toggling, and removing Windows startup items
    /// across Registry Run keys, Startup folders, Scheduled Tasks, and Services.
    /// </summary>
    public static class StartupEngine
    {
        private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunOnceRegPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        private const string StartupApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string StartupApprovedRun32Path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string StartupApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        private static readonly byte[] ApprovedEnabledBytes = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static readonly byte[] ApprovedDisabledBytes = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        #region STARTUP PROGRAMS (REGISTRY & FOLDERS)

        public static List<StartupProgramItem> GetAllStartupPrograms()
        {
            List<StartupProgramItem> list = new List<StartupProgramItem>();

            // 1. Current User (HKCU\Run)
            ScanRegistryRunKey(Registry.CurrentUser, RunRegPath, StartupLocationType.RegistryCurrentUser,
                "HKCU (Current User)", StartupApprovedRunPath, list);

            // 2. All Users (HKLM\Run 64-bit / native)
            ScanRegistryRunKey(Registry.LocalMachine, RunRegPath, StartupLocationType.RegistryLocalMachine,
                "HKLM (All Users)", StartupApprovedRunPath, list);

            // 3. All Users (HKLM\WOW6432Node\Run 32-bit)
            ScanRegistryRunKey(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                StartupLocationType.RegistryLocalMachine32, "HKLM 32-bit (All Users)", StartupApprovedRun32Path, list);

            // 4. Startup Folder (Current User)
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            ScanStartupFolder(userStartup, StartupLocationType.StartupFolderUser,
                "Startup Folder (User)", list);

            // 5. Startup Folder (Common / All Users)
            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            ScanStartupFolder(commonStartup, StartupLocationType.StartupFolderCommon,
                "Startup Folder (Common)", list);

            // Sort by Name
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static void ScanRegistryRunKey(
            RegistryKey root,
            string subKeyPath,
            StartupLocationType locType,
            string locDisplay,
            string approvedSubKey,
            List<StartupProgramItem> results)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subKeyPath, false))
                {
                    if (key == null) return;

                    string[] valueNames = key.GetValueNames();
                    foreach (string valName in valueNames)
                    {
                        if (string.IsNullOrWhiteSpace(valName)) continue;

                        object val = key.GetValue(valName);
                        string command = val != null ? val.ToString() : "";
                        string exePath = ExtractExePath(command);
                        bool fileExists = !string.IsNullOrEmpty(exePath) && File.Exists(exePath);

                        string publisher = "Unknown";
                        string fileDesc = "";
                        string fileSize = "—";
                        if (fileExists)
                        {
                            try
                            {
                                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(exePath);
                                if (!string.IsNullOrWhiteSpace(vi.CompanyName)) publisher = vi.CompanyName;
                                if (!string.IsNullOrWhiteSpace(vi.FileDescription)) fileDesc = vi.FileDescription;
                                else if (!string.IsNullOrWhiteSpace(vi.ProductName)) fileDesc = vi.ProductName;
                                FileInfo fi = new FileInfo(exePath);
                                fileSize = FormatBytes(fi.Length);
                            }
                            catch { }
                        }

                        bool isEnabled = IsRegistryStartupApproved(root, approvedSubKey, valName);

                        StartupProgramItem item = new StartupProgramItem
                        {
                            Id = locType.ToString() + ":" + valName,
                            Name = valName,
                            Command = command,
                            ExePath = exePath,
                            Publisher = publisher,
                            FileDescription = fileDesc,
                            LocationType = locType,
                            LocationDisplay = locDisplay,
                            SourcePath = subKeyPath,
                            FileExists = fileExists,
                            FileSizeDisplay = fileSize,
                            IsEnabled = isEnabled
                        };
                        results.Add(item);
                    }
                }
            }
            catch { }
        }

        private static void ScanStartupFolder(
            string folderPath,
            StartupLocationType locType,
            string locDisplay,
            List<StartupProgramItem> results)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

            try
            {
                string[] files = Directory.GetFiles(folderPath);
                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".lnk" && ext != ".exe" && ext != ".bat" && ext != ".cmd" && ext != ".url")
                        continue;

                    string fileName = Path.GetFileName(file);
                    string name = Path.GetFileNameWithoutExtension(file);
                    string targetPath = file;
                    if (ext == ".lnk")
                    {
                        string resolved = ResolveShortcutTarget(file);
                        if (!string.IsNullOrEmpty(resolved)) targetPath = resolved;
                    }

                    bool fileExists = File.Exists(targetPath);
                    string publisher = "Unknown";
                    string fileDesc = "";
                    string fileSize = "—";
                    if (fileExists)
                    {
                        try
                        {
                            FileVersionInfo vi = FileVersionInfo.GetVersionInfo(targetPath);
                            if (!string.IsNullOrWhiteSpace(vi.CompanyName)) publisher = vi.CompanyName;
                            if (!string.IsNullOrWhiteSpace(vi.FileDescription)) fileDesc = vi.FileDescription;
                            else if (!string.IsNullOrWhiteSpace(vi.ProductName)) fileDesc = vi.ProductName;
                            FileInfo fi = new FileInfo(targetPath);
                            fileSize = FormatBytes(fi.Length);
                        }
                        catch { }
                    }

                    RegistryKey root = (locType == StartupLocationType.StartupFolderUser) ? Registry.CurrentUser : Registry.LocalMachine;
                    bool isEnabled = IsRegistryStartupApproved(root, StartupApprovedFolder, fileName);

                    StartupProgramItem item = new StartupProgramItem
                    {
                        Id = locType.ToString() + ":" + fileName,
                        Name = name,
                        Command = targetPath,
                        ExePath = targetPath,
                        Publisher = publisher,
                        FileDescription = fileDesc,
                        LocationType = locType,
                        LocationDisplay = locDisplay,
                        SourcePath = file,
                        FileExists = fileExists,
                        FileSizeDisplay = fileSize,
                        IsEnabled = isEnabled
                    };
                    results.Add(item);
                }
            }
            catch { }
        }

        private static bool IsRegistryStartupApproved(RegistryKey root, string approvedSubKey, string valueName)
        {
            try
            {
                using (RegistryKey appKey = root.OpenSubKey(approvedSubKey, false))
                {
                    if (appKey == null) return true; // Enabled by default if key doesn't exist
                    object val = appKey.GetValue(valueName);
                    if (val is byte[])
                    {
                        byte[] bytes = (byte[])val;
                        if (bytes.Length > 0)
                        {
                            // 0x02 indicates enabled in Windows Task Manager; anything else (0x03, 0x01) is disabled
                            return bytes[0] == 0x02;
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        public static bool ToggleStartupProgram(StartupProgramItem item, out string error)
        {
            error = null;
            if (item == null) return false;

            try
            {
                bool newState = !item.IsEnabled;
                byte[] targetBytes = newState ? ApprovedEnabledBytes : ApprovedDisabledBytes;

                RegistryKey root = (item.LocationType == StartupLocationType.RegistryCurrentUser || item.LocationType == StartupLocationType.StartupFolderUser)
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                string approvedKeyPath = StartupApprovedRunPath;
                if (item.LocationType == StartupLocationType.RegistryLocalMachine32)
                {
                    approvedKeyPath = StartupApprovedRun32Path;
                }
                else if (item.LocationType == StartupLocationType.StartupFolderUser || item.LocationType == StartupLocationType.StartupFolderCommon)
                {
                    approvedKeyPath = StartupApprovedFolder;
                }

                using (RegistryKey key = root.CreateSubKey(approvedKeyPath))
                {
                    if (key != null)
                    {
                        string entryValName = (item.LocationType == StartupLocationType.StartupFolderUser || item.LocationType == StartupLocationType.StartupFolderCommon)
                            ? Path.GetFileName(item.SourcePath)
                            : item.Name;

                        key.SetValue(entryValName, targetBytes, RegistryValueKind.Binary);
                    }
                }

                item.IsEnabled = newState;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteStartupProgram(StartupProgramItem item, out string error)
        {
            error = null;
            if (item == null) return false;

            try
            {
                if (item.LocationType == StartupLocationType.StartupFolderUser || item.LocationType == StartupLocationType.StartupFolderCommon)
                {
                    if (File.Exists(item.SourcePath))
                    {
                        File.Delete(item.SourcePath);
                    }
                    // Remove from StartupApproved
                    RegistryKey root = (item.LocationType == StartupLocationType.StartupFolderUser) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey appKey = root.OpenSubKey(StartupApprovedFolder, true))
                    {
                        if (appKey != null)
                        {
                            try { appKey.DeleteValue(Path.GetFileName(item.SourcePath), false); } catch { }
                        }
                    }
                }
                else
                {
                    RegistryKey root = (item.LocationType == StartupLocationType.RegistryCurrentUser) ? Registry.CurrentUser : Registry.LocalMachine;
                    string subPath = item.SourcePath;
                    using (RegistryKey key = root.OpenSubKey(subPath, true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue(item.Name, false);
                        }
                    }

                    string approvedKeyPath = item.LocationType == StartupLocationType.RegistryLocalMachine32 ? StartupApprovedRun32Path : StartupApprovedRunPath;
                    using (RegistryKey appKey = root.OpenSubKey(approvedKeyPath, true))
                    {
                        if (appKey != null)
                        {
                            try { appKey.DeleteValue(item.Name, false); } catch { }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AddOrUpdateStartupProgram(string name, string command, StartupLocationType scope, string oldName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Name cannot be empty.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                error = "Command / Executable path cannot be empty.";
                return false;
            }

            try
            {
                RegistryKey root = (scope == StartupLocationType.RegistryCurrentUser) ? Registry.CurrentUser : Registry.LocalMachine;
                string subPath = (scope == StartupLocationType.RegistryLocalMachine32)
                    ? @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
                    : RunRegPath;

                using (RegistryKey key = root.CreateSubKey(subPath))
                {
                    if (key != null)
                    {
                        if (!string.IsNullOrEmpty(oldName) && !string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            try { key.DeleteValue(oldName, false); } catch { }
                        }
                        key.SetValue(name, command, RegistryValueKind.String);
                    }
                }

                // Ensure enabled in StartupApproved
                string approvedPath = (scope == StartupLocationType.RegistryLocalMachine32) ? StartupApprovedRun32Path : StartupApprovedRunPath;
                using (RegistryKey appKey = root.CreateSubKey(approvedPath))
                {
                    if (appKey != null)
                    {
                        if (!string.IsNullOrEmpty(oldName) && !string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            try { appKey.DeleteValue(oldName, false); } catch { }
                        }
                        appKey.SetValue(name, ApprovedEnabledBytes, RegistryValueKind.Binary);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        #endregion

        #region SCHEDULED TASKS (STARTUP & USER SOFTWARE)

        public static List<StartupTaskItem> GetStartupScheduledTasks(bool hideMicrosoft)
        {
            List<StartupTaskItem> list = new List<StartupTaskItem>();

            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                if (serviceType == null) return list;

                dynamic service = Activator.CreateInstance(serviceType);
                service.Connect();

                dynamic rootFolder = service.GetFolder("\\");
                EnumerateTasksRecursive(rootFolder, list, hideMicrosoft, 0, 4);
            }
            catch { }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static void EnumerateTasksRecursive(dynamic folder, List<StartupTaskItem> results, bool hideMicrosoft, int currentDepth, int maxDepth)
        {
            if (folder == null || currentDepth > maxDepth) return;

            try
            {
                dynamic tasks = folder.GetTasks(0);
                foreach (dynamic task in tasks)
                {
                    try
                    {
                        string name = task.Name;
                        string taskPath = task.Path;
                        bool enabled = task.Enabled;

                        string actionPath = "";
                        string args = "";
                        try
                        {
                            dynamic actions = task.Definition.Actions;
                            if (actions.Count > 0)
                            {
                                dynamic firstAction = actions[1];
                                actionPath = firstAction.Path ?? "";
                                args = firstAction.Arguments ?? "";
                            }
                        }
                        catch { }

                        string triggerType = "StartupOrLogon";
                        string triggerDisplay = "At Startup / User Logon";
                        bool hasStartupTrigger = false;
                        try
                        {
                            dynamic triggers = task.Definition.Triggers;
                            foreach (dynamic trg in triggers)
                            {
                                int trgType = (int)trg.Type;
                                // 8 = BootTrigger, 9 = LogonTrigger, 7 = RegistrationTrigger
                                if (trgType == 8)
                                {
                                    hasStartupTrigger = true;
                                    triggerType = "Boot";
                                    triggerDisplay = "At System Boot";
                                    break;
                                }
                                else if (trgType == 9 || trgType == 7)
                                {
                                    hasStartupTrigger = true;
                                    triggerType = "Logon";
                                    triggerDisplay = "At User Logon";
                                    break;
                                }
                            }
                        }
                        catch { }

                        string author = "";
                        try { author = task.Definition.RegistrationInfo.Author ?? ""; } catch { }

                        string publisher = "Unknown";
                        string fileDesc = "";
                        bool fileExists = false;
                        if (!string.IsNullOrEmpty(actionPath))
                        {
                            string cleanExe = ExtractExePath(actionPath);
                            if (File.Exists(cleanExe))
                            {
                                fileExists = true;
                                try
                                {
                                    FileVersionInfo vi = FileVersionInfo.GetVersionInfo(cleanExe);
                                    if (!string.IsNullOrWhiteSpace(vi.CompanyName)) publisher = vi.CompanyName;
                                    if (!string.IsNullOrWhiteSpace(vi.FileDescription)) fileDesc = vi.FileDescription;
                                    else if (!string.IsNullOrWhiteSpace(vi.ProductName)) fileDesc = vi.ProductName;
                                }
                                catch { }
                            }
                        }

                        bool isMicrosoft = IsMicrosoftTask(author, actionPath, publisher, taskPath);
                        if (hideMicrosoft && isMicrosoft) continue;

                        // Include task if it has a startup trigger OR is a recognized user program task
                        if (hasStartupTrigger || !isMicrosoft)
                        {
                            StartupTaskItem item = new StartupTaskItem
                            {
                                Name = name,
                                FolderPath = folder.Path,
                                FullTaskPath = taskPath,
                                ActionPath = actionPath,
                                Arguments = args,
                                TriggerType = triggerType,
                                TriggerDisplay = triggerDisplay,
                                Author = author,
                                Publisher = !string.IsNullOrEmpty(publisher) && publisher != "Unknown" ? publisher : (string.IsNullOrEmpty(author) ? "Unknown" : author),
                                FileDescription = fileDesc,
                                IsUserProgram = !isMicrosoft,
                                FileExists = fileExists,
                                IsEnabled = enabled
                            };
                            results.Add(item);
                        }
                    }
                    catch { }
                }

                dynamic subFolders = folder.GetFolders(0);
                foreach (dynamic sub in subFolders)
                {
                    string subName = sub.Name;
                    // If hideMicrosoft is true, skip internal \Microsoft folder
                    if (hideMicrosoft && string.Equals(subName, "Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    EnumerateTasksRecursive(sub, results, hideMicrosoft, currentDepth + 1, maxDepth);
                }
            }
            catch { }
        }

        private static bool IsMicrosoftTask(string author, string actionPath, string publisher, string taskPath)
        {
            if (!string.IsNullOrEmpty(taskPath) && taskPath.StartsWith("\\Microsoft\\Windows", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(author) && author.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(publisher) && publisher.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string cleanExe = ExtractExePath(actionPath ?? "").ToLowerInvariant();
            if (cleanExe.StartsWith(@"c:\windows\system32") || cleanExe.StartsWith(@"c:\windows\syswow64"))
                return true;

            return false;
        }

        public static bool ToggleTask(StartupTaskItem taskItem, out string error)
        {
            error = null;
            if (taskItem == null) return false;

            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                dynamic service = Activator.CreateInstance(serviceType);
                service.Connect();

                dynamic folder = service.GetFolder(taskItem.FolderPath);
                dynamic task = folder.GetTask(taskItem.Name);
                bool newState = !taskItem.IsEnabled;
                task.Enabled = newState;
                taskItem.IsEnabled = newState;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteTask(StartupTaskItem taskItem, out string error)
        {
            error = null;
            if (taskItem == null) return false;

            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                dynamic service = Activator.CreateInstance(serviceType);
                service.Connect();

                dynamic folder = service.GetFolder(taskItem.FolderPath);
                folder.DeleteTask(taskItem.Name, 0);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RunTask(StartupTaskItem taskItem, out string error)
        {
            error = null;
            if (taskItem == null) return false;

            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                dynamic service = Activator.CreateInstance(serviceType);
                service.Connect();

                dynamic folder = service.GetFolder(taskItem.FolderPath);
                dynamic task = folder.GetTask(taskItem.Name);
                task.Run(null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UpdateTask(StartupTaskItem taskItem, string newActionPath, string newArgs, out string error)
        {
            error = null;
            if (taskItem == null) return false;

            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                dynamic service = Activator.CreateInstance(serviceType);
                service.Connect();

                dynamic folder = service.GetFolder(taskItem.FolderPath);
                dynamic task = folder.GetTask(taskItem.Name);
                dynamic definition = task.Definition;

                if (definition.Actions != null && definition.Actions.Count > 0)
                {
                    definition.Actions[1].Path = newActionPath;
                    definition.Actions[1].Arguments = newArgs ?? "";
                }
                else
                {
                    dynamic action = definition.Actions.Create(0); // 0 = TASK_ACTION_EXEC
                    action.Path = newActionPath;
                    action.Arguments = newArgs ?? "";
                }

                // 4 = TASK_UPDATE, 0 = TASK_LOGON_NONE
                folder.RegisterTaskDefinition(taskItem.Name, definition, 4, null, null, 0, null);

                taskItem.ActionPath = newActionPath;
                taskItem.Arguments = newArgs ?? "";
                return true;
            }
            catch (Exception ex)
            {
                // Fallback to schtasks command line if COM registration requires different logon flags
                try
                {
                    string cleanPath = newActionPath;
                    if (!cleanPath.StartsWith("\"") && cleanPath.Contains(" "))
                    {
                        cleanPath = "\"" + cleanPath + "\"";
                    }
                    string fullTr = string.IsNullOrEmpty(newArgs) ? cleanPath : (cleanPath + " " + newArgs);
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = string.Format("/change /tn \"{0}\" /tr \"{1}\"", taskItem.FullTaskPath.TrimStart('\\'), fullTr.Replace("\"", "\\\"")),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit(5000);
                        if (p.ExitCode == 0)
                        {
                            taskItem.ActionPath = newActionPath;
                            taskItem.Arguments = newArgs ?? "";
                            return true;
                        }
                    }
                }
                catch { }

                error = ex.Message;
                return false;
            }
        }

        #endregion

        #region HELPERS

        public static string ExtractExePath(string rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand)) return "";
            string cmd = rawCommand.Trim();

            // Expand environment variables e.g. %LOCALAPPDATA%
            cmd = Environment.ExpandEnvironmentVariables(cmd);

            if (cmd.StartsWith("\""))
            {
                int nextQuote = cmd.IndexOf('"', 1);
                if (nextQuote > 1)
                {
                    return cmd.Substring(1, nextQuote - 1).Trim();
                }
            }

            // If no quote, find space or end
            int spaceIdx = cmd.IndexOf(' ');
            if (spaceIdx > 0)
            {
                string firstPart = cmd.Substring(0, spaceIdx).Trim();
                if (File.Exists(firstPart)) return firstPart;
            }

            if (File.Exists(cmd)) return cmd;
            return (spaceIdx > 0) ? cmd.Substring(0, spaceIdx) : cmd;
        }

        private static string ResolveShortcutTarget(string lnkPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return lnkPath;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                return shortcut.TargetPath;
            }
            catch
            {
                return lnkPath;
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            if (place >= suf.Length) place = suf.Length - 1;
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return num.ToString("0.#") + " " + suf[place];
        }

        #endregion
    }
}

