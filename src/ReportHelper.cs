using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OptiAssist
{
    public class MaintenanceRecord
    {
        public string CustomerName { get; set; }
        public string ContactInfo { get; set; }

        // Backward compatibility properties
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
                !notes.StartsWith("You can provide", StringComparison.OrdinalIgnoreCase))
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
                string status = hw.TpmStatus ?? "";
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
                !rec.SerialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                return rec.SerialNumber.Trim();
            }
            if (rec != null && rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.SerialNumber) &&
                !rec.Hardware.SerialNumber.Equals("Não informado", StringComparison.OrdinalIgnoreCase) &&
                !rec.Hardware.SerialNumber.Equals("Not informed", StringComparison.OrdinalIgnoreCase) &&
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

        public static string SaveReportToFile(MaintenanceRecord rec, string customPath)
        {
            SaveReportToReportsFolder(rec);
            return SaveReportCopy(rec, customPath);
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
}
