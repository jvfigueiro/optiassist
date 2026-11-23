using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OptiAssist.Platform
{
    public static class PlatformProfiler
    {
        #region WIN32 NATIVE P/INVOKES

        private const ushort IMAGE_FILE_MACHINE_ARM64 = 0xAA64;
        private const uint PF_COMPARE_EXCHANGE128 = 14;
        private const uint PF_SSE4_2_INSTRUCTIONS_AVAILABLE = 38;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll")]
        private static extern bool IsProcessorFeaturePresent(uint processorFeature);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFirmwareType(out uint firmwareType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemCpuSetInformation(
            IntPtr information,
            uint bufferLength,
            out uint returnedLength,
            IntPtr process,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        #endregion

        public static PlatformInfo CollectPlatformInfo(Action<string> log = null)
        {
            PlatformInfo info = new PlatformInfo();
            info.CollectedAtUtc = DateTime.UtcNow;

            try
            {
                CollectCpuInfo(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting CPU: " + ex.Message);
            }

            try
            {
                CollectOsAndFirmwareInfo(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting OS/Firmware: " + ex.Message);
            }

            try
            {
                CollectOemAndBiosInfo(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting OEM/BIOS: " + ex.Message);
            }

            try
            {
                CollectMemoryInfo(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting Memory: " + ex.Message);
            }

            try
            {
                CollectPnpAndGpuDevices(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting PnP/GPUs: " + ex.Message);
            }

            try
            {
                CollectStorageInfo(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error collecting Storage: " + ex.Message);
            }

            try
            {
                DetermineFormFactorAndChassis(info, log);
            }
            catch (Exception ex)
            {
                if (log != null) log("PlatformProfiler: Error determining FormFactor: " + ex.Message);
            }

            return info;
        }

        #region CPU COLLECTION

        private static void CollectCpuInfo(PlatformInfo info, Action<string> log)
        {
            CpuInfo cpu = info.Cpu;

            // 1. Architecture and ARM64 emulation check
            try
            {
                ushort processMachine;
                ushort nativeMachine;
                if (IsWow64Process2(GetCurrentProcess(), out processMachine, out nativeMachine))
                {
                    if (nativeMachine == IMAGE_FILE_MACHINE_ARM64)
                    {
                        cpu.NativeArchitecture = CpuArchitecture.Arm64;
                        if (IntPtr.Size == 8 || processMachine != IMAGE_FILE_MACHINE_ARM64)
                        {
                            cpu.IsArm64Emulated = true;
                        }
                    }
                    else if (nativeMachine == 0x8664)
                    {
                        cpu.NativeArchitecture = CpuArchitecture.X64;
                    }
                    else if (nativeMachine == 0x014c)
                    {
                        cpu.NativeArchitecture = CpuArchitecture.X86;
                    }
                }
                else
                {
                    cpu.NativeArchitecture = Environment.Is64BitOperatingSystem ? CpuArchitecture.X64 : CpuArchitecture.X86;
                }
            }
            catch
            {
                cpu.NativeArchitecture = Environment.Is64BitOperatingSystem ? CpuArchitecture.X64 : CpuArchitecture.X86;
            }

            // 2. Read CPU Registry Details
            try
            {
                using (RegistryKey cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", false))
                {
                    if (cpuKey != null)
                    {
                        cpu.VendorIdentifier = Convert.ToString(cpuKey.GetValue("VendorIdentifier", "")).Trim();
                        cpu.ProcessorNameString = Convert.ToString(cpuKey.GetValue("ProcessorNameString", "")).Trim();
                        cpu.RawIdentifier = Convert.ToString(cpuKey.GetValue("Identifier", "")).Trim();
                    }
                }
            }
            catch { }

            // Parse Family, Model, Stepping from RawIdentifier
            // e.g. "Intel64 Family 6 Model 191 Stepping 2" or "AMD64 Family 25 Model 33 Stepping 0"
            if (!string.IsNullOrEmpty(cpu.RawIdentifier))
            {
                Match mFamily = Regex.Match(cpu.RawIdentifier, @"Family\s+(\d+)", RegexOptions.IgnoreCase);
                if (mFamily.Success)
                {
                    int fam;
                    if (int.TryParse(mFamily.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fam)) cpu.Family = fam;
                }

                Match mModel = Regex.Match(cpu.RawIdentifier, @"Model\s+(\d+)", RegexOptions.IgnoreCase);
                if (mModel.Success)
                {
                    int mdl;
                    if (int.TryParse(mModel.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out mdl)) cpu.Model = mdl;
                }

                Match mStep = Regex.Match(cpu.RawIdentifier, @"Stepping\s+(\d+)", RegexOptions.IgnoreCase);
                if (mStep.Success)
                {
                    int stp;
                    if (int.TryParse(mStep.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out stp)) cpu.Stepping = stp;
                }
            }

            // 3. Instruction Support
            try
            {
                cpu.HasSse42 = IsProcessorFeaturePresent(PF_SSE4_2_INSTRUCTIONS_AVAILABLE);
                cpu.HasCmpxchg16b = IsProcessorFeaturePresent(PF_COMPARE_EXCHANGE128);
            }
            catch
            {
                cpu.HasSse42 = true;
                cpu.HasCmpxchg16b = true;
            }

            cpu.LogicalProcessors = Environment.ProcessorCount;
            cpu.PhysicalCores = cpu.LogicalProcessors;

            // 4. Hybrid Cores Detection (GetSystemCpuSetInformation)
            DetectHybridCores(cpu);

            // 5. Match with Microarchitecture Database
            MicroarchMatchResult match = MicroarchDatabase.Default.Match(
                cpu.VendorIdentifier,
                cpu.Family,
                cpu.Model,
                cpu.Stepping,
                cpu.ProcessorNameString);

            if (match != null)
            {
                cpu.Microarchitecture = match.Microarchitecture;
                cpu.ReleaseYear = match.ReleaseYear;
                cpu.MarketingGen = match.MarketingGen;
                cpu.Suffix = match.Suffix;
                if (!cpu.HasSse42 && match.HasSse42) cpu.HasSse42 = true;
                if (!cpu.HasCmpxchg16b && match.HasCmpxchg16b) cpu.HasCmpxchg16b = true;
            }
        }

        private static void DetectHybridCores(CpuInfo cpu)
        {
            try
            {
                uint bufferLength = 0;
                GetSystemCpuSetInformation(IntPtr.Zero, 0, out bufferLength, GetCurrentProcess(), 0);
                if (bufferLength > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)bufferLength);
                    try
                    {
                        uint returnedLength;
                        if (GetSystemCpuSetInformation(buffer, bufferLength, out returnedLength, GetCurrentProcess(), 0))
                        {
                            // Each SYSTEM_CPU_SET_INFORMATION is 32 bytes on both x86 and x64
                            int structSize = 32;
                            int count = (int)(returnedLength / structSize);
                            int pCores = 0;
                            int eCores = 0;
                            byte minEff = 255;
                            byte maxEff = 0;

                            for (int i = 0; i < count; i++)
                            {
                                IntPtr itemPtr = new IntPtr(buffer.ToInt64() + (i * structSize));
                                int type = Marshal.ReadInt32(itemPtr, 4); // Type offset
                                if (type == 0) // CpuSetInformation
                                {
                                    // EfficiencyClass is at offset 28
                                    byte eff = Marshal.ReadByte(itemPtr, 28);
                                    if (eff < minEff) minEff = eff;
                                    if (eff > maxEff) maxEff = eff;
                                    if (eff > 0) pCores++; else eCores++;
                                }
                            }

                            if (maxEff > minEff && minEff != 255)
                            {
                                cpu.HasHybridCores = true;
                                cpu.PerformanceCores = pCores;
                                cpu.EfficientCores = eCores;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region OS & FIRMWARE

        private static void CollectOsAndFirmwareInfo(PlatformInfo info, Action<string> log)
        {
            OsFirmwareInfo os = info.OsFirmware;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        os.EditionId = Convert.ToString(key.GetValue("EditionID", "")).Trim();
                        os.DisplayVersion = Convert.ToString(key.GetValue("DisplayVersion", "")).Trim();
                        os.ReleaseId = Convert.ToString(key.GetValue("ReleaseId", "")).Trim();
                        os.ProductName = Convert.ToString(key.GetValue("ProductName", "")).Trim();

                        string buildStr = Convert.ToString(key.GetValue("CurrentBuildNumber", ""));
                        if (string.IsNullOrEmpty(buildStr)) buildStr = Convert.ToString(key.GetValue("CurrentBuild", ""));
                        int build;
                        if (int.TryParse(buildStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out build))
                        {
                            os.CurrentBuild = build;
                        }

                        object ubrObj = key.GetValue("UBR", null);
                        if (ubrObj != null)
                        {
                            os.Ubr = Convert.ToInt32(ubrObj, CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            catch { }

            // Firmware Type (UEFI vs Legacy BIOS)
            try
            {
                uint fwType;
                if (GetFirmwareType(out fwType))
                {
                    if (fwType == 1) os.Firmware = SystemFirmwareType.Bios;
                    else if (fwType == 2) os.Firmware = SystemFirmwareType.Uefi;
                    else os.Firmware = SystemFirmwareType.Unknown;
                }
            }
            catch
            {
                os.Firmware = SystemFirmwareType.Unknown;
            }

            // Check Bay Trail 32-bit UEFI
            if (info.Cpu != null && string.Equals(info.Cpu.Microarchitecture, "Silvermont", StringComparison.OrdinalIgnoreCase))
            {
                if (os.Firmware == SystemFirmwareType.Uefi && (!Environment.Is64BitOperatingSystem || !Environment.Is64BitProcess))
                {
                    os.IsBayTrail32BitUefi = true;
                }
            }

            // Tamper Protection check
            try
            {
                using (RegistryKey tpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Features", false))
                {
                    if (tpKey != null)
                    {
                        object tpVal = tpKey.GetValue("TamperProtection", null);
                        if (tpVal != null && Convert.ToInt32(tpVal, CultureInfo.InvariantCulture) == 5)
                        {
                            os.IsTamperProtectionActive = true;
                        }
                    }
                }
            }
            catch { }

            // Active Power Scheme
            try
            {
                using (RegistryKey pKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", false))
                {
                    if (pKey != null)
                    {
                        os.ActivePowerSchemeGuid = Convert.ToString(pKey.GetValue("ActivePowerScheme", "")).Trim();
                    }
                }
            }
            catch { }
        }

        #endregion

        #region OEM & BIOS

        private static void CollectOemAndBiosInfo(PlatformInfo info, Action<string> log)
        {
            OemInfo oem = info.Oem;

            try
            {
                using (RegistryKey biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false))
                {
                    if (biosKey != null)
                    {
                        oem.Manufacturer = Convert.ToString(biosKey.GetValue("SystemManufacturer", "")).Trim();
                        oem.ProductName = Convert.ToString(biosKey.GetValue("SystemProductName", "")).Trim();
                        oem.Family = Convert.ToString(biosKey.GetValue("SystemFamily", "")).Trim();
                        oem.MotherboardManufacturer = Convert.ToString(biosKey.GetValue("BaseBoardManufacturer", "")).Trim();
                        oem.MotherboardProduct = Convert.ToString(biosKey.GetValue("BaseBoardProduct", "")).Trim();
                        oem.BiosReleaseDate = Convert.ToString(biosKey.GetValue("BIOSReleaseDate", "")).Trim();
                    }
                }
            }
            catch { }

            // Parse BiosReleaseYear
            if (!string.IsNullOrEmpty(oem.BiosReleaseDate))
            {
                Match mYear = Regex.Match(oem.BiosReleaseDate, @"(19\d\d|20\d\d)");
                if (mYear.Success)
                {
                    int yr;
                    if (int.TryParse(mYear.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out yr))
                    {
                        oem.BiosReleaseYear = yr;
                    }
                }
            }
        }

        #endregion

        #region MEMORY

        private static void CollectMemoryInfo(PlatformInfo info, Action<string> log)
        {
            MemoryInfo mem = info.Memory;

            try
            {
                MEMORYSTATUSEX memEx = new MEMORYSTATUSEX();
                memEx.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memEx))
                {
                    mem.TotalPhysicalBytes = memEx.ullTotalPhys;
                    mem.TotalPhysicalGb = Math.Round((double)memEx.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 1);
                }
            }
            catch { }

            // WMI Win32_PhysicalMemory for SMBIOS type and ECC
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType, MemoryErrorCorrection, Capacity FROM Win32_PhysicalMemory"))
                {
                    int slots = 0;
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        slots++;
                        object smbType = mo["SMBIOSMemoryType"];
                        if (smbType != null)
                        {
                            int typeCode = Convert.ToInt32(smbType, CultureInfo.InvariantCulture);
                            mem.SmbiosMemoryType = TranslateSmbiosMemoryType(typeCode);
                        }
                    }
                    mem.SlotsUsed = slots;
                }
            }
            catch { }

            try
            {
                using (ManagementObjectSearcher cs = new ManagementObjectSearcher("SELECT TotalPhysicalMemory, Model FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in cs.Get())
                    {
                        if (mem.TotalPhysicalGb <= 0)
                        {
                            object tot = mo["TotalPhysicalMemory"];
                            if (tot != null)
                            {
                                ulong bytes = Convert.ToUInt64(tot, CultureInfo.InvariantCulture);
                                mem.TotalPhysicalBytes = bytes;
                                mem.TotalPhysicalGb = Math.Round((double)bytes / (1024.0 * 1024.0 * 1024.0), 1);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static string TranslateSmbiosMemoryType(int code)
        {
            switch (code)
            {
                case 20: return "DDR";
                case 21: return "DDR2";
                case 22: return "DDR2 FB-DIMM";
                case 24: return "DDR3";
                case 26: return "DDR4";
                case 27: return "LPDDR";
                case 28: return "LPDDR2";
                case 29: return "LPDDR3";
                case 30: return "LPDDR4";
                case 34: return "DDR5";
                case 35: return "LPDDR5";
                default: return "DDR" + code;
            }
        }

        #endregion

        #region PNP & GPU DEVICES

        private static void CollectPnpAndGpuDevices(PlatformInfo info, Action<string> log)
        {
            try
            {
                // Query Win32_PnPEntity for relevant hardware devices (Display, Net, Sound, System)
                string query = "SELECT DeviceID, Name, ClassGuid, Service FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%'";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string devId = Convert.ToString(mo["DeviceID"] ?? "");
                        string name = Convert.ToString(mo["Name"] ?? "");
                        string classGuid = Convert.ToString(mo["ClassGuid"] ?? "");
                        string svc = Convert.ToString(mo["Service"] ?? "");

                        Match mPci = Regex.Match(devId, @"VEN_([0-9a-fA-F]{4})&DEV_([0-9a-fA-F]{4})", RegexOptions.IgnoreCase);
                        if (mPci.Success)
                        {
                            PnpDeviceItem item = new PnpDeviceItem();
                            item.DeviceId = devId;
                            item.Name = name;
                            item.ClassGuid = classGuid;
                            item.Service = svc;
                            item.VendorId = mPci.Groups[1].Value.ToLowerInvariant();
                            item.PciDeviceId = mPci.Groups[2].Value.ToLowerInvariant();
                            info.PnpDevices.Add(item);

                            // ClassGuid {4d36e968-e325-11ce-bfc1-08002be10318} is Display Adapter
                            if (string.Equals(classGuid, "{4d36e968-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                            {
                                GpuDeviceInfo gpu = new GpuDeviceInfo();
                                gpu.PnpDeviceId = devId;
                                gpu.VendorId = item.VendorId;
                                gpu.DeviceId = item.PciDeviceId;
                                gpu.Name = name;

                                PciLookupResult lookup = PciIdsDatabase.Default.Lookup(gpu.VendorId, gpu.DeviceId);
                                if (lookup != null)
                                {
                                    gpu.VendorName = lookup.VendorName;
                                    gpu.ArchitectureFamily = lookup.ArchitectureFamily;
                                    gpu.MarketingSeries = lookup.MarketingSeries;
                                    gpu.IsDiscrete = lookup.IsDiscreteGpu;
                                    gpu.IsIntegrated = lookup.IsIntegratedGpu;
                                }

                                if (!gpu.IsDiscrete && !gpu.IsIntegrated)
                                {
                                    // Fallback heuristic: Intel is usually integrated unless Arc; NVIDIA/AMD desktop is discrete
                                    if (gpu.VendorId == "8086" && (name.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) < 0))
                                    {
                                        gpu.IsIntegrated = true;
                                    }
                                    else
                                    {
                                        gpu.IsDiscrete = true;
                                    }
                                }

                                info.Gpus.Add(gpu);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region STORAGE

        private static void CollectStorageInfo(PlatformInfo info, Action<string> log)
        {
            StorageInfo st = info.Storage;
            string sysDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrEmpty(sysDrive)) sysDrive = "C:";
            st.SystemDriveLetter = sysDrive;
            char driveChar = sysDrive[0];

            // 1. Accurately resolve the physical DiskNumber hosting the OS drive (e.g. C:)
            int osDiskNumber = ResolveOsDiskNumber(driveChar);

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\microsoft\windows\storage",
                    "SELECT DeviceId, MediaType, BusType, FriendlyName, Model, SpindleSpeed FROM MSFT_PhysicalDisk"))
                {
                    int totalDisks = 0;
                    int nvmeDisks = 0;
                    bool matchedOsDisk = false;
                    string fallbackMedia = "Unknown";
                    string fallbackBus = "Unknown";
                    bool fallbackEmmc = false;

                    foreach (ManagementObject mo in searcher.Get())
                    {
                        totalDisks++;
                        string devIdStr = mo["DeviceId"] != null ? mo["DeviceId"].ToString().Trim() : "";
                        int diskId = -1;
                        int.TryParse(devIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out diskId);

                        object mtObj = mo["MediaType"];
                        object btObj = mo["BusType"];
                        object spObj = mo["SpindleSpeed"];
                        string friendly = mo["FriendlyName"] != null ? mo["FriendlyName"].ToString() : "";
                        string model = mo["Model"] != null ? mo["Model"].ToString() : "";

                        int busType = (btObj != null) ? Convert.ToInt32(btObj, CultureInfo.InvariantCulture) : 0;
                        int mediaType = (mtObj != null) ? Convert.ToInt32(mtObj, CultureInfo.InvariantCulture) : 0;
                        uint spindle = (spObj != null) ? Convert.ToUInt32(spObj, CultureInfo.InvariantCulture) : uint.MaxValue;

                        // BusType 17 = NVMe
                        if (busType == 17) nvmeDisks++;

                        string resolvedMedia = TranslateMediaType(mediaType);
                        if (busType == 17 || mediaType == 4 || mediaType == 5 ||
                            friendly.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            friendly.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            model.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            resolvedMedia = "SSD";
                        }
                        else if (mediaType == 3 || (spindle > 0 && spindle <= 30000))
                        {
                            resolvedMedia = "HDD";
                        }
                        else if (spindle == 0 && busType != 12 && busType != 13)
                        {
                            resolvedMedia = "SSD";
                        }

                        string resolvedBus = TranslateBusType(busType);
                        bool isEmmc = (busType == 12 || busType == 13);

                        if (osDiskNumber >= 0 && diskId == osDiskNumber)
                        {
                            st.SystemDiskMediaType = resolvedMedia;
                            st.SystemDiskBusType = resolvedBus;
                            st.IsSystemEmmcOrSd = isEmmc;
                            matchedOsDisk = true;
                        }
                        else if (totalDisks == 1)
                        {
                            fallbackMedia = resolvedMedia;
                            fallbackBus = resolvedBus;
                            fallbackEmmc = isEmmc;
                        }
                    }

                    if (!matchedOsDisk && totalDisks > 0)
                    {
                        st.SystemDiskMediaType = fallbackMedia;
                        st.SystemDiskBusType = fallbackBus;
                        st.IsSystemEmmcOrSd = fallbackEmmc;
                    }

                    st.TotalPhysicalDisksCount = totalDisks;
                    st.TotalNvmeDrivesCount = nvmeDisks;
                }
            }
            catch
            {
                // Fallback using DriveInfo
                try
                {
                    DriveInfo di = new DriveInfo(sysDrive.Substring(0, 1));
                    st.SystemDiskMediaType = di.DriveType.ToString();
                    st.TotalPhysicalDisksCount = 1;
                }
                catch { }
            }

            // Always capture logical capacity & free space on OS drive (C:)
            try
            {
                DriveInfo sysDi = new DriveInfo(sysDrive.Substring(0, 1));
                if (sysDi.IsReady)
                {
                    st.SystemDriveTotalGb = Math.Round((double)sysDi.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                    st.SystemDriveFreeGb = Math.Round((double)sysDi.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                }
            }
            catch { }
        }

        private static int ResolveOsDiskNumber(char driveLetter)
        {
            // Method 1: MSFT_Partition (most accurate on Windows 10/11 GPT/NVMe)
            try
            {
                string q = string.Format("SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{0}'", driveLetter);
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(@"root\microsoft\windows\storage", q))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        if (mo["DiskNumber"] != null)
                        {
                            return Convert.ToInt32(mo["DiskNumber"], CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            catch { }

            // Method 2: Win32_LogicalDiskToPartition fallback
            try
            {
                string q = string.Format("ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{0}:'}} WHERE AssocClass=Win32_LogicalDiskToPartition", driveLetter);
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(q))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        if (mo["DiskIndex"] != null)
                        {
                            return Convert.ToInt32(mo["DiskIndex"], CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            catch { }

            return -1;
        }

        private static string TranslateMediaType(int code)
        {
            switch (code)
            {
                case 3: return "HDD";
                case 4: return "SSD";
                case 5: return "SCM";
                default: return "Unspecified";
            }
        }

        private static string TranslateBusType(int code)
        {
            switch (code)
            {
                case 11: return "SATA";
                case 17: return "NVMe";
                case 12: return "SD";
                case 13: return "eMMC";
                case 7: return "USB";
                case 8: return "RAID";
                case 3: return "ATAPI";
                default: return "Bus" + code;
            }
        }

        #endregion

        #region FORM FACTOR & CHASSIS

        private static void DetermineFormFactorAndChassis(PlatformInfo info, Action<string> log)
        {
            // 1. Check Hypervisors / Virtual Machine
            string oemCombined = (info.Oem.Manufacturer + " " + info.Oem.ProductName + " " + info.Oem.MotherboardProduct).ToLowerInvariant();
            if (oemCombined.IndexOf("vmware", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsVirtualMachine = true;
                info.HypervisorName = "VMware";
                info.FormFactor = ChassisFormFactor.VirtualMachine;
                return;
            }
            if (oemCombined.IndexOf("virtualbox", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsVirtualMachine = true;
                info.HypervisorName = "VirtualBox";
                info.FormFactor = ChassisFormFactor.VirtualMachine;
                return;
            }
            if (oemCombined.IndexOf("qemu", StringComparison.OrdinalIgnoreCase) >= 0 || oemCombined.IndexOf("kvm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsVirtualMachine = true;
                info.HypervisorName = "QEMU/KVM";
                info.FormFactor = ChassisFormFactor.VirtualMachine;
                return;
            }
            if (oemCombined.IndexOf("hyper-v", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (oemCombined.IndexOf("microsoft corporation", StringComparison.OrdinalIgnoreCase) >= 0 && oemCombined.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                info.IsVirtualMachine = true;
                info.HypervisorName = "Hyper-V";
                info.FormFactor = ChassisFormFactor.VirtualMachine;
                return;
            }

            // 2. Battery Detection
            try
            {
                SYSTEM_POWER_STATUS sps;
                if (GetSystemPowerStatus(out sps))
                {
                    if (sps.BatteryFlag != 128 && sps.BatteryFlag != 255)
                    {
                        info.HasBattery = true;
                    }
                }
            }
            catch { }

            // 3. ChassisTypes from Win32_SystemEnclosure
            bool enclosureLaptop = false;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        ushort[] types = mo["ChassisTypes"] as ushort[];
                        if (types != null)
                        {
                            for (int i = 0; i < types.Length; i++)
                            {
                                int ct = types[i];
                                // 8: Portable, 9: Laptop, 10: Notebook, 11: Hand Held, 12: Docking Station, 14: Sub Notebook, 30: Tablet, 31: Convertible, 32: Detachable
                                if (ct == 8 || ct == 9 || ct == 10 || ct == 11 || ct == 14 || ct == 30 || ct == 31 || ct == 32)
                                {
                                    enclosureLaptop = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 4. Form Factor decision
            bool isMobile = info.HasBattery || enclosureLaptop;
            if (isMobile)
            {
                // Differentiate Ultraportable vs Laptop
                string cpuSuffix = (info.Cpu != null && info.Cpu.Suffix != null) ? info.Cpu.Suffix.ToUpperInvariant() : "";
                bool isLowPowerCpu = (cpuSuffix == "U" || cpuSuffix == "Y" || cpuSuffix == "N" || cpuSuffix == "P");
                bool isEmmc = info.Storage.IsSystemEmmcOrSd;

                if (isLowPowerCpu || isEmmc || (info.Cpu != null && string.Equals(info.Cpu.Microarchitecture, "Silvermont", StringComparison.OrdinalIgnoreCase)))
                {
                    info.FormFactor = ChassisFormFactor.Ultraportable;
                }
                else
                {
                    info.FormFactor = ChassisFormFactor.Laptop;
                }
            }
            else
            {
                info.FormFactor = ChassisFormFactor.Desktop;
            }
        }

        #endregion
    }
}
