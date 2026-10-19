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

namespace OptiAssist
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private HardwareSummary _hardware;
        private ObservableCollection<InstallAppItem> _apps;
        private ObservableCollection<InstallAppItem> _customEditorApps;
        private ObservableCollection<WingetUpgradeItem> _wingetUpgrades;
        private InstallAppItem _editingAppItem;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadApplicationSettings();
            LoadPostInstallApps();

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
                MessageBox.Show("Texto do console copiado para a Área de Transferência!", "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtConsoleLog.Text))
            {
                MessageBox.Show("O terminal está vazio no momento.", "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Arquivo de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*";
                sfd.FileName = string.Format("OptiAssist_Log_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                if (sfd.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(sfd.FileName, txtConsoleLog.Text, System.Text.Encoding.UTF8);
                    MessageBox.Show("Log salvo com sucesso em:\n" + sfd.FileName, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("[SAVED] Log exported to: " + sfd.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha ao salvar log: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region LOAD CONFIGURATION & APPLICATIONS
        private void LoadApplicationSettings()
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

            // Update footer with Custom App Name
            if (lblFooterStoreName != null)
            {
                string customName = _settings.CustomerName;
                if (txtSettingStore != null && !string.IsNullOrWhiteSpace(txtSettingStore.Text))
                {
                    customName = txtSettingStore.Text.Trim();
                }

                if (isProvider && !string.IsNullOrWhiteSpace(customName) && customName != "N/A")
                {
                    lblFooterStoreName.Text = customName;
                    lblFooterStoreName.Visibility = Visibility.Visible;
                }
                else
                {
                    lblFooterStoreName.Text = "";
                    lblFooterStoreName.Visibility = Visibility.Collapsed;
                }
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
            lblHeaderDevice.Text = LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblHeaderCpu.Text = LocalizationHelper.Get("Status_Detecting", "Detectando processador...");
            lblHeaderRam.Text = LocalizationHelper.Get("Status_Detecting", "Detectando RAM...");

            lblOsTitle.Text = LocalizationHelper.Get("Status_Detecting", "Consultando informações do sistema e WMI...");
            lblCpuDetails.Text = LocalizationHelper.Get("Label_Processor", "Processador: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblRamDetails.Text = LocalizationHelper.Get("Label_Ram", "Memória RAM: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblGpuDetails.Text = LocalizationHelper.Get("Label_Gpu", "Placa de Vídeo: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblMotherboard.Text = LocalizationHelper.Get("Label_Motherboard", "Placa-Mãe: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblBiosInfo.Text = LocalizationHelper.Get("Label_Bios", "BIOS: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblDiskDetails.Text = LocalizationHelper.Get("Label_Storage", "Armazenamento: ") + LocalizationHelper.Get("Status_Detecting", "Detectando...");
            lblSerialNumber.Text = "...";

            txtBitLockerStatus.Text = LocalizationHelper.Get("Status_Detecting", "Consultando...");
            txtBitLockerStatus.Foreground = (SolidColorBrush)FindResource("BrushWarning");
            txtTpmStatus.Text = LocalizationHelper.Get("Status_Detecting", "Consultando...");
            txtTpmStatus.Foreground = (SolidColorBrush)FindResource("BrushWarning");
            txtSecureBootStatus.Text = LocalizationHelper.Get("Status_Detecting", "Consultando...");
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
                LocalizationHelper.Get("Label_Motherboard", "Placa-Mãe: ").TrimEnd(':', ' '), info.Motherboard,
                LocalizationHelper.Get("Label_Bios", "BIOS: ").TrimEnd(':', ' '), info.BiosInfo,
                LocalizationHelper.Get("Label_SerialNumber", "Número de Série: ").TrimEnd(':', ' '), info.SerialNumber);

            lblHeaderCpu.Text = info.CpuName;
            lblHeaderCpu.ToolTip = string.Format("{0}: {1}\n{2} Núcleos / {3} Threads",
                LocalizationHelper.Get("Label_Processor", "Processador: ").TrimEnd(':', ' '), info.CpuName, info.CpuCores, info.CpuThreads);

            lblHeaderRam.Text = string.Format("{0} GB RAM", info.RamTotalGb);
            lblHeaderRam.ToolTip = string.Format("{0}: {1} GB ({2} GB)",
                LocalizationHelper.Get("Label_Ram", "Memória RAM: ").TrimEnd(':', ' '), info.RamTotalGb, info.RamFreeGb);

            // Operating System Card (Right)
            lblOsTitle.Text = string.Format("{0} {1}", info.OsName, info.OsDisplayVersion);
            lblOsDetails.Text = string.Format("Compilação: Build {0} ({1})",
                info.OsBuild, info.OsArchitecture);
            lblMachineName.Text = info.ComputerName;

            // Hardware Information Card (Left - 1 per line)
            lblSerialNumber.Text = info.SerialNumber;
            lblCpuDetails.Text = string.Format("{0}{1} ({2} Cores / {3} Threads)",
                LocalizationHelper.Get("Label_Processor", "Processador: "), info.CpuName, info.CpuCores, info.CpuThreads);
            lblRamDetails.Text = string.Format("{0}{1} GB Total ({2} GB)",
                LocalizationHelper.Get("Label_Ram", "Memória RAM: "), info.RamTotalGb, info.RamFreeGb);
            lblGpuDetails.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Gpu", "Placa de Vídeo: "), info.GpuName);
            lblMotherboard.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Motherboard", "Placa-Mãe: "), info.Motherboard);
            lblBiosInfo.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Bios", "BIOS: "), info.BiosInfo);
            lblDiskDetails.Text = string.Format("{0}{1}",
                LocalizationHelper.Get("Label_Storage", "Armazenamento: "), info.PrimaryDiskInfo);

            // Auto-fill serial number in maintenance report form if empty
            if (string.IsNullOrEmpty(txtReportSerial.Text) || txtReportSerial.Text == "Não informado" || txtReportSerial.Text == "Not informed")
            {
                txtReportSerial.Text = info.SerialNumber;
            }
        }

        private void BtnCopySerial_Click(object sender, RoutedEventArgs e)
        {
            string sn = lblSerialNumber.Text;
            if (!string.IsNullOrEmpty(sn) && sn != "..." && sn != "Não informado" && sn != "Not informed")
            {
                Clipboard.SetText(sn);
                MessageBox.Show(LocalizationHelper.Get("Msg_SerialCopied", "Número de série copiado para a Área de Transferência!") + "\n\n" + sn, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                Log(string.Format("[CLIPBOARD] Serial number '{0}' copied to the clipboard.", sn));
            }
            else
            {
                MessageBox.Show(LocalizationHelper.Get("Status_NotDetected", "Nenhum número de série válido foi detectado neste equipamento."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRebootBios_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBox.Show(
                LocalizationHelper.Get("Msg_RebootUefiConfirm", "Deseja realmente reiniciar o computador diretamente para o menu da BIOS / UEFI?\n\nCertifique-se de salvar todos os trabalhos abertos antes de continuar."),
                "OptiAssist - BIOS / UEFI",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                string error;
                bool ok = SystemInfoHelper.RebootToBios(out error);
                if (!ok)
                {
                    MessageBox.Show(error, "OptiAssist - Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Log("[BIOS WARNING] " + error);
                }
            }
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
                dlg.Title = "Selecionar instalador na pasta PostInstall";
                dlg.Filter = "Instaladores (*.exe;*.msi;*.msixbundle;*.appxbundle)|*.exe;*.msi;*.msixbundle;*.appxbundle|Todos os Arquivos (*.*)|*.*";
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
                MessageBox.Show("Erro ao selecionar arquivo: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
        {
            string name = txtNewAppName.Text != null ? txtNewAppName.Text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LocalizationHelper.Get("Msg_EnterAppName", "Por favor, digite o nome do programa."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(LocalizationHelper.Get("Msg_CustomAppsSaved", "Lista de programas salva em Config\\autoinstall.json com sucesso!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnResetDefaultApps_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(LocalizationHelper.Get("Msg_ResetDefaultAppsConfirm", "Deseja restaurar a lista padrão de programas recomendados no autoinstall.json?"), "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var defaults = InstallerHelper.GetDefaultApps();
                InstallerHelper.SaveApps(defaults);
                PopulateCustomAppsEditor();
                LoadPostInstallApps();
                MessageBox.Show(LocalizationHelper.Get("Msg_ResetDefaultAppsDone", "Lista padrão restaurada com sucesso!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show(LocalizationHelper.Get("Msg_NoAppsSelected", "Nenhum aplicativo foi selecionado."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("Instalação dos programas selecionados concluída!", "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            });
        }

        #region APPLICATION UPDATES (WINGET UPGRADE)
        private void BtnCheckWingetUpgrades_Click(object sender, RoutedEventArgs e)
        {
            cardWingetUpgrades.Visibility = Visibility.Visible;
            pbWingetScan.Visibility = Visibility.Visible;
            lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_Scanning", "Procurando por atualizações disponíveis no sistema via winget...");
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
                        lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_UpToDate", "✔️ Todos os aplicativos instalados já estão atualizados!");
                        borderUpgradesList.Visibility = Visibility.Collapsed;
                        gridUpgradeActions.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        lblWingetUpgradeStatus.Text = string.Format(LocalizationHelper.Get("Upgrade_Status_Found", "Foram encontradas atualizações para {0} aplicativo(s):"), upgrades.Count);
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
                MessageBox.Show(LocalizationHelper.Get("Msg_NoAppsSelected", "Nenhum aplicativo foi selecionado para atualização."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnUpgradeSelected.IsEnabled = false;
            btnUpgradeAll.IsEnabled = false;
            pbWingetScan.Visibility = Visibility.Visible;
            pnlWingetLogsAction.Visibility = Visibility.Visible;
            lblWingetUpgradeStatus.Text = string.Format(LocalizationHelper.Get("Upgrade_Status_Updating", "Atualizando {0} aplicativo(s) selecionado(s)... Acompanhe pelos Logs."), selected.Count);

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
                    MessageBox.Show(string.Format("Atualização de aplicativos concluída!\n{0} de {1} atualizados com sucesso.", successes, selected.Count),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Re-scan updates to refresh list
                    BtnCheckWingetUpgrades_Click(null, null);
                }));
            });
        }

        private void BtnUpgradeAll_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(LocalizationHelper.Get("Msg_UpgradeAllConfirm", "Deseja executar a atualização de todos os programas do sistema agora via 'winget upgrade --all'?"),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            btnUpgradeSelected.IsEnabled = false;
            btnUpgradeAll.IsEnabled = false;
            pbWingetScan.Visibility = Visibility.Visible;
            pnlWingetLogsAction.Visibility = Visibility.Visible;
            lblWingetUpgradeStatus.Text = LocalizationHelper.Get("Upgrade_Status_UpdatingAll", "Executando 'winget upgrade --all' em segundo plano... Acompanhe pelos Logs.");

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
                    MessageBox.Show("Comando de atualização geral finalizado! Verifique os detalhes na aba Logs.",
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
                    "RESULTADOS DO BENCHMARK ATUAL (WinSAT):\n\n" +
                    "• Pontuação Geral (WinSPR): {0:F1}\n" +
                    "• Processador (CPU):        {1:F1}\n" +
                    "• Memória RAM:              {2:F1}\n" +
                    "• Gráficos / Jogos (D3D):   {3:F1}\n" +
                    "• Gráficos Desktop:         {4:F1}\n" +
                    "• Disco Primário:           {5:F1}\n\n" +
                    "Deseja executar um NOVO benchmark completo agora? (Pode levar alguns minutos)",
                    current.OverallScore, current.CpuScore, current.MemoryScore, current.D3DScore, current.GraphicsScore, current.DiskScore);

                MessageBoxResult res = MessageBox.Show(infoMsg, "OptiAssist - Benchmark WinSAT", MessageBoxButton.YesNo, MessageBoxImage.Information);
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
                            string.Format("Benchmark WinSAT Finalizado com Sucesso!\n\nPontuação Geral: {0:F1}\nCPU: {1:F1} | RAM: {2:F1} | Disco: {3:F1}",
                            updated.OverallScore, updated.CpuScore, updated.MemoryScore, updated.DiskScore),
                            "OptiAssist - Benchmark Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
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

        #region MAINTENANCE REPORT (SERVICE ORDER)
        private void TxtReportNotes_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtReportNotes.Text.StartsWith("Você pode informar"))
            {
                txtReportNotes.Text = "";
                txtReportNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            }
        }

        private MaintenanceRecord BuildRecordFromUI()
        {
            MaintenanceRecord rec = new MaintenanceRecord();
            rec.StoreName = _settings != null ? _settings.CustomerName : "OptiAssist";
            rec.Phone = _settings != null ? _settings.ContactInfo : "N/A";
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
                "O registro de manutenção foi emitido na pasta Reports.\n\n" +
                "Deseja armazenar uma cópia deste relatório no equipamento do usuário?",
                "Armazenar Cópia no Equipamento",
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
                msg = "Registro de manutenção gravado com sucesso!\n\n" +
                      "• Pasta Reports (aplicativo): " + reportsFilePath + "\n" +
                      "• Cópia no equipamento: " + clientCopyPath + "\n\n" +
                      "Deseja abrir o arquivo agora?";
            }
            else
            {
                msg = "Registro de manutenção emitido com sucesso na pasta Reports!\n\n" +
                      "• Arquivo: " + reportsFilePath + "\n\n" +
                      "Deseja abrir o arquivo agora?";
            }

            MessageBoxResult openRes = MessageBox.Show(msg, "OptiAssist - Registro de Manutenção", MessageBoxButton.YesNo, MessageBoxImage.Information);
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
            MessageBox.Show("Relatório completo copiado para a Área de Transferência!\nVocê pode colá-lo no WhatsApp ou no seu sistema de gestão.",
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

            // 1. Sidebar Navigation
            if (rbNavDashboard != null) rbNavDashboard.Content = LocalizationHelper.Get("Nav_Dashboard", "💻  Visão Geral");
            if (rbNavPostInstall != null) rbNavPostInstall.Content = LocalizationHelper.Get("Nav_PostInstall", "📦  Gerenciador de Apps");
            if (rbNavOptimization != null) rbNavOptimization.Content = LocalizationHelper.Get("Nav_Optimization", "⚡  Otimização");
            if (rbNavShortcuts != null) rbNavShortcuts.Content = LocalizationHelper.Get("Nav_Shortcuts", "🛠️  Ferramentas do Windows");
            if (rbNavReport != null) rbNavReport.Content = LocalizationHelper.Get("Nav_Report", "📋  Registrar Manutenção");
            if (rbNavTerminal != null) rbNavTerminal.Content = LocalizationHelper.Get("Nav_Terminal", "⬛  Logs");
            if (rbNavSettings != null) rbNavSettings.Content = LocalizationHelper.Get("Nav_Settings", "⚙️  Configurações");
            if (rbNavAbout != null) rbNavAbout.Content = LocalizationHelper.Get("Nav_About", "ℹ️  Sobre o aplicativo");

            // 2. Overview (Dashboard)
            if (txtDashboardTitle != null) txtDashboardTitle.Text = LocalizationHelper.Get("Dashboard_Title", "Bem vindo ao OptiAssist!");
            if (txtDashboardSubtitle != null) txtDashboardSubtitle.Text = LocalizationHelper.Get("Dashboard_Subtitle", "A ferramenta suprema para manter seu equipamento em ordem");
            if (txtHwCardTitle != null) txtHwCardTitle.Text = LocalizationHelper.Get("Card_Hardware_Title", "🧠 Informações do hardware");
            if (txtOsCardTitle != null) txtOsCardTitle.Text = LocalizationHelper.Get("Card_Os_Title", "🖥️ Sistema Operacional");
            if (lblSerialTag != null) lblSerialTag.Text = LocalizationHelper.Get("Label_SerialNumber", "Número de Série: ");
            if (btnCopySerial != null) btnCopySerial.Content = LocalizationHelper.Get("Btn_Copy", "📋 Copiar");
            if (lblMachineNameTag != null) lblMachineNameTag.Text = LocalizationHelper.Get("Label_DeviceName", "Nome do Dispositivo:");
            if (txtCommonTasksTitle != null) txtCommonTasksTitle.Text = LocalizationHelper.Get("Card_CommonTasks_Title", "⚡ Tarefas Comuns");

            if (btnTaskPostInstall != null) btnTaskPostInstall.Content = LocalizationHelper.Get("Task_PostInstall", "📦 Gerenciador de Apps");
            if (btnTaskMaintenanceFolder != null) btnTaskMaintenanceFolder.Content = LocalizationHelper.Get("Task_MaintenanceFolder", "📁 Pasta Manutenção");
            if (btnTaskChrisTitus != null) btnTaskChrisTitus.Content = LocalizationHelper.Get("Task_ChrisTitus", "🚀 Chris Titus WinUtil");
            if (btnTaskMassgrave != null) btnTaskMassgrave.Content = LocalizationHelper.Get("Task_Massgrave", "🔑 Massgrave (MAS)");
            if (btnTaskMemoryDiag != null) btnTaskMemoryDiag.Content = LocalizationHelper.Get("Task_MemoryDiag", "🧠 Diagnóstico Memória");
            if (btnTaskDevMgmt != null) btnTaskDevMgmt.Content = LocalizationHelper.Get("Task_DevMgmt", "🖥️ Gerenc. Dispositivos");
            if (btnTaskSysDm != null) btnTaskSysDm.Content = LocalizationHelper.Get("Task_SysDm", "⚙️ Propriedades Sistema");
            if (btnRebootBios != null) btnRebootBios.Content = LocalizationHelper.Get("Task_RebootBios", "⚡ Reiniciar p/ UEFI");

            // 3. App Manager (PostInstall)
            if (txtPostInstallTitle != null) txtPostInstallTitle.Text = LocalizationHelper.Get("PostInstall_Title", "Gerenciador de Apps");
            if (txtPostInstallSubtitle != null) txtPostInstallSubtitle.Text = LocalizationHelper.Get("PostInstall_Subtitle", "Instalação automatizada de softwares essenciais e atualização de programas instalados");
            if (txtWingetCardTitle != null) txtWingetCardTitle.Text = LocalizationHelper.Get("Upgrade_Card_Title", "🔄 Atualização de Aplicativos");
            if (btnCheckWingetUpgrades != null) btnCheckWingetUpgrades.Content = LocalizationHelper.Get("Btn_CheckUpdates", "🔄 Verificar Atualizações");
            if (btnViewLogsFromUpgrade != null) btnViewLogsFromUpgrade.Content = LocalizationHelper.Get("Btn_ViewLogs", "⬛ Visualizar Logs");
            if (btnSelectAllUpgrades != null) btnSelectAllUpgrades.Content = LocalizationHelper.Get("Btn_SelectAll", "Selecionar Todos");
            if (btnDeselectAllUpgrades != null) btnDeselectAllUpgrades.Content = LocalizationHelper.Get("Btn_DeselectAll", "Desmarcar Todos");
            if (btnRecheckWingetUpgrades != null) btnRecheckWingetUpgrades.Content = LocalizationHelper.Get("Btn_Recheck", "🔄 Verificar Novamente");
            if (btnUpgradeAll != null) btnUpgradeAll.Content = LocalizationHelper.Get("Btn_UpgradeAll", "🚀 Atualizar Tudo");
            if (btnUpgradeSelected != null) btnUpgradeSelected.Content = LocalizationHelper.Get("Btn_UpgradeSelected", "🔄 Atualizar Selecionados");

            if (txtCustomizeHeaderTitle != null) txtCustomizeHeaderTitle.Text = LocalizationHelper.Get("Custom_Card_Title", "⚙️ Personalizar instalação automatizada de aplicativos");
            if (txtCustomizeHeaderSub != null) txtCustomizeHeaderSub.Text = LocalizationHelper.Get("Custom_Card_Subtitle", "Adicione novos programas, defina se serão automatizados por padrão e escolha entre instalador Local ou Winget.");
            if (btnCloseCustomize != null) btnCloseCustomize.Content = LocalizationHelper.Get("Btn_Close", "✕ Fechar");
            if (txtNewAppNameLabel != null) txtNewAppNameLabel.Text = LocalizationHelper.Get("Custom_Label_Name", "Nome do Programa:");
            if (txtNewAppTypeLabel != null) txtNewAppTypeLabel.Text = LocalizationHelper.Get("Custom_Label_Type", "Tipo de Instalação:");
            if (cmbItemLocal != null) cmbItemLocal.Content = LocalizationHelper.Get("Custom_Type_Local", "📁 Local (PostInstall)");
            if (cmbItemWinget != null) cmbItemWinget.Content = LocalizationHelper.Get("Custom_Type_Winget", "🌐 Winget (Repositório)");
            if (txtNewAppLocalLabel != null) txtNewAppLocalLabel.Text = LocalizationHelper.Get("Custom_Label_LocalFile", "Executável na pasta PostInstall:");
            if (btnSelectLocalInstaller != null) btnSelectLocalInstaller.Content = LocalizationHelper.Get("Btn_SelectLocal", "📁 Selecionar...");
            if (txtNewAppWingetLabel != null) txtNewAppWingetLabel.Text = LocalizationHelper.Get("Custom_Label_WingetId", "ID do App no Winget (ex: Google.Chrome):");
            if (btnAddCustomApp != null)
            {
                btnAddCustomApp.Content = (_editingAppItem != null) ? LocalizationHelper.Get("Btn_Update", "💾 Atualizar") : LocalizationHelper.Get("Btn_Add", "➕ Adicionar");
            }
            if (chkNewAppPreSelected != null) chkNewAppPreSelected.Content = LocalizationHelper.Get("Custom_Chk_AutomateDefault", "Automatizar por padrão");
            if (txtNewAppSilentLabel != null) txtNewAppSilentLabel.Text = LocalizationHelper.Get("Custom_Label_SilentArgs", "Argumentos silenciosos (opcional):");
            if (txtCurrentListTitle != null) txtCurrentListTitle.Text = LocalizationHelper.Get("Custom_CurrentList_Title", "Programas na lista atual:");
            if (btnSaveCustomApps != null) btnSaveCustomApps.Content = LocalizationHelper.Get("Btn_SaveCustomApps", "💾 Salvar Alterações");
            if (btnResetDefaultApps != null) btnResetDefaultApps.Content = LocalizationHelper.Get("Btn_ResetDefaultApps", "🔄 Restaurar Lista Padrão");
            if (btnOpenPostInstallJson != null) btnOpenPostInstallJson.Content = LocalizationHelper.Get("Btn_OpenPostInstallJson", "📂 Abrir autoinstall.json");

            if (txtAppsInstallCardTitle != null) txtAppsInstallCardTitle.Text = LocalizationHelper.Get("Install_Card_Title", "📦 Instalação Automatizada de Aplicativos");
            if (txtAppsInstallCardSub != null) txtAppsInstallCardSub.Text = LocalizationHelper.Get("Install_Card_Subtitle", "Selecione os programas que deseja instalar automaticamente em lote no computador:");
            if (btnSelectEssentials != null) btnSelectEssentials.Content = LocalizationHelper.Get("Btn_SelectEssentials", "Selecionar Essenciais");
            if (btnDeselectAll != null) btnDeselectAll.Content = LocalizationHelper.Get("Btn_DeselectAll", "Desmarcar Todos");
            if (btnToggleCustomizePostInstall != null) btnToggleCustomizePostInstall.Content = LocalizationHelper.Get("Btn_CustomizeAutomated", "⚙️ Personalizar Instalação Automatizada");
            if (btnOpenPostInstallFolder != null) btnOpenPostInstallFolder.Content = LocalizationHelper.Get("Btn_OpenPostInstallFolder", "📂 Pasta de Instaladores");
            if (btnRunInstallSelected != null) btnRunInstallSelected.Content = LocalizationHelper.Get("Btn_RunInstallSelected", "🚀 Instalar Selecionados");

            // 4. Optimization
            if (txtOptTitle != null) txtOptTitle.Text = LocalizationHelper.Get("Opt_Title", "Otimização do Sistema e Limpeza");
            if (txtOptSubtitle != null) txtOptSubtitle.Text = LocalizationHelper.Get("Opt_Subtitle", "Ajustes avançados de desempenho, correções de integridade e utilitários rápidos");
            if (txtCommSectionTitle != null) txtCommSectionTitle.Text = LocalizationHelper.Get("Opt_Community_Title", "🌐 Ferramentas da Comunidade");
            if (btnChrisTitusOpt != null) btnChrisTitusOpt.Content = LocalizationHelper.Get("Btn_ChrisTitus", "🚀 Chris Titus Tool (WinUtil)");
            if (btnMassgraveOpt != null) btnMassgraveOpt.Content = LocalizationHelper.Get("Btn_Massgrave", "🔑 Ativação Massgrave (MAS)");
            if (txtIntegrityTitle != null) txtIntegrityTitle.Text = LocalizationHelper.Get("Opt_Integrity_Title", "🛡️ Integridade do Sistema e Correções");
            if (txtBtnSfcText != null) txtBtnSfcText.Text = LocalizationHelper.Get("Btn_Sfc", "Verificar e Reparar Integridade do Windows via SFC");
            if (txtBtnDismText != null) txtBtnDismText.Text = LocalizationHelper.Get("Btn_Dism", "Verificar e Reparar Integridade do Windows via DISM");
            if (btnChkdsk != null) btnChkdsk.Content = LocalizationHelper.Get("Btn_Chkdsk", "Verificar Integridade do Disco (CHKDSK C:)");
            if (btnPrinterFix != null) btnPrinterFix.Content = LocalizationHelper.Get("Btn_PrinterFix", "Correção de problema de impressoras em rede");
            if (btnDualBoot != null) btnDualBoot.Content = LocalizationHelper.Get("Btn_DualBoot", "Correção de hora do sistema para instalação dual boot");
            if (btnStoreFix != null) btnStoreFix.Content = LocalizationHelper.Get("Btn_StoreFix", "Instalar ou Reparar a Microsoft Store");
            if (btnNetReset != null) btnNetReset.Content = LocalizationHelper.Get("Btn_NetReset", "Redefinição da Rede");
            if (btnWinSat != null) btnWinSat.Content = LocalizationHelper.Get("Btn_WinSat", "Benchmark WinSAT");
            if (txtCleanupTitle != null) txtCleanupTitle.Text = LocalizationHelper.Get("Opt_Cleanup_Title", "⚡ Limpeza & Desempenho");
            if (btnTempClean != null) btnTempClean.Content = LocalizationHelper.Get("Btn_TempClean", "Limpar Arquivos Temporários");
            if (btnUpdateClean != null) btnUpdateClean.Content = LocalizationHelper.Get("Btn_UpdateClean", "Limpar Cache do Windows Update");
            if (btnVisualEffects != null) btnVisualEffects.Content = LocalizationHelper.Get("Btn_VisualEffects", "Otimizar Efeitos Visuais");
            if (btnTelemetry != null) btnTelemetry.Content = LocalizationHelper.Get("Btn_Telemetry", "Desativar Telemetria e Otimizar Serviços do Sistema");
            if (txtBtnCompactOsText != null) txtBtnCompactOsText.Text = LocalizationHelper.Get("Btn_CompactOs", "Compactar Instalação do Windows");
            if (btnOpenScriptsFolder != null) btnOpenScriptsFolder.Content = LocalizationHelper.Get("Btn_OpenScriptsFolder", "📂 Abrir Pasta Scripts");

            // 5. Windows Administrative Tools
            if (txtToolsTitle != null) txtToolsTitle.Text = LocalizationHelper.Get("Tools_Title", "Ferramentas do Windows");
            if (txtToolsSubtitle != null) txtToolsSubtitle.Text = LocalizationHelper.Get("Tools_Subtitle", "Acesso imediato às ferramentas administrativas nativas do Windows e utilitários portáteis");
            if (txtAdminToolsTitle != null) txtAdminToolsTitle.Text = LocalizationHelper.Get("Tools_Section_Title", "🪟 Ferramentas Administrativas");
            if (btnDevMgmtTool != null) btnDevMgmtTool.Content = LocalizationHelper.Get("Tool_DevMgmt", "🔌 Gerenciador de Dispositivos");
            if (btnNetplwiz != null) btnNetplwiz.Content = LocalizationHelper.Get("Tool_Netplwiz", "👤 Contas de Usuário Legado");
            if (btnDiskMgmt != null) btnDiskMgmt.Content = LocalizationHelper.Get("Tool_DiskMgmt", "💾 Gerenciamento de Disco");
            if (btnControl != null) btnControl.Content = LocalizationHelper.Get("Tool_Control", "⚙️ Painel de Controle Clássico");
            if (btnMsinfo != null) btnMsinfo.Content = LocalizationHelper.Get("Tool_Msinfo", "ℹ️ Informações do Sistema");
            if (btnWinver != null) btnWinver.Content = LocalizationHelper.Get("Tool_Winver", "📋 Sobre o Windows");
            if (btnMsconfig != null) btnMsconfig.Content = LocalizationHelper.Get("Tool_Msconfig", "🔧 Ferramenta MSCONFIG");
            if (btnSysDmTool != null) btnSysDmTool.Content = LocalizationHelper.Get("Tool_SysDm", "💻 Propriedades do Sistema");
            if (btnNcpa != null) btnNcpa.Content = LocalizationHelper.Get("Tool_Ncpa", "🌐 Conexões de Rede");
            if (btnOptionalFeatures != null) btnOptionalFeatures.Content = LocalizationHelper.Get("Tool_OptionalFeatures", "📦 Recursos do Windows");
            if (btnRegedit != null) btnRegedit.Content = LocalizationHelper.Get("Tool_Regedit", "🔑 Editor de Registro");
            if (btnTaskmgr != null) btnTaskmgr.Content = LocalizationHelper.Get("Tool_Taskmgr", "📊 Gerenciador de Tarefas");
            if (btnResmon != null) btnResmon.Content = LocalizationHelper.Get("Tool_Resmon", "⚡ Monitor de Recursos");
            if (btnMemoryDiagTool != null) btnMemoryDiagTool.Content = LocalizationHelper.Get("Tool_MemoryDiag", "🧠 Diagnóstico de Memória");
            if (btnRelMon != null) btnRelMon.Content = LocalizationHelper.Get("Tool_Reliability", "📈 Monitor de Confiabilidade");
            if (btnEventVwr != null) btnEventVwr.Content = LocalizationHelper.Get("Tool_EventVwr", "📑 Visualizador de Eventos");
            if (txtBtnGpeditText != null) txtBtnGpeditText.Text = LocalizationHelper.Get("Tool_Gpedit", "📜 Ferramenta GPEDIT (Pro/Enterprise)");
            if (btnOpenMaintenanceFolderTool != null) btnOpenMaintenanceFolderTool.Content = LocalizationHelper.Get("Btn_OpenMaintenanceFolder", "📂 Pasta de Ferramentas de Manutenção");

            // 6. Maintenance Report
            if (txtReportTitle != null) txtReportTitle.Text = LocalizationHelper.Get("Report_Title", "Registrar Ordem de Manutenção");
            if (txtReportSub != null) txtReportSub.Text = LocalizationHelper.Get("Report_Subtitle", "Gera um laudo com especificações da máquina, placa-mãe, número de série e procedimentos");
            if (txtReportOrderLabel != null) txtReportOrderLabel.Text = LocalizationHelper.Get("Report_Label_Order", "Nº da Ordem de Serviço:");
            if (txtReportClientLabel != null) txtReportClientLabel.Text = LocalizationHelper.Get("Report_Label_Client", "Nome do Cliente:");
            if (txtReportSerialLabel != null) txtReportSerialLabel.Text = LocalizationHelper.Get("Report_Label_Serial", "Nº de Série / Service Tag:");
            if (txtChecklistLabel != null) txtChecklistLabel.Text = LocalizationHelper.Get("Report_Label_Checklist", "Checklist de Procedimentos Realizados:");
            if (chkActSmart != null) chkActSmart.Content = LocalizationHelper.Get("Act_Smart", "Diagnóstico e saúde de disco (SMART)");
            if (chkActClean != null) chkActClean.Content = LocalizationHelper.Get("Act_Clean", "Limpeza interna preventiva e pasta térmica");
            if (chkActFormat != null) chkActFormat.Content = LocalizationHelper.Get("Act_Format", "Formatação e instalação limpa do Windows");
            if (chkActDrivers != null) chkActDrivers.Content = LocalizationHelper.Get("Act_Drivers", "Atualização completa de drivers e BIOS");
            if (chkActApps != null) chkActApps.Content = LocalizationHelper.Get("Act_Apps", "Instalação de programas essenciais");
            if (chkActTemp != null) chkActTemp.Content = LocalizationHelper.Get("Act_Temp", "Limpeza profunda de arquivos temporários");
            if (chkActDism != null) chkActDism.Content = LocalizationHelper.Get("Act_Dism", "Reparo de integridade (DISM / SFC)");
            if (chkActNet != null) chkActNet.Content = LocalizationHelper.Get("Act_Net", "Configuração de rede e impressoras");
            if (txtReportNotesLabel != null) txtReportNotesLabel.Text = LocalizationHelper.Get("Report_Label_Notes", "Laudo Técnico e Observações:");
            if (btnSaveReport != null) btnSaveReport.Content = LocalizationHelper.Get("Btn_SaveReport", "📄 Gerar Registro de Manutenção");
            if (btnCopyReport != null) btnCopyReport.Content = LocalizationHelper.Get("Btn_CopyReport", "📋 Copiar Laudo Técnico");
            if (btnOpenReportFolder != null) btnOpenReportFolder.Content = LocalizationHelper.Get("Btn_OpenReportsFolder", "📂 Abrir Pasta Reports");

            // 7. Logs
            if (txtLogsTitle != null) txtLogsTitle.Text = LocalizationHelper.Get("Logs_Title", "⬛ Logs");
            if (txtLogsSub != null) txtLogsSub.Text = LocalizationHelper.Get("Logs_Subtitle", "Histórico em tempo real de diagnósticos, scripts e execuções do sistema");
            if (btnExportLog != null) btnExportLog.Content = LocalizationHelper.Get("Btn_ExportLog", "💾 Exportar Log");
            if (btnCopyAllLog != null) btnCopyAllLog.Content = LocalizationHelper.Get("Btn_CopyAllLog", "📋 Copiar Tudo");
            if (btnClearLog != null) btnClearLog.Content = LocalizationHelper.Get("Btn_ClearLog", "🗑️ Limpar Console");

            // 8. Settings
            if (txtSettingsTitle != null) txtSettingsTitle.Text = LocalizationHelper.Get("Settings_Title", "Configurações do OptiAssist");
            if (txtSettingsSub != null) txtSettingsSub.Text = LocalizationHelper.Get("Settings_Subtitle", "Dados salvos em Config\\settings.json no pen drive");
            if (chkServiceProviderMode != null) chkServiceProviderMode.Content = LocalizationHelper.Get("Settings_Chk_ProviderMode", "Modo prestador de serviço");
            if (txtServiceProviderDesc != null) txtServiceProviderDesc.Text = LocalizationHelper.Get("Settings_Desc_ProviderMode", "Habilita personalização da marca e opções para atendimento e laudo técnico");
            if (txtStoreNameLabel != null) txtStoreNameLabel.Text = LocalizationHelper.Get("Settings_Label_StoreName", "Nome personalizado do aplicativo:");
            if (txtStoreNameHint != null) txtStoreNameHint.Text = LocalizationHelper.Get("Settings_Hint_StoreName", "💡 Customize a aplicação de acordo com o nome que você preferir.");
            if (txtPhoneLabel != null) txtPhoneLabel.Text = LocalizationHelper.Get("Settings_Label_Phone", "Informações de Contato:");
            if (txtPhoneHint != null) txtPhoneHint.Text = LocalizationHelper.Get("Settings_Hint_Phone", "💡 Informe um e-mail, número de contato ou rede social que preferir");
            if (chkEnableMaintenanceReport != null) chkEnableMaintenanceReport.Content = LocalizationHelper.Get("Settings_Chk_EnableReport", "Habilitar Registro de Manutenção");
            if (txtEnableReportDesc != null) txtEnableReportDesc.Text = LocalizationHelper.Get("Settings_Desc_EnableReport", "Exibe ou oculta a tela de Registrar Manutenção no menu lateral da aplicação");
            if (txtReportPathLabel != null) txtReportPathLabel.Text = LocalizationHelper.Get("Settings_Label_ReportPath", "Caminho Padrão do Arquivo de Relatório:");
            if (txtLangLabel != null) txtLangLabel.Text = LocalizationHelper.Get("Settings_Label_Language", "Idioma da Aplicação (Language):");
            if (lblCurrentLanguage != null) lblCurrentLanguage.Text = LocalizationHelper.Get("Settings_Current_Lang", (langCode == "en-US") ? "Current: English (en-US)" : "Atual: Português (pt-BR)");
            if (chkSettingWinget != null) chkSettingWinget.Content = LocalizationHelper.Get("Settings_Chk_WingetFallback", "Habilitar download via Winget quando o programa não existir na pasta PostInstall");
            if (chkSettingAutoRefresh != null) chkSettingAutoRefresh.Content = LocalizationHelper.Get("Settings_Chk_AutoRefresh", "Atualizar especificações de hardware automaticamente na inicialização");
            if (btnSaveSettings != null) btnSaveSettings.Content = LocalizationHelper.Get("Btn_SaveSettings", "💾 Salvar Configurações");
            if (btnResetSettings != null) btnResetSettings.Content = LocalizationHelper.Get("Btn_ResetSettings", "🔄 Restaurar Padrões");
            if (txtPersonalizeTitle != null) txtPersonalizeTitle.Text = LocalizationHelper.Get("Settings_Personalize_Title", "🎨 Personalizar o Aplicativo");
            if (txtPersonalizeSub != null) txtPersonalizeSub.Text = LocalizationHelper.Get("Settings_Personalize_Subtitle", "Personalize o tema visual, as cores dos botões principais e acentos do OptiAssist:");
            if (txtThemeLabel != null) txtThemeLabel.Text = LocalizationHelper.Get("Settings_Label_Theme", "Tema Visual:");
            if (txtThemeText != null) txtThemeText.Text = _isDarkMode ? LocalizationHelper.Get("Theme_Light", "Modo Claro") : LocalizationHelper.Get("Theme_Dark", "Modo Escuro");
            if (txtAccentLabel != null) txtAccentLabel.Text = LocalizationHelper.Get("Settings_Label_AccentColor", "Cor de Acento e Botões (Hexadecimal):");
            if (btnOpenColorDialog != null) btnOpenColorDialog.Content = LocalizationHelper.Get("Btn_ColorPicker", "🎨 Seletor de Cores...");
            if (btnApplyCustomColor != null) btnApplyCustomColor.Content = LocalizationHelper.Get("Btn_ApplyColor", "Aplicar Cor");
            if (txtPreviewLabel != null) txtPreviewLabel.Text = LocalizationHelper.Get("Settings_Label_Preview", "Pré-visualização: ");
            if (txtPresetLabel != null) txtPresetLabel.Text = LocalizationHelper.Get("Settings_Label_Presets", "Cores Sugeridas:");

            // Suggested color presets
            if (btnPresetBlue != null) btnPresetBlue.Content = "🔵 " + LocalizationHelper.Get("Color_Blue", "Azul (Padrão)");
            if (btnPresetGreen != null) btnPresetGreen.Content = "🟢 " + LocalizationHelper.Get("Color_Green", "Verde claro");
            if (btnPresetPurple != null) btnPresetPurple.Content = "🟣 " + LocalizationHelper.Get("Color_Purple", "Roxo");
            if (btnPresetOrange != null) btnPresetOrange.Content = "🟠 " + LocalizationHelper.Get("Color_Orange", "Laranja");
            if (btnPresetRed != null) btnPresetRed.Content = "🔴 " + LocalizationHelper.Get("Color_Red", "Vermelho");
            if (btnPresetYellow != null) btnPresetYellow.Content = "🟡 " + LocalizationHelper.Get("Color_Yellow", "Amarelo");
            if (btnPresetPink != null) btnPresetPink.Content = "🌸 " + LocalizationHelper.Get("Color_Pink", "Rosa");

            // 9. About
            if (txtAboutTitle != null) txtAboutTitle.Text = LocalizationHelper.Get("About_Title", "Sobre o OptiAssist");
            if (txtAboutSub != null) txtAboutSub.Text = LocalizationHelper.Get("About_Subtitle", "Informações da versão, direitos autorais e licença de código aberto");
            if (btnAboutGithub != null) btnAboutGithub.Content = LocalizationHelper.Get("Btn_AboutGithub", "🌐 Repositório GitHub");
            if (btnAboutWebsite != null) btnAboutWebsite.Content = LocalizationHelper.Get("Btn_AboutWebsite", "🔗 Site Oficial");

            // 10. Bottom status bar
            if (txtBitLockerLabel != null) txtBitLockerLabel.Text = LocalizationHelper.Get("Status_BitLocker", "🔒 BitLocker: ");
            if (txtTpmLabel != null) txtTpmLabel.Text = LocalizationHelper.Get("Status_Tpm", "🛡️ TPM: ");
            if (txtSecureBootLabel != null) txtSecureBootLabel.Text = LocalizationHelper.Get("Status_SecureBoot", "⚡ Secure Boot: ");

            // 11. Update hardware display if already detected
            if (_hardware != null)
            {
                UpdateHardwareDisplay(_hardware);
            }
        }

        private void ChkServiceProviderMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ServiceProviderMode = chkServiceProviderMode.IsChecked == true;
            }
            UpdateServiceProviderUI();
        }

        private void ChkEnableMaintenanceReport_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableMaintenanceReport = chkEnableMaintenanceReport.IsChecked == true;
            }
            UpdateServiceProviderUI();
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

            MessageBox.Show(LocalizationHelper.Get("Msg_SettingsSaved", "Configurações salvas com sucesso em Config\\settings.json!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("[CONFIG] Application settings updated and saved.");
        }

        private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                LocalizationHelper.Get("Msg_ResetSettingsConfirm", "Deseja restaurar todas as configurações do aplicativo para os valores padrão?"),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            // A fresh AppSettings instance holds the factory defaults
            AppSettings defaults = new AppSettings();
            SettingsHelper.Save(defaults);

            // Reload everything from disk so language, theme, accent color and visibility are re-applied
            LoadApplicationSettings();

            MessageBox.Show(LocalizationHelper.Get("Msg_ResetSettingsDone", "Configurações padrão restauradas com sucesso!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("[CONFIG] Application settings restored to factory defaults.");
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
                MessageBox.Show("Erro ao abrir seletor de cores: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Cor de botões e acentos aplicada com sucesso!\n" + hex, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Código hexadecimal inválido! Use o formato #RRGGBB (ex: #0EA5E9).", "OptiAssist - Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                txtThemeText.Text = isDark ? LocalizationHelper.Get("Theme_Light", "Modo Claro") : LocalizationHelper.Get("Theme_Dark", "Modo Escuro");
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
                Process.Start(new ProcessStartInfo("https://github.com/joaovfigueiro/OptiAssist") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir o link: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAboutWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://joaovfigueiro.github.io/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir o link: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
        #endregion
    }
}
