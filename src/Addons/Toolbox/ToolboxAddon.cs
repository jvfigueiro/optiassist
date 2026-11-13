using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace OptiAssist.Addons.Toolbox
{
    /// <summary>
    /// Toolbox Add-on Module (Config\Addons\Toolbox\Toolbox.dll) allowing users to create, manage,
    /// categorize, and execute custom actions (CMD, PowerShell, or standalone binaries)
    /// integrated seamlessly into the application.
    /// </summary>
    public class ToolboxAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ToolboxStorageManager _storage;
        private ToolboxProcessRunner _runner;
        private List<ToolboxCommandItem> _commands;
        private ToolboxCommandItem _editingItem;

        // Navigation Tabs (Toolbox | Windows Debloater)
        private Border _tabToolboxBorder;
        private TextBlock _tabToolboxText;
        private Border _tabDebloaterBorder;
        private TextBlock _tabDebloaterText;
        private Grid _pnlMainToolboxView;
        private ScrollViewer _pnlDebloaterView;
        private List<KeyValuePair<CheckBox, ToolboxCommandItem>> _toolboxChecks = new List<KeyValuePair<CheckBox, ToolboxCommandItem>>();
        private List<KeyValuePair<CheckBox, ToolboxCommandItem>> _debloatChecks = new List<KeyValuePair<CheckBox, ToolboxCommandItem>>();

        // Top Header & Toolbar controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private TextBox _txtSearch;
        private TextBlock _txtSearchWatermark;
        private Button _btnOptions;
        private Button _btnNewCommand;
        private Button _btnRestorePresets;
        private Button _btnOpenJson;

        // Options Panel & Edit Mode
        private Border _borderOptionsCard;
        private TextBlock _txtOptionsTitle;
        private CheckBox _chkEditMode;
        private Button _btnManageCategories;
        private bool _isEditModeEnabled = false;

        // Category Manager Card
        private Border _borderCategoryManagerCard;
        private TextBlock _txtCatMgrHeader;
        private TextBlock _lblCatMgrSelect;
        private ComboBox _cmbManageExistingCategories;
        private TextBlock _lblCatMgrNewName;
        private TextBox _txtRenameCategory;
        private Button _btnRenameCategory;
        private Button _btnDeleteCategory;
        private TextBlock _lblCatMgrAdd;
        private TextBox _txtAddNewCategory;
        private Button _btnAddCategory;
        private Button _btnCloseCategoryManager;

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
        private CheckBox _chkOpenExternalLogs;
        private Button _btnDeleteFromEditor;
        private Button _btnSaveCommand;
        private Button _btnCancelEdit;

        // Dynamic Categories Accordion host
        private StackPanel _spCategoriesHost;

        public string Id { get { return "toolbox"; } }
        public string Name { get { return T("Nav_Toolbox", "Toolbox"); } }
        public string Description { get { return T("Toolbox_Subtitle", "Custom actions and system tools integrated into OptiAssist"); } }
        public string Version { get { return "1.2.S4"; } }
        public string Author { get { return "João Figueiró"; } }
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
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Top Header
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Navigation Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 2: Active Tab Content

            // =====================================================================
            // ROW 0: TOP HEADER (Harmonized layout across all OptiAssist modules)
            // =====================================================================
            Grid headerGrid = new Grid() { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };

            _txtTitle = new TextBlock()
            {
                Text = string.Format("{0}  {1}", Icon, Name),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            titlePanel.Children.Add(_txtTitle);

            _txtSubtitle = new TextBlock()
            {
                Text = T("Toolbox_Subtitle", "Custom actions and system tools integrated into OptiAssist"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            titlePanel.Children.Add(_txtSubtitle);

            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            StackPanel topActions = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnOptions = new Button()
            {
                Content = T("Btn_ToolboxOptions", "⚙️ Options"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                ToolTip = T("Tooltip_ToolboxOptions", "Open Toolbox options")
            };
            _btnOptions.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOptions.Click += (s, e) =>
            {
                _borderOptionsCard.Visibility = (_borderOptionsCard.Visibility == Visibility.Visible)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
            topActions.Children.Add(_btnOptions);

            Grid.SetColumn(topActions, 1);
            headerGrid.Children.Add(topActions);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // =====================================================================
            // ROW 1: NAVIGATION TABS (Toolbox | Windows Debloater)
            // =====================================================================
            Border tabsBar = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 0, 10)
            };
            tabsBar.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            tabsBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel tabsPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            _tabToolboxBorder = CreateTabButton("🛠️  " + T("Toolbox_Tab_Main", "Toolbox"), true, out _tabToolboxText);
            _tabToolboxBorder.MouseLeftButtonDown += (s, e) => SwitchToolboxTab(true);
            tabsPanel.Children.Add(_tabToolboxBorder);

            _tabDebloaterBorder = CreateTabButton("🧹  " + T("Toolbox_Tab_Debloater", "Windows Debloater"), false, out _tabDebloaterText);
            _tabDebloaterBorder.MouseLeftButtonDown += (s, e) => SwitchToolboxTab(false);
            tabsPanel.Children.Add(_tabDebloaterBorder);

            tabsBar.Child = tabsPanel;
            Grid.SetRow(tabsBar, 1);
            rootGrid.Children.Add(tabsBar);

            // =====================================================================
            // ROW 2: TAB CONTENTS (Toolbox Main + Windows Debloater)
            // =====================================================================
            Grid tabHost = new Grid();
            Grid.SetRow(tabHost, 2);

            _pnlMainToolboxView = new Grid();
            _pnlMainToolboxView.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Search Toolbar
            _pnlMainToolboxView.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Options Panel
            _pnlMainToolboxView.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 2: Category Manager Panel
            _pnlMainToolboxView.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 3: CRUD Form Modal/Panel
            _pnlMainToolboxView.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 4: Categories Accordion

            // =====================================================================
            // MAIN TOOLBOX ROW 0: SEARCH & BATCH TOOLBAR (Windows Debloater Style)
            // =====================================================================
            Border toolbarCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            toolbarCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            toolbarCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            Grid searchInputGrid = new Grid();
            _txtSearch = new TextBox()
            {
                Height = 28,
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = T("Toolbox_Search_Placeholder", "🔍 Type here to search for the tool, category, or command you want...")
            };
            _txtSearch.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtSearch.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtSearch.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");

            _txtSearchWatermark = new TextBlock()
            {
                Text = T("Toolbox_Search_Placeholder", "🔍 Type here to search for the tool, category, or command you want..."),
                FontSize = 11,
                Margin = new Thickness(10, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _txtSearchWatermark.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");

            _txtSearch.TextChanged += (s, e) =>
            {
                if (_txtSearchWatermark != null)
                {
                    _txtSearchWatermark.Visibility = string.IsNullOrEmpty(_txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
                }
                RebuildCategoriesAccordion();
            };

            searchInputGrid.Children.Add(_txtSearch);
            searchInputGrid.Children.Add(_txtSearchWatermark);
            Grid.SetColumn(searchInputGrid, 0);
            toolbarGrid.Children.Add(searchInputGrid);

            StackPanel toolboxBatchPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Button btnToolboxSelectAll = new Button() { Content = T("Btn_SelectAll", "Select All"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
            btnToolboxSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnToolboxSelectAll.Click += (s, e) => { foreach (var kv in _toolboxChecks) kv.Key.IsChecked = true; };

            Button btnToolboxClearAll = new Button() { Content = T("Btn_ClearAll", "Clear"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
            btnToolboxClearAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnToolboxClearAll.Click += (s, e) => { foreach (var kv in _toolboxChecks) kv.Key.IsChecked = false; };

            Button btnToolboxRunSelected = new Button() { Content = "🚀 " + T("Toolbox_RunSelected", "Run Selected"), Height = 28, Padding = new Thickness(14, 0, 14, 0), FontSize = 11, FontWeight = FontWeights.Bold };
            btnToolboxRunSelected.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnToolboxRunSelected.Click += (s, e) =>
            {
                List<ToolboxCommandItem> selected = new List<ToolboxCommandItem>();
                foreach (var kv in _toolboxChecks)
                {
                    if (kv.Key.IsChecked == true) selected.Add(kv.Value);
                }
                if (selected.Count == 0)
                {
                    MessageBox.Show(T("Toolbox_NoneSelected", "Select at least one task to run."), "OptiAssist - Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var confirm = MessageBox.Show(
                    string.Format(T("Toolbox_ConfirmSelected", "Execute {0} selected Toolbox tasks sequentially?"), selected.Count),
                    "OptiAssist - Toolbox",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
                ExecuteBatchItems(selected, 0, btnToolboxRunSelected, btnToolboxRunSelected.Content);
            };

            toolboxBatchPanel.Children.Add(btnToolboxSelectAll);
            toolboxBatchPanel.Children.Add(btnToolboxClearAll);
            toolboxBatchPanel.Children.Add(btnToolboxRunSelected);
            Grid.SetColumn(toolboxBatchPanel, 1);
            toolbarGrid.Children.Add(toolboxBatchPanel);

            toolbarCard.Child = toolbarGrid;
            Grid.SetRow(toolbarCard, 0);
            _pnlMainToolboxView.Children.Add(toolbarCard);

            // =====================================================================
            // ROW 2: OPTIONS PANEL
            // =====================================================================
            _borderOptionsCard = BuildOptionsCard();
            _borderOptionsCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderOptionsCard, 1);
            _pnlMainToolboxView.Children.Add(_borderOptionsCard);

            // =====================================================================
            // MAIN TOOLBOX ROW 2: CATEGORY MANAGER PANEL
            // =====================================================================
            _borderCategoryManagerCard = BuildCategoryManagerCard();
            _borderCategoryManagerCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderCategoryManagerCard, 2);
            _pnlMainToolboxView.Children.Add(_borderCategoryManagerCard);

            // =====================================================================
            // MAIN TOOLBOX ROW 3: CRUD ADD / EDIT FORM MODAL PANEL (COLLAPSIBLE)
            // =====================================================================
            _borderEditorCard = BuildCrudEditorCard();
            _borderEditorCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderEditorCard, 3);
            _pnlMainToolboxView.Children.Add(_borderEditorCard);

            // =====================================================================
            // MAIN TOOLBOX ROW 4: DYNAMIC CATEGORIES ACCORDION VIEW
            // =====================================================================
            ScrollViewer svCategories = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 0)
            };
            svCategories.PreviewMouseWheel += (s, e) =>
            {
                if (!e.Handled && svCategories.ScrollableHeight > 0)
                {
                    e.Handled = true;
                    svCategories.ScrollToVerticalOffset(svCategories.VerticalOffset - (e.Delta / 2.5));
                }
            };

            _spCategoriesHost = new StackPanel();
            svCategories.Content = _spCategoriesHost;

            Grid.SetRow(svCategories, 4);
            _pnlMainToolboxView.Children.Add(svCategories);

            _pnlDebloaterView = BuildDebloaterTab();
            _pnlDebloaterView.Visibility = Visibility.Collapsed;

            tabHost.Children.Add(_pnlMainToolboxView);
            tabHost.Children.Add(_pnlDebloaterView);
            rootGrid.Children.Add(tabHost);

            RebuildCategoriesAccordion();
            return rootGrid;
        }

        private Border BuildOptionsCard()
        {
            Border card = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel sp = new StackPanel();

            _txtOptionsTitle = new TextBlock()
            {
                Text = T("Toolbox_Options_Title", "⚙️ Toolbox Options"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _txtOptionsTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            sp.Children.Add(_txtOptionsTitle);

            WrapPanel wpButtons = new WrapPanel() { Margin = new Thickness(0, 0, 0, 8) };

            _btnNewCommand = new Button()
            {
                Content = T("Btn_ToolboxNew", "➕ New Toolbox"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand
            };
            _btnNewCommand.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnNewCommand.Click += (s, e) =>
            {
                _borderOptionsCard.Visibility = Visibility.Collapsed;
                _borderCategoryManagerCard.Visibility = Visibility.Collapsed;
                OpenEditorForNew();
            };
            wpButtons.Children.Add(_btnNewCommand);

            _btnRestorePresets = new Button()
            {
                Content = T("Btn_ToolboxRestorePresets", "🔄 Restore Presets"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand
            };
            _btnRestorePresets.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnRestorePresets.Click += BtnRestorePresets_Click;
            wpButtons.Children.Add(_btnRestorePresets);

            _btnOpenJson = new Button()
            {
                Content = T("Btn_ToolboxOpenJson", "📂 Open toolbox.json"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand
            };
            _btnOpenJson.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenJson.Click += BtnOpenJson_Click;
            wpButtons.Children.Add(_btnOpenJson);

            _btnManageCategories = new Button()
            {
                Content = T("Btn_ManageCategories", "🏷️ Manage Categories"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand
            };
            _btnManageCategories.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnManageCategories.Click += (s, e) =>
            {
                RefreshCategoryManagerDropdowns();
                _borderCategoryManagerCard.Visibility = (_borderCategoryManagerCard.Visibility == Visibility.Visible)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
            wpButtons.Children.Add(_btnManageCategories);

            sp.Children.Add(wpButtons);

            _chkEditMode = new CheckBox()
            {
                Content = T("Toolbox_Chk_EditMode", "Enable edit mode"),
                IsChecked = _isEditModeEnabled,
                FontSize = 12,
                Margin = new Thickness(2, 2, 0, 2)
            };
            _chkEditMode.Checked += (s, e) =>
            {
                _isEditModeEnabled = true;
                RebuildCategoriesAccordion();
            };
            _chkEditMode.Unchecked += (s, e) =>
            {
                _isEditModeEnabled = false;
                RebuildCategoriesAccordion();
            };
            sp.Children.Add(_chkEditMode);

            card.Child = sp;
            return card;
        }

        private Border BuildCategoryManagerCard()
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

            // Header row with Title and Close button
            Grid headerGrid = new Grid() { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _txtCatMgrHeader = new TextBlock()
            {
                Text = T("Category_Manager_Title", "🏷️ Manage Categories"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtCatMgrHeader.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(_txtCatMgrHeader, 0);
            headerGrid.Children.Add(_txtCatMgrHeader);

            _btnCloseCategoryManager = new Button()
            {
                Content = T("Btn_Close", "✕ Close"),
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11
            };
            _btnCloseCategoryManager.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCloseCategoryManager.Click += (s, e) => { _borderCategoryManagerCard.Visibility = Visibility.Collapsed; };
            Grid.SetColumn(_btnCloseCategoryManager, 1);
            headerGrid.Children.Add(_btnCloseCategoryManager);
            sp.Children.Add(headerGrid);

            // Section 1: Rename or Delete Existing Category
            Border borderExisting = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            borderExisting.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            borderExisting.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel spExisting = new StackPanel();

            _lblCatMgrSelect = CreateFormLabel(T("Category_Select_Label", "Existing category:"));
            spExisting.Children.Add(_lblCatMgrSelect);

            _cmbManageExistingCategories = new ComboBox()
            {
                Height = 28,
                Margin = new Thickness(0, 2, 0, 8),
                FontSize = 12
            };
            _cmbManageExistingCategories.SelectionChanged += (s, e) =>
            {
                if (_cmbManageExistingCategories.SelectedItem != null)
                {
                    _txtRenameCategory.Text = _cmbManageExistingCategories.SelectedItem.ToString();
                }
            };
            spExisting.Children.Add(_cmbManageExistingCategories);

            _lblCatMgrNewName = CreateFormLabel(T("Category_New_Name", "New category name:"));
            spExisting.Children.Add(_lblCatMgrNewName);

            _txtRenameCategory = CreateFormTextBox();
            spExisting.Children.Add(_txtRenameCategory);

            StackPanel spCatActions = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            _btnRenameCategory = new Button()
            {
                Content = T("Btn_RenameCategory", "✏️ Rename Category"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnRenameCategory.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnRenameCategory.Click += BtnRenameCategory_Click;
            spCatActions.Children.Add(_btnRenameCategory);

            _btnDeleteCategory = new Button()
            {
                Content = T("Btn_DeleteCategory", "🗑️ Delete Category"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5
            };
            _btnDeleteCategory.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeleteCategory.Click += BtnDeleteCategory_Click;
            spCatActions.Children.Add(_btnDeleteCategory);

            spExisting.Children.Add(spCatActions);
            borderExisting.Child = spExisting;
            sp.Children.Add(borderExisting);

            // Section 2: Add New Category
            Border borderNew = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 4)
            };
            borderNew.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            borderNew.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel spNew = new StackPanel();
            _lblCatMgrAdd = CreateFormLabel(T("Category_Add_Label", "Create new category:"));
            spNew.Children.Add(_lblCatMgrAdd);

            Grid gridNew = new Grid() { Margin = new Thickness(0, 2, 0, 0) };
            gridNew.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            gridNew.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _txtAddNewCategory = CreateFormTextBox();
            Grid.SetColumn(_txtAddNewCategory, 0);
            gridNew.Children.Add(_txtAddNewCategory);

            _btnAddCategory = new Button()
            {
                Content = T("Btn_AddCategory", "➕ Create Category"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(8, 0, 0, 0)
            };
            _btnAddCategory.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnAddCategory.Click += BtnAddCategory_Click;
            Grid.SetColumn(_btnAddCategory, 1);
            gridNew.Children.Add(_btnAddCategory);

            spNew.Children.Add(gridNew);
            borderNew.Child = spNew;
            sp.Children.Add(borderNew);

            card.Child = sp;
            return card;
        }

        private void RefreshCategoryManagerDropdowns()
        {
            if (_storage == null) return;
            List<string> cats = _storage.GetDistinctCategories();
            if (_cmbManageExistingCategories != null)
            {
                _cmbManageExistingCategories.Items.Clear();
                foreach (string c in cats) _cmbManageExistingCategories.Items.Add(c);
                if (_cmbManageExistingCategories.Items.Count > 0)
                {
                    _cmbManageExistingCategories.SelectedIndex = 0;
                    if (_txtRenameCategory != null)
                    {
                        _txtRenameCategory.Text = _cmbManageExistingCategories.SelectedItem.ToString();
                    }
                }
                else
                {
                    if (_txtRenameCategory != null) _txtRenameCategory.Text = "";
                }
            }
            if (_cmbFieldCategory != null)
            {
                string curr = _cmbFieldCategory.Text;
                _cmbFieldCategory.Items.Clear();
                foreach (string c in cats) _cmbFieldCategory.Items.Add(c);
                _cmbFieldCategory.Text = curr;
            }
        }

        private void BtnRenameCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_cmbManageExistingCategories == null || _cmbManageExistingCategories.SelectedItem == null)
            {
                MessageBox.Show(T("Msg_SelectCategoryFirst", "Please select an existing category first."), "Toolbox", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string oldName = _cmbManageExistingCategories.SelectedItem.ToString();
            string newName = (_txtRenameCategory.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show(T("Msg_CategoryNameEmpty", "Category name cannot be empty."), "Toolbox", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int count = _storage.RenameCategory(oldName, newName);
            _commands = _storage.LoadCommands();
            RefreshCategoryManagerDropdowns();
            RebuildCategoriesAccordion();
            HostLog(string.Format("[TOOLBOX] Renamed category '{0}' to '{1}' ({2} actions updated).", oldName, newName, count));
            MessageBox.Show(
                string.Format(T("Msg_CategoryRenamed", "Category successfully renamed to '{0}'. {1} action(s) updated."), newName, count),
                "Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_cmbManageExistingCategories == null || _cmbManageExistingCategories.SelectedItem == null)
            {
                MessageBox.Show(T("Msg_SelectCategoryFirst", "Please select an existing category first."), "Toolbox", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cat = _cmbManageExistingCategories.SelectedItem.ToString();
            int matching = 0;
            if (_commands != null)
            {
                foreach (ToolboxCommandItem c in _commands)
                {
                    if (c != null && string.Equals(c.category, cat, StringComparison.OrdinalIgnoreCase)) matching++;
                }
            }

            var res = MessageBox.Show(
                string.Format(T("Msg_CategoryDeleteConfirm", "Are you sure you want to delete category '{0}' and all {1} action(s) in it?"), cat, matching),
                "Toolbox", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            int deleted = _storage.DeleteCategory(cat);
            _commands = _storage.LoadCommands();
            RefreshCategoryManagerDropdowns();
            RebuildCategoriesAccordion();
            HostLog(string.Format("[TOOLBOX] Deleted category '{0}' and its {1} action(s).", cat, deleted));
            MessageBox.Show(
                string.Format(T("Msg_CategoryDeleted", "Category '{0}' and {1} action(s) deleted successfully."), cat, deleted),
                "Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            string newCat = (_txtAddNewCategory.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(newCat))
            {
                MessageBox.Show(T("Msg_CategoryNameEmpty", "Category name cannot be empty."), "Toolbox", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _borderCategoryManagerCard.Visibility = Visibility.Collapsed;
            _borderOptionsCard.Visibility = Visibility.Collapsed;
            OpenEditorForNew();
            _cmbFieldCategory.Text = newCat;
            _txtFieldTitle.Focus();
            MessageBox.Show(
                string.Format(T("Msg_CategoryAdded", "Category '{0}' created! Fill in the first action to save it."), newCat),
                "Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Text = T("Toolbox_Editor_NewTitle", "➕ Add Toolbox"),
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

            // Row D: Checkboxes on Left | Delete button + Save & Cancel Buttons on Right
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
                Content = T("Toolbox_Chk_CaptureOutput", "Stream output to application Logs"),
                IsChecked = true,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 14, 2)
            };
            _chkCaptureOutput.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpChecks.Children.Add(_chkCaptureOutput);

            _chkOpenExternalLogs = new CheckBox()
            {
                Content = T("Toolbox_Chk_OpenExternalLogs", "Open external logs window"),
                IsChecked = true,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 14, 2)
            };
            _chkOpenExternalLogs.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            wpChecks.Children.Add(_chkOpenExternalLogs);

            Grid.SetColumn(wpChecks, 0);
            rowD.Children.Add(wpChecks);

            StackPanel spFormBtns = new StackPanel() { Orientation = Orientation.Horizontal };

            _btnDeleteFromEditor = new Button()
            {
                Content = T("Btn_ToolboxDelete", "🗑️ Delete"),
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnDeleteFromEditor.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeleteFromEditor.Click += (s, e) =>
            {
                if (_editingItem != null)
                {
                    ConfirmAndDeleteItem(_editingItem);
                }
            };
            spFormBtns.Children.Add(_btnDeleteFromEditor);

            _btnSaveCommand = new Button()
            {
                Content = T("Btn_ToolboxSave", "💾 Save"),
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

        private void RebuildCategoriesAccordion()
        {
            if (_spCategoriesHost == null) return;
            _spCategoriesHost.Children.Clear();
            _toolboxChecks.Clear();

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
                    Text = T("Toolbox_Empty_List", "No items found. Click '➕ New Toolbox' to add one or '🔄 Restore Presets'."),
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
            Border sectionContainer = new Border()
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            StackPanel sectionStack = new StackPanel();

            // Category Section Header: Icon + Name + Count Badge + Batch Run All
            Grid headerGrid = new Grid() { Margin = new Thickness(2, 2, 2, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel spHeaderLeft = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock txtCatName = new TextBlock()
            {
                Text = "📂  " + categoryName,
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtCatName.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            spHeaderLeft.Children.Add(txtCatName);

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
            spHeaderLeft.Children.Add(badgeCount);

            Grid.SetColumn(spHeaderLeft, 0);
            headerGrid.Children.Add(spHeaderLeft);

            List<ToolboxCommandItem> batchList = new List<ToolboxCommandItem>(items);
            Button btnRunCategoryBatch = new Button()
            {
                Content = "▶  " + T("Btn_Toolbox_RunCategory", "Run All"),
                Height = 26,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = T("Tooltip_Toolbox_RunCategory", "Execute all commands in this category sequentially")
            };
            btnRunCategoryBatch.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnRunCategoryBatch.Click += (s, e) =>
            {
                if (batchList.Count == 0) return;
                var res = MessageBox.Show(
                    string.Format(T("Msg_Toolbox_ConfirmBatch", "Execute all {0} tasks in category '{1}' sequentially?"), batchList.Count, categoryName),
                    "OptiAssist - Toolbox",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
                ExecuteBatchItems(batchList, 0, btnRunCategoryBatch, btnRunCategoryBatch.Content);
            };
            Grid.SetColumn(btnRunCategoryBatch, 1);
            headerGrid.Children.Add(btnRunCategoryBatch);

            sectionStack.Children.Add(headerGrid);

            foreach (ToolboxCommandItem item in items)
            {
                sectionStack.Children.Add(BuildCommandItemCard(item));
            }

            sectionContainer.Child = sectionStack;
            return sectionContainer;
        }

        private Border BuildCommandItemCard(ToolboxCommandItem item)
        {
            Border itemCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            itemCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            itemCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            CheckBox chk = new CheckBox()
            {
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(chk, 0);
            g.Children.Add(chk);
            _toolboxChecks.Add(new KeyValuePair<CheckBox, ToolboxCommandItem>(chk, item));

            StackPanel spLeft = new StackPanel()
            {
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock txtItemTitle = new TextBlock()
            {
                Text = item.title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            txtItemTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spLeft.Children.Add(txtItemTitle);

            if (!string.IsNullOrWhiteSpace(item.description))
            {
                TextBlock txtDesc = new TextBlock()
                {
                    Text = item.description,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                txtDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                spLeft.Children.Add(txtDesc);
            }

            Grid.SetColumn(spLeft, 1);
            g.Children.Add(spLeft);

            StackPanel spActions = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            ToolboxCommandItem capturedItem = item;

            Button btnRun = new Button()
            {
                Content = T("Btn_ToolboxRun", "▶ Run"),
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                ToolTip = T("Tooltip_ToolboxRun", "Execute this action")
            };
            btnRun.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnRun.Click += (s, e) => ExecuteCommandItem(capturedItem, btnRun);
            spActions.Children.Add(btnRun);

            Button btnEdit = new Button()
            {
                Content = "✏️",
                Width = 30,
                Height = 28,
                FontSize = 12,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = T("Tooltip_Edit", "Edit"),
                Visibility = _isEditModeEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditorForItem(capturedItem);
            spActions.Children.Add(btnEdit);

            Grid.SetColumn(spActions, 2);
            g.Children.Add(spActions);

            itemCard.Child = g;
            return itemCard;
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

            _txtEditorHeader.Text = T("Toolbox_Editor_NewTitle", "➕ Add Toolbox");
            _txtFieldTitle.Text = "";
            _cmbFieldCategory.Text = "Otimização";
            _cmbFieldShell.SelectedIndex = 0;
            _txtFieldCommand.Text = "";
            _txtFieldDescription.Text = "";
            _txtFieldTimeout.Text = "0";
            _chkWaitForExit.IsChecked = true;
            _chkCaptureOutput.IsChecked = true;
            _chkOpenExternalLogs.IsChecked = true;

            if (_btnDeleteFromEditor != null)
            {
                _btnDeleteFromEditor.Visibility = Visibility.Collapsed;
            }

            _borderEditorCard.Visibility = Visibility.Visible;
            _txtFieldTitle.Focus();
        }

        private void OpenEditorForItem(ToolboxCommandItem item)
        {
            if (item == null) return;
            _editingItem = item;
            PopulateCategoryComboChoices();

            _txtEditorHeader.Text = T("Toolbox_Editor_EditTitle", "✏️ Edit Toolbox");
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
            _chkOpenExternalLogs.IsChecked = item.open_external_logs;

            if (_btnDeleteFromEditor != null)
            {
                _btnDeleteFromEditor.Visibility = Visibility.Visible;
            }

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
                open_external_logs = _chkOpenExternalLogs.IsChecked == true,
                requires_admin = true
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
                HostLog(string.Format("[TOOLBOX] Saved action '{0}' ({1}) in category '{2}'.",
                    candidate.title, candidate.shell, candidate.category));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to save action: " + ex.Message,
                    "OptiAssist - Toolbox Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ConfirmAndDeleteItem(ToolboxCommandItem item)
        {
            if (item == null) return;
            string confirmMsg = string.Format(
                T("Msg_ToolboxDeleteConfirm", "Are you sure you want to delete '{0}'?"),
                item.title);

            if (MessageBox.Show(confirmMsg, "OptiAssist - Toolbox", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _commands = _storage.DeleteCommand(item.id);
                CloseEditor();
                RebuildCategoriesAccordion();
                HostLog(string.Format("[TOOLBOX] Deleted action '{0}' (id: {1}).", item.title, item.id));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting action: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestorePresets_Click(object sender, RoutedEventArgs e)
        {
            string msg = T("Msg_ToolboxRestoreConfirm",
                "Do you want to restore the default Toolbox presets?\nThis will reset toolbox.json to the factory preset list.");
            if (MessageBox.Show(msg, "OptiAssist - Toolbox", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _commands = _storage.RestoreDefaultPresets();
            CloseEditor();
            RebuildCategoriesAccordion();
            HostLog("[TOOLBOX] Default presets restored.");
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

        private Border CreateTabButton(string text, bool isActive, out TextBlock lbl)
        {
            Border b = new Border()
            {
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(0, 0, 0, 2.5),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            lbl = new TextBlock()
            {
                Text = text,
                FontSize = 12,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold
            };
            if (isActive)
            {
                b.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            }
            else
            {
                b.BorderBrush = Brushes.Transparent;
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            }
            b.Child = lbl;
            return b;
        }

        private void SwitchToolboxTab(bool isMainToolbox)
        {
            if (_pnlMainToolboxView != null) _pnlMainToolboxView.Visibility = isMainToolbox ? Visibility.Visible : Visibility.Collapsed;
            if (_pnlDebloaterView != null) _pnlDebloaterView.Visibility = isMainToolbox ? Visibility.Collapsed : Visibility.Visible;
            if (_btnOptions != null) _btnOptions.Visibility = isMainToolbox ? Visibility.Visible : Visibility.Collapsed;

            if (_tabToolboxBorder != null && _tabToolboxText != null)
            {
                if (isMainToolbox)
                {
                    _tabToolboxBorder.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
                    _tabToolboxText.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                    _tabToolboxText.FontWeight = FontWeights.Bold;
                }
                else
                {
                    _tabToolboxBorder.BorderBrush = Brushes.Transparent;
                    _tabToolboxText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                    _tabToolboxText.FontWeight = FontWeights.SemiBold;
                }
            }

            if (_tabDebloaterBorder != null && _tabDebloaterText != null)
            {
                if (!isMainToolbox)
                {
                    _tabDebloaterBorder.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
                    _tabDebloaterText.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                    _tabDebloaterText.FontWeight = FontWeights.Bold;
                }
                else
                {
                    _tabDebloaterBorder.BorderBrush = Brushes.Transparent;
                    _tabDebloaterText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                    _tabDebloaterText.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private ScrollViewer BuildDebloaterTab()
        {
            _debloatChecks.Clear();
            ScrollViewer sv = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            sv.PreviewMouseWheel += (s, e) =>
            {
                if (!e.Handled && sv.ScrollableHeight > 0)
                {
                    e.Handled = true;
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / 2.5));
                }
            };

            StackPanel root = new StackPanel() { Margin = new Thickness(0, 0, 0, 8) };

            // Header Card with Batch Controls
            Border topCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            topCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            topCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel infoStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            TextBlock lblTitle = new TextBlock()
            {
                Text = "🧹 " + T("Debloater_Title", "Windows Debloater"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold
            };
            lblTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            TextBlock lblSub = new TextBlock()
            {
                Text = T("Debloater_Subtitle", "Select telemetry, bloatware, AI, and browser tweaks built directly into the module and apply them individually or in batch."),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            lblSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            infoStack.Children.Add(lblTitle);
            infoStack.Children.Add(lblSub);
            Grid.SetColumn(infoStack, 0);
            topGrid.Children.Add(infoStack);

            StackPanel btnPanel = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Button btnSelectAll = new Button() { Content = T("Btn_SelectAll", "Select All"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
            btnSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnSelectAll.Click += (s, e) => { foreach (var kv in _debloatChecks) kv.Key.IsChecked = true; };

            Button btnClearAll = new Button() { Content = T("Btn_ClearAll", "Clear"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
            btnClearAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnClearAll.Click += (s, e) => { foreach (var kv in _debloatChecks) kv.Key.IsChecked = false; };

            Button btnApplySelected = new Button() { Content = "🚀 " + T("Debloater_ApplySelected", "Apply Selected"), Height = 28, Padding = new Thickness(14, 0, 14, 0), FontSize = 11, FontWeight = FontWeights.Bold };
            btnApplySelected.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnApplySelected.Click += (s, e) =>
            {
                List<ToolboxCommandItem> selected = new List<ToolboxCommandItem>();
                foreach (var kv in _debloatChecks)
                {
                    if (kv.Key.IsChecked == true) selected.Add(kv.Value);
                }
                if (selected.Count == 0)
                {
                    MessageBox.Show(T("Debloater_NoneSelected", "Select at least one debloat tweak to apply."), "OptiAssist - Windows Debloater", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var confirm = MessageBox.Show(
                    string.Format(T("Debloater_ConfirmBatch", "Apply {0} selected Windows Debloater tweaks sequentially?"), selected.Count),
                    "OptiAssist - Windows Debloater",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
                ExecuteBatchItems(selected, 0, btnApplySelected, btnApplySelected.Content);
            };

            btnPanel.Children.Add(btnSelectAll);
            btnPanel.Children.Add(btnClearAll);
            btnPanel.Children.Add(btnApplySelected);
            Grid.SetColumn(btnPanel, 1);
            topGrid.Children.Add(btnPanel);

            StackPanel topCardStack = new StackPanel();
            topCardStack.Children.Add(topGrid);

            WrapPanel presetBar = new WrapPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };
            TextBlock lblPresets = new TextBlock()
            {
                Text = T("Debloater_Presets_Label", "Presets:"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            lblPresets.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            presetBar.Children.Add(lblPresets);

            Button btnPresetLaptop = new Button()
            {
                Content = "💻 " + T("Debloater_Preset_Laptop", "Laptop de Entrada"),
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnPresetLaptop.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnPresetLaptop.Click += (s, e) => ApplyDebloaterPreset(new string[]
            {
                "dll-debloat-consumer-features",
                "dll-debloat-telemetry",
                "dll-debloat-widgets",
                "dll-debloat-wpbt",
                "dll-debloat-activity-history",
                "dll-debloat-bg-apps",
                "dll-debloat-windows-ai",
                "dll-debloat-bing-search"
            });
            presetBar.Children.Add(btnPresetLaptop);

            Button btnPresetGaming = new Button()
            {
                Content = "🎮 " + T("Debloater_Preset_Gaming", "PC Gamer"),
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnPresetGaming.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnPresetGaming.Click += (s, e) => ApplyDebloaterPreset(new string[]
            {
                "dll-debloat-consumer-features",
                "dll-debloat-telemetry",
                "dll-debloat-widgets",
                "dll-debloat-wpbt",
                "dll-debloat-bg-apps",
                "dll-debloat-windows-ai",
                "dll-debloat-bing-search",
                "dll-debloat-edge"
            });
            presetBar.Children.Add(btnPresetGaming);

            Button btnPresetConventional = new Button()
            {
                Content = "🖥️ " + T("Debloater_Preset_Conventional", "Convencional"),
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11
            };
            btnPresetConventional.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnPresetConventional.Click += (s, e) => ApplyDebloaterPreset(new string[]
            {
                "dll-debloat-consumer-features",
                "dll-debloat-telemetry",
                "dll-debloat-widgets",
                "dll-debloat-activity-history",
                "dll-debloat-bing-search",
                "dll-debloat-edge"
            });
            presetBar.Children.Add(btnPresetConventional);

            topCardStack.Children.Add(presetBar);
            topCard.Child = topCardStack;
            root.Children.Add(topCard);

            // Built-in Windows Debloater Tweaks (directly in Toolbox.dll)
            List<ToolboxCommandItem> builtInTweaks = GetBuiltInDebloatTweaks();
            foreach (ToolboxCommandItem tweak in builtInTweaks)
            {
                Border card = new Border()
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
                card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                CheckBox chk = new CheckBox()
                {
                    IsChecked = false,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(chk, 0);
                row.Children.Add(chk);

                StackPanel textCol = new StackPanel() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                TextBlock titleTxt = new TextBlock() { Text = tweak.title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                titleTxt.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                TextBlock descTxt = new TextBlock() { Text = tweak.description, FontSize = 11, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
                descTxt.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                textCol.Children.Add(titleTxt);
                textCol.Children.Add(descTxt);
                Grid.SetColumn(textCol, 1);
                row.Children.Add(textCol);

                Button btnApplyOne = new Button()
                {
                    Content = "▶ " + T("Btn_Apply", "Apply"),
                    Height = 28,
                    Padding = new Thickness(12, 0, 12, 0),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnApplyOne.SetResourceReference(Button.StyleProperty, "BtnDefault");
                ToolboxCommandItem captured = tweak;
                btnApplyOne.Click += (s, e) => ExecuteCommandItem(captured, btnApplyOne);
                Grid.SetColumn(btnApplyOne, 2);
                row.Children.Add(btnApplyOne);

                card.Child = row;
                root.Children.Add(card);
                _debloatChecks.Add(new KeyValuePair<CheckBox, ToolboxCommandItem>(chk, tweak));
            }

            sv.Content = root;
            return sv;
        }

        private void ApplyDebloaterPreset(string[] targetIds)
        {
            HashSet<string> set = new HashSet<string>(targetIds, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _debloatChecks)
            {
                kv.Key.IsChecked = (kv.Value != null && set.Contains(kv.Value.id));
            }
        }

        private List<ToolboxCommandItem> GetBuiltInDebloatTweaks()
        {
            var list = new List<ToolboxCommandItem>();
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-consumer-features",
                title = T("Debloat_ConsumerFeatures_Title", "Disable Consumer Features & Store Suggestions"),
                description = T("Debloat_ConsumerFeatures_Desc", "Prevents automatic installation of sponsored apps and Microsoft Store suggestions"),
                command = "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-telemetry",
                title = T("Debloat_Telemetry_Title", "Disable Full Windows Telemetry & Data Collection"),
                description = T("Debloat_Telemetry_Desc", "Disables diagnostic reporting, advertising ID, tailored experiences, and DiagTrack/wermgr services"),
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$paths = @('HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo','HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy','HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'); foreach ($p in $paths) { if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null } }; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection' -Name AllowTelemetry -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -Value 0 -Type DWord -Force; Get-Service diagtrack, wermgr -ErrorAction SilentlyContinue | Set-Service -StartupType Disabled -ErrorAction SilentlyContinue; Stop-Service diagtrack, wermgr -Force -ErrorAction SilentlyContinue; Write-Host 'Telemetry disabled!'\"",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-widgets",
                title = T("Debloat_Widgets_Title", "Remove Taskbar Widgets (WebExperience)"),
                description = T("Debloat_Widgets_Desc", "Terminates and uninstalls Widgets and WebExperience packages to free up RAM"),
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-Process *Widget* -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Get-AppxPackage Microsoft.WidgetsPlatformRuntime -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Get-AppxPackage MicrosoftWindows.Client.WebExperience -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Write-Host 'Widgets removed!'\"",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-wpbt",
                title = T("Debloat_Wpbt_Title", "Disable WPBT Execution (BIOS/OEM Bloatware)"),
                description = T("Debloat_Wpbt_Desc", "Blocks automatic execution of OEM binaries and drivers injected via ACPI WPBT table at boot"),
                command = "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\" /v DisableWpbtExecution /t REG_DWORD /d 1 /f",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-activity-history",
                title = T("Debloat_ActivityHistory_Title", "Disable User Activity History"),
                description = T("Debloat_ActivityHistory_Desc", "Prevents Windows from publishing or uploading user activity history"),
                command = "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /t REG_DWORD /d 0 /f && reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v UploadUserActivities /t REG_DWORD /d 0 /f",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-bg-apps",
                title = T("Debloat_BgApps_Title", "Disable Background Apps"),
                description = T("Debloat_BgApps_Desc", "Globally disables background execution for Microsoft Store applications"),
                command = "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications\" /v GlobalUserDisabled /t REG_DWORD /d 1 /f",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-windows-ai",
                title = T("Debloat_WindowsAI_Title", "Disable & Remove Windows AI Features (Copilot / Recall)"),
                description = T("Debloat_WindowsAI_Desc", "Removes Copilot, hides AI components in Settings, disables Recall and Notepad AI features"),
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"reg add \\\"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\\" /v SettingsPageVisibility /t REG_SZ /d \\\"hide:aicomponents\\\" /f; reg add \\\"HKLM\\SOFTWARE\\Policies\\WindowsNotepad\\\" /v DisableAIFeatures /t REG_DWORD /d 1 /f; Get-AppxPackage -AllUsers *Copilot* -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Disable-WindowsOptionalFeature -FeatureName Recall -Online -NoRestart -ErrorAction SilentlyContinue; Write-Host 'AI features disabled!'\"",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 120
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-bing-search",
                title = T("Debloat_BingSearch_Title", "Disable Bing Web Search in Windows Search Bar"),
                description = T("Debloat_BingSearch_Desc", "Removes Bing web results from the Start Menu, speeding up local file searches"),
                command = "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 0 /f && reg add \"HKCU\\Software\\Policies\\Microsoft\\Windows\\Explorer\" /v DisableSearchBoxSuggestions /t REG_DWORD /d 1 /f",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-edge",
                title = T("Debloat_Edge_Title", "Debloat Microsoft Edge (Shopping, Telemetry & Popups)"),
                description = T("Debloat_Edge_Desc", "Disables shopping assistant, first-run popups, promotions, widgets, and telemetry in Edge"),
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$edge = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge'; if (-not (Test-Path $edge)) { New-Item -Path $edge -Force | Out-Null }; @('PersonalizationReportingEnabled','ShowRecommendationsEnabled','UserFeedbackAllowed','AlternateErrorPagesEnabled','EdgeCollectionsEnabled','EdgeShoppingAssistantEnabled','MicrosoftEdgeInsiderPromotionEnabled','ShowMicrosoftRewards','WebWidgetAllowed','DiagnosticData') | ForEach-Object { Set-ItemProperty -Path $edge -Name $_ -Value 0 -Type DWord -Force }; @('HideFirstRunExperience','ConfigureDoNotTrack') | ForEach-Object { Set-ItemProperty -Path $edge -Name $_ -Value 1 -Type DWord -Force }; Write-Host 'Edge debloat applied!'\"",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            list.Add(new ToolboxCommandItem
            {
                id = "dll-debloat-brave",
                title = T("Debloat_Brave_Title", "Debloat Brave Browser (Crypto, VPN & Leo AI)"),
                description = T("Debloat_Brave_Desc", "Applies corporate policies in Brave to disable crypto wallet, VPN, Leo AI, and telemetry"),
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$path = 'HKLM:\\SOFTWARE\\Policies\\BraveSoftware\\Brave'; if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }; @('BraveRewardsDisabled','BraveWalletDisabled','BraveVPNDisabled','BraveNewsDisabled','BraveTalkDisabled','TorDisabled') | ForEach-Object { Set-ItemProperty -Path $path -Name $_ -Value 1 -Type DWord -Force }; @('BraveAIChatEnabled','BraveStatsPingEnabled','BraveP3AEnabled','MetricsReportingEnabled') | ForEach-Object { Set-ItemProperty -Path $path -Name $_ -Value 0 -Type DWord -Force }; Write-Host 'Brave debloat applied!'\"",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });
            return list;
        }

        private void ExecuteBatchItems(List<ToolboxCommandItem> queue, int index, Button triggerBtn, object originalContent)
        {
            if (queue == null || index >= queue.Count)
            {
                if (triggerBtn != null)
                {
                    triggerBtn.IsEnabled = true;
                    triggerBtn.Content = originalContent;
                }
                HostLog("[TOOLBOX BATCH] ✔️ Batch execution completed.");
                return;
            }

            if (index == 0)
            {
                if (_runner != null && _runner.IsRunning)
                {
                    MessageBox.Show(T("Msg_ToolboxBusy", "A Toolbox action is already running in background. Please wait for it to complete."), "OptiAssist - Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (_host != null) _host.OpenLogWindow();
                if (triggerBtn != null) triggerBtn.IsEnabled = false;
            }

            ToolboxCommandItem item = queue[index];
            if (triggerBtn != null)
            {
                triggerBtn.Content = string.Format("⏳ {0}/{1}", index + 1, queue.Count);
            }
            HostLog(string.Format("[TOOLBOX BATCH {0}/{1}] Starting '{2}'...", index + 1, queue.Count, item.title));

            _runner.RunAsync(
                item,
                (line, isErr) =>
                {
                    if (item.capture_output)
                    {
                        HostLog(isErr ? "[TOOLBOX:STDERR] " + line : "[TOOLBOX] " + line);
                    }
                },
                (result) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        HostLog(result.Success
                            ? string.Format("[TOOLBOX BATCH {0}/{1}] ✔️ '{2}' OK ({3} ms).", index + 1, queue.Count, item.title, result.ElapsedMilliseconds)
                            : string.Format("[TOOLBOX BATCH {0}/{1}] ⚠️ '{2}' Exit Code {3}.", index + 1, queue.Count, item.title, result.ExitCode));
                        ExecuteBatchItems(queue, index + 1, triggerBtn, originalContent);
                    }));
                });
        }

        #region COMMAND EXECUTION

        private void ExecuteCommandItem(ToolboxCommandItem item, Button runButton)
        {
            if (item == null) return;
            if (_runner != null && _runner.IsRunning)
            {
                MessageBox.Show(
                    T("Msg_ToolboxBusy", "A Toolbox action is already running in background. Please wait for it to complete."),
                    "OptiAssist - Toolbox",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (_host != null && item.open_external_logs)
            {
                _host.OpenLogWindow();
            }

            string headerLine = string.Format("[TOOLBOX] Starting '{0}' ({1}: {2})...",
                item.title, item.shell.ToUpperInvariant(), item.command);
            HostLog(headerLine);

            object originalContent = runButton != null ? runButton.Content : null;
            if (runButton != null)
            {
                runButton.IsEnabled = false;
                runButton.Content = "⏳...";
            }

            _runner.RunAsync(
                item,
                (line, isErr) =>
                {
                    if (item.capture_output)
                    {
                        string formatted = isErr ? "[TOOLBOX:STDERR] " + line : "[TOOLBOX] " + line;
                        HostLog(formatted);
                    }
                },
                (result) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (runButton != null)
                        {
                            runButton.IsEnabled = true;
                            runButton.Content = originalContent;
                        }

                        if (result.LaunchedInBackground)
                        {
                            HostLog(string.Format("[TOOLBOX] '{0}' launched in background (PID: {1}).", item.title, result.ProcessId));
                        }
                        else if (result.Success)
                        {
                            HostLog(string.Format("[TOOLBOX] ✔️ '{0}' finished successfully (Exit Code: 0 in {1} ms).", item.title, result.ElapsedMilliseconds));
                        }
                        else
                        {
                            HostLog(string.Format("[TOOLBOX] ❌ '{0}' finished with Exit Code: {1} ({2} ms). {3}",
                                item.title, result.ExitCode, result.ElapsedMilliseconds, result.ErrorMessage ?? ""));
                        }
                    }));
                });
        }

        #endregion

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = string.Format("{0}  {1}", Icon, Name);
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Toolbox_Subtitle", "Custom actions and system tools integrated into OptiAssist");
            if (_txtSearchWatermark != null) _txtSearchWatermark.Text = T("Toolbox_Search_Placeholder", "🔍 Type here to search for the tool, category, or command you want...");
            if (_txtSearch != null) _txtSearch.ToolTip = T("Toolbox_Search_Placeholder", "🔍 Type here to search for the tool, category, or command you want...");
            if (_btnOptions != null) _btnOptions.Content = T("Btn_ToolboxOptions", "⚙️ Options");
            if (_txtOptionsTitle != null) _txtOptionsTitle.Text = T("Toolbox_Options_Title", "⚙️ Toolbox Options");
            if (_btnNewCommand != null) _btnNewCommand.Content = T("Btn_ToolboxNew", "➕ New Toolbox");
            if (_btnRestorePresets != null) _btnRestorePresets.Content = T("Btn_ToolboxRestorePresets", "🔄 Restore Presets");
            if (_btnOpenJson != null) _btnOpenJson.Content = T("Btn_ToolboxOpenJson", "📂 Open toolbox.json");
            if (_btnManageCategories != null) _btnManageCategories.Content = T("Btn_ManageCategories", "🏷️ Manage Categories");
            if (_chkEditMode != null) _chkEditMode.Content = T("Toolbox_Chk_EditMode", "Enable edit mode");

            if (_txtCatMgrHeader != null) _txtCatMgrHeader.Text = T("Category_Manager_Title", "🏷️ Manage Categories");
            if (_lblCatMgrSelect != null) _lblCatMgrSelect.Text = T("Category_Select_Label", "Existing category:");
            if (_lblCatMgrNewName != null) _lblCatMgrNewName.Text = T("Category_New_Name", "New category name:");
            if (_btnRenameCategory != null) _btnRenameCategory.Content = T("Btn_RenameCategory", "✏️ Rename Category");
            if (_btnDeleteCategory != null) _btnDeleteCategory.Content = T("Btn_DeleteCategory", "🗑️ Delete Category");
            if (_lblCatMgrAdd != null) _lblCatMgrAdd.Text = T("Category_Add_Label", "Create new category:");
            if (_btnAddCategory != null) _btnAddCategory.Content = T("Btn_AddCategory", "➕ Create Category");
            if (_btnCloseCategoryManager != null) _btnCloseCategoryManager.Content = T("Btn_Close", "✕ Close");

            if (_lblFieldTitle != null) _lblFieldTitle.Text = T("Toolbox_Field_Title", "Title * :");
            if (_lblFieldCategory != null) _lblFieldCategory.Text = T("Toolbox_Field_Category", "Category * :");
            if (_lblFieldShell != null) _lblFieldShell.Text = T("Toolbox_Field_Shell", "Shell / Interpreter * :");
            if (_lblFieldCommand != null) _lblFieldCommand.Text = T("Toolbox_Field_Command", "Command or Script * :");
            if (_lblFieldTimeout != null) _lblFieldTimeout.Text = T("Toolbox_Field_Timeout", "Timeout (sec, 0=none):");
            if (_lblFieldDescription != null) _lblFieldDescription.Text = T("Toolbox_Field_Description", "Description (optional):");
            if (_chkWaitForExit != null) _chkWaitForExit.Content = T("Toolbox_Chk_WaitForExit", "Wait for exit (Synchronous)");
            if (_chkCaptureOutput != null) _chkCaptureOutput.Content = T("Toolbox_Chk_CaptureOutput", "Stream output to application Logs");
            if (_chkOpenExternalLogs != null) _chkOpenExternalLogs.Content = T("Toolbox_Chk_OpenExternalLogs", "Open external logs window");
            if (_btnDeleteFromEditor != null) _btnDeleteFromEditor.Content = T("Btn_ToolboxDelete", "🗑️ Delete");
            if (_btnSaveCommand != null) _btnSaveCommand.Content = T("Btn_ToolboxSave", "💾 Save");
            if (_btnCancelEdit != null) _btnCancelEdit.Content = T("Btn_Cancel", "✕ Cancel");

            RebuildCategoriesAccordion();
            if (_tabToolboxText != null) _tabToolboxText.Text = "🧰  " + T("Toolbox_Tab_Main", "Toolbox");
            if (_tabDebloaterText != null) _tabDebloaterText.Text = "🧹  " + T("Toolbox_Tab_Debloater", "Windows Debloater");
            if (_pnlDebloaterView != null)
            {
                ScrollViewer refreshed = BuildDebloaterTab();
                _pnlDebloaterView.Content = refreshed.Content;
            }
        }
    }
}
