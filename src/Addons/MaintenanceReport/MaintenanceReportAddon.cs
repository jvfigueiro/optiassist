using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    public class MaintenanceReportConfig
    {
        public string CompanyName { get; set; }
        public string ContactInfo { get; set; }
        public string DefaultReportFilePath { get; set; }
        public bool SaveCopyOnClientPc { get; set; }
        public List<string> PreselectedProcedures { get; set; }
        public List<string> CustomProcedures { get; set; }

        public MaintenanceReportConfig()
        {
            CompanyName = "OptiAssist";
            ContactInfo = "";
            DefaultReportFilePath = @"C:\MaintenanceReport.txt";
            SaveCopyOnClientPc = false;
            PreselectedProcedures = new List<string>();
            CustomProcedures = new List<string>();
        }

        public static string GetAddonDirectory()
        {
            try
            {
                string loc = typeof(MaintenanceReportAddon).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }
            }
            catch { }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons", "MaintenanceReport");
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(GetAddonDirectory(), "MaintenanceReport.json");
        }

        public static string GetUsbReportsDirectory()
        {
            string dir = Path.Combine(GetAddonDirectory(), "Reports");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public static string GetLocalClientReportsDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "OptiAssist", "Reports");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public static MaintenanceReportConfig LoadConfig()
        {
            MaintenanceReportConfig cfg = new MaintenanceReportConfig();
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    return cfg;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                cfg.CompanyName = ExtractJsonString(json, "CompanyName", "OptiAssist");
                cfg.ContactInfo = ExtractJsonString(json, "ContactInfo", "");
                cfg.DefaultReportFilePath = ExtractJsonString(json, "DefaultReportFilePath", @"C:\MaintenanceReport.txt");
                cfg.SaveCopyOnClientPc = ExtractJsonBool(json, "SaveCopyOnClientPc", false);
                cfg.PreselectedProcedures = ExtractJsonStringList(json, "PreselectedProcedures");
                cfg.CustomProcedures = ExtractJsonStringList(json, "CustomProcedures");
            }
            catch { }
            return cfg;
        }

        public static void SaveConfig(MaintenanceReportConfig config)
        {
            try
            {
                string dir = GetAddonDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string path = GetConfigFilePath();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"CompanyName\": \"{0}\",", EscapeJson(config.CompanyName)));
                sb.AppendLine(string.Format("  \"ContactInfo\": \"{0}\",", EscapeJson(config.ContactInfo)));
                sb.AppendLine(string.Format("  \"DefaultReportFilePath\": \"{0}\",", EscapeJson(config.DefaultReportFilePath)));
                sb.AppendLine(string.Format("  \"SaveCopyOnClientPc\": {0},", config.SaveCopyOnClientPc ? "true" : "false"));

                // PreselectedProcedures array
                sb.Append("  \"PreselectedProcedures\": [");
                if (config.PreselectedProcedures != null && config.PreselectedProcedures.Count > 0)
                {
                    for (int i = 0; i < config.PreselectedProcedures.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(string.Format("\"{0}\"", EscapeJson(config.PreselectedProcedures[i])));
                    }
                }
                sb.AppendLine("],");

                // CustomProcedures array
                sb.Append("  \"CustomProcedures\": [");
                if (config.CustomProcedures != null && config.CustomProcedures.Count > 0)
                {
                    for (int i = 0; i < config.CustomProcedures.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(string.Format("\"{0}\"", EscapeJson(config.CustomProcedures[i])));
                    }
                }
                sb.AppendLine("]");

                sb.AppendLine("}");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string ExtractJsonString(string json, string key, string fallback)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return fallback;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return fallback;
            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0) return fallback;

            int i = firstQuote + 1;
            StringBuilder sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    if (next == '"') { sb.Append('"'); i += 2; continue; }
                    else if (next == '\\') { sb.Append('\\'); i += 2; continue; }
                    else if (next == 'r') { sb.Append('\r'); i += 2; continue; }
                    else if (next == 'n') { sb.Append('\n'); i += 2; continue; }
                    else if (next == 't') { sb.Append('\t'); i += 2; continue; }
                    else { sb.Append(next); i += 2; continue; }
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static bool ExtractJsonBool(string json, string key, bool fallback)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return fallback;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return fallback;
            int comma = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            string val = comma >= 0 ? json.Substring(colon + 1, comma - colon - 1).Trim() : json.Substring(colon + 1).Trim();
            bool res;
            if (bool.TryParse(val, out res)) return res;
            return fallback;
        }

        private static List<string> ExtractJsonStringList(string json, string key)
        {
            List<string> result = new List<string>();
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return result;
            int bracketOpen = json.IndexOf('[', idx + pattern.Length);
            if (bracketOpen < 0) return result;
            int bracketClose = json.IndexOf(']', bracketOpen + 1);
            if (bracketClose < 0) return result;

            string arrayContent = json.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
            int pos = 0;
            while (pos < arrayContent.Length)
            {
                int qStart = arrayContent.IndexOf('"', pos);
                if (qStart < 0) break;
                int i = qStart + 1;
                StringBuilder sb = new StringBuilder();
                while (i < arrayContent.Length)
                {
                    char c = arrayContent[i];
                    if (c == '\\' && i + 1 < arrayContent.Length)
                    {
                        char next = arrayContent[i + 1];
                        if (next == '"') { sb.Append('"'); i += 2; continue; }
                        else if (next == '\\') { sb.Append('\\'); i += 2; continue; }
                        else if (next == 'r') { sb.Append('\r'); i += 2; continue; }
                        else if (next == 'n') { sb.Append('\n'); i += 2; continue; }
                        else if (next == 't') { sb.Append('\t'); i += 2; continue; }
                        else { sb.Append(next); i += 2; continue; }
                    }
                    else if (c == '"')
                    {
                        result.Add(sb.ToString());
                        pos = i + 1;
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                if (i >= arrayContent.Length) break;
            }
            return result;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\r", "\\r")
                      .Replace("\n", "\\n")
                      .Replace("\t", "\\t");
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

            // Windows Debloater 2.0 Summary (auto-imported from Toolbox backups if present)
            string debloatSummary = LoadLatestDebloaterReportSummary();
            if (!string.IsNullOrWhiteSpace(debloatSummary))
            {
                sb.AppendLine(debloatSummary.Trim());
                sb.AppendLine();
            }

            // Footer
            sb.AppendLine(DividerMajor);
            sb.AppendLine("\t\tReport generated via OptiAssist");
            sb.AppendLine(DividerMajor);

            return sb.ToString();
        }

        private static string LoadLatestDebloaterReportSummary()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate0 = Path.Combine(baseDir, @"Config\Debloater\backups\last_debloat_report.txt");
                if (File.Exists(candidate0))
                {
                    return File.ReadAllText(candidate0, Encoding.UTF8);
                }

                string candidate1 = Path.Combine(baseDir, @"Config\Addons\Toolbox\backups\last_debloat_report.txt");
                if (File.Exists(candidate1))
                {
                    return File.ReadAllText(candidate1, Encoding.UTF8);
                }

                string asmDir = Path.GetDirectoryName(typeof(ReportHelper).Assembly.Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    DirectoryInfo parent = Directory.GetParent(asmDir);
                    if (parent != null)
                    {
                        string candidate2 = Path.Combine(parent.FullName, @"Toolbox\backups\last_debloat_report.txt");
                        if (File.Exists(candidate2))
                        {
                            return File.ReadAllText(candidate2, Encoding.UTF8);
                        }
                    }
                }
            }
            catch { }
            return "";
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

        public static string CleanFileName(string fileName)
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
        private enum HistoryTab
        {
            Usb,
            Local
        }

        private IOptiAssistHost _host;
        private MaintenanceReportConfig _config;
        private HistoryTab _currentHistoryTab = HistoryTab.Usb;

        // UI controls held by the module
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnOptions;

        // Options Card Controls
        private Border _borderOptionsCard;
        private TextBlock _txtOptionsTitle;
        private TextBlock _lblOptCompanyName;
        private TextBox _txtOptCompanyName;
        private TextBlock _lblContactInfo;
        private TextBox _txtContactInfo;
        private TextBlock _lblContactInfoHint;
        private TextBlock _txtReportPathLabel;
        private TextBox _txtReportPath;
        private CheckBox _chkOptSaveCopy;
        private TextBlock _lblOptPreselectedTitle;
        private CheckBox _chkOptPrefSmart;
        private CheckBox _chkOptPrefClean;
        private CheckBox _chkOptPrefFormat;
        private CheckBox _chkOptPrefDrivers;
        private CheckBox _chkOptPrefApps;
        private CheckBox _chkOptPrefTemp;
        private CheckBox _chkOptPrefDism;
        private CheckBox _chkOptPrefNet;
        private TextBlock _lblOptCustomProcedures;
        private TextBox _txtOptCustomProcedures;
        private Button _btnSaveOptions;

        // Main Report Form Controls
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
        private StackPanel _pnlCustomChecklist;
        private List<CheckBox> _customCheckboxes;
        private TextBlock _txtNotesLabel;
        private TextBox _txtNotes;
        private Button _btnSaveReport;
        private Button _btnCopyReport;
        private Button _btnViewReports;
        private Button _btnOpenReportFolder;

        // History Card Controls
        private Border _cardReportsHistory;
        private TextBlock _txtHistoryTitle;
        private TextBlock _txtHistorySubtitle;
        private Button _btnTabUsb;
        private Button _btnTabLocal;
        private Button _btnRefreshReports;
        private Button _btnOpenCurrentReportsFolder;
        private StackPanel _pnlReportsList;

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
            get { return "1.2.S7"; }
        }

        public string Author
        {
            get { return "João Figueiró"; }
        }

        public string Icon
        {
            get { return "📋"; }
        }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _config = MaintenanceReportConfig.LoadConfig();
            _customCheckboxes = new List<CheckBox>();
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
            if (_config == null)
            {
                _config = MaintenanceReportConfig.LoadConfig();
            }
            if (_customCheckboxes == null)
            {
                _customCheckboxes = new List<CheckBox>();
            }

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(0);
            root.Loaded += (s, e) =>
            {
                RefreshSerialFromHardware();
                RefreshReportsList();
            };

            // 1. Header Section
            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 10);
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel titlePanel = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            _txtTitle = new TextBlock();
            _txtTitle.Text = string.Format("{0}  {1}", Icon, Name);
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

            titlePanel.Children.Add(_txtTitle);
            titlePanel.Children.Add(_txtSubtitle);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            _btnOptions = new Button();
            _btnOptions.Content = T("Report_Btn_Options", "⚙️ Options");
            _btnOptions.Height = 28;
            _btnOptions.Padding = new Thickness(12, 0, 12, 0);
            _btnOptions.FontSize = 11;
            _btnOptions.FontWeight = FontWeights.SemiBold;
            _btnOptions.Cursor = Cursors.Hand;
            _btnOptions.VerticalAlignment = VerticalAlignment.Center;
            _btnOptions.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOptions.Click += (s, e) =>
            {
                if (_borderOptionsCard != null)
                {
                    _borderOptionsCard.Visibility = (_borderOptionsCard.Visibility == Visibility.Visible)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            };
            Grid.SetColumn(_btnOptions, 1);
            headerGrid.Children.Add(_btnOptions);
            root.Children.Add(headerGrid);

            // 2. Options Card
            _borderOptionsCard = new Border();
            _borderOptionsCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            _borderOptionsCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            _borderOptionsCard.BorderThickness = new Thickness(1);
            _borderOptionsCard.CornerRadius = new CornerRadius(6);
            _borderOptionsCard.Padding = new Thickness(14);
            _borderOptionsCard.Margin = new Thickness(0, 0, 0, 10);
            _borderOptionsCard.Visibility = Visibility.Collapsed;

            StackPanel optionsStack = new StackPanel();

            _txtOptionsTitle = new TextBlock();
            _txtOptionsTitle.Text = T("Report_Card_Options", "⚙️ Options & Customization");
            _txtOptionsTitle.FontWeight = FontWeights.Bold;
            _txtOptionsTitle.FontSize = 12;
            _txtOptionsTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            _txtOptionsTitle.Margin = new Thickness(0, 0, 0, 10);
            optionsStack.Children.Add(_txtOptionsTitle);

            // Company Name
            _lblOptCompanyName = new TextBlock();
            _lblOptCompanyName.Text = T("Report_Opt_CompanyName", "Company / Service Provider Name:");
            _lblOptCompanyName.FontSize = 10;
            _lblOptCompanyName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _lblOptCompanyName.Margin = new Thickness(0, 0, 0, 2);
            optionsStack.Children.Add(_lblOptCompanyName);

            _txtOptCompanyName = new TextBox();
            _txtOptCompanyName.Height = 26;
            _txtOptCompanyName.Text = !string.IsNullOrEmpty(_config.CompanyName) ? _config.CompanyName : "OptiAssist";
            _txtOptCompanyName.Margin = new Thickness(0, 0, 0, 8);
            optionsStack.Children.Add(_txtOptCompanyName);

            // Contact Info
            _lblContactInfo = new TextBlock();
            _lblContactInfo.Text = T("Report_Label_ContactInfo", "Contact Information:");
            _lblContactInfo.FontSize = 10;
            _lblContactInfo.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _lblContactInfo.Margin = new Thickness(0, 0, 0, 2);
            optionsStack.Children.Add(_lblContactInfo);

            _txtContactInfo = new TextBox();
            _txtContactInfo.Height = 26;
            _txtContactInfo.Margin = new Thickness(0, 0, 0, 2);
            _txtContactInfo.Text = _config.ContactInfo ?? "";
            optionsStack.Children.Add(_txtContactInfo);

            _lblContactInfoHint = new TextBlock();
            _lblContactInfoHint.Text = T("Report_Hint_ContactInfo", "💡 Provide email, phone, address, or website to display in report header");
            _lblContactInfoHint.FontSize = 10;
            _lblContactInfoHint.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            _lblContactInfoHint.Margin = new Thickness(0, 0, 0, 10);
            optionsStack.Children.Add(_lblContactInfoHint);

            // Default Report File Path (moved inside Options)
            _txtReportPathLabel = new TextBlock();
            _txtReportPathLabel.Text = T("Settings_Label_ReportPath", "Default Report File Path:");
            _txtReportPathLabel.FontSize = 10;
            _txtReportPathLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtReportPathLabel.Margin = new Thickness(0, 0, 0, 2);
            optionsStack.Children.Add(_txtReportPathLabel);

            _txtReportPath = new TextBox();
            _txtReportPath.Height = 26;
            _txtReportPath.Text = !string.IsNullOrWhiteSpace(_config.DefaultReportFilePath)
                ? _config.DefaultReportFilePath
                : @"C:\MaintenanceReport.txt";
            _txtReportPath.Margin = new Thickness(0, 0, 0, 4);
            optionsStack.Children.Add(_txtReportPath);

            _chkOptSaveCopy = new CheckBox();
            _chkOptSaveCopy.Content = T("Report_Opt_SaveCopyCheck", "Save additional copy at default path when generating report");
            _chkOptSaveCopy.IsChecked = _config.SaveCopyOnClientPc;
            _chkOptSaveCopy.Margin = new Thickness(0, 0, 0, 10);
            optionsStack.Children.Add(_chkOptSaveCopy);

            // Pre-selected default checklist items
            _lblOptPreselectedTitle = new TextBlock();
            _lblOptPreselectedTitle.Text = T("Report_Opt_PreselectedTitle", "Default Pre-selected Procedures:");
            _lblOptPreselectedTitle.FontSize = 10;
            _lblOptPreselectedTitle.FontWeight = FontWeights.SemiBold;
            _lblOptPreselectedTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _lblOptPreselectedTitle.Margin = new Thickness(0, 0, 0, 4);
            optionsStack.Children.Add(_lblOptPreselectedTitle);

            UniformGrid optPrefGrid = new UniformGrid() { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };
            _chkOptPrefSmart = new CheckBox() { Content = T("Act_Smart", "Disk diagnostic and SMART health check"), Tag = "Act_Smart" };
            _chkOptPrefClean = new CheckBox() { Content = T("Act_Clean", "Preventive internal cleaning & thermal paste replacement"), Tag = "Act_Clean" };
            _chkOptPrefFormat = new CheckBox() { Content = T("Act_Format", "Clean Windows format and reinstallation"), Tag = "Act_Format" };
            _chkOptPrefDrivers = new CheckBox() { Content = T("Act_Drivers", "Full driver and BIOS update"), Tag = "Act_Drivers" };
            _chkOptPrefApps = new CheckBox() { Content = T("Act_Apps", "Essential application installation"), Tag = "Act_Apps" };
            _chkOptPrefTemp = new CheckBox() { Content = T("Act_Temp", "Deep temporary file cleanup"), Tag = "Act_Temp" };
            _chkOptPrefDism = new CheckBox() { Content = T("Act_Dism", "System integrity repair (DISM / SFC)"), Tag = "Act_Dism" };
            _chkOptPrefNet = new CheckBox() { Content = T("Act_Net", "Network and printer setup"), Tag = "Act_Net" };

            optPrefGrid.Children.Add(_chkOptPrefSmart);
            optPrefGrid.Children.Add(_chkOptPrefClean);
            optPrefGrid.Children.Add(_chkOptPrefFormat);
            optPrefGrid.Children.Add(_chkOptPrefDrivers);
            optPrefGrid.Children.Add(_chkOptPrefApps);
            optPrefGrid.Children.Add(_chkOptPrefTemp);
            optPrefGrid.Children.Add(_chkOptPrefDism);
            optPrefGrid.Children.Add(_chkOptPrefNet);
            optionsStack.Children.Add(optPrefGrid);

            // Custom procedures definition
            _lblOptCustomProcedures = new TextBlock();
            _lblOptCustomProcedures.Text = T("Report_Opt_CustomProceduresTitle", "Custom Procedures (one per line):");
            _lblOptCustomProcedures.FontSize = 10;
            _lblOptCustomProcedures.FontWeight = FontWeights.SemiBold;
            _lblOptCustomProcedures.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _lblOptCustomProcedures.Margin = new Thickness(0, 0, 0, 2);
            optionsStack.Children.Add(_lblOptCustomProcedures);

            _txtOptCustomProcedures = new TextBox();
            _txtOptCustomProcedures.Height = 55;
            _txtOptCustomProcedures.TextWrapping = TextWrapping.Wrap;
            _txtOptCustomProcedures.AcceptsReturn = true;
            _txtOptCustomProcedures.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _txtOptCustomProcedures.Margin = new Thickness(0, 0, 0, 10);
            optionsStack.Children.Add(_txtOptCustomProcedures);

            // Save Options Button
            _btnSaveOptions = new Button();
            _btnSaveOptions.Content = T("Report_Btn_SaveOptions", "💾 Save Options");
            _btnSaveOptions.Height = 28;
            _btnSaveOptions.Padding = new Thickness(14, 0, 14, 0);
            _btnSaveOptions.HorizontalAlignment = HorizontalAlignment.Left;
            _btnSaveOptions.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnSaveOptions.Click += BtnSaveOptions_Click;
            optionsStack.Children.Add(_btnSaveOptions);

            _borderOptionsCard.Child = optionsStack;
            root.Children.Add(_borderOptionsCard);

            // 3. Main Report Form Card
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

            // Row 2: Services Checklist
            _txtChecklistLabel = new TextBlock()
            {
                Text = T("Report_Label_Checklist", "Checklist of Performed Procedures:"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _txtChecklistLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            formStack.Children.Add(_txtChecklistLabel);

            UniformGrid checkGrid = new UniformGrid() { Columns = 2, Margin = new Thickness(0, 0, 0, 4) };
            _chkActSmart = new CheckBox() { Content = T("Act_Smart", "Disk diagnostic and SMART health check"), Tag = "Act_Smart" };
            _chkActClean = new CheckBox() { Content = T("Act_Clean", "Preventive internal cleaning & thermal paste replacement"), Tag = "Act_Clean" };
            _chkActFormat = new CheckBox() { Content = T("Act_Format", "Clean Windows format and reinstallation"), Tag = "Act_Format" };
            _chkActDrivers = new CheckBox() { Content = T("Act_Drivers", "Full driver and BIOS update"), Tag = "Act_Drivers" };
            _chkActApps = new CheckBox() { Content = T("Act_Apps", "Essential application installation"), Tag = "Act_Apps" };
            _chkActTemp = new CheckBox() { Content = T("Act_Temp", "Deep temporary file cleanup"), Tag = "Act_Temp" };
            _chkActDism = new CheckBox() { Content = T("Act_Dism", "System integrity repair (DISM / SFC)"), Tag = "Act_Dism" };
            _chkActNet = new CheckBox() { Content = T("Act_Net", "Network and printer setup"), Tag = "Act_Net" };

            checkGrid.Children.Add(_chkActSmart);
            checkGrid.Children.Add(_chkActClean);
            checkGrid.Children.Add(_chkActFormat);
            checkGrid.Children.Add(_chkActDrivers);
            checkGrid.Children.Add(_chkActApps);
            checkGrid.Children.Add(_chkActTemp);
            checkGrid.Children.Add(_chkActDism);
            checkGrid.Children.Add(_chkActNet);
            formStack.Children.Add(checkGrid);

            _pnlCustomChecklist = new StackPanel() { Margin = new Thickness(0, 0, 0, 8) };
            formStack.Children.Add(_pnlCustomChecklist);

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
                Margin = new Thickness(0, 0, 0, 10)
            };
            _txtNotes.SetResourceReference(TextBox.ForegroundProperty, "BrushTextSecondary");
            _txtNotes.GotFocus += TxtNotes_GotFocus;
            _txtNotes.LostFocus += TxtNotes_LostFocus;
            formStack.Children.Add(_txtNotes);

            // Row 4: Action Buttons
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

            _btnViewReports = new Button()
            {
                Content = T("Report_Btn_ViewReports", "📑 Generated Reports"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnViewReports.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnViewReports.Click += (s, e) =>
            {
                if (_cardReportsHistory != null)
                {
                    _cardReportsHistory.Visibility = (_cardReportsHistory.Visibility == Visibility.Visible)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    if (_cardReportsHistory.Visibility == Visibility.Visible)
                    {
                        RefreshReportsList();
                    }
                }
            };

            _btnOpenReportFolder = new Button()
            {
                Content = T("Btn_OpenReportsFolder", "📂 Open Reports Folder")
            };
            _btnOpenReportFolder.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenReportFolder.Click += BtnOpenReportFolder_Click;

            actionsRow.Children.Add(_btnSaveReport);
            actionsRow.Children.Add(_btnCopyReport);
            actionsRow.Children.Add(_btnViewReports);
            actionsRow.Children.Add(_btnOpenReportFolder);
            formStack.Children.Add(actionsRow);

            formCard.Child = formStack;
            root.Children.Add(formCard);

            // 4. Reports History Card
            _cardReportsHistory = new Border();
            _cardReportsHistory.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            _cardReportsHistory.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            _cardReportsHistory.BorderThickness = new Thickness(1);
            _cardReportsHistory.CornerRadius = new CornerRadius(6);
            _cardReportsHistory.Padding = new Thickness(12);
            _cardReportsHistory.Margin = new Thickness(0, 0, 0, 10);
            _cardReportsHistory.Visibility = Visibility.Visible;

            StackPanel historyStack = new StackPanel();

            Grid histHeaderGrid = new Grid();
            histHeaderGrid.Margin = new Thickness(0, 0, 0, 8);
            histHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            histHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel histTitleCol = new StackPanel();
            _txtHistoryTitle = new TextBlock();
            _txtHistoryTitle.Text = T("Report_History_Title", "📑 Generated Reports History");
            _txtHistoryTitle.FontWeight = FontWeights.Bold;
            _txtHistoryTitle.FontSize = 12;
            _txtHistoryTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtHistorySubtitle = new TextBlock();
            _txtHistorySubtitle.Text = T("Report_History_Subtitle", "Browse, preview, copy, or open reports generated on this PC or the USB drive");
            _txtHistorySubtitle.FontSize = 10;
            _txtHistorySubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtHistorySubtitle.Margin = new Thickness(0, 2, 0, 0);

            histTitleCol.Children.Add(_txtHistoryTitle);
            histTitleCol.Children.Add(_txtHistorySubtitle);
            Grid.SetColumn(histTitleCol, 0);
            histHeaderGrid.Children.Add(histTitleCol);

            // Tab Switchers & Folder Open
            StackPanel histTabsCol = new StackPanel() { Orientation = Orientation.Horizontal };

            _btnTabUsb = new Button();
            _btnTabUsb.Content = T("Report_Tab_Usb", "🔌 USB / AddOn Reports");
            _btnTabUsb.Height = 26;
            _btnTabUsb.Padding = new Thickness(8, 0, 8, 0);
            _btnTabUsb.FontSize = 11;
            _btnTabUsb.Margin = new Thickness(0, 0, 4, 0);
            _btnTabUsb.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnTabUsb.Click += (s, e) =>
            {
                _currentHistoryTab = HistoryTab.Usb;
                UpdateTabStyles();
                RefreshReportsList();
            };

            _btnTabLocal = new Button();
            _btnTabLocal.Content = T("Report_Tab_Local", "💻 Client PC Reports (AppData)");
            _btnTabLocal.Height = 26;
            _btnTabLocal.Padding = new Thickness(8, 0, 8, 0);
            _btnTabLocal.FontSize = 11;
            _btnTabLocal.Margin = new Thickness(0, 0, 4, 0);
            _btnTabLocal.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnTabLocal.Click += (s, e) =>
            {
                _currentHistoryTab = HistoryTab.Local;
                UpdateTabStyles();
                RefreshReportsList();
            };

            _btnRefreshReports = new Button();
            _btnRefreshReports.Content = "🔄";
            _btnRefreshReports.ToolTip = "Refresh list";
            _btnRefreshReports.Height = 26;
            _btnRefreshReports.Width = 28;
            _btnRefreshReports.Margin = new Thickness(0, 0, 4, 0);
            _btnRefreshReports.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnRefreshReports.Click += (s, e) => RefreshReportsList();

            _btnOpenCurrentReportsFolder = new Button();
            _btnOpenCurrentReportsFolder.Content = "📂";
            _btnOpenCurrentReportsFolder.ToolTip = T("Btn_OpenReportsFolder", "Open folder in File Explorer");
            _btnOpenCurrentReportsFolder.Height = 26;
            _btnOpenCurrentReportsFolder.Width = 28;
            _btnOpenCurrentReportsFolder.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnOpenCurrentReportsFolder.Click += (s, e) => OpenActiveReportsFolder();

            histTabsCol.Children.Add(_btnTabUsb);
            histTabsCol.Children.Add(_btnTabLocal);
            histTabsCol.Children.Add(_btnRefreshReports);
            histTabsCol.Children.Add(_btnOpenCurrentReportsFolder);

            Grid.SetColumn(histTabsCol, 1);
            histHeaderGrid.Children.Add(histTabsCol);

            historyStack.Children.Add(histHeaderGrid);

            // Reports Items List
            ScrollViewer scrollReports = new ScrollViewer();
            scrollReports.MaxHeight = 240;
            scrollReports.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollReports.Margin = new Thickness(0, 4, 0, 0);

            _pnlReportsList = new StackPanel();
            scrollReports.Content = _pnlReportsList;
            historyStack.Children.Add(scrollReports);

            _cardReportsHistory.Child = historyStack;
            root.Children.Add(_cardReportsHistory);

            // Populate Checklists from Config
            PopulateChecklists();

            RefreshSerialFromHardware();
            return root;
        }

        private void UpdateTabStyles()
        {
            if (_btnTabUsb == null || _btnTabLocal == null) return;
            if (_currentHistoryTab == HistoryTab.Usb)
            {
                _btnTabUsb.SetResourceReference(Button.StyleProperty, "BtnAccent");
                _btnTabLocal.SetResourceReference(Button.StyleProperty, "BtnDefault");
            }
            else
            {
                _btnTabLocal.SetResourceReference(Button.StyleProperty, "BtnAccent");
                _btnTabUsb.SetResourceReference(Button.StyleProperty, "BtnDefault");
            }
        }

        private void PopulateChecklists()
        {
            if (_config == null) return;

            // 1. In Options card preselection checkboxes
            if (_chkOptPrefSmart != null) _chkOptPrefSmart.IsChecked = _config.PreselectedProcedures.Contains("Act_Smart");
            if (_chkOptPrefClean != null) _chkOptPrefClean.IsChecked = _config.PreselectedProcedures.Contains("Act_Clean");
            if (_chkOptPrefFormat != null) _chkOptPrefFormat.IsChecked = _config.PreselectedProcedures.Contains("Act_Format");
            if (_chkOptPrefDrivers != null) _chkOptPrefDrivers.IsChecked = _config.PreselectedProcedures.Contains("Act_Drivers");
            if (_chkOptPrefApps != null) _chkOptPrefApps.IsChecked = _config.PreselectedProcedures.Contains("Act_Apps");
            if (_chkOptPrefTemp != null) _chkOptPrefTemp.IsChecked = _config.PreselectedProcedures.Contains("Act_Temp");
            if (_chkOptPrefDism != null) _chkOptPrefDism.IsChecked = _config.PreselectedProcedures.Contains("Act_Dism");
            if (_chkOptPrefNet != null) _chkOptPrefNet.IsChecked = _config.PreselectedProcedures.Contains("Act_Net");

            if (_txtOptCustomProcedures != null && _config.CustomProcedures != null)
            {
                _txtOptCustomProcedures.Text = string.Join(Environment.NewLine, _config.CustomProcedures.ToArray());
            }

            // 2. In Main card standard checklist
            if (_chkActSmart != null) _chkActSmart.IsChecked = _config.PreselectedProcedures.Contains("Act_Smart");
            if (_chkActClean != null) _chkActClean.IsChecked = _config.PreselectedProcedures.Contains("Act_Clean");
            if (_chkActFormat != null) _chkActFormat.IsChecked = _config.PreselectedProcedures.Contains("Act_Format");
            if (_chkActDrivers != null) _chkActDrivers.IsChecked = _config.PreselectedProcedures.Contains("Act_Drivers");
            if (_chkActApps != null) _chkActApps.IsChecked = _config.PreselectedProcedures.Contains("Act_Apps");
            if (_chkActTemp != null) _chkActTemp.IsChecked = _config.PreselectedProcedures.Contains("Act_Temp");
            if (_chkActDism != null) _chkActDism.IsChecked = _config.PreselectedProcedures.Contains("Act_Dism");
            if (_chkActNet != null) _chkActNet.IsChecked = _config.PreselectedProcedures.Contains("Act_Net");

            // 3. Custom procedures dynamic checkboxes on main form
            if (_pnlCustomChecklist != null)
            {
                _pnlCustomChecklist.Children.Clear();
                _customCheckboxes.Clear();

                if (_config.CustomProcedures != null && _config.CustomProcedures.Count > 0)
                {
                    UniformGrid customGrid = new UniformGrid() { Columns = 2 };
                    foreach (string proc in _config.CustomProcedures)
                    {
                        if (string.IsNullOrWhiteSpace(proc)) continue;
                        string cleanProc = proc.Trim();
                        CheckBox chk = new CheckBox();
                        chk.Content = cleanProc;
                        chk.Margin = new Thickness(0, 2, 4, 2);
                        chk.IsChecked = _config.PreselectedProcedures.Contains(cleanProc);
                        _customCheckboxes.Add(chk);
                        customGrid.Children.Add(chk);
                    }
                    _pnlCustomChecklist.Children.Add(customGrid);
                }
            }
        }

        private void BtnSaveOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_config == null) _config = new MaintenanceReportConfig();

            _config.CompanyName = _txtOptCompanyName != null ? _txtOptCompanyName.Text.Trim() : "OptiAssist";
            _config.ContactInfo = _txtContactInfo != null ? _txtContactInfo.Text.Trim() : "";
            _config.DefaultReportFilePath = _txtReportPath != null ? _txtReportPath.Text.Trim() : @"C:\MaintenanceReport.txt";
            _config.SaveCopyOnClientPc = _chkOptSaveCopy != null && _chkOptSaveCopy.IsChecked == true;

            // Collect Preselected standard procedures
            List<string> presel = new List<string>();
            if (_chkOptPrefSmart != null && _chkOptPrefSmart.IsChecked == true) presel.Add("Act_Smart");
            if (_chkOptPrefClean != null && _chkOptPrefClean.IsChecked == true) presel.Add("Act_Clean");
            if (_chkOptPrefFormat != null && _chkOptPrefFormat.IsChecked == true) presel.Add("Act_Format");
            if (_chkOptPrefDrivers != null && _chkOptPrefDrivers.IsChecked == true) presel.Add("Act_Drivers");
            if (_chkOptPrefApps != null && _chkOptPrefApps.IsChecked == true) presel.Add("Act_Apps");
            if (_chkOptPrefTemp != null && _chkOptPrefTemp.IsChecked == true) presel.Add("Act_Temp");
            if (_chkOptPrefDism != null && _chkOptPrefDism.IsChecked == true) presel.Add("Act_Dism");
            if (_chkOptPrefNet != null && _chkOptPrefNet.IsChecked == true) presel.Add("Act_Net");

            // Custom procedures
            List<string> customs = new List<string>();
            if (_txtOptCustomProcedures != null && !string.IsNullOrWhiteSpace(_txtOptCustomProcedures.Text))
            {
                string[] lines = _txtOptCustomProcedures.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (!string.IsNullOrEmpty(t) && !customs.Contains(t))
                    {
                        customs.Add(t);
                    }
                }
            }

            _config.PreselectedProcedures = presel;
            _config.CustomProcedures = customs;

            // Save to MaintenanceReport.json
            MaintenanceReportConfig.SaveConfig(_config);

            // Sync with Host settings
            if (_host != null && _host.Settings != null)
            {
                _host.Settings.CustomerName = _config.CompanyName;
                _host.Settings.ContactInfo = _config.ContactInfo;
                _host.Settings.ReportSavePath = _config.DefaultReportFilePath;
                _host.SaveSettings();
            }

            // Refresh UI checklist
            PopulateChecklists();

            OptiDialog.Show(
                T("Report_Msg_OptionsSaved", "Options successfully saved to MaintenanceReport.json!"),
                "OptiAssist",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Log("[REPORT] Options saved in MaintenanceReport.json");
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

            rec.CustomerName = (_txtOptCompanyName != null && !string.IsNullOrWhiteSpace(_txtOptCompanyName.Text))
                ? _txtOptCompanyName.Text.Trim()
                : (_config != null && !string.IsNullOrWhiteSpace(_config.CompanyName)
                    ? _config.CompanyName
                    : (_host != null && _host.Settings != null && !string.IsNullOrWhiteSpace(_host.Settings.CustomerName)
                        ? _host.Settings.CustomerName
                        : "OptiAssist"));

            rec.ContactInfo = (_txtContactInfo != null && !string.IsNullOrWhiteSpace(_txtContactInfo.Text))
                ? _txtContactInfo.Text.Trim()
                : (_config != null && !string.IsNullOrWhiteSpace(_config.ContactInfo)
                    ? _config.ContactInfo
                    : (_host != null && _host.Settings != null && !string.IsNullOrWhiteSpace(_host.Settings.ContactInfo)
                        ? _host.Settings.ContactInfo
                        : ""));

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

            if (_customCheckboxes != null)
            {
                foreach (CheckBox chk in _customCheckboxes)
                {
                    if (chk.IsChecked == true && chk.Content != null)
                    {
                        rec.ActionsDone.Add(chk.Content.ToString());
                    }
                }
            }

            return rec;
        }

        private void BtnSaveReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();

            // 1. Generate report text
            string reportContent = ReportHelper.GenerateReportText(rec);

            string safeOrder = string.IsNullOrEmpty(rec.OrderNumber) ? "OS" : ReportHelper.CleanFileName(rec.OrderNumber);
            string safeFileName = string.Format("Maintenance_{0}_{1}.txt",
                safeOrder,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            // 2. Save on USB / AddOn Reports folder
            string usbDir = MaintenanceReportConfig.GetUsbReportsDirectory();
            string fullPathUsb = Path.Combine(usbDir, safeFileName);
            try
            {
                File.WriteAllText(fullPathUsb, reportContent, Encoding.UTF8);
                Log("[REPORT] Record issued in USB / AddOn Reports folder: " + fullPathUsb);
            }
            catch (Exception ex)
            {
                Log("[REPORT] Error saving to USB Reports folder: " + ex.Message);
            }

            // 3. Save on Client PC LocalAppData Reports folder
            string localDir = MaintenanceReportConfig.GetLocalClientReportsDirectory();
            string fullPathLocal = Path.Combine(localDir, safeFileName);
            try
            {
                File.WriteAllText(fullPathLocal, reportContent, Encoding.UTF8);
                Log("[REPORT] Record issued in Client PC AppData Reports folder: " + fullPathLocal);
            }
            catch (Exception ex)
            {
                Log("[REPORT] Error saving to Client PC AppData Reports folder: " + ex.Message);
            }

            // 4. Save copy if enabled in options
            string customCopyPath = null;
            if (_config != null && _config.SaveCopyOnClientPc && !string.IsNullOrWhiteSpace(_config.DefaultReportFilePath))
            {
                try
                {
                    customCopyPath = ReportHelper.SaveReportCopy(rec, _config.DefaultReportFilePath);
                    Log("[REPORT] Copy saved at default path: " + customCopyPath);
                }
                catch (Exception ex)
                {
                    Log("[REPORT] Error saving copy at default path: " + ex.Message);
                }
            }

            // Refresh history list immediately
            RefreshReportsList();

            // 5. User prompt and open
            string msg = string.Format(
                "{0}\n\n• USB / AddOn:\n  {1}\n\n• Client PC (AppData):\n  {2}",
                T("Msg_ReportSavedSuccess", "Maintenance report generated successfully!"),
                fullPathUsb,
                fullPathLocal);

            if (!string.IsNullOrEmpty(customCopyPath))
            {
                msg += string.Format("\n\n• Default Path:\n  {0}", customCopyPath);
            }

            msg += string.Format("\n\n{0}", T("Msg_ReportOpenPrompt", "Do you want to open the report file now?"));

            MessageBoxResult openRes = OptiDialog.Show(msg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (openRes == MessageBoxResult.Yes)
            {
                try
                {
                    string toOpen = File.Exists(fullPathUsb) ? fullPathUsb : (File.Exists(fullPathLocal) ? fullPathLocal : customCopyPath);
                    if (!string.IsNullOrEmpty(toOpen))
                    {
                        Process.Start(toOpen);
                    }
                }
                catch { }
            }
        }

        private void BtnCopyReport_Click(object sender, RoutedEventArgs e)
        {
            MaintenanceRecord rec = BuildRecordFromUI();
            string text = ReportHelper.GenerateReportText(rec);
            Clipboard.SetText(text);
            OptiDialog.Show(
                T("Msg_ReportCopied", "Full maintenance report copied to clipboard!\nYou can paste it into WhatsApp, email, or your management system."),
                "OptiAssist",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Log("[REPORT] Technical report copied to the clipboard.");
        }

        private void BtnOpenReportFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenActiveReportsFolder();
        }

        private void OpenActiveReportsFolder()
        {
            try
            {
                string targetDir = (_currentHistoryTab == HistoryTab.Usb)
                    ? MaintenanceReportConfig.GetUsbReportsDirectory()
                    : MaintenanceReportConfig.GetLocalClientReportsDirectory();

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                Process.Start("explorer.exe", targetDir);
            }
            catch { }
        }

        private void RefreshReportsList()
        {
            if (_pnlReportsList == null) return;
            _pnlReportsList.Children.Clear();

            string targetDir = (_currentHistoryTab == HistoryTab.Usb)
                ? MaintenanceReportConfig.GetUsbReportsDirectory()
                : MaintenanceReportConfig.GetLocalClientReportsDirectory();

            if (!Directory.Exists(targetDir))
            {
                TextBlock emptyBlock = new TextBlock();
                emptyBlock.Text = T("Report_NoReports", "No reports found in this folder.");
                emptyBlock.FontSize = 11;
                emptyBlock.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
                emptyBlock.Margin = new Thickness(6, 12, 6, 12);
                _pnlReportsList.Children.Add(emptyBlock);
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(targetDir, "*.txt");
            }
            catch
            {
                files = new string[0];
            }

            if (files == null || files.Length == 0)
            {
                TextBlock emptyBlock = new TextBlock();
                emptyBlock.Text = T("Report_NoReports", "No reports found in this folder.");
                emptyBlock.FontSize = 11;
                emptyBlock.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
                emptyBlock.Margin = new Thickness(6, 12, 6, 12);
                _pnlReportsList.Children.Add(emptyBlock);
                return;
            }

            // Sort files by creation time descending
            Array.Sort(files, delegate(string a, string b)
            {
                try
                {
                    return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                }
                catch
                {
                    return string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
                }
            });

            foreach (string file in files)
            {
                FileInfo fi;
                try { fi = new FileInfo(file); }
                catch { continue; }

                Border rowBorder = new Border();
                rowBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
                rowBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
                rowBorder.BorderThickness = new Thickness(1);
                rowBorder.CornerRadius = new CornerRadius(4);
                rowBorder.Padding = new Thickness(8, 6, 8, 6);
                rowBorder.Margin = new Thickness(0, 0, 0, 4);

                Grid rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                // Left: Name & Metadata
                StackPanel leftCol = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };

                TextBlock txtName = new TextBlock();
                txtName.Text = "📄 " + fi.Name;
                txtName.FontSize = 11;
                txtName.FontWeight = FontWeights.SemiBold;
                txtName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                txtName.TextTrimming = TextTrimming.CharacterEllipsis;

                TextBlock txtMeta = new TextBlock();
                txtMeta.Text = string.Format("{0}  •  {1:0.0} KB", fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"), fi.Length / 1024.0);
                txtMeta.FontSize = 10;
                txtMeta.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                txtMeta.Margin = new Thickness(0, 2, 0, 0);

                leftCol.Children.Add(txtName);
                leftCol.Children.Add(txtMeta);
                Grid.SetColumn(leftCol, 0);
                rowGrid.Children.Add(leftCol);

                // Right: Action Buttons
                StackPanel rightCol = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                string currentFilePath = file;

                // View in Dialog
                Button btnView = new Button();
                btnView.Content = T("Report_Btn_View", "👁️ View");
                btnView.Height = 24;
                btnView.Padding = new Thickness(8, 0, 8, 0);
                btnView.FontSize = 10;
                btnView.Margin = new Thickness(0, 0, 4, 0);
                btnView.SetResourceReference(Button.StyleProperty, "BtnDefault");
                btnView.Click += (s, e) => ShowReportViewer(currentFilePath);

                // Copy to Clipboard
                Button btnCopy = new Button();
                btnCopy.Content = "📋";
                btnCopy.ToolTip = T("Btn_CopyReport", "Copy report text to clipboard");
                btnCopy.Height = 24;
                btnCopy.Width = 26;
                btnCopy.FontSize = 10;
                btnCopy.Margin = new Thickness(0, 0, 4, 0);
                btnCopy.SetResourceReference(Button.StyleProperty, "BtnDefault");
                btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        string txt = File.ReadAllText(currentFilePath, Encoding.UTF8);
                        Clipboard.SetText(txt);
                        OptiDialog.Show(
                            T("Msg_ReportCopied", "Full maintenance report copied to clipboard!"),
                            "OptiAssist",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch { }
                };

                // Delete
                Button btnDelete = new Button();
                btnDelete.Content = T("Report_Btn_Delete", "🗑️ Delete");
                btnDelete.Height = 24;
                btnDelete.Padding = new Thickness(8, 0, 8, 0);
                btnDelete.FontSize = 10;
                btnDelete.SetResourceReference(Button.StyleProperty, "BtnDefault");
                btnDelete.Click += (s, e) =>
                {
                    MessageBoxResult res = OptiDialog.Show(
                        string.Format("Deseja realmente excluir o relatório '{0}'?", Path.GetFileName(currentFilePath)),
                        "OptiAssist",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(currentFilePath);
                            RefreshReportsList();
                        }
                        catch (Exception ex)
                        {
                            OptiDialog.Show("Erro ao excluir arquivo: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };

                rightCol.Children.Add(btnView);
                rightCol.Children.Add(btnCopy);
                rightCol.Children.Add(btnDelete);
                Grid.SetColumn(rightCol, 1);
                rowGrid.Children.Add(rightCol);

                rowBorder.Child = rowGrid;
                _pnlReportsList.Children.Add(rowBorder);
            }
        }

        private void ShowReportViewer(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                string title = string.Format("{0} - {1}", T("Report_Viewer_Title", "Maintenance Report Preview"), Path.GetFileName(filePath));
                OptiDialog.ShowTextViewer(title, text, null, 700, 560);
            }
            catch (Exception ex)
            {
                OptiDialog.Show("Erro ao visualizar relatório: " + ex.Message, "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = string.Format("{0}  {1}", Icon, Name);
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Report_Subtitle", "Generates a report with machine specs, motherboard, serial number, and performed services");
            if (_btnOptions != null) _btnOptions.Content = T("Report_Btn_Options", "⚙️ Options");
            if (_txtOptionsTitle != null) _txtOptionsTitle.Text = T("Report_Card_Options", "⚙️ Options & Customization");
            if (_lblOptCompanyName != null) _lblOptCompanyName.Text = T("Report_Opt_CompanyName", "Company / Service Provider Name:");
            if (_lblContactInfo != null) _lblContactInfo.Text = T("Report_Label_ContactInfo", "Contact Information:");
            if (_lblContactInfoHint != null) _lblContactInfoHint.Text = T("Report_Hint_ContactInfo", "💡 Provide email, phone, address, or website to display in report header");
            if (_txtReportPathLabel != null) _txtReportPathLabel.Text = T("Settings_Label_ReportPath", "Default Report File Path:");
            if (_chkOptSaveCopy != null) _chkOptSaveCopy.Content = T("Report_Opt_SaveCopyCheck", "Save additional copy at default path when generating report");
            if (_lblOptPreselectedTitle != null) _lblOptPreselectedTitle.Text = T("Report_Opt_PreselectedTitle", "Default Pre-selected Procedures:");
            if (_chkOptPrefSmart != null) _chkOptPrefSmart.Content = T("Act_Smart", "Disk diagnostic and SMART health check");
            if (_chkOptPrefClean != null) _chkOptPrefClean.Content = T("Act_Clean", "Preventive internal cleaning & thermal paste replacement");
            if (_chkOptPrefFormat != null) _chkOptPrefFormat.Content = T("Act_Format", "Clean Windows format and reinstallation");
            if (_chkOptPrefDrivers != null) _chkOptPrefDrivers.Content = T("Act_Drivers", "Full driver and BIOS update");
            if (_chkOptPrefApps != null) _chkOptPrefApps.Content = T("Act_Apps", "Essential application installation");
            if (_chkOptPrefTemp != null) _chkOptPrefTemp.Content = T("Act_Temp", "Deep temporary file cleanup");
            if (_chkOptPrefDism != null) _chkOptPrefDism.Content = T("Act_Dism", "System integrity repair (DISM / SFC)");
            if (_chkOptPrefNet != null) _chkOptPrefNet.Content = T("Act_Net", "Network and printer setup");
            if (_lblOptCustomProcedures != null) _lblOptCustomProcedures.Text = T("Report_Opt_CustomProceduresTitle", "Custom Procedures (one per line):");
            if (_btnSaveOptions != null) _btnSaveOptions.Content = T("Report_Btn_SaveOptions", "💾 Save Options");

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
            if (_btnViewReports != null) _btnViewReports.Content = T("Report_Btn_ViewReports", "📑 Generated Reports");
            if (_btnOpenReportFolder != null) _btnOpenReportFolder.Content = T("Btn_OpenReportsFolder", "📂 Open Reports Folder");

            if (_txtHistoryTitle != null) _txtHistoryTitle.Text = T("Report_History_Title", "📑 Generated Reports History");
            if (_txtHistorySubtitle != null) _txtHistorySubtitle.Text = T("Report_History_Subtitle", "Browse, preview, copy, or open reports generated on this PC or the USB drive");
            if (_btnTabUsb != null) _btnTabUsb.Content = T("Report_Tab_Usb", "🔌 USB / AddOn Reports");
            if (_btnTabLocal != null) _btnTabLocal.Content = T("Report_Tab_Local", "💻 Client PC Reports (AppData)");
            if (_btnOpenCurrentReportsFolder != null) _btnOpenCurrentReportsFolder.ToolTip = T("Btn_OpenReportsFolder", "Open folder in File Explorer");

            RefreshReportsList();
        }
    }
}
