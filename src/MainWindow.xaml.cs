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
using System.Linq;

namespace OptiAssist
{
    public partial class MainWindow : Window, IOptiAssistHost
    {
        private AppSettings _settings;
        private HardwareSummary _hardware;
        private ObservableCollection<AddonEntry> _addons;
        private List<CustomShortcutItem> _customShortcuts = new List<CustomShortcutItem>();
        private bool _isRefreshingHardwareBg = false;
        private bool _isLoadingSettings = false;
        private System.Windows.Threading.DispatcherTimer _sidebarHoverTimer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            if (btnToggleSidebar != null)
            {
                btnToggleSidebar.MouseEnter += BtnToggleSidebar_MouseEnter;
                btnToggleSidebar.MouseLeave += BtnToggleSidebar_MouseLeave;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadApplicationSettings();
            LoadAppLogo();
            UpdateDeploymentButtonsUI();
            ReloadAddonsList();
            _customShortcuts = CustomShortcutsHelper.Load();
            RenderCommonTasks();

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
        private void BtnToggleSidebar_MouseEnter(object sender, MouseEventArgs e)
        {
            if (borderSidebar == null || colSidebar == null) return;
            if (borderSidebar.Visibility == Visibility.Collapsed || colSidebar.Width.Value == 0)
            {
                if (_sidebarHoverTimer == null)
                {
                    _sidebarHoverTimer = new System.Windows.Threading.DispatcherTimer();
                    _sidebarHoverTimer.Interval = TimeSpan.FromMilliseconds(700);
                    _sidebarHoverTimer.Tick += (s, args) =>
                    {
                        _sidebarHoverTimer.Stop();
                        if (btnToggleSidebar != null && btnToggleSidebar.IsMouseOver &&
                            (borderSidebar.Visibility == Visibility.Collapsed || colSidebar.Width.Value == 0))
                        {
                            colSidebar.Width = new GridLength(200);
                            borderSidebar.Visibility = Visibility.Visible;
                        }
                    };
                }
                else
                {
                    _sidebarHoverTimer.Stop();
                    _sidebarHoverTimer.Interval = TimeSpan.FromMilliseconds(700);
                }
                _sidebarHoverTimer.Start();
            }
        }

        private void BtnToggleSidebar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_sidebarHoverTimer != null)
            {
                _sidebarHoverTimer.Stop();
            }
        }

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarHoverTimer != null) _sidebarHoverTimer.Stop();
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
                scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewerContent.Visibility = Visibility.Visible;
                panelDashboard.Visibility = Visibility.Visible;
                RefreshHardwareBackground();
            }
            else if (rb == rbNavShortcuts)
            {
                scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
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
                scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewerContent.Visibility = Visibility.Visible;
                panelSettings.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavAbout)
            {
                scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewerContent.Visibility = Visibility.Visible;
                panelAbout.Visibility = Visibility.Visible;
            }
            else if (rb.Tag is AddonEntry)
            {
                AddonEntry entry = (AddonEntry)rb.Tag;
                if (string.Equals(entry.Id, "system-cleaner", StringComparison.OrdinalIgnoreCase))
                {
                    scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
                else
                {
                    scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }

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
                if (chkSettingAlwaysOnTop != null) chkSettingAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
                this.Topmost = _settings.AlwaysOnTop;

                string currentLang = string.IsNullOrEmpty(_settings.Language) ? "en-US" : _settings.Language;
                ApplyLanguage(currentLang);

                UpdateServiceProviderUI();
                LoadAppLogo();
                UpdateDeploymentButtonsUI();

                // Load and apply custom accent and button color
                string accent = string.IsNullOrEmpty(_settings.AccentColor) ? "#0EA5E9" : _settings.AccentColor;
                txtSettingAccentColor.Text = accent;
                ApplyThemeAccentColor(accent);

                // Load and apply theme (Light is default)
                if (!string.IsNullOrEmpty(_settings.Theme) && _settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyTheme(true);
                }
                else
                {
                    ApplyTheme(false);
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
            if (lblOsInstallDate != null) lblOsInstallDate.Text = LocalizationHelper.Get("Label_OsInstallDate", "Install Date: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
            if (lblOsActivation != null) lblOsActivation.Text = LocalizationHelper.Get("Label_OsActivation", "Activation: ") + LocalizationHelper.Get("Status_Detecting", "Detecting...");
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

            _isRefreshingHardwareBg = true;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    HardwareSummary info = SystemInfoHelper.GetSystemInfo();
                    _hardware = info;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        UpdateHardwareDisplay(info);
                        Log(string.Format("[INFO] Hardware updated. Serial: {0} | Motherboard: {1}", info.SerialNumber, info.Motherboard));
                    }));
                }
                finally
                {
                    _isRefreshingHardwareBg = false;
                }
            });
        }

        private void RefreshHardwareBackground()
        {
            if (_isRefreshingHardwareBg) return;
            if (_hardware == null)
            {
                RefreshHardwareAsync();
                return;
            }

            _isRefreshingHardwareBg = true;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    HardwareSummary info = SystemInfoHelper.GetSystemInfo();
                    _hardware = info;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        UpdateHardwareDisplay(info);
                    }));
                }
                catch { }
                finally
                {
                    _isRefreshingHardwareBg = false;
                }
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
            lblHeaderRam.ToolTip = string.Format("{0}: {1} GB",
                LocalizationHelper.Get("Label_Ram", "RAM Memory: ").TrimEnd(':', ' '), info.RamTotalGb);

            // Operating System Card (Right)
            lblOsTitle.Text = string.Format("{0} {1}", info.OsName, info.OsDisplayVersion);
            lblOsDetails.Text = string.Format("Build: {0} ({1})",
                info.OsBuild, info.OsArchitecture);
            if (lblOsInstallDate != null)
            {
                string installDateVal = !string.IsNullOrEmpty(info.OsInstallDate)
                    ? info.OsInstallDate
                    : LocalizationHelper.Get("Status_NotInformed", "Not informed");
                lblOsInstallDate.Text = string.Format("{0}{1}",
                    LocalizationHelper.Get("Label_OsInstallDate", "Install Date: "), installDateVal);
            }
            if (lblOsActivation != null)
            {
                string actText = info.OsIsActivated
                    ? LocalizationHelper.Get("Status_OsActivated", "Activated")
                    : LocalizationHelper.Get("Status_OsNotActivated", "Not Activated");
                lblOsActivation.Text = string.Format("{0}{1}",
                    LocalizationHelper.Get("Label_OsActivation", "Activation: "), actText);
            }
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
            lblRamDetails.Text = string.Format("{0}{1} GB",
                LocalizationHelper.Get("Label_Ram", "RAM Memory: "), info.RamTotalGb);
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
            MessageBoxResult confirm = MessageBox.Show(
                LocalizationHelper.Get("Msg_RunPostInstallConfirm", "Are you sure you want to run the automated application installation now?\n\nOptiAssist will navigate to App Manager (Automated Application Installation), open the external Logs window, and start installing the selected programs."),
                LocalizationHelper.Get("Task_RunPostInstall", "🚀 Run Post-Install"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            if (!NavigateToAddon("app-manager"))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_AppManagerNotAvailable", "The App Manager module is currently disabled or not installed."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenExternalLogWindow();

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

        private void BtnDateTime_Click(object sender, RoutedEventArgs e)
        {
            Log("[SHORTCUT] Opening Windows Date & Time administrative settings (timedate.cpl)...");
            ShortcutsHelper.OpenDateTimeSettings();
        }

        private void BtnAdminCmd_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenAdminCmd(); }
        private void BtnAdminPowerShell_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenAdminPowerShell(); }

        private void BtnOpenMaintenanceFolder_Click(object sender, RoutedEventArgs e) { ShortcutsHelper.OpenMaintenanceFolder(); }

        #region COMMON TASKS & CUSTOM SHORTCUTS (DASHBOARD)
        private void BtnCustomizeCommonTasks_Click(object sender, RoutedEventArgs e)
        {
            ShowCustomizeCommonTasksDialog();
        }

        private void ShowCustomizeCommonTasksDialog()
        {
            Window dlg = new Window
            {
                Title = LocalizationHelper.Get("Dashboard_CustomizeTasks_Title", "Customize Common Tasks"),
                Width = 560,
                Height = 490,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            dlg.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Border card = new Border
            {
                Margin = new Thickness(14),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            StackPanel spHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            TextBlock lblTitle = new TextBlock
            {
                Text = "⚙️ " + LocalizationHelper.Get("Dashboard_CustomizeTasks_Title", "Customize Common Tasks"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            lblTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            TextBlock lblDesc = new TextBlock
            {
                Text = LocalizationHelper.Get("Dashboard_CustomizeTasks_Desc", "Manage, add, reorder or remove shortcuts and actions shown in Common Tasks."),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            lblDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            spHeader.Children.Add(lblTitle);
            spHeader.Children.Add(lblDesc);
            Grid.SetRow(spHeader, 0);
            mainGrid.Children.Add(spHeader);

            // Middle: ListBox (Left) and Action Buttons (Right)
            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            ListBox lstTasks = new ListBox
            {
                SelectionMode = SelectionMode.Single,
                Margin = new Thickness(0, 0, 10, 0),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            lstTasks.SetResourceReference(ListBox.BackgroundProperty, "BrushBgInput");
            lstTasks.SetResourceReference(ListBox.BorderBrushProperty, "BrushBorderCard");
            lstTasks.SetResourceReference(ListBox.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(lstTasks, 0);
            contentGrid.Children.Add(lstTasks);

            StackPanel spActions = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            Button btnAdd = new Button
            {
                Content = "➕ " + LocalizationHelper.Get("Btn_Add", "Add..."),
                Style = (Style)FindResource("BtnAccent"),
                Height = 30,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 11
            };
            Button btnEdit = new Button
            {
                Content = "✏️ " + LocalizationHelper.Get("Btn_Edit", "Edit..."),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 11
            };
            Button btnUp = new Button
            {
                Content = LocalizationHelper.Get("Btn_MoveUp", "🔼 Move Up"),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 11
            };
            Button btnDown = new Button
            {
                Content = LocalizationHelper.Get("Btn_MoveDown", "🔽 Move Down"),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 11
            };
            Button btnRemove = new Button
            {
                Content = "🗑️ " + LocalizationHelper.Get("Btn_Remove", "Remove"),
                Style = (Style)FindResource("BtnDanger"),
                Height = 30,
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 11
            };
            Button btnRestore = new Button
            {
                Content = LocalizationHelper.Get("Btn_RestoreDefaults", "🔄 Restore Defaults"),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                FontSize = 10.5
            };

            spActions.Children.Add(btnAdd);
            spActions.Children.Add(btnEdit);
            spActions.Children.Add(btnUp);
            spActions.Children.Add(btnDown);
            spActions.Children.Add(btnRemove);
            spActions.Children.Add(btnRestore);
            Grid.SetColumn(spActions, 1);
            contentGrid.Children.Add(spActions);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // Populate ListBox
            Action<int> refreshList = (selectIndex) =>
            {
                lstTasks.Items.Clear();
                if (_customShortcuts != null)
                {
                    for (int i = 0; i < _customShortcuts.Count; i++)
                    {
                        var it = _customShortcuts[i];
                        Grid itemGrid = new Grid { Margin = new Thickness(4, 5, 4, 5) };
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        TextBlock tbIcon = new TextBlock
                        {
                            Text = !string.IsNullOrWhiteSpace(it.Icon) ? it.Icon : "⚡",
                            FontSize = 14,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        Grid.SetColumn(tbIcon, 0);
                        itemGrid.Children.Add(tbIcon);

                        StackPanel spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
                        TextBlock tbTitle = new TextBlock
                        {
                            Text = FormatShortcutButtonContent(it),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 11.5
                        };
                        tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

                        string sub = string.Equals(it.TargetType, "Section", StringComparison.OrdinalIgnoreCase)
                            ? LocalizationHelper.Get("Shortcut_Type_Section", "App Section") + " (" + it.TargetValue + ")"
                            : it.TargetValue;
                        if (!string.IsNullOrWhiteSpace(it.Arguments)) sub += " " + it.Arguments;

                        TextBlock tbSub = new TextBlock
                        {
                            Text = sub,
                            FontSize = 10,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        tbSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");

                        spText.Children.Add(tbTitle);
                        spText.Children.Add(tbSub);
                        Grid.SetColumn(spText, 1);
                        itemGrid.Children.Add(spText);

                        ListBoxItem lbi = new ListBoxItem
                        {
                            Content = itemGrid,
                            Tag = it
                        };
                        lstTasks.Items.Add(lbi);
                    }
                }

                if (selectIndex >= 0 && selectIndex < lstTasks.Items.Count)
                {
                    lstTasks.SelectedIndex = selectIndex;
                    lstTasks.ScrollIntoView(lstTasks.SelectedItem);
                }
                else if (lstTasks.Items.Count > 0)
                {
                    lstTasks.SelectedIndex = 0;
                }
            };

            refreshList(0);

            // Double click to edit
            lstTasks.MouseDoubleClick += (s, e) =>
            {
                ListBoxItem lbi = lstTasks.SelectedItem as ListBoxItem;
                if (lbi != null && lbi.Tag is CustomShortcutItem)
                {
                    int curIdx = lstTasks.SelectedIndex;
                    ShowCustomShortcutDialog((CustomShortcutItem)lbi.Tag, dlg);
                    refreshList(curIdx);
                }
            };

            btnAdd.Click += (s, e) =>
            {
                ShowCustomShortcutDialog(null, dlg);
                refreshList(_customShortcuts != null ? _customShortcuts.Count - 1 : 0);
            };

            btnEdit.Click += (s, e) =>
            {
                ListBoxItem lbi = lstTasks.SelectedItem as ListBoxItem;
                if (lbi != null && lbi.Tag is CustomShortcutItem)
                {
                    int curIdx = lstTasks.SelectedIndex;
                    ShowCustomShortcutDialog((CustomShortcutItem)lbi.Tag, dlg);
                    refreshList(curIdx);
                }
            };

            btnUp.Click += (s, e) =>
            {
                int idx = lstTasks.SelectedIndex;
                if (_customShortcuts != null && idx > 0 && idx < _customShortcuts.Count)
                {
                    var temp = _customShortcuts[idx];
                    _customShortcuts[idx] = _customShortcuts[idx - 1];
                    _customShortcuts[idx - 1] = temp;
                    CustomShortcutsHelper.Save(_customShortcuts);
                    RenderCommonTasks();
                    refreshList(idx - 1);
                }
            };

            btnDown.Click += (s, e) =>
            {
                int idx = lstTasks.SelectedIndex;
                if (_customShortcuts != null && idx >= 0 && idx < _customShortcuts.Count - 1)
                {
                    var temp = _customShortcuts[idx];
                    _customShortcuts[idx] = _customShortcuts[idx + 1];
                    _customShortcuts[idx + 1] = temp;
                    CustomShortcutsHelper.Save(_customShortcuts);
                    RenderCommonTasks();
                    refreshList(idx + 1);
                }
            };

            btnRemove.Click += (s, e) =>
            {
                ListBoxItem lbi = lstTasks.SelectedItem as ListBoxItem;
                if (lbi != null && lbi.Tag is CustomShortcutItem)
                {
                    int curIdx = lstTasks.SelectedIndex;
                    var itemToRemove = (CustomShortcutItem)lbi.Tag;
                    _customShortcuts.Remove(itemToRemove);
                    CustomShortcutsHelper.Save(_customShortcuts);
                    RenderCommonTasks();
                    refreshList(Math.Min(curIdx, _customShortcuts.Count - 1));
                    Log(string.Format("[SHORTCUTS] Common task '{0}' removed.", itemToRemove.Title));
                }
            };

            btnRestore.Click += (s, e) =>
            {
                var res = MessageBox.Show(dlg,
                    LocalizationHelper.Get("Msg_RestoreCommonTasksConfirm", "Do you want to reset Common Tasks to default factory shortcuts?"),
                    "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _customShortcuts = CustomShortcutsHelper.CreateDefaultShortcuts();
                    CustomShortcutsHelper.Save(_customShortcuts);
                    RenderCommonTasks();
                    refreshList(0);
                    Log("[SHORTCUTS] Common tasks restored to defaults.");
                }
            };

            // Bottom Close Bar
            StackPanel spBottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Button btnClose = new Button
            {
                Content = LocalizationHelper.Get("Btn_Close", "Close"),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 11
            };
            btnClose.Click += (s, e) => dlg.Close();
            spBottom.Children.Add(btnClose);

            Grid.SetRow(spBottom, 2);
            mainGrid.Children.Add(spBottom);

            card.Child = mainGrid;
            dlg.Content = card;
            dlg.ShowDialog();
        }

        private string GetSectionDisplayLabel(string sectionId, out string defaultIcon)
        {
            defaultIcon = "⭐";
            if (string.IsNullOrEmpty(sectionId)) return "";

            switch (sectionId.ToLowerInvariant())
            {
                case "toolbox":
                    defaultIcon = "🧰";
                    return LocalizationHelper.Get("Task_Toolbox", "🧰 Toolbox").Replace("🧰", "").Trim();
                case "app-manager":
                    defaultIcon = "📦";
                    return LocalizationHelper.Get("Task_PostInstall", "📦 App Manager").Replace("📦", "").Trim();
                case "post-install-run":
                    defaultIcon = "🚀";
                    return LocalizationHelper.Get("Task_RunPostInstall", "🚀 Run Post-Install").Replace("🚀", "").Trim();
                case "maintenance-folder":
                    defaultIcon = "📁";
                    return LocalizationHelper.Get("Task_MaintenanceFolder", "📁 Maintenance Folder").Replace("📁", "").Trim();
                case "installers-folder":
                    defaultIcon = "📂";
                    return LocalizationHelper.Get("Task_InstallersFolder", "📂 Installers Folder").Replace("📂", "").Trim();
                case "chris-titus":
                    defaultIcon = "🚀";
                    return LocalizationHelper.Get("Task_ChrisTitus", "🚀 Chris Titus WinUtil").Replace("🚀", "").Trim();
                case "memory-diag":
                    defaultIcon = "🧠";
                    return LocalizationHelper.Get("Task_MemoryDiag", "🧠 Memory Diagnostic").Replace("🧠", "").Trim();
                case "dev-mgmt":
                    defaultIcon = "🖥️";
                    return LocalizationHelper.Get("Task_DevMgmt", "🖥️ Device Manager").Replace("🖥️", "").Trim();
                case "sys-dm":
                    defaultIcon = "⚙️";
                    return LocalizationHelper.Get("Task_SysDm", "⚙️ System Properties").Replace("⚙️", "").Trim();
                case "reboot-uefi":
                    defaultIcon = "⚡";
                    return LocalizationHelper.Get("Task_RebootBios", "⚡ Reboot to UEFI").Replace("⚡", "").Trim();
                case "windows-tools":
                    defaultIcon = "🛠️";
                    return LocalizationHelper.Get("Nav_Shortcuts", "🛠️  Windows Tools").Replace("🛠️", "").Trim();
                case "logs":
                    defaultIcon = "📜";
                    return LocalizationHelper.Get("Nav_Terminal", "📜  Logs").Replace("📜", "").Trim();
                case "settings":
                    defaultIcon = "⚙️";
                    return LocalizationHelper.Get("Nav_Settings", "⚙️  Settings").Replace("⚙️", "").Trim();
                case "about":
                    defaultIcon = "ℹ️";
                    return LocalizationHelper.Get("Nav_About", "ℹ️  About").Replace("ℹ️", "").Trim();
                case "system-cleaner":
                    defaultIcon = "🧹";
                    return LocalizationHelper.Get("Nav_SystemCleaner", "System Cleaner");
                case "startup-manager":
                    defaultIcon = "🚀";
                    return LocalizationHelper.Get("Nav_StartupManager", "Startup Manager");
                case "maintenance-report":
                    defaultIcon = "📋";
                    return LocalizationHelper.Get("Nav_MaintenanceReport", "Maintenance Report");
            }

            if (_addons != null)
            {
                foreach (var a in _addons)
                {
                    if (string.Equals(a.Id, sectionId, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIcon = !string.IsNullOrEmpty(a.Icon) ? a.Icon : "🧩";
                        return a.Name;
                    }
                }
            }

            return sectionId;
        }

        private string FormatShortcutButtonContent(CustomShortcutItem item)
        {
            if (item == null) return "";
            string icon = !string.IsNullOrWhiteSpace(item.Icon) ? item.Icon.Trim() : "";
            string title = item.Title;

            if (string.Equals(item.TargetType, "Section", StringComparison.OrdinalIgnoreCase))
            {
                string defIcon;
                string secTitle = GetSectionDisplayLabel(item.TargetValue, out defIcon);
                if (string.IsNullOrWhiteSpace(title) ||
                    (!string.IsNullOrEmpty(item.Id) && item.Id.StartsWith("default-", StringComparison.OrdinalIgnoreCase)))
                {
                    title = secTitle;
                }
                if (string.IsNullOrWhiteSpace(icon)) icon = defIcon;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(icon)) icon = "⚡";
            }

            return string.Format("{0} {1}", icon, title).Trim();
        }

        private string GetCommonTaskTooltip(CustomShortcutItem item)
        {
            if (item == null) return "";
            if (string.Equals(item.TargetType, "Section", StringComparison.OrdinalIgnoreCase))
            {
                string sec = (item.TargetValue ?? "").Trim().ToLowerInvariant();
                switch (sec)
                {
                    case "post-install-run":
                        return LocalizationHelper.Get("Tooltip_TaskRunPostInstall", "Open App Manager and install all selected applications");
                    case "reboot-uefi":
                        return LocalizationHelper.Get("Tooltip_TaskRebootBios", "Restart computer directly into BIOS / UEFI");
                    case "toolbox":
                        return LocalizationHelper.Get("Tooltip_TaskToolbox", "Open Toolbox custom actions and tools");
                    case "app-manager":
                        return LocalizationHelper.Get("Tooltip_TaskPostInstall", "Go to application and installer manager");
                    case "memory-diag":
                        return LocalizationHelper.Get("Tooltip_TaskMemoryDiag", "Run Windows Memory Diagnostic tool");
                    case "dev-mgmt":
                        return LocalizationHelper.Get("Tooltip_TaskDevMgmt", "Open Windows Device Manager");
                    case "sys-dm":
                        return LocalizationHelper.Get("Tooltip_TaskSysDm", "Open System Properties dialog");
                    case "maintenance-folder":
                        return LocalizationHelper.Get("Tooltip_TaskMaintenanceFolder", "Open Maintenance Tools folder");
                    case "installers-folder":
                        return LocalizationHelper.Get("Tooltip_TaskInstallersFolder", "Open Installers folder");
                    case "chris-titus":
                        return LocalizationHelper.Get("Tooltip_TaskChrisTitus", "Run Chris Titus WinUtil in background");
                }
                return string.Format("{0}: {1}", LocalizationHelper.Get("Shortcut_Type_Section", "App Section"), FormatShortcutButtonContent(item));
            }

            return !string.IsNullOrWhiteSpace(item.Arguments)
                ? string.Format("{0} {1}", item.TargetValue, item.Arguments)
                : item.TargetValue;
        }

        private void RenderCommonTasks()
        {
            if (ugCommonTasks == null || txtCommonTasksEmpty == null) return;

            ugCommonTasks.Children.Clear();

            if (_customShortcuts == null || _customShortcuts.Count == 0)
            {
                txtCommonTasksEmpty.Visibility = Visibility.Visible;
                ugCommonTasks.Visibility = Visibility.Collapsed;
                return;
            }

            txtCommonTasksEmpty.Visibility = Visibility.Collapsed;
            ugCommonTasks.Visibility = Visibility.Visible;

            foreach (CustomShortcutItem item in _customShortcuts)
            {
                CustomShortcutItem currentItem = item;
                string labelText = FormatShortcutButtonContent(currentItem);
                string tooltipText = GetCommonTaskTooltip(currentItem);

                Style btnStyle = (Style)FindResource("BtnDefault");
                if (string.Equals(currentItem.TargetType, "Section", StringComparison.OrdinalIgnoreCase))
                {
                    string sec = (currentItem.TargetValue ?? "").Trim().ToLowerInvariant();
                    if (sec == "post-install-run")
                    {
                        btnStyle = (Style)FindResource("BtnAccent");
                    }
                    else if (sec == "reboot-uefi")
                    {
                        btnStyle = (Style)FindResource("BtnDanger");
                    }
                }

                Button btn = new Button
                {
                    Content = labelText,
                    Style = btnStyle,
                    Height = 34,
                    Margin = new Thickness(3),
                    FontSize = 11,
                    ToolTip = tooltipText
                };
                btn.Click += (s, e) => ExecuteCommonTask(currentItem, btn);

                ContextMenu cm = new ContextMenu();
                MenuItem miCustomize = new MenuItem { Header = LocalizationHelper.Get("Btn_Customize", "⚙️ Customize...") };
                miCustomize.Click += (s, e) => ShowCustomizeCommonTasksDialog();
                MenuItem miEdit = new MenuItem { Header = LocalizationHelper.Get("Shortcut_Menu_Edit", "✏️ Edit Task") };
                miEdit.Click += (s, e) => ShowCustomShortcutDialog(currentItem);
                MenuItem miRemove = new MenuItem { Header = LocalizationHelper.Get("Shortcut_Menu_Remove", "🗑️ Remove Task") };
                miRemove.Click += (s, e) => RemoveCommonTask(currentItem);

                cm.Items.Add(miCustomize);
                cm.Items.Add(new Separator());
                cm.Items.Add(miEdit);
                cm.Items.Add(miRemove);
                btn.ContextMenu = cm;

                ugCommonTasks.Children.Add(btn);
            }
        }

        private void RemoveCommonTask(CustomShortcutItem item)
        {
            if (item == null || _customShortcuts == null) return;
            _customShortcuts.Remove(item);
            CustomShortcutsHelper.Save(_customShortcuts);
            RenderCommonTasks();
            Log(string.Format("[SHORTCUTS] Common task '{0}' removed.", item.Title));
        }

        private void ExecuteCommonTask(CustomShortcutItem item, Button runButton = null)
        {
            if (item == null) return;

            if (string.Equals(item.TargetType, "Section", StringComparison.OrdinalIgnoreCase))
            {
                string sec = (item.TargetValue ?? "").Trim().ToLowerInvariant();
                if (sec == "post-install-run")
                {
                    BtnRunPostInstallTask_Click(this, new RoutedEventArgs());
                    return;
                }
                if (sec == "reboot-uefi")
                {
                    BtnRebootBios_Click(this, new RoutedEventArgs());
                    return;
                }
                if (sec == "memory-diag")
                {
                    Log("[SHORTCUTS] Opening Windows Memory Diagnostic...");
                    ShortcutsHelper.OpenMemoryDiagnostic();
                    return;
                }
                if (sec == "dev-mgmt")
                {
                    Log("[SHORTCUTS] Opening Device Manager (devmgmt.msc)...");
                    ShortcutsHelper.OpenWindowsTool("devmgmt.msc");
                    return;
                }
                if (sec == "sys-dm")
                {
                    Log("[SHORTCUTS] Opening System Properties (sysdm.cpl)...");
                    ShortcutsHelper.OpenWindowsTool("sysdm.cpl");
                    return;
                }
                if (sec == "maintenance-folder")
                {
                    Log("[SHORTCUTS] Opening Maintenance folder...");
                    ShortcutsHelper.OpenMaintenanceFolder();
                    return;
                }
                if (sec == "installers-folder")
                {
                    Log("[SHORTCUTS] Opening Installers folder...");
                    ShortcutsHelper.OpenInstallersFolder();
                    return;
                }
                if (sec == "chris-titus")
                {
                    string script = "$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; irm https://christitus.com/win | iex";
                    string base64Cmd = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                    RunShortcutCommandInBackground(
                        "Chris Titus WinUtil",
                        "powershell.exe",
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + base64Cmd,
                        runButton);
                    return;
                }
                if (sec == "windows-tools")
                {
                    if (rbNavShortcuts != null)
                    {
                        rbNavShortcuts.IsChecked = true;
                        NavButton_Click(rbNavShortcuts, new RoutedEventArgs());
                    }
                    return;
                }
                if (sec == "logs")
                {
                    if (rbNavTerminal != null)
                    {
                        rbNavTerminal.IsChecked = true;
                        NavButton_Click(rbNavTerminal, new RoutedEventArgs());
                    }
                    return;
                }
                if (sec == "settings")
                {
                    if (rbNavSettings != null)
                    {
                        rbNavSettings.IsChecked = true;
                        NavButton_Click(rbNavSettings, new RoutedEventArgs());
                    }
                    return;
                }
                if (sec == "about")
                {
                    if (rbNavAbout != null)
                    {
                        rbNavAbout.IsChecked = true;
                        NavButton_Click(rbNavAbout, new RoutedEventArgs());
                    }
                    return;
                }

                NavigateToAddonModule(sec);
                return;
            }

            ExecuteCustomShortcutTarget(item, runButton);
        }

        private void ExecuteCustomShortcutTarget(CustomShortcutItem item, Button runButton = null)
        {
            if (item == null) return;

            string target = Environment.ExpandEnvironmentVariables(item.TargetValue ?? "").Trim();
            string args = (item.Arguments ?? "").Trim();

            // Folders and URLs open directly via ShellExecute
            if (Directory.Exists(target) ||
                target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    Log(string.Format("[SHORTCUTS] Opened '{0}' -> {1}", item.Title, target));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(LocalizationHelper.Get("Shortcut_Msg_LaunchError", "Unable to launch shortcut '{0}':\n{1}"), item.Title, ex.Message),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            // External applications, scripts, and commands follow background execution logic
            string fileName = target;
            string finalArgs = args;
            string ext = "";
            try { ext = Path.GetExtension(target).ToLowerInvariant(); } catch { }

            if (ext == ".ps1")
            {
                fileName = "powershell.exe";
                finalArgs = string.Format("-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{0}\" {1}", target, args).Trim();
            }
            else if (ext == ".bat" || ext == ".cmd")
            {
                fileName = "cmd.exe";
                finalArgs = string.Format("/c \"\"{0}\" {1}\"", target, args).Trim();
            }
            else if (target.StartsWith("powershell ", StringComparison.OrdinalIgnoreCase) ||
                     target.StartsWith("powershell.exe ", StringComparison.OrdinalIgnoreCase))
            {
                int firstSpace = target.IndexOf(' ');
                fileName = "powershell.exe";
                string inlineArgs = firstSpace > 0 ? target.Substring(firstSpace + 1).Trim() : "";
                finalArgs = ("-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden " + inlineArgs + " " + args).Trim();
            }
            else if (target.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase) ||
                     target.StartsWith("cmd.exe ", StringComparison.OrdinalIgnoreCase))
            {
                int firstSpace = target.IndexOf(' ');
                fileName = "cmd.exe";
                string inlineArgs = firstSpace > 0 ? target.Substring(firstSpace + 1).Trim() : "";
                finalArgs = (inlineArgs + " " + args).Trim();
            }

            RunShortcutCommandInBackground(item.Title, fileName, finalArgs, runButton);
        }

        private void RunShortcutCommandInBackground(string title, string fileName, string arguments, Button runButton)
        {
            Log(string.Format("[SHORTCUTS] Starting '{0}' ({1} {2})...", title, fileName, arguments));

            object originalContent = runButton != null ? runButton.Content : null;
            if (runButton != null)
            {
                runButton.IsEnabled = false;
                runButton.Content = "⏳...";
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                int pid = 0;
                Exception startEx = null;
                bool isConsoleWrapper = fileName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                                        fileName.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                                        fileName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                                        fileName.Equals("cmd", StringComparison.OrdinalIgnoreCase);

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        CreateNoWindow = isConsoleWrapper,
                        WindowStyle = isConsoleWrapper ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                        UseShellExecute = !isConsoleWrapper
                    };

                    if (isConsoleWrapper)
                    {
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;
                    }

                    Process proc = new Process { StartInfo = psi };
                    if (isConsoleWrapper)
                    {
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log(string.Format("[SHORTCUTS:{0}] {1}", title, e.Data));
                            }
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log(string.Format("[SHORTCUTS:{0}:ERR] {1}", title, e.Data));
                            }
                        };
                    }

                    proc.Start();
                    try { pid = proc.Id; } catch { }

                    if (isConsoleWrapper)
                    {
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                    }

                    // Brief feedback pause like Toolbox fire-and-forget launch
                    proc.WaitForExit(1000);
                }
                catch (Exception ex)
                {
                    startEx = ex;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (runButton != null)
                    {
                        runButton.IsEnabled = true;
                        runButton.Content = originalContent;
                    }

                    if (startEx != null)
                    {
                        Log(string.Format("[SHORTCUTS] ❌ Failed to start '{0}': {1}", title, startEx.Message));
                        MessageBox.Show(
                            string.Format(LocalizationHelper.Get("Shortcut_Msg_LaunchError", "Unable to launch shortcut '{0}':\n{1}"), title, startEx.Message),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        Log(string.Format("[SHORTCUTS] ✔️ '{0}' launched in background (PID: {1}).", title, pid));
                    }
                }));
            });
        }

        private void NavigateToAddonModule(string addonId)
        {
            if (_addons != null)
            {
                foreach (AddonEntry addon in _addons)
                {
                    if (string.Equals(addon.Id, addonId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!addon.IsEnabled)
                        {
                            addon.IsEnabled = true;
                            RebuildSidebarAddonsMenu();
                        }

                        if (addon.SidebarButton != null)
                        {
                            addon.SidebarButton.IsChecked = true;
                            NavButton_Click(addon.SidebarButton, new RoutedEventArgs());
                            return;
                        }
                    }
                }
            }
        }

        private void ShowCustomShortcutDialog(CustomShortcutItem existingItem, Window ownerWindow = null)
        {
            bool isEdit = existingItem != null;
            Window dlg = new Window
            {
                Title = isEdit
                    ? LocalizationHelper.Get("Shortcut_Dialog_EditTitle", "Edit Shortcut / Task")
                    : LocalizationHelper.Get("Shortcut_Dialog_AddTitle", "Add Shortcut / Task"),
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = ownerWindow ?? this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            dlg.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Border card = new Border
            {
                Margin = new Thickness(14),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel sp = new StackPanel();

            TextBlock lblHeader = new TextBlock
            {
                Text = isEdit
                    ? "✏️ " + LocalizationHelper.Get("Shortcut_Dialog_EditTitle", "Edit Shortcut / Task")
                    : "➕ " + LocalizationHelper.Get("Shortcut_Dialog_AddTitle", "Add Shortcut / Task"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            lblHeader.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            sp.Children.Add(lblHeader);

            TextBlock lblType = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_TypeLabel", "Shortcut Type:"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            lblType.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            sp.Children.Add(lblType);

            StackPanel spRadios = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            RadioButton rbSection = new RadioButton
            {
                Content = LocalizationHelper.Get("Shortcut_Type_Section", "App Section / Module"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 18, 0),
                IsChecked = !isEdit || string.Equals(existingItem.TargetType, "Section", StringComparison.OrdinalIgnoreCase)
            };
            rbSection.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");

            RadioButton rbCustom = new RadioButton
            {
                Content = LocalizationHelper.Get("Shortcut_Type_Custom", "Custom Program / Folder / URL"),
                FontSize = 11,
                IsChecked = isEdit && string.Equals(existingItem.TargetType, "Custom", StringComparison.OrdinalIgnoreCase)
            };
            rbCustom.SetResourceReference(RadioButton.ForegroundProperty, "BrushTextPrimary");

            spRadios.Children.Add(rbSection);
            spRadios.Children.Add(rbCustom);
            sp.Children.Add(spRadios);

            // Section Picker Panel
            StackPanel pnlSection = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            TextBlock lblSelectSection = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_SelectSection", "Select Section or Module:"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblSelectSection.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            pnlSection.Children.Add(lblSelectSection);

            ComboBox cmbSections = new ComboBox { Height = 28, FontSize = 11 };
            string[] sectionIds = new string[]
            {
                "toolbox", "app-manager", "post-install-run",
                "maintenance-folder", "installers-folder", "chris-titus",
                "memory-diag", "dev-mgmt", "sys-dm", "reboot-uefi",
                "system-cleaner", "startup-manager", "maintenance-report",
                "windows-tools", "logs", "settings", "about"
            };
            foreach (string sid in sectionIds)
            {
                string ic;
                string name = GetSectionDisplayLabel(sid, out ic);
                ComboBoxItem cbi = new ComboBoxItem
                {
                    Content = string.Format("{0}  {1}", ic, name),
                    Tag = sid
                };
                cmbSections.Items.Add(cbi);
                if (isEdit && string.Equals(existingItem.TargetValue, sid, StringComparison.OrdinalIgnoreCase))
                {
                    cmbSections.SelectedItem = cbi;
                }
            }

            // Also add any loaded external add-ons
            if (_addons != null)
            {
                foreach (var addon in _addons)
                {
                    bool exists = false;
                    foreach (ComboBoxItem existingCbi in cmbSections.Items)
                    {
                        if (string.Equals(existingCbi.Tag as string, addon.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        ComboBoxItem cbi = new ComboBoxItem
                        {
                            Content = string.Format("{0}  {1}", !string.IsNullOrEmpty(addon.Icon) ? addon.Icon : "🧩", addon.Name),
                            Tag = addon.Id
                        };
                        cmbSections.Items.Add(cbi);
                        if (isEdit && string.Equals(existingItem.TargetValue, addon.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            cmbSections.SelectedItem = cbi;
                        }
                    }
                }
            }

            if (cmbSections.SelectedIndex < 0 && cmbSections.Items.Count > 0)
            {
                cmbSections.SelectedIndex = 0;
            }
            pnlSection.Children.Add(cmbSections);
            sp.Children.Add(pnlSection);

            // Custom Target Panel
            StackPanel pnlCustom = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            TextBlock lblTarget = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_TargetLabel", "Target Path, Command, Folder, or URL *:"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblTarget.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            pnlCustom.Children.Add(lblTarget);

            Grid targetGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtTarget = new TextBox
            {
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
                Text = (isEdit && rbCustom.IsChecked == true) ? existingItem.TargetValue : ""
            };
            Grid.SetColumn(txtTarget, 0);
            targetGrid.Children.Add(txtTarget);

            Button btnBrowseFile = new Button
            {
                Content = "📄 " + LocalizationHelper.Get("Startup_Btn_Browse", "Browse..."),
                Style = (Style)FindResource("BtnDefault"),
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 10.5
            };
            Grid.SetColumn(btnBrowseFile, 1);
            targetGrid.Children.Add(btnBrowseFile);

            Button btnBrowseFolder = new Button
            {
                Content = "📁 " + LocalizationHelper.Get("Shortcut_Btn_Folder", "Folder..."),
                Style = (Style)FindResource("BtnDefault"),
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 10.5
            };
            Grid.SetColumn(btnBrowseFolder, 2);
            targetGrid.Children.Add(btnBrowseFolder);
            pnlCustom.Children.Add(targetGrid);

            TextBlock lblArgs = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_ArgsLabel", "Arguments (optional):"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblArgs.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            pnlCustom.Children.Add(lblArgs);

            TextBox txtArgs = new TextBox
            {
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
                Text = isEdit ? (existingItem.Arguments ?? "") : ""
            };
            pnlCustom.Children.Add(txtArgs);
            sp.Children.Add(pnlCustom);

            // Title & Icon Row
            Grid nameIconGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            nameIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            nameIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel spIcon = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            TextBlock lblIcon = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_IconLabel", "Icon:"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblIcon.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            TextBox txtIcon = new TextBox
            {
                Height = 28,
                FontSize = 12,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = isEdit ? existingItem.Icon : "🧰"
            };
            spIcon.Children.Add(lblIcon);
            spIcon.Children.Add(txtIcon);
            Grid.SetColumn(spIcon, 0);
            nameIconGrid.Children.Add(spIcon);

            StackPanel spName = new StackPanel();
            TextBlock lblName = new TextBlock
            {
                Text = LocalizationHelper.Get("Shortcut_Dialog_NameLabel", "Display Name *:"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            TextBox txtName = new TextBox
            {
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
                Text = isEdit ? existingItem.Title : ""
            };
            spName.Children.Add(lblName);
            spName.Children.Add(txtName);
            Grid.SetColumn(spName, 1);
            nameIconGrid.Children.Add(spName);
            sp.Children.Add(nameIconGrid);

            Action syncSelectionToFields = () =>
            {
                if (rbSection.IsChecked == true)
                {
                    pnlSection.Visibility = Visibility.Visible;
                    pnlCustom.Visibility = Visibility.Collapsed;
                    ComboBoxItem sel = cmbSections.SelectedItem as ComboBoxItem;
                    if (sel != null && sel.Tag != null)
                    {
                        string ic;
                        string nm = GetSectionDisplayLabel(sel.Tag.ToString(), out ic);
                        txtIcon.Text = ic;
                        txtName.Text = nm;
                    }
                }
                else
                {
                    pnlSection.Visibility = Visibility.Collapsed;
                    pnlCustom.Visibility = Visibility.Visible;
                    if (!isEdit && (string.IsNullOrWhiteSpace(txtIcon.Text) || txtIcon.Text == "🧰"))
                    {
                        txtIcon.Text = "⚡";
                    }
                }
            };

            if (!isEdit)
            {
                syncSelectionToFields();
            }
            else
            {
                pnlSection.Visibility = rbSection.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                pnlCustom.Visibility = rbCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            rbSection.Checked += (s, e) => syncSelectionToFields();
            rbCustom.Checked += (s, e) =>
            {
                pnlSection.Visibility = Visibility.Collapsed;
                pnlCustom.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(txtName.Text) || cmbSections.SelectedItem != null)
                {
                    if (!isEdit) txtName.Text = "";
                }
                txtIcon.Text = "⚡";
            };
            cmbSections.SelectionChanged += (s, e) =>
            {
                if (rbSection.IsChecked == true) syncSelectionToFields();
            };

            btnBrowseFile.Click += (s, e) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "All Files (*.*)|*.*|Programs (*.exe;*.bat;*.cmd;*.ps1;*.msc)|*.exe;*.bat;*.cmd;*.ps1;*.msc";
                if (ofd.ShowDialog(dlg) == true)
                {
                    txtTarget.Text = ofd.FileName;
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        txtName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };

            btnBrowseFolder.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtTarget.Text = fbd.SelectedPath;
                        if (string.IsNullOrWhiteSpace(txtName.Text))
                        {
                            txtName.Text = new DirectoryInfo(fbd.SelectedPath).Name;
                        }
                        txtIcon.Text = "📁";
                    }
                }
            };

            // Bottom Action Buttons
            StackPanel spBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button btnCancel = new Button
            {
                Content = LocalizationHelper.Get("Btn_Cancel", "Cancel"),
                Style = (Style)FindResource("BtnDefault"),
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (s, e) => dlg.Close();
            spBtns.Children.Add(btnCancel);

            Button btnSave = new Button
            {
                Content = LocalizationHelper.Get("Startup_Btn_Save", "Save"),
                Style = (Style)FindResource("BtnAccent"),
                Height = 30,
                Padding = new Thickness(18, 0, 18, 0)
            };
            btnSave.Click += (s, e) =>
            {
                bool isSec = rbSection.IsChecked == true;
                string targetVal = "";
                if (isSec)
                {
                    ComboBoxItem sel = cmbSections.SelectedItem as ComboBoxItem;
                    if (sel != null && sel.Tag != null) targetVal = sel.Tag.ToString();
                }
                else
                {
                    targetVal = txtTarget.Text.Trim();
                }

                if (string.IsNullOrWhiteSpace(targetVal))
                {
                    MessageBox.Show(
                        LocalizationHelper.Get("Shortcut_Msg_Required", "Please specify a valid target path or select an app section."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string titleVal = txtName.Text.Trim();
                if (string.IsNullOrWhiteSpace(titleVal))
                {
                    if (isSec)
                    {
                        string ic;
                        titleVal = GetSectionDisplayLabel(targetVal, out ic);
                    }
                    else
                    {
                        titleVal = targetVal;
                    }
                }

                string iconVal = string.IsNullOrWhiteSpace(txtIcon.Text) ? (isSec ? "⭐" : "⚡") : txtIcon.Text.Trim();

                if (isEdit)
                {
                    existingItem.Title = titleVal;
                    existingItem.Icon = iconVal;
                    existingItem.TargetType = isSec ? "Section" : "Custom";
                    existingItem.TargetValue = targetVal;
                    existingItem.Arguments = isSec ? "" : txtArgs.Text.Trim();
                }
                else
                {
                    CustomShortcutItem newItem = new CustomShortcutItem
                    {
                        Title = titleVal,
                        Icon = iconVal,
                        TargetType = isSec ? "Section" : "Custom",
                        TargetValue = targetVal,
                        Arguments = isSec ? "" : txtArgs.Text.Trim()
                    };
                    if (_customShortcuts == null) _customShortcuts = new List<CustomShortcutItem>();
                    _customShortcuts.Add(newItem);
                }

                CustomShortcutsHelper.Save(_customShortcuts);
                RenderCommonTasks();
                dlg.Close();
            };
            spBtns.Children.Add(btnSave);

            sp.Children.Add(spBtns);
            card.Child = sp;
            dlg.Content = card;
            dlg.ShowDialog();
        }
        #endregion
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

            var sortedAddons = _addons
                .OrderBy(a => AddonHelper.GetModuleSortOrder(a.Id))
                .ThenBy(a => a.Name)
                .ToList();

            bool restoredActiveAddon = false;
            foreach (AddonEntry entry in sortedAddons)
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
                    if (string.Equals(entry.Id, "system-cleaner", StringComparison.OrdinalIgnoreCase))
                    {
                        scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                    else
                    {
                        scrollViewerContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    }
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
            if (rbNavTerminal != null) rbNavTerminal.Content = LocalizationHelper.Get("Nav_Terminal", "📜  Logs");
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
            if (btnCustomizeCommonTasks != null)
            {
                btnCustomizeCommonTasks.Content = LocalizationHelper.Get("Btn_Customize", "⚙️ Customize");
                btnCustomizeCommonTasks.ToolTip = LocalizationHelper.Get("Tooltip_CustomizeCommonTasks", "Customize common tasks, add new shortcuts, reorganize or remove buttons");
            }
            if (txtCommonTasksEmpty != null)
            {
                txtCommonTasksEmpty.Text = LocalizationHelper.Get("Dashboard_CommonTasks_Empty", "No common tasks configured. Click 'Customize' to add or restore default tasks.");
            }
            RenderCommonTasks();

            // 5. Windows Administrative Tools
            if (txtToolsTitle != null) txtToolsTitle.Text = LocalizationHelper.Get("Nav_Shortcuts", "🛠️  Windows Tools");
            if (txtToolsSubtitle != null) txtToolsSubtitle.Text = LocalizationHelper.Get("Tools_Subtitle", "Quick access to primary management consoles and native system utilities");
            if (txtAdminToolsTitle != null) txtAdminToolsTitle.Text = LocalizationHelper.Get("Tools_Section_Title", "🛠️ Administrative Tools");
            if (btnDevMgmtTool != null) btnDevMgmtTool.Content = LocalizationHelper.Get("Tool_DevMgmt", "Device Manager");
            if (btnNetplwiz != null) btnNetplwiz.Content = LocalizationHelper.Get("Tool_Netplwiz", "User Accounts");
            if (btnDiskMgmt != null) btnDiskMgmt.Content = LocalizationHelper.Get("Tool_DiskMgmt", "Disk Management");
            if (btnControl != null) btnControl.Content = LocalizationHelper.Get("Tool_Control", "Classic Control Panel");
            if (btnMsinfo != null) btnMsinfo.Content = LocalizationHelper.Get("Tool_Msinfo", "System Information");
            if (btnWinver != null) btnWinver.Content = LocalizationHelper.Get("Tool_Winver", "About Windows");
            if (btnMsconfig != null) btnMsconfig.Content = LocalizationHelper.Get("Tool_Msconfig", "MSCONFIG Tool");
            if (btnSysDmTool != null) btnSysDmTool.Content = LocalizationHelper.Get("Tool_SysDm", "System Properties");
            if (btnNcpa != null) btnNcpa.Content = LocalizationHelper.Get("Tool_Ncpa", "Network Connections");
            if (btnOptionalFeatures != null) btnOptionalFeatures.Content = LocalizationHelper.Get("Tool_OptionalFeatures", "Windows Features");
            if (btnRegedit != null) btnRegedit.Content = LocalizationHelper.Get("Tool_Regedit", "Registry Editor");
            if (btnTaskmgr != null) btnTaskmgr.Content = LocalizationHelper.Get("Tool_Taskmgr", "Task Manager");
            if (btnResmon != null) btnResmon.Content = LocalizationHelper.Get("Tool_Resmon", "Resource Monitor");
            if (btnMemoryDiagTool != null) btnMemoryDiagTool.Content = LocalizationHelper.Get("Tool_MemoryDiag", "Memory Diagnostic");
            if (btnRelMon != null) btnRelMon.Content = LocalizationHelper.Get("Tool_Reliability", "Reliability Monitor");
            if (btnEventVwr != null) btnEventVwr.Content = LocalizationHelper.Get("Tool_EventVwr", "Event Viewer");
            if (txtBtnGpeditText != null) txtBtnGpeditText.Text = LocalizationHelper.Get("Tool_Gpedit", "GPEDIT Tool (Pro/Ent)");
            if (btnGpedit != null) btnGpedit.ToolTip = LocalizationHelper.Get("Tooltip_Gpedit", "Group Policy Editor (gpedit.msc) - Available in Windows Pro & Enterprise editions");
            if (btnServicesTool != null)
            {
                btnServicesTool.Content = LocalizationHelper.Get("Tool_Services", "Windows Services");
                btnServicesTool.ToolTip = LocalizationHelper.Get("Tooltip_ToolServices", "Windows Services console (services.msc)");
            }
            if (btnDateTimeTool != null)
            {
                btnDateTimeTool.Content = LocalizationHelper.Get("Tool_DateTime", "Date & Time");
                btnDateTimeTool.ToolTip = LocalizationHelper.Get("Tooltip_ToolDateTime", "Windows Date & Time administrative settings (timedate.cpl)");
            }
            if (btnOpenMaintenanceFolderTool != null)
            {
                btnOpenMaintenanceFolderTool.Content = LocalizationHelper.Get("Btn_OpenMaintenanceFolder", "📁 Maintenance Tools");
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
            if (txtLogsTitle != null) txtLogsTitle.Text = LocalizationHelper.Get("Nav_Terminal", "📜  Logs");
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
            if (txtSettingsTitle != null) txtSettingsTitle.Text = LocalizationHelper.Get("Nav_Settings", "⚙️  Settings");
            if (txtSettingsSub != null) txtSettingsSub.Text = LocalizationHelper.Get("Settings_Subtitle", "Configure general preferences, language, visual theme, system installation, and modular add-ons");
            if (txtLangLabel != null) txtLangLabel.Text = LocalizationHelper.Get("Settings_Label_Language", "Application Language:");
            SetupLanguageButtons(langCode);

            if (chkSettingWinget != null) chkSettingWinget.Content = LocalizationHelper.Get("Settings_Chk_WingetFallback", "Enable Winget download fallback when program is not in Installers folder");
            if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.Content = LocalizationHelper.Get("Settings_Chk_AutoRefresh", "Automatically refresh hardware specs on startup");
            if (chkSettingExternalLogs != null) chkSettingExternalLogs.Content = LocalizationHelper.Get("Settings_Chk_ExternalLogs", "Open external Logs window by default");
            if (chkSettingAlwaysOnTop != null) chkSettingAlwaysOnTop.Content = LocalizationHelper.Get("Settings_Chk_AlwaysOnTop", "Keep OptiAssist main window always on top (Always on top)");
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

            // Install / Uninstall buttons (in main Settings card)
            if (btnInstallApp != null)
            {
                btnInstallApp.ToolTip = LocalizationHelper.Get("Tooltip_InstallApp", "Open installation & portable deployment options for OptiAssist");
            }
            if (btnUninstallApp != null)
            {
                btnUninstallApp.Content = LocalizationHelper.Get("Btn_UninstallApp", "🗑️ Uninstall OptiAssist");
                btnUninstallApp.ToolTip = LocalizationHelper.Get("Tooltip_UninstallApp", "Uninstall OptiAssist from this device and remove all shortcuts");
            }
            UpdateDeploymentButtonsUI();

            // 9. About
            if (txtAboutTitle != null) txtAboutTitle.Text = LocalizationHelper.Get("Nav_About", "ℹ️  About");
            if (txtAboutSub != null) txtAboutSub.Text = LocalizationHelper.Get("About_Subtitle", "Version information, copyright, and open-source license");
            if (txtAboutCreditsTitle != null) txtAboutCreditsTitle.Text = LocalizationHelper.Get("About_Credits_Title", "🤝 Acknowledgments & Credits");
            if (txtAboutCreditsText != null) txtAboutCreditsText.Text = LocalizationHelper.Get("About_Credits_Text", "Special thanks and credit to Chris Titus Tech and the open-source community behind WinUtil, whose work served as an essential inspiration and foundation for the implementation of several system maintenance and optimization features in OptiAssist.");
            if (btnAboutGithub != null) btnAboutGithub.Content = LocalizationHelper.Get("Btn_AboutGithub", "🌐 GitHub Repository");
            if (btnAboutWebsite != null) btnAboutWebsite.Content = LocalizationHelper.Get("Btn_AboutWebsite", "🔗 Official Website");
            if (btnAboutDocumentation != null)
            {
                btnAboutDocumentation.Content = LocalizationHelper.Get("Btn_AboutDocumentation", "📖 Documentation");
                btnAboutDocumentation.ToolTip = LocalizationHelper.Get("Tooltip_AboutDocumentation", "Access the official OptiAssist documentation");
            }
            if (btnAboutLicense != null)
            {
                btnAboutLicense.Content = LocalizationHelper.Get("Btn_AboutLicense", "⚖️ License");
                btnAboutLicense.ToolTip = LocalizationHelper.Get("Tooltip_AboutLicense", "View GNU GPLv3 license notice");
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
                if (lblOsInstallDate != null) lblOsInstallDate.Text = LocalizationHelper.Get("Label_OsInstallDate", "Install Date: ") + detecting;
                if (lblOsActivation != null) lblOsActivation.Text = LocalizationHelper.Get("Label_OsActivation", "Activation: ") + detecting;
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

        private void ChkSettingAlwaysOnTop_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.AlwaysOnTop = chkSettingAlwaysOnTop.IsChecked == true;
            this.Topmost = _settings.AlwaysOnTop;
            SettingsHelper.Save(_settings);

            Log(string.Format("[CONFIG] AlwaysOnTop updated to {0} and saved to settings.json.", _settings.AlwaysOnTop));
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) _settings = new AppSettings();
            _settings.CustomerName = txtSettingStore != null ? txtSettingStore.Text.Trim() : "";
            _settings.EnableWingetFallback = chkSettingWinget != null && chkSettingWinget.IsChecked == true;
            _settings.AutoRefreshHardware = chkSettingAutoRefresh != null && chkSettingAutoRefresh.IsChecked == true;
            _settings.OpenExternalLogsByDefault = chkSettingExternalLogs != null && chkSettingExternalLogs.IsChecked == true;
            _settings.AlwaysOnTop = chkSettingAlwaysOnTop != null && chkSettingAlwaysOnTop.IsChecked == true;
            _settings.AccentColor = txtSettingAccentColor.Text.Trim();

            this.Topmost = _settings.AlwaysOnTop;
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
                    bool showUninstall = isInstalled || runningFromInstalled;
                    btnUninstallApp.Visibility = showUninstall ? Visibility.Visible : Visibility.Collapsed;
                    btnUninstallApp.IsEnabled = showUninstall;
                }
            }
            catch { }
        }

        private void BtnInstallApp_Click(object sender, RoutedEventArgs e)
        {
            Log("[INSTALL] Opening OptiAssist installation & deployment window...");
            AppDeploymentHelper.ShowInstallDialog(this, Log);
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

        private bool _isDarkMode = false;
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

        private void BtnAboutLicense_Click(object sender, RoutedEventArgs e)
        {
            AppDeploymentHelper.ShowLicenseDialog(this);
        }
        #endregion
        #endregion
    }
}
