using System;
using System.IO;
using System.Diagnostics;
using System.Windows;

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
}
