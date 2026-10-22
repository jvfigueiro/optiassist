using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace OptiAssist
{
    public static class ShortcutsHelper
    {
        public static string GetMaintenanceDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string maintDir = Path.Combine(baseDir, "Maintenance");
            if (!Directory.Exists(maintDir))
            {
                try { Directory.CreateDirectory(maintDir); } catch { }
            }
            return maintDir;
        }

        public static void OpenWindowsTool(string command, string args = "")
        {
            try
            {
                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                {
                    string sysnative = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative");
                    if (Directory.Exists(sysnative)) sysDir = sysnative;
                }

                string candidate = Path.Combine(sysDir, command);
                string targetFile = File.Exists(candidate) ? candidate : command;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = targetFile;
                psi.Arguments = args;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Fallback via cmd /c start
                try
                {
                    ProcessStartInfo psiFallback = new ProcessStartInfo();
                    psiFallback.FileName = "cmd.exe";
                    psiFallback.Arguments = string.Format("/c start {0} {1}", command, args);
                    psiFallback.UseShellExecute = true;
                    psiFallback.CreateNoWindow = true;
                    Process.Start(psiFallback);
                }
                catch
                {
                    MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_ToolOpenError", "Unable to start command '{0}': {1}"), command, ex.Message),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        public static void OpenBitLockerSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.BitLockerDriveEncryption") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:deviceencryption") { UseShellExecute = true });
                }
                catch
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:bitlocker") { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_ToolOpenError", "Unable to start command '{0}': {1}"), "BitLocker", ex.Message),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        public static void OpenMsConfig()
        {
            try
            {
                // msconfig must be opened with UseShellExecute or via cmd /c start
                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                {
                    string sysnative = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative");
                    if (Directory.Exists(sysnative)) sysDir = sysnative;
                }

                string msconfigPath = Path.Combine(sysDir, "msconfig.exe");
                if (File.Exists(msconfigPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = msconfigPath;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                else
                {
                    Process.Start("cmd.exe", "/c start msconfig.exe");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Process.Start("cmd.exe", "/c start msconfig.exe");
                }
                catch
                {
                    MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_MsconfigError", "Unable to open msconfig: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        public static void OpenNetplwiz()
        {
            OpenWindowsTool("netplwiz.exe");
        }

        public static void OpenMemoryDiagnostic()
        {
            OpenWindowsTool("mdsched.exe");
        }

        public static void OpenReliabilityMonitor()
        {
            OpenWindowsTool("perfmon.exe", "/rel");
        }

        public static void OpenEventViewer()
        {
            OpenWindowsTool("eventvwr.msc");
        }

        public static void OpenServices()
        {
            OpenWindowsTool("services.msc");
        }

        public static void OpenGpedit()
        {
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                string sysnative = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative");
                if (Directory.Exists(sysnative)) sysDir = sysnative;
            }

            string gpedit = Path.Combine(sysDir, "gpedit.msc");
            if (File.Exists(gpedit))
            {
                OpenWindowsTool("gpedit.msc");
            }
            else
            {
                MessageBox.Show(
                    LocalizationHelper.Get("Msg_GpeditNotFound", "The Local Group Policy Editor (gpedit.msc) was not found on this machine.\n\nThis tool is native exclusively to Windows Pro, Enterprise, and Education editions.\nOn Windows Home / Single Language editions, gpedit is not pre-installed by Windows."),
                    "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static void OpenDevicesAndPrinters()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "shell:::{A8A91A42-3A74-401B-987E-758B4F24E6DA}";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
                OpenWindowsTool("control.exe", "printers");
            }
        }

        public static void OpenChrisTitusTool()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://christitus.com/win | iex\"";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_ChrisTitusError", "Error launching Chris Titus Tool: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void OpenCrystalDiskInfo()
        {
            string maintDir = GetMaintenanceDirectory();
            string[] possiblePaths = new string[]
            {
                Path.Combine(maintDir, @"CrystalDiskInfo\DiskInfo64.exe"),
                Path.Combine(maintDir, @"CrystalDiskInfo\DiskInfo32.exe"),
                Path.Combine(maintDir, @"CrystalDiskInfo\DiskInfo.exe"),
                Path.Combine(maintDir, "DiskInfo64.exe")
            };

            foreach (string p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    OpenWindowsTool(p);
                    return;
                }
            }

            string expectedPath = Path.Combine(maintDir, @"CrystalDiskInfo\DiskInfo64.exe");
            MessageBoxResult res = MessageBox.Show(
                string.Format(LocalizationHelper.Get("Msg_CrystalDiskNotFound", "Portable CrystalDiskInfo was not found at:\n{0}\n\nDo you want to open the 'Maintenance' folder to place portable tool files?"), expectedPath),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                OpenMaintenanceFolder();
            }
        }

        public static void OpenCpuZ()
        {
            string maintDir = GetMaintenanceDirectory();
            string[] possiblePaths = new string[]
            {
                Path.Combine(maintDir, @"CPU-Z\cpuz_x64.exe"),
                Path.Combine(maintDir, @"CPU-Z\cpuz.exe"),
                Path.Combine(maintDir, "cpuz_x64.exe")
            };

            foreach (string p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    OpenWindowsTool(p);
                    return;
                }
            }

            string expectedPath = Path.Combine(maintDir, @"CPU-Z\cpuz_x64.exe");
            MessageBoxResult res = MessageBox.Show(
                string.Format(LocalizationHelper.Get("Msg_CpuZNotFound", "Portable CPU-Z was not found at:\n{0}\n\nDo you want to open the 'Maintenance' folder to place portable tool files?"), expectedPath),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                OpenMaintenanceFolder();
            }
        }

        public static void OpenMaintenanceFolder()
        {
            string dir = GetMaintenanceDirectory();
            try
            {
                Process.Start("explorer.exe", dir);
            }
            catch { }
        }

        public static void OpenAdminCmd()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
            }
            catch { }
        }

        public static void OpenAdminPowerShell()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoExit";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
            }
            catch { }
        }
    }

    public static class AppDeploymentHelper
    {
        public static string GetInstalledDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OptiAssist");
        }

        public static bool IsAppInstalled()
        {
            try
            {
                string targetDir = GetInstalledDirectory();
                string targetExe = Path.Combine(targetDir, "OptiAssist.exe");
                if (File.Exists(targetExe)) return true;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void InstallOptiAssist(Action<string> log = null)
        {
            try
            {
                string targetDir = GetInstalledDirectory();
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

                if (string.Equals(targetDir.TrimEnd('\\', '/'), currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_AlreadyInstalled", "OptiAssist is already running from the installed directory:\n" + targetDir),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string confirmMsg = string.Format(
                    LocalizationHelper.Get("Msg_InstallConfirm", "Do you want to install OptiAssist to '{0}'?\n\nThis will copy application binaries, configurations, modules, and create shortcuts on your Desktop and Start Menu."),
                    targetDir);

                var confirm = MessageBox.Show(confirmMsg, "OptiAssist - Install", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                if (log != null) log("[INSTALL] Creating target installation directory: " + targetDir);
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                CopyDirectoryRecursive(currentDir, targetDir, log);

                string targetExe = Path.Combine(targetDir, "OptiAssist.exe");

                // Start Menu Shortcut
                string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                string startMenuLnk = Path.Combine(startMenuDir, "OptiAssist.lnk");
                CreateShortcut(startMenuLnk, targetExe, targetDir, "OptiAssist - System Optimization and Maintenance Assistant");

                // Desktop Shortcut
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string desktopLnk = Path.Combine(desktopDir, "OptiAssist.lnk");
                CreateShortcut(desktopLnk, targetExe, targetDir, "OptiAssist - System Optimization and Maintenance Assistant");

                // Registry Registration
                RegisterInUninstall(targetDir, targetExe);

                if (log != null) log("[INSTALL] OptiAssist successfully installed to " + targetDir);

                MessageBox.Show(
                    LocalizationHelper.Get("Msg_InstallSuccess", "OptiAssist was successfully installed on this computer!\n\nShortcuts have been created on your Desktop and Start Menu."),
                    "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (log != null) log("[INSTALL ERROR] " + ex.Message);
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_InstallError", "Failed to install OptiAssist: {0}"), ex.Message),
                    "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void UninstallOptiAssist(Action<string> log = null)
        {
            try
            {
                string targetDir = GetInstalledDirectory();
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                bool runningFromTarget = string.Equals(targetDir.TrimEnd('\\', '/'), currentDir, StringComparison.OrdinalIgnoreCase);

                if (!Directory.Exists(targetDir) && !runningFromTarget)
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_NotInstalled", "OptiAssist is not installed in Program Files on this device."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    LocalizationHelper.Get("Msg_UninstallConfirm", "Are you sure you want to uninstall OptiAssist from this device?\n\nThis will remove Desktop and Start Menu shortcuts and unregister the application."),
                    "OptiAssist - Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                // Delete Shortcuts
                string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                string startMenuLnk = Path.Combine(startMenuDir, "OptiAssist.lnk");
                if (File.Exists(startMenuLnk))
                {
                    try { File.Delete(startMenuLnk); } catch { }
                }

                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string desktopLnk = Path.Combine(desktopDir, "OptiAssist.lnk");
                if (File.Exists(desktopLnk))
                {
                    try { File.Delete(desktopLnk); } catch { }
                }

                // Delete Registry Key
                UnregisterFromUninstall();

                if (log != null) log("[UNINSTALL] Shortcuts and registry registrations removed.");

                if (runningFromTarget)
                {
                    string cmd = string.Format("/c ping 127.0.0.1 -n 3 >nul & rd /s /q \"{0}\"", targetDir);
                    Process.Start(new ProcessStartInfo("cmd.exe", cmd)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_UninstallSuccessClosing", "OptiAssist has been successfully uninstalled from this computer. The application will now close."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    if (Directory.Exists(targetDir))
                    {
                        try { Directory.Delete(targetDir, true); } catch { }
                    }

                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_UninstallSuccess", "OptiAssist was successfully uninstalled from this computer!"),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("[UNINSTALL ERROR] " + ex.Message);
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_UninstallError", "Failed to uninstall OptiAssist: {0}"), ex.Message),
                    "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir, Action<string> log)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fName = Path.GetFileName(file);
                if (fName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                string dest = Path.Combine(targetDir, fName);
                try { File.Copy(file, dest, true); } catch { }
            }

            foreach (string sub in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(sub);
                if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".vs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string destSub = Path.Combine(targetDir, dirName);
                if (!Directory.Exists(destSub)) Directory.CreateDirectory(destSub);
                CopyDirectoryRecursive(sub, destSub, log);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    object shell = Activator.CreateInstance(shellType);
                    object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                    if (shortcut != null)
                    {
                        Type scType = shortcut.GetType();
                        scType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                        scType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                        scType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { description });
                        scType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                        scType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                        return;
                    }
                }
            }
            catch { }

            try
            {
                string ps = string.Format(
                    "-NoProfile -ExecutionPolicy Bypass -Command \"$s=(New-Object -COM WScript.Shell).CreateShortcut('{0}');$s.TargetPath='{1}';$s.WorkingDirectory='{2}';$s.Description='{3}';$s.IconLocation='{1},0';$s.Save()\"",
                    shortcutPath.Replace("'", "''"), targetPath.Replace("'", "''"), workingDir.Replace("'", "''"), description.Replace("'", "''"));
                Process.Start(new ProcessStartInfo("powershell.exe", ps)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        private static void RegisterInUninstall(string installDir, string exePath)
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "OptiAssist");
                        key.SetValue("DisplayVersion", "1.1.0");
                        key.SetValue("Publisher", "João Figueiró");
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("DisplayIcon", exePath + ",0");
                        key.SetValue("UninstallString", "\"" + exePath + "\" --uninstall");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private static void UnregisterFromUninstall()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist", false);
            }
            catch { }
        }
    }
}
