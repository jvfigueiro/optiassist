using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;

namespace OptiAssist.Addons.StartupManager
{
    public class StartupManagerAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private FrameworkElement _rootView;

        private enum StartupTab { Programs, Tasks }
        private StartupTab _activeTab = StartupTab.Programs;

        // Cached datasets
        private List<StartupProgramItem> _allPrograms = new List<StartupProgramItem>();
        private List<StartupTaskItem> _allTasks = new List<StartupTaskItem>();

        // UI Header controls
        private TextBlock _txtHeaderTitle;
        private TextBlock _txtHeaderSubtitle;
        private Button _btnNewProgram;
        private Button _btnRefresh;
        private TextBlock _txtSearchWatermark;
        private TextBox _txtSearchFilter;
        private CheckBox _chkHideMicrosoft;
        private bool _suppressHideMsEvent = false;

        // Tabs Controls
        private Border _tabProgramsBorder;
        private TextBlock _tabProgramsText;
        private Border _tabTasksBorder;
        private TextBlock _tabTasksText;

        // Container & Stats
        private TextBlock _txtStatsSummary;
        private StackPanel _pnlItemsList;

        // Modal Add/Edit
        private Grid _modalOverlay;
        private TextBlock _txtModalTitle;
        private TextBox _txtModalName;
        private TextBlock _lblPath;
        private TextBox _txtModalPath;
        private TextBlock _lblModalArgs;
        private TextBox _txtModalArgs;
        private TextBlock _lblScope;
        private ComboBox _cmbModalScope;
        private Button _btnModalBrowse;
        private Button _btnModalRunTask;
        private Button _btnModalExplorer;
        private Button _btnModalDelete;
        private Button _btnModalSave;
        private Button _btnModalCancel;
        private StartupProgramItem _editingProgram = null;
        private StartupTaskItem _editingTask = null;

