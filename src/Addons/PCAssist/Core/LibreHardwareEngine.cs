using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

namespace OptiAssist.Addons.PCAssist.Core
{
    public class LibreSensorRow
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public double? Value { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public string Unit { get; set; }

        public string FormattedValue { get { return FormatVal(Value, Unit); } }
        public string FormattedMin { get { return FormatVal(Min, Unit); } }
        public string FormattedMax { get { return FormatVal(Max, Unit); } }

        private static string FormatVal(double? v, string unit)
        {
            if (!v.HasValue || double.IsNaN(v.Value) || double.IsInfinity(v.Value)) return "—";
            if (unit == "RPM" || unit == "MHz") return string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", v.Value, unit);
            if (unit == "V") return string.Format(CultureInfo.InvariantCulture, "{0:0.000} V", v.Value);
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", v.Value, unit);
        }
    }

    public class LibreHardwareGroup
    {
        public string HardwareName { get; set; }
        public string HardwareType { get; set; }
        public string Icon { get; set; }
        public List<LibreSensorRow> Sensors { get; set; }

        public LibreHardwareGroup()
        {
            Sensors = new List<LibreSensorRow>();
        }
    }

    public class LibreStorageSummary
    {
        public string Name { get; set; }
        public double? TempC { get; set; }
        public double? RemainingLifePercent { get; set; }
        public double? UsedSpacePercent { get; set; }
        public double? TotalWrittenGb { get; set; }
    }

    public class LibreTelemetrySnapshot
    {
        public bool IsAvailable { get; set; }
        public bool IsPawnIoInstalled { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }

        // CPU
        public string CpuName { get; set; }
        public double? CpuUsagePercent { get; set; }
        public double? CpuPackageTempC { get; set; }
        public double? CpuClockMhz { get; set; }
        public double? CpuPackagePowerW { get; set; }

        // GPU
        public string GpuName { get; set; }
        public double? GpuUsagePercent { get; set; }
        public double? GpuTempC { get; set; }
        public double? GpuHotSpotTempC { get; set; }
        public double? GpuVramUsedMb { get; set; }
        public double? GpuVramTotalMb { get; set; }
        public double? GpuClockMhz { get; set; }
        public double? GpuPowerW { get; set; }
        public double? GpuFanPercent { get; set; }

        // Memory
        public double? RamUsedGb { get; set; }
        public double? RamAvailableGb { get; set; }
        public double? RamUsagePercent { get; set; }

        // Network (aggregate KB/s)
        public double NetworkDownKbSec { get; set; }
        public double NetworkUpKbSec { get; set; }

        // Battery (native IOCTL via LibreHardwareMonitor)
        public bool HasBattery { get; set; }
        public string BatteryName { get; set; }
        public double? BatteryChargePercent { get; set; }
        public double? BatteryDegradationPercent { get; set; }
        public double? BatteryVoltageV { get; set; }
        public double? BatteryChargeRateW { get; set; }
        public double? BatteryDischargeRateW { get; set; }
        public double? BatteryDesignedCapMwh { get; set; }
        public double? BatteryFullCapMwh { get; set; }
        public double? BatteryRemainingCapMwh { get; set; }

        // Storage SMART/NVMe
        public List<LibreStorageSummary> StorageDrives { get; set; }

        // All grouped sensors for the Sensors tab
        public List<LibreHardwareGroup> Groups { get; set; }

        public LibreTelemetrySnapshot()
        {
            Timestamp = DateTime.Now;
            StorageDrives = new List<LibreStorageSummary>();
            Groups = new List<LibreHardwareGroup>();
        }
    }

    /// <summary>
    /// Thread-safe wrapper around LibreHardwareMonitorLib (MPL-2.0) with graceful fallback
    /// when PawnIO kernel driver is not installed. Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public class LibreHardwareEngine : IDisposable
    {
        private class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }

