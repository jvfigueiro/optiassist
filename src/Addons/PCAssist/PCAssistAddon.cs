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
        private LibreHardwareEngine _lhmEngine;
        private LibreTelemetrySnapshot _lastLhmSnapshot;
        private bool _isSamplingLhm = false;
        private BatteryDiagnosticService _batteryService;
        private BenchEngineService _benchService;
        private DeviceInventoryService _inventoryService;

        private DeviceInventory _cachedInventory;
        private DispatcherTimer _monitorTimer;
        private bool _isMonitoringPaused = false;
        private CancellationTokenSource _benchCts;
        private int _tickCount = 0;

        // UI Tabs (5 Tabs: Summary | Sensors | Devices | Battery | Bench)
        private enum PCAssistTab { Summary, Sensors, Devices, Battery, Bench }
        private PCAssistTab _currentTab = PCAssistTab.Summary;

        private Border _tabSummaryBorder;
        private TextBlock _tabSummaryText;
        private Border _tabSensorsBorder;
        private TextBlock _tabSensorsText;
        private Border _tabDevicesBorder;
        private TextBlock _tabDevicesText;
        private Border _tabBatteryBorder;
        private TextBlock _tabBatteryText;
        private Border _tabBenchBorder;
        private TextBlock _tabBenchText;

        private ScrollViewer _pnlTabSummary;
        private ScrollViewer _pnlTabSensors;
        private ScrollViewer _pnlTabDevices;
        private ScrollViewer _pnlTabBattery;
        private ScrollViewer _pnlTabBench;

        // Tab 2: Live Sensors (LibreHardwareMonitor) Controls
        private Border _bannerPawnIo;
        private TextBlock _txtPawnIoStatus;
        private Button _btnInstallPawnIo;
        private TextBox _txtSensorsSearch;
        private StackPanel _pnlSensorsContainer;
        private TextBlock _txtGpuLiveMetrics;
        private TextBlock _txtBenchLiveTelemetry;

        // Header controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnToggleMonitor;

        // Tab 1: Summary Controls (HWiNFO-style 3 columns)
        private TextBlock _txtCpuUsageTop;
        private ProgressBar _pbCpuUsageTop;
        private TextBlock _txtRamUsageTop;
        private ProgressBar _pbRamUsageTop;

        private TextBlock _txtCpuNameSum;
        private TextBlock _txtCpuSocketSum;
        private TextBlock _txtCpuCoresSum;
        private TextBlock _txtCpuCacheSum;
        private WrapPanel _pnlCpuInstructions;
        private TextBlock _txtCpuClockLive;

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
        public string Version { get { return "1.2.S7"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "⚡"; } }

        static PCAssistAddon()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
            {
                try
                {
                    if (args == null || string.IsNullOrEmpty(args.Name)) return null;
                    string simple = new System.Reflection.AssemblyName(args.Name).Name;
                    if (string.IsNullOrEmpty(simple) || simple.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;

                    System.Reflection.Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < loaded.Length; i++)
                    {
                        try
                        {
                            if (string.Equals(loaded[i].GetName().Name, simple, StringComparison.OrdinalIgnoreCase))
                            {
                                return loaded[i];
                            }
                        }
                        catch { }
                    }

                    string pcDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons", "PCAssist");
                    string cand = Path.Combine(pcDir, simple + ".dll");
                    if (File.Exists(cand))
                    {
                        return System.Reflection.Assembly.Load(File.ReadAllBytes(cand));
                    }
                }
                catch { }
                return null;
            };
        }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _monitorService = new HardwareMonitorService();
            try
            {
                _lhmEngine = new LibreHardwareEngine(msg => { if (_host != null) _host.Log(msg); });
            }
            catch (Exception ex)
            {
                if (_host != null) _host.Log("[PCAssist:LHM Init] " + ex.Message);
            }
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

            // 1. Header (Standardized to match other OptiAssist modules)
            Grid headerGrid = new Grid { Margin = new Thickness(4, 2, 4, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _txtTitle = new TextBlock
            {
                Text = "⚡ " + T("Nav_HardwareMonitor", "Hardware Monitor"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Border betaBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            betaBadge.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            betaBadge.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
            TextBlock betaText = new TextBlock
            {
                Text = "Beta",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            betaText.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            betaBadge.Child = betaText;
            titleRow.Children.Add(_txtTitle);
            titleRow.Children.Add(betaBadge);

            _txtSubtitle = new TextBlock
            {
                Text = T("HardwareMonitor_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics"),
                FontSize = 11,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titlePanel.Children.Add(titleRow);
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

            Grid.SetRow(headerGrid, 0);
            root.Children.Add(headerGrid);

            // 2. Tabs Navigation Bar
            Border tabsBar = new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(4, 0, 4, 10),
                Padding = new Thickness(8, 0, 8, 0)
            };

            StackPanel tabsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _tabSummaryBorder = CreateTabButton("🖥️ " + T("PCAssist_Tab_Summary", "System Summary"), true, out _tabSummaryText);
            _tabSummaryBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Summary);
            tabsPanel.Children.Add(_tabSummaryBorder);

            _tabSensorsBorder = CreateTabButton("🌡️ " + T("PCAssist_Tab_Sensors", "Sensors"), false, out _tabSensorsText);
            _tabSensorsBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Sensors);
            tabsPanel.Children.Add(_tabSensorsBorder);

            _tabDevicesBorder = CreateTabButton("🔌 " + T("PCAssist_Tab_Devices", "Devices & Peripherals"), false, out _tabDevicesText);
            _tabDevicesBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Devices);
            tabsPanel.Children.Add(_tabDevicesBorder);

            _tabBatteryBorder = CreateTabButton("🔋 " + T("PCAssist_Tab_Battery", "Battery & Power"), false, out _tabBatteryText);
            bool quickHasBattery = DetectQuickBatteryPresence();
            _tabBatteryBorder.Visibility = quickHasBattery ? Visibility.Visible : Visibility.Collapsed;
            _tabBatteryBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Battery);
            tabsPanel.Children.Add(_tabBatteryBorder);

            _tabBenchBorder = CreateTabButton("🚀 " + T("PCAssist_Tab_Bench", "Benchmark & Stress Test"), false, out _tabBenchText);
            _tabBenchBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Bench);
            tabsPanel.Children.Add(_tabBenchBorder);

            tabsBar.Child = tabsPanel;
            Grid.SetRow(tabsBar, 1);
            root.Children.Add(tabsBar);

            // 3. Tab Contents
            Grid contentArea = new Grid();
            Grid.SetRow(contentArea, 2);

            _pnlTabSummary = BuildSummaryTab();
            _pnlTabSensors = BuildSensorsTab();
            _pnlTabDevices = BuildDevicesTab();
            _pnlTabBattery = BuildBatteryTab();
            _pnlTabBench = BuildBenchTab();

            contentArea.Children.Add(_pnlTabSummary);
            contentArea.Children.Add(_pnlTabSensors);
            contentArea.Children.Add(_pnlTabDevices);
            contentArea.Children.Add(_pnlTabBattery);
            contentArea.Children.Add(_pnlTabBench);

            root.Children.Add(contentArea);

            LoadDeviceInventory();
            UpdateBatteryReport();
            _monitorTimer.Start();

            root.Loaded += (s, e) => { UpdateBatteryReport(); if (!_monitorTimer.IsEnabled) _monitorTimer.Start(); };
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

            _tabSensorsBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Sensors ? 3 : 0);
            _tabSensorsText.FontWeight = tab == PCAssistTab.Sensors ? FontWeights.Bold : FontWeights.Normal;
            _tabSensorsText.Foreground = tab == PCAssistTab.Sensors ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

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
            _pnlTabSensors.Visibility = (tab == PCAssistTab.Sensors) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabDevices.Visibility = (tab == PCAssistTab.Devices) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBattery.Visibility = (tab == PCAssistTab.Battery) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBench.Visibility = (tab == PCAssistTab.Bench) ? Visibility.Visible : Visibility.Collapsed;

            if (tab == PCAssistTab.Battery) UpdateBatteryReport();
            if (tab == PCAssistTab.Sensors && _lastLhmSnapshot != null) RenderSensorsTab(_lastLhmSnapshot);
        }

        #region TAB 1: System Summary (HWiNFO-style)
        private ScrollViewer BuildSummaryTab()
        {
            ScrollViewer sv = CreateTabScrollViewer(Visibility.Visible);
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(4, 0, 4, 8) };

            // ==================== TOP LIVE USAGE BARS (CPU & RAM) ====================
            Grid topUsageGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            topUsageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topUsageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border cpuTopCard = CreateCard();
            cpuTopCard.Margin = new Thickness(0, 0, 6, 0);
            StackPanel cpuTopStack = new StackPanel();
            _txtCpuUsageTop = new TextBlock
            {
                Text = "CPU Usage: 0.0%",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _pbCpuUsageTop = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Foreground = GetResourceBrush("BrushAccent", "#0EA5E9")
            };
            cpuTopStack.Children.Add(_txtCpuUsageTop);
            cpuTopStack.Children.Add(_pbCpuUsageTop);
            cpuTopCard.Child = cpuTopStack;
            Grid.SetColumn(cpuTopCard, 0);
            topUsageGrid.Children.Add(cpuTopCard);

            Border ramTopCard = CreateCard();
            ramTopCard.Margin = new Thickness(6, 0, 0, 0);
            StackPanel ramTopStack = new StackPanel();
            _txtRamUsageTop = new TextBlock
            {
                Text = "RAM Usage: 0.0%",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _pbRamUsageTop = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString("#8B5CF6")
            };
            ramTopStack.Children.Add(_txtRamUsageTop);
            ramTopStack.Children.Add(_pbRamUsageTop);
            ramTopCard.Child = ramTopStack;
            Grid.SetColumn(ramTopCard, 1);
            topUsageGrid.Children.Add(ramTopCard);

            rootPanel.Children.Add(topUsageGrid);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ==================== ROW 0, COL 0: PROCESSOR (CPU) ====================
            Border cpuCard = CreateCard();
            cpuCard.Margin = new Thickness(0, 0, 6, 12);
            StackPanel cpuPanel = new StackPanel();

            cpuPanel.Children.Add(CreateDenseHeader("PROCESSOR (CPU)", "#0EA5E9"));
            _txtCpuNameSum = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 8, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };
            cpuPanel.Children.Add(_txtCpuNameSum);

            Grid cpuDetails = new Grid { Margin = new Thickness(0, 2, 0, 8) };
            cpuDetails.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cpuDetails.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel cpuDLeft = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            _txtCpuSocketSum = AddMiniParam(cpuDLeft, "Platform:", "Detecting...");
            _txtCpuCoresSum = AddMiniParam(cpuDLeft, "Cores/Threads:", "Detecting...");

            StackPanel cpuDRight = new StackPanel();
            _txtCpuCacheSum = AddMiniParam(cpuDRight, "L3 Cache:", "Detecting...");
            AddMiniParam(cpuDRight, "Architecture:", Environment.Is64BitOperatingSystem ? "x64 (64-bit)" : "x86 (32-bit)");

            Grid.SetColumn(cpuDLeft, 0);
            Grid.SetColumn(cpuDRight, 1);
            cpuDetails.Children.Add(cpuDLeft);
            cpuDetails.Children.Add(cpuDRight);
            cpuPanel.Children.Add(cpuDetails);

            cpuPanel.Children.Add(CreateSubDivider("OPERATING CLOCK"));
            _txtCpuClockLive = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 6, 0, 10),
                Text = "Clock: Detecting..."
            };
            cpuPanel.Children.Add(_txtCpuClockLive);

            cpuPanel.Children.Add(CreateSubDivider("INSTRUCTION SET FEATURES"));
            _pnlCpuInstructions = new WrapPanel { Margin = new Thickness(0, 6, 0, 2) };
            cpuPanel.Children.Add(_pnlCpuInstructions);

            cpuCard.Child = cpuPanel;
            Grid.SetRow(cpuCard, 0);
            Grid.SetColumn(cpuCard, 0);
            grid.Children.Add(cpuCard);

            // ==================== ROW 0, COL 1: MEMORY (RAM) ====================
            Border ramCard = CreateCard();
            ramCard.Margin = new Thickness(6, 0, 0, 12);
            StackPanel ramPanel = new StackPanel();

            ramPanel.Children.Add(CreateDenseHeader("MEMORY (RAM)", "#3B82F6"));
            _txtRamTotalSum = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 8, 0, 2)
            };
            _txtRamModeSum = new TextBlock
            {
                FontSize = 11.5,
                Foreground = GetResourceBrush("BrushAccent", "#0EA5E9"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            ramPanel.Children.Add(_txtRamTotalSum);
            ramPanel.Children.Add(_txtRamModeSum);

            ramPanel.Children.Add(CreateSubDivider("INSTALLED MODULES (PER SLOT)"));
            _pnlRamModulesSum = new StackPanel { Margin = new Thickness(0, 6, 0, 2) };
            ramPanel.Children.Add(_pnlRamModulesSum);

            ramCard.Child = ramPanel;
            Grid.SetRow(ramCard, 0);
            Grid.SetColumn(ramCard, 1);
            grid.Children.Add(ramCard);

            // ==================== ROW 1, COL 0: MOTHERBOARD & BIOS ====================
            Border moboCard = CreateCard();
            moboCard.Margin = new Thickness(0, 0, 6, 12);
            StackPanel moboPanel = new StackPanel();

            moboPanel.Children.Add(CreateDenseHeader("MOTHERBOARD & CHIPSET", "#10B981"));
            _txtMoboNameSum = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 8, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };
            moboPanel.Children.Add(_txtMoboNameSum);

            _txtChipsetNameSum = AddMiniParam(moboPanel, "Chipset:", "Detecting...");

            Grid biosGrid = new Grid { Margin = new Thickness(0, 6, 0, 2) };
            biosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            biosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _txtBiosInfoSum = new TextBlock
            {
                FontSize = 11.5,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Text = "BIOS: Detecting...",
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            _badgeUefiSum = CreateBadge("UEFI", "#10B981");
            Grid.SetColumn(_txtBiosInfoSum, 0);
            Grid.SetColumn(_badgeUefiSum, 1);
            biosGrid.Children.Add(_txtBiosInfoSum);
            biosGrid.Children.Add(_badgeUefiSum);
            moboPanel.Children.Add(biosGrid);

            moboCard.Child = moboPanel;
            Grid.SetRow(moboCard, 1);
            Grid.SetColumn(moboCard, 0);
            grid.Children.Add(moboCard);

            // ==================== ROW 1, COL 1: GRAPHICS & DISPLAYS ====================
            Border gpuCard = CreateCard();
            gpuCard.Margin = new Thickness(6, 0, 0, 12);
            StackPanel gpuPanel = new StackPanel();

            gpuPanel.Children.Add(CreateDenseHeader("GRAPHICS & DISPLAYS", "#8B5CF6"));
            _txtGpuNameSum = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 8, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };
            gpuPanel.Children.Add(_txtGpuNameSum);

            _txtGpuVramSum = AddMiniParam(gpuPanel, "Memory:", "Detecting...");
            _txtGpuBusSum = AddMiniParam(gpuPanel, "Interface:", "PCIe x16");
            _txtGpuLiveMetrics = AddMiniParam(gpuPanel, "Live Telemetry:", "Sampling...");

            gpuPanel.Children.Add(CreateSubDivider("ACTIVE DISPLAYS & MONITORS"));
            _pnlDisplaysSummary = new StackPanel { Margin = new Thickness(0, 6, 0, 2) };
            gpuPanel.Children.Add(_pnlDisplaysSummary);

            gpuCard.Child = gpuPanel;
            Grid.SetRow(gpuCard, 1);
            Grid.SetColumn(gpuCard, 1);
            grid.Children.Add(gpuCard);

            // ==================== ROW 2, COL 0: PHYSICAL DRIVES ====================
            Border drivesCard = CreateCard();
            drivesCard.Margin = new Thickness(0, 0, 6, 0);
            StackPanel drivesPanel = new StackPanel();

            drivesPanel.Children.Add(CreateDenseHeader("PHYSICAL DRIVES", "#F59E0B"));
            _pnlDrivesSummary = new StackPanel { Margin = new Thickness(0, 8, 0, 2) };
            drivesPanel.Children.Add(_pnlDrivesSummary);

            drivesCard.Child = drivesPanel;
            Grid.SetRow(drivesCard, 2);
            Grid.SetColumn(drivesCard, 0);
            grid.Children.Add(drivesCard);

            // ==================== ROW 2, COL 1: NETWORK & AUDIO ====================
            Border netAudioCard = CreateCard();
            netAudioCard.Margin = new Thickness(6, 0, 0, 0);
            StackPanel netAudioPanel = new StackPanel();

            netAudioPanel.Children.Add(CreateDenseHeader("NETWORK & AUDIO", "#EC4899"));
            netAudioPanel.Children.Add(CreateSubDivider("NETWORK INTERFACES"));
            _pnlNetworkSummary = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
            netAudioPanel.Children.Add(_pnlNetworkSummary);

            netAudioPanel.Children.Add(CreateSubDivider("AUDIO DEVICES"));
            _txtAudioSummary = new TextBlock
            {
                FontSize = 11.5,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                Margin = new Thickness(0, 6, 0, 2),
                TextWrapping = TextWrapping.Wrap
            };
            netAudioPanel.Children.Add(_txtAudioSummary);

            netAudioCard.Child = netAudioPanel;
            Grid.SetRow(netAudioCard, 2);
            Grid.SetColumn(netAudioCard, 1);
            grid.Children.Add(netAudioCard);

            rootPanel.Children.Add(grid);
            sv.Content = rootPanel;
            return sv;
        }

        private TextBlock AddMiniParam(StackPanel parent, string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock { Text = label, FontSize = 11, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 0, 8, 0) };
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
            string icon = "⚙️";
            string upper = (text ?? "").ToUpperInvariant();
            if (upper.Contains("CPU") || upper.Contains("PROCESSOR")) icon = "🧠";
            else if (upper.Contains("RAM") || upper.Contains("MEMORY")) icon = "⚡";
            else if (upper.Contains("MOTHERBOARD") || upper.Contains("CHIPSET")) icon = "🔌";
            else if (upper.Contains("GRAPHICS") || upper.Contains("DISPLAY")) icon = "🎮";
            else if (upper.Contains("DRIVE") || upper.Contains("STORAGE")) icon = "💾";
            else if (upper.Contains("NETWORK") || upper.Contains("AUDIO")) icon = "🌐";

            StackPanel headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };

            Border iconBadge = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hexAccent),
                Margin = new Thickness(0, 0, 10, 0)
            };
            iconBadge.Child = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerRow.Children.Add(iconBadge);

            TextBlock titleTb = new TextBlock
            {
                Text = text,
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerRow.Children.Add(titleTb);

            return new Border
            {
                Child = headerRow,
                Margin = new Thickness(0, 0, 0, 4)
            };
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
            ScrollViewer sv = CreateTabScrollViewer(Visibility.Collapsed);
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

            // 7. Audio Devices
            Border cardAudio = CreateCard();
            cardAudio.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stAudio = new StackPanel();
            stAudio.Children.Add(CreateCardHeader("🔊 " + T("PCAssist_Dev_AudioTitle", "Audio Devices")));
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
            ScrollViewer sv = CreateTabScrollViewer(Visibility.Collapsed);
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
            Grid g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 0, 14, 0),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            TextBlock val = new TextBlock
            {
                Text = value,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            g.Children.Add(lbl);
            g.Children.Add(val);
            parent.Children.Add(g);
            return val;
        }

        private bool DetectQuickBatteryPresence()
        {
            try
            {
                if (_host != null && _host.Platform != null && _host.Platform.HasBattery)
                {
                    return true;
                }
            }
            catch { }

            try
            {
                NativeMethods.SYSTEM_POWER_STATUS sps;
                if (NativeMethods.GetSystemPowerStatus(out sps))
                {
                    if ((sps.BatteryFlag & 128) == 0 && sps.BatteryFlag != 255)
                    {
                        return true;
                    }
                    if (sps.BatteryLifePercent <= 100)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void UpdateBatteryReport()
        {
            Task.Run(() =>
            {
                try
                {
                    var report = _batteryService.AnalyzeBattery();
                    if (report == null) return;
                    if (!report.HasBattery && DetectQuickBatteryPresence())
                    {
                        report.HasBattery = true;
                        if (string.IsNullOrEmpty(report.DiagnosticSummary) || report.DiagnosticSummary.IndexOf("Desktop", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            report.DiagnosticSummary = "Battery detected via Windows Power Status (limited EC telemetry).";
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (!report.HasBattery)
                            {
                                if (_tabBatteryBorder != null) _tabBatteryBorder.Visibility = Visibility.Collapsed;
                                if (_cardBatteryDesktop != null) _cardBatteryDesktop.Visibility = Visibility.Collapsed;
                                if (_cardBatteryNotebook != null) _cardBatteryNotebook.Visibility = Visibility.Collapsed;
                                if (_currentTab == PCAssistTab.Battery)
                                {
                                    SwitchTab(PCAssistTab.Summary);
                                }
                                return;
                            }

                            if (_tabBatteryBorder != null) _tabBatteryBorder.Visibility = Visibility.Visible;
                            if (_cardBatteryDesktop != null) _cardBatteryDesktop.Visibility = Visibility.Collapsed;
                            if (_cardBatteryNotebook != null) _cardBatteryNotebook.Visibility = Visibility.Visible;

                            double safeHealth = (double.IsNaN(report.HealthPercent) || double.IsInfinity(report.HealthPercent)) ? 0.0 : Math.Max(0.0, Math.Min(100.0, report.HealthPercent));
                            double safeWear = (double.IsNaN(report.WearPercent) || double.IsInfinity(report.WearPercent)) ? 0.0 : Math.Max(0.0, Math.Min(100.0, report.WearPercent));

                            _txtBatteryHealthBig.Text = safeHealth > 0
                                ? string.Format("{0:F1}% {1}", safeHealth, T("PCAssist_Battery_Health", "Battery Health"))
                                : string.Format("{0}% {1}", report.ChargePercent, T("PCAssist_Battery_Charge", "Charge"));
                            _txtBatteryWearBadge.Text = string.Format("{0:F1}% {1}", safeWear, T("PCAssist_Battery_Wear", "Wear"));
                            _pbBatteryHealth.Value = safeHealth > 0 ? safeHealth : report.ChargePercent;

                            if (safeHealth >= 85 || safeHealth == 0) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushSuccess", "#10B981");
                            else if (safeHealth >= 70) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushAccent", "#0EA5E9");
                            else if (safeHealth >= 50) _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushWarning", "#F59E0B");
                            else _txtBatteryHealthBig.Foreground = GetResourceBrush("BrushDanger", "#EF4444");

                            _txtBatteryHealthSubtitle.Text = report.DiagnosticSummary ?? "";

                            _txtBatteryDesignCap.Text = report.DesignCapacityMwh > 0 ? string.Format("{0:N0} mWh", report.DesignCapacityMwh) : "N/A";
                            _txtBatteryFullCap.Text = report.FullChargeCapacityMwh > 0 ? string.Format("{0:N0} mWh", report.FullChargeCapacityMwh) : "N/A";
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

                            _txtBatteryVoltage.Text = report.VoltageVolts > 0 ? string.Format("{0:F2} V", report.VoltageVolts) : "N/A";
                            _txtBatteryRate.Text = string.Format("{0:F2} W ({1})", report.RateWatts, report.IsCharging ? "Charging" : "Discharging");
                            string maker = string.Format("{0} {1}", report.Manufacturer, report.DeviceName).Trim();
                            _txtBatteryMaker.Text = string.IsNullOrEmpty(maker) ? "Standard ACPI Battery" : maker;
                            _txtBatteryChemistry.Text = string.IsNullOrEmpty(report.Chemistry) ? "Li-Ion / Polymer" : report.Chemistry;
                            _txtBatteryCycles.Text = report.CycleCount.HasValue ? string.Format("{0} cycles", report.CycleCount.Value) : "Not reported by EC";
                        }
                        catch (Exception uiEx)
                        {
                            if (_host != null) _host.Log("[HardwareMonitor:Battery:UI] " + uiEx.Message);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (_host != null) _host.Log("[HardwareMonitor:Battery] " + ex.Message);
                }
            });
        }
        #endregion

        #region TAB 2: Live Sensors (LibreHardwareMonitor)
        private ScrollViewer BuildSensorsTab()
        {
            ScrollViewer sv = CreateTabScrollViewer(Visibility.Collapsed);
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(4, 0, 4, 12) };

            // PawnIO Optional Driver Status Banner
            _bannerPawnIo = CreateCard();
            _bannerPawnIo.Margin = new Thickness(0, 0, 0, 10);
            Grid pawnGrid = new Grid();
            pawnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pawnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtPawnIoStatus = new TextBlock
            {
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A")
            };
            Grid.SetColumn(_txtPawnIoStatus, 0);
            pawnGrid.Children.Add(_txtPawnIoStatus);

            _btnInstallPawnIo = CreateStyledButton(
                "⬇️ " + T("PCAssist_Sensors_InstallPawnIO", "Install Sensor Driver (PawnIO)"),
                GetResourceBrush("BrushAccent", "#0EA5E9"),
                Brushes.White);
            _btnInstallPawnIo.Margin = new Thickness(12, 0, 0, 0);
            _btnInstallPawnIo.VerticalAlignment = VerticalAlignment.Center;
            _btnInstallPawnIo.Click += (s, e) =>
            {
                bool isPt = _host != null && (_host.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);
                string confirmMsg = isPt
                    ? "O OptiAssist baixará o instalador oficial assinado do driver PawnIO diretamente do GitHub (namazso/PawnIO.Setup) e o executará para habilitar sensores de temperatura/clock da CPU e ventoinhas/tensões da placa-mãe.\n\nDeseja baixar e instalar agora?"
                    : "OptiAssist will download the official signed PawnIO driver installer directly from GitHub (namazso/PawnIO.Setup) and launch it to unlock CPU temperatures/clocks and motherboard Super I/O fans/voltages.\n\nDownload and install now?";

                if (OptiDialog.Show(confirmMsg, "OptiAssist - PawnIO", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                _btnInstallPawnIo.IsEnabled = false;
                _btnInstallPawnIo.Content = "⏳ " + (isPt ? "Baixando..." : "Downloading...");
                LibreHardwareEngine.DownloadAndInstallPawnIoAsync(
                    msg => { if (_host != null) _host.Log(msg); },
                    (ok, msg) =>
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            _btnInstallPawnIo.IsEnabled = true;
                            _btnInstallPawnIo.Content = "⬇️ " + T("PCAssist_Sensors_InstallPawnIO", "Install Sensor Driver (PawnIO)");
                            if (ok && _lhmEngine != null)
                            {
                                _lhmEngine.Reinitialize();
                            }
                            UpdatePawnIoBanner();
                            OptiDialog.Show(msg, "OptiAssist - PawnIO", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                        }));
                    });
            };
            Grid.SetColumn(_btnInstallPawnIo, 1);
            pawnGrid.Children.Add(_btnInstallPawnIo);
            _bannerPawnIo.Child = pawnGrid;
            rootPanel.Children.Add(_bannerPawnIo);
            UpdatePawnIoBanner();

            // Toolbar: Search + Reset Min/Max + Export Report
            Grid toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtSensorsSearch = new TextBox
            {
                Height = 28,
                FontSize = 11.5,
                Padding = new Thickness(8, 3, 8, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = T("PCAssist_Sensors_SearchTip", "Filter sensors by hardware or sensor name (e.g. Temp, Fan, GPU, NVMe)")
            };
            _txtSensorsSearch.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtSensorsSearch.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtSensorsSearch.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
            _txtSensorsSearch.TextChanged += (s, e) =>
            {
                if (_lastLhmSnapshot != null) RenderSensorsTab(_lastLhmSnapshot);
            };
            Grid.SetColumn(_txtSensorsSearch, 0);
            toolbar.Children.Add(_txtSensorsSearch);

            Button btnReset = CreateStyledButton("🔄 " + T("PCAssist_Sensors_ResetMinMax", "Reset Min/Max"), GetResourceBrush("BrushBtnBg", "#E2E8F0"), GetResourceBrush("BrushTextPrimary", "#0F172A"));
            btnReset.Margin = new Thickness(8, 0, 6, 0);
            btnReset.Click += (s, e) =>
            {
                if (_lhmEngine != null) _lhmEngine.ResetMinMax();
            };
            Grid.SetColumn(btnReset, 1);
            toolbar.Children.Add(btnReset);

            Button btnExport = CreateStyledButton("📋 " + T("PCAssist_Sensors_Export", "Export Report"), GetResourceBrush("BrushBtnBg", "#E2E8F0"), GetResourceBrush("BrushTextPrimary", "#0F172A"));
            btnExport.Click += (s, e) =>
            {
                if (_lhmEngine == null) return;
                string report = _lhmEngine.ExportTextReport(_lastLhmSnapshot);
                OptiDialog.ShowTextViewer("Hardware Monitor — Sensors Report", report, null, 780, 540);
            };
            Grid.SetColumn(btnExport, 2);
            toolbar.Children.Add(btnExport);

            rootPanel.Children.Add(toolbar);

            _pnlSensorsContainer = new StackPanel();
            TextBlock loadingTxt = new TextBlock
            {
                Text = T("PCAssist_Sensors_Loading", "Initializing LibreHardwareMonitor sensors..."),
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(4, 12, 4, 12)
            };
            _pnlSensorsContainer.Children.Add(loadingTxt);
            rootPanel.Children.Add(_pnlSensorsContainer);

            sv.Content = rootPanel;
            return sv;
        }

        private void UpdatePawnIoBanner()
        {
            if (_txtPawnIoStatus == null || _btnInstallPawnIo == null) return;
            bool isPt = _host != null && (_host.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);
            bool installed = LibreHardwareEngine.IsPawnIoInstalled();
            if (installed)
            {
                _txtPawnIoStatus.Text = isPt
                    ? "✅ Driver de sensores PawnIO ativo — leitura completa de CPU, Placa-Mãe (Super I/O), GPU, SMART/NVMe e Bateria habilitada."
                    : "✅ PawnIO sensor driver active — full CPU, Motherboard (Super I/O), GPU, SMART/NVMe, and Battery telemetry enabled.";
                _btnInstallPawnIo.Visibility = Visibility.Collapsed;
            }
            else
            {
                _txtPawnIoStatus.Text = isPt
                    ? "ℹ️ GPU, Armazenamento (SMART), Memória, Rede e Bateria estão ativos. Para liberar temperaturas/clocks da CPU e ventoinhas/tensões da Placa-Mãe, instale opcionalmente o driver oficial assinado PawnIO."
                    : "ℹ️ GPU, Storage (SMART), Memory, Network & Battery are active. To unlock CPU temperatures/clocks and Motherboard fans/voltages, optionally install the official signed PawnIO driver.";
                _btnInstallPawnIo.Visibility = Visibility.Visible;
            }
        }

        private void RenderSensorsTab(LibreTelemetrySnapshot snap)
        {
            if (_pnlSensorsContainer == null) return;
            _pnlSensorsContainer.Children.Clear();

            if (snap == null || !snap.IsAvailable || snap.Groups == null || snap.Groups.Count == 0)
            {
                TextBlock empty = new TextBlock
                {
                    Text = (snap != null && !string.IsNullOrEmpty(snap.ErrorMessage))
                        ? "LibreHardwareMonitor: " + snap.ErrorMessage
                        : T("PCAssist_Sensors_Empty", "No hardware sensors detected yet."),
                    FontSize = 12,
                    Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                    Margin = new Thickness(4, 10, 4, 10)
                };
                _pnlSensorsContainer.Children.Add(empty);
                return;
            }

            string filter = (_txtSensorsSearch != null) ? (_txtSensorsSearch.Text ?? "").Trim() : "";

            for (int g = 0; g < snap.Groups.Count; g++)
            {
                LibreHardwareGroup grp = snap.Groups[g];
                List<LibreSensorRow> matchedRows = new List<LibreSensorRow>();
                bool groupMatches = string.IsNullOrEmpty(filter) ||
                    (grp.HardwareName != null && grp.HardwareName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (grp.HardwareType != null && grp.HardwareType.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                for (int i = 0; i < grp.Sensors.Count; i++)
                {
                    LibreSensorRow r = grp.Sensors[i];
                    if (groupMatches ||
                        (r.Name != null && r.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (r.Category != null && r.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchedRows.Add(r);
                    }
                }

                if (matchedRows.Count == 0) continue;

                Border card = CreateCard();
                card.Margin = new Thickness(0, 0, 0, 10);
                StackPanel sp = new StackPanel();

                // Group Header
                TextBlock hdr = new TextBlock
                {
                    Text = string.Format("{0}  {1}  ({2})", grp.Icon, grp.HardwareName, grp.HardwareType),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetResourceBrush("BrushAccent", "#0EA5E9"),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                sp.Children.Add(hdr);

                // Table Column Headers
                Grid colHdr = new Grid { Margin = new Thickness(4, 0, 4, 4) };
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AddSensorCell(colHdr, 0, "Sensor", true, HorizontalAlignment.Left, "BrushTextSecondary");
                AddSensorCell(colHdr, 1, T("PCAssist_Col_Current", "Current"), true, HorizontalAlignment.Right, "BrushTextSecondary");
                AddSensorCell(colHdr, 2, T("PCAssist_Col_Min", "Min"), true, HorizontalAlignment.Right, "BrushTextSecondary");
                AddSensorCell(colHdr, 3, T("PCAssist_Col_Max", "Max"), true, HorizontalAlignment.Right, "BrushTextSecondary");
                sp.Children.Add(colHdr);

                for (int i = 0; i < matchedRows.Count; i++)
                {
                    LibreSensorRow r = matchedRows[i];
                    Border rowBorder = new Border
                    {
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                        Padding = new Thickness(4, 3, 4, 3)
                    };
                    Grid rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    AddSensorCell(rowGrid, 0, string.Format("{0} ({1})", r.Name, r.Category), false, HorizontalAlignment.Left, "BrushTextPrimary");
                    AddSensorCell(rowGrid, 1, r.FormattedValue, true, HorizontalAlignment.Right, "BrushAccent");
                    AddSensorCell(rowGrid, 2, r.FormattedMin, false, HorizontalAlignment.Right, "BrushTextSecondary");
                    AddSensorCell(rowGrid, 3, r.FormattedMax, false, HorizontalAlignment.Right, "BrushTextSecondary");

                    rowBorder.Child = rowGrid;
                    sp.Children.Add(rowBorder);
                }

                card.Child = sp;
                _pnlSensorsContainer.Children.Add(card);
            }
        }

        private void AddSensorCell(Grid grid, int col, string text, bool bold, HorizontalAlignment align, string brushKey)
        {
            TextBlock tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = 11,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = align,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }
        #endregion

        #region TAB 4: Bench Diagnostics View
        private ScrollViewer BuildBenchTab()
        {
            ScrollViewer sv = CreateTabScrollViewer(Visibility.Collapsed);
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(4, 0, 4, 12) };

            // Live Thermal & Clock Monitoring Strip during Benchmarks
            Border liveStrip = CreateCard();
            liveStrip.Margin = new Thickness(0, 0, 0, 10);
            _txtBenchLiveTelemetry = new TextBlock
            {
                Text = "🌡️ Live Telemetry: Waiting for sensors...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"),
                TextWrapping = TextWrapping.Wrap
            };
            liveStrip.Child = _txtBenchLiveTelemetry;
            rootPanel.Children.Add(liveStrip);

            _btnCancelBench = CreateStyledButton(T("PCAssist_Bench_BtnCancel", "⏹️ Cancel Test"), GetResourceBrush("BrushDanger", "#EF4444"), Brushes.White);
            _btnCancelBench.Visibility = Visibility.Collapsed;
            _btnCancelBench.Margin = new Thickness(0, 0, 0, 12);
            _btnCancelBench.Click += (s, e) =>
            {
                if (_benchCts != null) _benchCts.Cancel();
            };
            rootPanel.Children.Add(_btnCancelBench);

            Grid benchGrid = new Grid();
            benchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            benchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            benchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            benchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. CPU Benchmark Card (Row 0, Col 0)
            Border cpuBenchCard = CreateCard();
            cpuBenchCard.Margin = new Thickness(0, 0, 6, 12);
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
            _txtCpuBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };

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
            Grid.SetRow(cpuBenchCard, 0);
            Grid.SetColumn(cpuBenchCard, 0);
            benchGrid.Children.Add(cpuBenchCard);

            // 2. RAM Benchmark Card (Row 0, Col 1)
            Border ramBenchCard = CreateCard();
            ramBenchCard.Margin = new Thickness(6, 0, 0, 12);
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
            _txtRamBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };

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
            Grid.SetRow(ramBenchCard, 0);
            Grid.SetColumn(ramBenchCard, 1);
            benchGrid.Children.Add(ramBenchCard);

            // 3. Storage Benchmark Card (Row 1, Col 0)
            Border diskBenchCard = CreateCard();
            diskBenchCard.Margin = new Thickness(0, 0, 6, 0);
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
            _txtDiskBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };

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
            Grid.SetRow(diskBenchCard, 1);
            Grid.SetColumn(diskBenchCard, 0);
            benchGrid.Children.Add(diskBenchCard);

            // 4. Stress Test Card (Row 1, Col 1)
            Border stressBenchCard = CreateCard();
            stressBenchCard.Margin = new Thickness(6, 0, 0, 0);
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
            _txtStressBenchResult = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };

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
            Grid.SetRow(stressBenchCard, 1);
            Grid.SetColumn(stressBenchCard, 1);
            benchGrid.Children.Add(stressBenchCard);

            rootPanel.Children.Add(benchGrid);

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
            _txtGpuVramSum.Text = !string.IsNullOrEmpty(inv.GpuVramDisplay) ? inv.GpuVramDisplay : T("Status_NotAvailable", "Not available");
            _txtGpuBusSum.Text = !string.IsNullOrEmpty(inv.GpuPcieBus) ? inv.GpuPcieBus : T("Status_NotAvailable", "Not available");

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
                    AddDetailRow(_pnlDevicesStorageControllers, ctrl.Name + ":", string.Format("{0}: {1} | Status: {2}", T("PCAssist_Dev_Manufacturer", "Manufacturer"), ctrl.Manufacturer, ctrl.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesStorageControllers, T("PCAssist_Dev_Controllers", "Controllers:"), T("PCAssist_Dev_NoStorageCtrl", "No additional storage controllers reported"));
            }

            // 3. Physical Drives
            _pnlDevicesDrives.Children.Clear();
            foreach (var drv in inv.PhysicalDrives)
            {
                AddDetailRow(_pnlDevicesDrives, drv.InterfaceType + ":", string.Format("{0} | {1}: {2:N0} GB | Status: {3}", drv.Model, T("PCAssist_Dev_Capacity", "Capacity"), drv.SizeGb, drv.IsHealthy ? T("PCAssist_Dev_Healthy", "Healthy (OK)") : T("PCAssist_Dev_SmartAlert", "SMART Warning")));
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
                AddDetailRow(_pnlDevicesPciBridges, T("PCAssist_Dev_Buses", "Buses:"), T("PCAssist_Dev_NoPci", "No additional PCI bridges reported"));
            }

            // 5. USB Controllers & Hubs
            _pnlDevicesUsb.Children.Clear();
            if (inv.UsbControllers.Count > 0)
            {
                foreach (var usb in inv.UsbControllers)
                {
                    AddDetailRow(_pnlDevicesUsb, usb.Name + ":", string.Format("{0}: {1} | Status: {2}", T("PCAssist_Dev_Manufacturer", "Manufacturer"), usb.Manufacturer, usb.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesUsb, T("PCAssist_Dev_UsbCtrl", "USB Controllers:"), T("PCAssist_Dev_NoUsb", "No USB controllers reported"));
            }

            // 6. Displays & EDID
            _pnlDevicesDisplays.Children.Clear();
            foreach (var disp in inv.Displays)
            {
                AddDetailRow(_pnlDevicesDisplays, disp.MonitorModel + ":", string.Format("{0}x{1} @ {2} Hz ({3} bpp) | {4}: {5} | {6}: {7}", disp.Width, disp.Height, disp.RefreshRateHz, disp.BitsPerPixel, T("PCAssist_Dev_Connection", "Connection"), disp.ConnectionType, T("PCAssist_Dev_Adapter", "Adapter"), disp.AdapterName));
            }

            // 7. Audio Devices
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
                AddDetailRow(_pnlDevicesMemory, string.Format("Slot #{0} ({1}):", i, mod.SlotLabel), string.Format("{0:N0} GB {1} ({2} MHz) - {3}: {4} (PN: {5})", mod.SizeGb, mod.MemoryType, mod.SpeedMhz, T("PCAssist_Dev_Manufacturer", "Manufacturer"), mod.Manufacturer, mod.PartNumber));
            }

            // 10. System Sensors
            _pnlDevicesSensors.Children.Clear();
            if (inv.SystemSensors.Count > 0)
            {
                foreach (var sensor in inv.SystemSensors)
                {
                    AddDetailRow(_pnlDevicesSensors, sensor.Name + ":", string.Format("{0}: {1} | Status: {2}", T("PCAssist_Dev_Type", "Type"), sensor.Category, sensor.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesSensors, T("PCAssist_Dev_Sensors", "Sensors:"), T("PCAssist_Dev_NoSensors", "CPU/GPU DTS sensors require a kernel-level (Ring-0) driver"));
            }

            // 11. Input Devices
            _pnlDevicesInput.Children.Clear();
            if (inv.InputDevices.Count > 0)
            {
                foreach (var input in inv.InputDevices)
                {
                    AddDetailRow(_pnlDevicesInput, input.Name + ":", string.Format("{0}: {1} | Status: {2}", T("PCAssist_Dev_Type", "Type"), input.Category, input.Status));
                }
            }
            else
            {
                AddDetailRow(_pnlDevicesInput, T("PCAssist_Dev_Input", "Input Devices:"), T("PCAssist_Dev_StdInput", "Standard system devices"));
            }
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            if (_isMonitoringPaused || _isSamplingLhm) return;
            _tickCount++;
            _isSamplingLhm = true;

            Task.Run(() =>
            {
                try
                {
                    var snap = _monitorService.CaptureSnapshot();
                    LibreTelemetrySnapshot lhm = (_lhmEngine != null) ? _lhmEngine.Sample() : null;
                    _lastLhmSnapshot = lhm;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        double cpuUsage = (lhm != null && lhm.CpuUsagePercent.HasValue) ? lhm.CpuUsagePercent.Value : snap.Cpu.UsagePercent;
                        if (_txtCpuUsageTop != null)
                        {
                            string cpuTempTag = (lhm != null && lhm.CpuPackageTempC.HasValue)
                                ? string.Format("  •  {0:F1} °C", lhm.CpuPackageTempC.Value)
                                : "";
                            _txtCpuUsageTop.Text = string.Format("CPU Usage: {0:F1}%{1}", cpuUsage, cpuTempTag);
                        }
                        if (_pbCpuUsageTop != null) _pbCpuUsageTop.Value = Math.Max(0, Math.Min(100, cpuUsage));

                        double ramUsed = (lhm != null && lhm.RamUsedGb.HasValue) ? lhm.RamUsedGb.Value : snap.Ram.UsedGb;
                        double ramTotal = (lhm != null && lhm.RamUsedGb.HasValue && lhm.RamAvailableGb.HasValue)
                            ? (lhm.RamUsedGb.Value + lhm.RamAvailableGb.Value)
                            : snap.Ram.TotalGb;
                        double ramPct = (lhm != null && lhm.RamUsagePercent.HasValue)
                            ? lhm.RamUsagePercent.Value
                            : (ramTotal > 0 ? Math.Round((ramUsed / ramTotal) * 100.0, 1) : 0);
                        if (_txtRamUsageTop != null) _txtRamUsageTop.Text = string.Format("RAM Usage: {0:F1}% ({1:F1} / {2:F1} GB)", ramPct, ramUsed, ramTotal);
                        if (_pbRamUsageTop != null) _pbRamUsageTop.Value = Math.Max(0, Math.Min(100, ramPct));

                        if (_txtCpuClockLive != null)
                        {
                            double clkMhz = (lhm != null && lhm.CpuClockMhz.HasValue && lhm.CpuClockMhz.Value > 100)
                                ? lhm.CpuClockMhz.Value
                                : snap.Cpu.ClockMhz;
                            string clockStr = clkMhz > 0 ? string.Format("{0:N0} MHz", clkMhz) : T("PCAssist_Dynamic", "Dynamic");
                            string extra = "";
                            if (lhm != null && lhm.CpuPackageTempC.HasValue) extra += string.Format("  •  {0:F1} °C", lhm.CpuPackageTempC.Value);
                            if (lhm != null && lhm.CpuPackagePowerW.HasValue) extra += string.Format("  •  {0:F1} W", lhm.CpuPackagePowerW.Value);
                            _txtCpuClockLive.Text = string.Format("Clock: {0}{1}", clockStr, extra);
                        }

                        if (_txtGpuLiveMetrics != null && lhm != null)
                        {
                            List<string> gParts = new List<string>();
                            if (lhm.GpuUsagePercent.HasValue) gParts.Add(string.Format("Load {0:F0}%", lhm.GpuUsagePercent.Value));
                            if (lhm.GpuTempC.HasValue) gParts.Add(string.Format("{0:F0} °C", lhm.GpuTempC.Value));
                            if (lhm.GpuVramUsedMb.HasValue) gParts.Add(string.Format("VRAM {0:F0} MB", lhm.GpuVramUsedMb.Value));
                            if (lhm.GpuPowerW.HasValue) gParts.Add(string.Format("{0:F0} W", lhm.GpuPowerW.Value));
                            if (lhm.GpuFanPercent.HasValue) gParts.Add(string.Format("Fan {0:F0}%", lhm.GpuFanPercent.Value));
                            _txtGpuLiveMetrics.Text = gParts.Count > 0 ? string.Join("  •  ", gParts.ToArray()) : "Active";
                        }

                        if (lhm != null && lhm.HasBattery && _tabBatteryBorder != null && _tabBatteryBorder.Visibility != Visibility.Visible)
                        {
                            _tabBatteryBorder.Visibility = Visibility.Visible;
                        }

                        if (_currentTab == PCAssistTab.Sensors && lhm != null)
                        {
                            RenderSensorsTab(lhm);
                        }

                        if (_txtBenchLiveTelemetry != null && lhm != null)
                        {
                            string clk = lhm.CpuClockMhz.HasValue ? string.Format("{0:N0} MHz", lhm.CpuClockMhz.Value) : (snap.Cpu.ClockMhz > 0 ? string.Format("{0:N0} MHz", snap.Cpu.ClockMhz) : "—");
                            string tmp = lhm.CpuPackageTempC.HasValue ? string.Format("{0:F1} °C", lhm.CpuPackageTempC.Value) : "N/A (PawnIO)";
                            string pwr = lhm.CpuPackagePowerW.HasValue ? string.Format("{0:F1} W", lhm.CpuPackagePowerW.Value) : "—";
                            bool throttling = lhm.CpuPackageTempC.HasValue && lhm.CpuPackageTempC.Value >= 90.0;
                            _txtBenchLiveTelemetry.Text = string.Format(
                                "🌡️ CPU: {0:F1}%  |  Clock: {1}  |  Temp: {2}  |  Power: {3}{4}",
                                cpuUsage, clk, tmp, pwr,
                                throttling ? "   ⚠️ THERMAL THROTTLING WARNING!" : "");
                            if (throttling)
                            {
                                _txtBenchLiveTelemetry.Foreground = GetResourceBrush("BrushDanger", "#EF4444");
                            }
                            else
                            {
                                _txtBenchLiveTelemetry.Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A");
                            }
                        }
                    });
                }
                catch { }
                finally
                {
                    _isSamplingLhm = false;
                }
            });
        }
        #endregion

        #region UI Helpers
        private ScrollViewer CreateTabScrollViewer(Visibility visibility)
        {
            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                Visibility = visibility
            };
            sv.PreviewMouseWheel += (s, e) =>
            {
                if (!e.Handled && sv.ScrollableHeight > 0)
                {
                    e.Handled = true;
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / 2.5));
                }
            };
            return sv;
        }

        private Border CreateCard()
        {
            return new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
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
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12)
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
            if (_tabSensorsText != null) _tabSensorsText.Text = "🌡️ " + T("PCAssist_Tab_Sensors", "Sensors");
            if (_tabDevicesText != null) _tabDevicesText.Text = "🔌 " + T("PCAssist_Tab_Devices", "Devices & Peripherals");
            if (_tabBatteryText != null) _tabBatteryText.Text = "🔋 " + T("PCAssist_Tab_Battery", "Battery & Power");
            if (_tabBenchText != null) _tabBenchText.Text = "🚀 " + T("PCAssist_Tab_Bench", "Benchmark & Stress Test");

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
