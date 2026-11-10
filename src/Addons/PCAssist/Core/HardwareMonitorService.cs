using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace OptiAssist.Addons.PCAssist.Core
{
    public class CpuMetrics
    {
        public string Name { get; set; }
        public int Cores { get; set; }
        public int Threads { get; set; }
        public double UsagePercent { get; set; }
        public double ClockGhz { get; set; }
        public double? TemperatureC { get; set; }
    }

    public class RamMetrics
    {
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double FreeGb { get; set; }
        public double UsagePercent { get; set; }
        public int SpeedMhz { get; set; }
        public int SlotsCount { get; set; }
    }

    public class GpuMetrics
    {
        public string Name { get; set; }
        public string DriverVersion { get; set; }
        public long DedicatedVramMb { get; set; }
        public double UsagePercent { get; set; }
    }

    public class DiskPartitionMetrics
    {
        public string Root { get; set; }
        public string VolumeLabel { get; set; }
        public double TotalGb { get; set; }
        public double FreeGb { get; set; }
        public double UsagePercent { get; set; }
    }

    public class StorageMetrics
    {
        public double ReadRateMbSec { get; set; }
        public double WriteRateMbSec { get; set; }
        public bool SmartHealthy { get; set; }
        public string SmartStatusMessage { get; set; }
        public List<DiskPartitionMetrics> Partitions { get; set; }

        public StorageMetrics()
        {
            Partitions = new List<DiskPartitionMetrics>();
        }
    }

    public class SystemSnapshot
    {
        public CpuMetrics Cpu { get; set; }
        public RamMetrics Ram { get; set; }
        public GpuMetrics Gpu { get; set; }
        public StorageMetrics Storage { get; set; }
        public DateTime Timestamp { get; set; }

        public SystemSnapshot()
        {
            Cpu = new CpuMetrics();
            Ram = new RamMetrics();
            Gpu = new GpuMetrics();
            Storage = new StorageMetrics();
            Timestamp = DateTime.Now;
        }
    }

    public class HardwareMonitorService : IDisposable
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _diskReadCounter;
        private PerformanceCounter _diskWriteCounter;
        private PerformanceCounter _gpuCounter;

        private string _cachedCpuName = "";
        private int _cachedCpuCores = 0;
        private int _cachedCpuThreads = 0;
        private string _cachedGpuName = "";
        private string _cachedGpuDriver = "";
        private long _cachedGpuVramMb = 0;
        private int _cachedRamSpeed = 0;
        private int _cachedRamSlots = 0;
        private bool _hasInitializedStaticInfo = false;

        public HardwareMonitorService()
        {
            InitializeCounters();
        }

        private void InitializeCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue(); // Prime first sample
            }
            catch { _cpuCounter = null; }

            try
            {
                _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskReadCounter.NextValue();
            }
            catch { _diskReadCounter = null; }

            try
            {
                _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                _diskWriteCounter.NextValue();
            }
            catch { _diskWriteCounter = null; }

            try
            {
                // Attempt to hook GPU Engine counter for 3D engine on Windows 10/11
                PerformanceCounterCategory category = new PerformanceCounterCategory("GPU Engine");
                string[] instanceNames = category.GetInstanceNames();
                foreach (string inst in instanceNames)
                {
                    if (inst.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                        _gpuCounter.NextValue();
                        break;
                    }
                }
            }
            catch { _gpuCounter = null; }
        }

        private void EnsureStaticInfo()
        {
            if (_hasInitializedStaticInfo) return;
            _hasInitializedStaticInfo = true;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        _cachedCpuName = obj["Name"] != null ? obj["Name"].ToString().Trim() : "";
                        if (obj["NumberOfCores"] != null) int.TryParse(obj["NumberOfCores"].ToString(), out _cachedCpuCores);
                        if (obj["NumberOfLogicalProcessors"] != null) int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out _cachedCpuThreads);
                        break;
                    }
                }
            }
            catch { }

            if (_cachedCpuThreads == 0) _cachedCpuThreads = Environment.ProcessorCount;
            if (_cachedCpuCores == 0) _cachedCpuCores = _cachedCpuThreads;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"] != null ? obj["Name"].ToString().Trim() : "";
                        if (!string.IsNullOrEmpty(name))
                        {
                            _cachedGpuName = name;
                            _cachedGpuDriver = obj["DriverVersion"] != null ? obj["DriverVersion"].ToString() : "";
                            if (obj["AdapterRAM"] != null)
                            {
                                long ramBytes = 0;
                                long.TryParse(obj["AdapterRAM"].ToString(), out ramBytes);
                                _cachedGpuVramMb = ramBytes / (1024 * 1024);
                            }
                            break;
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
                {
                    int slots = 0;
                    int maxSpeed = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        slots++;
                        int speed = 0;
                        if (obj["ConfiguredClockSpeed"] != null) int.TryParse(obj["ConfiguredClockSpeed"].ToString(), out speed);
                        if (speed == 0 && obj["Speed"] != null) int.TryParse(obj["Speed"].ToString(), out speed);
                        if (speed > maxSpeed) maxSpeed = speed;
                    }
                    _cachedRamSlots = slots;
                    _cachedRamSpeed = maxSpeed;
                }
            }
            catch { }
        }

        public SystemSnapshot CaptureSnapshot()
        {
            EnsureStaticInfo();
            var snapshot = new SystemSnapshot();

            // 1. CPU
            snapshot.Cpu.Name = _cachedCpuName;
            snapshot.Cpu.Cores = _cachedCpuCores;
            snapshot.Cpu.Threads = _cachedCpuThreads;
            if (_cpuCounter != null)
            {
                try { snapshot.Cpu.UsagePercent = Math.Round(_cpuCounter.NextValue(), 1); }
                catch { }
            }
            snapshot.Cpu.ClockGhz = QueryCpuClock();
            snapshot.Cpu.TemperatureC = QueryCpuTemperature();

            // 2. RAM (via ultra-fast GlobalMemoryStatusEx)
            var memStatus = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(memStatus))
            {
                snapshot.Ram.TotalGb = Math.Round(memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 1);
                snapshot.Ram.FreeGb = Math.Round(memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0), 1);
                snapshot.Ram.UsedGb = Math.Round(snapshot.Ram.TotalGb - snapshot.Ram.FreeGb, 1);
                snapshot.Ram.UsagePercent = memStatus.dwMemoryLoad;
            }
            snapshot.Ram.SpeedMhz = _cachedRamSpeed;
            snapshot.Ram.SlotsCount = _cachedRamSlots;

            // 3. GPU
            snapshot.Gpu.Name = _cachedGpuName;
            snapshot.Gpu.DriverVersion = _cachedGpuDriver;
            snapshot.Gpu.DedicatedVramMb = _cachedGpuVramMb;
            if (_gpuCounter != null)
            {
                try { snapshot.Gpu.UsagePercent = Math.Round(_gpuCounter.NextValue(), 1); }
                catch { }
            }

            // 4. Storage & Partitions
            try
            {
                if (_diskReadCounter != null)
                {
                    double bytesRead = _diskReadCounter.NextValue();
                    snapshot.Storage.ReadRateMbSec = Math.Round(bytesRead / (1024.0 * 1024.0), 2);
                }
                if (_diskWriteCounter != null)
                {
                    double bytesWrite = _diskWriteCounter.NextValue();
                    snapshot.Storage.WriteRateMbSec = Math.Round(bytesWrite / (1024.0 * 1024.0), 2);
                }
            }
            catch { }

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (var d in drives)
                {
                    if (d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    {
                        double total = Math.Round(d.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                        double free = Math.Round(d.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                        double used = total - free;
                        double pct = total > 0 ? Math.Round((used / total) * 100.0, 1) : 0;

                        snapshot.Storage.Partitions.Add(new DiskPartitionMetrics
                        {
                            Root = d.Name,
                            VolumeLabel = string.IsNullOrEmpty(d.VolumeLabel) ? d.Name : d.VolumeLabel,
                            TotalGb = total,
                            FreeGb = free,
                            UsagePercent = pct
                        });
                    }
                }
            }
            catch { }

            // SMART Status
            CheckSmartStatus(snapshot.Storage);

            return snapshot;
        }

        private double QueryCpuClock()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["CurrentClockSpeed"] != null)
                        {
                            double mhz = Convert.ToDouble(obj["CurrentClockSpeed"]);
                            return Math.Round(mhz / 1000.0, 2);
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        private double? QueryCpuTemperature()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["CurrentTemperature"] != null)
                        {
                            double kelvinTenths = Convert.ToDouble(obj["CurrentTemperature"]);
                            double celsius = (kelvinTenths - 2732.0) / 10.0;
                            if (celsius > 0 && celsius < 130)
                            {
                                return Math.Round(celsius, 1);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private void CheckSmartStatus(StorageMetrics storage)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT PredictFailure FROM MSStorageDriver_FailurePredictStatus"))
                {
                    bool failurePredicted = false;
                    int count = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        count++;
                        if (obj["PredictFailure"] != null && Convert.ToBoolean(obj["PredictFailure"]))
                        {
                            failurePredicted = true;
                            break;
                        }
                    }

                    if (count > 0)
                    {
                        storage.SmartHealthy = !failurePredicted;
                        storage.SmartStatusMessage = failurePredicted ? "SMART_PREDICT_FAIL" : "SMART_HEALTHY";
                        return;
                    }
                }
            }
            catch { }

            // Fallback to Win32_DiskDrive Status
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Status FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string st = obj["Status"] != null ? obj["Status"].ToString() : "";
                        if (string.Equals(st, "OK", StringComparison.OrdinalIgnoreCase))
                        {
                            storage.SmartHealthy = true;
                            storage.SmartStatusMessage = "SMART_HEALTHY";
                            return;
                        }
                    }
                }
            }
            catch { }

            storage.SmartHealthy = true;
            storage.SmartStatusMessage = "SMART_OK_DEFAULT";
        }

        public void Dispose()
        {
            if (_cpuCounter != null) { try { _cpuCounter.Dispose(); } catch { } _cpuCounter = null; }
            if (_diskReadCounter != null) { try { _diskReadCounter.Dispose(); } catch { } _diskReadCounter = null; }
            if (_diskWriteCounter != null) { try { _diskWriteCounter.Dispose(); } catch { } _diskWriteCounter = null; }
            if (_gpuCounter != null) { try { _gpuCounter.Dispose(); } catch { } _gpuCounter = null; }
        }
    }
}
