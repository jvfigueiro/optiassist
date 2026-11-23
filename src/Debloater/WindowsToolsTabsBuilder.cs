using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OptiAssist.Platform;

namespace OptiAssist.Debloater
{
    /// <summary>
    /// Builds and manages the Windows Debloater 2.0 tab and the Compatibility Checker tab
    /// integrated natively inside OptiAssist's Windows Tools section.
    /// Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public class WindowsToolsTabsController
    {
        private readonly IOptiAssistHost _host;
        private readonly Action<string> _log;
        private readonly Action _onQuickFixApplied;

        private DebloaterEngine _debloatEngine;
        private readonly List<KeyValuePair<CheckBox, DebloatTweakDefinition>> _debloatChecks = new List<KeyValuePair<CheckBox, DebloatTweakDefinition>>();
        private readonly List<KeyValuePair<Border, DebloatTweakDefinition>> _debloatCards = new List<KeyValuePair<Border, DebloatTweakDefinition>>();
        private TextBlock _txtDebloatRiskCounter;
        private CheckBox _chkDebloatRestorePoint;
        private bool _userToggledRestorePoint = false;
        private string _selectedProfile = "";
        private string _selectedDebloatCategory = "all";
        private string _debloatSearchText = "";
        private string _lastDebloatSessionId = "";
        private ContentControl _debloaterHost;
        private ContentControl _compatHost;

        public WindowsToolsTabsController(IOptiAssistHost host, Action<string> log, Action onQuickFixApplied)
        {
            _host = host;
            _log = log;
            _onQuickFixApplied = onQuickFixApplied;
        }

        private string T(string key, string fallback)
        {
            return LocalizationHelper.Get(key, fallback);
        }

        private void HostLog(string msg)
        {
            if (_log != null) _log(msg);
            else if (_host != null) _host.Log(msg);
        }

        public void AttachHosts(ContentControl debloaterHost, ContentControl compatHost)
        {
            _debloaterHost = debloaterHost;
            _compatHost = compatHost;
            RebuildDebloaterView();
            RebuildCompatibilityView(false);
        }

        public void RefreshLanguage()
        {
            RebuildDebloaterView();
            RebuildCompatibilityView(false);
        }

        #region TAB 2: WINDOWS DEBLOATER 2.0

        public void OnLanguageChanged()
        {
            try
            {
                if (_debloaterHost != null && _debloaterHost.Content != null)
                {
                    RebuildDebloaterView();
                }
                if (_compatHost != null && _compatHost.Content != null)
                {
                    RebuildCompatibilityView(false);
                }
            }
            catch { }
        }

        public void RebuildDebloaterView()
        {
            if (_debloaterHost == null) return;

            HashSet<string> preservedChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hadChecks = (_debloatChecks.Count > 0);
            for (int i = 0; i < _debloatChecks.Count; i++)
            {
                if (_debloatChecks[i].Key.IsChecked == true && _debloatChecks[i].Value != null)
                {
                    preservedChecked.Add(_debloatChecks[i].Value.id);
                }
            }

            UIElement content = BuildDebloaterContent();
            FluentIcons.UpgradeVisualTree(content);
            _debloaterHost.Content = content;

            if (hadChecks && preservedChecked.Count > 0)
            {
                for (int i = 0; i < _debloatChecks.Count; i++)
                {
                    if (_debloatChecks[i].Value != null)
                    {
                        _debloatChecks[i].Key.IsChecked = preservedChecked.Contains(_debloatChecks[i].Value.id);
                    }
                }
                UpdateDebloatRiskCounter();
            }
        }

        private UIElement BuildDebloaterContent()
        {
            _debloatChecks.Clear();
            _debloatCards.Clear();

            if (_debloatEngine == null)
            {
                _debloatEngine = new DebloaterEngine(_host);
            }

            List<DebloatTweakDefinition> catalogTweaks = DebloaterCatalogLoader.LoadAllTweaks();
            _debloatEngine.RefreshAllStates(catalogTweaks);

            StackPanel root = new StackPanel() { Margin = new Thickness(0, 0, 0, 12) };

            // Header & Controls Card
            Border topCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            topCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            topCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel topStack = new StackPanel();

            StackPanel infoStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            TextBlock lblTitle = new TextBlock()
            {
                Text = "✨ " + T("OptiBoost_Title", "OptiBoost"),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            lblTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            TextBlock lblSub = new TextBlock()
            {
                Text = T("OptiBoost_Subtitle", "OptiBoost evaluates your installed hardware and Windows edition to determine automatic optimization suggestions tailored to your PC, or lets you configure everything manually with pre-change snapshots and rollback."),
                FontSize = 11.5,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            lblSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            infoStack.Children.Add(lblTitle);
            infoStack.Children.Add(lblSub);
            topStack.Children.Add(infoStack);

            WrapPanel btnPanel = new WrapPanel()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            _chkDebloatRestorePoint = new CheckBox()
            {
                Content = "🛡️ " + T("Debloater_Chk_RestorePoint", "Restore Point"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 4),
                ToolTip = T("Debloater_Chk_RestorePoint_Tooltip", "Create a Windows System Restore Point before applying (auto-enabled when Medium/High risk tweaks are selected)")
            };
            _chkDebloatRestorePoint.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            _chkDebloatRestorePoint.Click += (s, e) => { _userToggledRestorePoint = true; };

            Button btnSelectAll = new Button() { Content = T("Btn_SelectAll", "Select All"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 4) };
            btnSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnSelectAll.Click += (s, e) => { foreach (var kv in _debloatChecks) kv.Key.IsChecked = true; };

            Button btnClearAll = new Button() { Content = T("Btn_ClearAll", "Clear"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 4) };
            btnClearAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnClearAll.Click += (s, e) => { foreach (var kv in _debloatChecks) kv.Key.IsChecked = false; };

            Button btnDryRun = new Button() { Content = "🔍 " + T("Debloater_DryRun", "Dry-Run"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 4) };
            btnDryRun.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnDryRun.Click += (s, e) =>
            {
                List<DebloatTweakDefinition> selected = GetSelectedDebloatDefinitions();
                if (selected.Count == 0)
                {
                    OptiDialog.Show(T("Debloater_NoneSelected", "Select at least one debloat tweak to apply or simulate."), "OptiAssist - OptiBoost", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                ShowDryRunDialog(selected);
            };

            Button btnRevertSession = new Button() { Content = "↩ " + T("Debloater_RevertSession", "Revert Last Session"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 4) };
            btnRevertSession.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnRevertSession.IsEnabled = (DebloaterEngine.GetBackupSessionFilesNewestFirst().Count > 0);
            btnRevertSession.Click += (s, e) =>
            {
                MessageBoxResult confirm = OptiDialog.Show(
                    T("Debloater_ConfirmRevertSession", "Revert all changes from the most recent Windows Debloater backup session?"),
                    "OptiAssist - OptiBoost",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                if (_host != null) _host.OpenLogWindow();
                string sessionId;
                bool ok = _debloatEngine.RevertLatestBackupSession(HostLog, out sessionId);
                RebuildDebloaterView();
                if (ok)
                {
                    OptiDialog.Show(
                        string.Format(T("Debloater_Msg_RevertedSession", "Backup session '{0}' was reverted successfully."), sessionId),
                        "OptiAssist - OptiBoost",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    OptiDialog.Show(
                        T("Debloater_Msg_NoSnapshot", "No backup snapshot was found for this operation."),
                        "OptiAssist - OptiBoost",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };

            Button btnCopyReport = new Button() { Content = "📋 " + T("Debloater_CopyReport", "Report Summary"), Height = 28, Padding = new Thickness(10, 0, 10, 0), FontSize = 11, Margin = new Thickness(0, 0, 6, 4) };
            btnCopyReport.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCopyReport.Click += (s, e) =>
            {
                List<DebloatTweakDefinition> appliedList = new List<DebloatTweakDefinition>();
                for (int i = 0; i < _debloatChecks.Count; i++)
                {
                    if (_debloatChecks[i].Value != null && (_debloatChecks[i].Value.CurrentState == DebloatTweakState.Applied || _debloatChecks[i].Key.IsChecked == true))
                    {
                        appliedList.Add(_debloatChecks[i].Value);
                    }
                }
                string summary = _debloatEngine.GenerateMaintenanceReportSummary(appliedList, _lastDebloatSessionId, T, _selectedProfile);
                try { Clipboard.SetText(summary); } catch { }
                OptiDialog.Show(
                    T("Debloater_Msg_ReportCopied", "Structured Windows Debloater summary copied to clipboard for the Maintenance Report!"),
                    "OptiAssist - OptiBoost",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };

            Button btnApplySelected = new Button() { Content = "🚀 " + T("Debloater_ApplySelected", "Apply Selected"), Height = 28, Padding = new Thickness(14, 0, 14, 0), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) };
            btnApplySelected.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnApplySelected.Click += (s, e) =>
            {
                List<DebloatTweakDefinition> selected = GetSelectedDebloatDefinitions();
                if (selected.Count == 0)
                {
                    OptiDialog.Show(T("Debloater_NoneSelected", "Select at least one debloat tweak to apply or simulate."), "OptiAssist - OptiBoost", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                List<DebloatTweakDefinition> filtered = FilterAndConfirmDebloatDefinitions(selected);
                if (filtered.Count == 0) return;
                MessageBoxResult confirm = OptiDialog.Show(
                    string.Format(T("Debloater_ConfirmBatch", "Apply {0} selected Windows Debloater tweaks with automatic snapshot backup?"), filtered.Count),
                    "OptiAssist - OptiBoost",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
                RunDeclarativeDebloatAsync(filtered, btnApplySelected);
            };

            btnPanel.Children.Add(_chkDebloatRestorePoint);
            btnPanel.Children.Add(btnSelectAll);
            btnPanel.Children.Add(btnClearAll);
            btnPanel.Children.Add(btnDryRun);
            btnPanel.Children.Add(btnRevertSession);
            btnPanel.Children.Add(btnCopyReport);
            btnPanel.Children.Add(btnApplySelected);

            // Auto-detected Profile Strip ABOVE the action buttons (Dashboard recommendation strip style)
            PlatformDiagnosticSnapshot diagSnapshot = (_host != null) ? _host.PlatformDiagnostic : PlatformDiagnosticService.Instance.GetSnapshot(false);
            string recProfileCode = (diagSnapshot != null && diagSnapshot.ProfileRecommendation != null)
                ? diagSnapshot.ProfileRecommendation.ProfileCode
                : "conventional";

            if (string.IsNullOrEmpty(_selectedProfile))
            {
                _selectedProfile = recProfileCode;
            }

            if (diagSnapshot != null && diagSnapshot.ProfileRecommendation != null)
            {
                Border recStrip = new Border()
                {
                    BorderThickness = new Thickness(0, 1, 0, 1),
                    Padding = new Thickness(0, 8, 0, 8),
                    Margin = new Thickness(0, 10, 0, 2)
                };
                recStrip.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                Grid recGrid = new Grid();
                recGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                recGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                TextBlock recIcon = new TextBlock()
                {
                    Text = "⭐",
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(recIcon, 0);
                recGrid.Children.Add(recIcon);

                StackPanel spRec = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
                TextBlock recTitleTb = new TextBlock()
                {
                    Text = T("Debloater_Rec_Banner_Title", "Auto-Detected Profile:") + " " + diagSnapshot.ProfileRecommendation.Title + " (" + diagSnapshot.ProfileRecommendation.ConfidenceLevel + " " + T("Debloater_Confidence", "Confidence") + ")",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                recTitleTb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                spRec.Children.Add(recTitleTb);

                if (diagSnapshot.ProfileRecommendation.Reasons != null && diagSnapshot.ProfileRecommendation.Reasons.Count > 0)
                {
                    TextBlock reasonsTb = new TextBlock()
                    {
                        Text = string.Join(" • ", diagSnapshot.ProfileRecommendation.Reasons.ToArray()),
                        FontSize = 10.5,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    reasonsTb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                    spRec.Children.Add(reasonsTb);
                }

                Grid.SetColumn(spRec, 1);
                recGrid.Children.Add(spRec);
                recStrip.Child = recGrid;
                topStack.Children.Add(recStrip);
            }

            topStack.Children.Add(btnPanel);

            StackPanel topCardStack = new StackPanel();
            topCardStack.Children.Add(topStack);

            // 7 Approved Profiles Bar
            WrapPanel presetBar = new WrapPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            TextBlock lblPresets = new TextBlock()
            {
                Text = T("Debloater_Presets_Label", "Profiles:"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 4)
            };
            lblPresets.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            presetBar.Children.Add(lblPresets);

            presetBar.Children.Add(CreateProfilePresetButton("🎮 " + T("Debloater_Preset_Gaming", "Gaming PC"), "gaming", string.Equals(recProfileCode, "gaming", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("🛠️ " + T("Debloater_Preset_Workstation", "Workstation"), "workstation", string.Equals(recProfileCode, "workstation", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("⚡ " + T("Debloater_Preset_Hybrid", "Hybrid"), "hybrid", string.Equals(recProfileCode, "hybrid", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("🖥️ " + T("Debloater_Preset_Conventional", "Conventional Desktop"), "conventional", string.Equals(recProfileCode, "conventional", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("💻 " + T("Debloater_Preset_Laptop", "Laptop"), "laptop", string.Equals(recProfileCode, "laptop", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("🔥 " + T("Debloater_Preset_LaptopGaming", "Gaming Laptop"), "laptopGaming", string.Equals(recProfileCode, "laptopGaming", StringComparison.OrdinalIgnoreCase)));
            presetBar.Children.Add(CreateProfilePresetButton("🪶 " + T("Debloater_Preset_Ultraportable", "Ultraportable"), "ultraportable", string.Equals(recProfileCode, "ultraportable", StringComparison.OrdinalIgnoreCase)));
            topCardStack.Children.Add(presetBar);

            // Filter Bar (Category + Quick Search + Risk Counter)
            Grid filterBar = new Grid() { Margin = new Thickness(0, 10, 0, 0) };
            filterBar.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            filterBar.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            filterBar.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            ComboBox cmbCat = new ComboBox()
            {
                Width = 185,
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            KeyValuePair<string, string>[] categories = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("all", T("Debloater_Cat_All", "All Categories")),
                new KeyValuePair<string, string>("power", T("Debloater_Cat_Power", "⚡ Power & Plans")),
                new KeyValuePair<string, string>("appearance_ui", T("Debloater_Cat_Appearance", "🎨 Appearance & UI")),
                new KeyValuePair<string, string>("services", T("Debloater_Cat_Services", "⚙️ Windows Services")),
                new KeyValuePair<string, string>("features_capabilities", T("Debloater_Cat_Features", "🧩 Features & Capabilities")),
                new KeyValuePair<string, string>("policies_gpo", T("Debloater_Cat_Policies", "📜 Policies (GPO)")),
                new KeyValuePair<string, string>("security_best_effort", T("Debloater_Cat_Security", "🛡️ Security (Best Effort)")),
                new KeyValuePair<string, string>("apps_ctt", T("Debloater_Cat_Apps", "📦 Apps & Curated"))
            };
            int selCatIdx = 0;
            for (int c = 0; c < categories.Length; c++)
            {
                cmbCat.Items.Add(new ComboBoxItem() { Content = categories[c].Value, Tag = categories[c].Key });
                if (string.Equals(categories[c].Key, _selectedDebloatCategory, StringComparison.OrdinalIgnoreCase)) selCatIdx = c;
            }
            cmbCat.SelectedIndex = selCatIdx;
            cmbCat.SelectionChanged += (s, e) =>
            {
                ComboBoxItem sel = cmbCat.SelectedItem as ComboBoxItem;
                _selectedDebloatCategory = (sel != null && sel.Tag != null) ? sel.Tag.ToString() : "all";
                ApplyDebloatFilters();
            };
            Grid.SetColumn(cmbCat, 0);
            filterBar.Children.Add(cmbCat);

            Grid searchBoxHost = new Grid() { Margin = new Thickness(0, 0, 10, 0) };
            TextBox txtSearch = new TextBox()
            {
                Height = 28,
                FontSize = 11,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = _debloatSearchText ?? ""
            };
            TextBlock lblHint = new TextBlock()
            {
                Text = "🔍 " + T("Debloater_Search_Placeholder", "Quick search tweaks by title, description or ID..."),
                FontSize = 11,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(_debloatSearchText) ? Visibility.Visible : Visibility.Collapsed
            };
            lblHint.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            txtSearch.TextChanged += (s, e) =>
            {
                _debloatSearchText = txtSearch.Text ?? "";
                lblHint.Visibility = string.IsNullOrEmpty(_debloatSearchText) ? Visibility.Visible : Visibility.Collapsed;
                ApplyDebloatFilters();
            };
            searchBoxHost.Children.Add(txtSearch);
            searchBoxHost.Children.Add(lblHint);
            Grid.SetColumn(searchBoxHost, 1);
            filterBar.Children.Add(searchBoxHost);

            _txtDebloatRiskCounter = new TextBlock()
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtDebloatRiskCounter.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            Grid.SetColumn(_txtDebloatRiskCounter, 2);
            filterBar.Children.Add(_txtDebloatRiskCounter);

            topCardStack.Children.Add(filterBar);
            topCard.Child = topCardStack;
            root.Children.Add(topCard);

            // Tweak Cards
            for (int i = 0; i < catalogTweaks.Count; i++)
            {
                DebloatTweakDefinition tweak = catalogTweaks[i];
                if (tweak == null || !tweak.IsApplicable) continue;

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
                chk.Checked += (s, e) => UpdateDebloatRiskCounter();
                chk.Unchecked += (s, e) => UpdateDebloatRiskCounter();
                Grid.SetColumn(chk, 0);
                row.Children.Add(chk);

                StackPanel textCol = new StackPanel() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                WrapPanel headerRow = new WrapPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                TextBlock titleTxt = new TextBlock()
                {
                    Text = T(tweak.titleKey, tweak.titleFallback),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                titleTxt.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                headerRow.Children.Add(titleTxt);

                headerRow.Children.Add(CreateStateBadge(tweak.CurrentState));
                if (!string.Equals(tweak.risk, "low", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow.Children.Add(CreateRiskBadge(tweak.risk));
                }
                if (!string.IsNullOrEmpty(tweak.HardwareConditionBadge))
                {
                    headerRow.Children.Add(CreateHardwareConditionBadge(tweak.HardwareConditionBadge, tweak.HardwareConditionReason, tweak.ForceUncheckedByCondition));
                }

                TextBlock descTxt = new TextBlock()
                {
                    Text = T(tweak.descKey, tweak.descFallback),
                    FontSize = 11,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                descTxt.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

                textCol.Children.Add(headerRow);
                textCol.Children.Add(descTxt);

                if (!string.IsNullOrEmpty(tweak.HardwareConditionReason))
                {
                    TextBlock hwNote = new TextBlock()
                    {
                        Text = "ℹ️ " + tweak.HardwareConditionReason,
                        FontSize = 10,
                        Margin = new Thickness(0, 3, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    hwNote.SetResourceReference(TextBlock.ForegroundProperty, tweak.ForceUncheckedByCondition ? "BrushWarning" : "BrushAccent");
                    textCol.Children.Add(hwNote);
                }

                Grid.SetColumn(textCol, 1);
                row.Children.Add(textCol);

                StackPanel actionCol = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DebloatTweakDefinition captured = tweak;

                if (!string.IsNullOrEmpty(tweak.QuickActionUri))
                {
                    string capturedUri = tweak.QuickActionUri;
                    Button btnQuickUri = new Button()
                    {
                        Content = string.IsNullOrEmpty(tweak.QuickActionLabel) ? "🔗 Open" : tweak.QuickActionLabel,
                        Height = 28,
                        Padding = new Thickness(10, 0, 10, 0),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = capturedUri
                    };
                    btnQuickUri.SetResourceReference(Button.StyleProperty, "BtnDefault");
                    btnQuickUri.Click += (s, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo(capturedUri) { UseShellExecute = true }); }
                        catch (Exception ex) { HostLog("[Debloater] Could not open " + capturedUri + ": " + ex.Message); }
                    };
                    actionCol.Children.Add(btnQuickUri);
                }

                if (tweak.HasBackupSnapshot)
                {
                    Button btnRevertOne = new Button()
                    {
                        Content = "↩ " + T("Debloater_Btn_Revert", "Revert"),
                        Height = 28,
                        Padding = new Thickness(10, 0, 10, 0),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    btnRevertOne.SetResourceReference(Button.StyleProperty, "BtnDefault");
                    btnRevertOne.Click += (s, e) =>
                    {
                        string tTitle = T(captured.titleKey, captured.titleFallback);
                        MessageBoxResult res = OptiDialog.Show(
                            string.Format(T("Debloater_ConfirmRevertOne", "Revert tweak '{0}' using the most recent snapshot backup?"), tTitle),
                            "OptiAssist - OptiBoost",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        if (res != MessageBoxResult.Yes) return;

                        if (_host != null) _host.OpenLogWindow();
                        bool ok = _debloatEngine.RevertSingleTweak(captured.id, HostLog);
                        RebuildDebloaterView();
                        if (ok)
                        {
                            OptiDialog.Show(
                                string.Format(T("Debloater_Msg_RevertedOne", "Tweak '{0}' was reverted from snapshot."), tTitle),
                                "OptiAssist - OptiBoost",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    };
                    actionCol.Children.Add(btnRevertOne);
                }

                Button btnApplyOne = new Button()
                {
                    Content = "▶ " + T("Btn_Apply", "Apply"),
                    Height = 28,
                    Padding = new Thickness(12, 0, 12, 0),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnApplyOne.SetResourceReference(Button.StyleProperty, "BtnDefault");
                btnApplyOne.Click += (s, e) =>
                {
                    List<DebloatTweakDefinition> singleList = new List<DebloatTweakDefinition>() { captured };
                    List<DebloatTweakDefinition> confirmed = FilterAndConfirmDebloatDefinitions(singleList);
                    if (confirmed.Count > 0)
                    {
                        RunDeclarativeDebloatAsync(confirmed, btnApplyOne);
                    }
                };
                actionCol.Children.Add(btnApplyOne);

                Grid.SetColumn(actionCol, 2);
                row.Children.Add(actionCol);

                card.Child = row;
                root.Children.Add(card);
                _debloatChecks.Add(new KeyValuePair<CheckBox, DebloatTweakDefinition>(chk, tweak));
                _debloatCards.Add(new KeyValuePair<Border, DebloatTweakDefinition>(card, tweak));
            }

            if (!string.IsNullOrEmpty(_selectedProfile))
            {
                ApplyDebloaterProfile(_selectedProfile);
            }
            ApplyDebloatFilters();
            UpdateDebloatRiskCounter();

            return root;
        }

        private Button CreateProfilePresetButton(string label, string profileId, bool isRecommended)
        {
            string displayLabel = isRecommended ? label + " " + T("Debloater_Btn_Recommended", "(Recommended)") : label;
            Button btn = new Button()
            {
                Content = displayLabel,
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 4)
            };
            btn.SetResourceReference(Button.StyleProperty, isRecommended ? "BtnAccent" : "BtnDefault");
            string capturedProfile = profileId;
            btn.Click += (s, e) =>
            {
                _selectedProfile = capturedProfile;
                ApplyDebloaterProfile(capturedProfile);
                if (_host != null && _host.Settings != null)
                {
                    _host.Settings.DebloaterProfileOverride = capturedProfile;
                    _host.SaveSettings();
                }
            };
            return btn;
        }

        private void ApplyDebloaterProfile(string profileId)
        {
            for (int i = 0; i < _debloatChecks.Count; i++)
            {
                DebloatTweakDefinition def = _debloatChecks[i].Value;
                if (def != null && def.profiles != null)
                {
                    if (def.ForceUncheckedByCondition)
                    {
                        _debloatChecks[i].Key.IsChecked = false;
                    }
                    else
                    {
                        string mode = def.profiles.GetModeForProfile(profileId);
                        _debloatChecks[i].Key.IsChecked = string.Equals(mode, "on", StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    _debloatChecks[i].Key.IsChecked = false;
                }
            }
            UpdateDebloatRiskCounter();
        }

        private void ApplyDebloatFilters()
        {
            string cat = (_selectedDebloatCategory ?? "all").Trim().ToLowerInvariant();
            string query = (_debloatSearchText ?? "").Trim();

            for (int i = 0; i < _debloatCards.Count; i++)
            {
                Border card = _debloatCards[i].Key;
                DebloatTweakDefinition tweak = _debloatCards[i].Value;
                if (card == null || tweak == null) continue;

                bool catOk = (cat == "all" || string.Equals(tweak.category ?? "", cat, StringComparison.OrdinalIgnoreCase));
                bool searchOk = true;
                if (!string.IsNullOrEmpty(query))
                {
                    string title = T(tweak.titleKey, tweak.titleFallback);
                    string desc = T(tweak.descKey, tweak.descFallback);
                    searchOk = (title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                desc.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (tweak.id != null && tweak.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (tweak.category != null && tweak.category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                card.Visibility = (catOk && searchOk) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateDebloatRiskCounter()
        {
            if (_txtDebloatRiskCounter == null) return;
            int total = 0;
            int low = 0;
            int med = 0;
            int high = 0;

            for (int i = 0; i < _debloatChecks.Count; i++)
            {
                if (_debloatChecks[i].Key.IsChecked == true && _debloatChecks[i].Value != null)
                {
                    total++;
                    string r = (_debloatChecks[i].Value.risk ?? "low").Trim().ToLowerInvariant();
                    if (r == "high" || r == "irreversible") high++;
                    else if (r == "medium") med++;
                    else low++;
                }
            }

            _txtDebloatRiskCounter.Text = string.Format(
                T("Debloater_RiskCounter_Fmt", "Selected: {0}  (Low: {1} | Med: {2} | High: {3})"),
                total, low, med, high);

            if (_chkDebloatRestorePoint != null && !_userToggledRestorePoint)
            {
                _chkDebloatRestorePoint.IsChecked = (med > 0 || high > 0);
            }
        }

        private Border CreateStateBadge(DebloatTweakState state)
        {
            string text;
            Color bgCol;
            Color fgCol;

            if (state == DebloatTweakState.Applied)
            {
                text = T("Debloater_State_Applied", "✔ Applied");
                bgCol = Color.FromArgb(35, 16, 185, 129);
                fgCol = Color.FromRgb(16, 185, 129);
            }
            else if (state == DebloatTweakState.Partial)
            {
                text = T("Debloater_State_Partial", "⚠ Partial");
                bgCol = Color.FromArgb(35, 245, 158, 11);
                fgCol = Color.FromRgb(245, 158, 11);
            }
            else
            {
                text = T("Debloater_State_Pending", "○ Pending");
                bgCol = Color.FromArgb(25, 148, 163, 184);
                fgCol = Color.FromRgb(148, 163, 184);
            }

            Border b = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(bgCol),
                BorderBrush = new SolidColorBrush(fgCol),
                BorderThickness = new Thickness(1)
            };
            b.Child = new TextBlock()
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fgCol)
            };
            return b;
        }

        private Border CreateRiskBadge(string risk)
        {
            string r = (risk ?? "low").Trim().ToLowerInvariant();
            string text = (r == "high" || r == "irreversible")
                ? T("Debloater_Risk_High", "High Risk")
                : T("Debloater_Risk_Medium", "Medium Risk");
            Color fgCol = (r == "high" || r == "irreversible")
                ? Color.FromRgb(239, 68, 68)
                : Color.FromRgb(245, 158, 11);
            Color bgCol = Color.FromArgb(30, fgCol.R, fgCol.G, fgCol.B);

            Border b = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(bgCol)
            };
            b.Child = new TextBlock()
            {
                Text = "⚠ " + text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fgCol)
            };
            return b;
        }

        private Border CreateHardwareConditionBadge(string badgeText, string tooltipReason, bool isWarningStyle)
        {
            Color fgCol = isWarningStyle
                ? Color.FromRgb(245, 158, 11)
                : Color.FromRgb(14, 165, 233);
            Color bgCol = Color.FromArgb(28, fgCol.R, fgCol.G, fgCol.B);

            Border b = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(bgCol),
                BorderBrush = new SolidColorBrush(fgCol),
                BorderThickness = new Thickness(1),
                ToolTip = string.IsNullOrEmpty(tooltipReason) ? badgeText : tooltipReason
            };
            b.Child = new TextBlock()
            {
                Text = "🔧 " + badgeText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fgCol)
            };
            return b;
        }

        private List<DebloatTweakDefinition> GetSelectedDebloatDefinitions()
        {
            List<DebloatTweakDefinition> selected = new List<DebloatTweakDefinition>();
            for (int i = 0; i < _debloatChecks.Count; i++)
            {
                if (_debloatChecks[i].Key.IsChecked == true && _debloatChecks[i].Value != null)
                {
                    selected.Add(_debloatChecks[i].Value);
                }
            }
            return selected;
        }

        private List<DebloatTweakDefinition> FilterAndConfirmDebloatDefinitions(List<DebloatTweakDefinition> input)
        {
            List<DebloatTweakDefinition> result = new List<DebloatTweakDefinition>();
            if (input == null || input.Count == 0) return result;
            if (_debloatEngine == null) _debloatEngine = new DebloaterEngine(_host);

            bool hasWpbt = false;
            bool hasEdge = false;
            bool hasBrave = false;

            for (int i = 0; i < input.Count; i++)
            {
                DebloatTweakDefinition item = input[i];
                if (item == null) continue;
                if (string.Equals(item.id, "dll-debloat-wpbt", StringComparison.OrdinalIgnoreCase)) hasWpbt = true;
                if (string.Equals(item.id, "dll-debloat-edge", StringComparison.OrdinalIgnoreCase)) hasEdge = true;
                if (string.Equals(item.id, "dll-debloat-brave", StringComparison.OrdinalIgnoreCase)) hasBrave = true;
            }

            bool allowWpbt = true;
            if (hasWpbt)
            {
                string oem = _debloatEngine.GetSystemOemManufacturer();
                string ff = _debloatEngine.IsLaptopSystem() ? "Laptop / Portable" : "Desktop";
                string wpbtMsg = string.Format(
                    T("Debloat_Wpbt_ConfirmPrompt",
                      "Medium Risk Warning — WPBT Execution\n\nDetected OEM: {0}\nForm Factor: {1}\n\nOn some OEM computers, disabling WPBT may block legitimate firmware utilities.\n\nDo you want to apply DisableWpbtExecution=1?"),
                    oem, ff);
                allowWpbt = (OptiDialog.Show(wpbtMsg, "OptiAssist - OptiBoost", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
            }

            bool allowBrowserPolicies = true;
            if (hasEdge || hasBrave)
            {
                string browsers = (hasEdge && hasBrave) ? "Microsoft Edge & Brave Browser" : (hasEdge ? "Microsoft Edge" : "Brave Browser");
                string browserMsg = string.Format(
                    T("Debloat_BrowserPolicy_ConfirmPrompt",
                      "Browser Enterprise Policy Notice\n\nApplying debloat policies to {0} writes corporate policy keys under HKLM\\SOFTWARE\\Policies. As a result, the browser menu will show 'Managed by your organization'.\n\nDo you want to continue?"),
                    browsers);
                allowBrowserPolicies = (OptiDialog.Show(browserMsg, "OptiAssist - OptiBoost", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes);
            }

            for (int i = 0; i < input.Count; i++)
            {
                DebloatTweakDefinition item = input[i];
                if (item == null) continue;
                if (string.Equals(item.id, "dll-debloat-wpbt", StringComparison.OrdinalIgnoreCase) && !allowWpbt) continue;
                if ((string.Equals(item.id, "dll-debloat-edge", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(item.id, "dll-debloat-brave", StringComparison.OrdinalIgnoreCase)) && !allowBrowserPolicies) continue;

                if (item.doubleConfirm)
                {
                    string tTitle = T(item.titleKey, item.titleFallback);
                    string warnBody = (item.warnings != null && item.warnings.Count > 0)
                        ? string.Join("\n", item.warnings.ToArray())
                        : T(item.descKey, item.descFallback);
                    MessageBoxResult step1 = OptiDialog.Show(
                        string.Format(T("Debloat_DoubleConfirm_Step1", "HIGH RISK CONFIRMATION (1/2)\n\n{0}\n\n{1}\n\nDo you want to proceed?"), tTitle, warnBody),
                        "OptiAssist - OptiBoost",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (step1 != MessageBoxResult.Yes) continue;

                    MessageBoxResult step2 = OptiDialog.Show(
                        string.Format(T("Debloat_DoubleConfirm_Step2", "FINAL CONFIRMATION (2/2)\n\nAre you completely sure you want to apply '{0}'?\nThis action may be irreversible or long-running."), tTitle),
                        "OptiAssist - OptiBoost",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);
                    if (step2 != MessageBoxResult.Yes) continue;
                }

                result.Add(item);
            }

            return result;
        }

        private void ShowDryRunDialog(List<DebloatTweakDefinition> selected)
        {
            string report = _debloatEngine.GenerateDryRunReport(selected, T);
            string title = T("Debloater_DryRun_WindowTitle", "OptiBoost — Dry-Run Preview");
            OptiDialog.ShowTextViewer(title, report, null, 780, 540);
        }

        private void RunDeclarativeDebloatAsync(List<DebloatTweakDefinition> tweaksToApply, Button triggerBtn)
        {
            if (tweaksToApply == null || tweaksToApply.Count == 0) return;
            if (_host != null) _host.OpenLogWindow();

            object origContent = triggerBtn != null ? triggerBtn.Content : null;
            if (triggerBtn != null)
            {
                triggerBtn.IsEnabled = false;
                triggerBtn.Content = "⏳...";
            }

            bool shouldCreateRestorePoint = (_chkDebloatRestorePoint != null && _chkDebloatRestorePoint.IsChecked == true);
            string activeProfile = _selectedProfile ?? "";

            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                if (shouldCreateRestorePoint)
                {
                    SystemRestoreHelper.CreateRestorePoint("OptiAssist Debloater (" + (string.IsNullOrEmpty(activeProfile) ? "Custom" : activeProfile) + ")", HostLog);
                }

                DebloatBackupSession session = _debloatEngine.ApplyTweaksWithSnapshot(tweaksToApply, HostLog, T, activeProfile);
                _lastDebloatSessionId = session != null ? session.sessionId : "";

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (triggerBtn != null)
                    {
                        triggerBtn.IsEnabled = true;
                        triggerBtn.Content = origContent;
                    }
                    HostLog("[DEBLOATER] ✔️ Declarative execution & snapshot completed.");
                    RebuildDebloaterView();
                }));
            });
        }

        #endregion

        #region TAB 3: WINDOWS COMPATIBILITY CHECKER

        public void RebuildCompatibilityView(bool forceRefresh)
        {
            if (_compatHost == null) return;
            string ov = (_host != null && _host.Settings != null) ? _host.Settings.DebloaterProfileOverride : null;
            PlatformDiagnosticSnapshot snap = PlatformDiagnosticService.Instance.GetSnapshot(forceRefresh, ov, HostLog);
            UIElement compatView = BuildCompatibilityContent(snap);
            FluentIcons.UpgradeVisualTree(compatView);
            _compatHost.Content = compatView;
        }

        private UIElement BuildCompatibilityContent(PlatformDiagnosticSnapshot snap)
        {
            StackPanel root = new StackPanel() { Margin = new Thickness(0, 0, 0, 12) };
            PlatformInfo plat = snap != null ? snap.Platform : null;
            Win11CompatReport compat = snap != null ? snap.Win11Compat : null;

            // 1. Verdict Banner Card
            Border verdictCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            verdictCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");

            string verdictBrushKey = "BrushSuccess";
            string verdictIcon = "✅";
            string verdictTierExplain = "This hardware meets modern requirements (Core 3rd Gen+ / AMD Zen+, UEFI, SSD, modern GPU) and is ready for Windows 11 24H2 or Windows 11 IoT Enterprise LTSC 2024.";

            if (compat != null)
            {
                if (compat.Tier == Win11CompatTier.LayerC || !compat.HardGatesPassed)
                {
                    verdictBrushKey = "BrushDanger";
                    verdictIcon = "🛑";
                    verdictTierExplain = "Legacy hardware or missing mandatory Windows 11 24H2 requirements (SSE4.2 / CMPXCHG16B / UEFI / legacy chipset). Staying on Windows 10 IoT Enterprise LTSC 2021 (supported until Jan 2032) is strongly recommended.";
                }
                else if (compat.Tier == Win11CompatTier.LayerB)
                {
                    verdictBrushKey = "BrushWarning";
                    verdictIcon = "⚠️";
                    verdictTierExplain = "Intermediate hardware (Sandy Bridge / Bulldozer with UEFI & SSD). Functional on Windows 11 with bypass, but review the warnings below (e.g. Intel HD 2000/3000 stuttering at 1080p).";
                }
            }
            verdictCard.SetResourceReference(Border.BorderBrushProperty, verdictBrushKey);

            Grid verdictGrid = new Grid();
            verdictGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            verdictGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel verdictText = new StackPanel();
            TextBlock lblVerdict = new TextBlock()
            {
                Text = verdictIcon + "  " + (compat != null ? compat.Title : "Windows Compatibility Check"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            lblVerdict.SetResourceReference(TextBlock.ForegroundProperty, verdictBrushKey);
            verdictText.Children.Add(lblVerdict);

            TextBlock lblVerdictDesc = new TextBlock()
            {
                Text = verdictTierExplain,
                FontSize = 11.5,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            lblVerdictDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            verdictText.Children.Add(lblVerdictDesc);

            if (compat != null && compat.HardGateFailures.Count > 0)
            {
                TextBlock lblFail = new TextBlock()
                {
                    Text = "❌ Blockers: " + string.Join(" • ", compat.HardGateFailures.ToArray()),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                lblFail.SetResourceReference(TextBlock.ForegroundProperty, "BrushDanger");
                verdictText.Children.Add(lblFail);
            }

            if (compat != null && compat.Warnings.Count > 0)
            {
                TextBlock lblWarn = new TextBlock()
                {
                    Text = "⚠️ Warnings: " + string.Join(" • ", compat.Warnings.ToArray()),
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                lblWarn.SetResourceReference(TextBlock.ForegroundProperty, "BrushWarning");
                verdictText.Children.Add(lblWarn);
            }

            Grid.SetColumn(verdictText, 0);
            verdictGrid.Children.Add(verdictText);

            Button btnRefresh = new Button()
            {
                Content = "🔄 " + T("Btn_Refresh", "Refresh"),
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 0, 0, 0)
            };
            btnRefresh.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnRefresh.Click += (s, e) => RebuildCompatibilityView(true);
            Grid.SetColumn(btnRefresh, 1);
            verdictGrid.Children.Add(btnRefresh);

            verdictCard.Child = verdictGrid;

            // Hide Windows 11 Compatibility banner when hardware is officially compatible (Layer A)
            bool showWin11CompatCard = (compat != null && (compat.Tier != Win11CompatTier.LayerA || !compat.HardGatesPassed));
            if (showWin11CompatCard)
            {
                root.Children.Add(verdictCard);
            }

            // 2. Simplified Hardware Specs Card (CPU, GPU, RAM, Storage Type, Firmware + right-aligned badges)
            Border specsCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            specsCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            specsCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel spSpecs = new StackPanel();
            Grid specsHdr = new Grid() { Margin = new Thickness(0, 0, 0, 10) };
            specsHdr.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            specsHdr.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            TextBlock lblSpecsTitle = new TextBlock()
            {
                Text = "🖥️ " + T("Compat_HwSpecs_Title", "Hardware Specs"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            lblSpecsTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(lblSpecsTitle, 0);
            specsHdr.Children.Add(lblSpecsTitle);

            StackPanel specsActions = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Button btnSupportLifecycle = new Button()
            {
                Content = "📅 " + T("Compat_Btn_SupportLifecycle", "Windows Support Lifecycle"),
                Height = 26,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnSupportLifecycle.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnSupportLifecycle.Click += (s, e) =>
            {
                string lifeText = (compat != null)
                    ? string.Format("Recommended Edition: {0}\n\n{1}\n\nReference Support Dates (End of Servicing):\n• Windows 10 Home / Pro (22H2): October 14, 2025\n• Windows 10 Enterprise LTSC 2021: January 12, 2027\n• Windows 10 IoT Enterprise LTSC 2021: January 13, 2032\n• Windows 11 Home / Pro (23H2): November 11, 2025\n• Windows 11 Home / Pro (24H2): October 13, 2026\n• Windows 11 Enterprise LTSC 2024: October 9, 2029\n• Windows 11 IoT Enterprise LTSC 2024: October 10, 2034",
                        compat.RecommendedOsEdition,
                        compat.LifecycleSummary)
                    : "Reference Support Dates (End of Servicing):\n• Windows 10 Home / Pro (22H2): October 14, 2025\n• Windows 10 IoT Enterprise LTSC 2021: January 13, 2032\n• Windows 11 Home / Pro (24H2): October 13, 2026\n• Windows 11 IoT Enterprise LTSC 2024: October 10, 2034";
                OptiDialog.Show(lifeText, "OptiAssist - Windows Support Lifecycle", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            specsActions.Children.Add(btnSupportLifecycle);

            if (!showWin11CompatCard)
            {
                Button btnRefreshSpecs = new Button()
                {
                    Content = "🔄 " + T("Btn_Refresh", "Refresh"),
                    Height = 26,
                    Padding = new Thickness(10, 0, 10, 0),
                    FontSize = 11
                };
                btnRefreshSpecs.SetResourceReference(Button.StyleProperty, "BtnDefault");
                btnRefreshSpecs.Click += (s, e) => RebuildCompatibilityView(true);
                specsActions.Children.Add(btnRefreshSpecs);
            }
            Grid.SetColumn(specsActions, 1);
            specsHdr.Children.Add(specsActions);
            spSpecs.Children.Add(specsHdr);

            // 1) CPU
            string cpuVal = (plat != null && plat.Cpu != null && !string.IsNullOrEmpty(plat.Cpu.ProcessorNameString))
                ? plat.Cpu.ProcessorNameString
                : "Unknown";
            int cpuStatus = 1; // 0 = Not Compatible, 1 = Unknown/Not Tested, 2 = Compatible
            if (plat != null && plat.Cpu != null)
            {
                if (!plat.Cpu.HasSse42 || !plat.Cpu.HasCmpxchg16b || plat.Cpu.IsArm64Emulated) cpuStatus = 0;
                else if (plat.Cpu.ReleaseYear >= 2017) cpuStatus = 2;
                else if (plat.Cpu.ReleaseYear > 0 && plat.Cpu.ReleaseYear < 2017) cpuStatus = 1;
                else cpuStatus = 1;
            }
            spSpecs.Children.Add(CreateHardwareSpecRow("CPU:", cpuVal, cpuStatus));

            // 2) GPU
            string gpuVal = "Unknown";
            int gpuStatus = 1;
            if (plat != null && plat.Gpus != null && plat.Gpus.Count > 0)
            {
                List<string> gNames = new List<string>();
                bool hasLegacyUnsupportedGpu = false;
                bool hasModernGpu = false;
                for (int i = 0; i < plat.Gpus.Count; i++)
                {
                    GpuDeviceInfo g = plat.Gpus[i];
                    if (g == null) continue;
                    gNames.Add(g.Name);
                    string gn = (g.Name ?? "").ToLowerInvariant();
                    string arch = (g.ArchitectureFamily ?? "").ToLowerInvariant();
                    if (gn.Contains("gma") || gn.Contains("hd graphics 2000") || gn.Contains("hd graphics 3000") ||
                        gn.Contains("nforce") || gn.Contains("sis ") || gn.Contains("via ") || arch.Contains("terascale"))
                    {
                        hasLegacyUnsupportedGpu = true;
                    }
                    else if (g.IsDiscrete || gn.Contains("uhd") || gn.Contains("iris") || gn.Contains("radeon") || gn.Contains("rtx") || gn.Contains("gtx") || gn.Contains("arc"))
                    {
                        hasModernGpu = true;
                    }
                }
                gpuVal = string.Join(" / ", gNames.ToArray());
                if (hasModernGpu) gpuStatus = 2;
                else if (hasLegacyUnsupportedGpu) gpuStatus = 0;
                else gpuStatus = 1;
            }
            spSpecs.Children.Add(CreateHardwareSpecRow("GPU:", gpuVal, gpuStatus));

            // 3) RAM
            double ramGb = (plat != null && plat.Memory != null) ? plat.Memory.TotalPhysicalGb : 0;
            string ramVal = (plat != null && plat.Memory != null && ramGb > 0)
                ? string.Format("{0:F0} GB {1}", ramGb, string.IsNullOrEmpty(plat.Memory.SmbiosMemoryType) ? "" : plat.Memory.SmbiosMemoryType).Trim()
                : "Unknown";
            int ramStatus = (ramGb >= 4.0) ? 2 : (ramGb > 0 ? 0 : 1);
            spSpecs.Children.Add(CreateHardwareSpecRow("RAM:", ramVal, ramStatus));

            // 4) Storage Type
            string stVal = "Unknown";
            int stStatus = 1;
            if (plat != null && plat.Storage != null)
            {
                stVal = string.Format("{0} ({1})", plat.Storage.SystemDiskMediaType, plat.Storage.SystemDiskBusType);
                if (string.Equals(plat.Storage.SystemDiskMediaType, "SSD", StringComparison.OrdinalIgnoreCase) && !plat.Storage.IsSystemEmmcOrSd)
                {
                    stStatus = 2;
                }
                else if (plat.Storage.IsSystemEmmcOrSd || string.Equals(plat.Storage.SystemDiskMediaType, "HDD", StringComparison.OrdinalIgnoreCase))
                {
                    stStatus = 1;
                }
            }
            spSpecs.Children.Add(CreateHardwareSpecRow(T("Compat_Label_StorageType", "Storage Type:"), stVal, stStatus));

            // 5) Firmware
            string fwVal = "Unknown";
            int fwStatus = 1;
            if (plat != null && plat.OsFirmware != null)
            {
                string fwName = (plat.OsFirmware.Firmware == SystemFirmwareType.Uefi) ? "UEFI"
                    : ((plat.OsFirmware.Firmware == SystemFirmwareType.Bios) ? "Legacy BIOS" : plat.OsFirmware.Firmware.ToString());
                fwVal = fwName + (plat.OsFirmware.IsBayTrail32BitUefi ? " (32-bit Bay Trail)" : "");
                if (plat.OsFirmware.Firmware == SystemFirmwareType.Uefi && !plat.OsFirmware.IsBayTrail32BitUefi) fwStatus = 2;
                else if (plat.OsFirmware.Firmware == SystemFirmwareType.Bios || plat.OsFirmware.IsBayTrail32BitUefi) fwStatus = 0;
            }
            spSpecs.Children.Add(CreateHardwareSpecRow("Firmware:", fwVal, fwStatus));

            specsCard.Child = spSpecs;
            root.Children.Add(specsCard);

            // 3. Hardware Quirks & Known Issues Section
            Border quirksCard = new Border()
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14)
            };
            quirksCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            quirksCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel spQuirks = new StackPanel();
            TextBlock lblQuirksTitle = new TextBlock()
            {
                Text = "🔍 " + T("Compat_Quirks_Title", "Detected Hardware Quirks & Known Driver Issues"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            lblQuirksTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spQuirks.Children.Add(lblQuirksTitle);

            int quirkCount = 0;
            if (snap != null && snap.Recommendations != null)
            {
                for (int i = 0; i < snap.Recommendations.Count; i++)
                {
                    DiagnosticRecommendationItem item = snap.Recommendations[i];
                    if (item == null || !string.Equals(item.Category, "HardwareQuirk", StringComparison.OrdinalIgnoreCase)) continue;
                    quirkCount++;

                    Border qRow = new Border()
                    {
                        CornerRadius = new CornerRadius(6),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    qRow.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
                    qRow.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                    Grid qg = new Grid();
                    qg.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    qg.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                    StackPanel ql = new StackPanel();
                    TextBlock qt = new TextBlock()
                    {
                        Text = "⚠️ " + item.Title,
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold
                    };
                    qt.SetResourceReference(TextBlock.ForegroundProperty, item.Severity == RecommendationSeverity.Critical ? "BrushDanger" : "BrushWarning");
                    TextBlock qd = new TextBlock()
                    {
                        Text = item.Description + (string.IsNullOrEmpty(item.Notes) ? "" : " — " + item.Notes),
                        FontSize = 10.5,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    qd.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                    ql.Children.Add(qt);
                    ql.Children.Add(qd);
                    Grid.SetColumn(ql, 0);
                    qg.Children.Add(ql);

                    if (!string.IsNullOrEmpty(item.TargetRegistryPath) && !string.IsNullOrEmpty(item.TargetValueName))
                    {
                        DiagnosticRecommendationItem capturedItem = item;
                        Button btnFix = new Button()
                        {
                            Content = string.IsNullOrEmpty(item.ActionLabel) ? "Apply Fix" : item.ActionLabel,
                            Height = 28,
                            Padding = new Thickness(12, 0, 12, 0),
                            FontSize = 11,
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        btnFix.SetResourceReference(Button.StyleProperty, "BtnAccent");
                        btnFix.Click += (s, e) =>
                        {
                            bool ok = PlatformDiagnosticService.Instance.ApplyQuickRegistryFix(capturedItem, HostLog);
                            if (ok)
                            {
                                OptiDialog.Show("Fix applied successfully! Rollback snapshot saved in Config\\Debloater\\backups.", "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                                if (_onQuickFixApplied != null) _onQuickFixApplied();
                            }
                        };
                        Grid.SetColumn(btnFix, 1);
                        qg.Children.Add(btnFix);
                    }

                    qRow.Child = qg;
                    spQuirks.Children.Add(qRow);
                }
            }

            if (quirkCount == 0)
            {
                TextBlock noQuirks = new TextBlock()
                {
                    Text = "✔️ " + T("Compat_NoQuirks", "No known hardware or driver quirks (out of 13 documented scenarios) were detected on this machine."),
                    FontSize = 11
                };
                noQuirks.SetResourceReference(TextBlock.ForegroundProperty, "BrushSuccess");
                spQuirks.Children.Add(noQuirks);
            }

            quirksCard.Child = spQuirks;
            root.Children.Add(quirksCard);

            return root;
        }

        private Border CreateHardwareSpecRow(string label, string value, int status)
        {
            // status: 2 = Compatible (green), 1 = Unknown or Not Tested (amber), 0 = Not Compatible (red)
            Border rowBorder = new Border()
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 8)
            };
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(110) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            TextBlock tbLabel = new TextBlock()
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tbLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            Grid.SetColumn(tbLabel, 0);
            g.Children.Add(tbLabel);

            TextBlock tbVal = new TextBlock()
            {
                Text = value ?? "Unknown",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 12, 0)
            };
            tbVal.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(tbVal, 1);
            g.Children.Add(tbVal);

            Color fg;
            string badgeText;
            if (status == 2)
            {
                fg = Color.FromRgb(16, 185, 129);
                badgeText = T("Compat_Badge_Compatible", "Compatible");
            }
            else if (status == 0)
            {
                fg = Color.FromRgb(239, 68, 68);
                badgeText = T("Compat_Badge_NotCompatible", "Not Compatible");
            }
            else
            {
                fg = Color.FromRgb(245, 158, 11);
                badgeText = T("Compat_Badge_Unknown", "Unknown or Not Tested");
            }

            Border badge = new Border()
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(30, fg.R, fg.G, fg.B))
            };
            badge.Child = new TextBlock()
            {
                Text = badgeText,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(fg)
            };
            Grid.SetColumn(badge, 2);
            g.Children.Add(badge);

            rowBorder.Child = g;
            return rowBorder;
        }

        #endregion
    }
}