            public void VisitHardware(IHardware hardware)
            {
                try
                {
                    hardware.Update();
                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        subHardware.Accept(this);
                    }
                }
                catch { }
            }

            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

        private readonly object _lock = new object();
        private Computer _computer;
        private readonly UpdateVisitor _visitor = new UpdateVisitor();
        private readonly Dictionary<string, double> _minTrack = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _maxTrack = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private bool _isOpen = false;
        private string _initError = "";
        private readonly Action<string> _logger;

        public const string PawnIoDownloadUrl = "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

        public LibreHardwareEngine(Action<string> logger)
        {
            _logger = logger;
        }

        public static bool IsPawnIoInstalled()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO", false))
                {
                    if (k != null) return true;
                }
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(pf) && File.Exists(Path.Combine(pf, @"PawnIO\PawnIO.sys"))) return true;
                string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (!string.IsNullOrEmpty(sys) && File.Exists(Path.Combine(sys, @"drivers\PawnIO.sys"))) return true;
            }
            catch { }
            return false;
        }

        private static bool HasVirtualizedStorageDisk()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Model, Caption FROM Win32_DiskDrive"))
                {
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        string model = Convert.ToString(mo["Model"] ?? "") + " " + Convert.ToString(mo["Caption"] ?? "");
                        string low = model.ToLowerInvariant();
                        if (low.Contains("virtio") || low.Contains("qemu") || low.Contains("vmware") ||
                            low.Contains("vbox") || low.Contains("virtual hd") || low.Contains("msft virtual"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public bool EnsureOpen()
        {
            lock (_lock)
            {
                if (_isOpen && _computer != null) return true;
                try
                {
                    bool isVirtStorage = HasVirtualizedStorageDisk();
                    _computer = new Computer();
                    _computer.IsCpuEnabled = true;
                    _computer.IsGpuEnabled = true;
                    _computer.IsMemoryEnabled = true;
                    _computer.IsMotherboardEnabled = true;
                    _computer.IsControllerEnabled = true;
                    _computer.IsStorageEnabled = !isVirtStorage;
                    _computer.IsNetworkEnabled = true;
                    _computer.IsBatteryEnabled = true;
                    _computer.Open();
                    _isOpen = true;
                    _initError = "";
                    if (_logger != null) _logger("[LHM] LibreHardwareMonitorLib initialized (PawnIO=" + IsPawnIoInstalled() + ", Storage=" + (!isVirtStorage) + ")");
                    return true;
                }
                catch (Exception ex)
                {
                    _initError = ex.Message;
                    _isOpen = false;
                    if (_logger != null) _logger("[LHM ERROR] Failed to open Computer: " + ex.Message);
                    return false;
                }
            }
        }

        public void Reinitialize()
        {
            lock (_lock)
            {
                CloseInternal();
                _minTrack.Clear();
                _maxTrack.Clear();
            }
            EnsureOpen();
        }

        public void ResetMinMax()
        {
            lock (_lock)
            {
                _minTrack.Clear();
                _maxTrack.Clear();
            }
        }

        public LibreTelemetrySnapshot Sample()
        {
            LibreTelemetrySnapshot snap = new LibreTelemetrySnapshot();
            snap.IsPawnIoInstalled = IsPawnIoInstalled();
            snap.Timestamp = DateTime.Now;

            lock (_lock)
            {
                if (!_isOpen || _computer == null)
                {
                    if (!EnsureOpen())
                    {
                        snap.IsAvailable = false;
                        snap.ErrorMessage = _initError;
                        return snap;
                    }
                }

                try
                {
                    _computer.Accept(_visitor);
                    snap.IsAvailable = true;

                    double cpuClockSum = 0;
                    int cpuClockCount = 0;

                    foreach (IHardware hw in _computer.Hardware)
                    {
                        LibreHardwareGroup group = new LibreHardwareGroup();
                        group.HardwareName = hw.Name ?? hw.HardwareType.ToString();
                        group.HardwareType = hw.HardwareType.ToString();
                        group.Icon = GetHardwareIcon(hw.HardwareType);

                        List<ISensor> allSensors = new List<ISensor>();
                        CollectSensorsRecursive(hw, allSensors);

                        LibreStorageSummary storageSum = (hw.HardwareType == HardwareType.Storage)
                            ? new LibreStorageSummary() { Name = group.HardwareName }
                            : null;

                        for (int i = 0; i < allSensors.Count; i++)
                        {
                            ISensor s = allSensors[i];
                            if (s == null || !s.Value.HasValue) continue;
                            double val = (double)s.Value.Value;
                            if (double.IsNaN(val) || double.IsInfinity(val)) continue;

                            string id = (s.Identifier != null) ? s.Identifier.ToString() : (group.HardwareName + "/" + s.SensorType + "/" + s.Name);
                            double minVal = val;
                            double maxVal = val;
                            double prevMin;
                            if (_minTrack.TryGetValue(id, out prevMin))
                            {
                                minVal = Math.Min(prevMin, val);
                            }
                            _minTrack[id] = minVal;

                            double prevMax;
                            if (_maxTrack.TryGetValue(id, out prevMax))
                            {
                                maxVal = Math.Max(prevMax, val);
                            }
                            _maxTrack[id] = maxVal;

                            LibreSensorRow row = new LibreSensorRow();
                            row.Identifier = id;
                            row.Name = s.Name ?? "";
                            row.Category = s.SensorType.ToString();
                            row.Value = val;
                            row.Min = minVal;
                            row.Max = maxVal;
                            row.Unit = GetSensorUnit(s.SensorType);
                            group.Sensors.Add(row);

                            // Extract headline metrics
                            string sNameLow = row.Name.ToLowerInvariant();
                            if (hw.HardwareType == HardwareType.Cpu)
                            {
                                snap.CpuName = group.HardwareName;
                                if (s.SensorType == SensorType.Load && sNameLow.Contains("total"))
                                {
                                    snap.CpuUsagePercent = val;
                                }
                                else if (s.SensorType == SensorType.Temperature)
                                {
                                    if (sNameLow.Contains("package") || sNameLow.Contains("tctl") || sNameLow.Contains("tdie") || !snap.CpuPackageTempC.HasValue)
                                    {
                                        snap.CpuPackageTempC = val;
                                    }
                                }
                                else if (s.SensorType == SensorType.Clock && sNameLow.Contains("core") && !sNameLow.Contains("effective") && val > 100)
                                {
                                    cpuClockSum += val;
                                    cpuClockCount++;
                                }
                                else if (s.SensorType == SensorType.Power && (sNameLow.Contains("package") || !snap.CpuPackagePowerW.HasValue))
                                {
                                    snap.CpuPackagePowerW = val;
                                }
                            }
                            else if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel)
                            {
                                // Prefer discrete GPU if multiple exist
                                bool isDiscrete = (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd);
                                if (string.IsNullOrEmpty(snap.GpuName) || (isDiscrete && snap.GpuName.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    snap.GpuName = group.HardwareName;
                                }
                                if (string.Equals(snap.GpuName, group.HardwareName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (s.SensorType == SensorType.Load && (sNameLow.Contains("gpu core") || sNameLow.Contains("d3d 3d") || !snap.GpuUsagePercent.HasValue))
                                    {
                                        snap.GpuUsagePercent = val;
                                    }
                                    else if (s.SensorType == SensorType.Temperature)
                                    {
                                        if (sNameLow.Contains("hot spot") || sNameLow.Contains("hotspot")) snap.GpuHotSpotTempC = val;
                                        else if (sNameLow.Contains("core") || !snap.GpuTempC.HasValue) snap.GpuTempC = val;
                                    }
                                    else if (s.SensorType == SensorType.SmallData)
                                    {
                                        if (sNameLow.Contains("memory used") || sNameLow.Contains("dedicated memory used")) snap.GpuVramUsedMb = val;
                                        else if (sNameLow.Contains("memory total")) snap.GpuVramTotalMb = val;
                                    }
                                    else if (s.SensorType == SensorType.Clock && (sNameLow.Contains("core") || !snap.GpuClockMhz.HasValue))
                                    {
                                        snap.GpuClockMhz = val;
                                    }
                                    else if (s.SensorType == SensorType.Power && (sNameLow.Contains("package") || sNameLow.Contains("gpu") || !snap.GpuPowerW.HasValue))
                                    {
                                        snap.GpuPowerW = val;
                                    }
                                    else if (s.SensorType == SensorType.Control && sNameLow.Contains("fan"))
                                    {
                                        snap.GpuFanPercent = val;
                                    }
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Memory)
                            {
                                if (s.SensorType == SensorType.Data && sNameLow == "memory used") snap.RamUsedGb = val;
                                else if (s.SensorType == SensorType.Data && sNameLow == "memory available") snap.RamAvailableGb = val;
                                else if (s.SensorType == SensorType.Load && sNameLow == "memory") snap.RamUsagePercent = val;
                            }
                            else if (hw.HardwareType == HardwareType.Storage && storageSum != null)
                            {
                                if (s.SensorType == SensorType.Temperature && !storageSum.TempC.HasValue) storageSum.TempC = val;
                                else if (s.SensorType == SensorType.Level && (sNameLow.Contains("life") || sNameLow.Contains("health"))) storageSum.RemainingLifePercent = val;
                                else if (s.SensorType == SensorType.Load && sNameLow.Contains("used space")) storageSum.UsedSpacePercent = val;
                                else if (s.SensorType == SensorType.Data && sNameLow.Contains("written")) storageSum.TotalWrittenGb = val;
                            }
                            else if (hw.HardwareType == HardwareType.Network)
                            {
                                if (s.SensorType == SensorType.Throughput)
                                {
                                    // LibreHardwareMonitor reports Network Throughput in Bytes/sec
                                    if (sNameLow.Contains("download")) snap.NetworkDownKbSec += (val / 1024.0);
                                    else if (sNameLow.Contains("upload")) snap.NetworkUpKbSec += (val / 1024.0);
                                }
                            }
                            else if (hw.HardwareType == HardwareType.Battery)
                            {
                                snap.HasBattery = true;
                                snap.BatteryName = group.HardwareName;
                                if (s.SensorType == SensorType.Level && sNameLow.Contains("charge")) snap.BatteryChargePercent = val;
                                else if (s.SensorType == SensorType.Level && sNameLow.Contains("degradation")) snap.BatteryDegradationPercent = val;
                                else if (s.SensorType == SensorType.Voltage) snap.BatteryVoltageV = val;
                                else if (s.SensorType == SensorType.Power && sNameLow.Contains("charge")) snap.BatteryChargeRateW = val;
                                else if (s.SensorType == SensorType.Power && sNameLow.Contains("discharge")) snap.BatteryDischargeRateW = val;
                                else if (s.SensorType == SensorType.Energy)
                                {
                                    if (sNameLow.Contains("designed")) snap.BatteryDesignedCapMwh = val;
                                    else if (sNameLow.Contains("fully")) snap.BatteryFullCapMwh = val;
                                    else if (sNameLow.Contains("remaining")) snap.BatteryRemainingCapMwh = val;
                                }
                            }
                        }

                        if (storageSum != null)
                        {
                            snap.StorageDrives.Add(storageSum);
                        }

                        if (group.Sensors.Count > 0)
                        {
                            snap.Groups.Add(group);
                        }
                    }

                    if (cpuClockCount > 0)
                    {
                        snap.CpuClockMhz = cpuClockSum / cpuClockCount;
                    }
                }
                catch (Exception ex)
                {
                    snap.IsAvailable = false;
                    snap.ErrorMessage = ex.Message;
                }
            }

            return snap;
        }

        public string ExportTextReport(LibreTelemetrySnapshot snap)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine("  OPTIASSIST HARDWARE MONITOR — LIBREHARDWAREMONITOR REPORT");
            sb.AppendLine("  Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("  PawnIO Driver: " + (IsPawnIoInstalled() ? "Installed" : "Not Installed"));
            sb.AppendLine("============================================================");
            sb.AppendLine();

            if (snap == null || snap.Groups == null || snap.Groups.Count == 0)
            {
                sb.AppendLine("No sensor readings available.");
                return sb.ToString();
            }

            for (int g = 0; g < snap.Groups.Count; g++)
            {
                LibreHardwareGroup grp = snap.Groups[g];
                sb.AppendLine(string.Format("[{0}] {1}", grp.HardwareType, grp.HardwareName));
                sb.AppendLine(new string('-', 60));
                for (int i = 0; i < grp.Sensors.Count; i++)
                {
                    LibreSensorRow r = grp.Sensors[i];
                    sb.AppendLine(string.Format("  {0,-32} | {1,12} | Min: {2,10} | Max: {3,10}",
                        r.Name, r.FormattedValue, r.FormattedMin, r.FormattedMax));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static void DownloadAndInstallPawnIoAsync(Action<string> logger, Action<bool, string> onComplete)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    string tempExe = Path.Combine(Path.GetTempPath(), "PawnIO_setup.exe");
                    if (logger != null) logger("[PAWNIO] Downloading official PawnIO installer from GitHub...");

                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "OptiAssist-HardwareMonitor");
                        wc.DownloadFile(PawnIoDownloadUrl, tempExe);
                    }

                    if (!File.Exists(tempExe) || new FileInfo(tempExe).Length < 10000)
                    {
                        if (onComplete != null) onComplete(false, "Downloaded file is invalid or empty.");
                        return;
                    }

                    if (logger != null) logger("[PAWNIO] Launching PawnIO_setup.exe...");
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        FileName = tempExe,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    using (Process proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit();
                        }
                    }

                    bool installed = IsPawnIoInstalled();
                    if (logger != null) logger("[PAWNIO] Installer finished. Installed=" + installed);
                    if (onComplete != null)
                    {
                        onComplete(installed, installed
                            ? "PawnIO installed successfully!"
                            : "Installer closed, but PawnIO service was not detected yet.");
                    }
                }
                catch (Exception ex)
                {
                    if (logger != null) logger("[PAWNIO ERROR] " + ex.Message);
                    if (onComplete != null) onComplete(false, ex.Message);
                }
            });
        }

        private static void CollectSensorsRecursive(IHardware hw, List<ISensor> list)
        {
            if (hw == null) return;
            if (hw.Sensors != null)
            {
                for (int i = 0; i < hw.Sensors.Length; i++)
                {
                    list.Add(hw.Sensors[i]);
                }
            }
            if (hw.SubHardware != null)
            {
                for (int i = 0; i < hw.SubHardware.Length; i++)
                {
                    CollectSensorsRecursive(hw.SubHardware[i], list);
                }
            }
        }

        private static string GetHardwareIcon(HardwareType type)
        {
            switch (type)
            {
                case HardwareType.Cpu: return "🧠";
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel: return "🎮";
                case HardwareType.Motherboard:
                case HardwareType.SuperIO: return "🧩";
                case HardwareType.Memory: return "💾";
                case HardwareType.Storage: return "💿";
                case HardwareType.Network: return "🌐";
                case HardwareType.Battery: return "🔋";
                default: return "⚡";
            }
        }

        private static string GetSensorUnit(SensorType type)
        {
            switch (type)
            {
                case SensorType.Voltage: return "V";
                case SensorType.Current: return "A";
                case SensorType.Clock: return "MHz";
                case SensorType.Temperature: return "°C";
                case SensorType.Load:
                case SensorType.Control:
                case SensorType.Level: return "%";
                case SensorType.Fan: return "RPM";
                case SensorType.Flow: return "L/h";
                case SensorType.Power: return "W";
                case SensorType.Data: return "GB";
                case SensorType.SmallData: return "MB";
                case SensorType.Throughput: return "B/s";
                case SensorType.Energy: return "mWh";
                case SensorType.Frequency: return "Hz";
                default: return "";
            }
        }

        private void CloseInternal()
        {
            if (_computer != null)
            {
                try { _computer.Close(); } catch { }
                _computer = null;
            }
            _isOpen = false;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                CloseInternal();
            }
        }
    }
}
