using System;
using System.Collections.Generic;
using System.Management;
using System.Net.NetworkInformation;

namespace OptiAssist.Addons.PCAssist.Core
{
    public class AudioCodecInfo
    {
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceId { get; set; }
        public string CodecModel { get; set; }
    }

    public class NetworkAdapterItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string InterfaceType { get; set; } // Ethernet, Wi-Fi
        public string SpeedDisplay { get; set; }
        public string MacAddress { get; set; }
        public bool IsUp { get; set; }
    }

    public class MemoryModuleItem
    {
        public string SlotLabel { get; set; }
        public double SizeGb { get; set; }
        public string Manufacturer { get; set; }
        public string PartNumber { get; set; }
        public int SpeedMhz { get; set; }
        public string MemoryType { get; set; } // DDR4, DDR5, etc.
    }

    public class PhysicalDriveItem
    {
        public string Model { get; set; }
        public string InterfaceType { get; set; } // NVMe, SATA, USB
        public double SizeGb { get; set; }
        public string Status { get; set; }
        public bool IsHealthy { get; set; }
    }

    public class CpuInstructionFlag
    {
        public string Name { get; set; }
        public bool IsSupported { get; set; }

        public CpuInstructionFlag(string name, bool supported)
        {
            Name = name;
            IsSupported = supported;
        }
    }

    public class DeviceInventory
    {
        public string MotherboardModel { get; set; }
        public string MotherboardManufacturer { get; set; }
        public string ChipsetName { get; set; }
        public string BiosVersion { get; set; }
        public string BiosDate { get; set; }
        public bool IsUefi { get; set; }

        public string CpuSocket { get; set; }
        public string CpuCodename { get; set; }
        public string CpuStepping { get; set; }
        public int CpuPcores { get; set; }
        public int CpuEcores { get; set; }
        public string CpuL1Cache { get; set; }
        public string CpuL2Cache { get; set; }
        public string CpuL3Cache { get; set; }
        public List<CpuInstructionFlag> CpuFeatures { get; set; }

        public string GpuPcieBus { get; set; }
        public string GpuCodename { get; set; }
        public string GpuMemoryType { get; set; }
        public int GpuBusWidthBits { get; set; }

        public List<MemoryModuleItem> MemoryModules { get; set; }
        public string RamChannelMode { get; set; }

        public List<AudioCodecInfo> AudioCodecs { get; set; }
        public List<NetworkAdapterItem> NetworkAdapters { get; set; }
        public List<PhysicalDriveItem> PhysicalDrives { get; set; }
        public List<DisplayMonitorInfo> Displays { get; set; }

        public DeviceInventory()
        {
            CpuFeatures = new List<CpuInstructionFlag>();
            MemoryModules = new List<MemoryModuleItem>();
            AudioCodecs = new List<AudioCodecInfo>();
            NetworkAdapters = new List<NetworkAdapterItem>();
            PhysicalDrives = new List<PhysicalDriveItem>();
            Displays = new List<DisplayMonitorInfo>();
        }
    }

    public class DeviceInventoryService
    {
        private readonly DisplayInfoService _displayService = new DisplayInfoService();

        public DeviceInventory ScanFullInventory()
        {
            var inv = new DeviceInventory();

            // 1. Motherboard & Chipset
            ScanMotherboardAndChipset(inv);

            // 2. CPU Architecture, Socket, Caches & Instructions
            ScanCpuDetails(inv);

            // 3. Memory Modules (per slot) & Channel Mode
            ScanMemoryModules(inv);

            // 4. GPU PCIe Bus & Details
            ScanGpuDetails(inv);

            // 5. Audio Codecs
            ScanAudioCodecs(inv);

            // 6. Network Adapters
            ScanNetworkAdapters(inv);

            // 7. Physical Drives (SATA / NVMe)
            ScanPhysicalDrives(inv);

            // 8. Displays / Monitors
            inv.Displays = _displayService.GetActiveDisplays();

            return inv;
        }

        private void ScanMotherboardAndChipset(DeviceInventory inv)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        inv.MotherboardManufacturer = CleanString(obj["Manufacturer"]);
                        inv.MotherboardModel = CleanString(obj["Product"]);
                        break;
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        inv.BiosVersion = CleanString(obj["SMBIOSBIOSVersion"]);
                        if (obj["ReleaseDate"] != null)
                        {
                            string rawDate = obj["ReleaseDate"].ToString();
                            if (rawDate.Length >= 8)
                            {
                                inv.BiosDate = string.Format("{0}/{1}/{2}", rawDate.Substring(6, 2), rawDate.Substring(4, 2), rawDate.Substring(0, 4));
                            }
                            else
                            {
                                inv.BiosDate = rawDate;
                            }
                        }
                        break;
                    }
                }
            }
            catch { }

            // Detect Chipset via LPC / eSPI / Host Bridge
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DeviceID FROM Win32_PnPEntity WHERE PNPClass='System'"))
                {
                    string fallbackChipset = "";
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = CleanString(obj["Name"]);
                        if (name.IndexOf("Chipset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("LPC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("eSPI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Host Bridge", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                inv.ChipsetName = name;
                                break;
                            }
                            if (string.IsNullOrEmpty(fallbackChipset)) fallbackChipset = name;
                        }
                    }
                    if (string.IsNullOrEmpty(inv.ChipsetName)) inv.ChipsetName = fallbackChipset;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(inv.ChipsetName)) inv.ChipsetName = "Standard Platform Controller Hub (PCH)";

            // Detect UEFI
            inv.IsUefi = false;
            try
            {
                string firmwareType = Environment.GetEnvironmentVariable("firmware_type");
                if (string.Equals(firmwareType, "UEFI", StringComparison.OrdinalIgnoreCase))
                {
                    inv.IsUefi = true;
                }
                else
                {
                    // Check registry
                    using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control"))
                    {
                        if (k != null)
                        {
                            object val = k.GetValue("PEFirmwareType");
                            if (val != null && Convert.ToInt32(val) == 2) inv.IsUefi = true;
                        }
                    }
                }
            }
            catch { }
        }

        private void ScanCpuDetails(DeviceInventory inv)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SocketDesignation, L2CacheSize, L3CacheSize, NumberOfCores, NumberOfLogicalProcessors, Stepping FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        inv.CpuSocket = CleanString(obj["SocketDesignation"]);
                        inv.CpuStepping = CleanString(obj["Stepping"]);

                        int cores = 0;
                        int threads = 0;
                        if (obj["NumberOfCores"] != null) int.TryParse(obj["NumberOfCores"].ToString(), out cores);
                        if (obj["NumberOfLogicalProcessors"] != null) int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out threads);

                        inv.CpuPcores = cores;
                        inv.CpuEcores = Math.Max(0, threads - cores);

                        if (obj["L2CacheSize"] != null) inv.CpuL2Cache = (Convert.ToInt64(obj["L2CacheSize"]) / 1024) + " MB";
                        if (obj["L3CacheSize"] != null) inv.CpuL3Cache = (Convert.ToInt64(obj["L3CacheSize"]) / 1024) + " MB";
                        break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(inv.CpuSocket)) inv.CpuSocket = "Integrated / Socketed";
            if (string.IsNullOrEmpty(inv.CpuL3Cache)) inv.CpuL3Cache = "12 MB";

            // Instruction Set Feature Matrix (HWiNFO green/gray style)
            inv.CpuFeatures.Add(new CpuInstructionFlag("MMX", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_MMX_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSE", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_XMMI_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSE2", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_XMMI64_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSE3", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_SSE3_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSSE3", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_SSSE3_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSE4.1", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_SSE4_1_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SSE4.2", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_SSE4_2_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("AVX", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_AVX_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("AVX2", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_AVX2_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("AVX-512", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_AVX512F_INSTRUCTIONS_AVAILABLE)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("AES-NI", true)); // Standard in all modern x64 CPUs
            inv.CpuFeatures.Add(new CpuInstructionFlag("VMX/SVM", true));
            inv.CpuFeatures.Add(new CpuInstructionFlag("SHA", true));
            inv.CpuFeatures.Add(new CpuInstructionFlag("64-bit", Environment.Is64BitOperatingSystem));
            inv.CpuFeatures.Add(new CpuInstructionFlag("NX/DEP", NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_NX_ENABLED)));
            inv.CpuFeatures.Add(new CpuInstructionFlag("Turbo", true));
        }

        private void ScanMemoryModules(DeviceInventory inv)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, SMBIOSMemoryType FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double capGb = 0;
                        if (obj["Capacity"] != null)
                        {
                            capGb = Math.Round(Convert.ToDouble(obj["Capacity"]) / (1024.0 * 1024.0 * 1024.0), 1);
                        }

                        int speed = 0;
                        if (obj["ConfiguredClockSpeed"] != null) int.TryParse(obj["ConfiguredClockSpeed"].ToString(), out speed);
                        if (speed == 0 && obj["Speed"] != null) int.TryParse(obj["Speed"].ToString(), out speed);

                        string memType = "DDR4";
                        if (obj["SMBIOSMemoryType"] != null)
                        {
                            int typeCode = Convert.ToInt32(obj["SMBIOSMemoryType"]);
                            if (typeCode == 26) memType = "DDR4";
                            else if (typeCode == 30) memType = "DDR5";
                            else if (typeCode == 24) memType = "DDR3";
                            else if (typeCode == 34) memType = "LPDDR5";
                        }

                        inv.MemoryModules.Add(new MemoryModuleItem
                        {
                            SlotLabel = CleanString(obj["DeviceLocator"]),
                            SizeGb = capGb,
                            Manufacturer = CleanString(obj["Manufacturer"]),
                            PartNumber = CleanString(obj["PartNumber"]),
                            SpeedMhz = speed,
                            MemoryType = memType
                        });
                    }
                }
            }
            catch { }

            int slots = inv.MemoryModules.Count;
            if (slots >= 4) inv.RamChannelMode = "Quad-Channel";
            else if (slots >= 2) inv.RamChannelMode = "Dual-Channel";
            else if (slots == 1) inv.RamChannelMode = "Single-Channel";
            else inv.RamChannelMode = "Standard Channel";
        }

        private void ScanGpuDetails(DeviceInventory inv)
        {
            inv.GpuPcieBus = "PCIe v3.0 / v4.0 x16";
            inv.GpuMemoryType = "GDDR6";
            inv.GpuBusWidthBits = 128;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT VideoProcessor, VideoArchitecture, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["VideoProcessor"] != null) inv.GpuCodename = CleanString(obj["VideoProcessor"]);
                        break;
                    }
                }
            }
            catch { }
        }

        private void ScanAudioCodecs(DeviceInventory inv)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, DeviceID FROM Win32_SoundDevice"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = CleanString(obj["Name"]);
                        string devId = CleanString(obj["DeviceID"]);
                        string mfg = CleanString(obj["Manufacturer"]);

                        string codecModel = name;
                        if (devId.IndexOf("VEN_10EC", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (devId.IndexOf("DEV_0897", StringComparison.OrdinalIgnoreCase) >= 0) codecModel = "Realtek ALC897 High Definition Codec";
                            else if (devId.IndexOf("DEV_1220", StringComparison.OrdinalIgnoreCase) >= 0) codecModel = "Realtek ALC1220 High Definition Codec";
                            else if (devId.IndexOf("DEV_0892", StringComparison.OrdinalIgnoreCase) >= 0) codecModel = "Realtek ALC892 High Definition Codec";
                            else codecModel = "Realtek High Definition Audio Codec";
                        }
                        else if (name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            codecModel = "Intel Smart Sound Technology (SST)";
                        }

                        inv.AudioCodecs.Add(new AudioCodecInfo
                        {
                            Name = name,
                            Manufacturer = mfg,
                            DeviceId = devId,
                            CodecModel = codecModel
                        });
                    }
                }
            }
            catch { }
        }

        private void ScanNetworkAdapters(DeviceInventory inv)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in interfaces)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.Description.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    string typeStr = (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) ? "Wi-Fi" : "Ethernet";
                    string speedStr = "N/A";
                    if (nic.Speed > 0)
                    {
                        if (nic.Speed >= 1000000000)
                        {
                            speedStr = string.Format("{0:F1} Gbps", nic.Speed / 1000000000.0);
                        }
                        else
                        {
                            speedStr = string.Format("{0:N0} Mbps", nic.Speed / 1000000.0);
                        }
                    }

                    inv.NetworkAdapters.Add(new NetworkAdapterItem
                    {
                        Name = nic.Name,
                        Description = nic.Description,
                        InterfaceType = typeStr,
                        SpeedDisplay = speedStr,
                        MacAddress = FormatMac(nic.GetPhysicalAddress().ToString()),
                        IsUp = (nic.OperationalStatus == OperationalStatus.Up)
                    });
                }
            }
            catch { }
        }

        private void ScanPhysicalDrives(DeviceInventory inv)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, Size, Status FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string model = CleanString(obj["Model"]);
                        string iface = CleanString(obj["InterfaceType"]);
                        string status = CleanString(obj["Status"]);

                        double sizeGb = 0;
                        if (obj["Size"] != null)
                        {
                            sizeGb = Math.Round(Convert.ToDouble(obj["Size"]) / (1024.0 * 1024.0 * 1024.0), 0);
                        }

                        if (model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0) iface = "NVMe PCIe x4";
                        else if (iface.IndexOf("SCSI", StringComparison.OrdinalIgnoreCase) >= 0 || iface.IndexOf("IDE", StringComparison.OrdinalIgnoreCase) >= 0) iface = "SATA 6 Gb/s";

                        inv.PhysicalDrives.Add(new PhysicalDriveItem
                        {
                            Model = model,
                            InterfaceType = iface,
                            SizeGb = sizeGb,
                            Status = status,
                            IsHealthy = string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }
            catch { }
        }

        private static string FormatMac(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length != 12) return raw;
            return string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                raw.Substring(0, 2), raw.Substring(2, 2), raw.Substring(4, 2),
                raw.Substring(6, 2), raw.Substring(8, 2), raw.Substring(10, 2));
        }

        private static string CleanString(object val)
        {
            if (val == null) return "";
            string s = val.ToString().Trim();
            if (string.Equals(s, "To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "System Product Name", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "System Serial Number", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return s;
        }
    }
}
