using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
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

        public static void OpenDateTimeSettings()
        {
            OpenWindowsTool("timedate.cpl");
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
        public const string GplLicenseNoticeText =
@"This program is free software: you can redistribute it and/or modify 
it under the terms of the GNU General Public License as published by 
the Free Software Foundation, either version 3 of the License, or 
(at your option) any later version.

This program is distributed in the hope that it will be useful, 
but WITHOUT ANY WARRANTY; without even the implied warranty of 
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
GNU General Public License for more details.

You should have received a copy of the GNU General Public License 
along with this program. If not, see <https://www.gnu.org/licenses/>.";

        public static string GetDefaultInstallDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OptiAssist");
        }

        public static string GetInstalledDirectory()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist"))
                {
                    if (key != null)
                    {
                        object loc = key.GetValue("InstallLocation");
                        if (loc != null && !string.IsNullOrWhiteSpace(loc.ToString()))
                        {
                            return loc.ToString().TrimEnd('\\', '/');
                        }
                    }
                }
            }
            catch { }

            return GetDefaultInstallDirectory();
        }

        public static bool IsAppInstalled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist"))
                {
                    if (key != null) return true;
                }

                string targetDir = GetInstalledDirectory();
                string targetExe = Path.Combine(targetDir, "OptiAssist.exe");
                if (File.Exists(targetExe)) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void CreateUsbAutorun(string targetDir, Action<string> log = null)
        {
            try
            {
                string baseDir = string.IsNullOrWhiteSpace(targetDir) ? AppDomain.CurrentDomain.BaseDirectory : targetDir;
                string driveRoot = Path.GetPathRoot(baseDir);

                if (string.IsNullOrEmpty(driveRoot))
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_AutorunRootError", "Could not identify the root directory of the current drive."),
                        "AutoRun",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string exeFullPath = Path.Combine(baseDir.TrimEnd('\\', '/'), "OptiAssist.exe");
                if (!File.Exists(exeFullPath))
                {
                    exeFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OptiAssist.exe");
                }

                string relativeExePath = exeFullPath;
                if (exeFullPath.StartsWith(driveRoot, StringComparison.OrdinalIgnoreCase))
                {
                    relativeExePath = exeFullPath.Substring(driveRoot.Length).TrimStart('\\');
                }
                else
                {
                    relativeExePath = "OptiAssist.exe";
                }

                string autorunPath = Path.Combine(driveRoot, "autorun.inf");

                if (File.Exists(autorunPath))
                {
                    try
                    {
                        File.SetAttributes(autorunPath, FileAttributes.Normal);
                    }
                    catch { }
                }

                var sb = new StringBuilder();
                sb.AppendLine("[autorun]");
                sb.AppendLine(string.Format("open={0}", relativeExePath));
                sb.AppendLine(string.Format("icon={0},0", relativeExePath));
                sb.AppendLine("label=OptiAssist - Maintenance & Post-Installation");
                sb.AppendLine("action=Start OptiAssist");
                sb.AppendLine("shell\\optiassist=Open OptiAssist");
                sb.AppendLine(string.Format("shell\\optiassist\\command={0}", relativeExePath));
                sb.AppendLine("shell=optiassist");

                File.WriteAllText(autorunPath, sb.ToString(), Encoding.Default);

                if (log != null) log(string.Format("[AUTORUN] File created successfully at: {0} (pointing to {1})", autorunPath, relativeExePath));

                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_AutorunCreated", "autorun.inf file created successfully at drive root ({0})!\n\nConfigured target: {1}\n\nNote: On modern Windows 10/11 systems, USB AutoRun execution is restricted by Microsoft for security, but the custom drive icon and label will be applied when reconnecting the USB drive."), driveRoot, relativeExePath),
                    "USB AutoRun Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[AUTORUN ERROR] Failed to create autorun.inf: {0}", ex.Message));
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_AutorunError", "Failed to create autorun.inf: {0}"), ex.Message),
                    "AutoRun",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public static void ShowLicenseDialog(Window owner)
        {
            Window win = new Window
            {
                Title = LocalizationHelper.Get("LicenseWin_Title", "OptiAssist - Application License (GNU GPLv3)"),
                Width = 560,
                Height = 360,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            win.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Grid root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            TextBlock tbTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("About_License_Tag", "⚖️ License: GNU General Public License v3.0 (GNU GPLv3)"),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            headerStack.Children.Add(tbTitle);
            Grid.SetRow(headerStack, 0);
            root.Children.Add(headerStack);

            Border boxBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14)
            };
            boxBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            boxBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            TextBlock tbLicense = new TextBlock
            {
                Text = LocalizationHelper.Get("About_License_Body", GplLicenseNoticeText),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas, Lucida Console, Segoe UI"),
                FontSize = 11.5,
                LineHeight = 18
            };
            tbLicense.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            sv.Content = tbLicense;
            boxBorder.Child = sv;
            Grid.SetRow(boxBorder, 1);
            root.Children.Add(boxBorder);

            StackPanel bottomBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Button btnClose = new Button
            {
                Content = LocalizationHelper.Get("Btn_Close", "✕ Close"),
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0)
            };
            btnClose.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnClose.Click += (s, e) => win.Close();
            bottomBar.Children.Add(btnClose);
            Grid.SetRow(bottomBar, 2);
            root.Children.Add(bottomBar);

            win.Content = root;
            win.ShowDialog();
        }

        public static void ShowInstallDialog(Window owner, Action<string> log = null)
        {
            Window win = new Window
            {
                Title = LocalizationHelper.Get("InstallWin_Title", "📥 Install OptiAssist"),
                Width = 640,
                Height = 620,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            win.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Grid root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body Scroll
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Buttons

            // 1. Header
            StackPanel headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            TextBlock tbTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Title", "📥 Install OptiAssist"),
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbSub = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Subtitle", "Choose installation mode, target directory, and review license terms and conditions of use"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            tbSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            headerStack.Children.Add(tbTitle);
            headerStack.Children.Add(tbSub);
            Grid.SetRow(headerStack, 0);
            root.Children.Add(headerStack);

            // 2. Main Body
            ScrollViewer bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel bodyStack = new StackPanel();

            // Card A: Installation Mode (Normal vs Portable)
            Border modeCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            modeCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            modeCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel modeStack = new StackPanel();
            TextBlock lblModeTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Mode_Title", "1. Installation Mode"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            lblModeTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            modeStack.Children.Add(lblModeTitle);

            RadioButton rbNormal = new RadioButton
            {
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            rbNormal.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");
            StackPanel rbNormalContent = new StackPanel();
            TextBlock tbNormalHead = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Mode_Normal", "🖥️ Standard Installation (Recommended)"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5
            };
            tbNormalHead.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbNormalDesc = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Mode_NormalDesc", "Installs OptiAssist on this computer, creates Desktop and Start Menu shortcuts, and registers an entry in Windows Control Panel (Programs and Features)."),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            tbNormalDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            rbNormalContent.Children.Add(tbNormalHead);
            rbNormalContent.Children.Add(tbNormalDesc);
            rbNormal.Content = rbNormalContent;
            modeStack.Children.Add(rbNormal);

            RadioButton rbPortable = new RadioButton
            {
                IsChecked = false,
                Margin = new Thickness(0, 2, 0, 0),
                Cursor = Cursors.Hand
            };
            rbPortable.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");
            StackPanel rbPortableContent = new StackPanel();
            TextBlock tbPortableHead = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Mode_Portable", "💾 Portable Installation (USB Flash Drive / External Drive)"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5
            };
            tbPortableHead.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbPortableDesc = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Mode_PortableDesc", "Deploys a standalone portable copy of OptiAssist to any folder, partition, or USB drive without modifying the local Windows registry or Control Panel."),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            tbPortableDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            rbPortableContent.Children.Add(tbPortableHead);
            rbPortableContent.Children.Add(tbPortableDesc);
            rbPortable.Content = rbPortableContent;
            modeStack.Children.Add(rbPortable);

            modeCard.Child = modeStack;
            bodyStack.Children.Add(modeCard);

            // Card B: Target Directory & Options
            Border dirCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            dirCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            dirCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel dirStack = new StackPanel();
            TextBlock lblDirTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Dir_Title", "2. Target Directory & Options"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            lblDirTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            dirStack.Children.Add(lblDirTitle);

            Grid dirInputGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            dirInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dirInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string defaultSystemDir = GetDefaultInstallDirectory();
            TextBox txtTargetDir = new TextBox
            {
                Text = defaultSystemDir,
                Height = 30,
                FontSize = 11.5,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtTargetDir.SetResourceReference(TextBox.BackgroundProperty, "BrushInputBg");
            txtTargetDir.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            txtTargetDir.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            Grid.SetColumn(txtTargetDir, 0);
            dirInputGrid.Children.Add(txtTargetDir);

            Button btnBrowseDir = new Button
            {
                Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Browse..."),
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnBrowseDir.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnBrowseDir.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = LocalizationHelper.Get("InstallWin_Browse_Desc", "Select the destination folder or drive for OptiAssist:");
                    fbd.SelectedPath = txtTargetDir.Text;
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string selected = fbd.SelectedPath;
                        if (!selected.EndsWith("OptiAssist", StringComparison.OrdinalIgnoreCase))
                        {
                            selected = Path.Combine(selected, "OptiAssist");
                        }
                        txtTargetDir.Text = selected;
                    }
                }
            };
            Grid.SetColumn(btnBrowseDir, 1);
            dirInputGrid.Children.Add(btnBrowseDir);
            dirStack.Children.Add(dirInputGrid);

            CheckBox chkShortcuts = new CheckBox
            {
                Content = LocalizationHelper.Get("InstallWin_Chk_Shortcuts", "Create Desktop and Start Menu shortcuts & register in Control Panel"),
                IsChecked = true,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 4),
                Cursor = Cursors.Hand
            };
            chkShortcuts.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            dirStack.Children.Add(chkShortcuts);

            CheckBox chkAutorunAfterInstall = new CheckBox
            {
                Content = LocalizationHelper.Get("InstallWin_Chk_Autorun", "Create USB AutoRun (autorun.inf) on destination drive root"),
                IsChecked = false,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                Cursor = Cursors.Hand
            };
            chkAutorunAfterInstall.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            dirStack.Children.Add(chkAutorunAfterInstall);

            rbNormal.Checked += (s, e) =>
            {
                if (chkShortcuts != null)
                {
                    chkShortcuts.IsEnabled = true;
                    chkShortcuts.IsChecked = true;
                }
                if (chkAutorunAfterInstall != null)
                {
                    chkAutorunAfterInstall.IsChecked = false;
                }
                if (txtTargetDir != null && string.IsNullOrWhiteSpace(txtTargetDir.Text))
                {
                    txtTargetDir.Text = defaultSystemDir;
                }
            };

            rbPortable.Checked += (s, e) =>
            {
                if (chkShortcuts != null)
                {
                    chkShortcuts.IsChecked = false;
                    chkShortcuts.IsEnabled = false;
                }
                if (chkAutorunAfterInstall != null)
                {
                    chkAutorunAfterInstall.IsChecked = true;
                }
            };

            dirCard.Child = dirStack;
            bodyStack.Children.Add(dirCard);

            // Card C: License Terms & Conditions of Use
            Border licCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 4)
            };
            licCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            licCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel licStack = new StackPanel();
            TextBlock lblLicTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_License_Title", "3. License Terms & Conditions of Use (GNU GPLv3)"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            lblLicTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            licStack.Children.Add(lblLicTitle);

            string fullTerms = GplLicenseNoticeText + "\r\n\r\n" +
                LocalizationHelper.Get("InstallWin_Terms_Conditions",
                "CONDITIONS OF USE:\r\nOptiAssist provides system optimization, maintenance automation, and administrative tools. By installing or deploying OptiAssist, you acknowledge that administrative operations modify system configurations and that you are responsible for reviewing selected tasks prior to execution.");

            TextBox txtLicenseTerms = new TextBox
            {
                Text = fullTerms,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 115,
                FontFamily = new FontFamily("Consolas, Lucida Console, Segoe UI"),
                FontSize = 10.5,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            txtLicenseTerms.SetResourceReference(TextBox.BackgroundProperty, "BrushBgPrimary");
            txtLicenseTerms.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            txtLicenseTerms.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            licStack.Children.Add(txtLicenseTerms);

            CheckBox chkAcceptLicense = new CheckBox
            {
                Content = LocalizationHelper.Get("InstallWin_Chk_AcceptLicense", "I have read and agree to the License Terms (GNU GPLv3) and Conditions of Use"),
                IsChecked = true,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            chkAcceptLicense.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            licStack.Children.Add(chkAcceptLicense);

            licCard.Child = licStack;
            bodyStack.Children.Add(licCard);

            bodyScroll.Content = bodyStack;
            Grid.SetRow(bodyScroll, 1);
            root.Children.Add(bodyScroll);

            // 3. Footer Action Bar (Left: Create USB AutoRun | Right: Cancel, Install)
            Grid footerGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button btnCreateAutorun = new Button
            {
                Content = LocalizationHelper.Get("Btn_CreateAutorun", "💿 Create USB AutoRun"),
                ToolTip = LocalizationHelper.Get("Tooltip_CreateAutorun", "Create autorun.inf on drive root to display application icon and AutoPlay entry"),
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnCreateAutorun.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCreateAutorun.Click += (s, e) =>
            {
                string dirForAutorun = AppDomain.CurrentDomain.BaseDirectory;
                if (rbPortable.IsChecked == true && !string.IsNullOrWhiteSpace(txtTargetDir.Text))
                {
                    dirForAutorun = txtTargetDir.Text.Trim();
                }
                CreateUsbAutorun(dirForAutorun, log);
            };
            Grid.SetColumn(btnCreateAutorun, 0);
            footerGrid.Children.Add(btnCreateAutorun);

            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal };
            Button btnCancel = new Button
            {
                Content = LocalizationHelper.Get("Startup_Btn_Cancel", "Cancel"),
                Height = 32,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCancel.Click += (s, e) => win.Close();
            rightBtns.Children.Add(btnCancel);

            Button btnInstallNow = new Button
            {
                Content = LocalizationHelper.Get("InstallWin_Btn_InstallNow", "📥 Install Now"),
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0)
            };
            btnInstallNow.SetResourceReference(Button.StyleProperty, "BtnAccent");
            chkAcceptLicense.Checked += (s, e) => { btnInstallNow.IsEnabled = true; };
            chkAcceptLicense.Unchecked += (s, e) => { btnInstallNow.IsEnabled = false; };

            btnInstallNow.Click += (s, e) =>
            {
                if (chkAcceptLicense.IsChecked != true)
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("InstallWin_Msg_AcceptRequired", "Please accept the License Terms and Conditions of Use before proceeding."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string targetDir = txtTargetDir.Text.Trim();
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("InstallWin_Msg_DirRequired", "Please specify a valid destination directory."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool isPortable = (rbPortable.IsChecked == true);
                bool createShortcutsAndRegistry = !isPortable && (chkShortcuts.IsChecked == true);
                bool createAutorun = (chkAutorunAfterInstall.IsChecked == true);

                if (ExecuteInstallation(targetDir, isPortable, createShortcutsAndRegistry, createAutorun, log))
                {
                    win.DialogResult = true;
                    win.Close();
                }
            };
            rightBtns.Children.Add(btnInstallNow);
            Grid.SetColumn(rightBtns, 1);
            footerGrid.Children.Add(rightBtns);

            Grid.SetRow(footerGrid, 2);
            root.Children.Add(footerGrid);

            win.Content = root;
            win.ShowDialog();
        }

        public static bool ExecuteInstallation(string targetDir, bool isPortable, bool createShortcutsAndRegistry, bool createAutorun, Action<string> log = null)
        {
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                targetDir = targetDir.TrimEnd('\\', '/');

                if (string.Equals(targetDir, currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_AlreadyInstalled", "OptiAssist is already running from the selected directory:\n" + targetDir),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                if (log != null) log(string.Format("[INSTALL] Starting {0} installation to: {1}", isPortable ? "Portable" : "Standard", targetDir));
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                CopyDirectoryRecursive(currentDir, targetDir, log);

                string targetExe = Path.Combine(targetDir, "OptiAssist.exe");

                if (createShortcutsAndRegistry)
                {
                    // Start Menu Shortcut
                    string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                    string startMenuLnk = Path.Combine(startMenuDir, "OptiAssist.lnk");
                    CreateShortcut(startMenuLnk, targetExe, targetDir, "OptiAssist - System Optimization and Maintenance Assistant");

                    // Desktop Shortcut
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    string desktopLnk = Path.Combine(desktopDir, "OptiAssist.lnk");
                    CreateShortcut(desktopLnk, targetExe, targetDir, "OptiAssist - System Optimization and Maintenance Assistant");

                    // Registry Registration in Control Panel (Uninstall)
                    RegisterInUninstall(targetDir, targetExe);
                }

                if (createAutorun)
                {
                    CreateUsbAutorun(targetDir, log);
                }

                if (log != null) log("[INSTALL] OptiAssist successfully deployed to " + targetDir);

                string successMsg = isPortable
                    ? string.Format(LocalizationHelper.Get("Msg_PortableInstallSuccess", "OptiAssist portable installation completed successfully at:\n{0}"), targetDir)
                    : string.Format(LocalizationHelper.Get("Msg_InstallSuccess", "OptiAssist was successfully installed on this computer!\nLocation: {0}\nShortcuts have been created on your Desktop and Start Menu."), targetDir);

                MessageBox.Show(successMsg, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("[INSTALL ERROR] " + ex.Message);
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_InstallError", "Failed to install OptiAssist: {0}"), ex.Message),
                    "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static void InstallOptiAssist(Action<string> log = null)
        {
            ShowInstallDialog(Application.Current != null ? Application.Current.MainWindow : null, log);
        }

        public static void UninstallOptiAssist(Action<string> log = null)
        {
            try
            {
                string targetDir = GetInstalledDirectory();
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                bool runningFromTarget = string.Equals(targetDir.TrimEnd('\\', '/'), currentDir, StringComparison.OrdinalIgnoreCase);

                if (!IsAppInstalled() && !runningFromTarget)
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_NotInstalled", "OptiAssist is not installed on this device."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    LocalizationHelper.Get("Msg_UninstallConfirm", "Are you sure you want to uninstall OptiAssist from this device?\n\nThis will revert all installation actions (remove Desktop and Start Menu shortcuts, remove the Control Panel entry, and delete the installed files)."),
                    "OptiAssist - Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                // Delete Start Menu Shortcuts (Common & User)
                string[] startMenuDirs = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs)
                };
                foreach (string smDir in startMenuDirs)
                {
                    if (!string.IsNullOrEmpty(smDir))
                    {
                        string lnk = Path.Combine(smDir, "OptiAssist.lnk");
                        if (File.Exists(lnk))
                        {
                            try { File.Delete(lnk); } catch { }
                        }
                    }
                }

                // Delete Desktop Shortcuts (Common & User)
                string[] desktopDirs = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                };
                foreach (string dDir in desktopDirs)
                {
                    if (!string.IsNullOrEmpty(dDir))
                    {
                        string lnk = Path.Combine(dDir, "OptiAssist.lnk");
                        if (File.Exists(lnk))
                        {
                            try { File.Delete(lnk); } catch { }
                        }
                    }
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
