using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Input;
using System.Text;

namespace OptiAssist
{
    public partial class MainWindow : Window, IOptiAssistHost
    {
        private AppSettings _settings;
        private HardwareSummary _hardware;
        private ObservableCollection<AddonEntry> _addons;
        private bool _isLoadingSettings = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadApplicationSettings();
            LoadAppLogo();
            UpdateDeploymentButtonsUI();
            ReloadAddonsList();

            if (_settings != null && _settings.AutoRefreshHardware)
            {
                RefreshHardwareAsync();
            }
        }

        private void UpdateStatusBadge(TextBlock tb, string value)
        {
            if (string.IsNullOrEmpty(value) || value == "Indisponível/não suportado" || value == "Unavailable" || value == "Unavailable/Not supported")
            {
                tb.Text = LocalizationHelper.Get("Status_NotSupported", "Unavailable/Not supported");
                tb.Foreground = (SolidColorBrush)FindResource("BrushTextSecondary");
                return;
            }

            if (value.StartsWith("Ativo", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("Active", StringComparison.OrdinalIgnoreCase))
            {
                tb.Text = LocalizationHelper.Get("Status_Active", "Active");
                tb.Foreground = (SolidColorBrush)FindResource("BrushSuccess");
            }
            else if (value.StartsWith("Inativo", StringComparison.OrdinalIgnoreCase) ||
                     value.StartsWith("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                tb.Text = LocalizationHelper.Get("Status_Inactive", "Inactive");
                tb.Foreground = (SolidColorBrush)FindResource("BrushDanger");
            }
            else if (value.StartsWith("Disponível", StringComparison.OrdinalIgnoreCase) ||
                     value.StartsWith("Available", StringComparison.OrdinalIgnoreCase))
            {
                string extra = "";
                int parenIdx = value.IndexOf('(');
                if (parenIdx >= 0) extra = " " + value.Substring(parenIdx);
                tb.Text = LocalizationHelper.Get("Status_Available", "Available") + extra;
                tb.Foreground = (SolidColorBrush)FindResource("BrushSuccess");
            }
            else
            {
                tb.Text = value;
                tb.Foreground = (SolidColorBrush)FindResource("BrushTextSecondary");
            }
        }

        #region TAB NAVIGATION
        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (borderSidebar == null || colSidebar == null) return;
            if (borderSidebar.Visibility == Visibility.Visible)
            {
                borderSidebar.Visibility = Visibility.Collapsed;
                colSidebar.Width = new GridLength(0);
            }
            else
            {
                colSidebar.Width = new GridLength(200);
                borderSidebar.Visibility = Visibility.Visible;
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb == null) return;

            // Hide all panels
            scrollViewerContent.Visibility = Visibility.Collapsed;
            panelDashboard.Visibility = Visibility.Collapsed;
            panelShortcuts.Visibility = Visibility.Collapsed;
            panelAddonHost.Visibility = Visibility.Collapsed;
            panelTerminal.Visibility = Visibility.Collapsed;
            panelSettings.Visibility = Visibility.Collapsed;
            panelAbout.Visibility = Visibility.Collapsed;

            // Display corresponding panel
            if (rb == rbNavDashboard)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelDashboard.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavShortcuts)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelShortcuts.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavTerminal)
            {
                panelTerminal.Visibility = Visibility.Visible;
                if (_settings != null && _settings.OpenExternalLogsByDefault)
                {
                    OpenExternalLogWindow();
                }
            }
            else if (rb == rbNavSettings)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelSettings.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavAbout)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelAbout.Visibility = Visibility.Visible;
            }
            else if (rb.Tag is AddonEntry)
            {
                AddonEntry entry = (AddonEntry)rb.Tag;
                scrollViewerContent.Visibility = Visibility.Visible;
                panelAddonHost.Visibility = Visibility.Visible;
                panelAddonHost.Children.Clear();
                UIElement view = entry.GetOrCreateView();
                if (view != null)
                {
                    panelAddonHost.Children.Add(view);
                }
            }
        }
        #endregion

        #region CONSOLE & TERMINAL LOGGING
        private LogWindow _externalLogWindow;

        public void OpenExternalLogWindow()
        {
            if (_externalLogWindow == null)
            {
                _externalLogWindow = new LogWindow(this);
                _externalLogWindow.SetLogText(txtConsoleLog != null ? txtConsoleLog.Text : "");
                _externalLogWindow.ApplyTheme(_isDarkMode);
                _externalLogWindow.Show();
            }
            else
            {
                if (_externalLogWindow.WindowState == WindowState.Minimized)
                {
                    _externalLogWindow.WindowState = WindowState.Normal;
                }
                _externalLogWindow.ApplyTheme(_isDarkMode);
                _externalLogWindow.Activate();
            }
        }

        public void OpenLogWindow()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(OpenLogWindow));
                return;
            }
            OpenExternalLogWindow();
        }

        public void OnExternalLogWindowClosed()
        {
            _externalLogWindow = null;
        }

        public void ClearMainLogConsole()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(ClearMainLogConsole));
                return;
            }
            if (txtConsoleLog != null)
            {
                txtConsoleLog.Clear();
            }
        }

        private void BtnPopoutLog_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalLogWindow();
        }

        public void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(() => Log(message)));
                return;
            }

            txtConsoleLog.AppendText(message + Environment.NewLine);
            txtConsoleLog.CaretIndex = txtConsoleLog.Text.Length;
            txtConsoleLog.ScrollToEnd();

            if (_externalLogWindow != null)
            {
                _externalLogWindow.AppendLog(message);
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtConsoleLog.Clear();
            if (_externalLogWindow != null)
            {
                _externalLogWindow.ClearLog();
            }
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtConsoleLog.Text))
            {
                Clipboard.SetText(txtConsoleLog.Text);
                MessageBox.Show(LocalizationHelper.Get("Msg_LogCopied", "Console log copied to clipboard!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtConsoleLog.Text))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_LogEmpty", "The terminal log is currently empty."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Text File (*.txt)|*.txt|All Files (*.*)|*.*";
                sfd.FileName = string.Format("OptiAssist_Log_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                if (sfd.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(sfd.FileName, txtConsoleLog.Text, System.Text.Encoding.UTF8);
                    MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_LogSaved", "Log successfully saved to:\n{0}"), sfd.FileName), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("[SAVED] Log exported to: " + sfd.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_LogSaveError", "Failed to save log: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region LOAD CONFIGURATION & APPLICATIONS
        private void LoadApplicationSettings()
        {
            _isLoadingSettings = true;
            try
            {
                _settings = SettingsHelper.Load();

                if (txtSettingStore != null) txtSettingStore.Text = _settings.CustomerName;
                if (chkSettingWinget != null) chkSettingWinget.IsChecked = _settings.EnableWingetFallback;
                if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.IsChecked = _settings.AutoRefreshHardware;
                if (chkSettingExternalLogs != null) chkSettingExternalLogs.IsChecked = _settings.OpenExternalLogsByDefault;

                string currentLang = string.IsNullOrEmpty(_settings.Language) ? "en-US" : _settings.Language;
                ApplyLanguage(currentLang);

                UpdateServiceProviderUI();
                LoadAppLogo();
                UpdateDeploymentButtonsUI();

                // Load and apply custom accent and button color
                string accent = string.IsNullOrEmpty(_settings.AccentColor) ? "#0EA5E9" : _settings.AccentColor;
                txtSettingAccentColor.Text = accent;
                ApplyThemeAccentColor(accent);

                // Load and apply theme (Dark / Light)
                if (!string.IsNullOrEmpty(_settings.Theme) && _settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyTheme(false);
                }
                else
                {
                    ApplyTheme(true);
                }
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void UpdateServiceProviderUI()
        {
            if (_settings == null) return;

            // Update top bar application branding
            if (lblAppTitle != null)
            {
                string customName = _settings.CustomerName;
                if (txtSettingStore != null && !string.IsNullOrWhiteSpace(txtSettingStore.Text))
                {
                    customName = txtSettingStore.Text.Trim();
                }

                if (!string.IsNullOrWhiteSpace(customName) && customName != "N/A" && !customName.Equals("OptiAssist", StringComparison.OrdinalIgnoreCase))
                {
                    lblAppTitle.Text = customName;
                }
                else
                {
                    lblAppTitle.Text = "OptiAssist";
                }
            }

            // Bottom status bar right label remains always visible and displays "OptiAssist"
            if (lblFooterStoreName != null)
            {
                lblFooterStoreName.Text = "OptiAssist";
                lblFooterStoreName.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region OVERVIEW (DASHBOARD)
        private void RefreshHardwareAsync()
        {
            lblHeaderDevice.Text = LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblHeaderCpu.Text = LocalizationHelper.Get("Status_Detecting", "Detecting processor...");
            lblHeaderRam.Text = LocalizationHelper.Get("Status_Detecting", "Detecting RAM...");

            lblOsTitle.Text = LocalizationHelper.Get("Status_Detecting", "Querying system info and WMI...");
            lblCpuDetails.Text = LocalizationHelper.Get("Label_Processor", "Processor: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblRamDetails.Text = LocalizationHelper.Get("Label_Ram", "RAM Memory: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblGpuDetails.Text = LocalizationHelper.Get("Label_Gpu", "Graphics Card: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblMotherboard.Text = LocalizationHelper.Get("Label_Motherboard", "Motherboard: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblBiosInfo.Text = LocalizationHelper.Get("Label_Bios", "BIOS: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblDiskDetails.Text = LocalizationHelper.Get("Label_Storage", "Storage: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            lblSerialNumber.Text = "...";

            txtBitLockerStatus.Text = LocalizationHelper.Get("Status_Detecting", "Checking...");
            txtBitLockerStatus.Foreground = (SolidColorBrush)FindResource("BrushWarning");
            txtTpmStatus.Text = LocalizationHelper.Get("Status_Detecting", "Checking...");
            txtTpmStatus.Foreground = (SolidColorBrush)FindResource("BrushWarning");
            txtSecureBootStatus.Text = LocalizationHelper.Get("Status_Detecting", "Checking...");
            txtSecureBootStatus.Foreground = (SolidColorBrush)FindResource("BrushWarning");

            ThreadPool.QueueUserWorkItem(state =>
            {
                HardwareSummary info = SystemInfoHelper.GetSystemInfo();
                _hardware = info;

                Dispatcher.Invoke(new Action(() =>
                {
                    UpdateHardwareDisplay(info);
                    Log(string.Format("[INFO] Hardware updated. Serial: {0} | Motherboard: {1}", info.SerialNumber, info.Motherboard));
                }));
            });
        }

        public void UpdateHardwareDisplay(HardwareSummary info)
        {
            if (info == null) return;

            // Update bottom status bar badges
            UpdateStatusBadge(txtBitLockerStatus, info.BitLockerStatus);
            UpdateStatusBadge(txtTpmStatus, info.TpmStatus);
            UpdateStatusBadge(txtSecureBootStatus, info.SecureBootStatus);

            // Clean Header Bar: Motherboard on line 1, CPU and RAM on line 2
            lblHeaderDevice.Text = info.Motherboard;
            lblHeaderDevice.ToolTip = string.Format("{0}: {1}\n{2}: {3}\n{4}: {5}",
                LocalizationHelper.Get("Label_Motherboard", "Motherboard: ").TrimEnd(':', ' '), info.Motherboard,
                LocalizationHelper.Get("Label_Bios", "BIOS: ").TrimEnd(':', ' '), info.BiosInfo,
                LocalizationHelper.Get("Label_SerialNumber", "Serial Number: ").TrimEnd(':', ' '), info.SerialNumber);

            lblHeaderCpu.Text = info.CpuName;
            lblHeaderCpu.ToolTip = string.Format("{0}: {1}\n{2} Cores / {3} Threads",
                LocalizationHelper.Get("Label_Processor", "Processor: ").TrimEnd(':', ' '), info.CpuName, info.CpuCores, info.CpuThreads);

            lblHeaderRam.Text = string.Format("{0} GB RAM", info.RamTotalGb);
            lblHeaderRam.ToolTip = string.Format("{0}: {1} GB ({2} GB)",
                LocalizationHelper.Get("Label_Ram", "RAM Memory: ").TrimEnd(':', ' '), info.RamTotalGb, info.RamFreeGb);

            // Operating System Card (Right)
            lblOsTitle.Text = string.Format("{0} {1}", info.OsName, info.OsDisplayVersion);
            lblOsDetails.Text = string.Format("Build: {0} ({1})",
                info.OsBuild, info.OsArchitecture);
            lblMachineName.Text = info.ComputerName;

            // Hardware Information Card (Left - 1 per line)
            string sn = info.SerialNumber;
            if (string.IsNullOrEmpty(sn) || sn == "Not informed" || sn == "Não informado" || sn == "Not available" || sn == "...")
            {
                lblSerialNumber.Text = LocalizationHelper.Get("Status_NotInformed", "Not informed");
            }
            else
            {
                lblSerialNumber.Text = sn;
            }

            lblCpuDetails.Text = string.Format("{0}{1} ({2} Cores / {3} Threads)",
                LocalizationHelper.Get("Label_Processor", "Processor: "), info.CpuName, info.CpuCores, info.CpuThreads);
            lblRamDetails.Text = string.Format("{0}{1} GB Total ({2} GB)",
                LocalizationHelper.Get("Label_Ram", "RAM Memory: "), info.RamTotalGb, info.RamFreeGb);
            lblGpuDetails.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Gpu", "Graphics Card: "), info.GpuName);
            lblMotherboard.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Motherboard", "Motherboard: "), info.Motherboard);
            lblBiosInfo.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Bios", "BIOS: "), info.BiosInfo);
            lblDiskDetails.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Storage", "Storage: "), info.PrimaryDiskInfo);
        }

        private void BtnCopySerial_Click(object sender, RoutedEventArgs e)
        {
            string sn = lblSerialNumber.Text;
            if (!string.IsNullOrEmpty(sn) && sn != "..." && sn != "Não informado" && sn != "Not informed" && sn != "Not available")
            {
                Clipboard.SetText(sn);
                MessageBox.Show(LocalizationHelper.Get("Msg_SerialCopied", "Serial number copied to clipboard!") + "\n\n" + sn, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                Log(string.Format("[CLIPBOARD] Serial number '{0}' copied to the clipboard.", sn));
            }
            else
            {
                MessageBox.Show(LocalizationHelper.Get("Status_NotDetected", "No valid serial number was detected on this equipment."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRebootBios_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBox.Show(
                LocalizationHelper.Get("Msg_RebootUefiConfirm", "Are you sure you want to reboot directly into the BIOS / UEFI menu?\n\nMake sure to save all open work before continuing."),
                "OptiAssist - BIOS / UEFI",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                string error;
                bool ok = SystemInfoHelper.RebootToBios(out error);
                if (!ok)
                {
                    MessageBox.Show(error, "OptiAssist - Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Log("[BIOS WARNING] " + error);
                }
            }
        }

        private void BtnRunPostInstallTask_Click(object sender, RoutedEventArgs e)
        {
            if (!NavigateToAddon("app-manager"))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_AppManagerNotAvailable", "The App Manager module is currently disabled or not installed."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_addons != null)
            {
                foreach (AddonEntry entry in _addons)
                {
                    if (entry != null && string.Equals(entry.Id, "app-manager", StringComparison.OrdinalIgnoreCase) && entry.Instance != null)
                    {
                        try
                        {
                            var method = entry.Instance.GetType().GetMethod("RunAutomatedPostInstall");
                            if (method != null)
                            {
                                method.Invoke(entry.Instance, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("[APP MANAGER ERROR] " + ex.Message);
                        }
                        break;
                    }
                }
            }
        }

        private void BtnTaskPostInstall_Click(object sender, RoutedEventArgs e)
        {
            if (!NavigateToAddon("app-manager"))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_AppManagerNotAvailable", "The App Manager module is currently disabled or not installed."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region OPTIMIZATION & MAINTENANCE
        private void RunAsyncTweak(Action action)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log("[UNEXPECTED ERROR] " + ex.Message);
                }
            });
        }

        private void BtnChrisTitus_Click(object sender, RoutedEventArgs e)
        {
            Log("[TOOL] Launching Chris Titus Tool (irm https://christitus.com/win | iex)...");
            ShortcutsHelper.OpenChrisTitusTool();
        }

        private void BtnWinSatBenchmark_Click(object sender, RoutedEventArgs e)
        {
            // Check if WinSAT scores already exist
            WinSatScores current = SystemInfoHelper.GetWinSatScores();
            if (current.HasData)
            {
                string infoMsg = string.Format(
                    "CURRENT BENCHMARK RESULTS (WinSAT):\n\n" +
                    "• Overall Score (WinSPR): {0:F1}\n" +
                    "• Processor (CPU):        {1:F1}\n" +
                    "• RAM Memory:             {2:F1}\n" +
                    "• Graphics / Games (D3D): {3:F1}\n" +
                    "• Desktop Graphics:       {4:F1}\n" +
                    "• Primary Disk:           {5:F1}\n\n" +
                    "Do you want to run a NEW full benchmark now? (May take several minutes)",
                    current.OverallScore, current.CpuScore, current.MemoryScore, current.D3DScore, current.GraphicsScore, current.DiskScore);

                MessageBoxResult res = MessageBox.Show(infoMsg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res != MessageBoxResult.Yes)
                {
                    Log(string.Format("[WINSAT] Scores: Overall {0:F1} | CPU {1:F1} | RAM {2:F1} | Disk {3:F1} | D3D {4:F1}",
                        current.OverallScore, current.CpuScore, current.MemoryScore, current.DiskScore, current.D3DScore));
                    return;
                }
            }

            RunAsyncTweak(() =>
            {
                SystemInfoHelper.RunWinSatBenchmark(Log);

                // Query and display new benchmark scores
                WinSatScores updated = SystemInfoHelper.GetWinSatScores();
                if (updated.HasData)
                {
                    Log("\n--- WINSAT BENCHMARK SCORES ---");
                    Log(string.Format("• Overall Score (WinSPR): {0:F1}", updated.OverallScore));
                    Log(string.Format("• Processor (CPU):        {0:F1}", updated.CpuScore));
                    Log(string.Format("• Memory (RAM):           {0:F1}", updated.MemoryScore));
                    Log(string.Format("• Graphics / Games (D3D): {0:F1}", updated.D3DScore));
                    Log(string.Format("• Primary Disk:           {0:F1}", updated.DiskScore));
                    Log("--------------------------------------\n");

                    Dispatcher.Invoke(new Action(() =>
                    {
                        MessageBox.Show(
                            string.Format(LocalizationHelper.Get("Msg_WinSatCompleted", "WinSAT Benchmark Completed Successfully!\n\nOverall Score: {0:F1}\nCPU: {1:F1} | RAM: {2:F1} | Disk: {3:F1}"),
                            updated.OverallScore, updated.CpuScore, updated.MemoryScore, updated.DiskScore),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    }));
                }
            });
        }

        private void BtnCleanTemp_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.CleanTempFiles(Log));
        }

        private void BtnCleanUpdate_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.CleanWindowsUpdateCache(Log));
        }

        private void BtnVisualFX_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.OptimizeVisualEffects(Log));
        }

        private void BtnTelemetry_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.OptimizeTelemetryAndServices(Log));
        }

        private void BtnCompactOs_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.CompactOsInstall(Log));
        }

        private void BtnSfcRepair_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.RunSfcRepair(Log));
        }

        private void BtnDismRepair_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.RunDismRepair(Log));
        }

        private void BtnDismSfc_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.RunDismAndSfc(Log));
        }

        private void BtnChkdsk_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.RunChkdsk(Log));
        }

        private void BtnFixPrinter_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.FixNetworkPrinterRpc(Log));
        }

        private void BtnDualBootClock_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.FixDualBootClock(Log));
        }

        private void BtnWsReset_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.RepairWindowsStore(Log));
        }

        private void BtnResetNetwork_Click(object sender, RoutedEventArgs e)
        {
            RunAsyncTweak(() => TweakHelper.ResetNetwork(Log));
        }

        private void BtnOpenScriptsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", TweakHelper.GetScriptsDirectory());
            }
            catch { }
        }
        #endregion

        #region WINDOWS TOOLS
        private void BtnDevMgmt_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("devmgmt.msc"); }
        private void BtnNetplwiz_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Legacy User Accounts (netplwiz)...");
            ShortcutsHelper.OpenNetplwiz();
        }
        private void BtnDiskMgmt_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("diskmgmt.msc"); }
        private void BtnControlPanel_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("control.exe"); }
        private void BtnMsInfo_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("msinfo32.exe"); }
        private void BtnWinver_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("winver.exe"); }
        private void BtnMsConfig_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening MSCONFIG tool (msconfig)...");
            ShortcutsHelper.OpenMsConfig();
        }
        private void BtnSysDm_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("sysdm.cpl"); }
        private void BtnNcpa_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("ncpa.cpl"); }
        private void BtnOptionalFeatures_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("optionalfeatures.exe"); }
        private void BtnRegedit_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("regedit.exe"); }
        private void BtnTaskMgr_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("taskmgr.exe"); }
        private void BtnResmon_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenWindowsTool("resmon.exe"); }
        private void BtnMemoryDiag_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Windows Memory Diagnostic (mdsched.exe)...");
            ShortcutsHelper.OpenMemoryDiagnostic();
        }
        private void BtnReliability_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Reliability Monitor (perfmon.exe /rel)...");
            ShortcutsHelper.OpenReliabilityMonitor();
        }
        private void BtnEventVwr_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Event Viewer (eventvwr.msc)...");
            ShortcutsHelper.OpenEventViewer();
        }
        private void BtnGpedit_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening GPEDIT tool (gpedit.msc)...");
            ShortcutsHelper.OpenGpedit();
        }

        private void BtnServices_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Windows Services (services.msc)...");
            ShortcutsHelper.OpenServices();
        }

        private void BtnAdminCmd_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenAdminCmd(); }
        private void BtnAdminPowerShell_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenAdminPowerShell(); }

        private void BtnOpenMaintenanceFolder_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenMaintenanceFolder(); }
        #endregion

        #region ADD-ONS & MODULES MANAGEMENT (IOptiAssistHost)
        public string GetLocalizedString(string key, string fallback)
        {
            return LocalizationHelper.Get(key, fallback);
        }

        public string CurrentLanguage
        {
            get { return _settings != null && !string.IsNullOrEmpty(_settings.Language) ? _settings.Language : "en-US"; }
        }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public HardwareSummary Hardware
        {
            get { return _hardware; }
        }

        public void SaveSettings()
        {
            if (_settings != null)
            {
                SettingsHelper.Save(_settings);
            }
        }

        private void ReloadAddonsList()
        {
            try
            {
                List<string> disabled = _settings != null && _settings.DisabledAddons != null ? _settings.DisabledAddons : new List<string>();
                List<AddonEntry> list = AddonHelper.LoadDiscoveredAddons(this, disabled);
                _addons = new ObservableCollection<AddonEntry>(list);
                if (icAddonsList != null) icAddonsList.ItemsSource = _addons;
                if (icAboutAddonsList != null) icAboutAddonsList.ItemsSource = _addons;

                RebuildSidebarAddonsMenu();
            }
            catch (Exception ex)
            {
                Log("[ADDONS RELOAD ERROR] " + ex.Message);
            }
        }

        private void RebuildSidebarAddonsMenu()
        {
            if (pnlSidebarAddons == null) return;

            string previouslySelectedAddonId = null;
            foreach (object child in pnlSidebarAddons.Children)
            {
                RadioButton existingRb = child as RadioButton;
                if (existingRb != null && existingRb.IsChecked == true && existingRb.Tag is AddonEntry)
                {
                    previouslySelectedAddonId = ((AddonEntry)existingRb.Tag).Id;
                    break;
                }
            }

            pnlSidebarAddons.Children.Clear();

            if (_addons == null) return;

            bool restoredActiveAddon = false;
            foreach (AddonEntry entry in _addons)
            {
                entry.ActiveLabel = LocalizationHelper.Get("Addons_Label_Active", "Active");
                entry.RemoveLabel = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");

                if (!entry.IsEnabled) continue;

                RadioButton rb = new RadioButton();
                rb.Style = (Style)FindResource("BtnNav");
                rb.GroupName = "SidebarNavGroup";
                rb.Tag = entry;
                rb.Content = string.Format("{0}  {1}", entry.Icon, entry.Name);
                rb.Click += NavButton_Click;

                entry.SidebarButton = rb;
                pnlSidebarAddons.Children.Add(rb);

                if (!string.IsNullOrEmpty(previouslySelectedAddonId) &&
                    string.Equals(entry.Id, previouslySelectedAddonId, StringComparison.OrdinalIgnoreCase))
                {
                    rb.IsChecked = true;
                    restoredActiveAddon = true;
                    if (panelAddonHost != null)
                    {
                        panelAddonHost.Children.Clear();
                        UIElement view = entry.GetOrCreateView();
                        if (view != null) panelAddonHost.Children.Add(view);
                    }
                }
            }

            // If an add-on was active in the main view and got disabled or removed, fall back to Settings or Dashboard
            if (!string.IsNullOrEmpty(previouslySelectedAddonId) && !restoredActiveAddon)
            {
                if (panelAddonHost != null)
                {
                    panelAddonHost.Children.Clear();
                    panelAddonHost.Visibility = Visibility.Collapsed;
                }
                if (rbNavSettings != null && panelSettings != null && panelSettings.Visibility == Visibility.Visible)
                {
                    rbNavSettings.IsChecked = true;
                }
                else if (rbNavDashboard != null)
                {
                    rbNavDashboard.IsChecked = true;
                    scrollViewerContent.Visibility = Visibility.Visible;
                    panelDashboard.Visibility = Visibility.Visible;
                }
            }
        }

        public bool NavigateToAddon(string addonId)
        {
            if (_addons == null) return false;
            foreach (AddonEntry entry in _addons)
            {
                if (entry != null && string.Equals(entry.Id, addonId, StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.SidebarButton != null)
                    {
                        entry.SidebarButton.IsChecked = true;
                        NavButton_Click(entry.SidebarButton, new RoutedEventArgs());
                        return true;
                    }
                }
            }
            return false;
        }

        private void BtnTaskToolbox_Click(object sender, RoutedEventArgs e)
        {
            if (!NavigateToAddon("toolbox"))
            {
                Log("[Toolbox] Module not loaded or disabled in Settings.");
            }
        }

        private void BtnAddAddon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Title = LocalizationHelper.Get("Btn_AddAddon", "➕ Add Module (.dll)");
                dlg.Filter = "OptiAssist Module DLL (*.dll)|*.dll";
                dlg.CheckFileExists = true;

                if (dlg.ShowDialog() == true)
                {
                    string installedAddonName;
                    string errorMsg;
                    bool ok = AddonHelper.InstallAddonFromDll(dlg.FileName, this, out installedAddonName, out errorMsg);
                    if (ok)
                    {
                        Log("[ADDON] Installed module from: " + dlg.FileName);
                        ReloadAddonsList();
                        MessageBox.Show(
                            LocalizationHelper.Get("Msg_AddonAdded", "Module (.dll) successfully added and integrated into OptiAssist!"),
                            "OptiAssist",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Log("[ADDON ERROR] Failed to install module: " + errorMsg);
                        MessageBox.Show(
                            string.Format(LocalizationHelper.Get("Msg_AddonAddError", "Could not load or install the selected DLL module:\n{0}"), errorMsg),
                            "OptiAssist",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("[ADDON ADD ERROR] " + ex.Message);
            }
        }

        private void BtnRemoveAddon_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            AddonEntry entry = btn != null ? btn.DataContext as AddonEntry : null;
            if (entry == null) return;

            string confirmMsg = string.Format(
                LocalizationHelper.Get("Msg_RemoveAddonConfirm", "Are you sure you want to remove the module '{0}' ({1})?\n\nThis will delete its DLL from Config\\Addons and remove it from the sidebar."),
                entry.Name,
                entry.DisplayPath);

            MessageBoxResult res = MessageBox.Show(confirmMsg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            string errorMsg;
            if (AddonHelper.RemoveAddon(entry, out errorMsg))
            {
                if (_settings != null && _settings.DisabledAddons != null && _settings.DisabledAddons.Contains(entry.Id))
                {
                    _settings.DisabledAddons.Remove(entry.Id);
                    SettingsHelper.Save(_settings);
                }

                Log(string.Format("[ADDON] Removed module '{0}' ({1}).", entry.Name, entry.Id));
                ReloadAddonsList();

                MessageBox.Show(
                    LocalizationHelper.Get("Msg_AddonRemoved", "Module removed successfully."),
                    "OptiAssist",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                Log("[ADDON REMOVE ERROR] " + errorMsg);
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_AddonRemoveError", "Failed to remove module:\n{0}"), errorMsg),
                    "OptiAssist",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnOpenAddonsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = AddonHelper.GetAddonsDirectory();
                Process.Start("explorer.exe", dir);
                Log("[ADDON] Opening Add-ons directory: " + dir);
            }
            catch (Exception ex)
            {
                Log("[ADDON ERROR] Could not open directory: " + ex.Message);
            }
        }

        private void BtnReloadAddons_Click(object sender, RoutedEventArgs e)
        {
            ReloadAddonsList();
            Log("[ADDON] Add-ons list and sidebar modules reloaded.");
        }

        private void AddonEnableCheckbox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            AddonEntry entry = cb != null ? cb.DataContext as AddonEntry : null;
            if (entry != null && _settings != null)
            {
                if (!entry.CanModify)
                {
                    cb.IsChecked = true;
                    entry.IsEnabled = true;
                    return;
                }
                if (_settings.DisabledAddons == null) _settings.DisabledAddons = new List<string>();
                if (cb.IsChecked == true)
                {
                    entry.IsEnabled = true;
                    if (_settings.DisabledAddons.Contains(entry.Id))
                    {
                        _settings.DisabledAddons.Remove(entry.Id);
                    }
                }
                else
                {
                    entry.IsEnabled = false;
                    if (!_settings.DisabledAddons.Contains(entry.Id))
                    {
                        _settings.DisabledAddons.Add(entry.Id);
                    }
                }
                SettingsHelper.Save(_settings);
                RebuildSidebarAddonsMenu();
                Log(string.Format("[ADDON] Module '{0}' ({1}) is now {2}.", entry.Name, entry.Id, cb.IsChecked == true ? "Enabled" : "Disabled"));
            }
        }
        #endregion

        #region SETTINGS & LOCALIZATION
        private void BtnLangPt_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage("pt-BR");
        }

        private void BtnLangEn_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage("en-US");
        }

        public void SetLanguage(string langCode)
        {
            if (_settings == null) _settings = new AppSettings();
            _settings.Language = langCode;
            SettingsHelper.Save(_settings);
            ApplyLanguage(langCode);
            Log(string.Format("[LANGUAGE] Language changed to {0} and saved in Config\\settings.json.", langCode));
        }

        public void ApplyLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) langCode = "en-US";
            LocalizationHelper.LoadLanguage(langCode);

            // 0. Window Title & Header Controls
            this.Title = LocalizationHelper.Get("App_Title", "OptiAssist");
            if (btnToggleSidebar != null) btnToggleSidebar.ToolTip = LocalizationHelper.Get("Tooltip_ToggleSidebar", "Toggle Sidebar");

            // 1. Sidebar Navigation
            if (rbNavDashboard != null) rbNavDashboard.Content = LocalizationHelper.Get("Nav_Dashboard", "💻  Dashboard");
            if (rbNavShortcuts != null) rbNavShortcuts.Content = LocalizationHelper.Get("Nav_Shortcuts", "🛠️  Windows Tools");
            if (rbNavTerminal != null) rbNavTerminal.Content = LocalizationHelper.Get("Nav_Terminal", "⬛  Logs");
            if (rbNavSettings != null) rbNavSettings.Content = LocalizationHelper.Get("Nav_Settings", "⚙️  Settings");
            if (rbNavAbout != null) rbNavAbout.Content = LocalizationHelper.Get("Nav_About", "ℹ️  About");

            // 2. Overview (Dashboard)
            if (txtDashboardTitle != null) txtDashboardTitle.Text = LocalizationHelper.Get("Dashboard_Title", "Welcome to OptiAssist!");
            if (txtDashboardSubtitle != null) txtDashboardSubtitle.Text = LocalizationHelper.Get("Dashboard_Subtitle", "The ultimate tool to keep your system in order");
            if (txtHwCardTitle != null) txtHwCardTitle.Text = LocalizationHelper.Get("Card_Hardware_Title", "🧠 Hardware Information");
            if (txtOsCardTitle != null) txtOsCardTitle.Text = LocalizationHelper.Get("Card_Os_Title", "🖥️ Operating System");
            if (lblSerialTag != null) lblSerialTag.Text = LocalizationHelper.Get("Label_SerialNumber", "Serial Number: ");
            if (btnCopySerial != null)
            {
                btnCopySerial.Content = LocalizationHelper.Get("Btn_Copy", "📋 Copy");
                btnCopySerial.ToolTip = LocalizationHelper.Get("Tooltip_CopySerial", "Copy serial number to clipboard");
            }
            if (lblMachineNameTag != null) lblMachineNameTag.Text = LocalizationHelper.Get("Label_DeviceName", "Device Name:");
            if (txtCommonTasksTitle != null) txtCommonTasksTitle.Text = LocalizationHelper.Get("Card_CommonTasks_Title", "⚡ Common Tasks");

            if (btnRunPostInstallTask != null)
            {
                btnRunPostInstallTask.Content = LocalizationHelper.Get("Task_RunPostInstall", "🚀 Run Post-Install");
                btnRunPostInstallTask.ToolTip = LocalizationHelper.Get("Tooltip_TaskRunPostInstall", "Open App Manager and install all selected applications");
            }
            if (btnTaskPostInstall != null)
            {
                btnTaskPostInstall.Content = LocalizationHelper.Get("Task_PostInstall", "📦 App Manager");
                btnTaskPostInstall.ToolTip = LocalizationHelper.Get("Tooltip_TaskPostInstall", "Go to application and installer manager");
            }
            if (btnTaskMaintenanceFolder != null)
            {
                btnTaskMaintenanceFolder.Content = LocalizationHelper.Get("Task_MaintenanceFolder", "📁 Maintenance Folder");
                btnTaskMaintenanceFolder.ToolTip = LocalizationHelper.Get("Tooltip_TaskMaintenanceFolder", "Open portable maintenance tools folder");
            }
            if (btnTaskChrisTitus != null)
            {
                btnTaskChrisTitus.Content = LocalizationHelper.Get("Task_ChrisTitus", "🚀 Chris Titus WinUtil");
                btnTaskChrisTitus.ToolTip = LocalizationHelper.Get("Tooltip_TaskChrisTitus", "Run Chris Titus Tech utility (PowerShell)");
            }
            if (btnTaskMemoryDiag != null)
            {
                btnTaskMemoryDiag.Content = LocalizationHelper.Get("Task_MemoryDiag", "🧠 Memory Diagnostic");
                btnTaskMemoryDiag.ToolTip = LocalizationHelper.Get("Tooltip_TaskMemoryDiag", "Run Windows Memory Diagnostic tool");
            }
            if (btnTaskDevMgmt != null)
            {
                btnTaskDevMgmt.Content = LocalizationHelper.Get("Task_DevMgmt", "🖥️ Device Manager");
                btnTaskDevMgmt.ToolTip = LocalizationHelper.Get("Tooltip_TaskDevMgmt", "Open Windows Device Manager");
            }
            if (btnTaskSysDm != null)
            {
                btnTaskSysDm.Content = LocalizationHelper.Get("Task_SysDm", "⚙️ System Properties");
                btnTaskSysDm.ToolTip = LocalizationHelper.Get("Tooltip_TaskSysDm", "Open System Properties dialog");
            }
            if (btnRebootBios != null)
            {
                btnRebootBios.Content = LocalizationHelper.Get("Task_RebootBios", "⚡ Reboot to UEFI");
                btnRebootBios.ToolTip = LocalizationHelper.Get("Tooltip_TaskRebootBios", "Restart computer directly into BIOS / UEFI");
            }
            if (btnTaskToolbox != null)
            {
                btnTaskToolbox.Content = LocalizationHelper.Get("Task_Toolbox", "🧰 Toolbox");
                btnTaskToolbox.ToolTip = LocalizationHelper.Get("Tooltip_TaskToolbox", "Open Toolbox custom actions and tools");
            }

            // 5. Windows Administrative Tools
            if (txtToolsTitle != null) txtToolsTitle.Text = LocalizationHelper.Get("Tools_Title", "Windows Administrative Tools");
            if (txtToolsSubtitle != null) txtToolsSubtitle.Text = LocalizationHelper.Get("Tools_Subtitle", "Quick access to primary management consoles and native system utilities");
            if (txtAdminToolsTitle != null) txtAdminToolsTitle.Text = LocalizationHelper.Get("Tools_Section_Title", "🛠️ Administrative Tools");
            if (btnDevMgmtTool != null) btnDevMgmtTool.Content = LocalizationHelper.Get("Tool_DevMgmt", "🔌 Device Manager");
            if (btnNetplwiz != null) btnNetplwiz.Content = LocalizationHelper.Get("Tool_Netplwiz", "👤 User Accounts (Netplwiz)");
            if (btnDiskMgmt != null) btnDiskMgmt.Content = LocalizationHelper.Get("Tool_DiskMgmt", "💾 Disk Management");
            if (btnControl != null) btnControl.Content = LocalizationHelper.Get("Tool_Control", "⚙️ Classic Control Panel");
            if (btnMsinfo != null) btnMsinfo.Content = LocalizationHelper.Get("Tool_Msinfo", "ℹ️ System Information (msinfo32)");
            if (btnWinver != null) btnWinver.Content = LocalizationHelper.Get("Tool_Winver", "📋 About Windows (winver)");
            if (btnMsconfig != null) btnMsconfig.Content = LocalizationHelper.Get("Tool_Msconfig", "🔧 MSCONFIG Tool");
            if (btnSysDmTool != null) btnSysDmTool.Content = LocalizationHelper.Get("Tool_SysDm", "💻 System Properties");
            if (btnNcpa != null) btnNcpa.Content = LocalizationHelper.Get("Tool_Ncpa", "🌐 Network Connections (ncpa.cpl)");
            if (btnOptionalFeatures != null) btnOptionalFeatures.Content = LocalizationHelper.Get("Tool_OptionalFeatures", "📦 Windows Features");
            if (btnRegedit != null) btnRegedit.Content = LocalizationHelper.Get("Tool_Regedit", "🔑 Registry Editor (regedit)");
            if (btnTaskmgr != null) btnTaskmgr.Content = LocalizationHelper.Get("Tool_Taskmgr", "📊 Task Manager");
            if (btnResmon != null) btnResmon.Content = LocalizationHelper.Get("Tool_Resmon", "⚡ Resource Monitor");
            if (btnMemoryDiagTool != null) btnMemoryDiagTool.Content = LocalizationHelper.Get("Tool_MemoryDiag", "🧠 Memory Diagnostic");
            if (btnRelMon != null) btnRelMon.Content = LocalizationHelper.Get("Tool_Reliability", "📈 Reliability Monitor");
            if (btnEventVwr != null) btnEventVwr.Content = LocalizationHelper.Get("Tool_EventVwr", "📑 Event Viewer");
            if (txtBtnGpeditText != null) txtBtnGpeditText.Text = LocalizationHelper.Get("Tool_Gpedit", "📜 GPEDIT Tool (Pro/Ent)");
            if (btnGpedit != null) btnGpedit.ToolTip = LocalizationHelper.Get("Tooltip_Gpedit", "Group Policy Editor (gpedit.msc) - Available in Windows Pro & Enterprise editions");
            if (btnServicesTool != null)
            {
                btnServicesTool.Content = LocalizationHelper.Get("Tool_Services", "⚙️ Windows Services (services.msc)");
                btnServicesTool.ToolTip = LocalizationHelper.Get("Tooltip_ToolServices", "Windows Services console (services.msc)");
            }
            if (btnOpenMaintenanceFolderTool != null)
            {
                btnOpenMaintenanceFolderTool.Content = LocalizationHelper.Get("Btn_OpenMaintenanceFolder", "📂 Maintenance Tools Folder (Portable)");
                btnOpenMaintenanceFolderTool.ToolTip = LocalizationHelper.Get("Tooltip_OpenMaintenanceFolder", "Access local Maintenance folder containing portable tools");
            }

            // 5.5. Add-ons & Modular Plugins Management (in Settings + Dynamic Sidebar Modules)
            if (txtAddonsManagerTitle != null) txtAddonsManagerTitle.Text = LocalizationHelper.Get("Addons_Manager_Title", "🧩 Modular Add-ons & Plugins");
            if (txtAddonsManagerSubtitle != null) txtAddonsManagerSubtitle.Text = LocalizationHelper.Get("Addons_Manager_Subtitle", "Add, enable, disable, or remove modular DLL plugins stored in Config\\Addons. Active modules appear directly in the sidebar.");
            if (btnAddAddon != null) btnAddAddon.Content = LocalizationHelper.Get("Btn_AddAddon", "➕ Add Module (.dll)");
            if (btnOpenAddonsFolder != null) btnOpenAddonsFolder.Content = LocalizationHelper.Get("Btn_OpenAddonsFolder", "📁 Open Add-ons Folder");
            if (btnReloadAddons != null) btnReloadAddons.Content = LocalizationHelper.Get("Btn_ReloadAddons", "🔄 Reload Modules");
            if (txtAddonsHelpTitle != null) txtAddonsHelpTitle.Text = LocalizationHelper.Get("Addons_Help_Title", "💡 How Modular DLL Add-ons Work");
            if (txtAddonsHelpText != null) txtAddonsHelpText.Text = LocalizationHelper.Get("Addons_Help_Text", "Each OptiAssist module is a compiled .NET library (.dll) stored in Config\\Addons\\<ModuleName>\\<ModuleName>.dll implementing IOptiAssistAddon. Enabled modules integrate seamlessly into the sidebar navigation as native pages.");

            if (_addons != null)
            {
                string activeLbl = LocalizationHelper.Get("Addons_Label_Active", "Active");
                string removeLbl = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");
                foreach (AddonEntry addon in _addons)
                {
                    addon.ActiveLabel = activeLbl;
                    addon.RemoveLabel = removeLbl;
                    if (addon.Instance != null)
                    {
                        try
                        {
                            addon.Instance.OnLanguageChanged(langCode);
                        }
                        catch { }
                    }
                    addon.RefreshMetadataFromInstance();
                }
                RebuildSidebarAddonsMenu();
            }

            // 7. Logs
            if (txtLogsTitle != null) txtLogsTitle.Text = LocalizationHelper.Get("Logs_Title", "⬛ Logs");
            if (txtLogsSub != null) txtLogsSub.Text = LocalizationHelper.Get("Logs_Subtitle", "Real-time log of diagnostics, scripts, and system executions");
            if (btnPopoutLog != null)
            {
                btnPopoutLog.Content = LocalizationHelper.Get("Btn_PopoutLog", "🗗 Pop-out Logs");
                btnPopoutLog.ToolTip = LocalizationHelper.Get("Tooltip_PopoutLog", "Open Logs in a separate floating window");
            }
            if (btnExportLog != null) btnExportLog.Content = LocalizationHelper.Get("Btn_ExportLog", "💾 Export Log");
            if (btnCopyAllLog != null) btnCopyAllLog.Content = LocalizationHelper.Get("Btn_CopyAllLog", "📋 Copy All");
            if (btnClearLog != null) btnClearLog.Content = LocalizationHelper.Get("Btn_ClearLog", "🗑️ Clear Console");

            // 8. Settings
            if (txtSettingsTitle != null) txtSettingsTitle.Text = LocalizationHelper.Get("Settings_Title", "OptiAssist Settings");
            if (txtSettingsSub != null) txtSettingsSub.Text = LocalizationHelper.Get("Settings_Subtitle", "Settings saved in Config\\settings.json on the portable drive");
            if (txtLangLabel != null) txtLangLabel.Text = LocalizationHelper.Get("Settings_Label_Language", "Application Language:");
            SetupLanguageButtons(langCode);

            if (chkSettingWinget != null) chkSettingWinget.Content = LocalizationHelper.Get("Settings_Chk_WingetFallback", "Enable Winget download fallback when program is not in Installers folder");
            if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.Content = LocalizationHelper.Get("Settings_Chk_AutoRefresh", "Automatically refresh hardware specs on startup");
            if (chkSettingExternalLogs != null) chkSettingExternalLogs.Content = LocalizationHelper.Get("Settings_Chk_ExternalLogs", "Open external Logs window by default");
            if (btnSaveSettings != null) btnSaveSettings.Content = LocalizationHelper.Get("Btn_SaveSettings", "💾 Save Settings");
            if (btnResetSettings != null) btnResetSettings.Content = LocalizationHelper.Get("Btn_ResetSettings", "🔄 Restore Defaults");

            if (txtPersonalizeTitle != null) txtPersonalizeTitle.Text = LocalizationHelper.Get("Settings_Personalize_Title", "🎨 Customize Application");
            if (txtPersonalizeSub != null) txtPersonalizeSub.Text = LocalizationHelper.Get("Settings_Personalize_Subtitle", "Customize the visual theme, button accent colors, branding, and styling of OptiAssist:");
            if (txtStoreNameLabel != null) txtStoreNameLabel.Text = LocalizationHelper.Get("Settings_Label_StoreName", "Custom application name:");
            if (txtSettingStore != null) txtSettingStore.ToolTip = LocalizationHelper.Get("Settings_Hint_StoreName", "Customize the application according to the name you prefer.");
            if (txtStoreNameHint != null) txtStoreNameHint.Text = LocalizationHelper.Get("Settings_Hint_StoreName", "Customize the application according to the name you prefer.");
            if (btnChangeAppLogo != null)
            {
                btnChangeAppLogo.Content = LocalizationHelper.Get("Btn_ChangeAppLogo", "🖼️ Change App Logo");
                btnChangeAppLogo.ToolTip = LocalizationHelper.Get("Tooltip_ChangeAppLogo", "Select a custom logo image (.png, .jpg, .ico) for the application");
            }
            if (txtThemeLabel != null) txtThemeLabel.Text = LocalizationHelper.Get("Settings_Label_Theme", "Visual Theme:");
            if (txtThemeText != null) txtThemeText.Text = _isDarkMode ? LocalizationHelper.Get("Theme_Light", "Light Mode") : LocalizationHelper.Get("Theme_Dark", "Dark Mode");
            if (btnToggleTheme != null) btnToggleTheme.ToolTip = LocalizationHelper.Get("Tooltip_ToggleTheme", "Toggle between dark mode and light mode");
            if (txtAccentLabel != null) txtAccentLabel.Text = LocalizationHelper.Get("Settings_Label_AccentColor", "Accent & Button Color (Hexadecimal):");
            if (btnOpenColorDialog != null)
            {
                btnOpenColorDialog.Content = LocalizationHelper.Get("Btn_ColorPicker", "🎨 Pick Color...");
                btnOpenColorDialog.ToolTip = LocalizationHelper.Get("Tooltip_ColorPicker", "Open Windows color picker dialog");
            }
            if (btnApplyCustomColor != null)
            {
                btnApplyCustomColor.Content = LocalizationHelper.Get("Btn_ApplyColor", "Apply Color");
                btnApplyCustomColor.ToolTip = LocalizationHelper.Get("Tooltip_ApplyColor", "Apply the specified accent color immediately to OptiAssist");
            }
            if (txtPreviewLabel != null) txtPreviewLabel.Text = LocalizationHelper.Get("Settings_Label_Preview", "Preview: ");
            if (borderPreviewColor != null) borderPreviewColor.ToolTip = LocalizationHelper.Get("Tooltip_PreviewColor", "Current accent color");
            if (txtPresetLabel != null) txtPresetLabel.Text = LocalizationHelper.Get("Settings_Label_Presets", "Suggested Colors:");

            // Suggested color presets
            if (btnPresetBlue != null) btnPresetBlue.Content = "🔵 " + LocalizationHelper.Get("Color_Blue", "Blue (Default)");
            if (btnPresetGreen != null) btnPresetGreen.Content = "🟢 " + LocalizationHelper.Get("Color_Green", "Light Green");
            if (btnPresetPurple != null) btnPresetPurple.Content = "🟣 " + LocalizationHelper.Get("Color_Purple", "Purple");
            if (btnPresetOrange != null) btnPresetOrange.Content = "🟠 " + LocalizationHelper.Get("Color_Orange", "Orange");
            if (btnPresetRed != null) btnPresetRed.Content = "🔴 " + LocalizationHelper.Get("Color_Red", "Red");
            if (btnPresetYellow != null) btnPresetYellow.Content = "🟡 " + LocalizationHelper.Get("Color_Yellow", "Yellow");
            if (btnPresetPink != null) btnPresetPink.Content = "🌸 " + LocalizationHelper.Get("Color_Pink", "Pink");

            // Additional Settings card
            if (txtAdditionalSettingsTitle != null) txtAdditionalSettingsTitle.Text = LocalizationHelper.Get("Settings_Card_AdditionalSettings", "🛠️ Additional Settings");
            if (txtAdditionalSettingsSub != null) txtAdditionalSettingsSub.Text = LocalizationHelper.Get("Settings_Sub_AdditionalSettings", "Device integration, system installation, and portable drive options:");
            if (btnCreateAutorun != null)
            {
                btnCreateAutorun.Content = LocalizationHelper.Get("Btn_CreateAutorun", "💿 Create USB AutoRun");
                btnCreateAutorun.ToolTip = LocalizationHelper.Get("Tooltip_CreateAutorun", "Create autorun.inf on drive root to display application icon and AutoPlay entry");
            }
            if (btnInstallApp != null)
            {
                btnInstallApp.ToolTip = LocalizationHelper.Get("Tooltip_InstallApp", "Install OptiAssist to C:\\Program Files\\OptiAssist with Desktop and Start Menu shortcuts");
            }
            if (btnUninstallApp != null)
            {
                btnUninstallApp.Content = LocalizationHelper.Get("Btn_UninstallApp", "🗑️ Uninstall OptiAssist");
                btnUninstallApp.ToolTip = LocalizationHelper.Get("Tooltip_UninstallApp", "Uninstall OptiAssist from this device and remove all shortcuts");
            }
            UpdateDeploymentButtonsUI();

            // 9. About
            if (txtAboutTitle != null) txtAboutTitle.Text = LocalizationHelper.Get("About_Title", "About OptiAssist");
            if (txtAboutSub != null) txtAboutSub.Text = LocalizationHelper.Get("About_Subtitle", "Version information, copyright, and open-source license");
            if (txtAboutLicenseTag != null) txtAboutLicenseTag.Text = LocalizationHelper.Get("About_License_Tag", "⚖️ License: GNU General Public License v3.0 (GNU GPLv3)");
            if (txtAboutCreditsTitle != null) txtAboutCreditsTitle.Text = LocalizationHelper.Get("About_Credits_Title", "🤝 Acknowledgments & Credits");
            if (txtAboutCreditsText != null) txtAboutCreditsText.Text = LocalizationHelper.Get("About_Credits_Text", "Special thanks and credit to Chris Titus Tech and the open-source community behind WinUtil, whose work served as an essential inspiration and foundation for the implementation of several system maintenance and optimization features in OptiAssist.");
            if (btnAboutGithub != null) btnAboutGithub.Content = LocalizationHelper.Get("Btn_AboutGithub", "🌐 GitHub Repository");
            if (btnAboutWebsite != null) btnAboutWebsite.Content = LocalizationHelper.Get("Btn_AboutWebsite", "🔗 Official Website");
            if (btnAboutDocumentation != null)
            {
                btnAboutDocumentation.Content = LocalizationHelper.Get("Btn_AboutDocumentation", "📖 Documentation");
                btnAboutDocumentation.ToolTip = LocalizationHelper.Get("Tooltip_AboutDocumentation", "Access the official OptiAssist documentation");
            }
            if (txtAboutAddonsTitle != null) txtAboutAddonsTitle.Text = LocalizationHelper.Get("About_Addons_Title", "🧩 About Installed Modules & Plugins");
            if (txtAboutAddonsSubtitle != null) txtAboutAddonsSubtitle.Text = LocalizationHelper.Get("About_Addons_Subtitle", "Quick summary and AssemblyInfo metadata of all modular add-ons integrated into OptiAssist:");

            // 10. Bottom status bar
            if (txtBitLockerLabel != null) txtBitLockerLabel.Text = LocalizationHelper.Get("Status_BitLocker", "🔒 BitLocker: ");
            if (txtTpmLabel != null) txtTpmLabel.Text = LocalizationHelper.Get("Status_Tpm", "🛡️ TPM: ");
            if (txtSecureBootLabel != null) txtSecureBootLabel.Text = LocalizationHelper.Get("Status_SecureBoot", "⚡ Secure Boot: ");
            if (pnlBitLockerStatus != null) pnlBitLockerStatus.ToolTip = LocalizationHelper.Get("Tooltip_BitLockerStatus", "Click to open BitLocker / Device Encryption settings");
            if (pnlTpmStatus != null) pnlTpmStatus.ToolTip = LocalizationHelper.Get("Tooltip_TpmStatus", "Click to open TPM Management console (tpm.msc)");

            // 11. Update hardware display or detecting placeholders
            if (_hardware != null)
            {
                UpdateHardwareDisplay(_hardware);
            }
            else
            {
                string detecting = LocalizationHelper.Get("Status_Detecting", "Detecting...");
                if (lblHeaderDevice != null)
                {
                    lblHeaderDevice.Text = detecting;
                    lblHeaderDevice.ToolTip = LocalizationHelper.Get("Tooltip_HeaderMotherboard", "Motherboard Model");
                }
                if (lblHeaderCpu != null)
                {
                    lblHeaderCpu.Text = detecting;
                    lblHeaderCpu.ToolTip = LocalizationHelper.Get("Tooltip_HeaderCpu", "Processor");
                }
                if (lblHeaderRam != null)
                {
                    lblHeaderRam.Text = detecting;
                    lblHeaderRam.ToolTip = LocalizationHelper.Get("Tooltip_HeaderRam", "Total RAM Memory");
                }
                if (lblOsTitle != null) lblOsTitle.Text = LocalizationHelper.Get("Status_Detecting", "Detecting Windows...");
                if (lblOsDetails != null) lblOsDetails.Text = detecting;
                if (lblCpuDetails != null) lblCpuDetails.Text = LocalizationHelper.Get("Label_Processor", "Processor: ") + detecting;
                if (lblRamDetails != null) lblRamDetails.Text = LocalizationHelper.Get("Label_Ram", "RAM Memory: ") + detecting;
                if (lblGpuDetails != null) lblGpuDetails.Text = LocalizationHelper.Get("Label_Gpu", "Graphics Card: ") + detecting;
                if (lblMotherboard != null) lblMotherboard.Text = LocalizationHelper.Get("Label_Motherboard", "Motherboard: ") + detecting;
                if (lblBiosInfo != null) lblBiosInfo.Text = LocalizationHelper.Get("Label_Bios", "BIOS: ") + detecting;
                if (lblDiskDetails != null) lblDiskDetails.Text = LocalizationHelper.Get("Label_Storage", "Storage: ") + detecting;
                if (lblSerialNumber != null) lblSerialNumber.Text = LocalizationHelper.Get("Status_NotDetected", "Not available");
                if (lblMachineName != null) lblMachineName.Text = detecting;

                if (txtBitLockerStatus != null) txtBitLockerStatus.Text = detecting;
                if (txtTpmStatus != null) txtTpmStatus.Text = detecting;
                if (txtSecureBootStatus != null) txtSecureBootStatus.Text = detecting;
            }
        }

        private void SetupLanguageButtons(string activeLang)
        {
            if (btnLangPt != null)
            {
                btnLangPt.Style = (activeLang == "pt-BR") ? (Style)FindResource("BtnAccent") : (Style)FindResource("BtnDefault");
            }
            if (btnLangEn != null)
            {
                btnLangEn.Style = (activeLang == "en-US") ? (Style)FindResource("BtnAccent") : (Style)FindResource("BtnDefault");
            }
            if (lblCurrentLanguage != null)
            {
                lblCurrentLanguage.Text = LocalizationHelper.Get("Settings_Current_Lang", (activeLang == "pt-BR") ? "Atual: Português (pt-BR)" : "Current: English (en-US)");
            }

            if (pnlLanguageButtons == null) return;

            var allLangs = LocalizationHelper.GetAvailableLanguages();
            foreach (var lang in allLangs)
            {
                if (lang.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) || lang.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                    continue;

                string btnName = "btnLang_" + lang.Replace("-", "_");
                Button dynamicBtn = pnlLanguageButtons.FindName(btnName) as Button;
                if (dynamicBtn == null)
                {
                    dynamicBtn = new Button();
                    dynamicBtn.Name = btnName;
                    dynamicBtn.Content = LocalizationHelper.GetLanguageDisplayName(lang);
                    dynamicBtn.Height = 32;
                    dynamicBtn.Padding = new Thickness(14, 4, 14, 4);
                    dynamicBtn.Margin = new Thickness(0, 0, 8, 0);
                    dynamicBtn.Tag = lang;
                    dynamicBtn.Click += (s, e) =>
                    {
                        var btn = s as Button;
                        if (btn != null && btn.Tag != null)
                        {
                            SetLanguage(btn.Tag.ToString());
                        }
                    };
                    int idx = pnlLanguageButtons.Children.IndexOf(lblCurrentLanguage);
                    if (idx >= 0)
                        pnlLanguageButtons.Children.Insert(idx, dynamicBtn);
                    else
                        pnlLanguageButtons.Children.Add(dynamicBtn);
                }
                dynamicBtn.Style = (activeLang == lang) ? (Style)FindResource("BtnAccent") : (Style)FindResource("BtnDefault");
            }
        }

        private void TxtSettingStore_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            UpdateServiceProviderUI();
        }

        private void PnlBitLockerStatus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Log("[SHORTCUT] Opening BitLocker / Device Encryption settings...");
            ShortcutsHelper.OpenBitLockerSettings();
        }

        private void PnlTpmStatus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Log("[SHORTCUT] Opening TPM Management console (tpm.msc)...");
            ShortcutsHelper.OpenWindowsTool("tpm.msc");
        }

        private void ChkSettingWinget_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.EnableWingetFallback = chkSettingWinget.IsChecked == true;
            SettingsHelper.Save(_settings);

            Log(string.Format("[CONFIG] EnableWingetFallback updated to {0} and saved to settings.json.", _settings.EnableWingetFallback));
        }

        private void ChkSettingAutoRefresh_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.AutoRefreshHardware = chkSettingAutoRefresh.IsChecked == true;
            SettingsHelper.Save(_settings);

            Log(string.Format("[CONFIG] AutoRefreshHardware updated to {0} and saved to settings.json.", _settings.AutoRefreshHardware));
        }

        private void ChkSettingExternalLogs_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.OpenExternalLogsByDefault = chkSettingExternalLogs.IsChecked == true;
            SettingsHelper.Save(_settings);

            Log(string.Format("[CONFIG] OpenExternalLogsByDefault updated to {0} and saved to settings.json.", _settings.OpenExternalLogsByDefault));
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) _settings = new AppSettings();
            _settings.CustomerName = txtSettingStore != null ? txtSettingStore.Text.Trim() : "";
            _settings.EnableWingetFallback = chkSettingWinget != null && chkSettingWinget.IsChecked == true;
            _settings.AutoRefreshHardware = chkSettingAutoRefresh != null && chkSettingAutoRefresh.IsChecked == true;
            _settings.OpenExternalLogsByDefault = chkSettingExternalLogs != null && chkSettingExternalLogs.IsChecked == true;
            _settings.AccentColor = txtSettingAccentColor.Text.Trim();

            SettingsHelper.Save(_settings);

            UpdateServiceProviderUI();
            ApplyThemeAccentColor(_settings.AccentColor);

            MessageBox.Show(LocalizationHelper.Get("Msg_SettingsSaved", "Settings successfully saved in Config\\settings.json!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("[CONFIG] Application settings updated and saved.");
        }

        private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                LocalizationHelper.Get("Msg_ResetSettingsConfirm", "Do you want to restore all application settings to their default values?"),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            // A fresh AppSettings instance holds the factory defaults
            AppSettings defaults = new AppSettings();
            SettingsHelper.Save(defaults);

            // Reload everything from disk so language, theme, accent color and visibility are re-applied
            LoadApplicationSettings();

            MessageBox.Show(LocalizationHelper.Get("Msg_ResetSettingsDone", "Default settings successfully restored!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("[CONFIG] Application settings restored to factory defaults.");
        }

        private void BtnCreateAutorun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string driveRoot = Path.GetPathRoot(baseDir);

                if (string.IsNullOrEmpty(driveRoot))
                {
                    throw new InvalidOperationException("Could not determine drive root path.");
                }

                string exeFullPath = null;
                try
                {
                    var curProc = Process.GetCurrentProcess();
                    if (curProc != null && curProc.MainModule != null)
                    {
                        exeFullPath = curProc.MainModule.FileName;
                    }
                }
                catch
                {
                    // Fallback to standard base path executable if Process module cannot be queried
                }

                if (string.IsNullOrEmpty(exeFullPath) || !File.Exists(exeFullPath))
                {
                    exeFullPath = Path.Combine(baseDir, "OptiAssist.exe");
                }

                // Compute relative path from drive root to the application executable
                string relativeExePath;
                if (exeFullPath.StartsWith(driveRoot, StringComparison.OrdinalIgnoreCase))
                {
                    relativeExePath = exeFullPath.Substring(driveRoot.Length).TrimStart('\\', '/');
                }
                else
                {
                    relativeExePath = Path.GetFileName(exeFullPath);
                }

                string autorunPath = Path.Combine(driveRoot, "autorun.inf");

                // Clear any existing read-only or hidden attributes before writing
                if (File.Exists(autorunPath))
                {
                    File.SetAttributes(autorunPath, FileAttributes.Normal);
                }

                // AutoRun configuration without altering volume label
                string autorunContent = string.Format(
                    "[AutoRun]\r\nopen={0}\r\naction=Executar o OptiAssist\r\nicon={0},0\r\n",
                    relativeExePath);

                File.WriteAllText(autorunPath, autorunContent, Encoding.Default);

                Log(string.Format("[AUTORUN] autorun.inf created successfully at: {0}", autorunPath));
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_AutorunCreated", "autorun.inf created successfully on drive root: {0}\nWhen connecting the USB drive, Windows will display the OptiAssist icon and execution action."), autorunPath),
                    "AutoRun",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log(string.Format("[AUTORUN ERROR] Failed to create autorun.inf: {0}", ex.Message));
                MessageBox.Show(
                    string.Format(LocalizationHelper.Get("Msg_AutorunError", "Failed to create autorun.inf: {0}"), ex.Message),
                    "AutoRun",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #region APPLICATION BRANDING & LOGO
        private void LoadAppLogo()
        {
            try
            {
                string logoPath = null;
                if (_settings != null && !string.IsNullOrWhiteSpace(_settings.CustomLogoPath))
                {
                    string p = _settings.CustomLogoPath;
                    if (!Path.IsPathRooted(p))
                    {
                        p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p);
                    }
                    if (File.Exists(p))
                    {
                        logoPath = p;
                    }
                }

                if (string.IsNullOrEmpty(logoPath))
                {
                    string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.png");
                    if (File.Exists(defaultPath))
                    {
                        logoPath = defaultPath;
                    }
                }

                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    if (imgAppHeaderLogo != null) imgAppHeaderLogo.Source = bmp;
                    if (imgAboutLogo != null) imgAboutLogo.Source = bmp;
                }
            }
            catch (Exception ex)
            {
                Log("[LOGO ERROR] Failed to load logo: " + ex.Message);
            }
        }

        private void BtnChangeAppLogo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = LocalizationHelper.Get("Filter_Images", "Image Files (*.png;*.jpg;*.jpeg;*.ico;*.bmp)|*.png;*.jpg;*.jpeg;*.ico;*.bmp|All Files (*.*)|*.*");
                ofd.Title = LocalizationHelper.Get("Title_SelectLogo", "Select Application Logo");
                if (ofd.ShowDialog() == true)
                {
                    string selectedFile = ofd.FileName;
                    string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    string ext = Path.GetExtension(selectedFile);
                    string destFile = Path.Combine(configDir, "custom_logo" + ext);

                    File.Copy(selectedFile, destFile, true);

                    if (_settings == null) _settings = SettingsHelper.Load();
                    _settings.CustomLogoPath = Path.Combine("Config", "custom_logo" + ext);
                    SettingsHelper.Save(_settings);

                    LoadAppLogo();

                    MessageBox.Show(LocalizationHelper.Get("Msg_LogoChanged", "Application logo updated successfully!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("[CONFIG] Custom application logo changed to: " + _settings.CustomLogoPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_LogoError", "Failed to update logo: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                Log("[CONFIG ERROR] Failed to change logo: " + ex.Message);
            }
        }
        #endregion

        #region APPLICATION DEPLOYMENT (INSTALL / UNINSTALL)
        private void UpdateDeploymentButtonsUI()
        {
            try
            {
                bool isInstalled = AppDeploymentHelper.IsAppInstalled();
                string installedDir = AppDeploymentHelper.GetInstalledDirectory().TrimEnd('\\', '/');
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                bool runningFromInstalled = string.Equals(installedDir, currentDir, StringComparison.OrdinalIgnoreCase);

                if (btnInstallApp != null)
                {
                    if (runningFromInstalled)
                    {
                        btnInstallApp.IsEnabled = false;
                        btnInstallApp.Content = LocalizationHelper.Get("Btn_AppInstalled", "✔️ OptiAssist Installed");
                    }
                    else
                    {
                        btnInstallApp.IsEnabled = true;
                        btnInstallApp.Content = LocalizationHelper.Get("Btn_InstallApp", "📥 Install OptiAssist");
                    }
                }

                if (btnUninstallApp != null)
                {
                    btnUninstallApp.IsEnabled = isInstalled || runningFromInstalled;
                }
            }
            catch { }
        }

        private void BtnInstallApp_Click(object sender, RoutedEventArgs e)
        {
            Log("[INSTALL] Starting OptiAssist installation to Program Files...");
            AppDeploymentHelper.InstallOptiAssist(Log);
            UpdateDeploymentButtonsUI();
        }

        private void BtnUninstallApp_Click(object sender, RoutedEventArgs e)
        {
            Log("[UNINSTALL] Requesting OptiAssist uninstallation...");
            AppDeploymentHelper.UninstallOptiAssist(Log);
            UpdateDeploymentButtonsUI();
        }
        #endregion

        #region THEME & COLOR PERSONALIZATION
        public void ApplyThemeAccentColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor)) return;
            if (!hexColor.StartsWith("#")) hexColor = "#" + hexColor;

            try
            {
                var colorObj = System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                if (colorObj == null) return;
                System.Windows.Media.Color color = (System.Windows.Media.Color)colorObj;

                // Lighter tone for hover and darker tone for pressed
                System.Windows.Media.Color hoverColor = System.Windows.Media.Color.FromArgb(
                    color.A,
                    (byte)Math.Min(255, color.R + 35),
                    (byte)Math.Min(255, color.G + 35),
                    (byte)Math.Min(255, color.B + 35));

                System.Windows.Media.Color pressedColor = System.Windows.Media.Color.FromArgb(
                    color.A,
                    (byte)Math.Max(0, color.R - 35),
                    (byte)Math.Max(0, color.G - 35),
                    (byte)Math.Max(0, color.B - 35));

                // Update dynamic global application resources
                Application.Current.Resources["ColorAccent"] = color;
                Application.Current.Resources["ColorAccentHover"] = hoverColor;
                Application.Current.Resources["ColorAccentPressed"] = pressedColor;

                SolidColorBrush mainBrush = new SolidColorBrush(color);
                SolidColorBrush hoverBrush = new SolidColorBrush(hoverColor);
                SolidColorBrush pressedBrush = new SolidColorBrush(pressedColor);

                Application.Current.Resources["BrushAccent"] = mainBrush;
                Application.Current.Resources["BrushAccentHover"] = hoverBrush;
                Application.Current.Resources["BrushAccentPressed"] = pressedBrush;

                if (borderPreviewColor != null)
                {
                    borderPreviewColor.Background = mainBrush;
                }
            }
            catch { }
        }

        private void TxtSettingAccentColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSettingAccentColor == null || borderPreviewColor == null) return;
            string hex = txtSettingAccentColor.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (hex.Length == 7 || hex.Length == 9)
            {
                try
                {
                    var colorObj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
                    if (colorObj != null)
                    {
                        borderPreviewColor.Background = new SolidColorBrush((System.Windows.Media.Color)colorObj);
                    }
                }
                catch { }
            }
        }

        private void BtnOpenColorDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var cd = new System.Windows.Forms.ColorDialog())
                {
                    cd.AllowFullOpen = true;
                    cd.AnyColor = true;
                    try
                    {
                        string hex = txtSettingAccentColor.Text.Trim();
                        if (!hex.StartsWith("#")) hex = "#" + hex;
                        var cur = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                        cd.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                    }
                    catch { }

                    if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string hex = string.Format("#{0:X2}{1:X2}{2:X2}", cd.Color.R, cd.Color.G, cd.Color.B);
                        txtSettingAccentColor.Text = hex;
                        ApplyThemeAccentColor(hex);
                        if (_settings == null) _settings = new AppSettings();
                        _settings.AccentColor = hex;
                        SettingsHelper.Save(_settings);
                        Log("[CONFIG] Custom color selected and saved: " + hex);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_ColorPickerError", "Error opening color picker: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            string hex = txtSettingAccentColor.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try
            {
                System.Windows.Media.ColorConverter.ConvertFromString(hex);
                ApplyThemeAccentColor(hex);
                if (_settings == null) _settings = new AppSettings();
                _settings.AccentColor = hex;
                SettingsHelper.Save(_settings);
                Log("[CONFIG] Custom color applied and saved: " + hex);
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_ColorApplied", "Button and accent color applied successfully!\n{0}"), hex), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_ColorInvalid", "Invalid hexadecimal code! Use format #RRGGBB (e.g. #0EA5E9)."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnColorPreset_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                string hex = btn.Tag.ToString();
                txtSettingAccentColor.Text = hex;
                ApplyThemeAccentColor(hex);
                if (_settings == null) _settings = new AppSettings();
                _settings.AccentColor = hex;
                SettingsHelper.Save(_settings);
                Log("[CONFIG] Custom color applied and saved: " + hex);
            }
        }

        private bool _isDarkMode = true;
        public bool IsDarkMode { get { return _isDarkMode; } }

        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;

            SetColorResource("BrushBgPrimary", isDark ? "#18181B" : "#F1F5F9");
            SetColorResource("BrushBgSidebar", isDark ? "#121214" : "#FFFFFF");
            SetColorResource("BrushBgCard", isDark ? "#222228" : "#FFFFFF");
            SetColorResource("BrushBorderCard", isDark ? "#33333D" : "#CBD5E1");
            SetColorResource("BrushCardHover", isDark ? "#2C2C36" : "#F8FAFC");
            SetColorResource("BrushTextPrimary", isDark ? "#F8FAFC" : "#0F172A");
            SetColorResource("BrushTextSecondary", isDark ? "#94A3B8" : "#475569");
            SetColorResource("BrushTextMuted", isDark ? "#64748B" : "#94A3B8");
            SetColorResource("BrushBtnBg", isDark ? "#2A2A34" : "#E2E8F0");
            SetColorResource("BrushBtnHover", isDark ? "#383848" : "#CBD5E1");
            SetColorResource("BrushBtnBorderHover", isDark ? "#4B4B5E" : "#94A3B8");
            SetColorResource("BrushBtnPressed", isDark ? "#202028" : "#94A3B8");
            SetColorResource("BrushNavHover", isDark ? "#1A1A22" : "#F1F5F9");
            SetColorResource("BrushNavChecked", isDark ? "#202430" : "#E2E8F0");
            SetColorResource("BrushInputBg", isDark ? "#1C1C22" : "#FFFFFF");
            SetColorResource("BrushConsoleBg", isDark ? "#0C0C0F" : "#0F172A");
            SetColorResource("BrushConsoleText", isDark ? "#E2E8F0" : "#F8FAFC");

            if (txtThemeIcon != null && txtThemeText != null)
            {
                txtThemeIcon.Text = isDark ? "☀️" : "🌙";
                txtThemeText.Text = isDark ? LocalizationHelper.Get("Theme_Light", "Light Mode") : LocalizationHelper.Get("Theme_Dark", "Dark Mode");
            }

            if (_externalLogWindow != null)
            {
                _externalLogWindow.ApplyTheme(isDark);
            }
        }

        private void SetColorResource(string key, string hex)
        {
            try
            {
                var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                Application.Current.Resources[key] = new SolidColorBrush(col);
            }
            catch { }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!_isDarkMode);
            if (_settings == null) _settings = new AppSettings();
            _settings.Theme = _isDarkMode ? "Dark" : "Light";
            SettingsHelper.Save(_settings);
            Log(string.Format("[THEME] Visual mode switched to: {0}", _isDarkMode ? "Dark" : "Light"));
        }
        #endregion

        #region ABOUT APPLICATION
        private void BtnAboutGithub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/jvfigueiro/optiassist") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_LinkOpenError", "Unable to open link: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAboutWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://jvfigueiro.com/projects/optiassist") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_LinkOpenError", "Unable to open link: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAboutDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string docsReadme = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "README.md");
                if (File.Exists(docsReadme))
                {
                    Process.Start(new ProcessStartInfo(docsReadme) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Msg_DocumentationInDevelopment", "The documentation page is currently under development and will be available in an upcoming update."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(
                    LocalizationHelper.Get("Msg_DocumentationInDevelopment", "The documentation page is currently under development and will be available in an upcoming update."),
                    "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion
        #endregion
    }
}
