using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OptiAssist.Addons.MaintenanceReport
{
    public class MaintenanceRecord
    {
        public string CustomerName { get; set; }
        public string ContactInfo { get; set; }

        public string StoreName
        {
            get { return CustomerName; }
            set { CustomerName = value; }
        }
        public string Phone
        {
            get { return ContactInfo; }
            set { ContactInfo = value; }
        }

        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string OrderNumber { get; set; }
        public string SerialNumber { get; set; }
        public DateTime ExecutionDate { get; set; }
        public HardwareSummary Hardware { get; set; }
        public List<string> ActionsDone { get; set; }
        public string Notes { get; set; }

        public MaintenanceRecord()
        {
            ExecutionDate = DateTime.Now;
            ActionsDone = new List<string>();
            SerialNumber = "N/A";
            ContactInfo = "";
            OrderNumber = "";
            ClientName = "";
            ClientPhone = "";
            Notes = "";
            CustomerName = "OptiAssist";
        }
    }

    public static class ReportHelper
    {
        private const string DividerMajor = "=============================================================";
        private const string DividerMinor = "-------------------------------------------------------------";

        public static string GenerateReportText(MaintenanceRecord rec)
        {
            StringBuilder sb = new StringBuilder();

            // Header
            sb.AppendLine(DividerMajor);

            string customer = !string.IsNullOrWhiteSpace(rec.CustomerName) && rec.CustomerName != "N/A"
                ? rec.CustomerName.Trim()
                : "OptiAssist";
            string contact = !string.IsNullOrWhiteSpace(rec.ContactInfo) && rec.ContactInfo != "N/A"
                ? rec.ContactInfo.Trim()
                : "";

            sb.AppendLine(string.Format("\t\t    {0}", customer));
            if (!string.IsNullOrEmpty(contact))
            {
                sb.AppendLine(string.Format("\t\t    {0}", contact));
            }
            sb.AppendLine("\t\t   Maintenance Report");
            sb.AppendLine(DividerMajor);
            sb.AppendLine();

            // Service Details
            sb.AppendLine("SERVICE DETAILS");
            sb.AppendLine(DividerMinor);
            string clientDisplay = !string.IsNullOrWhiteSpace(rec.ClientName) ? rec.ClientName.Trim() : "N/A";
            if (!string.IsNullOrWhiteSpace(rec.ClientPhone) && clientDisplay != "N/A")
            {
                clientDisplay += string.Format(" ({0})", rec.ClientPhone.Trim());
            }
            sb.AppendLine(string.Format("Client:\t\t{0}", clientDisplay));
            sb.AppendLine(string.Format("Order No.:\t{0}", string.IsNullOrWhiteSpace(rec.OrderNumber) ? "N/A" : rec.OrderNumber.Trim()));
            sb.AppendLine(string.Format("Date:\t\t{0}", rec.ExecutionDate.ToString("dd/MM/yyyy HH:mm:ss")));
            sb.AppendLine();
            sb.AppendLine();

            // Device Specs
            sb.AppendLine("DEVICE SPECS");
            sb.AppendLine(DividerMinor);
            sb.AppendLine(string.Format("Device Name:\t{0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.ComputerName) ? rec.Hardware.ComputerName : Environment.MachineName));
            sb.AppendLine(string.Format("BIOS:\t\t{0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.BiosInfo) && !rec.Hardware.BiosInfo.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) ? rec.Hardware.BiosInfo : "Not detected"));
            sb.AppendLine(string.Format("OS:\t\t{0}", GetFormattedOs(rec.Hardware)));
            sb.AppendLine(string.Format("MB:\t\t{0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.Motherboard) && !rec.Hardware.Motherboard.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) ? rec.Hardware.Motherboard : "Not detected"));
            sb.AppendLine(string.Format("CPU:     \t{0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.CpuName) && !rec.Hardware.CpuName.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) ? rec.Hardware.CpuName : "Not detected"));
            sb.AppendLine(string.Format("RAM:\t\t{0}", GetFormattedRam(rec.Hardware)));
            sb.AppendLine(string.Format("GPU:\t\t{0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.GpuName) && !rec.Hardware.GpuName.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) ? rec.Hardware.GpuName : "Not detected"));
            sb.AppendLine(string.Format("Main Storage:\t{0}", GetFormattedDisk(rec.Hardware)));
            sb.AppendLine(string.Format("Secure Boot:\t{0}", GetFormattedSecureBoot(rec.Hardware)));
            sb.AppendLine(string.Format("TPM:\t\t{0}", GetFormattedTpm(rec.Hardware)));
            sb.AppendLine(string.Format("BitLocker:\t{0}", GetFormattedBitLocker(rec.Hardware)));
            sb.AppendLine(string.Format("Serial Number:\t{0}", GetFormattedSerialNumber(rec)));
            sb.AppendLine();

            // Services and Procedures Performed
            sb.AppendLine("SERVICES AND PROCEDURES PERFORMED");
            sb.AppendLine(DividerMinor);
            if (rec.ActionsDone != null && rec.ActionsDone.Count > 0)
            {
                foreach (string action in rec.ActionsDone)
                {
                    sb.AppendLine(string.Format(" [X] {0}", action));
                }
            }
            else
            {
                sb.AppendLine(" No procedure checked.");
            }
            sb.AppendLine();

            // Observations
            sb.AppendLine("OBSERVATIONS");
            sb.AppendLine(DividerMinor);
            string notes = rec.Notes != null ? rec.Notes.Trim() : "";
            if (!string.IsNullOrEmpty(notes) &&
                !notes.StartsWith("Você pode informar", StringComparison.OrdinalIgnoreCase) &&
                !notes.StartsWith("You can enter", StringComparison.OrdinalIgnoreCase) &&
                !notes.StartsWith("You can provide", StringComparison.OrdinalIgnoreCase) &&
                !notes.StartsWith("Enter technical observations", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(notes);
            }
            else
            {
                sb.AppendLine("Nothing Reported");
            }
            sb.AppendLine();

            // Footer
            sb.AppendLine(DividerMajor);
            sb.AppendLine("\t\tReport generated via OptiAssist");
            sb.AppendLine(DividerMajor);

            return sb.ToString();
        }

        private static string GetFormattedOs(HardwareSummary hw)
        {
            if (hw == null || string.IsNullOrWhiteSpace(hw.OsName)) return "Windows";
            StringBuilder sb = new StringBuilder();
            sb.Append(hw.OsName);
            if (!string.IsNullOrWhiteSpace(hw.OsDisplayVersion))
            {
                sb.Append(" " + hw.OsDisplayVersion);
            }
            if (!string.IsNullOrWhiteSpace(hw.OsBuild))
            {
                sb.Append(string.Format(" (Build {0})", hw.OsBuild));
            }
            if (!string.IsNullOrWhiteSpace(hw.OsArchitecture))
            {
                sb.Append(" " + hw.OsArchitecture);
            }
            return sb.ToString().Trim();
        }

        private static string GetFormattedRam(HardwareSummary hw)
        {
            if (hw == null || hw.RamTotalGb <= 0) return "Not detected";
            if (hw.RamFreeGb > 0)
            {
                return string.Format("{0} GB ({1} GB free)", hw.RamTotalGb, hw.RamFreeGb);
            }
            return string.Format("{0} GB Total", hw.RamTotalGb);
        }

        private static string GetFormattedDisk(HardwareSummary hw)
        {
            try
            {
                DriveInfo c = new DriveInfo("C");
                if (c.IsReady)
                {
                    double totalGb = Math.Round(c.TotalSize / (1024.0 * 1024.0 * 1024.0), 0);
                    double freeGb = Math.Round(c.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 0);
                    return string.Format("C: ({0} GB free of {1} GB - {2})", freeGb, totalGb, c.DriveFormat);
                }
            }
            catch { }

            if (hw != null && !string.IsNullOrWhiteSpace(hw.PrimaryDiskInfo) &&
                hw.PrimaryDiskInfo != "Detectando disco..." &&
                hw.PrimaryDiskInfo != "Detecting storage...")
            {
                return hw.PrimaryDiskInfo;
            }
            return "C: (Information unavailable)";
        }

        private static string GetFormattedSecureBoot(HardwareSummary hw)
        {
            if (hw == null) return "Not detected";
            if (hw.SecureBootActive ||
                string.Equals(hw.SecureBootStatus, "Ativo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.SecureBootStatus, "Enabled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.SecureBootStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return "Enabled";
            }
            if (string.Equals(hw.SecureBootStatus, "Inativo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.SecureBootStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "Disabled";
            }
            if (string.IsNullOrWhiteSpace(hw.SecureBootStatus) || hw.SecureBootStatus.StartsWith("Detect", StringComparison.OrdinalIgnoreCase))
            {
                return "Not detected";
            }
            return "Unavailable / Not supported";
        }

        private static string GetFormattedTpm(HardwareSummary hw)
        {
            if (hw == null) return "Not detected";
            if (hw.TpmActive || (!string.IsNullOrEmpty(hw.TpmStatus) && (hw.TpmStatus.IndexOf("Disponível", StringComparison.OrdinalIgnoreCase) >= 0 || hw.TpmStatus.IndexOf("Available", StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                string status = hw.TpmStatus != null ? hw.TpmStatus : "";
                status = status.Replace("Disponível", "Available");
                return status;
            }
            if (string.Equals(hw.TpmStatus, "Inativo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.TpmStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "Disabled";
            }
            if (string.IsNullOrWhiteSpace(hw.TpmStatus) || hw.TpmStatus.StartsWith("Detect", StringComparison.OrdinalIgnoreCase))
            {
                return "Not detected";
            }
            return "Unavailable / Not supported";
        }

        private static string GetFormattedBitLocker(HardwareSummary hw)
        {
            if (hw == null) return "Not detected";
            if (hw.BitLockerActive ||
                string.Equals(hw.BitLockerStatus, "Ativo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.BitLockerStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.BitLockerStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
            {
                return "Active";
            }
            if (string.Equals(hw.BitLockerStatus, "Inativo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.BitLockerStatus, "Inactive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hw.BitLockerStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "Inactive";
            }
            if (string.IsNullOrWhiteSpace(hw.BitLockerStatus) || hw.BitLockerStatus.StartsWith("Detect", StringComparison.OrdinalIgnoreCase))
            {
                return "Not detected";
            }
            return "Unavailable / Not supported";
        }

        private static string GetFormattedSerialNumber(MaintenanceRecord rec)
        {
            if (rec != null && !string.IsNullOrWhiteSpace(rec.SerialNumber) &&
                !rec.SerialNumber.Equals("Não informado", StringComparison.OrdinalIgnoreCase) &&
                !rec.SerialNumber.Equals("Not informed", StringComparison.OrdinalIgnoreCase) &&
                !rec.SerialNumber.Equals("Not available", StringComparison.OrdinalIgnoreCase) &&
                !rec.SerialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                return rec.SerialNumber.Trim();
            }
            if (rec != null && rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.SerialNumber) &&
                !rec.Hardware.SerialNumber.Equals("Não informado", StringComparison.OrdinalIgnoreCase) &&
                !rec.Hardware.SerialNumber.Equals("Not informed", StringComparison.OrdinalIgnoreCase) &&
                !rec.Hardware.SerialNumber.Equals("Not available", StringComparison.OrdinalIgnoreCase) &&
                !rec.Hardware.SerialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                return rec.Hardware.SerialNumber.Trim();
            }
            return "N/A";
        }

        public static string SaveReportToReportsFolder(MaintenanceRecord rec)
        {
            string reportContent = GenerateReportText(rec);
            string baseReports = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            if (!Directory.Exists(baseReports))
            {
                try { Directory.CreateDirectory(baseReports); } catch { }
            }

            string safeOrder = string.IsNullOrEmpty(rec.OrderNumber) ? "OS" : CleanFileName(rec.OrderNumber);
            string safeFileName = string.Format("Maintenance_{0}_{1}.txt",
                safeOrder,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string fullPath = Path.Combine(baseReports, safeFileName);

            File.WriteAllText(fullPath, reportContent, Encoding.UTF8);
            return fullPath;
        }

        public static string SaveReportCopy(MaintenanceRecord rec, string customPath)
        {
            string reportContent = GenerateReportText(rec);
            string targetPath = customPath;

            if (string.IsNullOrEmpty(targetPath))
            {
                targetPath = @"C:\MaintenanceReport.txt";
            }

            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(targetPath, reportContent, Encoding.UTF8);
                return targetPath;
            }
            catch
            {
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MaintenanceReport.txt");
                File.WriteAllText(desktopPath, reportContent, Encoding.UTF8);
                return desktopPath;
            }
        }

        private static string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "OS";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName.Replace(' ', '_');
        }
    }

    /// <summary>
    /// Self-contained Maintenance Report (Service Order) Add-on Module compiled into MaintenanceReport.dll.
    /// </summary>
    public class MaintenanceReportAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;

        // UI controls held by the module
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private TextBlock _txtOrderLabel;
        private TextBox _txtOrder;
        private TextBlock _txtClientLabel;
        private TextBox _txtClient;
        private TextBlock _txtSerialLabel;
        private TextBox _txtSerial;
        private TextBlock _txtChecklistLabel;
        private CheckBox _chkActSmart;
        private CheckBox _chkActClean;
        private CheckBox _chkActFormat;
        private CheckBox _chkActDrivers;
        private CheckBox _chkActApps;
        private CheckBox _chkActTemp;
        private CheckBox _chkActDism;
        private CheckBox _chkActNet;
        private TextBlock _txtNotesLabel;
        private TextBox _txtNotes;
        private TextBlock _txtReportPathLabel;
        private TextBox _txtReportPath;
        private Button _btnSaveReport;
        private Button _btnCopyReport;
        private Button _btnOpenReportFolder;

        public string Id
        {
            get { return "maintenance-report"; }
        }

        public string Name
        {
            get { return T("Nav_MaintenanceReport", "Maintenance Report"); }
        }

        public string Description
        {
            get { return T("Report_Subtitle", "Generates a report with machine specs, motherboard, serial number, and performed services"); }
        }

        public string Version
        {
            get { return "1.0.0"; }
        }

        public string Author
        {
            get { return "OptiAssist"; }
        }

        public string Icon
        {
            get { return "📋"; }
        }

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
            if (_host != null)
            {
                _host.Log(msg);
            }
        }

        public FrameworkElement CreateView()
        {
            StackPanel root = new StackPanel();
            root.Margin = new Thickness(0);
            root.Loaded += (s, e) => RefreshSerialFromHardware();

            // 1. Header Section
            StackPanel headerPanel = new StackPanel();
            headerPanel.Margin = new Thickness(0, 0, 0, 10);

            _txtTitle = new TextBlock();
            _txtTitle.Text = T("Report_Title", "Maintenance Service Record");
            _txtTitle.FontSize = 15;
            _txtTitle.FontWeight = FontWeights.Bold;
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _txtTitle.TextTrimming = TextTrimming.CharacterEllipsis;

            _txtSubtitle = new TextBlock();
            _txtSubtitle.Text = T("Report_Subtitle", "Generates a report with machine specs, motherboard, serial number, and performed services");
            _txtSubtitle.FontSize = 11;
            _txtSubtitle.Margin = new Thickness(0, 2, 0, 0);
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtSubtitle.TextTrimming = TextTrimming.CharacterEllipsis;

            headerPanel.Children.Add(_txtTitle);
            headerPanel.Children.Add(_txtSubtitle);
            root.Children.Add(headerPanel);

            // 2. Main Report Form Card
            Border formCard = new Border();
            formCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            formCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            formCard.BorderThickness = new Thickness(1);
            formCard.CornerRadius = new CornerRadius(6);
            formCard.Padding = new Thickness(12);
            formCard.Margin = new Thickness(0, 0, 0, 8);
            formCard.HorizontalAlignment = HorizontalAlignment.Stretch;

            StackPanel formStack = new StackPanel();

            // Row 1: Order No., Client Name, Serial Number / Service Tag
            Grid topGrid = new Grid();
            topGrid.Margin = new Thickness(0, 0, 0, 8);
            topGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(160) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(220) });

            StackPanel colOrder = new StackPanel() { Margin = new Thickness(0, 0, 6, 0) };
            _txtOrderLabel = new TextBlock()
            {
                Text = T("Report_Label_Order", "Service Order (SO) Number:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtOrderLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtOrder = new TextBox() { Height = 26, Text = "" };
            colOrder.Children.Add(_txtOrderLabel);
            colOrder.Children.Add(_txtOrder);
            Grid.SetColumn(colOrder, 0);
            topGrid.Children.Add(colOrder);

            StackPanel colClient = new StackPanel() { Margin = new Thickness(0, 0, 6, 0) };
            _txtClientLabel = new TextBlock()
            {
                Text = T("Report_Label_Client", "Client Name:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtClientLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtClient = new TextBox() { Height = 26, Text = "" };
            colClient.Children.Add(_txtClientLabel);
            colClient.Children.Add(_txtClient);
            Grid.SetColumn(colClient, 1);
            topGrid.Children.Add(colClient);

            StackPanel colSerial = new StackPanel();
            _txtSerialLabel = new TextBlock()
            {
                Text = T("Report_Label_Serial", "Serial Number / Service Tag:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtSerialLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtSerial = new TextBox() { Height = 26, Text = "" };
            colSerial.Children.Add(_txtSerialLabel);
            colSerial.Children.Add(_txtSerial);
            Grid.SetColumn(colSerial, 2);
            topGrid.Children.Add(colSerial);

            formStack.Children.Add(topGrid);

            // Row 2: Services Checklist (2-column UniformGrid)
            _txtChecklistLabel = new TextBlock()
            {
                Text = T("Report_Label_Checklist", "Checklist of Performed Procedures:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _txtChecklistLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            formStack.Children.Add(_txtChecklistLabel);

            UniformGrid checkGrid = new UniformGrid() { Columns = 2, Margin = new Thickness(0, 0, 0, 8) };
            _chkActSmart = new CheckBox() { Content = T("Act_Smart", "Disk diagnostic and SMART health check"), IsChecked = false };
            _chkActClean = new CheckBox() { Content = T("Act_Clean", "Preventive internal cleaning & thermal paste replacement"), IsChecked = false };
            _chkActFormat = new CheckBox() { Content = T("Act_Format", "Clean Windows format and reinstallation"), IsChecked = false };
            _chkActDrivers = new CheckBox() { Content = T("Act_Drivers", "Full driver and BIOS update"), IsChecked = false };
            _chkActApps = new CheckBox() { Content = T("Act_Apps", "Essential application installation"), IsChecked = false };
            _chkActTemp = new CheckBox() { Content = T("Act_Temp", "Deep temporary file cleanup"), IsChecked = false };
            _chkActDism = new CheckBox() { Content = T("Act_Dism", "System integrity repair (DISM / SFC)"), IsChecked = false };
            _chkActNet = new CheckBox() { Content = T("Act_Net", "Network and printer setup"), IsChecked = false };

            checkGrid.Children.Add(_chkActSmart);
            checkGrid.Children.Add(_chkActClean);
            checkGrid.Children.Add(_chkActFormat);
            checkGrid.Children.Add(_chkActDrivers);
            checkGrid.Children.Add(_chkActApps);
            checkGrid.Children.Add(_chkActTemp);
            checkGrid.Children.Add(_chkActDism);
            checkGrid.Children.Add(_chkActNet);
            formStack.Children.Add(checkGrid);

            // Row 3: Technical Assessment & Observations
            _txtNotesLabel = new TextBlock()
            {
                Text = T("Report_Label_Notes", "Technical Report & Observations:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtNotesLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            formStack.Children.Add(_txtNotesLabel);

            _txtNotes = new TextBox()
            {
                Height = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = T("Report_Notes_Placeholder", "You can provide further details about the services performed on this machine here..."),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _txtNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            _txtNotes.GotFocus += TxtNotes_GotFocus;
            _txtNotes.LostFocus += TxtNotes_LostFocus;
            formStack.Children.Add(_txtNotes);

            // Row 4: Default Report Copy Path (persisted in Settings)
            _txtReportPathLabel = new TextBlock()
            {
                Text = T("Settings_Label_ReportPath", "Default Report File Path:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            _txtReportPathLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            formStack.Children.Add(_txtReportPathLabel);

            string initialPath = (_host != null && _host.Settings != null && !string.IsNullOrWhiteSpace(_host.Settings.ReportSavePath))
                ? _host.Settings.ReportSavePath
                : @"C:\MaintenanceReport.txt";

            _txtReportPath = new TextBox()
            {
                Height = 26,
                Text = initialPath,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _txtReportPath.LostFocus += (s, e) =>
            {
                if (_host != null && _host.Settings != null && _txtReportPath != null)
                {
                    _host.Settings.ReportSavePath = _txtReportPath.Text.Trim();
                    _host.SaveSettings();
                }
            };
            formStack.Children.Add(_txtReportPath);

            // Row 5: Action Buttons
            StackPanel actionsRow = new StackPanel() { Orientation = Orientation.Horizontal };

            _btnSaveReport = new Button()
            {
                Content = T("Btn_SaveReport", "📄 Generate Maintenance Report"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnSaveReport.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnSaveReport.Click += BtnSaveReport_Click;

            _btnCopyReport = new Button()
            {
                Content = T("Btn_CopyReport", "📋 Copy Technical Report"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnCopyReport.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnCopyReport.Click += BtnCopyReport_Click;

            _btnOpenReportFolder = new Button()
            {
                Content = T("Btn_OpenReportsFolder", "📂 Open Reports Folder")
            };
            _btnOpenReportFolder.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenReportFolder.Click += BtnOpenReportFolder_Click;

            actionsRow.Children.Add(_btnSaveReport);
            actionsRow.Children.Add(_btnCopyReport);
            actionsRow.Children.Add(_btnOpenReportFolder);
            formStack.Children.Add(actionsRow);

            formCard.Child = formStack;
            root.Children.Add(formCard);

            RefreshSerialFromHardware();
            return root;
        }

        private void RefreshSerialFromHardware()
        {
            if (_txtSerial == null || _host == null || _host.Hardware == null) return;

            string cur = _txtSerial.Text != null ? _txtSerial.Text.Trim() : "";
            if (string.IsNullOrEmpty(cur) || cur == "Não informado" || cur == "Not informed" || cur == "Not available")
            {
                if (!string.IsNullOrWhiteSpace(_host.Hardware.SerialNumber))
                {
                    _txtSerial.Text = _host.Hardware.SerialNumber;
                }
            }
        }

        private void TxtNotes_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_txtNotes == null) return;
            if (_txtNotes.Text.StartsWith("Você pode informar", StringComparison.OrdinalIgnoreCase) ||
                _txtNotes.Text.StartsWith("You can enter", StringComparison.OrdinalIgnoreCase) ||
                _txtNotes.Text.StartsWith("You can provide", StringComparison.OrdinalIgnoreCase) ||
                _txtNotes.Text.StartsWith("Enter technical observations", StringComparison.OrdinalIgnoreCase))
            {
                _txtNotes.Text = "";
                _txtNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
            }
        }

        private void TxtNotes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_txtNotes == null) return;
            if (string.IsNullOrWhiteSpace(_txtNotes.Text))
            {
                _txtNotes.Text = T("Report_Notes_Placeholder", "You can provide further details about the services performed on this machine here...");
                _txtNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            }
        }

        private MaintenanceRecord BuildRecordFromUI()
        {
            RefreshSerialFromHardware();

            MaintenanceRecord rec = new MaintenanceRecord();
            AppSettings settings = _host != null ? _host.Settings : null;

            rec.CustomerName = (settings != null && settings.ServiceProviderMode && !string.IsNullOrWhiteSpace(settings.CustomerName))
                ? settings.CustomerName
                : "OptiAssist";
            rec.ContactInfo = (settings != null && settings.ServiceProviderMode && !string.IsNullOrWhiteSpace(settings.ContactInfo))
                ? settings.ContactInfo
                : "";
            rec.OrderNumber = _txtOrder != null ? _txtOrder.Text.Trim() : "";
            rec.ClientName = _txtClient != null ? _txtClient.Text.Trim() : "";
            rec.ClientPhone = "";
            rec.SerialNumber = _txtSerial != null ? _txtSerial.Text.Trim() : "";
            rec.ExecutionDate = DateTime.Now;
            rec.Hardware = _host != null ? _host.Hardware : null;
            rec.Notes = _txtNotes != null ? _txtNotes.Text.Trim() : "";

            rec.ActionsDone = new List<string>();
            if (_chkActSmart != null && _chkActSmart.IsChecked == true) rec.ActionsDone.Add(_chkActSmart.Content.ToString());
            if (_chkActClean != null && _chkActClean.IsChecked == true) rec.ActionsDone.Add(_chkActClean.Content.ToString());
            if (_chkActFormat != null && _chkActFormat.IsChecked == true) rec.ActionsDone.Add(_chkActFormat.Content.ToString());
            if (_chkActDrivers != null && _chkActDrivers.IsChecked == true) rec.ActionsDone.Add(_chkActDrivers.Content.ToString());
            if (_chkActApps != null && _chkActApps.IsChecked == true) rec.ActionsDone.Add(_chkActApps.Content.ToString());
            if (_chkActTemp != null && _chkActTemp.IsChecked == true) rec.ActionsDone.Add(_chkActTemp.Content.ToString());
            if (_chkActDism != null && _chkActDism.IsChecked == true) rec.ActionsDone.Add(_chkActDism.Content.ToString());
            if (_chkActNet != null && _chkActNet.IsChecked == true) rec.ActionsDone.Add(_chkActNet.Content.ToString());

            return rec;
        }

        private void BtnSaveReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();

            // Persist custom report path if modified
            if (_host != null && _host.Settings != null && _txtReportPath != null && !string.IsNullOrWhiteSpace(_txtReportPath.Text))
            {
                _host.Settings.ReportSavePath = _txtReportPath.Text.Trim();
                _host.SaveSettings();
            }

            // 1. Always generate in application Reports folder
            string reportsFilePath = ReportHelper.SaveReportToReportsFolder(rec);
            Log("[REPORT] Record issued in Reports folder: " + reportsFilePath);

            // 2. Ask user whether to save an extra copy on machine
            MessageBoxResult promptCopy = MessageBox.Show(
                T("Msg_ReportPromptCopy", "The maintenance report has been generated in the Reports folder.\n\nDo you want to save a copy of this report on the user's machine?"),
                "OptiAssist",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            string clientCopyPath = null;
            if (promptCopy == MessageBoxResult.Yes)
            {
                string configuredPath = (_txtReportPath != null && !string.IsNullOrWhiteSpace(_txtReportPath.Text))
                    ? _txtReportPath.Text.Trim()
                    : @"C:\MaintenanceReport.txt";

                clientCopyPath = ReportHelper.SaveReportCopy(rec, configuredPath);
                Log("[REPORT] Copy saved on the user's machine: " + clientCopyPath);
            }

            // 3. Display confirmation and option to open
            string msg;
            if (!string.IsNullOrEmpty(clientCopyPath))
            {
                msg = "Maintenance report saved successfully!\n\n" +
                      "• Reports folder (application): " + reportsFilePath + "\n" +
                      "• Copy on machine: " + clientCopyPath + "\n\n" +
                      "Do you want to open the report file now?";
            }
            else
            {
                msg = string.Format(
                    T("Msg_ReportSaved", "Report saved successfully!\n\nFile: {0}\n\nDo you want to open the report file now?"),
                    reportsFilePath);
            }

            MessageBoxResult openRes = MessageBox.Show(msg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (openRes == MessageBoxResult.Yes)
            {
                try
                {
                    string pathToOpen = !string.IsNullOrEmpty(clientCopyPath) ? clientCopyPath : reportsFilePath;
                    Process.Start(pathToOpen);
                }
                catch
                {
                    try { Process.Start(reportsFilePath); } catch { }
                }
            }
        }

        private void BtnCopyReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();
            string text = ReportHelper.GenerateReportText(rec);
            Clipboard.SetText(text);
            MessageBox.Show(
                T("Msg_ReportCopied", "Full maintenance report copied to clipboard!\nYou can paste it into WhatsApp, email, or your management system."),
                "OptiAssist",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Log("[REPORT] Technical report copied to the clipboard.");
        }

        private void BtnOpenReportFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                if (!Directory.Exists(reportsDir)) Directory.CreateDirectory(reportsDir);
                Process.Start("explorer.exe", reportsDir);
            }
            catch { }
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = T("Report_Title", "Maintenance Service Record");
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Report_Subtitle", "Generates a report with machine specs, motherboard, serial number, and performed services");
            if (_txtOrderLabel != null) _txtOrderLabel.Text = T("Report_Label_Order", "Service Order (SO) Number:");
            if (_txtClientLabel != null) _txtClientLabel.Text = T("Report_Label_Client", "Client Name:");
            if (_txtSerialLabel != null) _txtSerialLabel.Text = T("Report_Label_Serial", "Serial Number / Service Tag:");
            if (_txtChecklistLabel != null) _txtChecklistLabel.Text = T("Report_Label_Checklist", "Checklist of Performed Procedures:");
            if (_chkActSmart != null) _chkActSmart.Content = T("Act_Smart", "Disk diagnostic and SMART health check");
            if (_chkActClean != null) _chkActClean.Content = T("Act_Clean", "Preventive internal cleaning & thermal paste replacement");
            if (_chkActFormat != null) _chkActFormat.Content = T("Act_Format", "Clean Windows format and reinstallation");
            if (_chkActDrivers != null) _chkActDrivers.Content = T("Act_Drivers", "Full driver and BIOS update");
            if (_chkActApps != null) _chkActApps.Content = T("Act_Apps", "Essential application installation");
            if (_chkActTemp != null) _chkActTemp.Content = T("Act_Temp", "Deep temporary file cleanup");
            if (_chkActDism != null) _chkActDism.Content = T("Act_Dism", "System integrity repair (DISM / SFC)");
            if (_chkActNet != null) _chkActNet.Content = T("Act_Net", "Network and printer setup");
            if (_txtNotesLabel != null) _txtNotesLabel.Text = T("Report_Label_Notes", "Technical Report & Observations:");
            if (_txtReportPathLabel != null) _txtReportPathLabel.Text = T("Settings_Label_ReportPath", "Default Report File Path:");

            if (_txtNotes != null)
            {
                string cur = _txtNotes.Text != null ? _txtNotes.Text.Trim() : "";
                if (string.IsNullOrEmpty(cur) ||
                    cur.StartsWith("Você pode informar", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("You can enter", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("You can provide", StringComparison.OrdinalIgnoreCase) ||
                    cur.StartsWith("Enter technical observations", StringComparison.OrdinalIgnoreCase))
                {
                    _txtNotes.Text = T("Report_Notes_Placeholder", "You can provide further details about the services performed on this machine here...");
                    _txtNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
                }
            }

            if (_btnSaveReport != null) _btnSaveReport.Content = T("Btn_SaveReport", "📄 Generate Maintenance Report");
            if (_btnCopyReport != null) _btnCopyReport.Content = T("Btn_CopyReport", "📋 Copy Technical Report");
            if (_btnOpenReportFolder != null) _btnOpenReportFolder.Content = T("Btn_OpenReportsFolder", "📂 Open Reports Folder");
        }
    }
}

