using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace OptiAssist.Addons.Toolbox
{
    /// <summary>
    /// Toolbox Add-on Module (Config\Addons\Toolbox\Toolbox.dll) allowing users to create, manage,
    /// categorize, and execute custom system commands (CMD, PowerShell, or standalone binaries)
    /// with real-time stdout/stderr streaming and exit-code tracking.
    /// </summary>
    public class ToolboxAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ToolboxStorageManager _storage;
        private ToolboxProcessRunner _runner;
        private List<ToolboxCommandItem> _commands;
        private ToolboxCommandItem _editingItem;

        // Top Header & Toolbar controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private TextBox _txtSearch;
        private Button _btnNewCommand;
        private Button _btnRestorePresets;
        private Button _btnOpenJson;

        // CRUD Form Editor controls
        private Border _borderEditorCard;
        private TextBlock _txtEditorHeader;
        private TextBlock _lblFieldTitle;
        private TextBox _txtFieldTitle;
        private TextBlock _lblFieldCategory;
        private ComboBox _cmbFieldCategory;
        private TextBlock _lblFieldShell;
        private ComboBox _cmbFieldShell;
        private TextBlock _lblFieldCommand;
        private TextBox _txtFieldCommand;
        private TextBlock _lblFieldDescription;
        private TextBox _txtFieldDescription;
        private TextBlock _lblFieldTimeout;
        private TextBox _txtFieldTimeout;
        private CheckBox _chkWaitForExit;
        private CheckBox _chkCaptureOutput;
        private CheckBox _chkRequiresAdmin;
        private Button _btnSaveCommand;
        private Button _btnCancelEdit;

        // Dynamic Categories Accordion host
        private StackPanel _spCategoriesHost;

        // Built-in Real-Time Console Viewer controls
        private TextBlock _txtConsoleTitle;
        private Border _borderStatusPill;
        private TextBlock _txtStatusPill;
        private Button _btnStopProcess;
        private Button _btnCopyConsole;
        private Button _btnClearConsole;
        private TextBox _txtConsoleOutput;

        public string Id { get { return "toolbox"; } }
        public string Name { get { return T("Nav_Toolbox", "Toolbox"); } }
        public string Description { get { return T("Toolbox_Subtitle", "Create, organize, and run custom CMD, PowerShell, and binary commands with live output streaming"); } }
        public string Version { get { return "1.0.0"; } }
        public string Author { get { return "OptiAssist"; } }
        public string Icon { get { return "🧰"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _storage = new ToolboxStorageManager();
            _runner = new ToolboxProcessRunner();
            _commands = _storage.LoadCommands();
        }

        private string T(string key, string defaultVal)
        {
            if (_host != null)
            {
                return _host.GetLocalizedString(key, defaultVal);
            }
            return defaultVal;
        }

        private void HostLog(string msg)
        {
            if (_host != null)
            {
                _host.Log(msg);
            }
        }

        public FrameworkElement CreateView()
        {
            if (_storage == null) _storage = new ToolboxStorageManager();
            if (_runner == null) _runner = new ToolboxProcessRunner();
            if (_commands == null) _commands = _storage.LoadCommands();

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Header & Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: CRUD Form Modal/Panel
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 2: Categories Accordion
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(195) }); // 3: Real-Time Console Panel

            // =====================================================================
            // ROW 0: HEADER & ACTION TOOLBAR
            // =====================================================================
            StackPanel spTop = new StackPanel() { Margin = new Thickness(0, 0, 0, 10) };

            _txtTitle = new TextBlock()
            {
                Text = T("Toolbox_Title", "🧰 Custom Command Toolbox"),
                FontSize = 22,
                FontWeight = FontWeights.Bold
            };
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTop.Children.Add(_txtTitle);

            _txtSubtitle = new TextBlock()
            {
                Text = T("Toolbox_Subtitle", "Create, organize, and run custom CMD, PowerShell, and binary commands with live output streaming"),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spTop.Children.Add(_txtSubtitle);

            Border toolbarCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            toolbarCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            toolbarCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            // Search box on left
            StackPanel spSearch = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock lblSearchIcon = new TextBlock()
            {
                Text = "🔎",
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            spSearch.Children.Add(lblSearchIcon);

            _txtSearch = new TextBox()
            {
                Width = 260,
                Height = 28,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                ToolTip = T("Toolbox_Search_Tooltip", "Filter commands by title, category, or command text")
            };
            _txtSearch.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtSearch.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtSearch.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            _txtSearch.TextChanged += (s, e) => RebuildCategoriesAccordion();
            spSearch.Children.Add(_txtSearch);
            Grid.SetColumn(spSearch, 0);
            toolbarGrid.Children.Add(spSearch);

            // Action buttons on right
            StackPanel spToolbarButtons = new StackPanel() { Orientation = Orientation.Horizontal };

            _btnNewCommand = new Button()
            {
                Content = T("Btn_ToolboxNew", "➕ New Command"),
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnNewCommand.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnNewCommand.Click += (s, e) => OpenEditorForNew();
            spToolbarButtons.Children.Add(_btnNewCommand);

            _btnRestorePresets = new Button()
            {
                Content = T("Btn_ToolboxRestorePresets", "🔄 Restore Presets"),
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnRestorePresets.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnRestorePresets.Click += BtnRestorePresets_Click;
            spToolbarButtons.Children.Add(_btnRestorePresets);

            _btnOpenJson = new Button()
            {
                Content = T("Btn_ToolboxOpenJson", "📂 Open toolbox.json"),
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11.5
            };
            _btnOpenJson.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenJson.Click += BtnOpenJson_Click;
            spToolbarButtons.Children.Add(_btnOpenJson);

            Grid.SetColumn(spToolbarButtons, 1);
            toolbarGrid.Children.Add(spToolbarButtons);

            toolbarCard.Child = toolbarGrid;
            spTop.Children.Add(toolbarCard);

            Grid.SetRow(spTop, 0);
            rootGrid.Children.Add(spTop);

            // =====================================================================
            // ROW 1: CRUD ADD / EDIT FORM MODAL PANEL (COLLAPSIBLE)
            // =====================================================================
            _borderEditorCard = BuildCrudEditorCard();
            _borderEditorCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderEditorCard, 1);
            rootGrid.Children.Add(_borderEditorCard);

            // =====================================================================
            // ROW 2: DYNAMIC CATEGORIES ACCORDION VIEW
            // =====================================================================
            ScrollViewer svCategories = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 10)
            };

            _spCategoriesHost = new StackPanel();
            svCategories.Content = _spCategoriesHost;

            Grid.SetRow(svCategories, 2);
            rootGrid.Children.Add(svCategories);

            // =====================================================================
            // ROW 3: EMBEDDED REAL-TIME CONSOLE OUTPUT VIEWER
            // =====================================================================
            Border consoleCard = BuildConsoleViewerCard();
            Grid.SetRow(consoleCard, 3);
            rootGrid.Children.Add(consoleCard);

            RebuildCategoriesAccordion();
            return rootGrid;
        }

        private Border BuildCrudEditorCard()
        {
            Border card = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");

            StackPanel sp = new StackPanel();

            _txtEditorHeader = new TextBlock()
            {
                Text = T("Toolbox_Editor_NewTitle", "➕ Add Custom Command"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _txtEditorHeader.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            sp.Children.Add(_txtEditorHeader);

            // Row A: Title (50%) | Category (30%) | Shell (20%)
            Grid rowA = new Grid() { Margin = new Thickness(0, 0, 0, 8) };
            rowA.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2.2, GridUnitType.Star) });
            rowA.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.4, GridUnitType.Star) });
            rowA.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });

            // Title
            StackPanel spTitle = new StackPanel() { Margin = new Thickness(0, 0, 8, 0) };
            _lblFieldTitle = CreateFormLabel(T("Toolbox_Field_Title", "Title * :"));
            spTitle.Children.Add(_lblFieldTitle);
            _txtFieldTitle = CreateFormTextBox();
            spTitle.Children.Add(_txtFieldTitle);
            Grid.SetColumn(spTitle, 0);
            rowA.Children.Add(spTitle);

            // Category (Editable ComboBox)
            StackPanel spCategory = new StackPanel() { Margin = new Thickness(0, 0, 8, 0) };
            _lblFieldCategory = CreateFormLabel(T("Toolbox_Field_Category", "Category * :"));
            spCategory.Children.Add(_lblFieldCategory);
            _cmbFieldCategory = new ComboBox()
            {
                IsEditable = true,
                Height = 28,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            spCategory.Children.Add(_cmbFieldCategory);
            Grid.SetColumn(spCategory, 1);
            rowA.Children.Add(spCategory);

            // Shell (ComboBox: cmd, powershell, binary)
            StackPanel spShell = new StackPanel();
            _lblFieldShell = CreateFormLabel(T("Toolbox_Field_Shell", "Shell / Interpreter * :"));
            spShell.Children.Add(_lblFieldShell);
            _cmbFieldShell = new ComboBox()
            {
                Height = 28,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _cmbFieldShell.Items.Add(new ComboBoxItem() { Content = "CMD (cmd.exe)", Tag = ToolboxShellType.Cmd });
            _cmbFieldShell.Items.Add(new ComboBoxItem() { Content = "PowerShell (powershell.exe)", Tag = ToolboxShellType.PowerShell });
            _cmbFieldShell.Items.Add(new ComboBoxItem() { Content = "Binary / Direct Executable", Tag = ToolboxShellType.Binary });
            _cmbFieldShell.SelectedIndex = 0;
            spShell.Children.Add(_cmbFieldShell);
            Grid.SetColumn(spShell, 2);
            rowA.Children.Add(spShell);

            sp.Children.Add(rowA);

            // Row B: Command (75%) | Timeout Seconds (25%)
            Grid rowB = new Grid() { Margin = new Thickness(0, 0, 0, 8) };
            rowB.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(3.5, GridUnitType.Star) });
            rowB.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });

            StackPanel spCmd = new StackPanel() { Margin = new Thickness(0, 0, 8, 0) };
            _lblFieldCommand = CreateFormLabel(T("Toolbox_Field_Command", "Command or Script * :"));
            spCmd.Children.Add(_lblFieldCommand);
            _txtFieldCommand = CreateFormTextBox();
            _txtFieldCommand.FontFamily = new FontFamily("Consolas");
            _txtFieldCommand.TextChanged += (s, e) =>
            {
                if (_editingItem == null && _chkRequiresAdmin != null)
                {
                    if (ToolboxCommandItem.InferRequiresAdmin(_txtFieldCommand.Text))
                    {
                        _chkRequiresAdmin.IsChecked = true;
                    }
                }
            };
            spCmd.Children.Add(_txtFieldCommand);
            Grid.SetColumn(spCmd, 0);
            rowB.Children.Add(spCmd);

            StackPanel spTimeout = new StackPanel();
            _lblFieldTimeout = CreateFormLabel(T("Toolbox_Field_Timeout", "Timeout (sec, 0=none):"));
            spTimeout.Children.Add(_lblFieldTimeout);
            _txtFieldTimeout = CreateFormTextBox();
            _txtFieldTimeout.Text = "0";
            spTimeout.Children.Add(_txtFieldTimeout);
            Grid.SetColumn(spTimeout, 1);
            rowB.Children.Add(spTimeout);

            sp.Children.Add(rowB);

            // Row C: Description
            StackPanel spDesc = new StackPanel() { Margin = new Thickness(0, 0, 0, 8) };
            _lblFieldDescription = CreateFormLabel(T("Toolbox_Field_Description", "Description (optional):"));
            spDesc.Children.Add(_lblFieldDescription);
            _txtFieldDescription = CreateFormTextBox();
            spDesc.Children.Add(_txtFieldDescription);
            sp.Children.Add(spDesc);

            // Row D: Checkboxes on Left | Save & Cancel Buttons on Right
            Grid rowD = new Grid();
            rowD.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            rowD.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            WrapPanel wpChecks = new WrapPanel() { VerticalAlignment = VerticalAlignment.Center };

            _chkWaitForExit = new CheckBox()
            {
                Content = T("Toolbox_Chk_WaitForExit", "Wait for exit (Synchronous)"),
                IsChecked = true,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 14, 2)
            };
            _chkWaitForExit.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpChecks.Children.Add(_chkWaitForExit);

            _chkCaptureOutput = new CheckBox()
            {
                Content = T("Toolbox_Chk_CaptureOutput", "Capture output (stdout/stderr)"),
                IsChecked = true,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 14, 2)
            };
            _chkCaptureOutput.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpChecks.Children.Add(_chkCaptureOutput);

            _chkRequiresAdmin = new CheckBox()
            {
                Content = T("Toolbox_Chk_RequiresAdmin", "🛡️ Requires Administrator"),
                IsChecked = false,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _chkRequiresAdmin.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpChecks.Children.Add(_chkRequiresAdmin);

            Grid.SetColumn(wpChecks, 0);
            rowD.Children.Add(wpChecks);

            StackPanel spFormBtns = new StackPanel() { Orientation = Orientation.Horizontal };
            _btnSaveCommand = new Button()
            {
                Content = T("Btn_ToolboxSave", "💾 Save Command"),
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnSaveCommand.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnSaveCommand.Click += BtnSaveCommand_Click;
            spFormBtns.Children.Add(_btnSaveCommand);

            _btnCancelEdit = new Button()
            {
                Content = T("Btn_Cancel", "✕ Cancel"),
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11.5
            };
            _btnCancelEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCancelEdit.Click += (s, e) => CloseEditor();
            spFormBtns.Children.Add(_btnCancelEdit);

            Grid.SetColumn(spFormBtns, 1);
            rowD.Children.Add(spFormBtns);

            sp.Children.Add(rowD);
            card.Child = sp;
            return card;
        }

        private Border BuildConsoleViewerCard()
        {
            Border card = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

            // Console Header Bar
            Grid headerGrid = new Grid() { Margin = new Thickness(0, 0, 0, 6) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _txtConsoleTitle = new TextBlock()
            {
                Text = T("Toolbox_Console_Title", "⬛ Real-Time Output Console"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _txtConsoleTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(_txtConsoleTitle, 0);
            headerGrid.Children.Add(_txtConsoleTitle);

            // Exit Code / Execution Status Pill
            _borderStatusPill = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184))
            };
            _txtStatusPill = new TextBlock()
            {
                Text = T("Toolbox_Status_Idle", "Idle"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            _borderStatusPill.Child = _txtStatusPill;
            Grid.SetColumn(_borderStatusPill, 1);
            headerGrid.Children.Add(_borderStatusPill);

            // Console Action Buttons
            StackPanel spConsoleBtns = new StackPanel() { Orientation = Orientation.Horizontal };

            _btnStopProcess = new Button()
            {
                Content = T("Btn_ToolboxStop", "⏹ Stop"),
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                FontSize = 11,
                IsEnabled = false,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnStopProcess.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnStopProcess.Click += (s, e) =>
            {
                if (_runner != null && _runner.IsRunning)
                {
                    _runner.StopCurrentProcess();
                }
            };
            spConsoleBtns.Children.Add(_btnStopProcess);

            _btnCopyConsole = new Button()
            {
                Content = T("Btn_Copy", "📋 Copy"),
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnCopyConsole.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCopyConsole.Click += (s, e) =>
            {
                try
                {
                    if (_txtConsoleOutput != null && !string.IsNullOrEmpty(_txtConsoleOutput.Text))
                    {
                        Clipboard.SetText(_txtConsoleOutput.Text);
                    }
                }
                catch { }
            };
            spConsoleBtns.Children.Add(_btnCopyConsole);

            _btnClearConsole = new Button()
            {
                Content = T("Btn_Clear", "🧹 Clear"),
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                FontSize = 11
            };
            _btnClearConsole.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnClearConsole.Click += (s, e) =>
            {
                if (_txtConsoleOutput != null) _txtConsoleOutput.Clear();
                SetConsoleStatusPill(T("Toolbox_Status_Idle", "Idle"), Color.FromRgb(148, 163, 184));
            };
            spConsoleBtns.Children.Add(_btnClearConsole);

            Grid.SetColumn(spConsoleBtns, 3);
            headerGrid.Children.Add(spConsoleBtns);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            // Monospace Output TextBox
            _txtConsoleOutput = new TextBox()
            {
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(12, 16, 23)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            _txtConsoleOutput.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            Grid.SetRow(_txtConsoleOutput, 1);
            grid.Children.Add(_txtConsoleOutput);

            card.Child = grid;
            return card;
        }

        private void RebuildCategoriesAccordion()
        {
            if (_spCategoriesHost == null) return;
            _spCategoriesHost.Children.Clear();

            string filter = (_txtSearch != null && _txtSearch.Text != null)
                ? _txtSearch.Text.Trim()
                : "";

            // Group commands dynamically by category preserving insertion order
            Dictionary<string, List<ToolboxCommandItem>> grouped = new Dictionary<string, List<ToolboxCommandItem>>(StringComparer.OrdinalIgnoreCase);
            List<string> categoryOrder = new List<string>();

            if (_commands != null)
            {
                foreach (ToolboxCommandItem item in _commands)
                {
                    if (item == null) continue;

                    if (!string.IsNullOrEmpty(filter))
                    {
                        bool matches =
                            (item.title != null && item.title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (item.category != null && item.category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (item.command != null && item.command.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (item.description != null && item.description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                        if (!matches) continue;
                    }

                    string cat = string.IsNullOrWhiteSpace(item.category) ? "General" : item.category.Trim();
                    if (!grouped.ContainsKey(cat))
                    {
                        grouped[cat] = new List<ToolboxCommandItem>();
                        categoryOrder.Add(cat);
                    }
                    grouped[cat].Add(item);
                }
            }

            if (categoryOrder.Count == 0)
            {
                Border emptyCard = new Border()
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 4, 0, 4)
                };
                emptyCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
                emptyCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                TextBlock txtEmpty = new TextBlock()
                {
                    Text = T("Toolbox_Empty_List", "No commands found. Click '➕ New Command' to add one or '🔄 Restore Presets'."),
                    FontSize = 12.5,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                txtEmpty.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                emptyCard.Child = txtEmpty;
                _spCategoriesHost.Children.Add(emptyCard);
                return;
            }

            foreach (string catName in categoryOrder)
            {
                List<ToolboxCommandItem> itemsInCat = grouped[catName];
                Border catContainer = BuildCategoryExpanderCard(catName, itemsInCat);
                _spCategoriesHost.Children.Add(catContainer);
            }
        }

        private Border BuildCategoryExpanderCard(string categoryName, List<ToolboxCommandItem> items)
        {
            Border outerBorder = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 6, 10, 8)
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Expander expander = new Expander()
            {
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Expander Header: Category Icon + Name + Count Badge
            StackPanel spHeader = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 4, 4, 4)
            };

            TextBlock txtCatName = new TextBlock()
            {
                Text = "📂  " + categoryName,
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtCatName.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            spHeader.Children.Add(txtCatName);

            Border badgeCount = new Border()
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248))
            };
            TextBlock txtCount = new TextBlock()
            {
                Text = items.Count.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            txtCount.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            badgeCount.Child = txtCount;
            spHeader.Children.Add(badgeCount);

            expander.Header = spHeader;

            StackPanel spItems = new StackPanel() { Margin = new Thickness(0, 6, 0, 0) };
            foreach (ToolboxCommandItem item in items)
            {
                spItems.Children.Add(BuildCommandItemCard(item));
            }

            expander.Content = spItems;
            outerBorder.Child = expander;
            return outerBorder;
        }

        private Border BuildCommandItemCard(ToolboxCommandItem item)
        {
            Border itemCard = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 6)
            };
            itemCard.SetResourceReference(Border.BackgroundProperty, "BrushBgWindow");
            itemCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            // Left: Title + Badges + Description + Monospace Command Preview
            StackPanel spLeft = new StackPanel() { Margin = new Thickness(0, 0, 12, 0) };

            WrapPanel wpTitleRow = new WrapPanel() { VerticalAlignment = VerticalAlignment.Center };

            TextBlock txtItemTitle = new TextBlock()
            {
                Text = item.title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            txtItemTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            wpTitleRow.Children.Add(txtItemTitle);

            // Shield/Admin Indicator Badge
            if (item.requires_admin)
            {
                wpTitleRow.Children.Add(CreateBadge(
                    "🛡️ Admin",
                    Color.FromArgb(45, 245, 158, 11),
                    Color.FromRgb(251, 191, 36)));
            }

            // Shell Badge (CMD / PowerShell / Binary)
            wpTitleRow.Children.Add(CreateBadge(
                item.ShellDisplayBadge,
                Color.FromArgb(40, 14, 165, 233),
                Color.FromRgb(56, 189, 248)));

            // Wait vs Background Badge
            string modeLabel = item.wait_for_exit
                ? T("Toolbox_Badge_Sync", "⏳ Wait")
                : T("Toolbox_Badge_Async", "⚡ Background");
            wpTitleRow.Children.Add(CreateBadge(
                modeLabel,
                Color.FromArgb(35, 148, 163, 184),
                Color.FromRgb(148, 163, 184)));

            if (item.capture_output)
            {
                wpTitleRow.Children.Add(CreateBadge(
                    "📟 Stream",
                    Color.FromArgb(35, 34, 197, 94),
                    Color.FromRgb(74, 222, 128)));
            }

            spLeft.Children.Add(wpTitleRow);

            if (!string.IsNullOrWhiteSpace(item.description))
            {
                TextBlock txtDesc = new TextBlock()
                {
                    Text = item.description,
                    FontSize = 11.5,
                    Margin = new Thickness(0, 3, 0, 3),
                    TextWrapping = TextWrapping.Wrap
                };
                txtDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                spLeft.Children.Add(txtDesc);
            }

            Border cmdBox = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 3, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0))
            };
            TextBlock txtCmd = new TextBlock()
            {
                Text = item.command,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = item.command
            };
            txtCmd.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            cmdBox.Child = txtCmd;
            spLeft.Children.Add(cmdBox);

            Grid.SetColumn(spLeft, 0);
            g.Children.Add(spLeft);

            // Right: Execute, Edit, Delete Buttons
            StackPanel spActions = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            ToolboxCommandItem capturedItem = item;

            Button btnRun = new Button()
            {
                Content = T("Btn_ToolboxRun", "▶ Run"),
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = T("Tooltip_ToolboxRun", "Execute this command")
            };
            btnRun.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnRun.Click += (s, e) => ExecuteCommandItem(capturedItem);
            spActions.Children.Add(btnRun);

            Button btnEdit = new Button()
            {
                Content = "✏️",
                Width = 30,
                Height = 30,
                FontSize = 12,
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = T("Tooltip_Edit", "Edit")
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditorForItem(capturedItem);
            spActions.Children.Add(btnEdit);

            Button btnDelete = new Button()
            {
                Content = "🗑️",
                Width = 30,
                Height = 30,
                FontSize = 12,
                ToolTip = T("Tooltip_Remove", "Delete")
            };
            btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDelete.Click += (s, e) => ConfirmAndDeleteItem(capturedItem);
            spActions.Children.Add(btnDelete);

            Grid.SetColumn(spActions, 1);
            g.Children.Add(spActions);

            itemCard.Child = g;
            return itemCard;
        }

        private Border CreateBadge(string text, Color bg, Color fg)
        {
            Border b = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 1, 5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(bg)
            };
            TextBlock tb = new TextBlock()
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg)
            };
            b.Child = tb;
            return b;
        }

        private TextBlock CreateFormLabel(string text)
        {
            TextBlock lbl = new TextBlock()
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            return lbl;
        }

        private TextBox CreateFormTextBox()
        {
            TextBox tb = new TextBox()
            {
                Height = 28,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1)
            };
            tb.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            tb.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            tb.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            return tb;
        }

        #region CRUD EDITOR OPERATIONS

        private void PopulateCategoryComboChoices()
        {
            if (_cmbFieldCategory == null) return;
            string currentText = _cmbFieldCategory.Text;
            _cmbFieldCategory.Items.Clear();

            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Correção de Erros",
                "Rede",
                "Otimização",
                "Diagnóstico"
            };

            if (_commands != null)
            {
                foreach (ToolboxCommandItem cmd in _commands)
                {
                    if (cmd != null && !string.IsNullOrWhiteSpace(cmd.category))
                    {
                        unique.Add(cmd.category.Trim());
                    }
                }
            }

            foreach (string c in unique)
            {
                _cmbFieldCategory.Items.Add(c);
            }

            _cmbFieldCategory.Text = string.IsNullOrWhiteSpace(currentText) ? "Otimização" : currentText;
        }

        private void OpenEditorForNew()
        {
            _editingItem = null;
            PopulateCategoryComboChoices();

            _txtEditorHeader.Text = T("Toolbox_Editor_NewTitle", "➕ Add Custom Command");
            _txtFieldTitle.Text = "";
            _cmbFieldCategory.Text = "Otimização";
            _cmbFieldShell.SelectedIndex = 0;
            _txtFieldCommand.Text = "";
            _txtFieldDescription.Text = "";
            _txtFieldTimeout.Text = "0";
            _chkWaitForExit.IsChecked = true;
            _chkCaptureOutput.IsChecked = true;
            _chkRequiresAdmin.IsChecked = false;

            _borderEditorCard.Visibility = Visibility.Visible;
            _txtFieldTitle.Focus();
        }

        private void OpenEditorForItem(ToolboxCommandItem item)
        {
            if (item == null) return;
            _editingItem = item;
            PopulateCategoryComboChoices();

            _txtEditorHeader.Text = T("Toolbox_Editor_EditTitle", "✏️ Edit Command");
            _txtFieldTitle.Text = item.title;
            _cmbFieldCategory.Text = item.category;

            string normShell = ToolboxShellType.Normalize(item.shell);
            if (normShell == ToolboxShellType.PowerShell) _cmbFieldShell.SelectedIndex = 1;
            else if (normShell == ToolboxShellType.Binary) _cmbFieldShell.SelectedIndex = 2;
            else _cmbFieldShell.SelectedIndex = 0;

            _txtFieldCommand.Text = item.command;
            _txtFieldDescription.Text = item.description ?? "";
            _txtFieldTimeout.Text = item.timeout_seconds.ToString();
            _chkWaitForExit.IsChecked = item.wait_for_exit;
            _chkCaptureOutput.IsChecked = item.capture_output;
            _chkRequiresAdmin.IsChecked = item.requires_admin;

            _borderEditorCard.Visibility = Visibility.Visible;
            _txtFieldTitle.Focus();
        }

        private void CloseEditor()
        {
            _editingItem = null;
            if (_borderEditorCard != null)
            {
                _borderEditorCard.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSaveCommand_Click(object sender, RoutedEventArgs e)
        {
            string selectedShell = ToolboxShellType.Cmd;
            ComboBoxItem cbi = _cmbFieldShell.SelectedItem as ComboBoxItem;
            if (cbi != null && cbi.Tag != null)
            {
                selectedShell = cbi.Tag.ToString();
            }

            int timeoutSec = 0;
            int.TryParse((_txtFieldTimeout.Text ?? "0").Trim(), out timeoutSec);
            if (timeoutSec < 0) timeoutSec = 0;

            ToolboxCommandItem candidate = new ToolboxCommandItem()
            {
                id = (_editingItem != null && !string.IsNullOrWhiteSpace(_editingItem.id))
                    ? _editingItem.id
                    : Guid.NewGuid().ToString("N"),
                title = (_txtFieldTitle.Text ?? "").Trim(),
                category = (_cmbFieldCategory.Text ?? "").Trim(),
                shell = selectedShell,
                command = (_txtFieldCommand.Text ?? "").Trim(),
                description = (_txtFieldDescription.Text ?? "").Trim(),
                timeout_seconds = timeoutSec,
                wait_for_exit = _chkWaitForExit.IsChecked == true,
                capture_output = _chkCaptureOutput.IsChecked == true,
                requires_admin = _chkRequiresAdmin.IsChecked == true
            };

            string validationError;
            if (!candidate.Validate(out validationError))
            {
                MessageBox.Show(
                    validationError,
                    "OptiAssist - Toolbox Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _commands = _storage.UpsertCommand(candidate);
                CloseEditor();
                RebuildCategoriesAccordion();
                HostLog(string.Format("[TOOLBOX] Saved command '{0}' ({1}) in category '{2}'.",
                    candidate.title, candidate.shell, candidate.category));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to save command: " + ex.Message,
                    "OptiAssist - Toolbox Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ConfirmAndDeleteItem(ToolboxCommandItem item)
        {
            if (item == null) return;
            string confirmMsg = string.Format(
                T("Msg_ToolboxDeleteConfirm", "Are you sure you want to delete the command '{0}'?"),
                item.title);

            if (MessageBox.Show(confirmMsg, "OptiAssist - Toolbox", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _commands = _storage.DeleteCommand(item.id);
                if (_editingItem != null && string.Equals(_editingItem.id, item.id, StringComparison.OrdinalIgnoreCase))
                {
                    CloseEditor();
                }
                RebuildCategoriesAccordion();
                HostLog(string.Format("[TOOLBOX] Deleted command '{0}' (id: {1}).", item.title, item.id));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting command: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestorePresets_Click(object sender, RoutedEventArgs e)
        {
            string msg = T("Msg_ToolboxRestoreConfirm",
                "Do you want to restore the default Toolbox command presets?\nThis will reset toolbox.json to the factory preset list.");
            if (MessageBox.Show(msg, "OptiAssist - Toolbox", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _commands = _storage.RestoreDefaultPresets();
            CloseEditor();
            RebuildCategoriesAccordion();
            HostLog("[TOOLBOX] Default command presets restored.");
        }

        private void BtnOpenJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = _storage.StorageFilePath;
                if (!File.Exists(path))
                {
                    _storage.SaveCommands(_commands);
                }
                Process.Start(new ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open toolbox.json: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region COMMAND EXECUTION & STREAMING CONSOLE

        private void ExecuteCommandItem(ToolboxCommandItem item)
        {
            if (item == null) return;
            if (_runner != null && _runner.IsRunning)
            {
                MessageBox.Show(
                    T("Msg_ToolboxBusy", "A Toolbox command is already running. Wait for it to finish or click '⏹ Stop'."),
                    "OptiAssist - Toolbox",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (_txtConsoleOutput != null)
            {
                _txtConsoleOutput.Clear();
            }

            string headerLine = string.Format(">>> [{0}] {1} ({2}: {3})",
                DateTime.Now.ToString("HH:mm:ss"), item.title, item.shell.ToUpperInvariant(), item.command);
            AppendConsoleLine(headerLine);
            AppendConsoleLine(new string('-', 76));

            if (item.capture_output)
            {
                HostLog(headerLine);
            }

            SetConsoleStatusPill(
                string.Format(T("Toolbox_Status_Running", "⏳ Running: {0}..."), item.title),
                Color.FromRgb(56, 189, 248));

            if (_btnStopProcess != null) _btnStopProcess.IsEnabled = true;

            _runner.RunAsync(
                item,
                (line, isErr) =>
                {
                    string formatted = isErr ? "[STDERR] " + line : line;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AppendConsoleLine(formatted);
                    }));

                    if (item.capture_output)
                    {
                        HostLog(formatted);
                    }
                },
                (result) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_btnStopProcess != null) _btnStopProcess.IsEnabled = false;
                        AppendConsoleLine(new string('-', 76));

                        if (result.LaunchedInBackground)
                        {
                            string bgMsg = string.Format(
                                T("Toolbox_Status_Background", "⚡ Launched in background (PID: {0})"),
                                result.ProcessId);
                            SetConsoleStatusPill(bgMsg, Color.FromRgb(56, 189, 248));
                            AppendConsoleLine(bgMsg);
                            HostLog("[TOOLBOX] " + bgMsg);
                        }
                        else if (result.UacCanceled)
                        {
                            string uacMsg = T("Toolbox_Status_UacCanceled", "🚫 UAC Elevation Canceled by User");
                            SetConsoleStatusPill(uacMsg, Color.FromRgb(251, 146, 60));
                            AppendConsoleLine(uacMsg);
                            HostLog("[TOOLBOX] " + uacMsg);
                        }
                        else if (result.TimedOut)
                        {
                            string toMsg = string.Format(
                                T("Toolbox_Status_TimedOut", "⏱️ Timed Out ({0}s)"),
                                item.timeout_seconds);
                            SetConsoleStatusPill(toMsg, Color.FromRgb(251, 146, 60));
                            AppendConsoleLine(toMsg);
                            HostLog("[TOOLBOX] " + toMsg);
                        }
                        else if (result.CanceledByUser)
                        {
                            string cancelMsg = T("Toolbox_Status_Stopped", "⏹ Stopped by User");
                            SetConsoleStatusPill(cancelMsg, Color.FromRgb(251, 146, 60));
                            AppendConsoleLine(cancelMsg);
                            HostLog("[TOOLBOX] " + cancelMsg);
                        }
                        else if (result.Success)
                        {
                            string okMsg = string.Format(
                                T("Toolbox_Status_Success", "✔️ Success (Exit Code: {0} • {1} ms)"),
                                result.ExitCode, result.ElapsedMilliseconds);
                            SetConsoleStatusPill(okMsg, Color.FromRgb(74, 222, 128));
                            AppendConsoleLine(okMsg);
                            HostLog("[TOOLBOX] " + okMsg);
                        }
                        else
                        {
                            string failMsg = string.Format(
                                T("Toolbox_Status_Failed", "❌ Failed (Exit Code: {0} • {1} ms)"),
                                result.ExitCode, result.ElapsedMilliseconds);
                            SetConsoleStatusPill(failMsg, Color.FromRgb(248, 113, 113));
                            AppendConsoleLine(failMsg + (string.IsNullOrEmpty(result.ErrorMessage) ? "" : " - " + result.ErrorMessage));
                            HostLog("[TOOLBOX] " + failMsg);
                        }
                    }));
                });
        }

        private void AppendConsoleLine(string line)
        {
            if (_txtConsoleOutput == null) return;
            if (_txtConsoleOutput.Text.Length > 0)
            {
                _txtConsoleOutput.AppendText(Environment.NewLine + line);
            }
            else
            {
                _txtConsoleOutput.AppendText(line);
            }
            _txtConsoleOutput.ScrollToEnd();
        }

        private void SetConsoleStatusPill(string text, Color fgColor)
        {
            if (_txtStatusPill != null)
            {
                _txtStatusPill.Text = text;
                _txtStatusPill.Foreground = new SolidColorBrush(fgColor);
            }
            if (_borderStatusPill != null)
            {
                _borderStatusPill.Background = new SolidColorBrush(Color.FromArgb(38, fgColor.R, fgColor.G, fgColor.B));
            }
        }

        #endregion

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = T("Toolbox_Title", "🧰 Custom Command Toolbox");
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Toolbox_Subtitle", "Create, organize, and run custom CMD, PowerShell, and binary commands with live output streaming");
            if (_btnNewCommand != null) _btnNewCommand.Content = T("Btn_ToolboxNew", "➕ New Command");
            if (_btnRestorePresets != null) _btnRestorePresets.Content = T("Btn_ToolboxRestorePresets", "🔄 Restore Presets");
            if (_btnOpenJson != null) _btnOpenJson.Content = T("Btn_ToolboxOpenJson", "📂 Open toolbox.json");
            if (_txtConsoleTitle != null) _txtConsoleTitle.Text = T("Toolbox_Console_Title", "⬛ Real-Time Output Console");
            if (_btnStopProcess != null) _btnStopProcess.Content = T("Btn_ToolboxStop", "⏹ Stop");
            if (_btnCopyConsole != null) _btnCopyConsole.Content = T("Btn_Copy", "📋 Copy");
            if (_btnClearConsole != null) _btnClearConsole.Content = T("Btn_Clear", "🧹 Clear");
            if (_lblFieldTitle != null) _lblFieldTitle.Text = T("Toolbox_Field_Title", "Title * :");
            if (_lblFieldCategory != null) _lblFieldCategory.Text = T("Toolbox_Field_Category", "Category * :");
            if (_lblFieldShell != null) _lblFieldShell.Text = T("Toolbox_Field_Shell", "Shell / Interpreter * :");
            if (_lblFieldCommand != null) _lblFieldCommand.Text = T("Toolbox_Field_Command", "Command or Script * :");
            if (_lblFieldTimeout != null) _lblFieldTimeout.Text = T("Toolbox_Field_Timeout", "Timeout (sec, 0=none):");
            if (_lblFieldDescription != null) _lblFieldDescription.Text = T("Toolbox_Field_Description", "Description (optional):");
            if (_chkWaitForExit != null) _chkWaitForExit.Content = T("Toolbox_Chk_WaitForExit", "Wait for exit (Synchronous)");
            if (_chkCaptureOutput != null) _chkCaptureOutput.Content = T("Toolbox_Chk_CaptureOutput", "Capture output (stdout/stderr)");
            if (_chkRequiresAdmin != null) _chkRequiresAdmin.Content = T("Toolbox_Chk_RequiresAdmin", "🛡️ Requires Administrator");
            if (_btnSaveCommand != null) _btnSaveCommand.Content = T("Btn_ToolboxSave", "💾 Save Command");
            if (_btnCancelEdit != null) _btnCancelEdit.Content = T("Btn_Cancel", "✕ Cancel");

            RebuildCategoriesAccordion();
        }
    }
}
