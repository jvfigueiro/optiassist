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
        private ObservableCollection<InstallAppItem> _apps;
        private ObservableCollection<InstallAppItem> _customEditorApps;
        private ObservableCollection<WingetUpgradeItem> _wingetUpgrades;
        private InstallAppItem _editingAppItem;
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
            LoadPostInstallApps();
            ReloadAddonsList();

            if (_settings != null && _settings.AutoRefreshHardware)
            {
                RefreshHardwareAsync();
            }

            // Start background check for Winget updates
            BtnCheckWingetUpgrades_Click(null, null);
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
        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb == null) return;

            // Hide all panels
            scrollViewerContent.Visibility = Visibility.Collapsed;
            panelDashboard.Visibility = Visibility.Collapsed;
            panelPostInstall.Visibility = Visibility.Collapsed;
            panelOptimization.Visibility = Visibility.Collapsed;
            panelShortcuts.Visibility = Visibility.Collapsed;
            panelAddonHost.Visibility = Visibility.Collapsed;
            panelReport.Visibility = Visibility.Collapsed;
            panelTerminal.Visibility = Visibility.Collapsed;
            panelSettings.Visibility = Visibility.Collapsed;
            panelAbout.Visibility = Visibility.Collapsed;

            // Display corresponding panel
            if (rb == rbNavDashboard)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelDashboard.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavPostInstall)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelPostInstall.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavOptimization)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelOptimization.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavShortcuts)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelShortcuts.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavReport)
            {
                scrollViewerContent.Visibility = Visibility.Visible;
                panelReport.Visibility = Visibility.Visible;
            }
            else if (rb == rbNavTerminal)
            {
                panelTerminal.Visibility = Visibility.Visible;
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
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtConsoleLog.Clear();
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

                if (chkServiceProviderMode != null)
                    chkServiceProviderMode.IsChecked = _settings.ServiceProviderMode;
                if (chkEnableMaintenanceReport != null)
                    chkEnableMaintenanceReport.IsChecked = _settings.EnableMaintenanceReport;

                txtSettingStore.Text = _settings.CustomerName;
                txtSettingPhone.Text = _settings.ContactInfo;
                txtSettingReportPath.Text = string.IsNullOrEmpty(_settings.ReportSavePath) ? @"C:\MaintenanceReport.txt" : _settings.ReportSavePath;
                if (chkSettingWinget != null) chkSettingWinget.IsChecked = _settings.EnableWingetFallback;
                if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.IsChecked = _settings.AutoRefreshHardware;

                string currentLang = string.IsNullOrEmpty(_settings.Language) ? "en-US" : _settings.Language;
                ApplyLanguage(currentLang);

                txtReportOrder.Text = ""; // Kept empty by request

                UpdateServiceProviderUI();

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

            bool isProvider = chkServiceProviderMode != null && chkServiceProviderMode.IsChecked == true;
            bool enableReport = chkEnableMaintenanceReport != null && chkEnableMaintenanceReport.IsChecked == true;

            if (pnlServiceProviderFields != null)
            {
                pnlServiceProviderFields.Visibility = isProvider ? Visibility.Visible : Visibility.Collapsed;
            }

            // Show or hide "Maintenance Report" in sidebar menu
            if (rbNavReport != null)
            {
                bool showReport = isProvider && enableReport;
                rbNavReport.Visibility = showReport ? Visibility.Visible : Visibility.Collapsed;

                if (!showReport && rbNavReport.IsChecked == true && rbNavDashboard != null)
                {
                    rbNavDashboard.IsChecked = true;
                    NavButton_Click(rbNavDashboard, null);
                }
            }

            // Update top bar application branding
            if (lblAppTitle != null)
            {
                string customName = _settings.CustomerName;
                if (txtSettingStore != null && !string.IsNullOrWhiteSpace(txtSettingStore.Text))
                {
                    customName = txtSettingStore.Text.Trim();
                }

                if (isProvider && !string.IsNullOrWhiteSpace(customName) && customName != "N/A")
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

        private void LoadPostInstallApps()
        {
            var appsList = InstallerHelper.LoadApps();
            foreach (var app in appsList)
            {
                app.Selected = app.PreSelected;
            }
            _apps = new ObservableCollection<InstallAppItem>(appsList);
            icAppsList.ItemsSource = _apps;
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

            // Auto-fill serial number in maintenance report form if empty
            if (string.IsNullOrEmpty(txtReportSerial.Text) || txtReportSerial.Text == "Não informado" || txtReportSerial.Text == "Not informed" || txtReportSerial.Text == "Not available")
            {
                txtReportSerial.Text = info.SerialNumber;
            }
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
            if (rbNavPostInstall != null)
            {
                rbNavPostInstall.IsChecked = true;
                NavButton_Click(rbNavPostInstall, null);
            }
            BtnInstallSelected_Click(sender, e);
        }

        private void BtnTaskPostInstall_Click(object sender, RoutedEventArgs e)
        {
            rbNavPostInstall.IsChecked = true;
            NavButton_Click(rbNavPostInstall, null);
        }
        #endregion

        #region APP MANAGER (POST-INSTALL)
        private void BtnSelectEssentials_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) return;
            foreach (var app in _apps)
            {
                app.Selected = app.PreSelected;
            }
            icAppsList.Items.Refresh();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) return;
            foreach (var app in _apps)
            {
                app.Selected = false;
            }
            icAppsList.Items.Refresh();
        }

        private void BtnToggleCustomizePostInstall_Click(object sender, RoutedEventArgs e)
        {
            if (cardCustomizePostInstall.Visibility == Visibility.Visible)
            {
                cardCustomizePostInstall.Visibility = Visibility.Collapsed;
            }
            else
            {
                cardCustomizePostInstall.Visibility = Visibility.Visible;
                PopulateCustomAppsEditor();
            }
        }

        private void BtnCloseCustomize_Click(object sender, RoutedEventArgs e)
        {
            cardCustomizePostInstall.Visibility = Visibility.Collapsed;
        }

        private void PopulateCustomAppsEditor()
        {
            var list = InstallerHelper.LoadApps();
            _customEditorApps = new ObservableCollection<InstallAppItem>(list);
            icCustomAppsEditorList.ItemsSource = _customEditorApps;
        }

        private void CmbNewAppType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelNewAppLocal == null || panelNewAppWinget == null) return;
            var item = cmbNewAppType.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null && item.Tag.ToString() == "Winget")
            {
                panelNewAppLocal.Visibility = Visibility.Collapsed;
                panelNewAppWinget.Visibility = Visibility.Visible;
            }
            else
            {
                panelNewAppLocal.Visibility = Visibility.Visible;
                panelNewAppWinget.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSelectLocalInstaller_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string postDir = InstallerHelper.GetPostInstallDirectory();
                if (!Directory.Exists(postDir)) Directory.CreateDirectory(postDir);

                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.InitialDirectory = Path.GetFullPath(postDir);
                dlg.Title = LocalizationHelper.Get("Msg_SelectInstallerTitle", "Select installer in PostInstall folder");
                dlg.Filter = LocalizationHelper.Get("Msg_SelectInstallerFilter", "Installers (*.exe;*.msi;*.msixbundle;*.appxbundle)|*.exe;*.msi;*.msixbundle;*.appxbundle|All Files (*.*)|*.*");
                if (dlg.ShowDialog() == true)
                {
                    string fileName = Path.GetFileName(dlg.FileName);
                    txtNewAppLocalFile.Text = fileName;
                    if (string.IsNullOrWhiteSpace(txtNewAppName.Text))
                    {
                        string clean = Path.GetFileNameWithoutExtension(fileName)
                            .Replace("Setup", "")
                            .Replace("setup", "")
                            .Replace("_", " ")
                            .Trim();
                        if (!string.IsNullOrEmpty(clean))
                            txtNewAppName.Text = clean;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_SelectInstallerError", "Error selecting file: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
        {
            string name = txtNewAppName.Text != null ? txtNewAppName.Text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_EnterAppName", "Please enter the program name."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewAppName.Focus();
                return;
            }

            string installType = "Local";
            var selectedTypeItem = cmbNewAppType.SelectedItem as ComboBoxItem;
            if (selectedTypeItem != null && selectedTypeItem.Tag != null)
            {
                installType = selectedTypeItem.Tag.ToString();
            }

            string localFile = txtNewAppLocalFile.Text != null ? txtNewAppLocalFile.Text.Trim() : "";
            string wingetId = txtNewAppWingetId.Text != null ? txtNewAppWingetId.Text.Trim() : "";
            string silentArgs = txtNewAppSilentArgs.Text != null ? txtNewAppSilentArgs.Text.Trim() : "";
            bool preSelected = chkNewAppPreSelected.IsChecked == true;

            if (installType == "Winget" && string.IsNullOrEmpty(wingetId))
            {
                wingetId = name;
            }

            if (_customEditorApps == null)
            {
                _customEditorApps = new ObservableCollection<InstallAppItem>();
                icCustomAppsEditorList.ItemsSource = _customEditorApps;
            }

            if (_editingAppItem != null)
            {
                // Edit mode: update existing item
                _editingAppItem.Name = name;
                _editingAppItem.InstallType = installType;
                _editingAppItem.LocalInstallerFile = localFile;
                _editingAppItem.WingetId = wingetId;
                _editingAppItem.PreSelected = preSelected;
                _editingAppItem.SilentArgs = silentArgs;

                _editingAppItem = null;
                btnAddCustomApp.Content = LocalizationHelper.Get("Btn_Add", "➕ Adicionar");
                btnCancelEditApp.Visibility = Visibility.Collapsed;
                icCustomAppsEditorList.Items.Refresh();
            }
            else
            {
                // Add mode: add new item
                var newItem = new InstallAppItem(name, preSelected, installType, wingetId, silentArgs, localFile);
                _customEditorApps.Add(newItem);
            }

            // Clear form fields
            ClearCustomAppForm();
        }

        private void BtnEditCustomApp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as InstallAppItem;
            if (item == null) return;

            _editingAppItem = item;
            txtNewAppName.Text = item.Name;
            cmbNewAppType.SelectedIndex = (item.InstallType == "Winget") ? 1 : 0;
            txtNewAppLocalFile.Text = item.LocalInstallerFile;
            txtNewAppWingetId.Text = item.WingetId;
            chkNewAppPreSelected.IsChecked = item.PreSelected;
            txtNewAppSilentArgs.Text = item.SilentArgs;

            btnAddCustomApp.Content = LocalizationHelper.Get("Btn_Update", "💾 Atualizar");
            btnCancelEditApp.Visibility = Visibility.Visible;
            txtNewAppName.Focus();
        }

        private void BtnCancelEditApp_Click(object sender, RoutedEventArgs e)
        {
            _editingAppItem = null;
            ClearCustomAppForm();
            btnAddCustomApp.Content = LocalizationHelper.Get("Btn_Add", "➕ Adicionar");
            btnCancelEditApp.Visibility = Visibility.Collapsed;
        }

        private void ClearCustomAppForm()
        {
            txtNewAppName.Text = "";
            txtNewAppLocalFile.Text = "";
            txtNewAppWingetId.Text = "";
            txtNewAppSilentArgs.Text = "";
            chkNewAppPreSelected.IsChecked = false;
            cmbNewAppType.SelectedIndex = 0;
        }

        private void BtnRemoveCustomApp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as InstallAppItem;
            if (item != null && _customEditorApps != null)
            {
                _customEditorApps.Remove(item);
            }
        }

        private void BtnMoveAppUp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as InstallAppItem;
            if (item != null && _customEditorApps != null)
            {
                int index = _customEditorApps.IndexOf(item);
                if (index > 0)
                {
                    _customEditorApps.Move(index, index - 1);
                }
            }
        }

        private void BtnMoveAppDown_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as InstallAppItem;
            if (item != null && _customEditorApps != null)
            {
                int index = _customEditorApps.IndexOf(item);
                if (index >= 0 && index < _customEditorApps.Count - 1)
                {
                    _customEditorApps.Move(index, index + 1);
                }
            }
        }

        private void BtnSaveCustomApps_Click(object sender, RoutedEventArgs e)
        {
            if (_customEditorApps == null) return;
            InstallerHelper.SaveApps(_customEditorApps);
            LoadPostInstallApps();
            MessageBox.Show(LocalizationHelper.Get("Msg_CustomAppsSaved", "Application list successfully saved in Config\\autoinstall.json!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnResetDefaultApps_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(LocalizationHelper.Get("Msg_ResetDefaultAppsConfirm", "Do you want to restore the default program list in autoinstall.json?"), "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var defaults = InstallerHelper.GetDefaultApps();
                InstallerHelper.SaveApps(defaults);
                PopulateCustomAppsEditor();
                LoadPostInstallApps();
                MessageBox.Show(LocalizationHelper.Get("Msg_ResetDefaultAppsDone", "Default program list successfully restored!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOpenPostInstallJson_Click(object sender, RoutedEventArgs e)
        {
            InstallerHelper.OpenPostInstallJson();
        }

        private void BtnOpenPostInstallFolder_Click(object sender, RoutedEventArgs e)
        {
            InstallerHelper.OpenPostInstallFolder();
        }

        private void BtnInstallSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) return;

            List<InstallAppItem> selected = new List<InstallAppItem>();
            foreach (var app in _apps)
            {
                if (app.Selected) selected.Add(app);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_NoAppsSelected", "No application was selected."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnRunInstallSelected.IsEnabled = false;
            pbInstallProgress.Value = 0;
            pbInstallProgress.Maximum = selected.Count;

            ThreadPool.QueueUserWorkItem(state =>
            {
                Log("=================================================");
                Log("   STARTING AUTOMATED SOFTWARE INSTALLATION      ");
                Log(string.Format("   Total programs to install: {0}", selected.Count));
                Log("   (Configuration: Config\\autoinstall.json)");
                Log("=================================================\n");

                bool wingetFallback = _settings != null ? _settings.EnableWingetFallback : true;

                int current = 0;
                foreach (var app in selected)
                {
                    current++;
                    Log(string.Format("[{0}/{1}] Processing: {2}...", current, selected.Count, app.Name));
                    InstallerHelper.InstallApp(app, wingetFallback, msg => Log(msg));
                    Log("");

                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (pbInstallProgress.Value < pbInstallProgress.Maximum)
                            pbInstallProgress.Value++;
                    }));
                }

                Log("=================================================");
                Log("   PROGRAM INSTALLATION COMPLETED!               ");
                Log("=================================================");

                Dispatcher.Invoke(new Action(() =>
                {
                    btnRunInstallSelected.IsEnabled = true;
                    MessageBox.Show(LocalizationHelper.Get("Msg_InstallCompleted", "Installation of selected programs completed!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            });
        }

        #region APPLICATION UPDATES (WINGET UPGRADE)
        private enum WingetStatusState { Initial, Scanning, UpToDate, Found, Updating, UpdatingAll }
        private WingetStatusState _wingetStatusState = WingetStatusState.Initial;
        private int _wingetFoundCount = 0;
        private int _wingetUpdatingCount = 0;

        private void UpdateWingetStatusText()
        {
            if (lblWingetUpgradeStatus == null) return;
            switch (_wingetStatusState)
            {
                case WingetStatusState.Initial:
                    lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_Initial", "Click 'Check for Updates' or wait for winget scan...");
                    break;
                case WingetStatusState.Scanning:
                    lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_Scanning", "Scanning for available updates via winget...");
                    break;
                case WingetStatusState.UpToDate:
                    lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_UpToDate", "✔️ All installed applications are already up to date!");
                    break;
                case WingetStatusState.Found:
                    lblWingetUpgradeStatus.Text = string.Format(LocalizationHelper.Get("Upgrade_Status_Found", "Updates found for {0} application(s):"), _wingetFoundCount);
                    break;
                case WingetStatusState.Updating:
                    lblWingetUpgradeStatus.Text = string.Format(LocalizationHelper.Get("Upgrade_Status_Updating", "Updating {0} selected application(s)... Track progress in Logs."), _wingetUpdatingCount);
                    break;
                case WingetStatusState.UpdatingAll:
                    lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_UpdatingAll", "Running 'winget upgrade --all' in background... Track progress in Logs.");
                    break;
            }
        }

        private void BtnCheckWingetUpgrades_Click(object sender, RoutedEventArgs e)
        {
            cardWingetUpgrades.Visibility = Visibility.Visible;
            pbWingetScan.Visibility = Visibility.Visible;
            _wingetStatusState = WingetStatusState.Scanning;
            UpdateWingetStatusText();
            borderUpgradesList.Visibility = Visibility.Collapsed;
            gridUpgradeActions.Visibility = Visibility.Collapsed;

            ThreadPool.QueueUserWorkItem(state =>
            {
                var upgrades = InstallerHelper.CheckAvailableUpgrades(msg => Log(msg));

                Dispatcher.Invoke(new Action(() =>
                {
                    pbWingetScan.Visibility = Visibility.Collapsed;

                    if (upgrades == null || upgrades.Count == 0)
                    {
                        _wingetStatusState = WingetStatusState.UpToDate;
                        UpdateWingetStatusText();
                        borderUpgradesList.Visibility = Visibility.Collapsed;
                        gridUpgradeActions.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _wingetFoundCount = upgrades.Count;
                        _wingetStatusState = WingetStatusState.Found;
                        UpdateWingetStatusText();
                        _wingetUpgrades = new ObservableCollection<WingetUpgradeItem>(upgrades);
                        icWingetUpgradesList.ItemsSource = _wingetUpgrades;
                        borderUpgradesList.Visibility = Visibility.Visible;
                        gridUpgradeActions.Visibility = Visibility.Visible;
                    }
                }));
            });
        }

        private void BtnCloseWingetUpgrades_Click(object sender, RoutedEventArgs e)
        {
            cardWingetUpgrades.Visibility = Visibility.Collapsed;
        }

        private void BtnSelectAllUpgrades_Click(object sender, RoutedEventArgs e)
        {
            if (_wingetUpgrades == null) return;
            foreach (var item in _wingetUpgrades)
            {
                item.Selected = true;
            }
        }

        private void BtnDeselectAllUpgrades_Click(object sender, RoutedEventArgs e)
        {
            if (_wingetUpgrades == null) return;
            foreach (var item in _wingetUpgrades)
            {
                item.Selected = false;
            }
        }

        private void BtnRecheckWingetUpgrades_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckWingetUpgrades_Click(sender, e);
        }

        private void BtnUpgradeSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_wingetUpgrades == null) return;

            List<WingetUpgradeItem> selected = new List<WingetUpgradeItem>();
            foreach (var item in _wingetUpgrades)
            {
                if (item.Selected) selected.Add(item);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_NoAppsSelected", "No application was selected for update."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnUpgradeSelected.IsEnabled = false;
            btnUpgradeAll.IsEnabled = false;
            pbWingetScan.Visibility = Visibility.Visible;
            pnlWingetLogsAction.Visibility = Visibility.Visible;
            _wingetUpdatingCount = selected.Count;
            _wingetStatusState = WingetStatusState.Updating;
            UpdateWingetStatusText();

            ThreadPool.QueueUserWorkItem(state =>
            {
                Log("=================================================");
                Log("   STARTING APPLICATION UPDATE (WINGET)          ");
                Log(string.Format("   Total programs to update: {0}", selected.Count));
                Log("=================================================\n");

                int current = 0;
                int successes = 0;
                foreach (var item in selected)
                {
                    current++;
                    Log(string.Format("[{0}/{1}] Updating {2} ({3})...", current, selected.Count, item.Name, item.Id));
                    bool ok = InstallerHelper.UpgradeWingetPackage(item.Id, Log);
                    if (ok) successes++;
                    Log("");
                }

                Log("=================================================");
                Log(string.Format("   UPDATES COMPLETED ({0}/{1} successful)        ", successes, selected.Count));
                Log("=================================================\n");

                Dispatcher.Invoke(new Action(() =>
                {
                    btnUpgradeSelected.IsEnabled = true;
                    btnUpgradeAll.IsEnabled = true;
                    pbWingetScan.Visibility = Visibility.Collapsed;
                    pnlWingetLogsAction.Visibility = Visibility.Collapsed;
                    MessageBox.Show(string.Format(LocalizationHelper.Get("Msg_UpgradeCompleted", "Application update completed!\n{0} of {1} successfully updated."), successes, selected.Count),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Re-scan updates to refresh list
                    BtnCheckWingetUpgrades_Click(null, null);
                }));
            });
        }

        private void BtnUpgradeAll_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(LocalizationHelper.Get("Msg_UpgradeAllConfirm", "Do you want to update all system programs now via 'winget upgrade --all'?"),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            btnUpgradeSelected.IsEnabled = false;
            btnUpgradeAll.IsEnabled = false;
            pbWingetScan.Visibility = Visibility.Visible;
            pnlWingetLogsAction.Visibility = Visibility.Visible;
            _wingetStatusState = WingetStatusState.UpdatingAll;
            UpdateWingetStatusText();

            ThreadPool.QueueUserWorkItem(state =>
            {
                Log("=================================================");
                Log("   RUNNING BULK UPDATE: winget upgrade --all     ");
                Log("=================================================\n");

                bool ok = InstallerHelper.UpgradeAllWingetPackages(Log);

                Log("=================================================");
                Log(ok ? "   BULK UPDATE FINISHED SUCCESSFULLY!            " : "   BULK UPDATE FINISHED WITH WARNINGS/ERRORS     ");
                Log("=================================================\n");

                Dispatcher.Invoke(new Action(() =>
                {
                    btnUpgradeSelected.IsEnabled = true;
                    btnUpgradeAll.IsEnabled = true;
                    pbWingetScan.Visibility = Visibility.Collapsed;
                    pnlWingetLogsAction.Visibility = Visibility.Collapsed;
                    MessageBox.Show(LocalizationHelper.Get("Msg_UpgradeAllFinished", "Bulk update command finished! Check details in Logs tab."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Re-scan updates to refresh list
                    BtnCheckWingetUpgrades_Click(null, null);
                }));
            });
        }

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            rbNavTerminal.IsChecked = true;
            NavButton_Click(rbNavTerminal, null);
        }
        #endregion
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

        private void BtnMassgrave_Click(object sender, RoutedEventArgs e)
        {
            Log("[TOOL] Launching Massgrave (irm https://get.activated.win | iex)...");
            ShortcutsHelper.OpenMassgrave();
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

        private void ReloadAddonsList()
        {
            try
            {
                List<string> disabled = _settings != null && _settings.DisabledAddons != null ? _settings.DisabledAddons : new List<string>();
                List<AddonEntry> list = AddonHelper.LoadDiscoveredAddons(this, disabled);
                _addons = new ObservableCollection<AddonEntry>(list);
                if (icAddonsList != null) icAddonsList.ItemsSource = _addons;

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
                rb.Style = (Style)FindResource("NavRadioButtonStyle");
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

        #region MAINTENANCE REPORT (SERVICE ORDER)
        private void TxtReportNotes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtReportNotes.Text))
            {
                txtReportNotes.Text = LocalizationHelper.Get("Report_Notes_Placeholder", "You can provide further details about the services performed on this machine here...");
                txtReportNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            }
        }

        private void TxtReportNotes_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtReportNotes.Text.StartsWith("Você pode informar") ||
                txtReportNotes.Text.StartsWith("You can enter") ||
                txtReportNotes.Text.StartsWith("You can provide") ||
                txtReportNotes.Text.StartsWith("Enter technical observations"))
            {
                txtReportNotes.Text = "";
                txtReportNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            }
        }

        private MaintenanceRecord BuildRecordFromUI()
        {
            MaintenanceRecord rec = new MaintenanceRecord();
            rec.CustomerName = _settings != null ? _settings.CustomerName : "OptiAssist";
            rec.ContactInfo = _settings != null ? _settings.ContactInfo : "";
            rec.OrderNumber = txtReportOrder.Text.Trim();
            rec.ClientName = txtReportClient.Text.Trim();
            rec.ClientPhone = "";
            rec.SerialNumber = txtReportSerial.Text.Trim();
            rec.ExecutionDate = DateTime.Now;
            rec.Hardware = _hardware;
            rec.Notes = txtReportNotes.Text.Trim();

            rec.ActionsDone = new List<string>();
            if (chkActSmart.IsChecked == true) rec.ActionsDone.Add(chkActSmart.Content.ToString());
            if (chkActClean.IsChecked == true) rec.ActionsDone.Add(chkActClean.Content.ToString());
            if (chkActFormat.IsChecked == true) rec.ActionsDone.Add(chkActFormat.Content.ToString());
            if (chkActDrivers.IsChecked == true) rec.ActionsDone.Add(chkActDrivers.Content.ToString());
            if (chkActApps.IsChecked == true) rec.ActionsDone.Add(chkActApps.Content.ToString());
            if (chkActTemp.IsChecked == true) rec.ActionsDone.Add(chkActTemp.Content.ToString());
            if (chkActDism.IsChecked == true) rec.ActionsDone.Add(chkActDism.Content.ToString());
            if (chkActNet.IsChecked == true) rec.ActionsDone.Add(chkActNet.Content.ToString());

            return rec;
        }

        private void BtnSaveReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();

            // 1. Always generate in application Reports folder
            string reportsFilePath = ReportHelper.SaveReportToReportsFolder(rec);
            Log("[REPORT] Record issued in Reports folder: " + reportsFilePath);

            // 2. Ask user whether to save an extra copy on machine
            MessageBoxResult promptCopy = MessageBox.Show(
                LocalizationHelper.Get("Msg_ReportPromptCopy", "The maintenance report has been generated in the Reports folder.\n\nDo you want to save a copy of this report on the user's machine?"),

                "OptiAssist",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            string clientCopyPath = null;
            if (promptCopy == MessageBoxResult.Yes)
            {
                string configuredPath = _settings != null && !string.IsNullOrWhiteSpace(_settings.ReportSavePath) 
                    ? _settings.ReportSavePath 
                    : @"C:\MaintenanceReport.txt";

                clientCopyPath = ReportHelper.SaveReportCopy(rec, configuredPath);
                Log("[REPORT] Copy saved on the user's machine: " + clientCopyPath);
            }

            // 3. Display confirmation and option to open
            string msg;
            if (!string.IsNullOrEmpty(clientCopyPath))
            {
                msg = "Maintenance report saved successfully!\n\n" +
                      "• Reports folder (application): " + reportsFilePath + "\n" +
                      "• Copy on machine: " + clientCopyPath + "\n\n" +
                      "Do you want to open the report file now?";
            }
            else
            {
                msg = "Maintenance report issued successfully in the Reports folder!\n\n" +
                      "• File: " + reportsFilePath + "\n\n" +
                      "Do you want to open the report file now?";
            }

            MessageBoxResult openRes = MessageBox.Show(msg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (openRes == MessageBoxResult.Yes)
            {
                try
                {
                    string pathToOpen = !string.IsNullOrEmpty(clientCopyPath) ? clientCopyPath : reportsFilePath;
                    Process.Start(pathToOpen);
                }
                catch
                {
                    try { Process.Start(reportsFilePath); } catch { }
                }
            }
        }

        private void BtnCopyReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();
            string text = ReportHelper.GenerateReportText(rec);
            Clipboard.SetText(text);
            MessageBox.Show(LocalizationHelper.Get("Msg_ReportCopied", "Full maintenance report copied to clipboard!\nYou can paste it into WhatsApp, email, or your management system."),
                "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("[REPORT] Technical report copied to the clipboard.");
        }

        private void BtnOpenReportFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                if (!Directory.Exists(reportsDir)) Directory.CreateDirectory(reportsDir);
                Process.Start("explorer.exe", reportsDir);
            }
            catch { }
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

            // 0. Window Title
            this.Title = LocalizationHelper.Get("App_Title", "OptiAssist");

            // 1. Sidebar Navigation
            if (rbNavDashboard != null) rbNavDashboard.Content = LocalizationHelper.Get("Nav_Dashboard", "💻  Dashboard");
            if (rbNavPostInstall != null) rbNavPostInstall.Content = LocalizationHelper.Get("Nav_PostInstall", "📦  App Manager");
            if (rbNavOptimization != null) rbNavOptimization.Content = LocalizationHelper.Get("Nav_Optimization", "⚡  Optimization");
            if (rbNavShortcuts != null) rbNavShortcuts.Content = LocalizationHelper.Get("Nav_Shortcuts", "🛠️  Windows Tools");
            if (rbNavReport != null) rbNavReport.Content = LocalizationHelper.Get("Nav_Report", "📋  Maintenance Report");
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
            if (btnTaskMassgrave != null)
            {
                btnTaskMassgrave.Content = LocalizationHelper.Get("Task_Massgrave", "🔑 Massgrave (MAS)");
                btnTaskMassgrave.ToolTip = LocalizationHelper.Get("Tooltip_TaskMassgrave", "Run Microsoft MAS activator via PowerShell");
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

            // 3. App Manager (PostInstall)
            if (txtPostInstallTitle != null) txtPostInstallTitle.Text = LocalizationHelper.Get("PostInstall_Title", "App Manager");
            if (txtPostInstallSubtitle != null) txtPostInstallSubtitle.Text = LocalizationHelper.Get("PostInstall_Subtitle", "Automated installation of essential software and update of installed applications");
            if (txtWingetCardTitle != null) txtWingetCardTitle.Text = LocalizationHelper.Get("Upgrade_Card_Title", "🔄 Application Updates");
            UpdateWingetStatusText();
            if (btnCheckWingetUpgrades != null) btnCheckWingetUpgrades.Content = LocalizationHelper.Get("Btn_CheckUpdates", "🔄 Check for Updates");
            if (btnViewLogsFromUpgrade != null)
            {
                btnViewLogsFromUpgrade.Content = LocalizationHelper.Get("Btn_ViewLogs", "⬛ View Logs");
                btnViewLogsFromUpgrade.ToolTip = LocalizationHelper.Get("Tooltip_ViewLogs", "Monitor execution log in real time on the Logs screen");
            }
            if (btnSelectAllUpgrades != null) btnSelectAllUpgrades.Content = LocalizationHelper.Get("Btn_SelectAll", "Select All");
            if (btnDeselectAllUpgrades != null) btnDeselectAllUpgrades.Content = LocalizationHelper.Get("Btn_DeselectAll", "Deselect All");
            if (btnRecheckWingetUpgrades != null) btnRecheckWingetUpgrades.Content = LocalizationHelper.Get("Btn_Recheck", "🔄 Re-check");
            if (btnUpgradeAll != null) btnUpgradeAll.Content = LocalizationHelper.Get("Btn_UpgradeAll", "🚀 Update All");
            if (btnUpgradeSelected != null) btnUpgradeSelected.Content = LocalizationHelper.Get("Btn_UpgradeSelected", "🔄 Update Selected");

            if (txtCustomizeHeaderTitle != null) txtCustomizeHeaderTitle.Text = LocalizationHelper.Get("Custom_Card_Title", "⚙️ Customize automated application installation");
            if (txtCustomizeHeaderSub != null) txtCustomizeHeaderSub.Text = LocalizationHelper.Get("Custom_Card_Subtitle", "Add new programs, set default automation, and choose between Local installer or Winget.");
            if (btnCloseCustomize != null) btnCloseCustomize.Content = LocalizationHelper.Get("Btn_Close", "✕ Close");
            if (txtNewAppNameLabel != null) txtNewAppNameLabel.Text = LocalizationHelper.Get("Custom_Label_Name", "Program Name:");
            if (txtNewAppTypeLabel != null) txtNewAppTypeLabel.Text = LocalizationHelper.Get("Custom_Label_Type", "Installation Type:");
            if (cmbItemLocal != null) cmbItemLocal.Content = LocalizationHelper.Get("Custom_Type_Local", "📁 Local (PostInstall)");
            if (cmbItemWinget != null) cmbItemWinget.Content = LocalizationHelper.Get("Custom_Type_Winget", "🌐 Winget (Repository)");
            if (txtNewAppLocalLabel != null) txtNewAppLocalLabel.Text = LocalizationHelper.Get("Custom_Label_LocalFile", "Executable in PostInstall folder:");
            if (btnSelectLocalInstaller != null)
            {
                btnSelectLocalInstaller.Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Browse...");
                btnSelectLocalInstaller.ToolTip = LocalizationHelper.Get("Tooltip_SelectLocalInstaller", "Select executable file from PostInstall folder");
            }
            if (txtNewAppLocalFile != null) txtNewAppLocalFile.ToolTip = LocalizationHelper.Get("Tooltip_NewAppLocalFile", "Name of the executable in PostInstall folder (e.g. OfficeSetup.exe)");
            if (txtNewAppWingetLabel != null) txtNewAppWingetLabel.Text = LocalizationHelper.Get("Custom_Label_WingetId", "Winget App ID (e.g. Google.Chrome):");
            if (txtNewAppWingetId != null) txtNewAppWingetId.ToolTip = LocalizationHelper.Get("Tooltip_NewAppWingetId", "Package identifier in Winget repository");
            if (btnAddCustomApp != null)
            {
                btnAddCustomApp.Content = (_editingAppItem != null) ? LocalizationHelper.Get("Btn_Update", "💾 Update") : LocalizationHelper.Get("Btn_Add", "➕ Add");
            }
            if (btnCancelEditApp != null) btnCancelEditApp.ToolTip = LocalizationHelper.Get("Tooltip_CancelEdit", "Cancel edit");
            if (chkNewAppPreSelected != null) chkNewAppPreSelected.Content = LocalizationHelper.Get("Custom_Chk_AutomateDefault", "Automate by default");
            if (txtNewAppSilentLabel != null) txtNewAppSilentLabel.Text = LocalizationHelper.Get("Custom_Label_SilentArgs", "Silent arguments (optional):");
            if (txtCurrentListTitle != null) txtCurrentListTitle.Text = LocalizationHelper.Get("Custom_CurrentList_Title", "Programs in current list:");
            if (btnSaveCustomApps != null) btnSaveCustomApps.Content = LocalizationHelper.Get("Btn_SaveCustomApps", "💾 Save Changes");
            if (btnResetDefaultApps != null) btnResetDefaultApps.Content = LocalizationHelper.Get("Btn_ResetDefaultApps", "🔄 Reset to Defaults");
            if (btnOpenPostInstallJson != null) btnOpenPostInstallJson.Content = LocalizationHelper.Get("Btn_OpenPostInstallJson", "📂 Open autoinstall.json");

            if (_customEditorApps != null)
            {
                foreach (var item in _customEditorApps)
                {
                    item.RefreshLocalization();
                }
                if (icCustomAppsEditorList != null && icCustomAppsEditorList.Items != null)
                {
                    icCustomAppsEditorList.Items.Refresh();
                }
            }

            if (txtAppsInstallCardTitle != null) txtAppsInstallCardTitle.Text = LocalizationHelper.Get("Install_Card_Title", "📦 Automated Application Installation");
            if (txtAppsInstallCardSub != null) txtAppsInstallCardSub.Text = LocalizationHelper.Get("Install_Card_Subtitle", "Select the programs you want to batch install automatically on this computer:");
            if (btnSelectEssentials != null) btnSelectEssentials.Content = LocalizationHelper.Get("Btn_SelectEssentials", "Select Essentials");
            if (btnDeselectAll != null) btnDeselectAll.Content = LocalizationHelper.Get("Btn_DeselectAll", "Deselect All");
            if (btnToggleCustomizePostInstall != null) btnToggleCustomizePostInstall.Content = LocalizationHelper.Get("Btn_CustomizeAutomated", "⚙️ Customize Automated Installation");
            if (btnOpenPostInstallFolder != null) btnOpenPostInstallFolder.Content = LocalizationHelper.Get("Btn_OpenPostInstallFolder", "📂 Installers Folder");
            if (btnRunInstallSelected != null) btnRunInstallSelected.Content = LocalizationHelper.Get("Btn_RunInstallSelected", "🚀 Install Selected");

            // 4. Optimization
            if (txtOptTitle != null) txtOptTitle.Text = LocalizationHelper.Get("Opt_Title", "System Optimization and Cleanup");
            if (txtOptSubtitle != null) txtOptSubtitle.Text = LocalizationHelper.Get("Opt_Subtitle", "Advanced performance tweaks, integrity repairs, and quick utilities");
            if (txtCommSectionTitle != null) txtCommSectionTitle.Text = LocalizationHelper.Get("Opt_Community_Title", "🌐 Community Tools");
            if (btnChrisTitusOpt != null) btnChrisTitusOpt.Content = LocalizationHelper.Get("Btn_ChrisTitus", "🚀 Chris Titus Tool (WinUtil)");
            if (btnMassgraveOpt != null) btnMassgraveOpt.Content = LocalizationHelper.Get("Btn_Massgrave", "🔑 Massgrave Activation (MAS)");
            if (txtIntegrityTitle != null) txtIntegrityTitle.Text = LocalizationHelper.Get("Opt_Integrity_Title", "🛡️ System Integrity & Repairs");
            if (txtBtnSfcText != null) txtBtnSfcText.Text = LocalizationHelper.Get("Btn_Sfc", "Check & Repair Windows Integrity via SFC");
            if (btnSfc != null) btnSfc.ToolTip = LocalizationHelper.Get("Tooltip_Sfc", "Check and Repair Windows Integrity via SFC (sfc /scannow)");
            if (txtBtnDismText != null) txtBtnDismText.Text = LocalizationHelper.Get("Btn_Dism", "Check & Repair Windows Integrity via DISM");
            if (btnDism != null) btnDism.ToolTip = LocalizationHelper.Get("Tooltip_Dism", "Check and Repair Windows Integrity via DISM (Dism /Online /Cleanup-Image /RestoreHealth)");
            if (btnChkdsk != null) btnChkdsk.Content = LocalizationHelper.Get("Btn_Chkdsk", "Check Disk Integrity (CHKDSK C:)");
            if (btnPrinterFix != null) btnPrinterFix.Content = LocalizationHelper.Get("Btn_PrinterFix", "Fix Network Printer RPC Issue");
            if (btnDualBoot != null) btnDualBoot.Content = LocalizationHelper.Get("Btn_DualBoot", "Fix System Clock for Dual Boot");
            if (btnStoreFix != null) btnStoreFix.Content = LocalizationHelper.Get("Btn_StoreFix", "Install or Repair Microsoft Store");
            if (btnNetReset != null) btnNetReset.Content = LocalizationHelper.Get("Btn_NetReset", "Network Reset");
            if (btnWinSat != null) btnWinSat.Content = LocalizationHelper.Get("Btn_WinSat", "WinSAT Benchmark");
            if (txtCleanupTitle != null) txtCleanupTitle.Text = LocalizationHelper.Get("Opt_Cleanup_Title", "⚡ Cleanup & Performance");
            if (btnTempClean != null) btnTempClean.Content = LocalizationHelper.Get("Btn_TempClean", "Clean Temporary Files");
            if (btnUpdateClean != null) btnUpdateClean.Content = LocalizationHelper.Get("Btn_UpdateClean", "Clean Windows Update Cache");
            if (btnVisualEffects != null) btnVisualEffects.Content = LocalizationHelper.Get("Btn_VisualEffects", "Optimize Visual Effects");
            if (btnTelemetry != null) btnTelemetry.Content = LocalizationHelper.Get("Btn_Telemetry", "Disable Telemetry & Optimize Services");
            if (txtBtnCompactOsText != null) txtBtnCompactOsText.Text = LocalizationHelper.Get("Btn_CompactOs", "Compact Windows Installation");
            if (btnCompactOs != null) btnCompactOs.ToolTip = LocalizationHelper.Get("Tooltip_CompactOs", "Compact Windows OS (compact.exe /CompactOS:always) - Reduces disk footprint");
            if (btnOpenScriptsFolder != null) btnOpenScriptsFolder.Content = LocalizationHelper.Get("Btn_OpenScriptsFolder", "📂 Open Scripts Folder");

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
            }

            // 6. Maintenance Report
            if (txtReportTitle != null) txtReportTitle.Text = LocalizationHelper.Get("Report_Title", "Maintenance Service Record");
            if (txtReportSub != null) txtReportSub.Text = LocalizationHelper.Get("Report_Subtitle", "Generates a report with machine specs, motherboard, serial number, and performed services");
            if (txtReportOrderLabel != null) txtReportOrderLabel.Text = LocalizationHelper.Get("Report_Label_Order", "Service Order (SO) Number:");
            if (txtReportClientLabel != null) txtReportClientLabel.Text = LocalizationHelper.Get("Report_Label_Client", "Client Name:");
            if (txtReportSerialLabel != null) txtReportSerialLabel.Text = LocalizationHelper.Get("Report_Label_Serial", "Serial Number / Service Tag:");
            if (txtChecklistLabel != null) txtChecklistLabel.Text = LocalizationHelper.Get("Report_Label_Checklist", "Checklist of Performed Procedures:");
            if (chkActSmart != null) chkActSmart.Content = LocalizationHelper.Get("Act_Smart", "Disk diagnostic and SMART health check");
            if (chkActClean != null) chkActClean.Content = LocalizationHelper.Get("Act_Clean", "Preventive internal cleaning & thermal paste replacement");
            if (chkActFormat != null) chkActFormat.Content = LocalizationHelper.Get("Act_Format", "Clean Windows format and reinstallation");
            if (chkActDrivers != null) chkActDrivers.Content = LocalizationHelper.Get("Act_Drivers", "Full driver and BIOS update");
            if (chkActApps != null) chkActApps.Content = LocalizationHelper.Get("Act_Apps", "Essential application installation");
            if (chkActTemp != null) chkActTemp.Content = LocalizationHelper.Get("Act_Temp", "Deep temporary file cleanup");
            if (chkActDism != null) chkActDism.Content = LocalizationHelper.Get("Act_Dism", "System integrity repair (DISM / SFC)");
            if (chkActNet != null) chkActNet.Content = LocalizationHelper.Get("Act_Net", "Network and printer setup");
            if (txtReportNotesLabel != null) txtReportNotesLabel.Text = LocalizationHelper.Get("Report_Label_Notes", "Technical Report & Observations:");

            // Update notes placeholder if user hasn't typed custom notes
            if (txtReportNotes != null)
            {
                string cur = txtReportNotes.Text != null ? txtReportNotes.Text.Trim() : "";
                if (string.IsNullOrEmpty(cur) ||
                    cur.StartsWith("Você pode informar", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("You can enter", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("You can provide", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("Enter technical observations", StringComparison.OrdinalIgnoreCase))
                {
                    txtReportNotes.Text = LocalizationHelper.Get("Report_Notes_Placeholder", "You can provide further details about the services performed on this machine here...");
                    txtReportNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
                }
            }

            if (btnSaveReport != null) btnSaveReport.Content = LocalizationHelper.Get("Btn_SaveReport", "📄 Generate Maintenance Report");
            if (btnCopyReport != null) btnCopyReport.Content = LocalizationHelper.Get("Btn_CopyReport", "📋 Copy Technical Report");
            if (btnOpenReportFolder != null) btnOpenReportFolder.Content = LocalizationHelper.Get("Btn_OpenReportsFolder", "📂 Open Reports Folder");

            // 7. Logs
            if (txtLogsTitle != null) txtLogsTitle.Text = LocalizationHelper.Get("Logs_Title", "⬛ Logs");
            if (txtLogsSub != null) txtLogsSub.Text = LocalizationHelper.Get("Logs_Subtitle", "Real-time log of diagnostics, scripts, and system executions");
            if (btnExportLog != null) btnExportLog.Content = LocalizationHelper.Get("Btn_ExportLog", "💾 Export Log");
            if (btnCopyAllLog != null) btnCopyAllLog.Content = LocalizationHelper.Get("Btn_CopyAllLog", "📋 Copy All");
            if (btnClearLog != null) btnClearLog.Content = LocalizationHelper.Get("Btn_ClearLog", "🗑️ Clear Console");

            // 8. Settings
            if (txtSettingsTitle != null) txtSettingsTitle.Text = LocalizationHelper.Get("Settings_Title", "OptiAssist Settings");
            if (txtSettingsSub != null) txtSettingsSub.Text = LocalizationHelper.Get("Settings_Subtitle", "Settings saved in Config\\settings.json on the portable drive");
            if (chkServiceProviderMode != null) chkServiceProviderMode.Content = LocalizationHelper.Get("Settings_Chk_ProviderMode", "Service provider mode");
            if (txtServiceProviderDesc != null) txtServiceProviderDesc.Text = LocalizationHelper.Get("Settings_Desc_ProviderMode", "Enables branding customization and options for service orders and technical reports");
            if (txtStoreNameLabel != null) txtStoreNameLabel.Text = LocalizationHelper.Get("Settings_Label_StoreName", "Custom application name:");
            if (txtSettingStore != null) txtSettingStore.ToolTip = LocalizationHelper.Get("Settings_Hint_StoreName", "💡 Customize the application according to the name you prefer.");
            if (txtStoreNameHint != null) txtStoreNameHint.Text = LocalizationHelper.Get("Settings_Hint_StoreName", "💡 Customize the application according to the name you prefer.");
            if (txtPhoneLabel != null) txtPhoneLabel.Text = LocalizationHelper.Get("Settings_Label_Phone", "Contact Information:");
            if (txtSettingPhone != null) txtSettingPhone.ToolTip = LocalizationHelper.Get("Settings_Hint_Phone", "💡 Provide an email, phone number, or social media link of your choice");
            if (txtPhoneHint != null) txtPhoneHint.Text = LocalizationHelper.Get("Settings_Hint_Phone", "💡 Provide an email, phone number, or social media link of your choice");
            if (chkEnableMaintenanceReport != null) chkEnableMaintenanceReport.Content = LocalizationHelper.Get("Settings_Chk_EnableReport", "Enable Maintenance Report");
            if (txtEnableReportDesc != null) txtEnableReportDesc.Text = LocalizationHelper.Get("Settings_Desc_EnableReport", "Shows or hides the Maintenance Report screen in the sidebar menu");
            if (txtReportPathLabel != null) txtReportPathLabel.Text = LocalizationHelper.Get("Settings_Label_ReportPath", "Default Report File Path:");
            if (txtLangLabel != null) txtLangLabel.Text = LocalizationHelper.Get("Settings_Label_Language", "Application Language:");
            SetupLanguageButtons(langCode);

            if (chkSettingWinget != null) chkSettingWinget.Content = LocalizationHelper.Get("Settings_Chk_WingetFallback", "Enable Winget download fallback when program is not in PostInstall folder");
            if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.Content = LocalizationHelper.Get("Settings_Chk_AutoRefresh", "Automatically refresh hardware specs on startup");
            if (btnSaveSettings != null) btnSaveSettings.Content = LocalizationHelper.Get("Btn_SaveSettings", "💾 Save Settings");
            if (btnResetSettings != null) btnResetSettings.Content = LocalizationHelper.Get("Btn_ResetSettings", "🔄 Restore Defaults");
            if (btnCreateAutorun != null)
            {
                btnCreateAutorun.Content = LocalizationHelper.Get("Btn_CreateAutorun", "💿 Create USB AutoRun");
                btnCreateAutorun.ToolTip = LocalizationHelper.Get("Tooltip_CreateAutorun", "Create autorun.inf on drive root to display application icon and AutoPlay entry");
            }
            if (txtPersonalizeTitle != null) txtPersonalizeTitle.Text = LocalizationHelper.Get("Settings_Personalize_Title", "🎨 Customize Application");
            if (txtPersonalizeSub != null) txtPersonalizeSub.Text = LocalizationHelper.Get("Settings_Personalize_Subtitle", "Customize the visual theme, button accent colors, and styling of OptiAssist:");
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

            // 9. About
            if (txtAboutTitle != null) txtAboutTitle.Text = LocalizationHelper.Get("About_Title", "About OptiAssist");
            if (txtAboutSub != null) txtAboutSub.Text = LocalizationHelper.Get("About_Subtitle", "Version information, copyright, and open-source license");
            if (txtAboutLicenseTag != null) txtAboutLicenseTag.Text = LocalizationHelper.Get("About_License_Tag", "⚖️ License: GNU General Public License v3.0 (GNU GPLv3)");
            if (txtAboutCreditsTitle != null) txtAboutCreditsTitle.Text = LocalizationHelper.Get("About_Credits_Title", "🤝 Acknowledgments & Credits");
            if (txtAboutCreditsText != null) txtAboutCreditsText.Text = LocalizationHelper.Get("About_Credits_Text", "Special thanks and credit to Chris Titus Tech and the open-source community behind WinUtil, whose work served as an essential inspiration and foundation for the implementation of several system maintenance and optimization features in OptiAssist.");
            if (btnAboutGithub != null) btnAboutGithub.Content = LocalizationHelper.Get("Btn_AboutGithub", "🌐 GitHub Repository");
            if (btnAboutWebsite != null) btnAboutWebsite.Content = LocalizationHelper.Get("Btn_AboutWebsite", "🔗 Official Website");

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

        private void ChkServiceProviderMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.ServiceProviderMode = chkServiceProviderMode.IsChecked == true;
            SettingsHelper.Save(_settings);

            UpdateServiceProviderUI();
            Log(string.Format("[CONFIG] ServiceProviderMode updated to {0} and saved to settings.json.", _settings.ServiceProviderMode));
        }

        private void ChkEnableMaintenanceReport_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_settings == null) _settings = SettingsHelper.Load();
            _settings.EnableMaintenanceReport = chkEnableMaintenanceReport.IsChecked == true;
            SettingsHelper.Save(_settings);

            UpdateServiceProviderUI();
            Log(string.Format("[CONFIG] EnableMaintenanceReport updated to {0} and saved to settings.json.", _settings.EnableMaintenanceReport));
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

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) _settings = new AppSettings();
            _settings.ServiceProviderMode = chkServiceProviderMode.IsChecked == true;
            _settings.EnableMaintenanceReport = chkEnableMaintenanceReport.IsChecked == true;
            _settings.CustomerName = txtSettingStore.Text.Trim();
            _settings.ContactInfo = txtSettingPhone.Text.Trim();
            _settings.ReportSavePath = txtSettingReportPath.Text.Trim();
            _settings.EnableWingetFallback = chkSettingWinget.IsChecked == true;
            _settings.AutoRefreshHardware = chkSettingAutoRefresh.IsChecked == true;
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
        #endregion
        #endregion
    }
}
