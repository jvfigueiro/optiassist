using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
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

        public static string GetInstallersDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.Combine(baseDir, "Installers");
            if (!Directory.Exists(dir))
            {
                string legacy = Path.Combine(baseDir, "PostInstall");
                if (Directory.Exists(legacy))
                {
                    return legacy;
                }
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
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
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"irm https://christitus.com/win | iex\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
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

        public static void OpenInstallersFolder()
        {
            string dir = GetInstallersDirectory();
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

        public static string GetGplNoticeText()
        {
            return LocalizationHelper.Get("About_License_Body", GplLicenseNoticeText);
        }

        public static string GetFullTermsAndConditionsText()
        {
            string notice = GetGplNoticeText();
            string terms = LocalizationHelper.Get("InstallWin_Terms_Conditions",
@"CONDITIONS OF USE:
OptiAssist provides system optimization, maintenance automation, and administrative tools. By installing or deploying OptiAssist, you acknowledge that administrative operations modify system configurations and that you are responsible for reviewing selected tasks prior to execution.");
            return notice + "\r\n\r\n" + terms;
        }

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

        public static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                string trimmed = path.Trim().Trim('"', '\'');
                if (trimmed.Length == 2 && trimmed[1] == ':')
                {
                    trimmed += "\\";
                }
                string full = Path.GetFullPath(trimmed);
                string root = Path.GetPathRoot(full);
                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                {
                    return root.TrimEnd('\\', '/') + "\\";
                }
                return full.TrimEnd('\\', '/');
            }
            catch
            {
                return path.Trim().TrimEnd('\\', '/');
            }
        }

        public static void CreateUsbAutorun(string targetDir, Action<string> log = null, bool showDialog = true)
        {
            try
            {
                string baseDir = string.IsNullOrWhiteSpace(targetDir) ? AppDomain.CurrentDomain.BaseDirectory : targetDir;
                string driveRoot = Path.GetPathRoot(baseDir);

                if (string.IsNullOrEmpty(driveRoot))
                {
                    if (showDialog)
                    {
                        MessageBox.Show(
                            LocalizationHelper.Get("Msg_AutorunRootError", "Could not identify the root directory of the current drive."),
                            "AutoRun",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
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

                if (showDialog)
                {
                    MessageBox.Show(
                        string.Format(LocalizationHelper.Get("Msg_AutorunCreated", "autorun.inf file created successfully at drive root ({0})!\n\nConfigured target: {1}\n\nNote: On modern Windows 10/11 systems, USB AutoRun execution is restricted by Microsoft for security, but the custom drive icon and label will be applied when reconnecting the USB drive."), driveRoot, relativeExePath),
                        "USB AutoRun Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[AUTORUN ERROR] Failed to create autorun.inf: {0}", ex.Message));
                if (showDialog)
                {
                    MessageBox.Show(
                        string.Format(LocalizationHelper.Get("Msg_AutorunError", "Failed to create autorun.inf: {0}"), ex.Message),
                        "AutoRun",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
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
                Text = GetGplNoticeText(),
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

            // 1. Header (Left: Title & Subtitle | Right: Language Selector)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock tbTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Title", "📥 Install OptiAssist"),
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbSub = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Subtitle", "Review license terms, choose installation mode, and select target directory"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            tbSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            headerStack.Children.Add(tbTitle);
            headerStack.Children.Add(tbSub);
            Grid.SetColumn(headerStack, 0);
            headerGrid.Children.Add(headerStack);

            StackPanel langPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            TextBlock lblLang = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Label_Language", "🌐 Language:"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            lblLang.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            langPanel.Children.Add(lblLang);

            ComboBox cmbLang = new ComboBox
            {
                Width = 145,
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            List<string> availableLangs = LocalizationHelper.GetAvailableLanguages();
            string currentLang = LocalizationHelper.CurrentLanguage;
            int selectedLangIdx = 0;
            for (int i = 0; i < availableLangs.Count; i++)
            {
                string code = availableLangs[i];
                string disp = LocalizationHelper.GetLanguageDisplayName(code);
                cmbLang.Items.Add(new ComboBoxItem { Content = disp, Tag = code });
                if (string.Equals(code, currentLang, StringComparison.OrdinalIgnoreCase))
                {
                    selectedLangIdx = i;
                }
            }
            if (cmbLang.Items.Count > 0) cmbLang.SelectedIndex = selectedLangIdx;
            langPanel.Children.Add(cmbLang);
            Grid.SetColumn(langPanel, 1);
            headerGrid.Children.Add(langPanel);

            Grid.SetRow(headerGrid, 0);
            root.Children.Add(headerGrid);

            // 2. Main Body
            ScrollViewer bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel bodyStack = new StackPanel();

            // Card 1 (FIRST): License Terms & Conditions of Use
            Border licCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            licCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            licCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel licStack = new StackPanel();
            TextBlock lblLicTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_License_Title", "1. License Terms & Conditions of Use (GNU GPLv3)"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            lblLicTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            licStack.Children.Add(lblLicTitle);

            string fullTerms = GetFullTermsAndConditionsText();

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
                IsChecked = false,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            chkAcceptLicense.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            licStack.Children.Add(chkAcceptLicense);

            licCard.Child = licStack;
            bodyStack.Children.Add(licCard);

            // Card 2: Installation Mode (Normal vs Portable)
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
                Text = LocalizationHelper.Get("InstallWin_Mode_Title", "2. Installation Mode"),
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

            // Card 3: Modular Add-ons Selection
            Border modCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            modCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            modCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel modStack = new StackPanel();
            TextBlock lblModTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Modules_Title", "3. Module Selection (Add-ons / Plugins)"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblModTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            modStack.Children.Add(lblModTitle);

            TextBlock lblModSub = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Modules_Subtitle", "Choose which add-on modules to deploy to the destination. Toolbox is mandatory."),
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            lblModSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            modStack.Children.Add(lblModSub);

            WrapPanel wpModules = new WrapPanel { Orientation = Orientation.Horizontal };
            var addonCheckboxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

            CheckBox chkToolbox = new CheckBox
            {
                Content = LocalizationHelper.Get("Module_Toolbox_Required", "Toolbox (Mandatory Core Utility)"),
                IsChecked = true,
                IsEnabled = false,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 16, 6)
            };
            chkToolbox.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpModules.Children.Add(chkToolbox);
            addonCheckboxes["Toolbox"] = chkToolbox;

            string[] knownAddons = new string[] { "AppManager", "SystemCleaner", "StartupManager", "MaintenanceReport" };
            foreach (var mod in knownAddons)
            {
                CheckBox chk = new CheckBox
                {
                    Content = GetFriendlyModuleName(mod),
                    Tag = mod,
                    IsChecked = true,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 16, 6),
                    Cursor = Cursors.Hand
                };
                chk.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                wpModules.Children.Add(chk);
                addonCheckboxes[mod] = chk;
            }

            try
            {
                string addonsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons");
                if (Directory.Exists(addonsDir))
                {
                    foreach (var dir in Directory.GetDirectories(addonsDir))
                    {
                        string name = Path.GetFileName(dir);
                        if (!addonCheckboxes.ContainsKey(name))
                        {
                            CheckBox chk = new CheckBox
                            {
                                Content = name,
                                Tag = name,
                                IsChecked = true,
                                FontSize = 11,
                                Margin = new Thickness(0, 2, 16, 6),
                                Cursor = Cursors.Hand
                            };
                            chk.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                            wpModules.Children.Add(chk);
                            addonCheckboxes[name] = chk;
                        }
                    }
                }
            }
            catch { }

            modStack.Children.Add(wpModules);
            modCard.Child = modStack;
            bodyStack.Children.Add(modCard);

            // Card 4: Target Directory & Options
            Border dirCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 4)
            };
            dirCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            dirCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel dirStack = new StackPanel();
            TextBlock lblDirTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Dir_Title", "4. Target Directory & Options"),
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
                Padding = new Thickness(18, 0, 18, 0),
                IsEnabled = false
            };
            btnInstallNow.SetResourceReference(Button.StyleProperty, "BtnAccent");
            chkAcceptLicense.Checked += (s, e) => { btnInstallNow.IsEnabled = true; };
            chkAcceptLicense.Unchecked += (s, e) => { btnInstallNow.IsEnabled = false; };

            cmbLang.SelectionChanged += (s, e) =>
            {
                ComboBoxItem cbi = cmbLang.SelectedItem as ComboBoxItem;
                if (cbi == null || cbi.Tag == null) return;
                string newLang = cbi.Tag.ToString();

                MainWindow mw = owner as MainWindow;
                if (mw != null)
                {
                    mw.SetLanguage(newLang);
                }
                else
                {
                    LocalizationHelper.LoadLanguage(newLang);
                    AppSettings st = SettingsHelper.Load();
                    st.Language = newLang;
                    SettingsHelper.Save(st);
                }

                // Refresh all strings in the installer dialog immediately
                win.Title = LocalizationHelper.Get("InstallWin_Title", "📥 Install OptiAssist");
                tbTitle.Text = LocalizationHelper.Get("InstallWin_Title", "📥 Install OptiAssist");
                tbSub.Text = LocalizationHelper.Get("InstallWin_Subtitle", "Review license terms, choose installation mode, and select target directory");
                lblLang.Text = LocalizationHelper.Get("InstallWin_Label_Language", "🌐 Language:");
                lblLicTitle.Text = LocalizationHelper.Get("InstallWin_License_Title", "1. License Terms & Conditions of Use (GNU GPLv3)");
                txtLicenseTerms.Text = GetFullTermsAndConditionsText();
                chkAcceptLicense.Content = LocalizationHelper.Get("InstallWin_Chk_AcceptLicense", "I have read and agree to the License Terms (GNU GPLv3) and Conditions of Use");
                lblModeTitle.Text = LocalizationHelper.Get("InstallWin_Mode_Title", "2. Installation Mode");
                tbNormalHead.Text = LocalizationHelper.Get("InstallWin_Mode_Normal", "🖥️ Standard Installation (Recommended)");
                tbNormalDesc.Text = LocalizationHelper.Get("InstallWin_Mode_NormalDesc", "Installs OptiAssist on this computer, creates Desktop and Start Menu shortcuts, and registers an entry in Windows Control Panel (Programs and Features).");
                tbPortableHead.Text = LocalizationHelper.Get("InstallWin_Mode_Portable", "💾 Portable Installation (USB Flash Drive / External Drive)");
                tbPortableDesc.Text = LocalizationHelper.Get("InstallWin_Mode_PortableDesc", "Deploys a standalone portable copy of OptiAssist to any folder, partition, or USB drive without modifying the local Windows registry or Control Panel.");
                lblModTitle.Text = LocalizationHelper.Get("InstallWin_Modules_Title", "3. Module Selection (Add-ons / Plugins)");
                lblModSub.Text = LocalizationHelper.Get("InstallWin_Modules_Subtitle", "Choose which add-on modules to deploy to the destination. Toolbox is mandatory.");
                chkToolbox.Content = LocalizationHelper.Get("Module_Toolbox_Required", "Toolbox (Mandatory Core Utility)");
                foreach (var pair in addonCheckboxes)
                {
                    if (!pair.Key.Equals("Toolbox", StringComparison.OrdinalIgnoreCase))
                    {
                        pair.Value.Content = GetFriendlyModuleName(pair.Key);
                    }
                }
                lblDirTitle.Text = LocalizationHelper.Get("InstallWin_Dir_Title", "4. Target Directory & Options");
                btnBrowseDir.Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Browse...");
                chkShortcuts.Content = LocalizationHelper.Get("InstallWin_Chk_Shortcuts", "Create Desktop and Start Menu shortcuts & register in Control Panel");
                chkAutorunAfterInstall.Content = LocalizationHelper.Get("InstallWin_Chk_Autorun", "Create USB AutoRun (autorun.inf) on destination drive root");
                btnCreateAutorun.Content = LocalizationHelper.Get("Btn_CreateAutorun", "💿 Create USB AutoRun");
                btnCreateAutorun.ToolTip = LocalizationHelper.Get("Tooltip_CreateAutorun", "Create autorun.inf on drive root to display application icon and AutoPlay entry");
                btnCancel.Content = LocalizationHelper.Get("Startup_Btn_Cancel", "Cancel");
                btnInstallNow.Content = LocalizationHelper.Get("InstallWin_Btn_InstallNow", "📥 Install Now");
            };

            btnInstallNow.Click += async (s, e) =>
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

                var selectedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                selectedMods.Add("Toolbox");
                foreach (var pair in addonCheckboxes)
                {
                    if (pair.Value.IsChecked == true)
                    {
                        selectedMods.Add(pair.Key);
                    }
                }

                btnInstallNow.IsEnabled = false;
                btnCancel.IsEnabled = false;
                btnCreateAutorun.IsEnabled = false;
                btnInstallNow.Content = LocalizationHelper.Get("InstallWin_Btn_Installing", "⏳ Installing...");
                Mouse.OverrideCursor = Cursors.Wait;

                bool installSuccess = false;
                string errorMessage = null;

                try
                {
                    installSuccess = await Task.Run(() =>
                    {
                        return ExecuteInstallationInternal(targetDir, isPortable, createShortcutsAndRegistry, createAutorun, selectedMods, log, out errorMessage);
                    });
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    installSuccess = false;
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                    btnInstallNow.IsEnabled = true;
                    btnCancel.IsEnabled = true;
                    btnCreateAutorun.IsEnabled = true;
                    btnInstallNow.Content = LocalizationHelper.Get("InstallWin_Btn_InstallNow", "📥 Install Now");
                }

                if (installSuccess)
                {
                    string normTarget = NormalizeDirectoryPath(targetDir);
                    string successMsg = isPortable
                        ? string.Format(LocalizationHelper.Get("Msg_PortableInstallSuccess", "OptiAssist portable installation completed successfully at:\n{0}"), normTarget)
                        : string.Format(LocalizationHelper.Get("Msg_InstallSuccess", "OptiAssist was successfully installed on this computer!\nLocation: {0}\nShortcuts have been created on your Desktop and Start Menu."), normTarget);

                    MessageBox.Show(win, successMsg, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    win.DialogResult = true;
                    win.Close();
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.Show(win, errorMessage, "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            return ExecuteInstallation(targetDir, isPortable, createShortcutsAndRegistry, createAutorun, null, log);
        }

        public static bool ExecuteInstallation(string targetDir, bool isPortable, bool createShortcutsAndRegistry, bool createAutorun, HashSet<string> selectedAddons, Action<string> log)
        {
            string errorMsg;
            bool result = ExecuteInstallationInternal(targetDir, isPortable, createShortcutsAndRegistry, createAutorun, selectedAddons, log, out errorMsg);
            if (!result && !string.IsNullOrEmpty(errorMsg))
            {
                MessageBox.Show(errorMsg, "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (result)
            {
                string normTarget = NormalizeDirectoryPath(targetDir);
                string successMsg = isPortable
                    ? string.Format(LocalizationHelper.Get("Msg_PortableInstallSuccess", "OptiAssist portable installation completed successfully at:\n{0}"), normTarget)
                    : string.Format(LocalizationHelper.Get("Msg_InstallSuccess", "OptiAssist was successfully installed on this computer!\nLocation: {0}\nShortcuts have been created on your Desktop and Start Menu."), normTarget);
                MessageBox.Show(successMsg, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return result;
        }

        private static bool ExecuteInstallationInternal(
            string targetDir,
            bool isPortable,
            bool createShortcutsAndRegistry,
            bool createAutorun,
            HashSet<string> selectedAddons,
            Action<string> log,
            out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string currentDir = NormalizeDirectoryPath(AppDomain.CurrentDomain.BaseDirectory);
                string normalizedTarget = NormalizeDirectoryPath(targetDir);

                if (string.IsNullOrEmpty(normalizedTarget))
                {
                    errorMessage = LocalizationHelper.Get("InstallWin_Msg_DirRequired", "Please specify a valid destination directory.");
                    return false;
                }

                if (string.Equals(normalizedTarget, currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = LocalizationHelper.Get("Msg_AlreadyInstalled", "OptiAssist is already running from the selected directory:\n" + normalizedTarget);
                    return false;
                }

                if (selectedAddons == null)
                {
                    selectedAddons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Toolbox", "AppManager", "SystemCleaner", "StartupManager", "MaintenanceReport"
                    };
                }
                selectedAddons.Add("Toolbox");

                if (log != null) log(string.Format("[INSTALL] Starting {0} installation from '{1}' to '{2}'", isPortable ? "Portable" : "Standard", currentDir, normalizedTarget));

                if (!Directory.Exists(normalizedTarget))
                {
                    Directory.CreateDirectory(normalizedTarget);
                }

                // Deploy application payload
                DeployApplicationFiles(currentDir, normalizedTarget, selectedAddons, log);

                // Configure target settings.json (freshstart: "false")
                try
                {
                    string targetConfigDir = Path.Combine(normalizedTarget, "Config");
                    if (!Directory.Exists(targetConfigDir)) Directory.CreateDirectory(targetConfigDir);
                    string targetSettingsFile = Path.Combine(targetConfigDir, "settings.json");

                    if (File.Exists(targetSettingsFile))
                    {
                        string txt = File.ReadAllText(targetSettingsFile, Encoding.UTF8);
                        if (txt.Contains("\"freshstart\""))
                        {
                            txt = System.Text.RegularExpressions.Regex.Replace(txt, "\"freshstart\"\\s*:\\s*\"[^\"]*\"", "\"freshstart\": \"false\"");
                        }
                        else
                        {
                            txt = txt.Replace("\"DisabledAddons\":", "\"freshstart\": \"false\",\r\n  \"DisabledAddons\":");
                        }
                        File.WriteAllText(targetSettingsFile, txt, Encoding.UTF8);
                    }
                    else
                    {
                        AppSettings newSt = SettingsHelper.Load();
                        newSt.FreshStart = "false";
                        SettingsHelper.Save(newSt, targetSettingsFile);
                    }
                }
                catch (Exception exSettings)
                {
                    if (log != null) log("[INSTALL WARNING] Could not update target settings: " + exSettings.Message);
                }

                string targetExe = Path.Combine(normalizedTarget, "OptiAssist.exe");

                if (createShortcutsAndRegistry)
                {
                    try
                    {
                        // Start Menu Shortcut
                        string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                        string startMenuLnk = Path.Combine(startMenuDir, "OptiAssist.lnk");
                        CreateShortcut(startMenuLnk, targetExe, normalizedTarget, "OptiAssist - System Optimization and Maintenance Assistant");

                        // Desktop Shortcut
                        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                        string desktopLnk = Path.Combine(desktopDir, "OptiAssist.lnk");
                        CreateShortcut(desktopLnk, targetExe, normalizedTarget, "OptiAssist - System Optimization and Maintenance Assistant");

                        // Registry Registration in Control Panel (Uninstall)
                        RegisterInUninstall(normalizedTarget, targetExe);
                    }
                    catch (Exception exSc)
                    {
                        if (log != null) log("[INSTALL WARNING] Could not create all shortcuts/registry: " + exSc.Message);
                    }
                }

                if (createAutorun)
                {
                    CreateUsbAutorun(normalizedTarget, log, false);
                }

                if (log != null) log("[INSTALL] OptiAssist successfully deployed to " + normalizedTarget);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("[INSTALL ERROR] " + ex.Message);
                errorMessage = string.Format(LocalizationHelper.Get("Msg_InstallError", "Failed to install OptiAssist: {0}"), ex.Message);
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

        private static void DeployApplicationFiles(string sourceDir, string targetDir, HashSet<string> selectedAddons, Action<string> log)
        {
            // 1. Root application files
            string[] rootFilesToCopy = new string[]
            {
                "OptiAssist.exe",
                "OptiAssist.exe.config",
                "app.ico",
                "app.png"
            };

            foreach (string fileName in rootFilesToCopy)
            {
                string srcFile = Path.Combine(sourceDir, fileName);
                if (File.Exists(srcFile))
                {
                    string dstFile = Path.Combine(targetDir, fileName);
                    try
                    {
                        File.Copy(srcFile, dstFile, true);
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log(string.Format("[INSTALL WARNING] Could not copy {0}: {1}", fileName, ex.Message));
                    }
                }
            }

            // Also copy any standalone root .dll files if any exist
            try
            {
                foreach (string file in Directory.GetFiles(sourceDir, "*.dll"))
                {
                    try
                    {
                        string fName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(targetDir, fName), true);
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Config directory
            string srcConfigDir = Path.Combine(sourceDir, "Config");
            string dstConfigDir = Path.Combine(targetDir, "Config");
            if (!Directory.Exists(dstConfigDir)) Directory.CreateDirectory(dstConfigDir);

            // Root Config files
            string[] configFiles = new string[] { "custom_shortcuts.json", "settings.json" };
            foreach (string cName in configFiles)
            {
                string srcC = Path.Combine(srcConfigDir, cName);
                if (File.Exists(srcC))
                {
                    try { File.Copy(srcC, Path.Combine(dstConfigDir, cName), true); } catch { }
                }
            }

            // Languages folder
            string srcLangDir = Path.Combine(srcConfigDir, "Languages");
            string dstLangDir = Path.Combine(dstConfigDir, "Languages");
            if (Directory.Exists(srcLangDir))
            {
                if (!Directory.Exists(dstLangDir)) Directory.CreateDirectory(dstLangDir);
                try
                {
                    foreach (string lFile in Directory.GetFiles(srcLangDir, "*.json"))
                    {
                        try
                        {
                            File.Copy(lFile, Path.Combine(dstLangDir, Path.GetFileName(lFile)), true);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Addons folder
            string srcAddonsDir = Path.Combine(srcConfigDir, "Addons");
            string dstAddonsDir = Path.Combine(dstConfigDir, "Addons");
            if (Directory.Exists(srcAddonsDir))
            {
                if (!Directory.Exists(dstAddonsDir)) Directory.CreateDirectory(dstAddonsDir);

                string srcAddonsReadme = Path.Combine(srcAddonsDir, "README.md");
                if (File.Exists(srcAddonsReadme))
                {
                    try { File.Copy(srcAddonsReadme, Path.Combine(dstAddonsDir, "README.md"), true); } catch { }
                }

                try
                {
                    foreach (string addonPath in Directory.GetDirectories(srcAddonsDir))
                    {
                        string addonName = Path.GetFileName(addonPath);
                        bool isToolbox = addonName.Equals("Toolbox", StringComparison.OrdinalIgnoreCase);

                        if (!isToolbox && selectedAddons != null && !selectedAddons.Contains(addonName))
                        {
                            if (log != null) log(string.Format("[INSTALL] Skipping unselected module: {0}", addonName));
                            continue;
                        }

                        string dstAddonPath = Path.Combine(dstAddonsDir, addonName);
                        CopyDirectoryRecursiveSafe(addonPath, dstAddonPath, targetDir, log);
                    }
                }
                catch { }
            }

            // 3. Auxiliary folders (Installers, Maintenance, Reports)
            string[] auxFolders = new string[] { "Installers", "Maintenance", "Reports" };
            foreach (string aux in auxFolders)
            {
                string dstAux = Path.Combine(targetDir, aux);
                if (!Directory.Exists(dstAux)) Directory.CreateDirectory(dstAux);

                string srcReadme = Path.Combine(sourceDir, aux, "README.txt");
                if (File.Exists(srcReadme))
                {
                    try { File.Copy(srcReadme, Path.Combine(dstAux, "README.txt"), true); } catch { }
                }
            }
        }

        private static void CopyDirectoryRecursiveSafe(string sourceDir, string targetDir, string mainTargetRoot, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string normMainTarget = string.IsNullOrEmpty(mainTargetRoot) ? string.Empty : NormalizeDirectoryPath(mainTargetRoot);

                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    try
                    {
                        string fName = Path.GetFileName(file);
                        if (fName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                        string dest = Path.Combine(targetDir, fName);
                        File.Copy(file, dest, true);
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log(string.Format("[INSTALL WARNING] Could not copy file '{0}': {1}", file, ex.Message));
                    }
                }

                foreach (string sub in Directory.GetDirectories(sourceDir))
                {
                    try
                    {
                        string dirName = Path.GetFileName(sub);
                        if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Recovery", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("$WinREAgent", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string normSub = NormalizeDirectoryPath(sub);
                        if (!string.IsNullOrEmpty(normMainTarget) && normSub.StartsWith(normMainTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string destSub = Path.Combine(targetDir, dirName);
                        CopyDirectoryRecursiveSafe(sub, destSub, mainTargetRoot, log);
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log(string.Format("[INSTALL WARNING] Could not process directory '{0}': {1}", sub, ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[INSTALL WARNING] Skipping folder '{0}': {1}", sourceDir, ex.Message));
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
                        key.SetValue("DisplayVersion", "1.2.S4");
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

        public static string GetFriendlyModuleName(string addonId)
        {
            if (string.Equals(addonId, "Toolbox", StringComparison.OrdinalIgnoreCase))
                return LocalizationHelper.Get("Nav_Toolbox", "Toolbox");
            if (string.Equals(addonId, "AppManager", StringComparison.OrdinalIgnoreCase))
                return LocalizationHelper.Get("Nav_AppManager", "App Manager");
            if (string.Equals(addonId, "SystemCleaner", StringComparison.OrdinalIgnoreCase))
                return LocalizationHelper.Get("Nav_SystemCleaner", "System Cleaner");
            if (string.Equals(addonId, "StartupManager", StringComparison.OrdinalIgnoreCase))
                return LocalizationHelper.Get("Nav_StartupManager", "Startup Manager");
            if (string.Equals(addonId, "MaintenanceReport", StringComparison.OrdinalIgnoreCase))
                return LocalizationHelper.Get("Nav_MaintenanceReport", "Maintenance Report");
            return addonId;
        }

        public static bool ShowWelcomeWizard(Window owner, AppSettings settings, Action<string> log = null)
        {
            string curLang = settings != null && !string.IsNullOrEmpty(settings.Language) ? settings.Language : LocalizationHelper.CurrentLanguage;
            LocalizationHelper.LoadLanguage(curLang);

            Window win = new Window
            {
                Title = LocalizationHelper.Get("Welcome_Title", "Welcome to OptiAssist"),
                Width = 700,
                Height = 670,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            win.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Grid root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body Scroll
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Buttons

            // Header (Title, Subtitle, Language ComboBox)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock tbTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Title", "Welcome to OptiAssist"),
                FontSize = 17,
                FontWeight = FontWeights.Bold
            };
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbSub = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Subtitle", "Modular System Maintenance, Diagnostic, and Post-Install Assistant"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            tbSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            headerStack.Children.Add(tbTitle);
            headerStack.Children.Add(tbSub);
            Grid.SetColumn(headerStack, 0);
            headerGrid.Children.Add(headerStack);

            StackPanel langPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            TextBlock lblLang = new TextBlock
            {
                Text = LocalizationHelper.Get("InstallWin_Label_Language", "🌐 Language:"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            lblLang.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            langPanel.Children.Add(lblLang);

            ComboBox cmbLang = new ComboBox
            {
                Width = 150,
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var availableLangs = LocalizationHelper.GetAvailableLanguageItems();
            int selIdx = 0;
            for (int i = 0; i < availableLangs.Count; i++)
            {
                var item = availableLangs[i];
                cmbLang.Items.Add(new ComboBoxItem { Content = item.Name, Tag = item.Code });
                if (string.Equals(item.Code, curLang, StringComparison.OrdinalIgnoreCase))
                {
                    selIdx = i;
                }
            }
            if (cmbLang.Items.Count > 0) cmbLang.SelectedIndex = selIdx;
            langPanel.Children.Add(cmbLang);
            Grid.SetColumn(langPanel, 1);
            headerGrid.Children.Add(langPanel);

            Grid.SetRow(headerGrid, 0);
            root.Children.Add(headerGrid);

            // Body Scroll
            ScrollViewer bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel bodyStack = new StackPanel();

            // Card 1: Risk & Administrative Warning
            Border warnCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            warnCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            warnCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            StackPanel warnStack = new StackPanel();
            TextBlock lblWarnTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Notice_Title", "1. Safety Warning & System Operation Risks"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            lblWarnTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushWarning");
            warnStack.Children.Add(lblWarnTitle);
            TextBlock tbWarnText = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Notice_Text", "OptiAssist is a powerful toolkit designed for IT professionals and power users that performs deep system administrative operations (including system integrity checks, DISM/SFC repairs, system cleaning, and startup adjustments). Administrative commands modify low-level Windows settings. Ensure you review and understand actions prior to executing them."),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            tbWarnText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            warnStack.Children.Add(tbWarnText);
            warnCard.Child = warnStack;
            bodyStack.Children.Add(warnCard);

            // Card 2: License Terms & Conditions of Use (GNU GPLv3)
            Border licCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            licCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            licCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            StackPanel licStack = new StackPanel();
            TextBlock lblLicTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_License_Title", "2. Terms of Use & Software License (GNU GPLv3)"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            lblLicTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            licStack.Children.Add(lblLicTitle);

            string fullTerms = GetFullTermsAndConditionsText();

            TextBox txtTerms = new TextBox
            {
                Text = fullTerms,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 95,
                FontFamily = new FontFamily("Consolas, Lucida Console, Segoe UI"),
                FontSize = 10,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            txtTerms.SetResourceReference(TextBox.BackgroundProperty, "BrushBgPrimary");
            txtTerms.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            txtTerms.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            licStack.Children.Add(txtTerms);

            CheckBox chkAccept = new CheckBox
            {
                Content = LocalizationHelper.Get("Welcome_License_Chk", "I have read and agree to the Terms of Use and Software License (GNU GPLv3)"),
                IsChecked = false,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            chkAccept.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            licStack.Children.Add(chkAccept);
            licCard.Child = licStack;
            bodyStack.Children.Add(licCard);

            // Card 3: Execution / Installation Mode
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
                Text = LocalizationHelper.Get("Welcome_Mode_Title", "3. How would you like to use OptiAssist?"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            lblModeTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            modeStack.Children.Add(lblModeTitle);

            RadioButton rbRunAsIs = new RadioButton { IsChecked = true, Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand };
            rbRunAsIs.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");
            StackPanel spRunAsIs = new StackPanel();
            TextBlock tbHeadRun = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_PortableRun", "Simply run as-is (Standalone Portable Mode)"), FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
            tbHeadRun.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbDescRun = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_PortableRunDesc", "Runs the application directly from the current folder without moving files or registering system shortcuts."), FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            tbDescRun.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spRunAsIs.Children.Add(tbHeadRun);
            spRunAsIs.Children.Add(tbDescRun);
            rbRunAsIs.Content = spRunAsIs;
            modeStack.Children.Add(rbRunAsIs);

            RadioButton rbInstallPC = new RadioButton { IsChecked = false, Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand };
            rbInstallPC.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");
            StackPanel spInstallPC = new StackPanel();
            TextBlock tbHeadPC = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_InstallPC", "Install on this PC (Recommended for daily use)"), FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
            tbHeadPC.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbDescPC = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_InstallPCDesc", "Installs OptiAssist to local drive (C:\\Program Files\\OptiAssist), creates Desktop and Start Menu shortcuts, and registers in Control Panel."), FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            tbDescPC.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spInstallPC.Children.Add(tbHeadPC);
            spInstallPC.Children.Add(tbDescPC);
            rbInstallPC.Content = spInstallPC;
            modeStack.Children.Add(rbInstallPC);

            RadioButton rbInstallUsb = new RadioButton { IsChecked = false, Margin = new Thickness(0, 0, 0, 4), Cursor = Cursors.Hand };
            rbInstallUsb.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");
            StackPanel spInstallUsb = new StackPanel();
            TextBlock tbHeadUsb = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_Usb", "Install to USB drive / External media"), FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
            tbHeadUsb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            TextBlock tbDescUsb = new TextBlock { Text = LocalizationHelper.Get("Welcome_Mode_UsbDesc", "Deploys a custom standalone copy to any chosen USB drive or partition."), FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            tbDescUsb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spInstallUsb.Children.Add(tbHeadUsb);
            spInstallUsb.Children.Add(tbDescUsb);
            rbInstallUsb.Content = spInstallUsb;
            modeStack.Children.Add(rbInstallUsb);

            // Sub-panel for Target Path (visible when USB is selected)
            StackPanel spTargetDir = new StackPanel { Margin = new Thickness(24, 6, 0, 0), Visibility = Visibility.Collapsed };
            Grid gdDir = new Grid();
            gdDir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gdDir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBox txtUsbDir = new TextBox { Height = 28, FontSize = 11, Padding = new Thickness(6, 2, 6, 2), VerticalContentAlignment = VerticalAlignment.Center };
            txtUsbDir.SetResourceReference(TextBox.BackgroundProperty, "BrushInputBg");
            txtUsbDir.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            txtUsbDir.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            Grid.SetColumn(txtUsbDir, 0);
            gdDir.Children.Add(txtUsbDir);
            Button btnBrowseUsb = new Button { Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Browse..."), Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(6, 0, 0, 0) };
            btnBrowseUsb.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnBrowseUsb.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = LocalizationHelper.Get("InstallWin_Browse_Desc", "Select destination directory or drive:");
                    fbd.SelectedPath = txtUsbDir.Text;
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string sel = fbd.SelectedPath;
                        if (!sel.EndsWith("OptiAssist", StringComparison.OrdinalIgnoreCase)) sel = Path.Combine(sel, "OptiAssist");
                        txtUsbDir.Text = sel;
                    }
                }
            };
            Grid.SetColumn(btnBrowseUsb, 1);
            gdDir.Children.Add(btnBrowseUsb);
            spTargetDir.Children.Add(gdDir);
            modeStack.Children.Add(spTargetDir);

            rbRunAsIs.Checked += (s, e) => { spTargetDir.Visibility = Visibility.Collapsed; };
            rbInstallPC.Checked += (s, e) => { spTargetDir.Visibility = Visibility.Collapsed; };
            rbInstallUsb.Checked += (s, e) =>
            {
                spTargetDir.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(txtUsbDir.Text))
                {
                    txtUsbDir.Text = @"D:\OptiAssist";
                }
            };

            modeCard.Child = modeStack;
            bodyStack.Children.Add(modeCard);

            // Card 4: Module Selection (Add-ons / Plugins)
            Border modCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 4)
            };
            modCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            modCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            StackPanel modStack = new StackPanel();
            TextBlock lblModTitle = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Modules_Title", "4. Module Selection (Add-ons / Plugins)"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblModTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            modStack.Children.Add(lblModTitle);
            TextBlock lblModSub = new TextBlock
            {
                Text = LocalizationHelper.Get("Welcome_Modules_Subtitle", "Choose which add-on modules to include. The Toolbox module is mandatory as the system core."),
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            lblModSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            modStack.Children.Add(lblModSub);

            WrapPanel wpModules = new WrapPanel { Orientation = Orientation.Horizontal };
            var addonCheckboxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

            CheckBox chkToolbox = new CheckBox
            {
                Content = LocalizationHelper.Get("Module_Toolbox_Required", "Toolbox (Mandatory Core Utility)"),
                IsChecked = true,
                IsEnabled = false,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 16, 6)
            };
            chkToolbox.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpModules.Children.Add(chkToolbox);
            addonCheckboxes["Toolbox"] = chkToolbox;

            string[] knownAddons = new string[] { "AppManager", "SystemCleaner", "StartupManager", "MaintenanceReport" };
            foreach (var mod in knownAddons)
            {
                CheckBox chk = new CheckBox
                {
                    Content = GetFriendlyModuleName(mod),
                    Tag = mod,
                    IsChecked = true,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 16, 6),
                    Cursor = Cursors.Hand
                };
                chk.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                wpModules.Children.Add(chk);
                addonCheckboxes[mod] = chk;
            }

            try
            {
                string addonsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons");
                if (Directory.Exists(addonsDir))
                {
                    foreach (var dir in Directory.GetDirectories(addonsDir))
                    {
                        string name = Path.GetFileName(dir);
                        if (!addonCheckboxes.ContainsKey(name))
                        {
                            CheckBox chk = new CheckBox
                            {
                                Content = name,
                                Tag = name,
                                IsChecked = true,
                                FontSize = 11,
                                Margin = new Thickness(0, 2, 16, 6),
                                Cursor = Cursors.Hand
                            };
                            chk.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                            wpModules.Children.Add(chk);
                            addonCheckboxes[name] = chk;
                        }
                    }
                }
            }
            catch { }

            modStack.Children.Add(wpModules);
            modCard.Child = modStack;
            bodyStack.Children.Add(modCard);

            bodyScroll.Content = bodyStack;
            Grid.SetRow(bodyScroll, 1);
            root.Children.Add(bodyScroll);

            // Footer
            Grid footerGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button btnDecline = new Button
            {
                Content = LocalizationHelper.Get("Welcome_Btn_Decline", "Decline & Exit"),
                Height = 32,
                Padding = new Thickness(16, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnDecline.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDecline.Click += (s, e) =>
            {
                win.DialogResult = false;
                win.Close();
                Application.Current.Shutdown();
            };
            Grid.SetColumn(btnDecline, 0);
            footerGrid.Children.Add(btnDecline);

            Button btnContinue = new Button
            {
                Content = LocalizationHelper.Get("Welcome_Btn_Continue", "Finish & Launch"),
                Height = 32,
                Padding = new Thickness(20, 0, 20, 0),
                IsEnabled = false
            };
            btnContinue.SetResourceReference(Button.StyleProperty, "BtnAccent");
            chkAccept.Checked += (s, e) => { btnContinue.IsEnabled = true; };
            chkAccept.Unchecked += (s, e) => { btnContinue.IsEnabled = false; };

            cmbLang.SelectionChanged += (s, e) =>
            {
                ComboBoxItem cbi = cmbLang.SelectedItem as ComboBoxItem;
                if (cbi == null || cbi.Tag == null) return;
                string newLang = cbi.Tag.ToString();
                LocalizationHelper.LoadLanguage(newLang);
                if (settings != null)
                {
                    settings.Language = newLang;
                    SettingsHelper.Save(settings);
                }
                MainWindow mw = owner as MainWindow;
                if (mw != null) mw.SetLanguage(newLang);

                win.Title = LocalizationHelper.Get("Welcome_Title", "Welcome to OptiAssist");
                tbTitle.Text = LocalizationHelper.Get("Welcome_Title", "Welcome to OptiAssist");
                tbSub.Text = LocalizationHelper.Get("Welcome_Subtitle", "Modular System Maintenance, Diagnostic, and Post-Install Assistant");
                lblLang.Text = LocalizationHelper.Get("InstallWin_Label_Language", "🌐 Language:");
                lblWarnTitle.Text = LocalizationHelper.Get("Welcome_Notice_Title", "1. Safety Warning & System Operation Risks");
                tbWarnText.Text = LocalizationHelper.Get("Welcome_Notice_Text", "OptiAssist is a powerful toolkit designed for IT professionals and power users that performs deep system administrative operations (including system integrity checks, DISM/SFC repairs, system cleaning, and startup adjustments). Administrative commands modify low-level Windows settings. Ensure you review and understand actions prior to executing them.");
                lblLicTitle.Text = LocalizationHelper.Get("Welcome_License_Title", "2. Terms of Use & Software License (GNU GPLv3)");
                txtTerms.Text = GetFullTermsAndConditionsText();
                chkAccept.Content = LocalizationHelper.Get("Welcome_License_Chk", "I have read and agree to the Terms of Use and Software License (GNU GPLv3)");
                lblModeTitle.Text = LocalizationHelper.Get("Welcome_Mode_Title", "3. How would you like to use OptiAssist?");
                tbHeadRun.Text = LocalizationHelper.Get("Welcome_Mode_PortableRun", "Simply run as-is (Standalone Portable Mode)");
                tbDescRun.Text = LocalizationHelper.Get("Welcome_Mode_PortableRunDesc", "Runs the application directly from the current folder without moving files or registering system shortcuts.");
                tbHeadPC.Text = LocalizationHelper.Get("Welcome_Mode_InstallPC", "Install on this PC (Recommended for daily use)");
                tbDescPC.Text = LocalizationHelper.Get("Welcome_Mode_InstallPCDesc", "Installs OptiAssist to local drive (C:\\Program Files\\OptiAssist), creates Desktop and Start Menu shortcuts, and registers in Control Panel.");
                tbHeadUsb.Text = LocalizationHelper.Get("Welcome_Mode_Usb", "Install to USB drive / External media");
                tbDescUsb.Text = LocalizationHelper.Get("Welcome_Mode_UsbDesc", "Deploys a custom standalone copy to any chosen USB drive or partition.");
                lblModTitle.Text = LocalizationHelper.Get("Welcome_Modules_Title", "4. Module Selection (Add-ons / Plugins)");
                lblModSub.Text = LocalizationHelper.Get("Welcome_Modules_Subtitle", "Choose which add-on modules to include. The Toolbox module is mandatory as the system core.");
                chkToolbox.Content = LocalizationHelper.Get("Module_Toolbox_Required", "Toolbox (Mandatory Core Utility)");
                foreach (var pair in addonCheckboxes)
                {
                    if (!pair.Key.Equals("Toolbox", StringComparison.OrdinalIgnoreCase))
                    {
                        pair.Value.Content = GetFriendlyModuleName(pair.Key);
                    }
                }
                btnBrowseUsb.Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Browse...");
                btnDecline.Content = LocalizationHelper.Get("Welcome_Btn_Decline", "Decline & Exit");
                btnContinue.Content = LocalizationHelper.Get("Welcome_Btn_Continue", "Finish & Launch");
            };

            bool success = false;
            btnContinue.Click += async (s, e) =>
            {
                if (chkAccept.IsChecked != true) return;

                var selectedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                selectedMods.Add("Toolbox");
                foreach (var pair in addonCheckboxes)
                {
                    if (pair.Value.IsChecked == true) selectedMods.Add(pair.Key);
                }

                if (rbRunAsIs.IsChecked == true)
                {
                    if (settings != null)
                    {
                        if (settings.DisabledAddons == null) settings.DisabledAddons = new List<string>();
                        foreach (var pair in addonCheckboxes)
                        {
                            if (pair.Value.IsChecked != true)
                            {
                                if (!settings.DisabledAddons.Contains(pair.Key)) settings.DisabledAddons.Add(pair.Key);
                            }
                            else
                            {
                                settings.DisabledAddons.Remove(pair.Key);
                            }
                        }
                        settings.FreshStart = "false";
                        SettingsHelper.Save(settings);
                    }
                    success = true;
                    win.DialogResult = true;
                    win.Close();
                }
                else if (rbInstallPC.IsChecked == true || rbInstallUsb.IsChecked == true)
                {
                    bool isPortable = (rbInstallUsb.IsChecked == true);
                    string targetDir = isPortable ? txtUsbDir.Text.Trim() : GetDefaultInstallDirectory();

                    if (string.IsNullOrWhiteSpace(targetDir))
                    {
                        MessageBox.Show(win,
                            LocalizationHelper.Get("InstallWin_Msg_DirRequired", "Please specify a valid destination directory."),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    btnContinue.IsEnabled = false;
                    btnDecline.IsEnabled = false;
                    btnContinue.Content = LocalizationHelper.Get("Welcome_Btn_Installing", "⏳ Installing...");
                    Mouse.OverrideCursor = Cursors.Wait;

                    bool installSuccess = false;
                    string errorMessage = null;

                    try
                    {
                        installSuccess = await Task.Run(() =>
                        {
                            return ExecuteInstallationInternal(targetDir, isPortable, !isPortable, isPortable, selectedMods, log, out errorMessage);
                        });
                    }
                    catch (Exception ex)
                    {
                        errorMessage = ex.Message;
                        installSuccess = false;
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                        btnContinue.IsEnabled = true;
                        btnDecline.IsEnabled = true;
                        btnContinue.Content = LocalizationHelper.Get("Welcome_Btn_Continue", "Finish & Launch");
                    }

                    if (installSuccess)
                    {
                        if (settings != null)
                        {
                            settings.FreshStart = "false";
                            SettingsHelper.Save(settings);
                        }

                        string normTarget = NormalizeDirectoryPath(targetDir);
                        string successMsg = isPortable
                            ? string.Format(LocalizationHelper.Get("Msg_PortableInstallSuccess", "OptiAssist portable installation completed successfully at:\n{0}"), normTarget)
                            : string.Format(LocalizationHelper.Get("Msg_InstallSuccess", "OptiAssist was successfully installed on this computer!\nLocation: {0}\nShortcuts have been created on your Desktop and Start Menu."), normTarget);

                        MessageBox.Show(win, successMsg, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                        success = true;
                        win.DialogResult = true;
                        win.Close();
                    }
                    else if (!string.IsNullOrEmpty(errorMessage))
                    {
                        MessageBox.Show(win, errorMessage, "OptiAssist - Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            };
            Grid.SetColumn(btnContinue, 1);
            footerGrid.Children.Add(btnContinue);

            Grid.SetRow(footerGrid, 2);
            root.Children.Add(footerGrid);

            win.Closing += (s, e) =>
            {
                if (!success && win.DialogResult != true)
                {
                    Application.Current.Shutdown();
                }
            };

            win.Content = root;
            bool? res = win.ShowDialog();
            return success || (res == true);
        }
    }
}
