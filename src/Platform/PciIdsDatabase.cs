using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace OptiAssist.Platform
{
    public class PciLookupResult
    {
        public string VendorId { get; set; }
        public string DeviceId { get; set; }
        public string VendorName { get; set; }
        public string DeviceName { get; set; }
        public string ArchitectureFamily { get; set; }
        public string MarketingSeries { get; set; }
        public bool IsDiscreteGpu { get; set; }
        public bool IsIntegratedGpu { get; set; }

        public PciLookupResult()
        {
            VendorId = "";
            DeviceId = "";
            VendorName = "";
            DeviceName = "";
            ArchitectureFamily = "";
            MarketingSeries = "";
        }
    }

    public class PciIdsDatabase
    {
        private readonly Dictionary<string, string> _vendors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _devices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static PciIdsDatabase _defaultInstance;
        private static readonly object _lock = new object();

        public static PciIdsDatabase Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    lock (_lock)
                    {
                        if (_defaultInstance == null)
                        {
                            _defaultInstance = LoadDefault();
                        }
                    }
                }
                return _defaultInstance;
            }
        }

        public static PciIdsDatabase LoadDefault()
        {
            string path = GetDatabaseFilePath();
            if (File.Exists(path))
            {
                try
                {
                    return ParseFromFile(path);
                }
                catch { }
            }
            return new PciIdsDatabase();
        }

        public static string GetDatabaseFilePath()
        {
            try
            {
                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    string candidate = Path.Combine(asmDir, @"Config\Data\pci.ids.min");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, @"Config\Data\pci.ids.min");
        }

        public static PciIdsDatabase ParseFromFile(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                return ParseFromReader(sr);
            }
        }

        public static PciIdsDatabase ParseFromReader(TextReader reader)
        {
            PciIdsDatabase db = new PciIdsDatabase();
            string line;
            string currentVendor = "";

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#")) continue;

                if (line.StartsWith("\t\t"))
                {
                    // Subsystem line (ignore for now)
                    continue;
                }
                else if (line.StartsWith("\t"))
                {
                    // Device line: "\t1f07  TU106 [GeForce RTX 2070]"
                    string clean = line.Substring(1).Trim();
                    if (clean.Length >= 4 && !string.IsNullOrEmpty(currentVendor))
                    {
                        string devId = clean.Substring(0, 4).Trim().ToLowerInvariant();
                        string name = clean.Length > 4 ? clean.Substring(4).Trim() : "";
                        string key = currentVendor + ":" + devId;
                        db._devices[key] = name;
                    }
                }
                else
                {
                    // Vendor line: "10de  NVIDIA Corporation"
                    string clean = line.Trim();
                    if (clean.Length >= 4)
                    {
                        string vId = clean.Substring(0, 4).Trim().ToLowerInvariant();
                        string vName = clean.Length > 4 ? clean.Substring(4).Trim() : "";
                        currentVendor = vId;
                        db._vendors[vId] = vName;
                    }
                }
            }

            return db;
        }

        public PciLookupResult Lookup(string vendorId, string deviceId)
        {
            PciLookupResult res = new PciLookupResult();
            if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(deviceId)) return res;

            string v = vendorId.Trim().ToLowerInvariant().PadLeft(4, '0');
            string d = deviceId.Trim().ToLowerInvariant().PadLeft(4, '0');
            if (v.Length > 4) v = v.Substring(v.Length - 4);
            if (d.Length > 4) d = d.Substring(d.Length - 4);

            res.VendorId = v;
            res.DeviceId = d;

            string vName;
            if (_vendors.TryGetValue(v, out vName))
            {
                res.VendorName = vName;
            }

            string key = v + ":" + d;
            string dName;
            if (_devices.TryGetValue(key, out dName))
            {
                res.DeviceName = dName;
            }

            // GPU Classification and Architecture Family derivation
            ClassifyGpuArchitecture(res);
            return res;
        }

        private static void ClassifyGpuArchitecture(PciLookupResult res)
        {
            string v = res.VendorId;
            string name = (res.DeviceName ?? "").ToUpperInvariant();

            // NVIDIA (10de)
            if (v == "10de")
            {
                if (name.IndexOf("GB2", StringComparison.Ordinal) >= 0 || name.IndexOf("RTX 50", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Blackwell";
                    res.MarketingSeries = "GeForce RTX 50 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("AD1", StringComparison.Ordinal) >= 0 || name.IndexOf("RTX 40", StringComparison.Ordinal) >= 0 || name.IndexOf("ADA GENERATION", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Ada Lovelace";
                    res.MarketingSeries = "GeForce RTX 40 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("GA1", StringComparison.Ordinal) >= 0 || name.IndexOf("RTX 30", StringComparison.Ordinal) >= 0 || name.IndexOf("RTX A", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Ampere";
                    res.MarketingSeries = "GeForce RTX 30 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("TU11", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 16", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Turing";
                    res.MarketingSeries = "GeForce GTX 16 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("TU10", StringComparison.Ordinal) >= 0 || name.IndexOf("RTX 20", StringComparison.Ordinal) >= 0 || name.IndexOf("QUADRO RTX", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Turing";
                    res.MarketingSeries = "GeForce RTX 20 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("GP1", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 10", StringComparison.Ordinal) >= 0 || name.IndexOf("GT 1030", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Pascal";
                    res.MarketingSeries = "GeForce GTX 10 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("GM1", StringComparison.Ordinal) >= 0 || name.IndexOf("GM2", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 9", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 750", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Maxwell";
                    res.MarketingSeries = "GeForce GTX 900 / 750 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("GK1", StringComparison.Ordinal) >= 0 || name.IndexOf("GK2", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 6", StringComparison.Ordinal) >= 0 || name.IndexOf("GTX 7", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Kepler";
                    res.MarketingSeries = "GeForce GTX 600 / 700 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("QUADRO", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Quadro Workstation";
                    res.MarketingSeries = "NVIDIA Quadro";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("GEFORCE", StringComparison.Ordinal) >= 0)
                {
                    res.IsDiscreteGpu = true;
                }
                return;
            }

            // AMD / ATI (1002)
            if (v == "1002")
            {
                if (name.IndexOf("NAVI 4", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "RDNA 4";
                    res.MarketingSeries = "Radeon RX 8000 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("NAVI 3", StringComparison.Ordinal) >= 0 || name.IndexOf("RX 7", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "RDNA 3";
                    res.MarketingSeries = "Radeon RX 7000 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("NAVI 2", StringComparison.Ordinal) >= 0 || name.IndexOf("RX 6", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "RDNA 2";
                    res.MarketingSeries = "Radeon RX 6000 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("NAVI 1", StringComparison.Ordinal) >= 0 || name.IndexOf("RX 5", StringComparison.Ordinal) >= 0 && name.IndexOf("5700", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "RDNA 1";
                    res.MarketingSeries = "Radeon RX 5000 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("CEDAR", StringComparison.Ordinal) >= 0 || name.IndexOf("REDWOOD", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("JUNIPER", StringComparison.Ordinal) >= 0 || name.IndexOf("CYPRESS", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("HEMLOCK", StringComparison.Ordinal) >= 0 || name.IndexOf("TURKS", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("CAICOS", StringComparison.Ordinal) >= 0 || name.IndexOf("BARTS", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("CAYMAN", StringComparison.Ordinal) >= 0 || name.IndexOf("HD 5", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("HD 6", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "TeraScale";
                    res.MarketingSeries = "Radeon HD 5000 / 6000 Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("VEGA", StringComparison.Ordinal) >= 0 || name.IndexOf("POLARIS", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("ELLESMERE", StringComparison.Ordinal) >= 0 || name.IndexOf("BAFFIN", StringComparison.Ordinal) >= 0 ||
                         name.IndexOf("TAHITI", StringComparison.Ordinal) >= 0 || name.IndexOf("PITCAIRN", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "GCN";
                    res.MarketingSeries = "Radeon RX 400/500/Vega/GCN";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("RADEON PRO", StringComparison.Ordinal) >= 0 || name.IndexOf("FIREPRO", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Radeon Pro Workstation";
                    res.MarketingSeries = "Radeon Pro";
                    res.IsDiscreteGpu = true;
                }
                return;
            }

            // Intel (8086)
            if (v == "8086")
            {
                if (name.IndexOf("ARC", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Arc Alchemist";
                    res.MarketingSeries = "Intel Arc Series";
                    res.IsDiscreteGpu = true;
                }
                else if (name.IndexOf("IRIS", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Gen11/Xe";
                    res.MarketingSeries = "Intel Iris / Iris Xe";
                    res.IsIntegratedGpu = true;
                }
                else if (name.IndexOf("HD GRAPHICS", StringComparison.Ordinal) >= 0 || name.IndexOf("UHD GRAPHICS", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Intel HD/UHD";
                    res.MarketingSeries = "Intel HD Graphics";
                    res.IsIntegratedGpu = true;
                }
                else if (name.IndexOf("GMA", StringComparison.Ordinal) >= 0)
                {
                    res.ArchitectureFamily = "Legacy GMA";
                    res.MarketingSeries = "Intel GMA";
                    res.IsIntegratedGpu = true;
                }
                return;
            }
        }
    }
}
