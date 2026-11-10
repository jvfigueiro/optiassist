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

        private DispatcherTimer _monitorTimer;
        private bool _isMonitoringPaused = false;
        private CancellationTokenSource _benchCts;

        // UI Tabs
        private enum PCAssistTab { Monitor, Battery, Bench }
        private PCAssistTab _currentTab = PCAssistTab.Monitor;

        private Border _tabMonitorBorder;
        private TextBlock _tabMonitorText;
        private Border _tabBatteryBorder;
        private TextBlock _tabBatteryText;
        private Border _tabBenchBorder;
        private TextBlock _tabBenchText;

        private ScrollViewer _pnlTabMonitor;
        private ScrollViewer _pnlTabBattery;
        private ScrollViewer _pnlTabBench;

        // Header controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnToggleMonitor;

        // Tab 1: Monitor Controls
        private TextBlock _lblCpuTitle;
        private TextBlock _txtCpuName;
        private TextBlock _txtCpuLoad;
        private ProgressBar _pbCpuLoad;
        private TextBlock _txtCpuStats;

        private TextBlock _lblRamTitle;
        private TextBlock _txtRamLoad;
        private ProgressBar _pbRamLoad;
        private TextBlock _txtRamStats;

        private TextBlock _lblGpuTitle;
        private TextBlock _txtGpuName;
        private TextBlock _txtGpuLoad;
        private ProgressBar _pbGpuLoad;
        private TextBlock _txtGpuStats;

        private TextBlock _lblStorageTitle;
        private TextBlock _txtStorageIo;
        private TextBlock _txtSmartBadge;
        private StackPanel _pnlPartitions;

        // Tab 2: Battery Controls
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

        // Tab 3: Bench Controls
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
        public string Id { get { return "pc-assist"; } }
        public string Name { get { return T("Nav_PCAssist", "PC Assist"); } }
        public string Description { get { return T("PCAssist_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics"); } }
        public string Version { get { return "1.2.S4"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "⚡"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _monitorService = new HardwareMonitorService();
            _batteryService = new BatteryDiagnosticService();
            _benchService = new BenchEngineService();

            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromSeconds(1);
            _monitorTimer.Tick += OnMonitorTick;
        }

        public FrameworkElement CreateView()
        {
            Grid root = new Grid();
            root.Margin = new Thickness(0);

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tab bar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tab Content

            // 1. Header
            Border headerBorder = new Border
            {
                Background = GetResourceBrush("BrushBgCard", "#FFFFFF"),
                BorderBrush = GetResourceBrush("BrushBorderCard", "#CBD5E1"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel();
            _txtTitle = new TextBlock
            {
                Text = "⚡ " + T("Nav_PCAssist", "PC Assist"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A")
            };
            _txtSubtitle = new TextBlock
            {
                Text = T("PCAssist_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics"),
                FontSize = 12,
                Foreground = GetResourceBrush("BrushTextSecondary", "#475569"),
                Margin = new Thickness(0, 4, 0, 0)
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

            _tabMonitorBorder = CreateTabButton("📊 " + T("PCAssist_Tab_Monitor", "Hardware Monitor"), true, out _tabMonitorText);
            _tabMonitorBorder.MouseLeftButtonDown += (s, e) => SwitchTab(PCAssistTab.Monitor);
            tabsPanel.Children.Add(_tabMonitorBorder);

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

            _pnlTabMonitor = BuildMonitorTab();
            _pnlTabBattery = BuildBatteryTab();
            _pnlTabBench = BuildBenchTab();

            contentArea.Children.Add(_pnlTabMonitor);
            contentArea.Children.Add(_pnlTabBattery);
            contentArea.Children.Add(_pnlTabBench);

            root.Children.Add(contentArea);

            // Start Monitoring Timer
            _monitorTimer.Start();
            UpdateMonitorSnapshot();

            // When loaded / unloaded
            root.Loaded += (s, e) => { if (!_monitorTimer.IsEnabled) _monitorTimer.Start(); };
            root.Unloaded += (s, e) => { _monitorTimer.Stop(); };

            return root;
        }

        private Border CreateTabButton(string text, bool isActive, out TextBlock textBlock)
        {
            Border b = new Border
            {
                Padding = new Thickness(16, 12, 16, 10),
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

            _tabMonitorBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Monitor ? 3 : 0);
            _tabMonitorText.FontWeight = tab == PCAssistTab.Monitor ? FontWeights.Bold : FontWeights.Normal;
            _tabMonitorText.Foreground = tab == PCAssistTab.Monitor ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _tabBatteryBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Battery ? 3 : 0);
            _tabBatteryText.FontWeight = tab == PCAssistTab.Battery ? FontWeights.Bold : FontWeights.Normal;
            _tabBatteryText.Foreground = tab == PCAssistTab.Battery ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _tabBenchBorder.BorderThickness = new Thickness(0, 0, 0, tab == PCAssistTab.Bench ? 3 : 0);
            _tabBenchText.FontWeight = tab == PCAssistTab.Bench ? FontWeights.Bold : FontWeights.Normal;
            _tabBenchText.Foreground = tab == PCAssistTab.Bench ? GetResourceBrush("BrushAccent", "#0EA5E9") : GetResourceBrush("BrushTextSecondary", "#475569");

            _pnlTabMonitor.Visibility = (tab == PCAssistTab.Monitor) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBattery.Visibility = (tab == PCAssistTab.Battery) ? Visibility.Visible : Visibility.Collapsed;
            _pnlTabBench.Visibility = (tab == PCAssistTab.Bench) ? Visibility.Visible : Visibility.Collapsed;

            if (tab == PCAssistTab.Battery)
            {
                UpdateBatteryReport();
            }
        }

        #region TAB 1: Hardware Monitor View
        private ScrollViewer BuildMonitorTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Visible };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            // 2x2 Responsive Grid
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. CPU Card
            Border cpuCard = CreateCard();
            cpuCard.Margin = new Thickness(0, 0, 8, 16);
            StackPanel cpuStack = new StackPanel();

            _lblCpuTitle = CreateCardHeader("🖥️ " + T("PCAssist_Cpu_Title", "Processor (CPU)"));
            _txtCpuName = new TextBlock { FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 2, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis };
            _txtCpuLoad = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Text = "0.0%" };
            _pbCpuLoad = CreateMetricProgressBar();
            _txtCpuStats = new TextBlock { FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 8, 0, 0) };

            cpuStack.Children.Add(_lblCpuTitle);
            cpuStack.Children.Add(_txtCpuName);
            cpuStack.Children.Add(_txtCpuLoad);
            cpuStack.Children.Add(_pbCpuLoad);
            cpuStack.Children.Add(_txtCpuStats);
            cpuCard.Child = cpuStack;
            Grid.SetRow(cpuCard, 0);
            Grid.SetColumn(cpuCard, 0);
            grid.Children.Add(cpuCard);

            // 2. RAM Card
            Border ramCard = CreateCard();
            ramCard.Margin = new Thickness(8, 0, 0, 16);
            StackPanel ramStack = new StackPanel();

            _lblRamTitle = CreateCardHeader("🧠 " + T("PCAssist_Ram_Title", "Memory (RAM)"));
            _txtRamLoad = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Text = "0.0 GB (0%)" };
            _pbRamLoad = CreateMetricProgressBar();
            _txtRamStats = new TextBlock { FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 8, 0, 0) };

            ramStack.Children.Add(_lblRamTitle);
            ramStack.Children.Add(_txtRamLoad);
            ramStack.Children.Add(_pbRamLoad);
            ramStack.Children.Add(_txtRamStats);
            ramCard.Child = ramStack;
            Grid.SetRow(ramCard, 0);
            Grid.SetColumn(ramCard, 1);
            grid.Children.Add(ramCard);

            // 3. GPU Card
            Border gpuCard = CreateCard();
            gpuCard.Margin = new Thickness(0, 0, 8, 0);
            StackPanel gpuStack = new StackPanel();

            _lblGpuTitle = CreateCardHeader("🎮 " + T("PCAssist_Gpu_Title", "Graphics (GPU)"));
            _txtGpuName = new TextBlock { FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 2, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis };
            _txtGpuLoad = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Text = "0.0%" };
            _pbGpuLoad = CreateMetricProgressBar();
            _txtGpuStats = new TextBlock { FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569"), Margin = new Thickness(0, 8, 0, 0) };

            gpuStack.Children.Add(_lblGpuTitle);
            gpuStack.Children.Add(_txtGpuName);
            gpuStack.Children.Add(_txtGpuLoad);
            gpuStack.Children.Add(_pbGpuLoad);
            gpuStack.Children.Add(_txtGpuStats);
            gpuCard.Child = gpuStack;
            Grid.SetRow(gpuCard, 1);
            Grid.SetColumn(gpuCard, 0);
            grid.Children.Add(gpuCard);

            // 4. Storage Card
            Border storageCard = CreateCard();
            storageCard.Margin = new Thickness(8, 0, 0, 0);
            StackPanel storageStack = new StackPanel();

            _lblStorageTitle = CreateCardHeader("💾 " + T("PCAssist_Storage_Title", "Storage & Drives"));
            _txtStorageIo = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A"), Margin = new Thickness(0, 4, 0, 4) };
            _txtSmartBadge = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("BrushSuccess", "#10B981"), Margin = new Thickness(0, 0, 0, 8) };
            _pnlPartitions = new StackPanel();

            storageStack.Children.Add(_lblStorageTitle);
            storageStack.Children.Add(_txtStorageIo);
            storageStack.Children.Add(_txtSmartBadge);
            storageStack.Children.Add(_pnlPartitions);
            storageCard.Child = storageStack;
            Grid.SetRow(storageCard, 1);
            Grid.SetColumn(storageCard, 1);
            grid.Children.Add(storageCard);

            rootPanel.Children.Add(grid);
            sv.Content = rootPanel;
            return sv;
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            if (_isMonitoringPaused || _currentTab != PCAssistTab.Monitor) return;
            UpdateMonitorSnapshot();
        }

        private void UpdateMonitorSnapshot()
        {
            Task.Run(() =>
            {
                try
                {
                    var snap = _monitorService.CaptureSnapshot();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RenderSnapshot(snap);
                    });
                }
                catch { }
            });
        }

        private void RenderSnapshot(SystemSnapshot snap)
        {
            // CPU
            _txtCpuName.Text = string.IsNullOrEmpty(snap.Cpu.Name) ? "Generic x64 Processor" : snap.Cpu.Name;
            _txtCpuLoad.Text = string.Format("{0:F1}%", snap.Cpu.UsagePercent);
            _pbCpuLoad.Value = snap.Cpu.UsagePercent;
            string tempStr = snap.Cpu.TemperatureC.HasValue ? string.Format("{0:F1} °C", snap.Cpu.TemperatureC.Value) : "N/A";
            string clockStr = snap.Cpu.ClockGhz > 0 ? string.Format("{0:F2} GHz", snap.Cpu.ClockGhz) : "Dynamic";
            _txtCpuStats.Text = string.Format("Clock: {0}  |  Temp: {1}  |  {2} Cores / {3} Threads", clockStr, tempStr, snap.Cpu.Cores, snap.Cpu.Threads);

            // RAM
            _txtRamLoad.Text = string.Format("{0:F1} GB / {1:F1} GB ({2:F0}%)", snap.Ram.UsedGb, snap.Ram.TotalGb, snap.Ram.UsagePercent);
            _pbRamLoad.Value = snap.Ram.UsagePercent;
            string slotsText = snap.Ram.SlotsCount > 1 ? string.Format("{0} slots (Dual-Channel)", snap.Ram.SlotsCount) : string.Format("{0} slot", snap.Ram.SlotsCount);
            string speedText = snap.Ram.SpeedMhz > 0 ? string.Format("{0} MHz", snap.Ram.SpeedMhz) : "";
            _txtRamStats.Text = string.Format("Free: {0:F1} GB  |  {1}  {2}", snap.Ram.FreeGb, speedText, slotsText).Trim();

            // GPU
            _txtGpuName.Text = string.IsNullOrEmpty(snap.Gpu.Name) ? "Integrated / Basic Display Adapter" : snap.Gpu.Name;
            _txtGpuLoad.Text = string.Format("{0:F1}%", snap.Gpu.UsagePercent);
            _pbGpuLoad.Value = snap.Gpu.UsagePercent;
            string vramText = snap.Gpu.DedicatedVramMb > 0 ? string.Format("{0} MB VRAM", snap.Gpu.DedicatedVramMb) : "Shared Memory";
            _txtGpuStats.Text = string.Format("Driver: {0}  |  {1}", snap.Gpu.DriverVersion, vramText);

            // Storage
            _txtStorageIo.Text = string.Format("I/O: Read {0:F2} MB/s  |  Write {1:F2} MB/s", snap.Storage.ReadRateMbSec, snap.Storage.WriteRateMbSec);
            if (snap.Storage.SmartHealthy)
            {
                _txtSmartBadge.Text = "🛡️ SMART Status: Healthy (OK)";
                _txtSmartBadge.Foreground = GetResourceBrush("BrushSuccess", "#10B981");
            }
            else
            {
                _txtSmartBadge.Text = "⚠️ SMART Warning: Failure Predicted!";
                _txtSmartBadge.Foreground = GetResourceBrush("BrushDanger", "#EF4444");
            }

            _pnlPartitions.Children.Clear();
            foreach (var part in snap.Storage.Partitions)
            {
                StackPanel partStack = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
                Grid partHeader = new Grid();
                partHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                partHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock txtPartName = new TextBlock
                {
                    Text = string.Format("{0} ({1})", part.Root, part.VolumeLabel),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A")
                };
                TextBlock txtPartFree = new TextBlock
                {
                    Text = string.Format("{0:F1} GB free / {1:F1} GB ({2:F0}%)", part.FreeGb, part.TotalGb, part.UsagePercent),
                    FontSize = 11,
                    Foreground = GetResourceBrush("BrushTextSecondary", "#475569")
                };
                Grid.SetColumn(txtPartName, 0);
                Grid.SetColumn(txtPartFree, 1);
                partHeader.Children.Add(txtPartName);
                partHeader.Children.Add(txtPartFree);

                ProgressBar pbPart = new ProgressBar
                {
                    Height = 6,
                    Minimum = 0,
                    Maximum = 100,
                    Value = part.UsagePercent,
                    Margin = new Thickness(0, 3, 0, 0),
                    Foreground = (part.UsagePercent > 90) ? GetResourceBrush("BrushDanger", "#EF4444") : GetResourceBrush("BrushAccent", "#0EA5E9")
                };

                partStack.Children.Add(partHeader);
                partStack.Children.Add(pbPart);
                _pnlPartitions.Children.Add(partStack);
            }
        }
        #endregion

        #region TAB 2: Battery & Power View
        private ScrollViewer BuildBatteryTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            // 1. Desktop Card (Shown when no battery detected)
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

            // 2. Notebook Battery Diagnostics Card
            _cardBatteryNotebook = CreateCard();
            _cardBatteryNotebook.Visibility = Visibility.Visible;
            StackPanel nbStack = new StackPanel();

            TextBlock lblNbHeader = CreateCardHeader("🔋 " + T("PCAssist_Battery_Title", "Battery Diagnostics"));
            nbStack.Children.Add(lblNbHeader);

            // Health Big Stat Box
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

            // Detailed Key-Value Grid
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

            // Action Buttons
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
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock { Text = label, FontSize = 12, Foreground = GetResourceBrush("BrushTextSecondary", "#475569") };
            TextBlock val = new TextBlock { Text = value, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("BrushTextPrimary", "#0F172A") };

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

        #region TAB 3: Bench Diagnostics View
        private ScrollViewer BuildBenchTab()
        {
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            StackPanel rootPanel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };

            // Bench Cancel Toolbar
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
            if (_txtTitle != null) _txtTitle.Text = "⚡ " + T("Nav_PCAssist", "PC Assist");
            if (_txtSubtitle != null) _txtSubtitle.Text = T("PCAssist_Desc", "Hardware Monitor, Battery Health & Quick Bench Diagnostics");

            if (_tabMonitorText != null) _tabMonitorText.Text = "📊 " + T("PCAssist_Tab_Monitor", "Hardware Monitor");
            if (_tabBatteryText != null) _tabBatteryText.Text = "🔋 " + T("PCAssist_Tab_Battery", "Battery & Power");
            if (_tabBenchText != null) _tabBenchText.Text = "🚀 " + T("PCAssist_Tab_Bench", "Bench Diagnostics");

            if (_lblCpuTitle != null) _lblCpuTitle.Text = "🖥️ " + T("PCAssist_Cpu_Title", "Processor (CPU)");
            if (_lblRamTitle != null) _lblRamTitle.Text = "🧠 " + T("PCAssist_Ram_Title", "Memory (RAM)");
            if (_lblGpuTitle != null) _lblGpuTitle.Text = "🎮 " + T("PCAssist_Gpu_Title", "Graphics (GPU)");
            if (_lblStorageTitle != null) _lblStorageTitle.Text = "💾 " + T("PCAssist_Storage_Title", "Storage & Drives");

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
