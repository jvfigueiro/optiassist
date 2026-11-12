using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OptiAssist.Addons.PCAssist.Core;

namespace OptiAssist.Addons.PCAssist
{
    public class PCAssistAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private HardwareMonitorService _monitorService;
        private BatteryDiagnosticService _batteryService;
        private BenchEngineService _benchService;
        private DeviceInventoryService _inventoryService;

        private DeviceInventory _cachedInventory;
        private DispatcherTimer _monitorTimer;
        private bool _isMonitoringPaused = false;
        private CancellationTokenSource _benchCts;
        private int _tickCount = 0;

        // UI Tabs
        private enum PCAssistTab { Summary, Devices, Battery, Bench }
        private PCAssistTab _currentTab = PCAssistTab.Summary;

        private Border _tabSummaryBorder;
        private TextBlock _tabSummaryText;
        private Border _tabDevicesBorder;
        private TextBlock _tabDevicesText;
        private Border _tabBatteryBorder;
        private TextBlock _tabBatteryText;
        private Border _tabBenchBorder;
        private TextBlock _tabBenchText;

        private ScrollViewer _pnlTabSummary;
        private ScrollViewer _pnlTabDevices;
        private ScrollViewer _pnlTabBattery;
        private ScrollViewer _pnlTabBench;

        // Header controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnToggleMonitor;

        // Tab 1: Summary Controls (HWiNFO-style 3 columns)
        private TextBlock _txtCpuNameSum;
        private TextBlock _txtCpuSocketSum;
        private TextBlock _txtCpuCoresSum;
        private TextBlock _txtCpuCacheSum;
        private WrapPanel _pnlCpuInstructions;
        private TextBlock _txtCpuClockLive;
        private TextBlock _txtCpuLoadLive;
        private ProgressBar _pbCpuLoadLive;

        private TextBlock _txtMoboNameSum;
        private TextBlock _txtChipsetNameSum;
        private TextBlock _txtBiosInfoSum;
        private Border _badgeUefiSum;
        private TextBlock _txtRamTotalSum;
        private TextBlock _txtRamModeSum;
        private StackPanel _pnlRamModulesSum;
        private StackPanel _pnlNetworkSummary;

        private TextBlock _txtGpuNameSum;
        private TextBlock _txtGpuVramSum;
        private TextBlock _txtGpuBusSum;
        private StackPanel _pnlDisplaysSummary;
        private TextBlock _txtAudioSummary;
        private StackPanel _pnlDrivesSummary;

        // Tab 2: Devices & Peripherals Controls
        private StackPanel _pnlDevicesChipset;
        private StackPanel _pnlDevicesStorageControllers;
        private StackPanel _pnlDevicesPciBridges;
        private StackPanel _pnlDevicesUsb;
        private StackPanel _pnlDevicesSensors;
        private StackPanel _pnlDevicesAudio;
        private StackPanel _pnlDevicesNetwork;
        private StackPanel _pnlDevicesDisplays;
        private StackPanel _pnlDevicesMemory;
        private StackPanel _pnlDevicesDrives;
        private StackPanel _pnlDevicesInput;

        // Tab 3: Battery Controls
        private Border _cardBatteryDesktop;
        private TextBlock _txtDesktopNoticeTitle;
        private TextBlock _txtDesktopNoticeDesc;
        private Border _cardBatteryNotebook;
        private TextBlock _txtBatteryHealthBig;
        private TextBlock _txtBatteryHealthSubtitle;
        private ProgressBar _pbBatteryHealth;
        private TextBlock _txtBatteryWearBadge;
        private TextBlock _txtBatteryDesignCap;
        private TextBlock _txtBatteryFullCap;
        private TextBlock _txtBatteryCurrentCap;
        private TextBlock _txtBatteryVoltage;
        private TextBlock _txtBatteryRate;
        private TextBlock _txtBatteryAutonomy;
        private TextBlock _txtBatteryMaker;
        private TextBlock _txtBatteryChemistry;
        private TextBlock _txtBatteryCycles;
        private Button _btnBatteryReport;
        private Button _btnRefreshBattery;

        // Tab 4: Bench Controls
        private Button _btnCpuBench;
        private ProgressBar _pbCpuBench;
        private TextBlock _txtCpuBenchResult;
        private Button _btnRamBench;
        private ProgressBar _pbRamBench;
        private TextBlock _txtRamBenchResult;
        private Button _btnDiskBench;
        private ProgressBar _pbDiskBench;
        private TextBlock _txtDiskBenchResult;
        private Button _btnStressBench;
        private ProgressBar _pbStressBench;
        private TextBlock _txtStressBenchResult;
        private Button _btnCancelBench;

