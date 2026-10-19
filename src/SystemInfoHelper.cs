using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Management;
using Microsoft.Win32;

namespace OptiAssist
{
    public class HardwareSummary
    {
        public string OsName { get; set; }
        public string OsDisplayVersion { get; set; }
        public string OsBuild { get; set; }
        public string OsArchitecture { get; set; }
        public string ComputerName { get; set; }
        public string CpuName { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public double RamTotalGb { get; set; }
        public double RamFreeGb { get; set; }
        public string GpuName { get; set; }
        public string PrimaryDiskInfo { get; set; }
        public string SerialNumber { get; set; }
        public string Motherboard { get; set; }
        public string BiosInfo { get; set; }
        public string TpmStatus { get; set; }
        public bool TpmActive { get; set; }
        public string SecureBootStatus { get; set; }
        public bool SecureBootActive { get; set; }
        public string BitLockerStatus { get; set; }
        public bool BitLockerActive { get; set; }

        public HardwareSummary()
        {
            OsName = "Windows";
            OsDisplayVersion = "";
            OsBuild = "";
            OsArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            ComputerName = Environment.MachineName;
            CpuName = "Detectando processador...";
            GpuName = "Detectando placa de vídeo...";
            PrimaryDiskInfo = "Detectando disco...";
            SerialNumber = "Não informado";
            Motherboard = "Detectando placa-mãe...";
            BiosInfo = "Detectando BIOS...";
            TpmStatus = "Detectando TPM...";
            SecureBootStatus = "Detectando Secure Boot...";
            BitLockerStatus = "Detectando BitLocker...";
        }
    }

    public class WinSatScores
    {
        public bool HasData { get; set; }
        public double OverallScore { get; set; }
        public double CpuScore { get; set; }
        public double MemoryScore { get; set; }
        public double DiskScore { get; set; }
        public double D3DScore { get; set; }
        public double GraphicsScore { get; set; }

        public WinSatScores()
        {
            HasData = false;
        }
    }