        public string Id { get { return "startup-manager"; } }
        public string Name { get { return T("Nav_StartupManager", "Startup Manager"); } }
        public string Description { get { return T("Startup_Subtitle", "Monitor, enable, disable, or remove startup applications and scheduled tasks"); } }
        public string Version { get { return "1.2.S4"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "🚀"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
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
            if (_host != null) _host.Log(msg);
        }

        #region LOCALIZATION & FORMATTING HELPERS

        private string FormatLocation(StartupLocationType type)
        {
            switch (type)
            {
                case StartupLocationType.RegistryCurrentUser:
                    return T("Startup_Scope_User", "Current User");
                case StartupLocationType.RegistryLocalMachine:
                    return T("Startup_Scope_Machine", "All Users");
                case StartupLocationType.RegistryLocalMachine32:
                    return T("Startup_Scope_Machine32", "All Users 32-bit");
                case StartupLocationType.StartupFolderUser:
                    return T("Startup_Scope_StartupFolderUser", "Startup Folder (User)");
                case StartupLocationType.StartupFolderCommon:
                    return T("Startup_Scope_StartupFolderCommon", "Startup Folder (Common)");
                default:
                    return T("Startup_Scope_Unknown", "Unknown Location");
            }
        }

        private string FormatStatus(bool isEnabled)
        {
            return isEnabled ? T("Startup_Status_Enabled", "Enabled") : T("Startup_Status_Disabled", "Disabled");
        }

        private string FormatTrigger(StartupTaskItem task)
        {
            if (task == null) return "";
            if (string.Equals(task.TriggerType, "Boot", StringComparison.OrdinalIgnoreCase))
                return T("Startup_Trigger_Boot", "At System Boot");
            if (string.Equals(task.TriggerType, "Logon", StringComparison.OrdinalIgnoreCase))
                return T("Startup_Trigger_Logon", "At User Logon");
            if (string.Equals(task.TriggerType, "StartupOrLogon", StringComparison.OrdinalIgnoreCase))
                return T("Startup_Trigger_StartupOrLogon", "At Startup / Logon");
            return string.IsNullOrEmpty(task.TriggerDisplay) ? T("Startup_Trigger_StartupOrLogon", "At Startup / Logon") : task.TriggerDisplay;
        }

        #endregion

        public FrameworkElement CreateView()
        {
            if (_rootView != null) return _rootView;

            Grid root = new Grid();

            StackPanel mainStack = new StackPanel();

            // 1. Header (Title + Subtitle in visual harmony with AppManager & MaintenanceReport)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _txtHeaderTitle = new TextBlock
            {
                Text = string.Format("{0}  {1}", Icon, Name),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtHeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtHeaderSubtitle = new TextBlock
            {
                Text = T("Startup_Subtitle", "Monitor, enable, disable, or remove startup applications and scheduled tasks"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtHeaderSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            titlePanel.Children.Add(_txtHeaderTitle);
            titlePanel.Children.Add(_txtHeaderSubtitle);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            // Right header buttons
            StackPanel topActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _btnNewProgram = new Button
            {
                Content = T("Startup_Btn_New", "➕ Add Program"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _btnNewProgram.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnNewProgram.Click += (s, e) => OpenAddProgramModal();
            topActions.Children.Add(_btnNewProgram);

            _btnRefresh = new Button
            {
                Content = T("Startup_Btn_Refresh", "🔄 Refresh"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            _btnRefresh.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnRefresh.Click += (s, e) => ReloadActiveTabData();
            topActions.Children.Add(_btnRefresh);

            Grid.SetColumn(topActions, 1);
            headerGrid.Children.Add(topActions);
            mainStack.Children.Add(headerGrid);

            // 2. Navigation Tabs Bar
            Border tabStrip = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 14)
            };
            tabStrip.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel spTabs = new StackPanel { Orientation = Orientation.Horizontal };
            _tabProgramsBorder = CreateTabButton("🚀 " + T("Startup_Tab_Programs", "Startup Programs"), () => SwitchTab(StartupTab.Programs), out _tabProgramsText);
            _tabTasksBorder = CreateTabButton("⏰ " + T("Startup_Tab_Tasks", "Scheduled Tasks"), () => SwitchTab(StartupTab.Tasks), out _tabTasksText);

            spTabs.Children.Add(_tabProgramsBorder);
            spTabs.Children.Add(_tabTasksBorder);
            tabStrip.Child = spTabs;
            mainStack.Children.Add(tabStrip);

            // 3. Search Bar and Filter Controls
            Border filterCard = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            filterCard.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
            filterCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid filterGrid = new Grid();
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid searchInputGrid = new Grid();
            _txtSearchFilter = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _txtSearchFilter.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtSearchFilter.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtSearchFilter.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
            _txtSearchFilter.ToolTip = T("Startup_Filter_Placeholder", "🔍 Type here to search for the application, publisher, or command you want...");

            _txtSearchWatermark = new TextBlock
            {
                Text = T("Startup_Filter_Placeholder", "🔍 Type here to search for the application, publisher, or command you want..."),
                FontSize = 11,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _txtSearchWatermark.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");

            _txtSearchFilter.TextChanged += (s, e) =>
            {
                if (_txtSearchWatermark != null)
                {
                    _txtSearchWatermark.Visibility = string.IsNullOrEmpty(_txtSearchFilter.Text) ? Visibility.Visible : Visibility.Collapsed;
                }
                RenderCurrentTabList();
            };

            searchInputGrid.Children.Add(_txtSearchFilter);
            searchInputGrid.Children.Add(_txtSearchWatermark);
            Grid.SetColumn(searchInputGrid, 0);
            filterGrid.Children.Add(searchInputGrid);

            _chkHideMicrosoft = new CheckBox
            {
                Content = T("Startup_Chk_HideMicrosoft", "Hide native Microsoft / Windows entries"),
                FontSize = 11,
                IsChecked = true,
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            _chkHideMicrosoft.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            _chkHideMicrosoft.Checked += (s, e) =>
            {
                if (_suppressHideMsEvent) return;
                ReloadActiveTabData();
            };
            _chkHideMicrosoft.Unchecked += (s, e) =>
            {
                if (_suppressHideMsEvent) return;
                MessageBoxResult res = MessageBox.Show(
                    T("Startup_Msg_ShowMicrosoftWarning", "⚠️ Warning: Showing native Microsoft and Windows scheduled tasks will expose critical operating system tasks.\n\nDisabling, modifying, or deleting native Windows scheduled tasks can cause severe system instability, break Windows features, or prevent Windows from functioning properly.\n\nAre you sure you want to display native Microsoft / Windows entries?"),
                    T("Startup_Title_ShowMicrosoftWarning", "System Safety Warning"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (res != MessageBoxResult.Yes)
                {
                    _suppressHideMsEvent = true;
                    _chkHideMicrosoft.IsChecked = true;
                    _suppressHideMsEvent = false;
                    return;
                }
                ReloadActiveTabData();
            };
            Grid.SetColumn(_chkHideMicrosoft, 1);
            filterGrid.Children.Add(_chkHideMicrosoft);

            filterCard.Child = filterGrid;
            mainStack.Children.Add(filterCard);

            // 4. Stats Summary Bar
            _txtStatsSummary = new TextBlock
            {
                Text = T("Startup_Stats_Loading", "Loading..."),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 0, 10)
            };
            _txtStatsSummary.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            mainStack.Children.Add(_txtStatsSummary);

            // 5. Items List Panel
            _pnlItemsList = new StackPanel();
            mainStack.Children.Add(_pnlItemsList);

            root.Children.Add(mainStack);

            // 6. Modal Overlay for Add / Edit Program
            CreateModalOverlay(root);

            _rootView = root;

            // Initial load
            SwitchTab(StartupTab.Programs);

            return _rootView;
        }

        private Border CreateTabButton(string label, Action onClick, out TextBlock textBlock)
        {
            Border b = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            textBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            b.Child = textBlock;

            b.MouseLeftButtonDown += (s, e) => onClick();
            return b;
        }

        private void SwitchTab(StartupTab tab)
        {
            _activeTab = tab;

            // Update Tab Highlights
            HighlightTab(_tabProgramsBorder, _tabProgramsText, tab == StartupTab.Programs);
            HighlightTab(_tabTasksBorder, _tabTasksText, tab == StartupTab.Tasks);

            // Add program button only relevant for Tab 1 (Programs)
            if (_btnNewProgram != null)
            {
                _btnNewProgram.Visibility = (tab == StartupTab.Programs) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Hide Microsoft checkbox only applies to Tasks
            if (_chkHideMicrosoft != null)
            {
                _chkHideMicrosoft.Visibility = (tab == StartupTab.Programs) ? Visibility.Collapsed : Visibility.Visible;
            }

            ReloadActiveTabData();
        }

        private void HighlightTab(Border b, TextBlock tb, bool active)
        {
            if (b == null || tb == null) return;
            if (active)
            {
                b.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
                tb.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            }
            else
            {
                b.BorderBrush = Brushes.Transparent;
                tb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            }
        }

        private void ReloadActiveTabData()
        {
            if (_pnlItemsList == null) return;
            _pnlItemsList.Children.Clear();

            TextBlock loadingTb = new TextBlock
            {
                Text = "⏳ " + T("Startup_Loading_Data", "Loading system startup data..."),
                FontSize = 12,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            loadingTb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _pnlItemsList.Children.Add(loadingTb);

            bool hideMs = (_chkHideMicrosoft != null && _chkHideMicrosoft.IsChecked == true);

            // Load on background thread
            ThreadPool.QueueUserWorkItem(state =>
            {
                if (_activeTab == StartupTab.Programs)
                {
                    _allPrograms = StartupEngine.GetAllStartupPrograms();
                }
                else if (_activeTab == StartupTab.Tasks)
                {
                    _allTasks = StartupEngine.GetStartupScheduledTasks(hideMs);
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    RenderCurrentTabList();
                }));
            });
        }

        private void RenderCurrentTabList()
        {
            if (_pnlItemsList == null) return;
            _pnlItemsList.Children.Clear();

            string query = (_txtSearchFilter != null) ? _txtSearchFilter.Text.Trim().ToLowerInvariant() : "";

            if (_activeTab == StartupTab.Programs)
            {
                RenderProgramsList(query);
            }
            else if (_activeTab == StartupTab.Tasks)
            {
                RenderTasksList(query);
            }
        }

        #region TAB 1: PROGRAMS RENDERING

        private void RenderProgramsList(string query)
        {
            int total = _allPrograms.Count;
            int enabledCount = 0;
            int disabledCount = 0;
            foreach (var p in _allPrograms)
            {
                if (p.IsEnabled) enabledCount++;
                else disabledCount++;
            }

            if (_txtStatsSummary != null)
            {
                _txtStatsSummary.Text = string.Format(
                    T("Startup_Stats_Programs", "Total: {0} programs • {1} enabled • {2} disabled"),
                    total, enabledCount, disabledCount);
            }

            List<StartupProgramItem> filtered = new List<StartupProgramItem>();
            foreach (var p in _allPrograms)
            {
                if (string.IsNullOrEmpty(query) ||
                    (p.Name != null && p.Name.ToLowerInvariant().Contains(query)) ||
                    (p.Publisher != null && p.Publisher.ToLowerInvariant().Contains(query)) ||
                    (p.FileDescription != null && p.FileDescription.ToLowerInvariant().Contains(query)) ||
                    (p.Command != null && p.Command.ToLowerInvariant().Contains(query)))
                {
                    filtered.Add(p);
                }
            }

            if (filtered.Count == 0)
            {
                _pnlItemsList.Children.Add(CreateEmptyPlaceholder(T("Startup_Empty_Programs", "No startup programs found matching current filter.")));
                return;
            }

            foreach (var prog in filtered)
            {
                _pnlItemsList.Children.Add(BuildProgramCard(prog));
            }
        }

        private Border BuildProgramCard(StartupProgramItem prog)
        {
            Border card = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Name, Publisher, Location Badge, Missing File Badge, Command
            StackPanel spLeft = new StackPanel();

            StackPanel spTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            string displayTitle = !string.IsNullOrWhiteSpace(prog.FileDescription) ? prog.FileDescription : prog.Name;
            TextBlock tbName = new TextBlock
            {
                Text = displayTitle,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tbName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTitleRow.Children.Add(tbName);

            // Location Badge
            Border bLocation = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            bLocation.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            TextBlock tbLoc = new TextBlock
            {
                Text = FormatLocation(prog.LocationType),
                FontSize = 9.5
            };
            tbLoc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            bLocation.Child = tbLoc;
            spTitleRow.Children.Add(bLocation);

            // Missing File Warning Badge
            if (!prog.FileExists && !string.IsNullOrEmpty(prog.ExePath))
            {
                Border bMissing = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = T("Startup_Tooltip_Missing", "⚠️ The target executable was not found at the specified path")
                };
                TextBlock tbMissing = new TextBlock
                {
                    Text = T("Startup_Badge_NotFound", "⚠️ Not found"),
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
                };
                bMissing.Child = tbMissing;
                spTitleRow.Children.Add(bMissing);
            }

            spLeft.Children.Add(spTitleRow);

            // Entry Name (left of Publisher), Publisher & Size Row
            string pub = !string.IsNullOrWhiteSpace(prog.Publisher) ? prog.Publisher : T("Startup_Publisher_Unknown", "Unknown Publisher");
            string size = !string.IsNullOrWhiteSpace(prog.FileSizeDisplay) ? prog.FileSizeDisplay : "-";
            StringBuilder sbPub = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(prog.Name))
            {
                sbPub.Append(prog.Name).Append(" • ");
            }
            sbPub.Append(T("Startup_Label_Publisher", "Publisher:")).Append(" ").Append(pub);
            sbPub.Append(" • ").Append(T("Startup_Label_Size", "Size:")).Append(" ").Append(size);

            TextBlock tbPub = new TextBlock
            {
                Text = sbPub.ToString(),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 2)
            };
            tbPub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spLeft.Children.Add(tbPub);

            // Command / Path Row
            TextBlock tbCmd = new TextBlock
            {
                Text = prog.Command,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = prog.Command
            };
            tbCmd.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            spLeft.Children.Add(tbCmd);

            Grid.SetColumn(spLeft, 0);
            g.Children.Add(spLeft);

            // Right: Status Badge + Action Buttons (Enable/Disable and Edit only)
            StackPanel spRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Status Pill
            Border bStatus = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = prog.IsEnabled
                    ? new SolidColorBrush(Color.FromArgb(35, 34, 197, 94))
                    : new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
            };
            TextBlock tbStatus = new TextBlock
            {
                Text = FormatStatus(prog.IsEnabled),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = prog.IsEnabled
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            bStatus.Child = tbStatus;
            spRight.Children.Add(bStatus);

            // Toggle Button
            Button btnToggle = new Button
            {
                Content = prog.IsEnabled ? T("Startup_Btn_Disable", "Disable") : T("Startup_Btn_Enable", "Enable"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnToggle.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnToggle.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleStartupProgram(prog, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_ToggleFailed", "Could not toggle startup entry: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnToggle);

            // Delete Button (between Toggle and Edit)
            Button btnDelete = new Button
            {
                Content = T("Startup_Btn_Delete", "🗑️ Delete"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDelete.Click += (s, e) => ConfirmAndDeleteProgram(prog);
            spRight.Children.Add(btnDelete);

            // Edit Button
            Button btnEdit = new Button
            {
                Content = T("Startup_Btn_Edit", "✏️ Edit"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditProgramModal(prog);
            spRight.Children.Add(btnEdit);

            Grid.SetColumn(spRight, 1);
            g.Children.Add(spRight);

            card.Child = g;
            return card;
        }

        #endregion

        #region TAB 2: SCHEDULED TASKS RENDERING

        private void RenderTasksList(string query)
        {
            int total = _allTasks.Count;
            int enabledCount = 0;
            int disabledCount = 0;
            foreach (var t in _allTasks)
            {
                if (t.IsEnabled) enabledCount++;
                else disabledCount++;
            }

            if (_txtStatsSummary != null)
            {
                _txtStatsSummary.Text = string.Format(
                    T("Startup_Stats_Tasks", "Total: {0} startup tasks • {1} enabled • {2} disabled"),
                    total, enabledCount, disabledCount);
            }

            List<StartupTaskItem> filtered = new List<StartupTaskItem>();
            foreach (var t in _allTasks)
            {
                if (string.IsNullOrEmpty(query) ||
                    (t.Name != null && t.Name.ToLowerInvariant().Contains(query)) ||
                    (t.Publisher != null && t.Publisher.ToLowerInvariant().Contains(query)) ||
                    (t.FileDescription != null && t.FileDescription.ToLowerInvariant().Contains(query)) ||
                    (t.ActionPath != null && t.ActionPath.ToLowerInvariant().Contains(query)))
                {
                    filtered.Add(t);
                }
            }

            if (filtered.Count == 0)
            {
                _pnlItemsList.Children.Add(CreateEmptyPlaceholder(T("Startup_Empty_Tasks", "No user-installed scheduled tasks found matching current filter.")));
                return;
            }

            foreach (var task in filtered)
            {
                _pnlItemsList.Children.Add(BuildTaskCard(task));
            }
        }

        private Border BuildTaskCard(StartupTaskItem task)
        {
            Border card = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel spLeft = new StackPanel();

            StackPanel spTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock tbName = new TextBlock
            {
                Text = task.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tbName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTitleRow.Children.Add(tbName);

            // Trigger Badge
            Border bTrigger = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            bTrigger.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            TextBlock tbTrigger = new TextBlock
            {
                Text = FormatTrigger(task),
                FontSize = 9.5
            };
            tbTrigger.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            bTrigger.Child = tbTrigger;
            spTitleRow.Children.Add(bTrigger);

            spLeft.Children.Add(spTitleRow);

            // Author / Publisher, Executable Description & Task Path
            string pub = !string.IsNullOrWhiteSpace(task.Publisher) ? task.Publisher : T("Startup_Publisher_Unknown", "Unknown Publisher");
            StringBuilder sbPub = new StringBuilder();
            sbPub.Append(T("Startup_Label_Publisher", "Publisher:")).Append(" ").Append(pub);
            if (!string.IsNullOrWhiteSpace(task.FileDescription))
            {
                sbPub.Append(" • ").Append(T("Startup_Label_Description", "Description:")).Append(" ").Append(task.FileDescription);
            }
            sbPub.Append(" • ").Append(T("Startup_Label_TaskPath", "Task Path:")).Append(" ").Append(task.FullTaskPath);

            TextBlock tbPub = new TextBlock
            {
                Text = sbPub.ToString(),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 2)
            };
            tbPub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spLeft.Children.Add(tbPub);

            // Action / Command
            TextBlock tbCmd = new TextBlock
            {
                Text = !string.IsNullOrEmpty(task.ActionPath) ? (task.ActionPath + " " + task.Arguments).Trim() : "—",
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = task.ActionPath
            };
            tbCmd.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            spLeft.Children.Add(tbCmd);

            Grid.SetColumn(spLeft, 0);
            g.Children.Add(spLeft);

            // Right: Status Badge + Action Buttons (Enable/Disable and Edit only)
            StackPanel spRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Status Pill
            Border bStatus = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = task.IsEnabled
                    ? new SolidColorBrush(Color.FromArgb(35, 34, 197, 94))
                    : new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
            };
            TextBlock tbStatus = new TextBlock
            {
                Text = FormatStatus(task.IsEnabled),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = task.IsEnabled
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            bStatus.Child = tbStatus;
            spRight.Children.Add(bStatus);

            // Toggle Button
            Button btnToggle = new Button
            {
                Content = task.IsEnabled ? T("Startup_Btn_Disable", "Disable") : T("Startup_Btn_Enable", "Enable"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnToggle.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnToggle.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleTask(task, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_ToggleFailed", "Could not toggle scheduled task: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnToggle);

            // Delete Button (between Toggle and Edit)
            Button btnDelete = new Button
            {
                Content = T("Startup_Btn_Delete", "🗑️ Delete"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDelete.Click += (s, e) => ConfirmAndDeleteTask(task);
            spRight.Children.Add(btnDelete);

            // Edit Button
            Button btnEdit = new Button
            {
                Content = T("Startup_Btn_Edit", "✏️ Edit"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditTaskModal(task);
            spRight.Children.Add(btnEdit);

            Grid.SetColumn(spRight, 1);
            g.Children.Add(spRight);

            card.Child = g;
            return card;
        }

        #endregion

        private Border CreateEmptyPlaceholder(string message)
        {
            Border b = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 12, 0, 0)
            };
            b.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
            b.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            TextBlock tb = new TextBlock
            {
                Text = message,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            b.Child = tb;
            return b;
        }

        #region MODAL ADD / EDIT (PROGRAM & TASK)

        private void CreateModalOverlay(Grid root)
        {
            _modalOverlay = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))
            };

            Border modalCard = new Border
            {
                Width = 540,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            modalCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            modalCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel modalStack = new StackPanel();

            _txtModalTitle = new TextBlock
            {
                Text = T("Startup_Dialog_New_Title", "Add New Program to Startup"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _txtModalTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(_txtModalTitle);

            // Field 1: Name
            TextBlock lblName = new TextBlock
            {
                Text = T("Startup_Field_Name", "Application Name * :"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(lblName);

            _txtModalName = new TextBox
            {
                Height = 30,
                FontSize = 11.5,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 12),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _txtModalName.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtModalName.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtModalName.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
            modalStack.Children.Add(_txtModalName);

            // Field 2: Command / Path
            _lblPath = new TextBlock
            {
                Text = T("Startup_Field_Path", "Executable Path / Command * :"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _lblPath.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(_lblPath);

            Grid pathGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtModalPath = new TextBox
            {
                Height = 30,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _txtModalPath.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtModalPath.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtModalPath.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
            Grid.SetColumn(_txtModalPath, 0);
            pathGrid.Children.Add(_txtModalPath);

            _btnModalBrowse = new Button
            {
                Content = T("Startup_Btn_Browse", "Browse..."),
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnModalBrowse.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalBrowse.Click += (s, e) =>
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = T("Startup_Filter_Executables", "Executable Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All Files (*.*)|*.*"),
                    Title = T("Startup_Dialog_Browse_Title", "Select Executable for Startup")
                };
                if (ofd.ShowDialog() == true)
                {
                    if (_editingTask != null)
                    {
                        _txtModalPath.Text = ofd.FileName;
                    }
                    else
                    {
                        _txtModalPath.Text = string.Format("\"{0}\"", ofd.FileName);
                        if (string.IsNullOrWhiteSpace(_txtModalName.Text))
                        {
                            _txtModalName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                        }
                    }
                }
            };
            Grid.SetColumn(_btnModalBrowse, 1);
            pathGrid.Children.Add(_btnModalBrowse);
            modalStack.Children.Add(pathGrid);

            // Field 2.5: Optional Arguments (for Scheduled Tasks)
            _lblModalArgs = new TextBlock
            {
                Text = T("Startup_Field_Args", "Arguments / Parameters (optional):"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed
            };
            _lblModalArgs.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(_lblModalArgs);

            _txtModalArgs = new TextBox
            {
                Height = 30,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 12),
                VerticalContentAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _txtModalArgs.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtModalArgs.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtModalArgs.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
            modalStack.Children.Add(_txtModalArgs);

            // Field 3: Scope ComboBox (for Startup Programs)
            _lblScope = new TextBlock
            {
                Text = T("Startup_Field_Scope", "Startup Scope * :"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _lblScope.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(_lblScope);

            _cmbModalScope = new ComboBox
            {
                Height = 30,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _cmbModalScope.Items.Add(new ComboBoxItem { Content = T("Startup_Scope_User", "Current User"), Tag = StartupLocationType.RegistryCurrentUser });
            _cmbModalScope.Items.Add(new ComboBoxItem { Content = T("Startup_Scope_Machine", "All Users"), Tag = StartupLocationType.RegistryLocalMachine });
            _cmbModalScope.SelectedIndex = 0;
            modalStack.Children.Add(_cmbModalScope);

            // Modal Action Buttons (Bottom Bar: Left = Delete, Folder, Run / Right = Cancel, Save)
            Grid modalBtnsGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            modalBtnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modalBtnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel leftBtns = new StackPanel { Orientation = Orientation.Horizontal };

            _btnModalDelete = new Button
            {
                Content = T("Startup_Btn_Delete", "🗑️ Delete"),
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _btnModalDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalDelete.Click += (s, e) => HandleModalDelete();
            leftBtns.Children.Add(_btnModalDelete);

            _btnModalExplorer = new Button
            {
                Content = "📂 " + T("Startup_Btn_OpenFolder", "Open Folder"),
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _btnModalExplorer.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalExplorer.Click += (s, e) => HandleModalExplorer();
            leftBtns.Children.Add(_btnModalExplorer);

            _btnModalRunTask = new Button
            {
                Content = T("Startup_Btn_Run", "▶️ Run Now"),
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _btnModalRunTask.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalRunTask.Click += (s, e) => HandleModalRunTask();
            leftBtns.Children.Add(_btnModalRunTask);

            Grid.SetColumn(leftBtns, 0);
            modalBtnsGrid.Children.Add(leftBtns);

            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal };

            _btnModalCancel = new Button
            {
                Content = T("Startup_Btn_Cancel", "Cancel"),
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _btnModalCancel.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalCancel.Click += (s, e) => CloseModal();
            rightBtns.Children.Add(_btnModalCancel);

            _btnModalSave = new Button
            {
                Content = T("Startup_Btn_Save", "Save"),
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0),
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
            _btnModalSave.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnModalSave.Click += (s, e) => SaveModal();
            rightBtns.Children.Add(_btnModalSave);

            Grid.SetColumn(rightBtns, 1);
            modalBtnsGrid.Children.Add(rightBtns);

            modalStack.Children.Add(modalBtnsGrid);
            modalCard.Child = modalStack;
            _modalOverlay.Children.Add(modalCard);
            root.Children.Add(_modalOverlay);
        }

        private void OpenAddProgramModal()
        {
            _editingProgram = null;
            _editingTask = null;
            if (_txtModalTitle != null) _txtModalTitle.Text = T("Startup_Dialog_New_Title", "Add New Program to Startup");
            if (_txtModalName != null) { _txtModalName.Text = ""; _txtModalName.IsReadOnly = false; }
            if (_lblPath != null) _lblPath.Text = T("Startup_Field_Path", "Executable Path / Command * :");
            if (_txtModalPath != null) _txtModalPath.Text = "";
            if (_lblModalArgs != null) _lblModalArgs.Visibility = Visibility.Collapsed;
            if (_txtModalArgs != null) _txtModalArgs.Visibility = Visibility.Collapsed;
            if (_lblScope != null) _lblScope.Visibility = Visibility.Visible;
            if (_cmbModalScope != null)
            {
                _cmbModalScope.Visibility = Visibility.Visible;
                _cmbModalScope.IsEnabled = true;
                _cmbModalScope.SelectedIndex = 0;
            }
            if (_btnModalDelete != null) _btnModalDelete.Visibility = Visibility.Collapsed;
            if (_btnModalExplorer != null) _btnModalExplorer.Visibility = Visibility.Collapsed;
            if (_btnModalRunTask != null) _btnModalRunTask.Visibility = Visibility.Collapsed;
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Visible;
            if (_txtModalName != null) _txtModalName.Focus();
        }

        private void OpenEditProgramModal(StartupProgramItem prog)
        {
            _editingProgram = prog;
            _editingTask = null;
            if (_txtModalTitle != null) _txtModalTitle.Text = T("Startup_Dialog_Edit_Title", "Edit Startup Entry");
            if (_txtModalName != null) { _txtModalName.Text = prog.Name; _txtModalName.IsReadOnly = false; }
            if (_lblPath != null) _lblPath.Text = T("Startup_Field_Path", "Executable Path / Command * :");
            if (_txtModalPath != null) _txtModalPath.Text = prog.Command;
            if (_lblModalArgs != null) _lblModalArgs.Visibility = Visibility.Collapsed;
            if (_txtModalArgs != null) _txtModalArgs.Visibility = Visibility.Collapsed;
            if (_lblScope != null) _lblScope.Visibility = Visibility.Visible;
            if (_cmbModalScope != null)
            {
                _cmbModalScope.Visibility = Visibility.Visible;
                _cmbModalScope.IsEnabled = false; // Keep existing scope when editing
                _cmbModalScope.SelectedIndex = (prog.LocationType == StartupLocationType.RegistryCurrentUser) ? 0 : 1;
            }
            if (_btnModalDelete != null) _btnModalDelete.Visibility = Visibility.Visible;
            if (_btnModalExplorer != null)
            {
                bool canExplore = (!string.IsNullOrEmpty(prog.ExePath) && File.Exists(prog.ExePath)) ||
                                  (!string.IsNullOrEmpty(prog.SourcePath) && File.Exists(prog.SourcePath));
                _btnModalExplorer.Visibility = canExplore ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_btnModalRunTask != null) _btnModalRunTask.Visibility = Visibility.Collapsed;
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Visible;
            if (_txtModalName != null) _txtModalName.Focus();
        }

        private void OpenEditTaskModal(StartupTaskItem task)
        {
            _editingProgram = null;
            _editingTask = task;
            if (_txtModalTitle != null) _txtModalTitle.Text = T("Startup_Dialog_EditTask_Title", "Edit Scheduled Task");
            if (_txtModalName != null) { _txtModalName.Text = task.Name; _txtModalName.IsReadOnly = true; }
            if (_lblPath != null) _lblPath.Text = T("Startup_Field_ActionPath", "Executable / Action Path * :");
            if (_txtModalPath != null) _txtModalPath.Text = task.ActionPath ?? "";
            if (_lblModalArgs != null) _lblModalArgs.Visibility = Visibility.Visible;
            if (_txtModalArgs != null)
            {
                _txtModalArgs.Visibility = Visibility.Visible;
                _txtModalArgs.Text = task.Arguments ?? "";
            }
            if (_lblScope != null) _lblScope.Visibility = Visibility.Collapsed;
            if (_cmbModalScope != null) _cmbModalScope.Visibility = Visibility.Collapsed;

            if (_btnModalDelete != null) _btnModalDelete.Visibility = Visibility.Visible;
            if (_btnModalExplorer != null)
            {
                string clean = StartupEngine.ExtractExePath(task.ActionPath);
                _btnModalExplorer.Visibility = (!string.IsNullOrEmpty(clean) && File.Exists(clean)) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_btnModalRunTask != null) _btnModalRunTask.Visibility = Visibility.Visible;

            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Visible;
            if (_txtModalPath != null) _txtModalPath.Focus();
        }

        private void CloseModal()
        {
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Collapsed;
            _editingProgram = null;
            _editingTask = null;
        }

        private void ConfirmAndDeleteProgram(StartupProgramItem prog)
        {
            if (prog == null) return;
            string msg = string.Format(
                T("Startup_Confirm_Delete_Msg", "Are you sure you want to permanently delete '{0}' from Windows startup?\n\nThis will not uninstall the application, but will prevent it from launching automatically."),
                prog.Name);

            if (MessageBox.Show(msg, T("Startup_Confirm_Delete_Title", "Confirm Startup Entry Deletion"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string err;
                if (!StartupEngine.DeleteStartupProgram(prog, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_DeleteFailed", "Could not delete startup entry: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    _allPrograms.Remove(prog);
                    RenderCurrentTabList();
                }
            }
        }

        private void ConfirmAndDeleteTask(StartupTaskItem task)
        {
            if (task == null) return;
            string msg = string.Format(
                T("Startup_Confirm_Delete_Msg", "Are you sure you want to permanently delete scheduled task '{0}'?"),
                task.Name);

            if (MessageBox.Show(msg, T("Startup_Confirm_Delete_Title", "Confirm Scheduled Task Deletion"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string err;
                if (!StartupEngine.DeleteTask(task, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_DeleteFailed", "Could not delete scheduled task: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    _allTasks.Remove(task);
                    RenderCurrentTabList();
                }
            }
        }

        private void HandleModalDelete()
        {
            if (_editingProgram != null)
            {
                var prog = _editingProgram;
                CloseModal();
                ConfirmAndDeleteProgram(prog);
            }
            else if (_editingTask != null)
            {
                var task = _editingTask;
                CloseModal();
                ConfirmAndDeleteTask(task);
            }
        }

        private void HandleModalExplorer()
        {
            try
            {
                if (_editingProgram != null)
                {
                    string target = _editingProgram.ExePath;
                    if (File.Exists(target))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", target));
                    }
                    else if (File.Exists(_editingProgram.SourcePath))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", _editingProgram.SourcePath));
                    }
                }
                else if (_editingTask != null)
                {
                    string clean = StartupEngine.ExtractExePath(_editingTask.ActionPath);
                    if (File.Exists(clean))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", clean));
                    }
                }
            }
            catch { }
        }

        private void HandleModalRunTask()
        {
            if (_editingTask != null)
            {
                string err;
                if (!StartupEngine.RunTask(_editingTask, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_TaskRunError", "Error running task: {0}"), err), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_TaskRunSuccess", "Task '{0}' started successfully."), _editingTask.Name), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void SaveModal()
        {
            if (_editingTask != null)
            {
                string newPath = _txtModalPath != null ? _txtModalPath.Text.Trim() : "";
                string newArgs = _txtModalArgs != null ? _txtModalArgs.Text.Trim() : "";

                if (string.IsNullOrEmpty(newPath))
                {
                    MessageBox.Show(T("Startup_Msg_PathRequired", "Please specify the action path for the scheduled task."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string err;
                if (!StartupEngine.UpdateTask(_editingTask, newPath, newArgs, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_TaskUpdateError", "Error updating scheduled task: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CloseModal();
                ReloadActiveTabData();
                return;
            }

            // Program save / add
            string name = _txtModalName != null ? _txtModalName.Text.Trim() : "";
            string path = _txtModalPath != null ? _txtModalPath.Text.Trim() : "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                MessageBox.Show(T("Startup_Msg_NameAndPathRequired", "Please specify both the Name and Path for the program."),
                    "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartupLocationType scope = StartupLocationType.RegistryCurrentUser;
            if (_editingProgram != null)
            {
                scope = _editingProgram.LocationType;
            }
            else
            {
                ComboBoxItem cbi = _cmbModalScope.SelectedItem as ComboBoxItem;
                if (cbi != null && cbi.Tag is StartupLocationType)
                {
                    scope = (StartupLocationType)cbi.Tag;
                }
            }

            string oldName = (_editingProgram != null) ? _editingProgram.Name : null;
            string errProg;
            if (!StartupEngine.AddOrUpdateStartupProgram(name, path, scope, oldName, out errProg))
            {
                MessageBox.Show(string.Format(T("Startup_Msg_SaveError", "Error saving startup program: {0}"), errProg), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CloseModal();
            ReloadActiveTabData();
        }

        #endregion

        public void OnLanguageChanged(string langCode)
        {
            if (_txtHeaderTitle != null) _txtHeaderTitle.Text = string.Format("{0}  {1}", Icon, Name);
            if (_txtHeaderSubtitle != null) _txtHeaderSubtitle.Text = T("Startup_Subtitle", "Monitor, enable, disable, or remove startup applications and scheduled tasks");
            if (_btnNewProgram != null) _btnNewProgram.Content = T("Startup_Btn_New", "➕ Add Program");
            if (_btnRefresh != null) _btnRefresh.Content = T("Startup_Btn_Refresh", "🔄 Refresh");
            if (_chkHideMicrosoft != null) _chkHideMicrosoft.Content = T("Startup_Chk_HideMicrosoft", "Hide native Microsoft / Windows entries");
            if (_tabProgramsText != null) _tabProgramsText.Text = "🚀 " + T("Startup_Tab_Programs", "Startup Programs");
            if (_tabTasksText != null) _tabTasksText.Text = "⏰ " + T("Startup_Tab_Tasks", "Scheduled Tasks");
            if (_txtSearchWatermark != null) _txtSearchWatermark.Text = T("Startup_Filter_Placeholder", "🔍 Type here to search for the application, publisher, or command you want...");
            if (_txtSearchFilter != null) _txtSearchFilter.ToolTip = T("Startup_Filter_Placeholder", "🔍 Type here to search for the application, publisher, or command you want...");
            if (_cmbModalScope != null && _cmbModalScope.Items.Count >= 2)
            {
                ComboBoxItem item0 = _cmbModalScope.Items[0] as ComboBoxItem;
                ComboBoxItem item1 = _cmbModalScope.Items[1] as ComboBoxItem;
                if (item0 != null) item0.Content = T("Startup_Scope_User", "Current User");
                if (item1 != null) item1.Content = T("Startup_Scope_Machine", "All Users");
            }
            if (_btnModalCancel != null) _btnModalCancel.Content = T("Startup_Btn_Cancel", "Cancel");
            if (_btnModalSave != null) _btnModalSave.Content = T("Startup_Btn_Save", "Save");
            if (_btnModalDelete != null) _btnModalDelete.Content = T("Startup_Btn_Delete", "🗑️ Delete");
            if (_btnModalRunTask != null) _btnModalRunTask.Content = T("Startup_Btn_Run", "▶️ Run Now");
            if (_btnModalBrowse != null) _btnModalBrowse.Content = T("Startup_Btn_Browse", "Browse...");

            ReloadActiveTabData();
        }
    }
}