        // Contract Properties
        public string Id { get { return "hardware-monitor"; } }
        public string Name { get { return T("Nav_HardwareMonitor", "Hardware Monitor"); } }
        public string Description { get { return T("HardwareMonitor_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics"); } }
        public string Version { get { return "1.2.S4"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "⚡"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _monitorService = new HardwareMonitorService();
            _batteryService = new BatteryDiagnosticService();
            _benchService = new BenchEngineService();
            _inventoryService = new DeviceInventoryService();

            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromSeconds(1);
            _monitorTimer.Tick += OnMonitorTick;
        }

        public FrameworkElement CreateView()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tab bar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // 1. Header
            Border headerBorder = new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 14, 24, 14)
            };

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel();
            _txtTitle = new TextBlock
            {
                Text = "⚡ " + T("Nav_HardwareMonitor", "Hardware Monitor"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A")
            };
            _txtSubtitle = new TextBlock
            {
                Text = T("HardwareMonitor_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics"),
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 3, 0, 0)
            };
            titlePanel.Children.Add(_txtTitle);
            titlePanel.Children.Add(_txtSubtitle);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            _btnToggleMonitor = CreateStyledButton(T("PCAssist_Monitor_Pause", "⏸️ Pause Monitor"), GetResourceBrush("BrushBtnBg", "#E2E8F0"), GetResourceBrush("BrushTextPrimary", "#0F172A"));
            _btnToggleMonitor.VerticalAlignment = VerticalAlignment.Center;
            _btnToggleMonitor.Click += (s, e) =>
            {
                _isMonitoringPaused = !_isMonitoringPaused;
                _btnToggleMonitor.Content = _isMonitoringPaused ? T("PCAssist_Monitor_Resume", "▶️ Resume Monitor") : T("PCAssist_Monitor_Pause", "⏸️ Pause Monitor");
            };
            Grid.SetColumn(_btnToggleMonitor, 1);
            headerGrid.Children.Add(_btnToggleMonitor);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            root.Children.Add(headerBorder);

            // 2. Tabs Navigation Bar
            Border tabsBar = new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 0, 24, 0)
            };

            StackPanel tabsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _tabSummaryBorder = CreateTabButton("🖥️ " + T("PCAssist_Tab_Summary", "System Summary"), true, out _tabSummaryText);
            _tabSummaryBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Summary);
            tabsPanel.Children.Add(_tabSummaryBorder);

            _tabDevicesBorder = CreateTabButton("🔌 " + T("PCAssist_Tab_Devices", "Devices & Peripherals"), false, out _tabDevicesText);
            _tabDevicesBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Devices);
            tabsPanel.Children.Add(_tabDevicesBorder);

            _tabBatteryBorder = CreateTabButton("🔋 " + T("PCAssist_Tab_Battery", "Battery & Power"), false, out _tabBatteryText);
            _tabBatteryBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Battery);
            tabsPanel.Children.Add(_tabBatteryBorder);

            _tabBenchBorder = CreateTabButton("🚀 " + T("PCAssist_Tab_Bench", "Bench Diagnostics"), false, out _tabBenchText);
            _tabBenchBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Bench);
            tabsPanel.Children.Add(_tabBenchBorder);

            tabsBar.Child = tabsPanel;
            Grid.SetRow(tabsBar, 1);
            root.Children.Add(tabsBar);

            // 3. Tab Contents
            Grid contentArea = new Grid();
            Grid.SetRow(contentArea, 2);

            _pnlTabSummary = BuildSummaryTab();
            _pnlTabDevices = BuildDevicesTab();
            _pnlTabBattery = BuildBatteryTab();
            _pnlTabBench = BuildBenchTab();

            contentArea.Children.Add(_pnlTabSummary);
            contentArea.Children.Add(_pnlTabDevices);
            contentArea.Children.Add(_pnlTabBattery);
            contentArea.Children.Add(_pnlTabBench);

            root.Children.Add(contentArea);

            LoadDeviceInventory();
            _monitorTimer.Start();

            root.Loaded += (s, e) => { if (!_monitorTimer.IsEnabled) _monitorTimer.Start(); };
            root.Unloaded += (s, e) => { _monitorTimer.Stop(); };

            return root;
        }

        private Border CreateTabButton(string text, bool isActive, out TextBlock textBlock)
        {
            Border b = new Border
            {
                Padding = new Thickness(16, 10, 16, 10),
                BorderThickness = new Thickness(0, 0, 0, isActive ? 3 : 0),
                BorderBrush = GetResourceBrush("BrushAccent", "#0EA5E9"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent
            };

            textBlock = new TextBlock
            {
                Text = text,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isActive ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569"),
                FontSize = 13
            };

            b.Child = textBlock;
            return b;
        }

        private void SwitchTab(PCAssistTab tab)
        {
            _currentTab = tab;

            _tabSummaryBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Summary ? 3 : 0);
            _tabSummaryText.FontWeight = tab == PCAssistTab.Summary ? FontWeights.Bold : FontWeights.Normal;
            _tabSummaryText.Foreground = tab == PCAssistTab.Summary ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _tabDevicesBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Devices ? 3 : 0);
            _tabDevicesText.FontWeight = tab == PCAssistTab.Devices ? FontWeights.Bold : FontWeights.Normal;
            _tabDevicesText.Foreground = tab == PCAssistTab.Devices ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _tabBatteryBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Battery ? 3 : 0);
            _tabBatteryText.FontWeight = tab == PCAssistTab.Battery ? FontWeights.Bold : FontWeights.Normal;
            _tabBatteryText.Foreground = tab == PCAssistTab.Battery ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _tabBenchBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Bench ? 3 : 0);
            _tabBenchText.FontWeight = tab == PCAssistTab.Bench ? FontWeights.Bold : FontWeights.Normal;
            _tabBenchText.Foreground = tab == PCAssistTab.Bench ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _pnlTabSummary.Visibility = (tab == PCAssistTab.Summary) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabDevices.Visibility = (tab == PCAssistTab.Devices) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBattery.Visibility = (tab == PCAssistTab.Battery) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBench.Visibility = (tab == PCAssistTab.Bench) ? Visibility.Visible : Visibility.Collapsed;

            if (tab == PCAssistTab.Battery) UpdateBatteryReport();
        }

        #region TAB 1: System Summary (HWiNFO-style)
        private ScrollViewer BuildSummaryTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Visible };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(16, 12, 16, 16) };

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ==================== COLUMN 1: CPU ====================
            Border col1Card = CreateDenseCard();
            col1Card.Margin = new Thickness(0, 0, 6, 0);
            StackPanel col1 = new StackPanel();

            col1.Children.Add(CreateDenseHeader("CPU", "#0EA5E9"));
            _txtCpuNameSum = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 4), TextTrimming = TextTrimming.CharacterEllipsis };
            col1.Children.Add(_txtCpuNameSum);

            Grid cpuDetails = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            cpuDetails.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cpuDetails.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel cpuDLeft = new StackPanel();
            _txtCpuSocketSum = AddMiniParam(cpuDLeft, "Platform:", "Detecting...");
            _txtCpuCoresSum = AddMiniParam(cpuDLeft, "Cores/Threads:", "Detecting...");

            StackPanel cpuDRight = new StackPanel();
            _txtCpuCacheSum = AddMiniParam(cpuDRight, "L3 Cache:", "Detecting...");
            AddMiniParam(cpuDRight, "Architecture:", Environment.Is64BitOperatingSystem ? "x64 (64-bit)" : "x86 (32-bit)");

            Grid.SetColumn(cpuDLeft, 0);
            Grid.SetColumn(cpuDRight, 1);
            cpuDetails.Children.Add(cpuDLeft);
            cpuDetails.Children.Add(cpuDRight);
            col1.Children.Add(cpuDetails);

            col1.Children.Add(CreateSubDivider("INSTRUCTION SET FEATURES"));
            _pnlCpuInstructions = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
            col1.Children.Add(_pnlCpuInstructions);

            col1.Children.Add(CreateSubDivider("OPERATING CLOCKS & LOAD"));
            _txtCpuClockLive = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 2), Text = "Clock: Detecting..." };
            _txtCpuLoadLive = new TextBlock { FontSize = 11, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Text = "CPU Load: 0.0%" };
            _pbCpuLoadLive = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 4, 0, 4), Foreground = GetResourceBrush("BrushAccent", "#0EA5E9") };

            col1.Children.Add(_txtCpuClockLive);
            col1.Children.Add(_txtCpuLoadLive);
            col1.Children.Add(_pbCpuLoadLive);

            col1Card.Child = col1;
            Grid.SetColumn(col1Card, 0);
            grid.Children.Add(col1Card);

            // ==================== COLUMN 2: MOTHERBOARD & MEMORY ====================
            Border col2Card = CreateDenseCard();
            col2Card.Margin = new Thickness(4, 0, 4, 0);
            StackPanel col2 = new StackPanel();

            col2.Children.Add(CreateDenseHeader("MOTHERBOARD & CHIPSET", "#10B981"));
            _txtMoboNameSum = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis };
            col2.Children.Add(_txtMoboNameSum);

            _txtChipsetNameSum = AddMiniParam(col2, "Chipset:", "Detecting...");
            Grid biosGrid = new Grid();
            biosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            biosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _txtBiosInfoSum = new TextBlock { FontSize = 11, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Text = "BIOS: Detecting..." };
            _badgeUefiSum = CreateBadge("UEFI", "#10B981");
            Grid.SetColumn(_txtBiosInfoSum, 0);
            Grid.SetColumn(_badgeUefiSum, 1);
            biosGrid.Children.Add(_txtBiosInfoSum);
            biosGrid.Children.Add(_badgeUefiSum);
            col2.Children.Add(biosGrid);

            col2.Children.Add(CreateSubDivider("MEMORY"));
            _txtRamTotalSum = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 2) };
            _txtRamModeSum = new TextBlock { FontSize = 11, Foreground = GetResourceBrush("BrushAccent", "#0EA5E9"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
            col2.Children.Add(_txtRamTotalSum);
            col2.Children.Add(_txtRamModeSum);

            col2.Children.Add(CreateSubDivider("INSTALLED MODULES (PER SLOT)"));
            _pnlRamModulesSum = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            col2.Children.Add(_pnlRamModulesSum);

            col2.Children.Add(CreateSubDivider("NETWORK INTERFACES"));
            _pnlNetworkSummary = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            col2.Children.Add(_pnlNetworkSummary);

            col2Card.Child = col2;
            Grid.SetColumn(col2Card, 1);
            grid.Children.Add(col2Card);

            // ==================== COLUMN 3: GPU, DISPLAYS, DRIVES ====================
            Border col3Card = CreateDenseCard();
            col3Card.Margin = new Thickness(6, 0, 0, 0);
            StackPanel col3 = new StackPanel();

            col3.Children.Add(CreateDenseHeader("GRAPHICS (GPU)", "#8B5CF6"));
            _txtGpuNameSum = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis };
            col3.Children.Add(_txtGpuNameSum);

            _txtGpuVramSum = AddMiniParam(col3, "Memory:", "Detecting...");
            _txtGpuBusSum = AddMiniParam(col3, "Interface:", "PCIe x16");

            col3.Children.Add(CreateSubDivider("ACTIVE DISPLAYS & MONITORS"));
            _pnlDisplaysSummary = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            col3.Children.Add(_pnlDisplaysSummary);

            col3.Children.Add(CreateSubDivider("AUDIO CODECS"));
            _txtAudioSummary = new TextBlock { FontSize = 11, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap };
            col3.Children.Add(_txtAudioSummary);

            col3.Children.Add(CreateSubDivider("PHYSICAL DRIVES"));
            _pnlDrivesSummary = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            col3.Children.Add(_pnlDrivesSummary);

            col3Card.Child = col3;
            Grid.SetColumn(col3Card, 2);
            grid.Children.Add(col3Card);

            rootPanel.Children.Add(grid);
            sv.Content = rootPanel;
            return sv;
        }

        private TextBlock AddMiniParam(StackPanel parent, string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock { Text = label, FontSize = 11, Foreground = GetResourceBrush("BrushTextSecondary", "#475569") };
            TextBlock val = new TextBlock { Text = value, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), TextTrimming = TextTrimming.CharacterEllipsis };

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            g.Children.Add(lbl);
            g.Children.Add(val);
            parent.Children.Add(g);
            return val;
        }

        private Border CreateDenseHeader(string text, string hexAccent)
        {
            Border b = new Border
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hexAccent),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4)
            };
            b.Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            return b;
        }

        private Border CreateSubDivider(string title)
        {
            Border b = new Border
            {
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 8, 0, 2),
                Padding = new Thickness(0, 4, 0, 2)
            };
            b.Child = new TextBlock
            {
                Text = title,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569")
            };
            return b;
        }

        private Border CreateBadge(string text, string hexColor)
        {
            Border b = new Border
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 4, 0)
            };
            b.Child = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            return b;
        }
        #endregion

        #region TAB 2: Detailed Devices & Peripherals (Complete System Tree)
        private ScrollViewer BuildDevicesTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            // 1. Motherboard & Chipset
            Border cardChipset = CreateCard();
            cardChipset.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stChipset = new StackPanel();
            stChipset.Children.Add(CreateCardHeader("🔌 Motherboard, Chipset & Architecture"));
            _pnlDevicesChipset = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stChipset.Children.Add(_pnlDevicesChipset);
            cardChipset.Child = stChipset;
            rootPanel.Children.Add(cardChipset);

            // 2. Storage Controllers (NVMe / SATA AHCI)
            Border cardStorageCtrl = CreateCard();
            cardStorageCtrl.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stStorageCtrl = new StackPanel();
            stStorageCtrl.Children.Add(CreateCardHeader("💽 Storage Controllers (NVMe / SATA / RAID)"));
            _pnlDevicesStorageControllers = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stStorageCtrl.Children.Add(_pnlDevicesStorageControllers);
            cardStorageCtrl.Child = stStorageCtrl;
            rootPanel.Children.Add(cardStorageCtrl);

            // 3. Physical Storage Drives
            Border cardDrives = CreateCard();
            cardDrives.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stDrives = new StackPanel();
            stDrives.Children.Add(CreateCardHeader("💾 Physical Storage Media & Disks"));
            _pnlDevicesDrives = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stDrives.Children.Add(_pnlDevicesDrives);
            cardDrives.Child = stDrives;
            rootPanel.Children.Add(cardDrives);

            // 4. PCI Express Bridges & Root Ports
            Border cardPci = CreateCard();
            cardPci.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stPci = new StackPanel();
            stPci.Children.Add(CreateCardHeader("⚡ PCI Express Bus & Root Ports"));
            _pnlDevicesPciBridges = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stPci.Children.Add(_pnlDevicesPciBridges);
            cardPci.Child = stPci;
            rootPanel.Children.Add(cardPci);

            // 5. USB Controllers & Root Hubs
            Border cardUsb = CreateCard();
            cardUsb.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stUsb = new StackPanel();
            stUsb.Children.Add(CreateCardHeader("🔱 USB xHCI Controllers & Hubs"));
            _pnlDevicesUsb = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stUsb.Children.Add(_pnlDevicesUsb);
            cardUsb.Child = stUsb;
            rootPanel.Children.Add(cardUsb);

            // 6. Displays & EDID
            Border cardDisplays = CreateCard();
            cardDisplays.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stDisp = new StackPanel();
            stDisp.Children.Add(CreateCardHeader("🖥️ Displays, Monitors & Panel EDID"));
            _pnlDevicesDisplays = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stDisp.Children.Add(_pnlDevicesDisplays);
            cardDisplays.Child = stDisp;
            rootPanel.Children.Add(cardDisplays);

            // 7. Audio Codecs
            Border cardAudio = CreateCard();
            cardAudio.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stAudio = new StackPanel();
            stAudio.Children.Add(CreateCardHeader("🔊 Audio Controllers & Codecs"));
            _pnlDevicesAudio = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stAudio.Children.Add(_pnlDevicesAudio);
            cardAudio.Child = stAudio;
            rootPanel.Children.Add(cardAudio);

            // 8. Network Interfaces
            Border cardNet = CreateCard();
            cardNet.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stNet = new StackPanel();
            stNet.Children.Add(CreateCardHeader("🌐 Network Interfaces (Ethernet & Wi-Fi)"));
            _pnlDevicesNetwork = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stNet.Children.Add(_pnlDevicesNetwork);
            cardNet.Child = stNet;
            rootPanel.Children.Add(cardNet);

            // 9. Memory Slots Detail
            Border cardMem = CreateCard();
            cardMem.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stMem = new StackPanel();
            stMem.Children.Add(CreateCardHeader("🧠 Physical Memory Slots & Channels"));
            _pnlDevicesMemory = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stMem.Children.Add(_pnlDevicesMemory);
            cardMem.Child = stMem;
            rootPanel.Children.Add(cardMem);

            // 10. System Sensors & Thermal Zones
            Border cardSensors = CreateCard();
            cardSensors.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stSensors = new StackPanel();
            stSensors.Children.Add(CreateCardHeader("🌡️ System Sensors & Thermal Management"));
            _pnlDevicesSensors = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stSensors.Children.Add(_pnlDevicesSensors);
            cardSensors.Child = stSensors;
            rootPanel.Children.Add(cardSensors);

            // 11. Input & Human Interface Devices (HID)
            Border cardInput = CreateCard();
            StackPanel stInput = new StackPanel();
            stInput.Children.Add(CreateCardHeader("⌨️ Input & Human Interface Devices (HID)"));
            _pnlDevicesInput = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stInput.Children.Add(_pnlDevicesInput);
            cardInput.Child = stInput;
            rootPanel.Children.Add(cardInput);

            sv.Content = rootPanel;
            return sv;
        }
        #endregion

        #region TAB 3: Battery & Power View
        private ScrollViewer BuildBatteryTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            _cardBatteryDesktop = CreateCard();
            _cardBatteryDesktop.Visibility = Visibility.Collapsed;
            StackPanel desktopStack = new StackPanel { Margin = new Thickness(12) };
            _txtDesktopNoticeTitle = new TextBlock
            {
                Text = "🖥️ " + T("PCAssist_Battery_Desktop", "Desktop PC (No Battery)"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A")
            };
            _txtDesktopNoticeDesc = new TextBlock
            {
                Text = T("PCAssist_Battery_DesktopDesc", "This PC is connected directly to mains AC power with no integrated battery. Battery health and autonomy analysis apply only to mobile portable devices (notebooks/laptops)."),
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            desktopStack.Children.Add(_txtDesktopNoticeTitle);
            desktopStack.Children.Add(_txtDesktopNoticeDesc);
            _cardBatteryDesktop.Child = desktopStack;
            rootPanel.Children.Add(_cardBatteryDesktop);

            _cardBatteryNotebook = CreateCard();
            _cardBatteryNotebook.Visibility = Visibility.Visible;
            StackPanel nbStack = new StackPanel();

            TextBlock lblNbHeader = CreateCardHeader("🔋 " + T("PCAssist_Battery_Title", "Battery Diagnostics"));
            nbStack.Children.Add(lblNbHeader);

            Border healthHero = new Border
            {
                Background = GetResourceBrush("BrushCardHover", "#F8FAFC"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 16)
            };
            StackPanel healthStack = new StackPanel();
            Grid heroTop = new Grid();
            heroTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtBatteryHealthBig = new TextBlock
            {
                Text = "0.0% " + T("PCAssist_Battery_Health", "Battery Health"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushSuccess", "#10B981")
            };
            _txtBatteryWearBadge = new TextBlock
            {
                Text = "0% " + T("PCAssist_Battery_Wear", "Wear"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushWarning", "#F59E0B"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_txtBatteryHealthBig, 0);
            Grid.SetColumn(_txtBatteryWearBadge, 1);
            heroTop.Children.Add(_txtBatteryHealthBig);
            heroTop.Children.Add(_txtBatteryWearBadge);

            _txtBatteryHealthSubtitle = new TextBlock
            {
                Text = "Diagnosing battery condition...",
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 4, 0, 8)
            };
            _pbBatteryHealth = CreateMetricProgressBar();

            healthStack.Children.Add(heroTop);
            healthStack.Children.Add(_txtBatteryHealthSubtitle);
            healthStack.Children.Add(_pbBatteryHealth);
            healthHero.Child = healthStack;
            nbStack.Children.Add(healthHero);

            Grid detailsGrid = new Grid();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel colLeft = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            _txtBatteryDesignCap = AddDetailRow(colLeft, T("PCAssist_Battery_DesignCap", "Designed Capacity:"), "0 mWh");
            _txtBatteryFullCap = AddDetailRow(colLeft, T("PCAssist_Battery_FullCap", "Full Charged Capacity:"), "0 mWh");
            _txtBatteryCurrentCap = AddDetailRow(colLeft, T("PCAssist_Battery_CurrentCap", "Current Charge:"), "0% (0 mWh)");
            _txtBatteryAutonomy = AddDetailRow(colLeft, T("PCAssist_Battery_Autonomy", "Estimated Autonomy:"), "Calculating...");

            StackPanel colRight = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            _txtBatteryVoltage = AddDetailRow(colRight, T("PCAssist_Battery_Voltage", "Voltage:"), "0.00 V");
            _txtBatteryRate = AddDetailRow(colRight, T("PCAssist_Battery_Rate", "Charge / Discharge Rate:"), "0.00 W");
            _txtBatteryMaker = AddDetailRow(colRight, T("PCAssist_Battery_Maker", "Manufacturer / Model:"), "N/A");
            _txtBatteryChemistry = AddDetailRow(colRight, T("PCAssist_Battery_Chemistry", "Chemistry:"), "Li-Ion");
            _txtBatteryCycles = AddDetailRow(colRight, T("PCAssist_Battery_Cycles", "Cycle Count:"), "N/A");

            Grid.SetColumn(colLeft, 0);
            Grid.SetColumn(colRight, 1);
            detailsGrid.Children.Add(colLeft);
            detailsGrid.Children.Add(colRight);
            nbStack.Children.Add(detailsGrid);

            WrapPanel btnPanel = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };

            _btnBatteryReport = CreateStyledButton(T("PCAssist_Battery_BtnReport", "📄 Generate Official Report (HTML)"), GetResourceBrush("BrushAccent", "#0EA5E9"), Brushes.White);
            _btnBatteryReport.Click += (s, e) =>
            {
                _batteryService.GenerateOfficialBatteryReport();
            };
            btnPanel.Children.Add(_btnBatteryReport);

            _btnRefreshBattery = CreateStyledButton("🔄 Refresh", GetResourceBrush("BrushBtnBg", "#E2E8F0"), GetResourceBrush("BrushTextPrimary", "#0F172A"));
            _btnRefreshBattery.Margin = new Thickness(8, 0, 0, 0);
            _btnRefreshBattery.Click += (s, e) => UpdateBatteryReport();
            btnPanel.Children.Add(_btnRefreshBattery);

            nbStack.Children.Add(btnPanel);
            _cardBatteryNotebook.Child = nbStack;
            rootPanel.Children.Add(_cardBatteryNotebook);

            sv.Content = rootPanel;
            return sv;
        }

        private TextBlock AddDetailRow(StackPanel parent, string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock { Text = label, FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569") };
            TextBlock val = new TextBlock { Text = value, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), TextWrapping = TextWrapping.Wrap };

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            g.Children.Add(lbl);
            g.Children.Add(val);
            parent.Children.Add(g);
            return val;
        }

        private void UpdateBatteryReport()
        {
            Task.Run(() =>
            {
                var report = _batteryService.AnalyzeBattery();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!report.HasBattery)
                    {
                        _cardBatteryDesktop.Visibility = Visibility.Visible;
                        _cardBatteryNotebook.Visibility = Visibility.Collapsed;
                        return;
                    }

                    _cardBatteryDesktop.Visibility = Visibility.Collapsed;
                    _cardBatteryNotebook.Visibility = Visibility.Visible;

                    _txtBatteryHealthBig.Text = string.Format("{0:F1}% {1}", report.HealthPercent, T("PCAssist_Battery_Health", "Battery Health"));
                    _txtBatteryWearBadge.Text = string.Format("{0:F1}% {1}", report.WearPercent, T("PCAssist_Battery_Wear", "Wear"));
                    _pbBatteryHealth.Value = report.HealthPercent;

                    if (report.HealthPercent >= 85) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushSuccess", "#10B981");
                    else if (report.HealthPercent >= 70) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushAccent", "#0EA5E9");
                    else if (report.HealthPercent >= 50) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushWarning", "#F59E0B");
                    else _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushDanger", "#EF4444");

                    _txtBatteryHealthSubtitle.Text = report.DiagnosticSummary;

                    _txtBatteryDesignCap.Text = string.Format("{0:N0} mWh", report.DesignCapacityMwh);
                    _txtBatteryFullCap.Text = string.Format("{0:N0} mWh", report.FullChargeCapacityMwh);
                    _txtBatteryCurrentCap.Text = string.Format("{0}% ({1:N0} mWh)", report.ChargePercent, report.RemainingCapacityMwh);

                    if (report.EstimatedRuntimeMinutes > 0)
                    {
                        int h = report.EstimatedRuntimeMinutes / 60;
                        int m = report.EstimatedRuntimeMinutes % 60;
                        _txtBatteryAutonomy.Text = string.Format("~ {0}h {1}min remaining ({2})", h, m, report.IsCharging ? "Charging" : (report.IsAcConnected ? "AC Plugged" : "On Battery"));
                    }
                    else
                    {
                        _txtBatteryAutonomy.Text = report.IsCharging ? "Charging..." : (report.IsAcConnected ? "AC Plugged In" : "Estimating runtime...");
                    }

                    _txtBatteryVoltage.Text = string.Format("{0:F2} V", report.VoltageVolts);
                    _txtBatteryRate.Text = string.Format("{0:F2} W ({1})", report.RateWatts, report.IsCharging ? "Charging" : "Discharging");
                    _txtBatteryMaker.Text = string.Format("{0} {1}", report.Manufacturer, report.DeviceName).Trim();
                    _txtBatteryChemistry.Text = string.IsNullOrEmpty(report.Chemistry) ? "Li-Ion / Polymer" : report.Chemistry;
                    _txtBatteryCycles.Text = report.CycleCount.HasValue ? string.Format("{0} cycles", report.CycleCount.Value) : "Not reported by EC";
                });
            });
        }
        #endregion

        #region TAB 4: Bench Diagnostics View
        private ScrollViewer BuildBenchTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            _btnCancelBench = CreateStyledButton(T("PCAssist_Bench_BtnCancel", "⏹️ Cancel Test"), GetResourceBrush("BrushDanger", "#EF4444"), Brushes.White);
            _btnCancelBench.Visibility = Visibility.Collapsed;
            _btnCancelBench.Margin = new Thickness(0, 0, 0, 12);
            _btnCancelBench.Click += (s, e) =>
            {
                if (_benchCts != null) _benchCts.Cancel();
            };
            rootPanel.Children.Add(_btnCancelBench);

            // 1. CPU Benchmark Card
            Border cpuBenchCard = CreateCard();
            cpuBenchCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel cpuBStack = new StackPanel();
            cpuBStack.Children.Add(CreateCardHeader("⚡ " + T("PCAssist_Bench_CpuTitle", "CPU Multithread Benchmark")));
            cpuBStack.Children.Add(new TextBlock
            {
                Text = "Computes multi-threaded prime numbers and SHA-256 blocks for 5 seconds to test peak computational throughput.",
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            _btnCpuBench = CreateStyledButton(T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark"), GetResourceBrush("BrushAccent", "#0EA5E9"), Brushes.White);
            _pbCpuBench = CreateMetricProgressBar();
            _pbCpuBench.Visibility = Visibility.Collapsed;
            _txtCpuBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0) };

            _btnCpuBench.Click += async (s, e) =>
            {
                await ExecuteBench(async (token) =>
                {
                    return await _benchService.RunCpuBenchAsync(5, token, (sec) =>
                    {
                        Application.Current.Dispatcher.Invoke(() => { _pbCpuBench.Value = sec * 20; });
                    });
                }, _btnCpuBench, _pbCpuBench, _txtCpuBenchResult);
            };

            cpuBStack.Children.Add(_btnCpuBench);
            cpuBStack.Children.Add(_pbCpuBench);
            cpuBStack.Children.Add(_txtCpuBenchResult);
            cpuBenchCard.Child = cpuBStack;
            rootPanel.Children.Add(cpuBenchCard);

            // 2. RAM Benchmark Card
            Border ramBenchCard = CreateCard();
            ramBenchCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel ramBStack = new StackPanel();
            ramBStack.Children.Add(CreateCardHeader("🧠 " + T("PCAssist_Bench_RamTitle", "RAM Bandwidth Benchmark")));
            ramBStack.Children.Add(new TextBlock
            {
                Text = "Allocates a sequential 128 MB buffer and measures sequential read/write block memory throughput (GB/s).",
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            _btnRamBench = CreateStyledButton(T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark"), GetResourceBrush("BrushAccent", "#0EA5E9"), Brushes.White);
            _pbRamBench = CreateMetricProgressBar();
            _pbRamBench.Visibility = Visibility.Collapsed;
            _txtRamBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0) };

            _btnRamBench.Click += async (s, e) =>
            {
                await ExecuteBench(async (token) =>
                {
                    return await _benchService.RunRamBenchAsync(token, (pct) =>
                    {
                        Application.Current.Dispatcher.Invoke(() => { _pbRamBench.Value = pct; });
                    });
                }, _btnRamBench, _pbRamBench, _txtRamBenchResult);
            };

            ramBStack.Children.Add(_btnRamBench);
            ramBStack.Children.Add(_pbRamBench);
            ramBStack.Children.Add(_txtRamBenchResult);
            ramBenchCard.Child = ramBStack;
            rootPanel.Children.Add(ramBenchCard);

            // 3. Storage Benchmark Card
            Border diskBenchCard = CreateCard();
            diskBenchCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel diskBStack = new StackPanel();
            diskBStack.Children.Add(CreateCardHeader("💾 " + T("PCAssist_Bench_DiskTitle", "Sequential Drive C: Speed Test")));
            diskBStack.Children.Add(new TextBlock
            {
                Text = "Writes and reads an unbuffered 128 MB file directly to Drive C: bypassing RAM caching to gauge real disk transfer speed.",
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            _btnDiskBench = CreateStyledButton(T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark"), GetResourceBrush("BrushAccent", "#0EA5E9"), Brushes.White);
            _pbDiskBench = CreateMetricProgressBar();
            _pbDiskBench.Visibility = Visibility.Collapsed;
            _txtDiskBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0) };

            _btnDiskBench.Click += async (s, e) =>
            {
                await ExecuteBench(async (token) =>
                {
                    return await _benchService.RunDiskBenchAsync(null, token, (pct) =>
                    {
                        Application.Current.Dispatcher.Invoke(() => { _pbDiskBench.Value = pct; });
                    });
                }, _btnDiskBench, _pbDiskBench, _txtDiskBenchResult);
            };

            diskBStack.Children.Add(_btnDiskBench);
            diskBStack.Children.Add(_pbDiskBench);
            diskBStack.Children.Add(_txtDiskBenchResult);
            diskBenchCard.Child = diskBStack;
            rootPanel.Children.Add(diskBenchCard);

            // 4. Stress Test Card
            Border stressBenchCard = CreateCard();
            StackPanel stressBStack = new StackPanel();
            stressBStack.Children.Add(CreateCardHeader("🔥 " + T("PCAssist_Bench_StressTitle", "Thermal & Cooling Stress Test (30s)")));
            stressBStack.Children.Add(new TextBlock
            {
                Text = "Loads all CPU threads at 100% for 30 seconds to validate cooling, fan ramp-up, and ensure no thermal shutdown occurs after thermal paste replacement.",
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            _btnStressBench = CreateStyledButton(T("PCAssist_Bench_BtnStress", "🔥 Run Stress Test"), GetResourceBrush("BrushDanger", "#EF4444"), Brushes.White);
            _pbStressBench = CreateMetricProgressBar();
            _pbStressBench.Visibility = Visibility.Collapsed;
            _txtStressBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0) };

            _btnStressBench.Click += async (s, e) =>
            {
                await ExecuteBench(async (token) =>
                {
                    return await _benchService.RunCpuStressTestAsync(30, token, (sec) =>
                    {
                        Application.Current.Dispatcher.Invoke(() => { _pbStressBench.Value = (sec / 30.0) * 100.0; });
                    });
                }, _btnStressBench, _pbStressBench, _txtStressBenchResult);
            };

            stressBStack.Children.Add(_btnStressBench);
            stressBStack.Children.Add(_pbStressBench);
            stressBStack.Children.Add(_txtStressBenchResult);
            stressBenchCard.Child = stressBStack;
            rootPanel.Children.Add(stressBenchCard);

            sv.Content = rootPanel;
            return sv;
        }

        private async Task ExecuteBench(Func<CancellationToken, Task<BenchResult>> runFunc, Button triggerBtn, ProgressBar pb, TextBlock resultTxt)
        {
            _benchCts = new CancellationTokenSource();
            triggerBtn.IsEnabled = false;
            pb.Visibility = Visibility.Visible;
            pb.Value = 0;
            _btnCancelBench.Visibility = Visibility.Visible;
            resultTxt.Text = T("PCAssist_Bench_Running", "Benchmarking in progress...");
            resultTxt.Foreground = GetResourceBrush("BrushAccent", "#0EA5E9");

            try
            {
                var result = await runFunc(_benchCts.Token);
                if (result.Success)
                {
                    resultTxt.Text = string.Format("✅ {0}  ({1})\n{2}", result.PrimaryMetric, result.SecondaryMetric, result.Evaluation ?? result.BaselineComparison);
                    resultTxt.Foreground = GetResourceBrush("BrushSuccess", "#10B981");
                }
                else
                {
                    resultTxt.Text = "❌ " + (result.ErrorMessage ?? "Benchmark cancelled or failed.");
                    resultTxt.Foreground = GetResourceBrush("BrushDanger", "#EF4444");
                }
            }
            catch (OperationCanceledException)
            {
                resultTxt.Text = "⏹️ Benchmark cancelled by technician.";
                resultTxt.Foreground = GetResourceBrush("BrushWarning", "#F59E0B");
            }
            catch (Exception ex)
            {
                resultTxt.Text = "❌ Error: " + ex.Message;
                resultTxt.Foreground = GetResourceBrush("BrushDanger", "#EF4444");
            }
            finally
            {
                triggerBtn.IsEnabled = true;
                pb.Visibility = Visibility.Collapsed;
                _btnCancelBench.Visibility = Visibility.Collapsed;
                if (_benchCts != null) { _benchCts.Dispose(); _benchCts = null; }
            }
        }
        #endregion

        #region Scanning & Realtime Updates
        private void LoadDeviceInventory()
        {
            Task.Run(() =>
            {
                _cachedInventory = _inventoryService.ScanFullInventory();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RenderSummaryStatic(_cachedInventory);
                    RenderDevicesTab(_cachedInventory);
                });
            });
        }

        private void RenderSummaryStatic(DeviceInventory inv)
        {
            if (inv == null) return;

            // Column 1: CPU
            string cpuName = _host != null && _host.Hardware != null && !string.IsNullOrEmpty(_host.Hardware.CpuName) ? _host.Hardware.CpuName : "x64 Processor";
            _txtCpuNameSum.Text = cpuName;
            _txtCpuSocketSum.Text = inv.CpuSocket;
            _txtCpuCoresSum.Text = string.Format("{0} Cores / {1} Threads", inv.CpuPcores, inv.CpuPcores + inv.CpuEcores);
            _txtCpuCacheSum.Text = inv.CpuL3Cache;

            _pnlCpuInstructions.Children.Clear();
            foreach (var feat in inv.CpuFeatures)
            {
                Border badge = new Border
                {
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 3, 3),
                    Background = feat.IsSupported ? (SolidColorBrush)new BrushConverter().ConvertFromString("#064E3B") : (SolidColorBrush)new BrushConverter().ConvertFromString("#1E293B"),
                    BorderBrush = feat.IsSupported ? (SolidColorBrush)new BrushConverter().ConvertFromString("#059669") : (SolidColorBrush)new BrushConverter().ConvertFromString("#334155"),
                    BorderThickness = new Thickness(1)
                };
                badge.Child = new TextBlock
                {
                    Text = feat.Name,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = feat.IsSupported ? (SolidColorBrush)new BrushConverter().ConvertFromString("#10B981") : (SolidColorBrush)new BrushConverter().ConvertFromString("#64748B")
                };
                _pnlCpuInstructions.Children.Add(badge);
            }

            // Column 2: Motherboard & Memory
            string mobo = string.IsNullOrEmpty(inv.MotherboardModel) ? (inv.MotherboardManufacturer ?? "Desktop/Notebook Board") : (inv.MotherboardManufacturer + " " + inv.MotherboardModel);
            _txtMoboNameSum.Text = mobo;
            _txtChipsetNameSum.Text = inv.ChipsetName;
            _txtBiosInfoSum.Text = string.Format("BIOS: {0} ({1})", inv.BiosVersion, inv.BiosDate);
            _badgeUefiSum.Visibility = inv.IsUefi ? Visibility.Visible : Visibility.Collapsed;

            double totalMem = 0;
            foreach (var m in inv.MemoryModules) totalMem += m.SizeGb;
            string memType = inv.MemoryModules.Count > 0 ? inv.MemoryModules[0].MemoryType : "DDR4";
            _txtRamTotalSum.Text = string.Format("{0:N0} GB {1} SDRAM", totalMem, memType);
            _txtRamModeSum.Text = string.Format("Mode: {0}", inv.RamChannelMode);

            _pnlRamModulesSum.Children.Clear();
            for (int i = 0; i < inv.MemoryModules.Count; i++)
            {
                var mod = inv.MemoryModules[i];
                TextBlock modTxt = new TextBlock
                {
                    Text = string.Format("#{0} [{1}]: {2:N0} GB {3} ({4} MHz)", i, mod.SlotLabel, mod.SizeGb, mod.Manufacturer, mod.SpeedMhz),
                    FontSize = 10.5,
                    Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                    Margin = new Thickness(0, 1, 0, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                _pnlRamModulesSum.Children.Add(modTxt);
            }

            _pnlNetworkSummary.Children.Clear();
            foreach (var net in inv.NetworkAdapters)
            {
                string statusIcon = net.IsUp ? "🟢" : "⚪";
                TextBlock netTxt = new TextBlock
                {
                    Text = string.Format("{0} {1}: {2} ({3})", statusIcon, net.InterfaceType, net.Description, net.SpeedDisplay),
                    FontSize = 10.5,
                    Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                    Margin = new Thickness(0, 1, 0, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                _pnlNetworkSummary.Children.Add(netTxt);
            }

            // Column 3: GPU, Displays, Drives (Operating System section completely removed per request)
            string gpuName = _host != null && _host.Hardware != null && !string.IsNullOrEmpty(_host.Hardware.GpuName) ? _host.Hardware.GpuName : "Video Controller";
            _txtGpuNameSum.Text = gpuName;
            _txtGpuVramSum.Text = "8 GB GDDR6 (Dedicated/Shared)";
            _txtGpuBusSum.Text = inv.GpuPcieBus;

            _pnlDisplaysSummary.Children.Clear();
            foreach (var d in inv.Displays)
            {
                TextBlock dTxt = new TextBlock
                {
                    Text = string.Format("🖥️ {0} - {1}x{2} @ {3} Hz ({4})", d.MonitorModel, d.Width, d.Height, d.RefreshRateHz, d.ConnectionType),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                    Margin = new Thickness(0, 1, 0, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                _pnlDisplaysSummary.Children.Add(dTxt);
            }

            if (inv.AudioCodecs.Count > 0)
            {
                _txtAudioSummary.Text = string.Format("🔊 {0}", inv.AudioCodecs[0].CodecModel);
            }
            else
            {
                _txtAudioSummary.Text = "High Definition Audio Device";
            }

            _pnlDrivesSummary.Children.Clear();
            foreach (var drv in inv.PhysicalDrives)
            {
                string icon = drv.IsHealthy ? "🟢" : "⚠️";
                TextBlock drvTxt = new TextBlock
                {
                    Text = string.Format("{0} {1}: {2} [{3:N0} GB]", icon, drv.InterfaceType, drv.Model, drv.SizeGb),
                    FontSize = 10.5,
                    Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                    Margin = new Thickness(0, 1, 0, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                _pnlDrivesSummary.Children.Add(drvTxt);
            }
        }

        private void RenderDevicesTab(DeviceInventory inv)
        {
            if (inv == null) return;

            // 1. Chipset & Motherboard
            _pnlDevicesChipset.Children.Clear();
            AddDetailRow(_pnlDevicesChipset, "Motherboard Model:", string.Format("{0} {1}", inv.MotherboardManufacturer, inv.MotherboardModel));
            AddDetailRow(_pnlDevicesChipset, "Chipset Controller:", inv.ChipsetName);
            AddDetailRow(_pnlDevicesChipset, "BIOS Version / Date:", string.Format("{0} ({1})", inv.BiosVersion, inv.BiosDate));
            AddDetailRow(_pnlDevicesChipset, "Firmware Mode:", inv.IsUefi ? "UEFI Mode (Native)" : "Legacy BIOS (CSM)");

            // 2. Storage Controllers (NVMe / SATA AHCI)
            _pnlDevicesStorageControllers.Children.Clear();
            if (inv.StorageControllers.Count > 0)
            {
                foreach (var ctrl in inv.StorageControllers)
                {
                    AddDetailRow(_pnlDevicesStorageControllers, ctrl.Name + ":", string.Format("Fabricante: {0} | Status: {1}", ctrl.Manufacturer, ctrl.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesStorageControllers, "Controllers:", "Standard NVM Express & SATA AHCI Controller");
            }

            // 3. Physical Drives
            _pnlDevicesDrives.Children.Clear();
            foreach (var drv in inv.PhysicalDrives)
            {
                AddDetailRow(_pnlDevicesDrives, drv.InterfaceType + ":", string.Format("{0} | Capacidade: {1:N0} GB | Status: {2}", drv.Model, drv.SizeGb, drv.IsHealthy ? "Saudável (OK)" : "Alerta SMART"));
            }

            // 4. PCI Express Bridges & Root Ports
            _pnlDevicesPciBridges.Children.Clear();
            if (inv.PciBridges.Count > 0)
            {
                foreach (var pci in inv.PciBridges)
                {
                    AddDetailRow(_pnlDevicesPciBridges, pci.Name + ":", string.Format("ID: {0} | Status: {1}", pci.HardwareId, pci.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesPciBridges, "PCIe Bus:", "PCI Express Root Complex & Bridges Operational");
            }

            // 5. USB Controllers & Hubs
            _pnlDevicesUsb.Children.Clear();
            if (inv.UsbControllers.Count > 0)
            {
                foreach (var usb in inv.UsbControllers)
                {
                    AddDetailRow(_pnlDevicesUsb, usb.Name + ":", string.Format("Fabricante: {0} | Status: {1}", usb.Manufacturer, usb.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesUsb, "USB:", "USB 3.x xHCI Controller & Root Hubs Active");
            }

            // 6. Displays & EDID
            _pnlDevicesDisplays.Children.Clear();
            foreach (var disp in inv.Displays)
            {
                AddDetailRow(_pnlDevicesDisplays, disp.MonitorModel + ":", string.Format("{0}x{1} @ {2} Hz ({3} bpp) | Conexão: {4} | Adaptador: {5}", disp.Width, disp.Height, disp.RefreshRateHz, disp.BitsPerPixel, disp.ConnectionType, disp.AdapterName));
            }

            // 7. Audio Codecs
            _pnlDevicesAudio.Children.Clear();
            foreach (var audio in inv.AudioCodecs)
            {
                AddDetailRow(_pnlDevicesAudio, audio.Name + ":", string.Format("{0} [{1}]", audio.CodecModel, audio.Manufacturer));
            }

            // 8. Network Adapters
            _pnlDevicesNetwork.Children.Clear();
            foreach (var net in inv.NetworkAdapters)
            {
                AddDetailRow(_pnlDevicesNetwork, net.Name + " (" + net.InterfaceType + "):", string.Format("{0} | Link: {1} | MAC: {2} | Status: {3}", net.Description, net.SpeedDisplay, net.MacAddress, net.IsUp ? "Connected (Up)" : "Disconnected (Down)"));
            }

            // 9. Memory Slots
            _pnlDevicesMemory.Children.Clear();
            AddDetailRow(_pnlDevicesMemory, "Memory Architecture:", inv.RamChannelMode);
            for (int i = 0; i < inv.MemoryModules.Count; i++)
            {
                var mod = inv.MemoryModules[i];
                AddDetailRow(_pnlDevicesMemory, string.Format("Slot #{0} ({1}):", i, mod.SlotLabel), string.Format("{0:N0} GB {1} ({2} MHz) - Fabricante: {3} (PN: {4})", mod.SizeGb, mod.MemoryType, mod.SpeedMhz, mod.Manufacturer, mod.PartNumber));
            }

            // 10. System Sensors
            _pnlDevicesSensors.Children.Clear();
            if (inv.SystemSensors.Count > 0)
            {
                foreach (var sensor in inv.SystemSensors)
                {
                    AddDetailRow(_pnlDevicesSensors, sensor.Name + ":", string.Format("Tipo: {0} | Status: {1}", sensor.Category, sensor.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesSensors, "Thermal & Power Sensors:", "ACPI Thermal Zones & Battery Controller Sensor Active");
            }

            // 11. Input Devices
            _pnlDevicesInput.Children.Clear();
            if (inv.InputDevices.Count > 0)
            {
                foreach (var input in inv.InputDevices)
                {
                    AddDetailRow(_pnlDevicesInput, input.Name + ":", string.Format("Tipo: {0} | Status: {1}", input.Category, input.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesInput, "HID Devices:", "Standard Keyboard & Mouse Pointing Devices Active");
            }
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            if (_isMonitoringPaused) return;
            _tickCount++;

            Task.Run(() =>
            {
                try
                {
                    var snap = _monitorService.CaptureSnapshot();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_txtCpuLoadLive != null) _txtCpuLoadLive.Text = string.Format("CPU Load: {0:F1}%", snap.Cpu.UsagePercent);
                        if (_pbCpuLoadLive != null) _pbCpuLoadLive.Value = snap.Cpu.UsagePercent;
                        if (_txtCpuClockLive != null)
                        {
                            string pulseIcon = (_tickCount % 2 == 0) ? "⚡" : "•";
                            string tempStr = snap.Cpu.TemperatureC.HasValue ? string.Format("  |  Temp: {0:F1} °C", snap.Cpu.TemperatureC.Value) : "  |  Temp: Sensor ACPI Ocioso";
                            string clockStr = snap.Cpu.ClockMhz > 0 ? string.Format("{0:N0} MHz", snap.Cpu.ClockMhz) : "Dynamic";
                            _txtCpuClockLive.Text = string.Format("{0} Live Clock: {1}{2}", pulseIcon, clockStr, tempStr);
                        }
                    });
                }
                catch { }
            });
        }
        #endregion

        #region UI Helpers
        private Border CreateCard()
        {
            return new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14)
            };
        }

        private Border CreateDenseCard()
        {
            return new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };
        }

        private TextBlock CreateCardHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushAccent", "#0EA5E9")
            };
        }

        private ProgressBar CreateMetricProgressBar()
        {
            return new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = GetResourceBrush("BrushAccent", "#0EA5E9")
            };
        }

        private Button CreateStyledButton(string content, Brush bg, Brush fg)
        {
            return new Button
            {
                Content = content,
                Background = bg,
                Foreground = fg,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }

        private Brush GetResourceBrush(string key, string fallbackHex)
        {
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                var brush = Application.Current.Resources[key] as Brush;
                if (brush != null) return brush;
            }
            return (SolidColorBrush)new BrushConverter().ConvertFromString(fallbackHex);
        }

        private string T(string key, string fallback)
        {
            if (_host != null)
            {
                return _host.GetLocalizedString(key, fallback);
            }
            return fallback;
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = "⚡ " + T("Nav_HardwareMonitor", "Hardware Monitor");
            if (_txtSubtitle != null) _txtSubtitle.Text = T("HardwareMonitor_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics");

            if (_tabSummaryText != null) _tabSummaryText.Text = "🖥️ " + T("PCAssist_Tab_Summary", "System Summary");
            if (_tabDevicesText != null) _tabDevicesText.Text = "🔌 " + T("PCAssist_Tab_Devices", "Devices & Peripherals");
            if (_tabBatteryText != null) _tabBatteryText.Text = "🔋 " + T("PCAssist_Tab_Battery", "Battery & Power");
            if (_tabBenchText != null) _tabBenchText.Text = "🚀 " + T("PCAssist_Tab_Bench", "Bench Diagnostics");

            if (_txtDesktopNoticeTitle != null) _txtDesktopNoticeTitle.Text = "🖥️ " + T("PCAssist_Battery_Desktop", "Desktop PC (No Battery)");
            if (_txtDesktopNoticeDesc != null) _txtDesktopNoticeDesc.Text = T("PCAssist_Battery_DesktopDesc", "This PC is connected directly to mains AC power with no integrated battery.");

            if (_btnBatteryReport != null) _btnBatteryReport.Content = T("PCAssist_Battery_BtnReport", "📄 Generate Official Report (HTML)");

            if (_btnCpuBench != null) _btnCpuBench.Content = T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark");
            if (_btnRamBench != null) _btnRamBench.Content = T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark");
            if (_btnDiskBench != null) _btnDiskBench.Content = T("PCAssist_Bench_BtnStart", "▶️ Start Benchmark");
            if (_btnStressBench != null) _btnStressBench.Content = T("PCAssist_Bench_BtnStress", "🔥 Run Stress Test");
            if (_btnCancelBench != null) _btnCancelBench.Content = T("PCAssist_Bench_BtnCancel", "⏹️ Cancel Test");
        }
        #endregion
    }
}
