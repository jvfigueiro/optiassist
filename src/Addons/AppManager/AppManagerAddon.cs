using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace OptiAssist.Addons.AppManager
{
    /// <summary>
    /// Modular App Manager Add-on (Config\Addons\AppManager\AppManager.dll) providing
    /// automated software installation (Local offline installers + Winget) and application updates.
    /// </summary>
    public class AppManagerAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ObservableCollection<InstallAppItem> _apps;
        private ObservableCollection<InstallAppItem> _customEditorApps;
        private ObservableCollection<WingetUpgradeItem> _wingetUpgrades;
        private InstallAppItem _editingAppItem;

        private enum WingetStatusState { Initial, Scanning, UpToDate, Found, Updating, UpdatingAll }
        private WingetStatusState _wingetStatusState = WingetStatusState.Initial;
        private int _wingetFoundCount = 0;
        private int _wingetUpdatingCount = 0;

        // Main Header controls
        private TextBlock _txtPostInstallTitle;
        private TextBlock _txtPostInstallSubtitle;

        // Section 1: Application Updates (Winget)
        private Border _cardWingetUpgrades;
        private TextBlock _txtWingetCardTitle;
        private TextBlock _lblWingetUpgradeStatus;
        private Button _btnCheckWingetUpgrades;
        private ProgressBar _pbWingetScan;
        private StackPanel _pnlWingetLogsAction;
        private Button _btnViewLogsFromUpgrade;
        private Border _borderUpgradesList;
        private ItemsControl _icWingetUpgradesList;
        private Grid _gridUpgradeActions;
        private Button _btnSelectAllUpgrades;
        private Button _btnDeselectAllUpgrades;
        private Button _btnRecheckWingetUpgrades;
        private Button _btnUpgradeAll;
        private Button _btnUpgradeSelected;

        // Section 2: Customize Automated Application Installation
        private Border _cardCustomizePostInstall;
        private TextBlock _txtCustomizeHeaderTitle;
        private TextBlock _txtCustomizeHeaderSub;
        private Button _btnCloseCustomize;
        private TextBlock _txtNewAppNameLabel;
        private TextBox _txtNewAppName;
        private TextBlock _txtNewAppTypeLabel;
        private ComboBox _cmbNewAppType;
        private ComboBoxItem _cmbItemLocal;
        private ComboBoxItem _cmbItemWinget;
        private StackPanel _panelNewAppLocal;
        private TextBlock _txtNewAppLocalLabel;
        private TextBox _txtNewAppLocalFile;
        private Button _btnSelectLocalInstaller;
        private StackPanel _panelNewAppWinget;
        private TextBlock _txtNewAppWingetLabel;
        private TextBox _txtNewAppWingetId;
        private Button _btnAddCustomApp;
        private Button _btnCancelEditApp;
        private CheckBox _chkNewAppPreSelected;
        private TextBlock _txtNewAppSilentLabel;
        private TextBox _txtNewAppSilentArgs;
        private TextBlock _txtCurrentListTitle;
        private ItemsControl _icCustomAppsEditorList;
        private Button _btnSaveCustomApps;
        private Button _btnResetDefaultApps;
        private Button _btnOpenPostInstallJson;

        // Section 3: Automated Application Installation List
        private TextBlock _txtAppsInstallCardTitle;
        private TextBlock _txtAppsInstallCardSub;
        private ItemsControl _icAppsList;
        private ProgressBar _pbInstallProgress;
        private Button _btnSelectEssentials;
        private Button _btnDeselectAll;
        private Button _btnToggleCustomizePostInstall;
        private Button _btnOpenPostInstallFolder;
        private Button _btnRunInstallSelected;

        private FrameworkElement _rootView;

        public string Id { get { return "app-manager"; } }
        public string Name { get { return T("PostInstall_Title", "App Manager"); } }
        public string Description { get { return T("PostInstall_Subtitle", "Automated installation of essential software and update of installed applications"); } }
        public string Version { get { return "1.1.0.0"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "📦"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            LoadPostInstallApps();
            CheckWingetUpgradesAsync();
        }

        private string T(string key, string fallback)
        {
            if (_host != null)
            {
                return _host.GetLocalizedString(key, fallback);
            }
            return fallback;
        }

        private void Log(string msg)
        {
            if (_host != null)
            {
                _host.Log(msg);
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
            if (_icAppsList != null)
            {
                _icAppsList.ItemsSource = _apps;
            }
        }

        public FrameworkElement CreateView()
        {
            if (_rootView != null)
            {
                return _rootView;
            }

            StackPanel root = new StackPanel() { Margin = new Thickness(0) };

            // =========================================================
            // MAIN HEADER
            // =========================================================
            StackPanel headerStack = new StackPanel() { Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center };
            _txtPostInstallTitle = new TextBlock()
            {
                Text = T("PostInstall_Title", "App Manager"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtPostInstallTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtPostInstallSubtitle = new TextBlock()
            {
                Text = T("PostInstall_Subtitle", "Automated installation of essential software and update of installed applications"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtPostInstallSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            headerStack.Children.Add(_txtPostInstallTitle);
            headerStack.Children.Add(_txtPostInstallSubtitle);
            root.Children.Add(headerStack);

            // =========================================================
            // SECTION 1: APPLICATION UPDATES (Always Visible)
            // =========================================================
            _cardWingetUpgrades = new Border()
            {
                Visibility = Visibility.Visible,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            _cardWingetUpgrades.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            _cardWingetUpgrades.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel upgStack = new StackPanel();

            Grid upgHeaderGrid = new Grid() { Margin = new Thickness(0, 0, 0, 8) };
            upgHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            upgHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel upgTitleStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            _txtWingetCardTitle = new TextBlock()
            {
                Text = T("Upgrade_Card_Title", "🔄 Application Updates"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            _txtWingetCardTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _lblWingetUpgradeStatus = new TextBlock()
            {
                Text = T("Upgrade_Status_Initial", "Click 'Check for Updates' or wait for winget scan..."),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _lblWingetUpgradeStatus.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            upgTitleStack.Children.Add(_txtWingetCardTitle);
            upgTitleStack.Children.Add(_lblWingetUpgradeStatus);
            Grid.SetColumn(upgTitleStack, 0);
            upgHeaderGrid.Children.Add(upgTitleStack);

            _btnCheckWingetUpgrades = new Button()
            {
                Content = T("Btn_CheckUpdates", "🔄 Check for Updates"),
                Padding = new Thickness(10, 3, 10, 3)
            };
            _btnCheckWingetUpgrades.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCheckWingetUpgrades.Click += BtnCheckWingetUpgrades_Click;
            Grid.SetColumn(_btnCheckWingetUpgrades, 1);
            upgHeaderGrid.Children.Add(_btnCheckWingetUpgrades);

            upgStack.Children.Add(upgHeaderGrid);

            _pbWingetScan = new ProgressBar()
            {
                IsIndeterminate = true,
                Height = 6,
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(0),
                Visibility = (_wingetStatusState == WingetStatusState.Scanning || _wingetStatusState == WingetStatusState.Updating || _wingetStatusState == WingetStatusState.UpdatingAll)
                    ? Visibility.Visible : Visibility.Collapsed
            };
            _pbWingetScan.SetResourceReference(ProgressBar.ForegroundProperty, "BrushAccent");
            _pbWingetScan.SetResourceReference(ProgressBar.BackgroundProperty, "BrushBgPrimary");
            upgStack.Children.Add(_pbWingetScan);

            _pnlWingetLogsAction = new StackPanel()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            _btnViewLogsFromUpgrade = new Button()
            {
                Content = T("Btn_ViewLogs", "⬛ View Logs"),
                Height = 28,
                Padding = new Thickness(14, 0, 14, 0),
                ToolTip = T("Tooltip_ViewLogs", "Monitor execution log in real time on the Logs screen")
            };
            _btnViewLogsFromUpgrade.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnViewLogsFromUpgrade.Click += (s, e) =>
            {
                if (_host != null) _host.OpenLogWindow();
            };
            _pnlWingetLogsAction.Children.Add(_btnViewLogsFromUpgrade);
            upgStack.Children.Add(_pnlWingetLogsAction);

            _borderUpgradesList = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4),
                MaxHeight = 220,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = (_wingetUpgrades != null && _wingetUpgrades.Count > 0) ? Visibility.Visible : Visibility.Collapsed
            };
            _borderUpgradesList.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            _borderUpgradesList.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            ScrollViewer svUpgrades = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _icWingetUpgradesList = new ItemsControl();
            _icWingetUpgradesList.ItemTemplate = BuildWingetUpgradeItemTemplate();
            if (_wingetUpgrades != null)
            {
                _icWingetUpgradesList.ItemsSource = _wingetUpgrades;
            }
            svUpgrades.Content = _icWingetUpgradesList;
            _borderUpgradesList.Child = svUpgrades;
            upgStack.Children.Add(_borderUpgradesList);

            _gridUpgradeActions = new Grid()
            {
                Visibility = (_wingetUpgrades != null && _wingetUpgrades.Count > 0) ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _gridUpgradeActions.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            _gridUpgradeActions.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            _gridUpgradeActions.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel upgLeftBtns = new StackPanel() { Orientation = Orientation.Horizontal };
            _btnSelectAllUpgrades = new Button()
            {
                Content = T("Btn_SelectAll", "Select All"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnSelectAllUpgrades.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectAllUpgrades.Click += BtnSelectAllUpgrades_Click;

            _btnDeselectAllUpgrades = new Button()
            {
                Content = T("Btn_DeselectAll", "Deselect All"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnDeselectAllUpgrades.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeselectAllUpgrades.Click += BtnDeselectAllUpgrades_Click;

            _btnRecheckWingetUpgrades = new Button()
            {
                Content = T("Btn_Recheck", "🔄 Re-check"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnRecheckWingetUpgrades.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnRecheckWingetUpgrades.Click += BtnCheckWingetUpgrades_Click;

            upgLeftBtns.Children.Add(_btnSelectAllUpgrades);
            upgLeftBtns.Children.Add(_btnDeselectAllUpgrades);
            upgLeftBtns.Children.Add(_btnRecheckWingetUpgrades);
            Grid.SetColumn(upgLeftBtns, 0);
            _gridUpgradeActions.Children.Add(upgLeftBtns);

            StackPanel upgRightBtns = new StackPanel() { Orientation = Orientation.Horizontal };
            _btnUpgradeAll = new Button()
            {
                Content = T("Btn_UpgradeAll", "🚀 Update All"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnUpgradeAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnUpgradeAll.Click += BtnUpgradeAll_Click;

            _btnUpgradeSelected = new Button()
            {
                Content = T("Btn_UpgradeSelected", "🔄 Update Selected")
            };
            _btnUpgradeSelected.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnUpgradeSelected.Click += BtnUpgradeSelected_Click;

            upgRightBtns.Children.Add(_btnUpgradeAll);
            upgRightBtns.Children.Add(_btnUpgradeSelected);
            Grid.SetColumn(upgRightBtns, 2);
            _gridUpgradeActions.Children.Add(upgRightBtns);

            upgStack.Children.Add(_gridUpgradeActions);
            _cardWingetUpgrades.Child = upgStack;
            root.Children.Add(_cardWingetUpgrades);

            UpdateWingetStatusText();

            // =========================================================
            // SECTION 2: EXPANDABLE CUSTOMIZATION PANEL (autoinstall.json)
            // =========================================================
            _cardCustomizePostInstall = new Border()
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            _cardCustomizePostInstall.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            _cardCustomizePostInstall.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");

            StackPanel custStack = new StackPanel();

            Grid custHeaderGrid = new Grid() { Margin = new Thickness(0, 0, 0, 8) };
            custHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            custHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel custTitleStack = new StackPanel();
            _txtCustomizeHeaderTitle = new TextBlock()
            {
                Text = T("Custom_Card_Title", "⚙️ Customize automated application installation"),
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            _txtCustomizeHeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _txtCustomizeHeaderSub = new TextBlock()
            {
                Text = T("Custom_Card_Subtitle", "Add new programs, set default automation, and choose between Local installer or Winget."),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            _txtCustomizeHeaderSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            custTitleStack.Children.Add(_txtCustomizeHeaderTitle);
            custTitleStack.Children.Add(_txtCustomizeHeaderSub);
            Grid.SetColumn(custTitleStack, 0);
            custHeaderGrid.Children.Add(custTitleStack);

            _btnCloseCustomize = new Button()
            {
                Content = T("Btn_Close", "✕ Close"),
                Padding = new Thickness(8, 2, 8, 2)
            };
            _btnCloseCustomize.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCloseCustomize.Click += (s, e) => { _cardCustomizePostInstall.Visibility = Visibility.Collapsed; };
            Grid.SetColumn(_btnCloseCustomize, 1);
            custHeaderGrid.Children.Add(_btnCloseCustomize);

            custStack.Children.Add(custHeaderGrid);

            // Program Add/Edit Form Box
            Border formBox = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            formBox.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            formBox.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid formGrid = new Grid();
            formGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.8, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.5, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2.2, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            // Col 0: Program Name
            StackPanel colName = new StackPanel() { Margin = new Thickness(0, 0, 8, 6) };
            _txtNewAppNameLabel = new TextBlock()
            {
                Text = T("Custom_Label_Name", "Program Name:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtNewAppNameLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtNewAppName = new TextBox() { Height = 26 };
            colName.Children.Add(_txtNewAppNameLabel);
            colName.Children.Add(_txtNewAppName);
            Grid.SetRow(colName, 0);
            Grid.SetColumn(colName, 0);
            formGrid.Children.Add(colName);

            // Col 1: Installation Type
            StackPanel colType = new StackPanel() { Margin = new Thickness(0, 0, 8, 6) };
            _txtNewAppTypeLabel = new TextBlock()
            {
                Text = T("Custom_Label_Type", "Installation Type:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtNewAppTypeLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _cmbNewAppType = new ComboBox() { Height = 26 };
            _cmbItemLocal = new ComboBoxItem() { Content = T("Custom_Type_Local", "📁 Local (Installers)"), Tag = "Local" };
            _cmbItemWinget = new ComboBoxItem() { Content = T("Custom_Type_Winget", "🌐 Winget (Repository)"), Tag = "Winget" };
            _cmbNewAppType.Items.Add(_cmbItemLocal);
            _cmbNewAppType.Items.Add(_cmbItemWinget);
            _cmbNewAppType.SelectedIndex = 0;
            _cmbNewAppType.SelectionChanged += CmbNewAppType_SelectionChanged;
            colType.Children.Add(_txtNewAppTypeLabel);
            colType.Children.Add(_cmbNewAppType);
            Grid.SetRow(colType, 0);
            Grid.SetColumn(colType, 1);
            formGrid.Children.Add(colType);

            // Col 2 (Local): Executable in Installers folder
            _panelNewAppLocal = new StackPanel() { Margin = new Thickness(0, 0, 8, 6), Visibility = Visibility.Visible };
            _txtNewAppLocalLabel = new TextBlock()
            {
                Text = T("Custom_Label_LocalFile", "Executable in Installers folder:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtNewAppLocalLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            Grid localFileGrid = new Grid();
            localFileGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            localFileGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _txtNewAppLocalFile = new TextBox()
            {
                Height = 26,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = T("Tooltip_NewAppLocalFile", "Name of the executable in Installers folder (e.g. OfficeSetup.exe)")
            };
            Grid.SetColumn(_txtNewAppLocalFile, 0);
            localFileGrid.Children.Add(_txtNewAppLocalFile);

            _btnSelectLocalInstaller = new Button()
            {
                Content = T("Btn_SelectLocal", "📁 Browse..."),
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                ToolTip = T("Tooltip_SelectLocalInstaller", "Select executable file from Installers folder")
            };
            _btnSelectLocalInstaller.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectLocalInstaller.Click += BtnSelectLocalInstaller_Click;
            Grid.SetColumn(_btnSelectLocalInstaller, 1);
            localFileGrid.Children.Add(_btnSelectLocalInstaller);

            _panelNewAppLocal.Children.Add(_txtNewAppLocalLabel);
            _panelNewAppLocal.Children.Add(localFileGrid);
            Grid.SetRow(_panelNewAppLocal, 0);
            Grid.SetColumn(_panelNewAppLocal, 2);
            formGrid.Children.Add(_panelNewAppLocal);

            // Col 2 (Winget): Winget App ID
            _panelNewAppWinget = new StackPanel() { Margin = new Thickness(0, 0, 8, 6), Visibility = Visibility.Collapsed };
            _txtNewAppWingetLabel = new TextBlock()
            {
                Text = T("Custom_Label_WingetId", "Winget App ID (e.g. Google.Chrome):"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtNewAppWingetLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtNewAppWingetId = new TextBox()
            {
                Height = 26,
                ToolTip = T("Tooltip_NewAppWingetId", "Package identifier in Winget repository")
            };
            _panelNewAppWinget.Children.Add(_txtNewAppWingetLabel);
            _panelNewAppWinget.Children.Add(_txtNewAppWingetId);
            Grid.SetRow(_panelNewAppWinget, 0);
            Grid.SetColumn(_panelNewAppWinget, 2);
            formGrid.Children.Add(_panelNewAppWinget);

            // Col 3: Add / Save and Cancel Buttons
            StackPanel colAddBtns = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _btnAddCustomApp = new Button()
            {
                Content = T("Btn_Add", "➕ Add"),
                Height = 26,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 4, 0)
            };
            _btnAddCustomApp.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnAddCustomApp.Click += BtnAddCustomApp_Click;

            _btnCancelEditApp = new Button()
            {
                Content = "✕",
                Height = 26,
                Width = 26,
                Visibility = Visibility.Collapsed,
                ToolTip = T("Tooltip_CancelEdit", "Cancel edit")
            };
            _btnCancelEditApp.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCancelEditApp.Click += BtnCancelEditApp_Click;

            colAddBtns.Children.Add(_btnAddCustomApp);
            colAddBtns.Children.Add(_btnCancelEditApp);
            Grid.SetRow(colAddBtns, 0);
            Grid.SetColumn(colAddBtns, 3);
            formGrid.Children.Add(colAddBtns);

            // Row 1: Automate and Silent Arguments
            StackPanel rowSilent = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _chkNewAppPreSelected = new CheckBox()
            {
                Content = T("Custom_Chk_AutomateDefault", "Automate by default"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            _txtNewAppSilentLabel = new TextBlock()
            {
                Text = T("Custom_Label_SilentArgs", "Silent arguments (optional):"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _txtNewAppSilentLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtNewAppSilentArgs = new TextBox() { Width = 180, Height = 24 };

            rowSilent.Children.Add(_chkNewAppPreSelected);
            rowSilent.Children.Add(_txtNewAppSilentLabel);
            rowSilent.Children.Add(_txtNewAppSilentArgs);
            Grid.SetRow(rowSilent, 1);
            Grid.SetColumn(rowSilent, 0);
            Grid.SetColumnSpan(rowSilent, 4);
            formGrid.Children.Add(rowSilent);

            formBox.Child = formGrid;
            custStack.Children.Add(formBox);

            // Current List Title
            _txtCurrentListTitle = new TextBlock()
            {
                Text = T("Custom_CurrentList_Title", "Programs in current list:"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _txtCurrentListTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            custStack.Children.Add(_txtCurrentListTitle);

            Border borderEditorList = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4),
                MaxHeight = 190
            };
            borderEditorList.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            borderEditorList.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            ScrollViewer svEditorList = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _icCustomAppsEditorList = new ItemsControl();
            _icCustomAppsEditorList.ItemTemplate = BuildCustomAppsEditorItemTemplate();
            svEditorList.Content = _icCustomAppsEditorList;
            borderEditorList.Child = svEditorList;
            custStack.Children.Add(borderEditorList);

            // Editor Footer Actions
            Grid editorFooterGrid = new Grid() { Margin = new Thickness(0, 8, 0, 0) };
            editorFooterGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            editorFooterGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            editorFooterGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel editorLeftBtns = new StackPanel() { Orientation = Orientation.Horizontal };
            _btnSaveCustomApps = new Button()
            {
                Content = T("Btn_SaveCustomApps", "💾 Save Changes"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnSaveCustomApps.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnSaveCustomApps.Click += BtnSaveCustomApps_Click;

            _btnResetDefaultApps = new Button()
            {
                Content = T("Btn_ResetDefaultApps", "🔄 Reset to Defaults"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnResetDefaultApps.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnResetDefaultApps.Click += BtnResetDefaultApps_Click;

            editorLeftBtns.Children.Add(_btnSaveCustomApps);
            editorLeftBtns.Children.Add(_btnResetDefaultApps);
            Grid.SetColumn(editorLeftBtns, 0);
            editorFooterGrid.Children.Add(editorLeftBtns);

            StackPanel editorRightBtns = new StackPanel() { Orientation = Orientation.Horizontal };
            _btnOpenPostInstallJson = new Button()
            {
                Content = T("Btn_OpenPostInstallJson", "📂 Open autoinstall.json")
            };
            _btnOpenPostInstallJson.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenPostInstallJson.Click += (s, e) => InstallerHelper.OpenPostInstallJson();
            editorRightBtns.Children.Add(_btnOpenPostInstallJson);
            Grid.SetColumn(editorRightBtns, 2);
            editorFooterGrid.Children.Add(editorRightBtns);

            custStack.Children.Add(editorFooterGrid);
            _cardCustomizePostInstall.Child = custStack;
            root.Children.Add(_cardCustomizePostInstall);

            // =========================================================
            // SECTION 3: AUTOMATED APPLICATION INSTALLATION LIST
            // =========================================================
            Border cardAppsInstall = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            cardAppsInstall.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            cardAppsInstall.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel installStack = new StackPanel();
            _txtAppsInstallCardTitle = new TextBlock()
            {
                Text = T("Install_Card_Title", "📦 Automated Application Installation"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtAppsInstallCardTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _txtAppsInstallCardSub = new TextBlock()
            {
                Text = T("Install_Card_Subtitle", "Select the programs you want to batch install automatically on this computer:"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _txtAppsInstallCardSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            installStack.Children.Add(_txtAppsInstallCardTitle);
            installStack.Children.Add(_txtAppsInstallCardSub);

            Border borderAppsList = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4),
                MaxHeight = 260,
                Margin = new Thickness(0, 0, 0, 10)
            };
            borderAppsList.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            borderAppsList.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            ScrollViewer svAppsList = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _icAppsList = new ItemsControl();
            _icAppsList.ItemTemplate = BuildInstallAppItemTemplate();
            _icAppsList.ItemsSource = _apps;
            svAppsList.Content = _icAppsList;
            borderAppsList.Child = svAppsList;
            installStack.Children.Add(borderAppsList);

            _pbInstallProgress = new ProgressBar()
            {
                Height = 6,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(0)
            };
            _pbInstallProgress.SetResourceReference(ProgressBar.ForegroundProperty, "BrushAccent");
            _pbInstallProgress.SetResourceReference(ProgressBar.BackgroundProperty, "BrushBgPrimary");
            installStack.Children.Add(_pbInstallProgress);

            Grid installActionsGrid = new Grid();
            installActionsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            installActionsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            installActionsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel installLeftBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _btnSelectEssentials = new Button()
            {
                Content = T("Btn_SelectEssentials", "Select Essentials"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnSelectEssentials.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectEssentials.Click += BtnSelectEssentials_Click;

            _btnDeselectAll = new Button()
            {
                Content = T("Btn_DeselectAll", "Deselect All"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnDeselectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeselectAll.Click += BtnDeselectAll_Click;

            _btnToggleCustomizePostInstall = new Button()
            {
                Content = T("Btn_CustomizeAutomated", "⚙️ Customize Automated Installation"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnToggleCustomizePostInstall.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnToggleCustomizePostInstall.Click += BtnToggleCustomizePostInstall_Click;

            _btnOpenPostInstallFolder = new Button()
            {
                Content = T("Btn_OpenPostInstallFolder", "📂 Installers Folder")
            };
            _btnOpenPostInstallFolder.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenPostInstallFolder.Click += (s, e) => InstallerHelper.OpenPostInstallFolder();

            installLeftBtns.Children.Add(_btnSelectEssentials);
            installLeftBtns.Children.Add(_btnDeselectAll);
            installLeftBtns.Children.Add(_btnToggleCustomizePostInstall);
            installLeftBtns.Children.Add(_btnOpenPostInstallFolder);
            Grid.SetColumn(installLeftBtns, 0);
            installActionsGrid.Children.Add(installLeftBtns);

            StackPanel installRightBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _btnRunInstallSelected = new Button()
            {
                Content = T("Btn_RunInstallSelected", "🚀 Install Selected"),
                Padding = new Thickness(16, 0, 16, 0),
                Height = 30
            };
            _btnRunInstallSelected.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnRunInstallSelected.Click += BtnInstallSelected_Click;

            installRightBtns.Children.Add(_btnRunInstallSelected);
            Grid.SetColumn(installRightBtns, 2);
            installActionsGrid.Children.Add(installRightBtns);

            installStack.Children.Add(installActionsGrid);
            cardAppsInstall.Child = installStack;
            root.Children.Add(cardAppsInstall);

            _rootView = root;
            return _rootView;
        }

        private DataTemplate BuildWingetUpgradeItemTemplate()
        {
            DataTemplate dt = new DataTemplate(typeof(WingetUpgradeItem));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetValue(Border.PaddingProperty, new Thickness(4, 5, 4, 5));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(220));
            FrameworkElementFactory col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(200));
            FrameworkElementFactory col3 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col3.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            grid.AppendChild(col0);
            grid.AppendChild(col1);
            grid.AppendChild(col2);
            grid.AppendChild(col3);

            FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
            chk.SetValue(Grid.ColumnProperty, 0);
            chk.SetBinding(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay });
            chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chk.SetValue(CheckBox.MarginProperty, new Thickness(0, 0, 8, 0));

            FrameworkElementFactory tbName = new FrameworkElementFactory(typeof(TextBlock));
            tbName.SetValue(Grid.ColumnProperty, 1);
            tbName.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            tbName.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            tbName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            tbName.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            FrameworkElementFactory tbVer = new FrameworkElementFactory(typeof(TextBlock));
            tbVer.SetValue(Grid.ColumnProperty, 2);
            tbVer.SetBinding(TextBlock.TextProperty, new Binding("VersionDisplay"));
            tbVer.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            tbVer.SetValue(TextBlock.FontSizeProperty, 11.0);
            tbVer.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            tbVer.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbVer.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            FrameworkElementFactory tbId = new FrameworkElementFactory(typeof(TextBlock));
            tbId.SetValue(Grid.ColumnProperty, 3);
            tbId.SetBinding(TextBlock.TextProperty, new Binding("Id"));
            tbId.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            tbId.SetValue(TextBlock.FontSizeProperty, 10.0);
            tbId.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbId.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            grid.AppendChild(chk);
            grid.AppendChild(tbName);
            grid.AppendChild(tbVer);
            grid.AppendChild(tbId);
            border.AppendChild(grid);

            dt.VisualTree = border;
            return dt;
        }

        private DataTemplate BuildCustomAppsEditorItemTemplate()
        {
            DataTemplate dt = new DataTemplate(typeof(InstallAppItem));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetValue(Border.PaddingProperty, new Thickness(4, 4, 4, 4));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(180));
            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(150));
            FrameworkElementFactory col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(120));
            FrameworkElementFactory col3 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col3.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            FrameworkElementFactory col4 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col4.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

            grid.AppendChild(col0);
            grid.AppendChild(col1);
            grid.AppendChild(col2);
            grid.AppendChild(col3);
            grid.AppendChild(col4);

            FrameworkElementFactory tbName = new FrameworkElementFactory(typeof(TextBlock));
            tbName.SetValue(Grid.ColumnProperty, 0);
            tbName.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            tbName.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            tbName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            tbName.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            FrameworkElementFactory tbType = new FrameworkElementFactory(typeof(TextBlock));
            tbType.SetValue(Grid.ColumnProperty, 1);
            tbType.SetBinding(TextBlock.TextProperty, new Binding("DisplayTypeInfo"));
            tbType.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            tbType.SetValue(TextBlock.FontSizeProperty, 11.0);
            tbType.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbType.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            FrameworkElementFactory chkAuto = new FrameworkElementFactory(typeof(CheckBox));
            chkAuto.SetValue(Grid.ColumnProperty, 2);
            chkAuto.SetBinding(CheckBox.ContentProperty, new Binding("AutomateLabel"));
            chkAuto.SetBinding(CheckBox.IsCheckedProperty, new Binding("PreSelected") { Mode = BindingMode.TwoWay });
            chkAuto.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory tbDetail = new FrameworkElementFactory(typeof(TextBlock));
            tbDetail.SetValue(Grid.ColumnProperty, 3);
            tbDetail.SetBinding(TextBlock.TextProperty, new Binding("DetailInfo"));
            tbDetail.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            tbDetail.SetValue(TextBlock.FontSizeProperty, 10.0);
            tbDetail.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbDetail.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            FrameworkElementFactory actionsStack = new FrameworkElementFactory(typeof(StackPanel));
            actionsStack.SetValue(Grid.ColumnProperty, 4);
            actionsStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            actionsStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory btnUp = new FrameworkElementFactory(typeof(Button));
            btnUp.SetValue(Button.ContentProperty, "▲");
            btnUp.SetValue(Button.WidthProperty, 28.0);
            btnUp.SetValue(Button.HeightProperty, 24.0);
            btnUp.SetValue(Button.MarginProperty, new Thickness(0, 0, 3, 0));
            btnUp.SetResourceReference(Button.StyleProperty, "BtnRowAction");
            btnUp.SetBinding(Button.TagProperty, new Binding());
            btnUp.SetBinding(Button.ToolTipProperty, new Binding("ToolTipMoveUp"));
            btnUp.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnMoveAppUp_Click));

            FrameworkElementFactory btnDown = new FrameworkElementFactory(typeof(Button));
            btnDown.SetValue(Button.ContentProperty, "▼");
            btnDown.SetValue(Button.WidthProperty, 28.0);
            btnDown.SetValue(Button.HeightProperty, 24.0);
            btnDown.SetValue(Button.MarginProperty, new Thickness(0, 0, 3, 0));
            btnDown.SetResourceReference(Button.StyleProperty, "BtnRowAction");
            btnDown.SetBinding(Button.TagProperty, new Binding());
            btnDown.SetBinding(Button.ToolTipProperty, new Binding("ToolTipMoveDown"));
            btnDown.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnMoveAppDown_Click));

            FrameworkElementFactory btnEdit = new FrameworkElementFactory(typeof(Button));
            btnEdit.SetValue(Button.ContentProperty, "✏");
            btnEdit.SetValue(Button.WidthProperty, 28.0);
            btnEdit.SetValue(Button.HeightProperty, 24.0);
            btnEdit.SetValue(Button.MarginProperty, new Thickness(0, 0, 3, 0));
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnRowAction");
            btnEdit.SetBinding(Button.TagProperty, new Binding());
            btnEdit.SetBinding(Button.ToolTipProperty, new Binding("ToolTipEdit"));
            btnEdit.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnEditCustomApp_Click));

            FrameworkElementFactory btnDel = new FrameworkElementFactory(typeof(Button));
            btnDel.SetValue(Button.ContentProperty, "🗑");
            btnDel.SetValue(Button.WidthProperty, 28.0);
            btnDel.SetValue(Button.HeightProperty, 24.0);
            btnDel.SetResourceReference(Button.StyleProperty, "BtnRowAction");
            btnDel.SetBinding(Button.TagProperty, new Binding());
            btnDel.SetBinding(Button.ToolTipProperty, new Binding("ToolTipRemove"));
            btnDel.AddHandler(Button.ClickEvent, new RoutedEventHandler(BtnRemoveCustomApp_Click));

            actionsStack.AppendChild(btnUp);
            actionsStack.AppendChild(btnDown);
            actionsStack.AppendChild(btnEdit);
            actionsStack.AppendChild(btnDel);

            grid.AppendChild(tbName);
            grid.AppendChild(tbType);
            grid.AppendChild(chkAuto);
            grid.AppendChild(tbDetail);
            grid.AppendChild(actionsStack);
            border.AppendChild(grid);

            dt.VisualTree = border;
            return dt;
        }

        private DataTemplate BuildInstallAppItemTemplate()
        {
            DataTemplate dt = new DataTemplate(typeof(InstallAppItem));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetValue(Border.PaddingProperty, new Thickness(6, 7, 6, 7));

            FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
            chk.SetBinding(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay });
            chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            tb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            tb.SetValue(TextBlock.FontSizeProperty, 12.0);
            tb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tb.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            chk.AppendChild(tb);
            border.AppendChild(chk);

            dt.VisualTree = border;
            return dt;
        }

        // =========================================================
        // AUTOMATED INSTALLATION HANDLERS
        // =========================================================
        public void RunAutomatedPostInstall()
        {
            CreateView();
            BtnInstallSelected_Click(null, null);
        }

        private void BtnSelectEssentials_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) return;
            foreach (var app in _apps)
            {
                app.Selected = app.PreSelected;
            }
            if (_icAppsList != null) _icAppsList.Items.Refresh();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) return;
            foreach (var app in _apps)
            {
                app.Selected = false;
            }
            if (_icAppsList != null) _icAppsList.Items.Refresh();
        }

        private void BtnToggleCustomizePostInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_cardCustomizePostInstall == null) return;
            if (_cardCustomizePostInstall.Visibility == Visibility.Visible)
            {
                _cardCustomizePostInstall.Visibility = Visibility.Collapsed;
            }
            else
            {
                _cardCustomizePostInstall.Visibility = Visibility.Visible;
                PopulateCustomAppsEditor();
            }
        }

        private void PopulateCustomAppsEditor()
        {
            var list = InstallerHelper.LoadApps();
            _customEditorApps = new ObservableCollection<InstallAppItem>(list);
            if (_icCustomAppsEditorList != null)
            {
                _icCustomAppsEditorList.ItemsSource = _customEditorApps;
            }
        }

        private void CmbNewAppType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_panelNewAppLocal == null || _panelNewAppWinget == null) return;
            var item = _cmbNewAppType.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null && item.Tag.ToString() == "Winget")
            {
                _panelNewAppLocal.Visibility = Visibility.Collapsed;
                _panelNewAppWinget.Visibility = Visibility.Visible;
            }
            else
            {
                _panelNewAppLocal.Visibility = Visibility.Visible;
                _panelNewAppWinget.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSelectLocalInstaller_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string postDir = InstallerHelper.GetInstallersDirectory();
                if (!Directory.Exists(postDir)) Directory.CreateDirectory(postDir);

                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.InitialDirectory = Path.GetFullPath(postDir);
                dlg.Title = T("Msg_SelectInstallerTitle", "Select installer in Installers folder");
                dlg.Filter = T("Msg_SelectInstallerFilter", "Installers (*.exe;*.msi;*.msixbundle;*.appxbundle)|*.exe;*.msi;*.msixbundle;*.appxbundle|All Files (*.*)|*.*");
                if (dlg.ShowDialog() == true)
                {
                    string fileName = Path.GetFileName(dlg.FileName);
                    _txtNewAppLocalFile.Text = fileName;
                    if (string.IsNullOrWhiteSpace(_txtNewAppName.Text))
                    {
                        string clean = Path.GetFileNameWithoutExtension(fileName)
                            .Replace("Setup", "")
                            .Replace("setup", "")
                            .Replace("_", " ")
                            .Trim();
                        if (!string.IsNullOrEmpty(clean))
                            _txtNewAppName.Text = clean;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("Msg_SelectInstallerError", "Error selecting file: {0}"), ex.Message), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
        {
            string name = _txtNewAppName.Text != null ? _txtNewAppName.Text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(T("Msg_EnterAppName", "Please enter the program name."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtNewAppName.Focus();
                return;
            }

            string installType = "Local";
            var selectedTypeItem = _cmbNewAppType.SelectedItem as ComboBoxItem;
            if (selectedTypeItem != null && selectedTypeItem.Tag != null)
            {
                installType = selectedTypeItem.Tag.ToString();
            }

            string localFile = _txtNewAppLocalFile.Text != null ? _txtNewAppLocalFile.Text.Trim() : "";
            string wingetId = _txtNewAppWingetId.Text != null ? _txtNewAppWingetId.Text.Trim() : "";
            string silentArgs = _txtNewAppSilentArgs.Text != null ? _txtNewAppSilentArgs.Text.Trim() : "";
            bool preSelected = _chkNewAppPreSelected.IsChecked == true;

            if (installType == "Winget" && string.IsNullOrEmpty(wingetId))
            {
                wingetId = name;
            }

            if (_customEditorApps == null)
            {
                _customEditorApps = new ObservableCollection<InstallAppItem>();
                _icCustomAppsEditorList.ItemsSource = _customEditorApps;
            }

            if (_editingAppItem != null)
            {
                _editingAppItem.Name = name;
                _editingAppItem.InstallType = installType;
                _editingAppItem.LocalInstallerFile = localFile;
                _editingAppItem.WingetId = wingetId;
                _editingAppItem.PreSelected = preSelected;
                _editingAppItem.SilentArgs = silentArgs;

                _editingAppItem = null;
                _btnAddCustomApp.Content = T("Btn_Add", "➕ Add");
                _btnCancelEditApp.Visibility = Visibility.Collapsed;
                _icCustomAppsEditorList.Items.Refresh();
            }
            else
            {
                var newItem = new InstallAppItem(name, preSelected, installType, wingetId, silentArgs, localFile);
                _customEditorApps.Add(newItem);
            }

            ClearCustomAppForm();
        }

        private void BtnEditCustomApp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as InstallAppItem;
            if (item == null) return;

            _editingAppItem = item;
            _txtNewAppName.Text = item.Name;
            _cmbNewAppType.SelectedIndex = (item.InstallType == "Winget") ? 1 : 0;
            _txtNewAppLocalFile.Text = item.LocalInstallerFile;
            _txtNewAppWingetId.Text = item.WingetId;
            _chkNewAppPreSelected.IsChecked = item.PreSelected;
            _txtNewAppSilentArgs.Text = item.SilentArgs;

            _btnAddCustomApp.Content = T("Btn_Update", "💾 Update");
            _btnCancelEditApp.Visibility = Visibility.Visible;
            _txtNewAppName.Focus();
        }

        private void BtnCancelEditApp_Click(object sender, RoutedEventArgs e)
        {
            _editingAppItem = null;
            ClearCustomAppForm();
            _btnAddCustomApp.Content = T("Btn_Add", "➕ Add");
            _btnCancelEditApp.Visibility = Visibility.Collapsed;
        }

        private void ClearCustomAppForm()
        {
            _txtNewAppName.Text = "";
            _txtNewAppLocalFile.Text = "";
            _txtNewAppWingetId.Text = "";
            _txtNewAppSilentArgs.Text = "";
            _chkNewAppPreSelected.IsChecked = false;
            _cmbNewAppType.SelectedIndex = 0;
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
            MessageBox.Show(T("Msg_CustomAppsSaved", "Application list successfully saved in Config\\Addons\\AppManager\\autoinstall.json!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnResetDefaultApps_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(T("Msg_ResetDefaultAppsConfirm", "Do you want to restore the default program list in autoinstall.json?"), "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var defaults = InstallerHelper.GetDefaultApps();
                InstallerHelper.SaveApps(defaults);
                PopulateCustomAppsEditor();
                LoadPostInstallApps();
                MessageBox.Show(T("Msg_ResetDefaultAppsDone", "Default program list successfully restored!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnInstallSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_apps == null) LoadPostInstallApps();
            if (_apps == null) return;

            List<InstallAppItem> selected = new List<InstallAppItem>();
            foreach (var app in _apps)
            {
                if (app.Selected) selected.Add(app);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoAppsSelected", "No application was selected."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_btnRunInstallSelected != null) _btnRunInstallSelected.IsEnabled = false;
            if (_pbInstallProgress != null)
            {
                _pbInstallProgress.Value = 0;
                _pbInstallProgress.Maximum = selected.Count;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                Log("=================================================");
                Log("   STARTING AUTOMATED SOFTWARE INSTALLATION      ");
                Log(string.Format("   Total programs to install: {0}", selected.Count));
                Log("   (Configuration: Config\\Addons\\AppManager\\autoinstall.json)");
                Log("=================================================\n");

                bool wingetFallback = (_host != null && _host.Settings != null) ? _host.Settings.EnableWingetFallback : true;

                int current = 0;
                foreach (var app in selected)
                {
                    current++;
                    Log(string.Format("[{0}/{1}] Processing: {2}...", current, selected.Count, app.Name));
                    InstallerHelper.InstallApp(app, wingetFallback, msg => Log(msg));
                    Log("");

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            if (_pbInstallProgress != null && _pbInstallProgress.Value < _pbInstallProgress.Maximum)
                                _pbInstallProgress.Value++;
                        }));
                    }
                }

                Log("=================================================");
                Log("   PROGRAM INSTALLATION COMPLETED!               ");
                Log("=================================================");

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_btnRunInstallSelected != null) _btnRunInstallSelected.IsEnabled = true;
                        MessageBox.Show(T("Msg_InstallCompleted", "Installation of selected programs completed!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                    }));
                }
            });
        }

        // =========================================================
        // APPLICATION UPDATES (WINGET UPGRADE)
        // =========================================================
        private void UpdateWingetStatusText()
        {
            if (_lblWingetUpgradeStatus == null) return;
            switch (_wingetStatusState)
            {
                case WingetStatusState.Initial:
                    _lblWingetUpgradeStatus.Text = T("Upgrade_Status_Initial", "Click 'Check for Updates' or wait for winget scan...");
                    break;
                case WingetStatusState.Scanning:
                    _lblWingetUpgradeStatus.Text = T("Upgrade_Status_Scanning", "Scanning for available updates via winget...");
                    break;
                case WingetStatusState.UpToDate:
                    _lblWingetUpgradeStatus.Text = T("Upgrade_Status_UpToDate", "✔️ All installed applications are already up to date!");
                    break;
                case WingetStatusState.Found:
                    _lblWingetUpgradeStatus.Text = string.Format(T("Upgrade_Status_Found", "Updates found for {0} application(s):"), _wingetFoundCount);
                    break;
                case WingetStatusState.Updating:
                    _lblWingetUpgradeStatus.Text = string.Format(T("Upgrade_Status_Updating", "Updating {0} selected application(s)... Track progress in Logs."), _wingetUpdatingCount);
                    break;
                case WingetStatusState.UpdatingAll:
                    _lblWingetUpgradeStatus.Text = T("Upgrade_Status_UpdatingAll", "Running 'winget upgrade --all' in background... Track progress in Logs.");
                    break;
            }
        }

        private void CheckWingetUpgradesAsync()
        {
            _wingetStatusState = WingetStatusState.Scanning;
            if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Visible;
            UpdateWingetStatusText();
            if (_borderUpgradesList != null) _borderUpgradesList.Visibility = Visibility.Collapsed;
            if (_gridUpgradeActions != null) _gridUpgradeActions.Visibility = Visibility.Collapsed;

            ThreadPool.QueueUserWorkItem(state =>
            {
                var upgrades = InstallerHelper.CheckAvailableUpgrades(msg => Log(msg));

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Collapsed;

                        if (upgrades == null || upgrades.Count == 0)
                        {
                            _wingetStatusState = WingetStatusState.UpToDate;
                            UpdateWingetStatusText();
                            if (_borderUpgradesList != null) _borderUpgradesList.Visibility = Visibility.Collapsed;
                            if (_gridUpgradeActions != null) _gridUpgradeActions.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            _wingetFoundCount = upgrades.Count;
                            _wingetStatusState = WingetStatusState.Found;
                            UpdateWingetStatusText();
                            _wingetUpgrades = new ObservableCollection<WingetUpgradeItem>(upgrades);
                            if (_icWingetUpgradesList != null) _icWingetUpgradesList.ItemsSource = _wingetUpgrades;
                            if (_borderUpgradesList != null) _borderUpgradesList.Visibility = Visibility.Visible;
                            if (_gridUpgradeActions != null) _gridUpgradeActions.Visibility = Visibility.Visible;
                        }
                    }));
                }
            });
        }

        private void BtnCheckWingetUpgrades_Click(object sender, RoutedEventArgs e)
        {
            CheckWingetUpgradesAsync();
        }

        private void BtnSelectAllUpgrades_Click(object sender, RoutedEventArgs e)
        {
            if (_wingetUpgrades == null) return;
            foreach (var item in _wingetUpgrades) item.Selected = true;
        }

        private void BtnDeselectAllUpgrades_Click(object sender, RoutedEventArgs e)
        {
            if (_wingetUpgrades == null) return;
            foreach (var item in _wingetUpgrades) item.Selected = false;
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
                MessageBox.Show(T("Msg_NoAppsSelected", "No application was selected for update."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_btnUpgradeSelected != null) _btnUpgradeSelected.IsEnabled = false;
            if (_btnUpgradeAll != null) _btnUpgradeAll.IsEnabled = false;
            if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Visible;
            if (_pnlWingetLogsAction != null) _pnlWingetLogsAction.Visibility = Visibility.Visible;
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

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_btnUpgradeSelected != null) _btnUpgradeSelected.IsEnabled = true;
                        if (_btnUpgradeAll != null) _btnUpgradeAll.IsEnabled = true;
                        if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Collapsed;
                        if (_pnlWingetLogsAction != null) _pnlWingetLogsAction.Visibility = Visibility.Collapsed;
                        MessageBox.Show(string.Format(T("Msg_UpgradeCompleted", "Application update completed!\n{0} of {1} successfully updated."), successes, selected.Count),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                        CheckWingetUpgradesAsync();
                    }));
                }
            });
        }

        private void BtnUpgradeAll_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(T("Msg_UpgradeAllConfirm", "Do you want to update all system programs now via 'winget upgrade --all'?"),
                "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            if (_btnUpgradeSelected != null) _btnUpgradeSelected.IsEnabled = false;
            if (_btnUpgradeAll != null) _btnUpgradeAll.IsEnabled = false;
            if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Visible;
            if (_pnlWingetLogsAction != null) _pnlWingetLogsAction.Visibility = Visibility.Visible;
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

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_btnUpgradeSelected != null) _btnUpgradeSelected.IsEnabled = true;
                        if (_btnUpgradeAll != null) _btnUpgradeAll.IsEnabled = true;
                        if (_pbWingetScan != null) _pbWingetScan.Visibility = Visibility.Collapsed;
                        if (_pnlWingetLogsAction != null) _pnlWingetLogsAction.Visibility = Visibility.Collapsed;
                        MessageBox.Show(T("Msg_UpgradeAllFinished", "Bulk update command finished! Check details in Logs tab."),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);

                        CheckWingetUpgradesAsync();
                    }));
                }
            });
        }

        // =========================================================
        // LOCALIZATION REFRESH
        // =========================================================
        public void OnLanguageChanged(string langCode)
        {
            if (_txtPostInstallTitle != null) _txtPostInstallTitle.Text = T("PostInstall_Title", "App Manager");
            if (_txtPostInstallSubtitle != null) _txtPostInstallSubtitle.Text = T("PostInstall_Subtitle", "Automated installation of essential software and update of installed applications");
            if (_txtWingetCardTitle != null) _txtWingetCardTitle.Text = T("Upgrade_Card_Title", "🔄 Application Updates");
            UpdateWingetStatusText();
            if (_btnCheckWingetUpgrades != null) _btnCheckWingetUpgrades.Content = T("Btn_CheckUpdates", "🔄 Check for Updates");
            if (_btnViewLogsFromUpgrade != null)
            {
                _btnViewLogsFromUpgrade.Content = T("Btn_ViewLogs", "⬛ View Logs");
                _btnViewLogsFromUpgrade.ToolTip = T("Tooltip_ViewLogs", "Monitor execution log in real time on the Logs screen");
            }
            if (_btnSelectAllUpgrades != null) _btnSelectAllUpgrades.Content = T("Btn_SelectAll", "Select All");
            if (_btnDeselectAllUpgrades != null) _btnDeselectAllUpgrades.Content = T("Btn_DeselectAll", "Deselect All");
            if (_btnRecheckWingetUpgrades != null) _btnRecheckWingetUpgrades.Content = T("Btn_Recheck", "🔄 Re-check");
            if (_btnUpgradeAll != null) _btnUpgradeAll.Content = T("Btn_UpgradeAll", "🚀 Update All");
            if (_btnUpgradeSelected != null) _btnUpgradeSelected.Content = T("Btn_UpgradeSelected", "🔄 Update Selected");

            if (_txtCustomizeHeaderTitle != null) _txtCustomizeHeaderTitle.Text = T("Custom_Card_Title", "⚙️ Customize automated application installation");
            if (_txtCustomizeHeaderSub != null) _txtCustomizeHeaderSub.Text = T("Custom_Card_Subtitle", "Add new programs, set default automation, and choose between Local installer or Winget.");
            if (_btnCloseCustomize != null) _btnCloseCustomize.Content = T("Btn_Close", "✕ Close");
            if (_txtNewAppNameLabel != null) _txtNewAppNameLabel.Text = T("Custom_Label_Name", "Program Name:");
            if (_txtNewAppTypeLabel != null) _txtNewAppTypeLabel.Text = T("Custom_Label_Type", "Installation Type:");
            if (_cmbItemLocal != null) _cmbItemLocal.Content = T("Custom_Type_Local", "📁 Local (Installers)");
            if (_cmbItemWinget != null) _cmbItemWinget.Content = T("Custom_Type_Winget", "🌐 Winget (Repository)");
            if (_txtNewAppLocalLabel != null) _txtNewAppLocalLabel.Text = T("Custom_Label_LocalFile", "Executable in Installers folder:");
            if (_btnSelectLocalInstaller != null)
            {
                _btnSelectLocalInstaller.Content = T("Btn_SelectLocal", "📁 Browse...");
                _btnSelectLocalInstaller.ToolTip = T("Tooltip_SelectLocalInstaller", "Select executable file from Installers folder");
            }
            if (_txtNewAppLocalFile != null) _txtNewAppLocalFile.ToolTip = T("Tooltip_NewAppLocalFile", "Name of the executable in Installers folder (e.g. OfficeSetup.exe)");
            if (_txtNewAppWingetLabel != null) _txtNewAppWingetLabel.Text = T("Custom_Label_WingetId", "Winget App ID (e.g. Google.Chrome):");
            if (_txtNewAppWingetId != null) _txtNewAppWingetId.ToolTip = T("Tooltip_NewAppWingetId", "Package identifier in Winget repository");
            if (_btnAddCustomApp != null)
            {
                _btnAddCustomApp.Content = (_editingAppItem != null) ? T("Btn_Update", "💾 Update") : T("Btn_Add", "➕ Add");
            }
            if (_btnCancelEditApp != null) _btnCancelEditApp.ToolTip = T("Tooltip_CancelEdit", "Cancel edit");
            if (_chkNewAppPreSelected != null) _chkNewAppPreSelected.Content = T("Custom_Chk_AutomateDefault", "Automate by default");
            if (_txtNewAppSilentLabel != null) _txtNewAppSilentLabel.Text = T("Custom_Label_SilentArgs", "Silent arguments (optional):");
            if (_txtCurrentListTitle != null) _txtCurrentListTitle.Text = T("Custom_CurrentList_Title", "Programs in current list:");
            if (_btnSaveCustomApps != null) _btnSaveCustomApps.Content = T("Btn_SaveCustomApps", "💾 Save Changes");
            if (_btnResetDefaultApps != null) _btnResetDefaultApps.Content = T("Btn_ResetDefaultApps", "🔄 Reset to Defaults");
            if (_btnOpenPostInstallJson != null) _btnOpenPostInstallJson.Content = T("Btn_OpenPostInstallJson", "📂 Open autoinstall.json");

            if (_customEditorApps != null)
            {
                foreach (var item in _customEditorApps)
                {
                    item.RefreshLocalization();
                }
                if (_icCustomAppsEditorList != null && _icCustomAppsEditorList.Items != null)
                {
                    _icCustomAppsEditorList.Items.Refresh();
                }
            }

            if (_txtAppsInstallCardTitle != null) _txtAppsInstallCardTitle.Text = T("Install_Card_Title", "📦 Automated Application Installation");
            if (_txtAppsInstallCardSub != null) _txtAppsInstallCardSub.Text = T("Install_Card_Subtitle", "Select the programs you want to batch install automatically on this computer:");
            if (_btnSelectEssentials != null) _btnSelectEssentials.Content = T("Btn_SelectEssentials", "Select Essentials");
            if (_btnDeselectAll != null) _btnDeselectAll.Content = T("Btn_DeselectAll", "Deselect All");
            if (_btnToggleCustomizePostInstall != null) _btnToggleCustomizePostInstall.Content = T("Btn_CustomizeAutomated", "⚙️ Customize Automated Installation");
            if (_btnOpenPostInstallFolder != null) _btnOpenPostInstallFolder.Content = T("Btn_OpenPostInstallFolder", "📂 Installers Folder");
            if (_btnRunInstallSelected != null) _btnRunInstallSelected.Content = T("Btn_RunInstallSelected", "🚀 Install Selected");
        }
    }
}
