using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace OptiAssist
{
    /// <summary>
    /// Pop-out external window for viewing and monitoring real-time OptiAssist logs in a separate window.
    /// </summary>
    public class LogWindow : Window
    {
        private readonly MainWindow _parent;
        private TextBox _txtConsole;
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnExport;
        private Button _btnCopy;
        private Button _btnClear;
        private Border _borderConsole;

        public LogWindow(MainWindow parent)
        {
            _parent = parent;

            this.Title = "OptiAssist - Logs";
            this.Width = 850;
            this.Height = 550;
            this.MinWidth = 500;
            this.MinHeight = 350;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Inherit application styles and colors
            this.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");
            this.SetResourceReference(Window.ForegroundProperty, "BrushTextPrimary");

            try
            {
                if (_parent != null && _parent.Icon != null)
                {
                    this.Icon = _parent.Icon;
                }
            }
            catch { }

            Grid mainGrid = new Grid() { Margin = new Thickness(14) };
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // Console

            // Top Header: Title + Action Buttons
            Grid headerGrid = new Grid() { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel spTitle = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            _txtTitle = new TextBlock()
            {
                Text = LocalizationHelper.Get("Logs_Title", "📜 Logs"),
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            spTitle.Children.Add(_txtTitle);

            _txtSubtitle = new TextBlock()
            {
                Text = LocalizationHelper.Get("Logs_Subtitle", "Real-time log of diagnostics, scripts, and system executions"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            spTitle.Children.Add(_txtSubtitle);

            Grid.SetColumn(spTitle, 0);
            headerGrid.Children.Add(spTitle);

            StackPanel spButtons = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnExport = new Button()
            {
                Content = LocalizationHelper.Get("Btn_ExportLog", "💾 Export Log"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 11.5
            };
            _btnExport.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnExport.Click += BtnExport_Click;
            spButtons.Children.Add(_btnExport);

            _btnCopy = new Button()
            {
                Content = LocalizationHelper.Get("Btn_CopyAll", "📋 Copy All"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 11.5
            };
            _btnCopy.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCopy.Click += BtnCopy_Click;
            spButtons.Children.Add(_btnCopy);

            _btnClear = new Button()
            {
                Content = LocalizationHelper.Get("Btn_ClearLog", "🗑️ Clear Console"),
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11.5
            };
            _btnClear.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnClear.Click += BtnClear_Click;
            spButtons.Children.Add(_btnClear);

            Grid.SetColumn(spButtons, 1);
            headerGrid.Children.Add(spButtons);

            Grid.SetRow(headerGrid, 0);
            mainGrid.Children.Add(headerGrid);

            // Center: Monospace Console / Terminal Window
            _borderConsole = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1)
            };
            _borderConsole.SetResourceReference(Border.BackgroundProperty, "BrushConsoleBg");
            _borderConsole.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            _txtConsole = new TextBox()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas, Lucida Console, Courier New"),
                FontSize = 12,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 10, 12, 10)
            };
            _txtConsole.SetResourceReference(TextBox.ForegroundProperty, "BrushConsoleText");
            _borderConsole.Child = _txtConsole;

            Grid.SetRow(_borderConsole, 1);
            mainGrid.Children.Add(_borderConsole);

            this.Content = mainGrid;
            this.Closed += LogWindow_Closed;

            try
            {
                if (_parent != null)
                {
                    ApplyTheme(_parent.IsDarkMode);
                }
            }
            catch { }
        }

        public void ApplyTheme(bool isDark)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(() => ApplyTheme(isDark)));
                return;
            }

            Brush bg = TryGetBrush("BrushBgPrimary", isDark ? "#18181B" : "#F1F5F9");
            Brush textPrimary = TryGetBrush("BrushTextPrimary", isDark ? "#F8FAFC" : "#0F172A");
            Brush textSecondary = TryGetBrush("BrushTextSecondary", isDark ? "#94A3B8" : "#475569");
            Brush borderCard = TryGetBrush("BrushBorderCard", isDark ? "#33333D" : "#CBD5E1");
            Brush consoleBg = TryGetBrush("BrushConsoleBg", isDark ? "#0C0C0F" : "#0F172A");
            Brush consoleText = TryGetBrush("BrushConsoleText", isDark ? "#E2E8F0" : "#F8FAFC");

            this.Background = bg;
            this.Foreground = textPrimary;

            if (_txtTitle != null) _txtTitle.Foreground = textPrimary;
            if (_txtSubtitle != null) _txtSubtitle.Foreground = textSecondary;
            if (_borderConsole != null)
            {
                _borderConsole.Background = consoleBg;
                _borderConsole.BorderBrush = borderCard;
            }
            if (_txtConsole != null)
            {
                _txtConsole.Foreground = consoleText;
            }
        }

        private static Brush TryGetBrush(string resourceKey, string fallbackHex)
        {
            if (Application.Current != null && Application.Current.Resources.Contains(resourceKey))
            {
                Brush b = Application.Current.Resources[resourceKey] as Brush;
                if (b != null) return b;
            }
            try
            {
                var col = (Color)ColorConverter.ConvertFromString(fallbackHex);
                return new SolidColorBrush(col);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public void SetLogText(string text)
        {
            if (_txtConsole != null)
            {
                _txtConsole.Text = text ?? "";
                _txtConsole.ScrollToEnd();
            }
        }

        public void AppendLog(string message)
        {
            if (_txtConsole == null) return;

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_txtConsole.Text.Length > 0)
                {
                    _txtConsole.AppendText(Environment.NewLine + message);
                }
                else
                {
                    _txtConsole.AppendText(message);
                }
                _txtConsole.ScrollToEnd();
            }));
        }

        public void ClearLog()
        {
            if (_txtConsole != null)
            {
                _txtConsole.Clear();
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|All Files (*.*)|*.*";
                sfd.FileName = string.Format("OptiAssist_Log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog(this) == true)
                {
                    File.WriteAllText(sfd.FileName, _txtConsole.Text, Encoding.UTF8);
                    MessageBox.Show(this,
                        string.Format(LocalizationHelper.Get("Msg_LogSaved", "Log successfully saved to:\n{0}"), sfd.FileName),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    string.Format(LocalizationHelper.Get("Msg_LogSaveError", "Failed to save log: {0}"), ex.Message),
                    "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_txtConsole.Text))
                {
                    Clipboard.SetText(_txtConsole.Text);
                    MessageBox.Show(this,
                        LocalizationHelper.Get("Msg_LogCopied", "Console log copied to clipboard!"),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        LocalizationHelper.Get("Msg_LogEmpty", "The terminal log is currently empty."),
                        "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearLog();
            if (_parent != null)
            {
                _parent.ClearMainLogConsole();
            }
        }

        private void LogWindow_Closed(object sender, EventArgs e)
        {
            if (_parent != null)
            {
                _parent.OnExternalLogWindowClosed();
            }
        }
    }
}

