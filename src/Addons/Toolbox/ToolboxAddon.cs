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

        // Top Header & Toolbar controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private TextBox _txtSearch;
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
        public string Version { get { return "1.1.0.0"; } }
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
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Search Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 2: Options Panel
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 3: Category Manager Panel
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 4: CRUD Form Modal/Panel
            rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 5: Categories Accordion

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
            // ROW 1: SEARCH & FILTER TOOLBAR
            // =====================================================================
            Border toolbarCard = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
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
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            spSearch.Children.Add(lblSearchIcon);

            _txtSearch = new TextBox()
            {
                Width = 280,
                Height = 28,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                ToolTip = T("Toolbox_Search_Tooltip", "Filter actions by title, category, or command")
            };
            _txtSearch.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
            _txtSearch.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            _txtSearch.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            _txtSearch.TextChanged += (s, e) => RebuildCategoriesAccordion();
            spSearch.Children.Add(_txtSearch);
            Grid.SetColumn(spSearch, 0);
            toolbarGrid.Children.Add(spSearch);

            toolbarCard.Child = toolbarGrid;
            Grid.SetRow(toolbarCard, 1);
            rootGrid.Children.Add(toolbarCard);

            // =====================================================================
            // ROW 2: OPTIONS PANEL
            // =====================================================================
            _borderOptionsCard = BuildOptionsCard();
            _borderOptionsCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderOptionsCard, 2);
            rootGrid.Children.Add(_borderOptionsCard);

            // =====================================================================
            // ROW 3: CATEGORY MANAGER PANEL
            // =====================================================================
            _borderCategoryManagerCard = BuildCategoryManagerCard();
            _borderCategoryManagerCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderCategoryManagerCard, 3);
            rootGrid.Children.Add(_borderCategoryManagerCard);

            // =====================================================================
            // ROW 4: CRUD ADD / EDIT FORM MODAL PANEL (COLLAPSIBLE)
            // =====================================================================
            _borderEditorCard = BuildCrudEditorCard();
            _borderEditorCard.Visibility = Visibility.Collapsed;
            Grid.SetRow(_borderEditorCard, 4);
            rootGrid.Children.Add(_borderEditorCard);

            // =====================================================================
            // ROW 5: DYNAMIC CATEGORIES ACCORDION VIEW
            // =====================================================================
            ScrollViewer svCategories = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _spCategoriesHost = new StackPanel();
            svCategories.Content = _spCategoriesHost;

            Grid.SetRow(svCategories, 5);
            rootGrid.Children.Add(svCategories);

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
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            itemCard.SetResourceReference(Border.BackgroundProperty, "BrushBgWindow");
            itemCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            // Left: Title + Description only (Clean integrated native look, no technical badges or raw command preview)
            StackPanel spLeft = new StackPanel()
            {
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock txtItemTitle = new TextBlock()
            {
                Text = item.title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            txtItemTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spLeft.Children.Add(txtItemTitle);

            if (!string.IsNullOrWhiteSpace(item.description))
            {
                TextBlock txtDesc = new TextBlock()
                {
                    Text = item.description,
                    FontSize = 11.5,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                txtDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                spLeft.Children.Add(txtDesc);
            }

            Grid.SetColumn(spLeft, 0);
            g.Children.Add(spLeft);

            // Right: Execute and Edit Buttons Only
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
                ToolTip = T("Tooltip_ToolboxRun", "Execute this action")
            };
            btnRun.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnRun.Click += (s, e) => ExecuteCommandItem(capturedItem, btnRun);
            spActions.Children.Add(btnRun);

            Button btnEdit = new Button()
            {
                Content = "✏️",
                Width = 32,
                Height = 30,
                FontSize = 12,
                ToolTip = T("Tooltip_Edit", "Edit"),
                Visibility = _isEditModeEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditorForItem(capturedItem);
            spActions.Children.Add(btnEdit);

            Grid.SetColumn(spActions, 1);
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
        }
    }
}
