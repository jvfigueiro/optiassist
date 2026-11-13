using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Management;
using Microsoft.Win32;

namespace OptiAssist
{
    public enum RecommendationLevel
    {
        Info,
        Warning,
        Critical
    }

    public class HardwareRecommendation
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ActionButtonText { get; set; }
        public string ActionTag { get; set; }
        public string Category { get; set; }
        public RecommendationLevel Level { get; set; }
        public string Icon { get; set; }
    }

    public class ProblemDeviceEntry
    {
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public string HardwareId { get; set; }
        public int ErrorCode { get; set; }
        public string StatusDescription { get; set; }
    }

    public class HardwareSummary
    {
        public string OsName { get; set; }
        public string OsDisplayVersion { get; set; }
        public string OsBuild { get; set; }
        public string OsArchitecture { get; set; }
        public string OsInstallDate { get; set; }
        public bool OsIsActivated { get; set; }
        public string ComputerName { get; set; }
        public string CpuName { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public double RamTotalGb { get; set; }
        public double RamFreeGb { get; set; }
        public string GpuName { get; set; }
        public string PrimaryDiskInfo { get; set; }
        public System.Collections.Generic.List<string> AllDrivesInfo { get; set; }
        public double PrimaryDiskTotalGb { get; set; }
        public double PrimaryDiskFreeGb { get; set; }
        public string PrimaryDiskMediaType { get; set; }
        public int RamSlotsCount { get; set; }
        public string SerialNumber { get; set; }
        public string Motherboard { get; set; }
        public string BiosInfo { get; set; }
        public string TpmStatus { get; set; }
        public bool TpmActive { get; set; }
        public string SecureBootStatus { get; set; }
        public bool SecureBootActive { get; set; }
        public string BitLockerStatus { get; set; }
        public bool BitLockerActive { get; set; }
        public string FirmwareMode { get; set; }
        public bool IsUefi { get; set; }
        public System.Collections.Generic.List<ProblemDeviceEntry> ProblemDevices { get; set; }
        public System.Collections.Generic.List<HardwareRecommendation> Recommendations { get; set; }

        public HardwareSummary()
        {
            OsName = "Windows";
            OsDisplayVersion = "";
            OsBuild = "";
            OsArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            OsInstallDate = "";
            OsIsActivated = false;
            ComputerName = Environment.MachineName;
            CpuName = "Detecting CPU...";
            GpuName = "Detecting GPU...";
            PrimaryDiskInfo = "Detecting storage...";
            AllDrivesInfo = new System.Collections.Generic.List<string>();
            PrimaryDiskTotalGb = 0;
            PrimaryDiskFreeGb = 0;
            PrimaryDiskMediaType = "";
            RamSlotsCount = 0;
            SerialNumber = "Not informed";
            Motherboard = "Detecting motherboard...";
            BiosInfo = "Detecting BIOS...";
            TpmStatus = "Detecting TPM...";
            SecureBootStatus = "Detecting Secure Boot...";
            BitLockerStatus = "Detecting BitLocker...";
            FirmwareMode = "Detecting...";
            ProblemDevices = new System.Collections.Generic.List<ProblemDeviceEntry>();
            Recommendations = new System.Collections.Generic.List<HardwareRecommendation>();
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

            // 1. Operating System Information
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture, FreePhysicalMemory, InstallDate FROM Win32_OperatingSystem"))
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
                        if (mo["InstallDate"] != null)
                        {
                            try
                            {
                                DateTime dt = ManagementDateTimeConverter.ToDateTime(mo["InstallDate"].ToString());
                                summary.OsInstallDate = dt.ToString("dd/MM/yyyy");
                            }
                            catch { }
                        }
                        break;
                    }
                }
            }
            catch { }

            // Query DisplayVersion, UBR, and fallback InstallDate from Windows Registry
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

                        if (string.IsNullOrEmpty(summary.OsInstallDate))
                        {
                            object instDateObj = rk.GetValue("InstallDate");
                            if (instDateObj != null)
                            {
                                long unixSec = Convert.ToInt64(instDateObj);
                                if (unixSec > 0)
                                {
                                    DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSec).ToLocalTime();
                                    summary.OsInstallDate = dt.ToString("dd/MM/yyyy");
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Query Windows Activation Status (SoftwareLicensingProduct)
            try
            {
                using (ManagementObjectSearcher licSearcher = new ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f' AND LicenseStatus = 1"))
                {
                    foreach (ManagementObject mo in licSearcher.Get())
                    {
                        if (mo["LicenseStatus"] != null && Convert.ToInt32(mo["LicenseStatus"]) == 1)
                        {
                            summary.OsIsActivated = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            // 2. Serial Number and BIOS (Win32_BIOS)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber, Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["SerialNumber"] != null)
                        {
                            string sn = SanitizeSmbiosValue(mo["SerialNumber"].ToString());
                            if (!string.IsNullOrEmpty(sn) && sn != LocalizationHelper.Get("Status_NotAvailable", "Not available"))
                            {
                                summary.SerialNumber = sn;
                            }
                        }

                        string biosVendor = mo["Manufacturer"] != null ? SanitizeSmbiosValue(mo["Manufacturer"].ToString()) : "";
                        if (biosVendor == LocalizationHelper.Get("Status_NotAvailable", "Not available")) biosVendor = "";
                        string biosVer = mo["SMBIOSBIOSVersion"] != null ? SanitizeSmbiosValue(mo["SMBIOSBIOSVersion"].ToString()) : "";
                        if (biosVer == LocalizationHelper.Get("Status_NotAvailable", "Not available")) biosVer = "";
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

                        string formattedBios = string.Format("{0} {1} {2}", biosVendor, string.IsNullOrEmpty(biosVer) ? "" : "v" + biosVer, string.IsNullOrEmpty(biosDate) ? "" : "(" + biosDate + ")").Trim();
                        summary.BiosInfo = SanitizeSmbiosValue(formattedBios);
                        break;
                    }
                }
            }
            catch { }

            // Fallback for Serial Number via Win32_ComputerSystemProduct
            if (string.IsNullOrEmpty(summary.SerialNumber) || summary.SerialNumber == "Not informed" || summary.SerialNumber == "Não informado" || summary.SerialNumber == LocalizationHelper.Get("Status_NotAvailable", "Not available"))
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            if (mo["IdentifyingNumber"] != null)
                            {
                                string id = SanitizeSmbiosValue(mo["IdentifyingNumber"].ToString());
                                if (!string.IsNullOrEmpty(id) && id != LocalizationHelper.Get("Status_NotAvailable", "Not available"))
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

            // 3. Motherboard (Win32_BaseBoard)
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string mbVendor = mo["Manufacturer"] != null ? SanitizeSmbiosValue(mo["Manufacturer"].ToString()) : "";
                        if (mbVendor == LocalizationHelper.Get("Status_NotAvailable", "Not available")) mbVendor = "";
                        string mbProduct = mo["Product"] != null ? SanitizeSmbiosValue(mo["Product"].ToString()) : "";
                        if (mbProduct == LocalizationHelper.Get("Status_NotAvailable", "Not available")) mbProduct = "";
                        string combinedMb = string.Format("{0} {1}", mbVendor, mbProduct).Trim();
                        summary.Motherboard = SanitizeSmbiosValue(combinedMb);
                        break;
                    }
                }
            }
            catch
            {
                summary.Motherboard = LocalizationHelper.Get("Status_NotAvailable", "Not available");
            }

            // 4. Processor (CPU)
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
                summary.CpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown Processor";
            }

            // 5. RAM Memory (Physical sum first, then usable fallback with friendly rounding)
            try
            {
                ulong physicalBytesSum = 0;
                try
                {
                    using (ManagementObjectSearcher physSearcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                    {
                        foreach (ManagementObject mo in physSearcher.Get())
                        {
                            if (mo["Capacity"] != null)
                            {
                                physicalBytesSum += Convert.ToUInt64(mo["Capacity"]);
                                summary.RamSlotsCount++;
                            }
                        }
                    }
                }
                catch { }

                if (physicalBytesSum > 0)
                {
                    double rawGb = physicalBytesSum / (1024.0 * 1024.0 * 1024.0);
                    summary.RamTotalGb = NormalizeRamGb(rawGb);
                }
                else
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            if (mo["TotalPhysicalMemory"] != null)
                            {
                                ulong totalBytes = Convert.ToUInt64(mo["TotalPhysicalMemory"]);
                                double rawGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                                summary.RamTotalGb = NormalizeRamGb(rawGb);
                            }
                            break;
                        }
                    }
                }
            }
            catch { }

            // 6. Graphics Card (GPU)
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
                    else summary.GpuName = "Generic Video Adapter";
                }
            }
            catch
            {
                summary.GpuName = "Unable to detect GPU";
            }

            // 7. Primary Storage (C:)
            try
            {
                DriveInfo cDrive = new DriveInfo("C");
                if (cDrive.IsReady)
                {
                    summary.PrimaryDiskTotalGb = Math.Round(cDrive.TotalSize / (1024.0 * 1024.0 * 1024.0), 0);
                    summary.PrimaryDiskFreeGb = Math.Round(cDrive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 0);
                    summary.PrimaryDiskInfo = string.Format("C: ({0} GB / {1} GB - {2})", summary.PrimaryDiskFreeGb, summary.PrimaryDiskTotalGb, cDrive.DriveFormat);
                }

                try
                {
                    foreach (DriveInfo drv in DriveInfo.GetDrives())
                    {
                        if ((drv.DriveType == DriveType.Fixed || drv.DriveType == DriveType.Removable) && drv.IsReady && drv.TotalSize > 0)
                        {
                            double totGb = Math.Round(drv.TotalSize / (1024.0 * 1024.0 * 1024.0), 0);
                            double freeGb = Math.Round(drv.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 0);
                            string label = drv.Name.TrimEnd('\\');
                            string vol = string.IsNullOrWhiteSpace(drv.VolumeLabel) ? "" : " [" + drv.VolumeLabel.Trim() + "]";
                            summary.AllDrivesInfo.Add(string.Format("{0}{1} ({2} GB / {3} GB - {4})", label, vol, freeGb, totGb, drv.DriveFormat));
                        }
                    }
                }
                catch { }

                try
                {
                    int cDiskIndex = 0;
                    try
                    {
                        using (ManagementObjectSearcher partSearcher = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='C:'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                        {
                            foreach (ManagementObject part in partSearcher.Get())
                            {
                                if (part["DiskIndex"] != null)
                                {
                                    cDiskIndex = Convert.ToInt32(part["DiskIndex"]);
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    // 1. Check MSFT_PhysicalDisk (Windows Storage Management API) for accurate MediaType / BusType / SpindleSpeed
                    try
                    {
                        using (ManagementObjectSearcher phySearcher = new ManagementObjectSearcher(
                            @"root\Microsoft\Windows\Storage",
                            "SELECT DeviceId, FriendlyName, Model, MediaType, BusType, SpindleSpeed FROM MSFT_PhysicalDisk"))
                        {
                            foreach (ManagementObject phy in phySearcher.Get())
                            {
                                string devId = phy["DeviceId"] != null ? phy["DeviceId"].ToString().Trim() : "";
                                int diskId = -1;
                                int.TryParse(devId, out diskId);
                                if (diskId >= 0 && diskId != cDiskIndex && !string.IsNullOrEmpty(summary.PrimaryDiskMediaType))
                                    continue;

                                int mediaType = phy["MediaType"] != null ? Convert.ToInt32(phy["MediaType"]) : 0;
                                int busType = phy["BusType"] != null ? Convert.ToInt32(phy["BusType"]) : 0;
                                uint spindle = phy["SpindleSpeed"] != null ? Convert.ToUInt32(phy["SpindleSpeed"]) : uint.MaxValue;
                                string friendly = phy["FriendlyName"] != null ? phy["FriendlyName"].ToString() : "";
                                string phyModel = phy["Model"] != null ? phy["Model"].ToString() : "";

                                // MediaType: 3 = HDD, 4 = SSD, 5 = SCM; BusType: 17 = NVMe
                                if (mediaType == 4 || mediaType == 5 || busType == 17 ||
                                    friendly.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    friendly.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    phyModel.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    phyModel.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    summary.PrimaryDiskMediaType = "SSD";
                                }
                                else if (mediaType == 3 || (spindle > 0 && spindle <= 30000))
                                {
                                    summary.PrimaryDiskMediaType = "HDD";
                                }
                                else if (spindle == 0)
                                {
                                    summary.PrimaryDiskMediaType = "SSD";
                                }

                                if (diskId == cDiskIndex && !string.IsNullOrEmpty(summary.PrimaryDiskMediaType))
                                    break;
                            }
                        }
                    }
                    catch { }

                    // 2. Fallback: check Win32_DiskDrive model / PNPDeviceID for explicit SSD/NVMe keywords
                    if (string.IsNullOrEmpty(summary.PrimaryDiskMediaType))
                    {
                        using (ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher("SELECT Index, Model, PNPDeviceID FROM Win32_DiskDrive"))
                        {
                            foreach (ManagementObject mo in diskSearcher.Get())
                            {
                                string model = mo["Model"] != null ? mo["Model"].ToString() : "";
                                string pnp = mo["PNPDeviceID"] != null ? mo["PNPDeviceID"].ToString() : "";
                                if (model.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    pnp.IndexOf("NVME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    pnp.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    summary.PrimaryDiskMediaType = "SSD";
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            catch
            {
                summary.PrimaryDiskInfo = "Drive C: (Information unavailable)";
            }

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
                                summary.SecureBootStatus = "Active";
                                summary.SecureBootActive = true;
                            }
                            else
                            {
                                summary.SecureBootStatus = "Inactive";
                                summary.SecureBootActive = false;
                            }
                        }
                        else
                        {
                            summary.SecureBootStatus = "Unavailable / Not supported";
                            summary.SecureBootActive = false;
                        }
                    }
                    else
                    {
                        summary.SecureBootStatus = "Unavailable / Not supported";
                        summary.SecureBootActive = false;
                    }
                }
            }
            catch
            {
                summary.SecureBootStatus = "Unavailable / Not supported";
                summary.SecureBootActive = false;
            }

            // 8b. Firmware Mode (UEFI vs Legacy BIOS)
            try
            {
                bool isUefi = false;
                string fwEnv = Environment.GetEnvironmentVariable("firmware_type");
                if (string.Equals(fwEnv, "UEFI", StringComparison.OrdinalIgnoreCase))
                {
                    isUefi = true;
                }
                else
                {
                    using (RegistryKey rkCtrl = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control"))
                    {
                        if (rkCtrl != null)
                        {
                            object peVal = rkCtrl.GetValue("PEFirmwareType");
                            if (peVal != null && Convert.ToInt32(peVal) == 2) isUefi = true;
                        }
                    }
                    if (!isUefi && summary.SecureBootActive) isUefi = true;
                }
                summary.IsUefi = isUefi;
                summary.FirmwareMode = isUefi ? "UEFI" : "Legacy BIOS";
            }
            catch
            {
                summary.FirmwareMode = "Legacy BIOS";
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
                            summary.TpmStatus = string.Format("Available ({0})", spec);
                            summary.TpmActive = true;
                        }
                        else
                        {
                            summary.TpmStatus = "Inactive";
                            summary.TpmActive = false;
                        }
                        tpmFound = true;
                        break;
                    }
                }

                if (!tpmFound)
                {
                    summary.TpmStatus = "Unavailable / Not supported";
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
                                summary.TpmStatus = string.Format("Available ({0})", ver);
                                summary.TpmActive = true;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            summary.TpmStatus = "Unavailable / Not supported";
                            summary.TpmActive = false;
                        }
                    }
                }
                catch
                {
                    summary.TpmStatus = "Unavailable / Not supported";
                    summary.TpmActive = false;
                }
            }

            // 10. BitLocker & Device Encryption
            DetectBitLockerStatus(summary);

            // 11. Problem / Missing Drivers Detection (Win32_PnPEntity with ConfigManagerErrorCode != 0)
            DetectProblemDevices(summary);

            // 12. Smart Hardware & Maintenance Recommendations Engine
            GenerateHardwareRecommendations(summary);

            return summary;
        }

        private static void DetectBitLockerStatus(HardwareSummary summary)
        {
            string sysDrive = "C:";
            try
            {
                string sysDir = Environment.SystemDirectory;
                if (!string.IsNullOrEmpty(sysDir))
                {
                    string root = Path.GetPathRoot(sysDir);
                    if (!string.IsNullOrEmpty(root))
                        sysDrive = root.TrimEnd('\\');
                }
            }
            catch { }

            // Method 1: WMI query to MicrosoftVolumeEncryption with PacketPrivacy
            try
            {
                ConnectionOptions options = new ConnectionOptions
                {
                    Impersonation = ImpersonationLevel.Impersonate,
                    Authentication = AuthenticationLevel.PacketPrivacy,
                    EnablePrivileges = true,
                    Timeout = TimeSpan.FromSeconds(3)
                };

                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2\Security\MicrosoftVolumeEncryption", options);
                scope.Connect();

                ObjectQuery query = new ObjectQuery("SELECT DeviceID, DriveLetter, ProtectionStatus, ConversionStatus, EncryptionMethod, VolumeType FROM Win32_EncryptableVolume");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string dl = mo["DriveLetter"] != null ? mo["DriveLetter"].ToString() : "";
                        int volType = mo["VolumeType"] != null ? Convert.ToInt32(mo["VolumeType"]) : -1;

                        // Match system drive letter or OS volume (VolumeType == 0)
                        bool isTargetVolume = string.Equals(dl, sysDrive, StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(dl, "C:", StringComparison.OrdinalIgnoreCase) ||
                                              volType == 0;

                        if (isTargetVolume)
                        {
                            int protectionStatus = 0;
                            int conversionStatus = 0;
                            int encryptionMethod = 0;

                            // Check ProtectionStatus (property or method call)
                            if (mo["ProtectionStatus"] != null)
                            {
                                int.TryParse(mo["ProtectionStatus"].ToString(), out protectionStatus);
                            }
                            else
                            {
                                try
                                {
                                    ManagementBaseObject outParams = mo.InvokeMethod("GetProtectionStatus", null, null);
                                    if (outParams != null && outParams["ProtectionStatus"] != null)
                                    {
                                        int.TryParse(outParams["ProtectionStatus"].ToString(), out protectionStatus);
                                    }
                                }
                                catch { }
                            }

                            // Check ConversionStatus (property or method call)
                            if (mo["ConversionStatus"] != null)
                            {
                                int.TryParse(mo["ConversionStatus"].ToString(), out conversionStatus);
                            }
                            else
                            {
                                try
                                {
                                    ManagementBaseObject outParams = mo.InvokeMethod("GetConversionStatus", null, null);
                                    if (outParams != null && outParams["ConversionStatus"] != null)
                                    {
                                        int.TryParse(outParams["ConversionStatus"].ToString(), out conversionStatus);
                                    }
                                }
                                catch { }
                            }

                            // Check EncryptionMethod (0 = None, 1..7 = AES/XTS-AES)
                            if (mo["EncryptionMethod"] != null)
                            {
                                int.TryParse(mo["EncryptionMethod"].ToString(), out encryptionMethod);
                            }

                            // Active if: Protection is ON (1), or volume is Encrypted/Encrypting (1, 2), or Method is active
                            // On clean Windows 11 installs, Device Encryption pre-provisions the drive (ConversionStatus = 1, ProtectionStatus = 0)
                            if (protectionStatus == 1 || conversionStatus == 1 || conversionStatus == 2 || encryptionMethod > 0)
                            {
                                summary.BitLockerStatus = "Active";
                                summary.BitLockerActive = true;
                                return;
                            }
                            else if (protectionStatus == 0 && conversionStatus == 0 && encryptionMethod == 0)
                            {
                                summary.BitLockerStatus = "Inactive";
                                summary.BitLockerActive = false;
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Proceed to manage-bde fallback
            }

            // Method 2: Native manage-bde.exe status fallback
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "manage-bde.exe"),
                    Arguments = string.Format("-status {0}", sysDrive),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);

                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            string lower = output.ToLowerInvariant();

                            // Look for encryption markers in English and Portuguese
                            bool isEncrypted = lower.Contains("fully encrypted") ||
                                               lower.Contains("totalmente criptografado") ||
                                               lower.Contains("encryption in progress") ||
                                               lower.Contains("criptografia em andamento") ||
                                               lower.Contains("protection on") ||
                                               lower.Contains("proteção ativada") ||
                                               lower.Contains("percentage encrypted: 100") ||
                                               lower.Contains("percentual criptografado: 100") ||
                                               lower.Contains("xts-aes") ||
                                               lower.Contains("aes-");

                            bool isDecrypted = (lower.Contains("fully decrypted") || lower.Contains("totalmente descriptografado")) &&
                                               (lower.Contains("protection off") || lower.Contains("proteção desativada"));

                            if (isEncrypted)
                            {
                                summary.BitLockerStatus = "Active";
                                summary.BitLockerActive = true;
                                return;
                            }
                            else if (isDecrypted)
                            {
                                summary.BitLockerStatus = "Inactive";
                                summary.BitLockerActive = false;
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback failed
            }

            // Default fallback if BitLocker is unavailable or not supported
            summary.BitLockerStatus = "Unavailable / Not supported";
            summary.BitLockerActive = false;
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
            log("       STARTING SYSTEM BENCHMARK (WINSAT)        ");
            log("=================================================");
            log("Executing: winsat.exe formal -restart clean");
            log("Please wait a few minutes while Windows benchmarks CPU, Memory, GPU and Storage...");

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
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("  [ERROR] " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }

                log("\n=== WINSAT BENCHMARK COMPLETED SUCCESSFULLY! ===");
            }
            catch (Exception ex)
            {
                log("[WINSAT ERROR] Failed to run benchmark: " + ex.Message);
            }
        }

        public static string SanitizeSmbiosValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return LocalizationHelper.Get("Status_NotAvailable", "Not available");
            }

            string s = value.Trim();

            // Check against common OEM and generic BIOS placeholders
            if (s.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("To Be Filled bt OEM", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("To be filled by OEM", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("To Be Filled By OEM", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("System Product Name", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("System Manufacturer", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Default string", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("All Series", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Type2 - Board Serial Number", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("0123456789", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Base Board Serial Number", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Chassis Serial Number", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Not Specified", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("SKU", StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationHelper.Get("Status_NotAvailable", "Not available");
            }

            return s;
        }

        public static double NormalizeRamGb(double rawGb)
        {
            if (rawGb <= 0) return 0;

            // Common desktop/laptop RAM capacities (GB)
            double[] standardCapacities = new double[] { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256 };
            foreach (double std in standardCapacities)
            {
                // If raw reported RAM is within standard bounds (e.g. 7.1GB - 8.05GB -> 8GB)
                if (rawGb >= (std * 0.88) && rawGb <= (std * 1.05))
                {
                    return std;
                }
            }

            return Math.Round(rawGb, 1);
        }

        private static void DetectProblemDevices(HardwareSummary summary)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, HardwareID, ConfigManagerErrorCode, Status FROM Win32_PnPEntity WHERE ConfigManagerErrorCode IS NOT NULL AND ConfigManagerErrorCode <> 0"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            int errCode = Convert.ToInt32(mo["ConfigManagerErrorCode"]);
                            // Ignore disabled devices (22), hidden/not-present devices (24), and phantom/disconnected devices (45)
                            if (errCode == 22 || errCode == 24 || errCode == 45)
                                continue;

                            string devName = mo["Name"] != null ? mo["Name"].ToString().Trim() : "";
                            string devId = mo["DeviceID"] != null ? mo["DeviceID"].ToString().Trim() : "";
                            string hwId = "";

                            if (mo["HardwareID"] is string[])
                            {
                                string[] hwArr = (string[])mo["HardwareID"];
                                if (hwArr.Length > 0 && !string.IsNullOrEmpty(hwArr[0])) hwId = hwArr[0];
                            }
                            else if (mo["HardwareID"] != null)
                            {
                                hwId = mo["HardwareID"].ToString();
                            }

                            // Ignore legacy ACPI i8042 PS/2 keyboard/mouse controller nodes that report errors when no PS/2 peripheral is plugged in
                            if (devName.IndexOf("PS/2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                devId.IndexOf("PNP0303", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                devId.IndexOf("PNP0F13", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                devId.IndexOf("PNP0F03", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                devId.IndexOf("PNP030B", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                devId.IndexOf("I8042", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                hwId.IndexOf("PNP0303", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                hwId.IndexOf("PNP0F13", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                hwId.IndexOf("PNP0F03", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                hwId.IndexOf("PNP030B", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                hwId.IndexOf("I8042", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(devName))
                            {
                                devName = !string.IsNullOrEmpty(hwId) ? hwId : (!string.IsNullOrEmpty(devId) ? devId : "Unknown Device");
                            }

                            string statusDesc = GetProblemDeviceDescription(errCode);

                            summary.ProblemDevices.Add(new ProblemDeviceEntry
                            {
                                Name = devName,
                                DeviceId = devId,
                                HardwareId = hwId,
                                ErrorCode = errCode,
                                StatusDescription = statusDesc
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static string GetProblemDeviceDescription(int errorCode)
        {
            switch (errorCode)
            {
                case 1:
                case 28:
                    return LocalizationHelper.Get("DevStatus_DriverMissing", "Driver not installed (Code 28)");
                case 10:
                    return LocalizationHelper.Get("DevStatus_Code10", "Device cannot start (Code 10)");
                case 12:
                    return LocalizationHelper.Get("DevStatus_Code12", "Cannot find enough free resources (Code 12)");
                case 14:
                    return LocalizationHelper.Get("DevStatus_Code14", "Restart required to complete setup (Code 14)");
                case 18:
                    return LocalizationHelper.Get("DevStatus_Code18", "Reinstall drivers for this device (Code 18)");
                case 19:
                    return LocalizationHelper.Get("DevStatus_Code19", "Registry information is incomplete or damaged (Code 19)");
                case 22:
                    return LocalizationHelper.Get("DevStatus_Code22", "Device is disabled (Code 22)");
                case 31:
                    return LocalizationHelper.Get("DevStatus_Code31", "Device not working properly (Code 31)");
                case 43:
                    return LocalizationHelper.Get("DevStatus_Code43", "Device stopped because it reported problems (Code 43)");
                default:
                    return string.Format(LocalizationHelper.Get("DevStatus_GenericProblem", "Hardware or driver issue (Code {0})"), errorCode);
            }
        }

        private static void GenerateHardwareRecommendations(HardwareSummary summary)
        {
            if (summary == null) return;
            var list = summary.Recommendations;

            bool isWin11 = (summary.OsName ?? "").IndexOf("11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (summary.OsBuild ?? "").StartsWith("10.0.2", StringComparison.OrdinalIgnoreCase);

            // 1. RAM Recommendations
            if (summary.RamTotalGb > 0 && summary.RamTotalGb <= 4.5)
            {
                if (isWin11)
                {
                    list.Add(new HardwareRecommendation
                    {
                        Title = LocalizationHelper.Get("Rec_RamLow_Title", "RAM Upgrade Strongly Recommended"),
                        Description = LocalizationHelper.Get("Rec_RamLow_Win11Desc", "This system is running Windows 11 with 4 GB of RAM (minimum requirement). Modern workloads and browser tabs will experience page-file throttling. Upgrading to at least 8 GB or 16 GB is strongly advised."),
                        Category = "Memory",
                        Level = RecommendationLevel.Critical,
                        Icon = "🧠",
                        ActionButtonText = LocalizationHelper.Get("Rec_Btn_MemDiag", "Run Memory Diagnostic"),
                        ActionTag = "memory_diag"
                    });
                }
                else
                {
                    list.Add(new HardwareRecommendation
                    {
                        Title = LocalizationHelper.Get("Rec_RamLow_Title", "RAM Upgrade Strongly Recommended"),
                        Description = LocalizationHelper.Get("Rec_RamLow_Win10Desc", "This PC has 4 GB of RAM installed. While Windows 10 functions, running multiple applications concurrently will cause memory bottlenecks. Adding more RAM will noticeably improve responsiveness."),
                        Category = "Memory",
                        Level = RecommendationLevel.Warning,
                        Icon = "🧠",
                        ActionButtonText = LocalizationHelper.Get("Rec_Btn_MemDiag", "Run Memory Diagnostic"),
                        ActionTag = "memory_diag"
                    });
                }
            }
            else if (summary.RamTotalGb > 4.5 && summary.RamTotalGb <= 8.5 && summary.RamSlotsCount == 1)
            {
                list.Add(new HardwareRecommendation
                {
                    Title = LocalizationHelper.Get("Rec_DualChannel_Title", "Enable Dual-Channel Memory"),
                    Description = LocalizationHelper.Get("Rec_DualChannel_Desc", "Detected 8 GB of RAM operating in single-channel mode (1 slot populated). Installing a matching second RAM module enables Dual Channel, providing up to 15-25% higher memory bandwidth for integrated graphics and multitasking."),
                    Category = "Memory",
                    Level = RecommendationLevel.Info,
                    Icon = "⚡",
                    ActionButtonText = LocalizationHelper.Get("Rec_Btn_LearnMore", "Learn More"),
                    ActionTag = "dual_channel_info"
                });
            }

            // 2. Storage Recommendations
            if (summary.PrimaryDiskTotalGb > 0)
            {
                if (summary.PrimaryDiskFreeGb < 15 || (summary.PrimaryDiskFreeGb / summary.PrimaryDiskTotalGb) < 0.12)
                {
                    list.Add(new HardwareRecommendation
                    {
                        Title = LocalizationHelper.Get("Rec_DiskLow_Title", "Low Free Space on Drive C:"),
                        Description = string.Format(LocalizationHelper.Get("Rec_DiskLow_Desc", "Drive C: has only {0} GB free space left. Running low on storage hampers Windows Update, page-file virtual memory, and SSD health. Run System Cleaner to reclaim disk space."), summary.PrimaryDiskFreeGb),
                        Category = "Storage",
                        Level = RecommendationLevel.Critical,
                        Icon = "💾",
                        ActionButtonText = LocalizationHelper.Get("Rec_Btn_SystemCleaner", "Run System Cleaner"),
                        ActionTag = "system_cleaner"
                    });
                }
                else if (summary.PrimaryDiskFreeGb < 30)
                {
                    list.Add(new HardwareRecommendation
                    {
                        Title = LocalizationHelper.Get("Rec_DiskWarning_Title", "Storage Maintenance Advised"),
                        Description = string.Format(LocalizationHelper.Get("Rec_DiskWarning_Desc", "Drive C: has {0} GB free. Clean residual update archives, browser caches, and temp files to ensure smooth system upgrades."), summary.PrimaryDiskFreeGb),
                        Category = "Storage",
                        Level = RecommendationLevel.Warning,
                        Icon = "🧹",
                        ActionButtonText = LocalizationHelper.Get("Rec_Btn_SystemCleaner", "Run System Cleaner"),
                        ActionTag = "system_cleaner"
                    });
                }

                if (string.Equals(summary.PrimaryDiskMediaType, "HDD", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new HardwareRecommendation
                    {
                        Title = LocalizationHelper.Get("Rec_Hdd_Title", "Upgrade Mechanical Hard Drive to SSD"),
                        Description = LocalizationHelper.Get("Rec_Hdd_Desc", "Windows is operating on a mechanical hard drive (HDD). Replacing it with a solid-state drive (SSD SATA or NVMe) will provide up to 5x-10x faster boot times and eliminate system latency."),
                        Category = "Storage",
                        Level = RecommendationLevel.Critical,
                        Icon = "🚀",
                        ActionButtonText = LocalizationHelper.Get("Rec_Btn_LearnMore", "Learn More"),
                        ActionTag = "ssd_upgrade_info"
                    });
                }
            }

            // 3. Driver & Device Issues
            if (summary.ProblemDevices != null && summary.ProblemDevices.Count > 0)
            {
                list.Add(new HardwareRecommendation
                {
                    Title = LocalizationHelper.Get("Rec_Drivers_Title", "Missing or Faulty Hardware Drivers"),
                    Description = string.Format(LocalizationHelper.Get("Rec_Drivers_Desc", "Detected {0} device(s) with missing or error-flagged drivers in Device Manager. Missing chipsets, audio, or graphics drivers reduce system stability."), summary.ProblemDevices.Count),
                    Category = "Drivers",
                    Level = RecommendationLevel.Warning,
                    Icon = "🔌",
                    ActionButtonText = LocalizationHelper.Get("Rec_Btn_InspectDrivers", "Inspect Drivers"),
                    ActionTag = "inspect_drivers"
                });
            }

            // 4. Windows 11 Readiness & Security
            if (isWin11 && !summary.SecureBootActive)
            {
                list.Add(new HardwareRecommendation
                {
                    Title = LocalizationHelper.Get("Rec_SecureBoot_Title", "Enable Secure Boot in UEFI"),
                    Description = LocalizationHelper.Get("Rec_SecureBoot_Desc", "Secure Boot is currently disabled. Enabling it in your UEFI BIOS firmware protects against bootkits and fulfills official Windows 11 platform security standards."),
                    Category = "Security",
                    Level = RecommendationLevel.Warning,
                    Icon = "🛡️",
                    ActionButtonText = LocalizationHelper.Get("Task_RebootBios", "⚡ Reboot to UEFI"),
                    ActionTag = "reboot_uefi"
                });
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
                            ? LocalizationHelper.Get("Msg_BiosDirectRebootError", "The motherboard or BIOS does not support direct software reboot to UEFI firmware.\nPlease restart manually and press DEL or F2 during boot.")
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
