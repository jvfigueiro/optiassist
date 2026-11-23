using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OptiAssist
{
    /// <summary>
    /// Unified OptiAssist-styled modal dialog and text viewer replacing native Windows MessageBox / chrome.
    /// Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public static class OptiDialog
    {
        public static MessageBoxResult Show(
            Window owner,
            string message,
            string title = "OptiAssist",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            return Show(message, title, buttons, icon, owner);
        }

        public static MessageBoxResult Show(
            string message,
            string title = "OptiAssist",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information,
            Window owner = null)
        {
            MessageBoxResult result = MessageBoxResult.None;

            Window resolvedOwner = owner;
            if (resolvedOwner == null && Application.Current != null)
            {
                resolvedOwner = Application.Current.MainWindow;
            }

            Window dlg = new Window();
            dlg.Title = string.IsNullOrEmpty(title) ? "OptiAssist" : title;
            dlg.WindowStyle = WindowStyle.None;
            dlg.AllowsTransparency = true;
            dlg.Background = Brushes.Transparent;
            dlg.ResizeMode = ResizeMode.NoResize;
            dlg.SizeToContent = SizeToContent.Height;
            dlg.Width = 480;
            dlg.MaxHeight = 560;
            dlg.ShowInTaskbar = false;
            if (resolvedOwner != null && resolvedOwner.IsVisible)
            {
                dlg.Owner = resolvedOwner;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            Border chrome = new Border();
            chrome.CornerRadius = new CornerRadius(10);
            chrome.BorderThickness = new Thickness(1);
            chrome.Margin = new Thickness(12);
            chrome.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            chrome.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            chrome.Effect = new DropShadowEffect()
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.30,
                Color = Colors.Black
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            // Header bar (draggable)
            Border headerBar = new Border();
            headerBar.CornerRadius = new CornerRadius(10, 10, 0, 0);
            headerBar.BorderThickness = new Thickness(0, 0, 0, 1);
            headerBar.Padding = new Thickness(16, 11, 12, 11);
            headerBar.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            headerBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            headerBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { dlg.DragMove(); } catch { }
                }
            };

            Grid hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            string iconGlyph = "ℹ️";
            string accentKey = "BrushAccent";
            if (icon == MessageBoxImage.Warning || icon == MessageBoxImage.Exclamation)
            {
                iconGlyph = "⚠️";
                accentKey = "BrushWarning";
            }
            else if (icon == MessageBoxImage.Error || icon == MessageBoxImage.Stop)
            {
                iconGlyph = "🛑";
                accentKey = "BrushDanger";
            }
            else if (icon == MessageBoxImage.Question)
            {
                iconGlyph = "❓";
                accentKey = "BrushAccent";
            }

            TextBlock tbIcon = new TextBlock();
            tbIcon.Text = iconGlyph;
            tbIcon.FontSize = 15;
            tbIcon.VerticalAlignment = VerticalAlignment.Center;
            tbIcon.Margin = new Thickness(0, 0, 8, 0);
            tbIcon.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
            Grid.SetColumn(tbIcon, 0);
            hdrGrid.Children.Add(tbIcon);

            TextBlock tbTitle = new TextBlock();
            tbTitle.Text = dlg.Title;
            tbTitle.FontSize = 13;
            tbTitle.FontWeight = FontWeights.Bold;
            tbTitle.VerticalAlignment = VerticalAlignment.Center;
            tbTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
            Grid.SetColumn(tbTitle, 1);
            hdrGrid.Children.Add(tbTitle);

            Button btnCloseX = new Button();
            btnCloseX.Content = "✕";
            btnCloseX.Width = 26;
            btnCloseX.Height = 26;
            btnCloseX.FontSize = 11;
            btnCloseX.Padding = new Thickness(0);
            btnCloseX.Cursor = Cursors.Hand;
            btnCloseX.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCloseX.Click += (s, e) =>
            {
                result = (buttons == MessageBoxButton.YesNo) ? MessageBoxResult.No : MessageBoxResult.Cancel;
                dlg.Close();
            };
            Grid.SetColumn(btnCloseX, 2);
            hdrGrid.Children.Add(btnCloseX);

            headerBar.Child = hdrGrid;
            Grid.SetRow(headerBar, 0);
            layout.Children.Add(headerBar);

            // Message Body
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.MaxHeight = 360;
            sv.Margin = new Thickness(18, 14, 18, 14);

            TextBlock tbMsg = new TextBlock();
            tbMsg.Text = message ?? "";
            tbMsg.FontSize = 12;
            tbMsg.LineHeight = 18;
            tbMsg.TextWrapping = TextWrapping.Wrap;
            tbMsg.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            sv.Content = tbMsg;
            Grid.SetRow(sv, 1);
            layout.Children.Add(sv);

            // Footer Action Buttons
            Border footer = new Border();
            footer.CornerRadius = new CornerRadius(0, 0, 10, 10);
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(16, 10, 16, 12);
            footer.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            footer.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel btnRow = new StackPanel();
            btnRow.Orientation = Orientation.Horizontal;
            btnRow.HorizontalAlignment = HorizontalAlignment.Right;

            bool isPt = (LocalizationHelper.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);

            if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
            {
                Button btnNo = CreateDialogButton(isPt ? "Não" : "No", false);
                btnNo.Click += (s, e) => { result = MessageBoxResult.No; dlg.Close(); };

                Button btnYes = CreateDialogButton(isPt ? "Sim" : "Yes", true);
                btnYes.IsDefault = true;
                btnYes.Click += (s, e) => { result = MessageBoxResult.Yes; dlg.Close(); };

                if (buttons == MessageBoxButton.YesNoCancel)
                {
                    Button btnCancel = CreateDialogButton(isPt ? "Cancelar" : "Cancel", false);
                    btnCancel.IsCancel = true;
                    btnCancel.Click += (s, e) => { result = MessageBoxResult.Cancel; dlg.Close(); };
                    btnRow.Children.Add(btnCancel);
                }
                else
                {
                    btnNo.IsCancel = true;
                }

                btnRow.Children.Add(btnNo);
                btnRow.Children.Add(btnYes);
            }
            else if (buttons == MessageBoxButton.OKCancel)
            {
                Button btnCancel = CreateDialogButton(isPt ? "Cancelar" : "Cancel", false);
                btnCancel.IsCancel = true;
                btnCancel.Click += (s, e) => { result = MessageBoxResult.Cancel; dlg.Close(); };

                Button btnOk = CreateDialogButton("OK", true);
                btnOk.IsDefault = true;
                btnOk.Click += (s, e) => { result = MessageBoxResult.OK; dlg.Close(); };

                btnRow.Children.Add(btnCancel);
                btnRow.Children.Add(btnOk);
            }
            else
            {
                Button btnOk = CreateDialogButton("OK", true);
                btnOk.IsDefault = true;
                btnOk.IsCancel = true;
                btnOk.Click += (s, e) => { result = MessageBoxResult.OK; dlg.Close(); };
                btnRow.Children.Add(btnOk);
            }

            footer.Child = btnRow;
            Grid.SetRow(footer, 2);
            layout.Children.Add(footer);

            chrome.Child = layout;
            dlg.Content = chrome;
            FluentIcons.UpgradeVisualTree(chrome);

            dlg.ShowDialog();
            return result;
        }

        public static MessageBoxResult ShowWithCheckbox(
            string message,
            string title,
            string checkboxLabel,
            bool defaultChecked,
            out bool isChecked,
            MessageBoxButton buttons = MessageBoxButton.YesNo,
            MessageBoxImage icon = MessageBoxImage.Warning,
            Window owner = null)
        {
            bool chkState = defaultChecked;
            MessageBoxResult result = MessageBoxResult.None;

            Window resolvedOwner = owner;
            if (resolvedOwner == null && Application.Current != null)
            {
                resolvedOwner = Application.Current.MainWindow;
            }

            Window dlg = new Window();
            dlg.Title = string.IsNullOrEmpty(title) ? "OptiAssist" : title;
            dlg.WindowStyle = WindowStyle.None;
            dlg.AllowsTransparency = true;
            dlg.Background = Brushes.Transparent;
            dlg.ResizeMode = ResizeMode.NoResize;
            dlg.SizeToContent = SizeToContent.Height;
            dlg.Width = 500;
            dlg.MaxHeight = 600;
            dlg.ShowInTaskbar = false;
            if (resolvedOwner != null && resolvedOwner.IsVisible)
            {
                dlg.Owner = resolvedOwner;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            Border chrome = new Border();
            chrome.CornerRadius = new CornerRadius(10);
            chrome.BorderThickness = new Thickness(1);
            chrome.Margin = new Thickness(12);
            chrome.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            chrome.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            chrome.Effect = new DropShadowEffect()
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.30,
                Color = Colors.Black
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            // Header bar
            Border headerBar = new Border();
            headerBar.CornerRadius = new CornerRadius(10, 10, 0, 0);
            headerBar.BorderThickness = new Thickness(0, 0, 0, 1);
            headerBar.Padding = new Thickness(16, 11, 12, 11);
            headerBar.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            headerBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            headerBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { dlg.DragMove(); } catch { }
                }
            };

            Grid hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            string iconGlyph = (icon == MessageBoxImage.Warning || icon == MessageBoxImage.Exclamation) ? "⚠️"
                : (icon == MessageBoxImage.Error || icon == MessageBoxImage.Stop) ? "🛑"
                : (icon == MessageBoxImage.Question) ? "❓" : "ℹ️";
            string accentKey = (icon == MessageBoxImage.Warning || icon == MessageBoxImage.Exclamation) ? "BrushWarning"
                : (icon == MessageBoxImage.Error || icon == MessageBoxImage.Stop) ? "BrushDanger" : "BrushAccent";

            TextBlock tbIcon = new TextBlock() { Text = iconGlyph, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            tbIcon.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
            Grid.SetColumn(tbIcon, 0);
            hdrGrid.Children.Add(tbIcon);

            TextBlock tbTitle = new TextBlock() { Text = dlg.Title, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
            Grid.SetColumn(tbTitle, 1);
            hdrGrid.Children.Add(tbTitle);

            Button btnCloseX = new Button() { Content = "✕", Width = 26, Height = 26, FontSize = 11, Padding = new Thickness(0), Cursor = Cursors.Hand };
            btnCloseX.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCloseX.Click += (s, e) => { result = MessageBoxResult.No; dlg.Close(); };
            Grid.SetColumn(btnCloseX, 2);
            hdrGrid.Children.Add(btnCloseX);

            headerBar.Child = hdrGrid;
            Grid.SetRow(headerBar, 0);
            layout.Children.Add(headerBar);

            // Message Body
            ScrollViewer sv = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 320,
                Margin = new Thickness(18, 14, 18, 10)
            };
            TextBlock tbMsg = new TextBlock()
            {
                Text = message ?? "",
                FontSize = 12,
                LineHeight = 18,
                TextWrapping = TextWrapping.Wrap
            };
            tbMsg.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            sv.Content = tbMsg;
            Grid.SetRow(sv, 1);
            layout.Children.Add(sv);

            // Option Checkbox Strip
            Border chkBorder = new Border()
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(18, 0, 18, 12)
            };
            chkBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            chkBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            CheckBox chkOption = new CheckBox()
            {
                Content = checkboxLabel ?? "",
                IsChecked = defaultChecked,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            chkOption.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
            chkOption.Checked += (s, e) => { chkState = true; };
            chkOption.Unchecked += (s, e) => { chkState = false; };
            chkBorder.Child = chkOption;
            Grid.SetRow(chkBorder, 2);
            layout.Children.Add(chkBorder);

            // Footer Buttons
            Border footer = new Border()
            {
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 10, 16, 12)
            };
            footer.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            footer.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel btnRow = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            bool isPt = (LocalizationHelper.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);

            Button btnCancel = new Button()
            {
                Content = isPt ? "Cancelar" : "Cancel",
                MinWidth = 86,
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btnCancel.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCancel.Click += (s, e) => { result = (buttons == MessageBoxButton.YesNo) ? MessageBoxResult.No : MessageBoxResult.Cancel; dlg.Close(); };

            Button btnOk = new Button()
            {
                Content = isPt ? "Continuar" : "Continue",
                MinWidth = 96,
                Height = 30,
                Padding = new Thickness(16, 0, 16, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnOk.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnOk.Click += (s, e) => { result = (buttons == MessageBoxButton.YesNo) ? MessageBoxResult.Yes : MessageBoxResult.OK; dlg.Close(); };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnOk);
            footer.Child = btnRow;
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            chrome.Child = layout;
            dlg.Content = chrome;
            FluentIcons.UpgradeVisualTree(chrome);
            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    result = (buttons == MessageBoxButton.YesNo) ? MessageBoxResult.No : MessageBoxResult.Cancel;
                    dlg.Close();
                }
            };

            dlg.ShowDialog();
            isChecked = chkState;
            return result;
        }

        public static void ShowTextViewer(string title, string content, Window owner = null, double width = 760, double height = 540)
        {
            Window resolvedOwner = owner;
            if (resolvedOwner == null && Application.Current != null)
            {
                resolvedOwner = Application.Current.MainWindow;
            }

            Window dlg = new Window();
            dlg.Title = string.IsNullOrEmpty(title) ? "OptiAssist" : title;
            dlg.WindowStyle = WindowStyle.None;
            dlg.AllowsTransparency = true;
            dlg.Background = Brushes.Transparent;
            dlg.ResizeMode = ResizeMode.CanResizeWithGrip;
            dlg.Width = width;
            dlg.Height = height;
            dlg.ShowInTaskbar = false;
            if (resolvedOwner != null && resolvedOwner.IsVisible)
            {
                dlg.Owner = resolvedOwner;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            Border chrome = new Border();
            chrome.CornerRadius = new CornerRadius(10);
            chrome.BorderThickness = new Thickness(1);
            chrome.Margin = new Thickness(12);
            chrome.SetResourceReference(Border.BackgroundProperty, "BrushBgPrimary");
            chrome.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            chrome.Effect = new DropShadowEffect()
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.30,
                Color = Colors.Black
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            Border headerBar = new Border();
            headerBar.CornerRadius = new CornerRadius(10, 10, 0, 0);
            headerBar.BorderThickness = new Thickness(0, 0, 0, 1);
            headerBar.Padding = new Thickness(16, 11, 12, 11);
            headerBar.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            headerBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            headerBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { dlg.DragMove(); } catch { }
                }
            };

            Grid hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            TextBlock tbTitle = new TextBlock();
            tbTitle.Text = "📄  " + dlg.Title;
            tbTitle.FontSize = 13;
            tbTitle.FontWeight = FontWeights.Bold;
            tbTitle.VerticalAlignment = VerticalAlignment.Center;
            tbTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            Grid.SetColumn(tbTitle, 0);
            hdrGrid.Children.Add(tbTitle);

            Button btnCloseX = new Button();
            btnCloseX.Content = "✕";
            btnCloseX.Width = 26;
            btnCloseX.Height = 26;
            btnCloseX.FontSize = 11;
            btnCloseX.Padding = new Thickness(0);
            btnCloseX.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnCloseX.Click += (s, e) => dlg.Close();
            Grid.SetColumn(btnCloseX, 1);
            hdrGrid.Children.Add(btnCloseX);

            headerBar.Child = hdrGrid;
            Grid.SetRow(headerBar, 0);
            layout.Children.Add(headerBar);

            TextBox tb = new TextBox();
            tb.Text = content ?? "";
            tb.IsReadOnly = true;
            tb.FontFamily = new FontFamily("Consolas");
            tb.FontSize = 11.5;
            tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            tb.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            tb.TextWrapping = TextWrapping.NoWrap;
            tb.Padding = new Thickness(12);
            tb.Margin = new Thickness(14);
            tb.BorderThickness = new Thickness(1);
            tb.SetResourceReference(TextBox.BackgroundProperty, "BrushBgCard");
            tb.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            tb.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderCard");
            Grid.SetRow(tb, 1);
            layout.Children.Add(tb);

            Border footer = new Border();
            footer.CornerRadius = new CornerRadius(0, 0, 10, 10);
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(16, 10, 16, 12);
            footer.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            footer.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            bool isPt = (LocalizationHelper.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);
            StackPanel btnRow = new StackPanel();
            btnRow.Orientation = Orientation.Horizontal;
            btnRow.HorizontalAlignment = HorizontalAlignment.Right;

            Button btnCopy = CreateDialogButton(isPt ? "📋 Copiar" : "📋 Copy", false);
            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(content ?? ""); } catch { }
            };
            Button btnClose = CreateDialogButton(isPt ? "Fechar" : "Close", true);
            btnClose.IsCancel = true;
            btnClose.IsDefault = true;
            btnClose.Click += (s, e) => dlg.Close();

            btnRow.Children.Add(btnCopy);
            btnRow.Children.Add(btnClose);
            footer.Child = btnRow;
            Grid.SetRow(footer, 2);
            layout.Children.Add(footer);

            chrome.Child = layout;
            dlg.Content = chrome;
            FluentIcons.UpgradeVisualTree(chrome);
            dlg.ShowDialog();
        }

        private static Button CreateDialogButton(string text, bool isPrimary)
        {
            Button btn = new Button();
            btn.Content = text;
            btn.Height = 30;
            btn.MinWidth = 84;
            btn.Padding = new Thickness(14, 0, 14, 0);
            btn.FontSize = 11.5;
            btn.FontWeight = isPrimary ? FontWeights.Bold : FontWeights.SemiBold;
            btn.Margin = new Thickness(8, 0, 0, 0);
            btn.Cursor = Cursors.Hand;
            btn.SetResourceReference(Button.StyleProperty, isPrimary ? "BtnAccent" : "BtnDefault");
            return btn;
        }
    }
}