    public static class SystemInfoHelper
    {
        public static HardwareSummary GetSystemInfo()
        {
            HardwareSummary summary = new HardwareSummary();

            // 1. Informações do Sistema Operacional
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["Caption"] != null) summary.OsName = mo["Caption"].ToString().Trim();
                        if (mo["Version"] != null) summary.OsBuild = mo["Version"].ToString().Trim();
                        if (mo["OSArchitecture"] != null) summary.OsArchitecture = mo["OSArchitecture"].ToString().Trim();
                        if (mo["FreePhysicalMemory"] != null)
                        {
                            ulong freeKb = Convert.ToUInt64(mo["FreePhysicalMemory"]);
                            summary.RamFreeGb = Math.Round(freeKb / (1024.0 * 1024.0), 1);
                        }
                        break;
                    }
                }
            }
            catch { }

            // Buscar DisplayVersion e UBR pelo Registro do Windows
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (rk != null)
                    {
                        object dv = rk.GetValue("DisplayVersion") ?? rk.GetValue("ReleaseId");
                        if (dv != null) summary.OsDisplayVersion = dv.ToString();

                        object ubr = rk.GetValue("UBR");
                        if (ubr != null && !string.IsNullOrEmpty(summary.OsBuild))
                        {
                            summary.OsBuild += "." + ubr.ToString();
                        }
                    }
                }
            }
            catch { }

            // 2. Número de Série e BIOS (Win32_BIOS)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber, Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["SerialNumber"] != null)
                        {
                            string sn = mo["SerialNumber"].ToString().Trim();
                            if (!string.IsNullOrEmpty(sn) &&
                                !sn.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) &&
                                !sn.Equals("Default string", StringComparison.OrdinalIgnoreCase) &&
                                !sn.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase))
                            {
                                summary.SerialNumber = sn;
                            }
                        }

                        string biosVendor = mo["Manufacturer"] != null ? mo["Manufacturer"].ToString().Trim() : "";
                        string biosVer = mo["SMBIOSBIOSVersion"] != null ? mo["SMBIOSBIOSVersion"].ToString().Trim() : "";
                        string biosDate = "";
                        if (mo["ReleaseDate"] != null)
                        {
                            string raw = mo["ReleaseDate"].ToString().Trim();
                            if (raw.Length >= 8)
                            {
                                string y = raw.Substring(0, 4);
                                string m = raw.Substring(4, 2);
                                string d = raw.Substring(6, 2);
                                biosDate = d + "/" + m + "/" + y;
                            }
                            else
                            {
                                biosDate = raw;
                            }
                        }

                        summary.BiosInfo = string.Format("{0} v{1} ({2})", biosVendor, biosVer, biosDate).Trim();
                        break;
                    }
                }
            }
            catch { }

            // Fallback para Número de Série via Win32_ComputerSystemProduct
            if (string.IsNullOrEmpty(summary.SerialNumber) || summary.SerialNumber == "Não informado")
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            if (mo["IdentifyingNumber"] != null)
                            {
                                string id = mo["IdentifyingNumber"].ToString().Trim();
                                if (!string.IsNullOrEmpty(id) &&
                                    !id.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) &&
                                    !id.Equals("Default string", StringComparison.OrdinalIgnoreCase))
                                {
                                    summary.SerialNumber = id;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Placa-Mãe (Win32_BaseBoard)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string mbVendor = mo["Manufacturer"] != null ? mo["Manufacturer"].ToString().Trim() : "";
                        string mbProduct = mo["Product"] != null ? mo["Product"].ToString().Trim() : "";
                        summary.Motherboard = string.Format("{0} {1}", mbVendor, mbProduct).Trim();
                        break;
                    }
                }
            }
            catch
            {
                summary.Motherboard = "Placa-Mãe não identificada";
            }

            // 4. Processador (CPU)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["Name"] != null) summary.CpuName = CleanCpuName(mo["Name"].ToString());
                        if (mo["NumberOfCores"] != null) summary.CpuCores = Convert.ToInt32(mo["NumberOfCores"]);
                        if (mo["NumberOfLogicalProcessors"] != null) summary.CpuThreads = Convert.ToInt32(mo["NumberOfLogicalProcessors"]);
                        break;
                    }
                }
            }
            catch
            {
                summary.CpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Processador Desconhecido";
            }

            // 5. Memória RAM
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["TotalPhysicalMemory"] != null)
                        {
                            ulong totalBytes = Convert.ToUInt64(mo["TotalPhysicalMemory"]);
                            summary.RamTotalGb = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 1);
                        }
                        break;
                    }
                }
            }
            catch { }

            // 6. Placa de Vídeo (GPU)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    string primaryGpu = "";
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = mo["Name"] != null ? mo["Name"].ToString().Trim() : "";
                        if (string.IsNullOrEmpty(name)) continue;

                        if (name.IndexOf("Remote", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (string.IsNullOrEmpty(primaryGpu)) primaryGpu = name;
                            continue;
                        }

                        primaryGpu = name;
                        break;
                    }
                    if (!string.IsNullOrEmpty(primaryGpu)) summary.GpuName = primaryGpu;
                    else summary.GpuName = "Adaptador de vídeo genérico";
                }
            }
            catch
            {
                summary.GpuName = "Não foi possível detectar GPU";
            }

            // 7. Disco Primário (C:)
            try
            {
                DriveInfo cDrive = new DriveInfo("C");
                if (cDrive.IsReady)
                {
                    double totalGb = Math.Round(cDrive.TotalSize / (1024.0 * 1024.0 * 1024.0), 0);
                    double freeGb = Math.Round(cDrive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 0);
                    summary.PrimaryDiskInfo = string.Format("C: ({0} GB livres de {1} GB - {2})", freeGb, totalGb, cDrive.DriveFormat);
                }
            }
            catch
            {
                summary.PrimaryDiskInfo = "Unidade C: (Informações indisponíveis)";
            }

            // 8. Secure Boot
            // 8. Secure Boot
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                {
                    if (rk != null)
                    {
                        object val = rk.GetValue("UEFISecureBootEnabled");
                        if (val != null)
                        {
                            int sb = Convert.ToInt32(val);
                            if (sb == 1)
                            {
                                summary.SecureBootStatus = "Ativo";
                                summary.SecureBootActive = true;
                            }
                            else
                            {
                                summary.SecureBootStatus = "Inativo";
                                summary.SecureBootActive = false;
                            }
                        }
                        else
                        {
                            summary.SecureBootStatus = "Indisponível/não suportado";
                            summary.SecureBootActive = false;
                        }
                    }
                    else
                    {
                        summary.SecureBootStatus = "Indisponível/não suportado";
                        summary.SecureBootActive = false;
                    }
                }
            }
            catch
            {
                summary.SecureBootStatus = "Indisponível/não suportado";
                summary.SecureBootActive = false;
            }

            // 9. TPM
            try
            {
                bool tpmFound = false;
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2\Security\MicrosoftTpm");
                scope.Connect();
                ObjectQuery query = new ObjectQuery("SELECT IsActivated_InitialValue, IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string spec = mo["SpecVersion"] != null ? mo["SpecVersion"].ToString() : "2.0";
                        if (spec.Contains(",")) spec = spec.Split(',')[0].Trim();
                        bool activated = mo["IsActivated_InitialValue"] != null && Convert.ToBoolean(mo["IsActivated_InitialValue"]);
                        bool enabled = mo["IsEnabled_InitialValue"] != null && Convert.ToBoolean(mo["IsEnabled_InitialValue"]);
                        if (activated || enabled)
                        {
                            summary.TpmStatus = string.Format("Disponível ({0})", spec);
                            summary.TpmActive = true;
                        }
                        else
                        {
                            summary.TpmStatus = "Inativo";
                            summary.TpmActive = false;
                        }
                        tpmFound = true;
                        break;
                    }
                }

                if (!tpmFound)
                {
                    summary.TpmStatus = "Indisponível/não suportado";
                    summary.TpmActive = false;
                }
            }
            catch
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'SecurityDevices'"))
                    {
                        bool found = false;
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            string name = mo["Name"] != null ? mo["Name"].ToString() : "";
                            if (name.IndexOf("TPM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Trusted Platform", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string ver = name.Contains("2.0") ? "2.0" : (name.Contains("1.2") ? "1.2" : "2.0");
                                summary.TpmStatus = string.Format("Disponível ({0})", ver);
                                summary.TpmActive = true;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            summary.TpmStatus = "Indisponível/não suportado";
                            summary.TpmActive = false;
                        }
                    }
                }
                catch
                {
                    summary.TpmStatus = "Indisponível/não suportado";
                    summary.TpmActive = false;
                }
            }

            // 10. BitLocker
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2\Security\MicrosoftVolumeEncryption");
                scope.Connect();
                ObjectQuery query = new ObjectQuery("SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                {
                    bool found = false;
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        int prot = mo["ProtectionStatus"] != null ? Convert.ToInt32(mo["ProtectionStatus"]) : 0;
                        if (prot == 1)
                        {
                            summary.BitLockerStatus = "Ativo";
                            summary.BitLockerActive = true;
                        }
                        else
                        {
                            summary.BitLockerStatus = "Inativo";
                            summary.BitLockerActive = false;
                        }
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        summary.BitLockerStatus = "Inativo";
                        summary.BitLockerActive = false;
                    }
                }
            }
            catch
            {
                summary.BitLockerStatus = "Indisponível/não suportado";
                summary.BitLockerActive = false;
            }

            return summary;
        }

        public static WinSatScores GetWinSatScores()
        {
            WinSatScores scores = new WinSatScores();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CPUScore, D3DScore, DiskScore, GraphicsScore, MemoryScore, WinSPRLevel FROM Win32_WinSAT"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["WinSPRLevel"] != null)
                        {
                            scores.OverallScore = Convert.ToDouble(mo["WinSPRLevel"]);
                            scores.CpuScore = mo["CPUScore"] != null ? Convert.ToDouble(mo["CPUScore"]) : 0;
                            scores.MemoryScore = mo["MemoryScore"] != null ? Convert.ToDouble(mo["MemoryScore"]) : 0;
                            scores.DiskScore = mo["DiskScore"] != null ? Convert.ToDouble(mo["DiskScore"]) : 0;
                            scores.D3DScore = mo["D3DScore"] != null ? Convert.ToDouble(mo["D3DScore"]) : 0;
                            scores.GraphicsScore = mo["GraphicsScore"] != null ? Convert.ToDouble(mo["GraphicsScore"]) : 0;
                            scores.HasData = scores.OverallScore > 0;
                            break;
                        }
                    }
                }
            }
            catch { }

            return scores;
        }

        public static void RunWinSatBenchmark(Action<string> log)
        {
            log("=================================================");
            log("      INICIANDO BENCHMARK DO SISTEMA (WINSAT)    ");
            log("=================================================");
            log("Executando: winsat.exe formal -restart clean");
            log("Aguarde alguns minutos enquanto o Windows avalia CPU, Memória, GPU e Disco...");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "winsat.exe";
                psi.Arguments = "formal -restart clean";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("  " + e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("  [ERRO] " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }

                log("\n=== BENCHMARK WINSAT CONCLUÍDO COM SUCESSO! ===");
            }
            catch (Exception ex)
            {
                log("[ERRO WINSAT] Falha ao rodar benchmark: " + ex.Message);
            }
        }

        private static string CleanCpuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Replace("  ", " ").Trim();
        }

        public static bool RebootToBios(out string errorMessage)
        {
            errorMessage = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "shutdown.exe";
                psi.Arguments = "/r /fw /t 0";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;

                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit(4000);
                    if (proc.ExitCode != 0)
                    {
                        string err = proc.StandardError.ReadToEnd();
                        errorMessage = string.IsNullOrEmpty(err)
                            ? "A placa-mãe ou BIOS não oferece suporte a reinicialização direta para UEFI via software.\nReinicie manualmente e pressione as teclas DEL ou F2 na inicialização."
                            : err.Trim();
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
