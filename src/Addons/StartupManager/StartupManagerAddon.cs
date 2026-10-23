using System;
using System.IO;
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

        private enum StartupTab { Programs, Tasks, Services }
        private StartupTab _activeTab = StartupTab.Programs;

        // Cached datasets
        private List<StartupProgramItem> _allPrograms = new List<StartupProgramItem>();
        private List<StartupTaskItem> _allTasks = new List<StartupTaskItem>();
        private List<StartupServiceItem> _allServices = new List<StartupServiceItem>();

        // UI Header controls
        private TextBlock _txtHeaderTitle;
        private TextBlock _txtHeaderSubtitle;
        private Button _btnNewProgram;
        private Button _btnRefresh;
        private TextBox _txtSearchFilter;
        private CheckBox _chkHideMicrosoft;

        // Tabs Controls
        private Border _tabProgramsBorder;
        private TextBlock _tabProgramsText;
        private Border _tabTasksBorder;
        private TextBlock _tabTasksText;
        private Border _tabServicesBorder;
        private TextBlock _tabServicesText;

        // Container & Stats
        private TextBlock _txtStatsSummary;
        private StackPanel _pnlItemsList;

        // Modal Add/Edit
        private Grid _modalOverlay;
        private TextBlock _txtModalTitle;
        private TextBox _txtModalName;
        private TextBox _txtModalPath;
        private ComboBox _cmbModalScope;
        private Button _btnModalBrowse;
        private Button _btnModalSave;
        private Button _btnModalCancel;
        private StartupProgramItem _editingProgram = null;

        public string Id { get { return "startup-manager"; } }
        public string Name { get { return T("Nav_StartupManager", "Startup Manager"); } }
        public string Description { get { return T("Startup_Subtitle", "Monitore, habilite, desabilite ou remova programas, tarefas agendadas e serviços de inicialização"); } }
        public string Version { get { return "1.1.0.0"; } }
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

        public FrameworkElement CreateView()
        {
            if (_rootView != null) return _rootView;

            Grid root = new Grid();

            // Main Content Area inside ScrollViewer
            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 20, 24, 24)
            };

            StackPanel mainStack = new StackPanel { MaxWidth = 1100 };

            // 1. Header (Rocket Icon + Title + Subtitle)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel();
            StackPanel spTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock icoTb = new TextBlock
            {
                Text = "🚀",
                FontSize = 26,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtHeaderTitle = new TextBlock
            {
                Text = T("Startup_Title", "Gerenciador de Inicialização do Sistema"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtHeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTitleRow.Children.Add(icoTb);
            spTitleRow.Children.Add(_txtHeaderTitle);

            _txtHeaderSubtitle = new TextBlock
            {
                Text = T("Startup_Subtitle", "Monitore, habilite, desabilite ou remova programas, tarefas agendadas e serviços de inicialização"),
                FontSize = 11.5,
                Margin = new Thickness(36, 4, 0, 0)
            };
            _txtHeaderSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            titlePanel.Children.Add(spTitleRow);
            titlePanel.Children.Add(_txtHeaderSubtitle);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            // Right header buttons
            StackPanel topActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            _btnNewProgram = new Button
            {
                Content = T("Startup_Btn_New", "➕ Adicionar Programa"),
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _btnNewProgram.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnNewProgram.Click += (s, e) => OpenAddProgramModal();
            topActions.Children.Add(_btnNewProgram);

            _btnRefresh = new Button
            {
                Content = T("Startup_Btn_Refresh", "🔄 Atualizar"),
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
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
            _tabProgramsBorder = CreateTabButton("🚀 " + T("Startup_Tab_Programs", "Programas de Inicialização"), () => SwitchTab(StartupTab.Programs), out _tabProgramsText);
            _tabTasksBorder = CreateTabButton("⏰ " + T("Startup_Tab_Tasks", "Tarefas Agendadas"), () => SwitchTab(StartupTab.Tasks), out _tabTasksText);
            _tabServicesBorder = CreateTabButton("⚙️ " + T("Startup_Tab_Services", "Serviços de Inicialização"), () => SwitchTab(StartupTab.Services), out _tabServicesText);

            spTabs.Children.Add(_tabProgramsBorder);
            spTabs.Children.Add(_tabTasksBorder);
            spTabs.Children.Add(_tabServicesBorder);
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
            _txtSearchFilter.ToolTip = T("Startup_Filter_Placeholder", "🔍 Filtrar itens por nome, comando ou fornecedor...");
            _txtSearchFilter.TextChanged += (s, e) => RenderCurrentTabList();
            Grid.SetColumn(_txtSearchFilter, 0);
            filterGrid.Children.Add(_txtSearchFilter);

            _chkHideMicrosoft = new CheckBox
            {
                Content = T("Startup_Chk_HideMicrosoft", "Ocultar itens nativos da Microsoft / Windows"),
                FontSize = 11,
                IsChecked = true,
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            _chkHideMicrosoft.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            _chkHideMicrosoft.Checked += (s, e) => ReloadActiveTabData();
            _chkHideMicrosoft.Unchecked += (s, e) => ReloadActiveTabData();
            Grid.SetColumn(_chkHideMicrosoft, 1);
            filterGrid.Children.Add(_chkHideMicrosoft);

            filterCard.Child = filterGrid;
            mainStack.Children.Add(filterCard);

            // 4. Stats Summary Bar
            _txtStatsSummary = new TextBlock
            {
                Text = "Carregando...",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 0, 10)
            };
            _txtStatsSummary.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            mainStack.Children.Add(_txtStatsSummary);

            // 5. Items List Panel
            _pnlItemsList = new StackPanel();
            mainStack.Children.Add(_pnlItemsList);

            sv.Content = mainStack;
            root.Children.Add(sv);

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
            HighlightTab(_tabServicesBorder, _tabServicesText, tab == StartupTab.Services);

            // Add program button only relevant for Tab 1 (Programs)
            if (_btnNewProgram != null)
            {
                _btnNewProgram.Visibility = (tab == StartupTab.Programs) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Hide Microsoft checkbox only applies to Tasks and Services
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
                Text = "⏳ " + T("Cleaner_Status_Scanning", "Carregando dados do sistema..."),
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
                else if (_activeTab == StartupTab.Services)
                {
                    _allServices = StartupEngine.GetStartupServices(hideMs);
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
            else if (_activeTab == StartupTab.Services)
            {
                RenderServicesList(query);
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
                    T("Startup_Stats_Programs", "Total: {0} programas • {1} ativados • {2} desativados"),
                    total, enabledCount, disabledCount);
            }

            List<StartupProgramItem> filtered = new List<StartupProgramItem>();
            foreach (var p in _allPrograms)
            {
                if (string.IsNullOrEmpty(query) ||
                    (p.Name != null && p.Name.ToLowerInvariant().Contains(query)) ||
                    (p.Publisher != null && p.Publisher.ToLowerInvariant().Contains(query)) ||
                    (p.Command != null && p.Command.ToLowerInvariant().Contains(query)))
                {
                    filtered.Add(p);
                }
            }

            if (filtered.Count == 0)
            {
                _pnlItemsList.Children.Add(CreateEmptyPlaceholder(T("Startup_Empty_Programs", "Nenhum programa de inicialização encontrado com o filtro atual.")));
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
            TextBlock tbName = new TextBlock
            {
                Text = prog.Name,
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
                Text = prog.LocationDisplay,
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
                    ToolTip = T("Startup_Tooltip_Missing", "⚠️ O arquivo do programa não foi localizado no caminho especificado")
                };
                TextBlock tbMissing = new TextBlock
                {
                    Text = "⚠️ Não encontrado",
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
                };
                bMissing.Child = tbMissing;
                spTitleRow.Children.Add(bMissing);
            }

            spLeft.Children.Add(spTitleRow);

            // Publisher Row
            TextBlock tbPub = new TextBlock
            {
                Text = string.Format("{0} {1} • Tamanho: {2}",
                    T("Startup_Label_Publisher", "Fornecedor:"),
                    prog.Publisher,
                    prog.FileSizeDisplay),
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

            // Right: Status Badge + Action Buttons
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
                Text = prog.IsEnabled ? T("Startup_Status_Enabled", "Ativado") : T("Startup_Status_Disabled", "Desativado"),
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
                Content = prog.IsEnabled ? T("Startup_Btn_Disable", "Desabilitar") : T("Startup_Btn_Enable", "Habilitar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnToggle.SetResourceReference(Button.StyleProperty, prog.IsEnabled ? "BtnDefault" : "BtnAccent");
            btnToggle.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleStartupProgram(prog, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_ToggleFailed", "Não foi possível alterar o estado da entrada: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnToggle);

            // Edit Button
            Button btnEdit = new Button
            {
                Content = T("Startup_Btn_Edit", "✏️ Editar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnEdit.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnEdit.Click += (s, e) => OpenEditProgramModal(prog);
            spRight.Children.Add(btnEdit);

            // Delete Button
            Button btnDelete = new Button
            {
                Content = T("Startup_Btn_Delete", "🗑️ Excluir"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDelete.Click += (s, e) =>
            {
                string msg = string.Format(
                    T("Startup_Confirm_Delete_Msg", "Tem certeza de que deseja excluir permanentemente a entrada '{0}' da inicialização do Windows?\n\nEsta ação não desinstala o aplicativo, apenas impede sua execução automática com o sistema."),
                    prog.Name);

                if (MessageBox.Show(msg, T("Startup_Confirm_Delete_Title", "Confirmar Exclusão de Inicialização"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    string err;
                    if (!StartupEngine.DeleteStartupProgram(prog, out err))
                    {
                        MessageBox.Show(string.Format(T("Startup_Msg_DeleteFailed", "Não foi possível excluir a entrada: {0}"), err),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        _allPrograms.Remove(prog);
                        RenderCurrentTabList();
                    }
                }
            };
            spRight.Children.Add(btnDelete);

            // Open Folder in Explorer Button
            Button btnExplorer = new Button
            {
                Content = "📂",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Padding = new Thickness(0),
                ToolTip = T("Startup_Tooltip_Explorer", "Abrir pasta de destino no Windows Explorer"),
                Cursor = Cursors.Hand
            };
            btnExplorer.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnExplorer.Click += (s, e) =>
            {
                try
                {
                    string target = prog.ExePath;
                    if (File.Exists(target))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", target));
                    }
                    else if (File.Exists(prog.SourcePath))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", prog.SourcePath));
                    }
                }
                catch { }
            };
            spRight.Children.Add(btnExplorer);

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
                    T("Startup_Stats_Tasks", "Total: {0} tarefas de inicialização • {1} ativadas • {2} desativadas"),
                    total, enabledCount, disabledCount);
            }

            List<StartupTaskItem> filtered = new List<StartupTaskItem>();
            foreach (var t in _allTasks)
            {
                if (string.IsNullOrEmpty(query) ||
                    (t.Name != null && t.Name.ToLowerInvariant().Contains(query)) ||
                    (t.Publisher != null && t.Publisher.ToLowerInvariant().Contains(query)) ||
                    (t.ActionPath != null && t.ActionPath.ToLowerInvariant().Contains(query)))
                {
                    filtered.Add(t);
                }
            }

            if (filtered.Count == 0)
            {
                _pnlItemsList.Children.Add(CreateEmptyPlaceholder(T("Startup_Empty_Tasks", "Nenhuma tarefa agendada de terceiros encontrada com o filtro atual.")));
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
                Text = task.TriggerDisplay,
                FontSize = 9.5
            };
            tbTrigger.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            bTrigger.Child = tbTrigger;
            spTitleRow.Children.Add(bTrigger);

            spLeft.Children.Add(spTitleRow);

            // Author / Publisher
            TextBlock tbPub = new TextBlock
            {
                Text = string.Format("{0} {1} • Caminho da Tarefa: {2}",
                    T("Startup_Label_Publisher", "Fornecedor:"),
                    task.Publisher,
                    task.FullTaskPath),
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

            // Right: Status + Actions
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
                Text = task.IsEnabled ? T("Startup_Status_Enabled", "Ativado") : T("Startup_Status_Disabled", "Desativado"),
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
                Content = task.IsEnabled ? T("Startup_Btn_Disable", "Desabilitar") : T("Startup_Btn_Enable", "Habilitar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnToggle.SetResourceReference(Button.StyleProperty, task.IsEnabled ? "BtnDefault" : "BtnAccent");
            btnToggle.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleTask(task, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_ToggleFailed", "Não foi possível alterar o estado da tarefa: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnToggle);

            // Run Now Button
            Button btnRun = new Button
            {
                Content = T("Startup_Btn_Run", "▶️ Executar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnRun.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnRun.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.RunTask(task, out err))
                {
                    MessageBox.Show("Erro ao executar tarefa: " + err, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(string.Format("Tarefa '{0}' iniciada com sucesso.", task.Name), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            spRight.Children.Add(btnRun);

            // Delete Task Button
            Button btnDelete = new Button
            {
                Content = T("Startup_Btn_Delete", "🗑️ Excluir"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDelete.Click += (s, e) =>
            {
                string msg = string.Format(
                    T("Startup_Confirm_Delete_Msg", "Tem certeza de que deseja excluir permanentemente a tarefa agendada '{0}'?"),
                    task.Name);

                if (MessageBox.Show(msg, T("Startup_Confirm_Delete_Title", "Confirmar Exclusão de Tarefa"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    string err;
                    if (!StartupEngine.DeleteTask(task, out err))
                    {
                        MessageBox.Show(string.Format(T("Startup_Msg_DeleteFailed", "Não foi possível excluir a tarefa: {0}"), err),
                            "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        _allTasks.Remove(task);
                        RenderCurrentTabList();
                    }
                }
            };
            spRight.Children.Add(btnDelete);

            // Explorer Button
            Button btnExplorer = new Button
            {
                Content = "📂",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Padding = new Thickness(0),
                ToolTip = T("Startup_Tooltip_Explorer", "Abrir pasta de destino no Windows Explorer"),
                Cursor = Cursors.Hand
            };
            btnExplorer.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnExplorer.Click += (s, e) =>
            {
                try
                {
                    string clean = StartupEngine.ExtractExePath(task.ActionPath);
                    if (File.Exists(clean))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", clean));
                    }
                }
                catch { }
            };
            spRight.Children.Add(btnExplorer);

            Grid.SetColumn(spRight, 1);
            g.Children.Add(spRight);

            card.Child = g;
            return card;
        }

        #endregion

        #region TAB 3: SERVICES RENDERING

        private void RenderServicesList(string query)
        {
            int total = _allServices.Count;
            int runningCount = 0;
            int autoCount = 0;
            foreach (var s in _allServices)
            {
                if (s.IsRunning) runningCount++;
                if (s.IsEnabled) autoCount++;
            }

            if (_txtStatsSummary != null)
            {
                _txtStatsSummary.Text = string.Format(
                    T("Startup_Stats_Services", "Total: {0} serviços de terceiros • {1} em execução • {2} automáticos"),
                    total, runningCount, autoCount);
            }

            List<StartupServiceItem> filtered = new List<StartupServiceItem>();
            foreach (var s in _allServices)
            {
                if (string.IsNullOrEmpty(query) ||
                    (s.DisplayName != null && s.DisplayName.ToLowerInvariant().Contains(query)) ||
                    (s.ServiceName != null && s.ServiceName.ToLowerInvariant().Contains(query)) ||
                    (s.Publisher != null && s.Publisher.ToLowerInvariant().Contains(query)))
                {
                    filtered.Add(s);
                }
            }

            if (filtered.Count == 0)
            {
                _pnlItemsList.Children.Add(CreateEmptyPlaceholder(T("Startup_Empty_Services", "Nenhum serviço de terceiros encontrado com o filtro atual.")));
                return;
            }

            foreach (var service in filtered)
            {
                _pnlItemsList.Children.Add(BuildServiceCard(service));
            }
        }

        private Border BuildServiceCard(StartupServiceItem service)
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
                Text = service.DisplayName,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tbName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTitleRow.Children.Add(tbName);

            // Startup Type Badge
            Border bType = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            bType.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            TextBlock tbType = new TextBlock
            {
                Text = service.StartupType,
                FontSize = 9.5
            };
            tbType.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            bType.Child = tbType;
            spTitleRow.Children.Add(bType);

            spLeft.Children.Add(spTitleRow);

            // Service internal name & publisher
            TextBlock tbPub = new TextBlock
            {
                Text = string.Format("Nome do Serviço: {0} • {1} {2}",
                    service.ServiceName,
                    T("Startup_Label_Publisher", "Fornecedor:"),
                    service.Publisher),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 2)
            };
            tbPub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spLeft.Children.Add(tbPub);

            // ImagePath
            TextBlock tbCmd = new TextBlock
            {
                Text = service.ImagePath,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = service.ImagePath
            };
            tbCmd.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            spLeft.Children.Add(tbCmd);

            Grid.SetColumn(spLeft, 0);
            g.Children.Add(spLeft);

            // Right: Status Badge + Action Buttons
            StackPanel spRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Running State Pill
            Border bRunState = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = service.IsRunning
                    ? new SolidColorBrush(Color.FromArgb(35, 34, 197, 94))
                    : new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
            };
            TextBlock tbRunState = new TextBlock
            {
                Text = service.IsRunning ? T("Startup_State_Running", "Em execução") : T("Startup_State_Stopped", "Parado"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = service.IsRunning
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            bRunState.Child = tbRunState;
            spRight.Children.Add(bRunState);

            // Toggle Startup Type Button (Automatic vs Disabled)
            Button btnToggleStartup = new Button
            {
                Content = service.IsEnabled ? T("Startup_Btn_Disable", "Desabilitar") : T("Startup_Btn_Enable", "Habilitar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnToggleStartup.SetResourceReference(Button.StyleProperty, service.IsEnabled ? "BtnDefault" : "BtnAccent");
            btnToggleStartup.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleServiceStartupType(service, out err))
                {
                    MessageBox.Show("Erro ao alterar inicialização do serviço: " + err, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnToggleStartup);

            // Start / Stop Service Button
            Button btnServiceState = new Button
            {
                Content = service.IsRunning ? T("Startup_Btn_StopService", "Parar") : T("Startup_Btn_StartService", "Iniciar"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnServiceState.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnServiceState.Click += (s, e) =>
            {
                string err;
                if (!StartupEngine.ToggleServiceRunning(service, out err))
                {
                    MessageBox.Show(string.Format(T("Startup_Msg_ServiceFailed", "Falha ao controlar serviço: {0}"), err),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    RenderCurrentTabList();
                }
            };
            spRight.Children.Add(btnServiceState);

            // Explorer Button
            Button btnExplorer = new Button
            {
                Content = "📂",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Padding = new Thickness(0),
                ToolTip = T("Startup_Tooltip_Explorer", "Abrir pasta de destino no Windows Explorer"),
                Cursor = Cursors.Hand
            };
            btnExplorer.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnExplorer.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(service.ExePath))
                    {
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", service.ExePath));
                    }
                }
                catch { }
            };
            spRight.Children.Add(btnExplorer);

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

        #region MODAL ADD / EDIT PROGRAM

        private void CreateModalOverlay(Grid root)
        {
            _modalOverlay = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))
            };

            Border modalCard = new Border
            {
                Width = 520,
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
                Text = T("Startup_Dialog_New_Title", "Adicionar Novo Programa à Inicialização"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _txtModalTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(_txtModalTitle);

            // Field 1: Name
            TextBlock lblName = new TextBlock
            {
                Text = T("Startup_Field_Name", "Nome do Aplicativo * :"),
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
            TextBlock lblPath = new TextBlock
            {
                Text = T("Startup_Field_Path", "Caminho do Executável / Comando * :"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblPath.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(lblPath);

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
                Content = T("Startup_Btn_Browse", "Procurar..."),
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
                    Filter = "Programas Executáveis (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|Todos os Arquivos (*.*)|*.*",
                    Title = "Selecione o Executável para Inicialização"
                };
                if (ofd.ShowDialog() == true)
                {
                    _txtModalPath.Text = string.Format("\"{0}\"", ofd.FileName);
                    if (string.IsNullOrWhiteSpace(_txtModalName.Text))
                    {
                        _txtModalName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            Grid.SetColumn(_btnModalBrowse, 1);
            pathGrid.Children.Add(_btnModalBrowse);
            modalStack.Children.Add(pathGrid);

            // Field 3: Scope ComboBox
            TextBlock lblScope = new TextBlock
            {
                Text = T("Startup_Field_Scope", "Escopo da Inicialização * :"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblScope.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            modalStack.Children.Add(lblScope);

            _cmbModalScope = new ComboBox
            {
                Height = 30,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 20)
            };
            _cmbModalScope.Items.Add(new ComboBoxItem { Content = T("Startup_Scope_User", "Usuário Atual (HKCU - Run)"), Tag = StartupLocationType.RegistryCurrentUser });
            _cmbModalScope.Items.Add(new ComboBoxItem { Content = T("Startup_Scope_Machine", "Todos os Usuários (HKLM - Run)"), Tag = StartupLocationType.RegistryLocalMachine });
            _cmbModalScope.SelectedIndex = 0;
            modalStack.Children.Add(_cmbModalScope);

            // Modal Action Buttons
            StackPanel modalBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            _btnModalCancel = new Button
            {
                Content = T("Startup_Btn_Cancel", "Cancelar"),
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _btnModalCancel.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnModalCancel.Click += (s, e) => CloseModal();
            modalBtns.Children.Add(_btnModalCancel);

            _btnModalSave = new Button
            {
                Content = T("Startup_Btn_Save", "Salvar"),
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0),
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
            _btnModalSave.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnModalSave.Click += (s, e) => SaveProgramModal();
            modalBtns.Children.Add(_btnModalSave);

            modalStack.Children.Add(modalBtns);
            modalCard.Child = modalStack;
            _modalOverlay.Children.Add(modalCard);
            root.Children.Add(_modalOverlay);
        }

        private void OpenAddProgramModal()
        {
            _editingProgram = null;
            if (_txtModalTitle != null) _txtModalTitle.Text = T("Startup_Dialog_New_Title", "Adicionar Novo Programa à Inicialização");
            if (_txtModalName != null) _txtModalName.Text = "";
            if (_txtModalPath != null) _txtModalPath.Text = "";
            if (_cmbModalScope != null)
            {
                _cmbModalScope.IsEnabled = true;
                _cmbModalScope.SelectedIndex = 0;
            }
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Visible;
            if (_txtModalName != null) _txtModalName.Focus();
        }

        private void OpenEditProgramModal(StartupProgramItem prog)
        {
            _editingProgram = prog;
            if (_txtModalTitle != null) _txtModalTitle.Text = T("Startup_Dialog_Edit_Title", "Editar Entrada de Inicialização");
            if (_txtModalName != null) _txtModalName.Text = prog.Name;
            if (_txtModalPath != null) _txtModalPath.Text = prog.Command;
            if (_cmbModalScope != null)
            {
                _cmbModalScope.IsEnabled = false; // Keep existing scope when editing
                _cmbModalScope.SelectedIndex = (prog.LocationType == StartupLocationType.RegistryCurrentUser) ? 0 : 1;
            }
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Visible;
            if (_txtModalName != null) _txtModalName.Focus();
        }

        private void CloseModal()
        {
            if (_modalOverlay != null) _modalOverlay.Visibility = Visibility.Collapsed;
            _editingProgram = null;
        }

        private void SaveProgramModal()
        {
            string name = _txtModalName.Text.Trim();
            string path = _txtModalPath.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                MessageBox.Show(T("Startup_Msg_NameAndPathRequired", "Por favor, informe o Nome e o Caminho do programa."),
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
            string err;
            if (!StartupEngine.AddOrUpdateStartupProgram(name, path, scope, oldName, out err))
            {
                MessageBox.Show("Erro ao salvar: " + err, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CloseModal();
            ReloadActiveTabData();
        }

        #endregion

        public void OnLanguageChanged(string langCode)
        {
            if (_txtHeaderTitle != null) _txtHeaderTitle.Text = T("Startup_Title", "Gerenciador de Inicialização do Sistema");
            if (_txtHeaderSubtitle != null) _txtHeaderSubtitle.Text = T("Startup_Subtitle", "Monitore, habilite, desabilite ou remova programas, tarefas agendadas e serviços de inicialização");
            if (_btnNewProgram != null) _btnNewProgram.Content = T("Startup_Btn_New", "➕ Adicionar Programa");
            if (_btnRefresh != null) _btnRefresh.Content = T("Startup_Btn_Refresh", "🔄 Atualizar");
            if (_chkHideMicrosoft != null) _chkHideMicrosoft.Content = T("Startup_Chk_HideMicrosoft", "Ocultar itens nativos da Microsoft / Windows");
            if (_tabProgramsText != null) _tabProgramsText.Text = "🚀 " + T("Startup_Tab_Programs", "Programas de Inicialização");
            if (_tabTasksText != null) _tabTasksText.Text = "⏰ " + T("Startup_Tab_Tasks", "Tarefas Agendadas");
            if (_tabServicesText != null) _tabServicesText.Text = "⚙️ " + T("Startup_Tab_Services", "Serviços de Inicialização");

            ReloadActiveTabData();
        }
    }
}
