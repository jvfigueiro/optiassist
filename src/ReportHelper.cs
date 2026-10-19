using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OptiAssist
{
    public class MaintenanceRecord
    {
        public string StoreName { get; set; }
        public string TechnicianName { get; set; }
        public string Phone { get; set; }
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
            SerialNumber = "Não informado";
            Phone = "N/A";
            OrderNumber = "";
            ClientName = "";
            ClientPhone = "";
            Notes = "";
            StoreName = "OptiAssist";
        }
    }

    public static class ReportHelper
    {
        public static string GenerateReportText(MaintenanceRecord rec)
        {
            StringBuilder sb = new StringBuilder();

            // Header: {StoreName} - {Phone}
            string storeName = !string.IsNullOrWhiteSpace(rec.StoreName) && rec.StoreName != "N/A" 
                ? rec.StoreName.Trim() 
                : "OptiAssist";
            string phone = !string.IsNullOrWhiteSpace(rec.Phone) && rec.Phone != "N/A" 
                ? rec.Phone.Trim() 
                : "";

            if (!string.IsNullOrEmpty(phone))
            {
                sb.AppendLine(string.Format("{0} - {1}", storeName, phone));
            }
            else
            {
                sb.AppendLine(storeName);
            }
            sb.AppendLine("REGISTRO DE MANUTENÇÃO");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine();

            // Service Details
            sb.AppendLine("DADOS DO ATENDIMENTO");
            sb.AppendLine("--------------------------------------------------------------------------------");
            string clientDisplay = !string.IsNullOrWhiteSpace(rec.ClientName) ? rec.ClientName.Trim() : "Não informado";
            if (!string.IsNullOrWhiteSpace(rec.ClientPhone) && clientDisplay != "Não informado")
            {
                clientDisplay += string.Format(" ({0})", rec.ClientPhone.Trim());
            }
            sb.AppendLine(string.Format("Cliente:                {0}", clientDisplay));
            sb.AppendLine(string.Format("Ordem de Serviço (OS):  {0}", string.IsNullOrWhiteSpace(rec.OrderNumber) ? "Não informado" : rec.OrderNumber.Trim()));
            sb.AppendLine(string.Format("Data e Hora:            {0}", rec.ExecutionDate.ToString("dd/MM/yyyy HH:mm:ss")));
            sb.AppendLine();
            sb.AppendLine();

            // Computer Specifications
            sb.AppendLine("ESPECIFICAÇÕES DO COMPUTADOR");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("Nome do Dispositivo:    {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.ComputerName) ? rec.Hardware.ComputerName : Environment.MachineName));
            sb.AppendLine(string.Format("BIOS:                   {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.BiosInfo) ? rec.Hardware.BiosInfo : "Não detectada"));
            sb.AppendLine(string.Format("Sistema Operacional:    {0}", GetFormattedOs(rec.Hardware)));
            sb.AppendLine(string.Format("Placa-Mãe:              {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.Motherboard) ? rec.Hardware.Motherboard : "Não detectada"));
            sb.AppendLine(string.Format("Processador (CPU):      {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.CpuName) ? rec.Hardware.CpuName : "Não detectado"));
            sb.AppendLine(string.Format("Memória RAM:            {0}", GetFormattedRam(rec.Hardware)));
            sb.AppendLine(string.Format("Placa de Vídeo (GPU):   {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.GpuName) ? rec.Hardware.GpuName : "Não detectada"));
            sb.AppendLine(string.Format("Armazenamento Primário: {0}", GetFormattedDisk(rec.Hardware)));
            sb.AppendLine(string.Format("Secure Boot:            {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.SecureBootStatus) ? rec.Hardware.SecureBootStatus : "Indisponível/não suportado"));
            sb.AppendLine(string.Format("TPM:                    {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.TpmStatus) ? rec.Hardware.TpmStatus : "Indisponível/não suportado"));
            sb.AppendLine(string.Format("BitLocker:              {0}", rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.BitLockerStatus) ? rec.Hardware.BitLockerStatus : "Indisponível/não suportado"));
            sb.AppendLine(string.Format("Número de Série / Tag:  {0}", !string.IsNullOrWhiteSpace(rec.SerialNumber) && rec.SerialNumber != "Não informado" ? rec.SerialNumber : (rec.Hardware != null && !string.IsNullOrWhiteSpace(rec.Hardware.SerialNumber) ? rec.Hardware.SerialNumber : "Não informado")));
            sb.AppendLine();

            // Services and Procedures Performed
            sb.AppendLine("SERVIÇOS E PROCEDIMENTOS EXECUTADOS");
            sb.AppendLine("--------------------------------------------------------------------------------");
            if (rec.ActionsDone != null && rec.ActionsDone.Count > 0)
            {
                foreach (string action in rec.ActionsDone)
                {
                    sb.AppendLine(string.Format(" [X] {0}", action));
                }
            }
            else
            {
                sb.AppendLine(" Nenhum procedimento marcado no checklist.");
            }
            sb.AppendLine();

            // Technical Assessment and Observations
            sb.AppendLine("LAUDO TÉCNICO E OBSERVAÇÕES");
            sb.AppendLine("--------------------------------------------------------------------------------");
            string notes = rec.Notes != null ? rec.Notes.Trim() : "";
            if (!string.IsNullOrEmpty(notes) && !notes.StartsWith("Você pode informar", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(notes);
            }
            else
            {
                sb.AppendLine("Não informado");
            }
            sb.AppendLine();

            // Footer
            sb.AppendLine("================================================================================");
            sb.AppendLine("Relatório gerado via OptiAssist");
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        private static string GetFormattedOs(HardwareSummary hw)
        {
            if (hw == null) return "Windows";
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
            if (hw == null) return "Não detectada";
            if (hw.RamFreeGb > 0)
            {
                return string.Format("{0} GB ({1} GB Livres)", hw.RamTotalGb, hw.RamFreeGb);
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
                    return string.Format("C: ({0} GB livres de {1} GB - {2})", freeGb, totalGb, c.DriveFormat);
                }
            }
            catch { }

            if (hw != null && !string.IsNullOrWhiteSpace(hw.PrimaryDiskInfo) && hw.PrimaryDiskInfo != "Detectando disco...")
            {
                return hw.PrimaryDiskInfo;
            }
            return "C: (Informações indisponíveis)";
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
            string safeFileName = string.Format("Manutencao_{0}_{1}.txt",
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
