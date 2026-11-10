using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace OptiAssist.Addons.PCAssist.Core
{
    public class DisplayMonitorInfo
    {
        public string DeviceName { get; set; }
        public string AdapterName { get; set; }
        public string MonitorModel { get; set; }
        public string Manufacturer { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRateHz { get; set; }
        public int BitsPerPixel { get; set; }
        public bool IsPrimary { get; set; }
        public string ConnectionType { get; set; }

        public string Summary
        {
            get
            {
                return string.Format("{0} ({1}x{2} @ {3} Hz)", MonitorModel ?? "Generic Monitor", Width, Height, RefreshRateHz);
            }
        }
    }

    public class DisplayInfoService
    {
        public List<DisplayMonitorInfo> GetActiveDisplays()
        {
            var list = new List<DisplayMonitorInfo>();

            try
            {
                var d = new NativeMethods.DISPLAY_DEVICE();
                d.cb = Marshal.SizeOf(d);
                uint devNum = 0;

                while (NativeMethods.EnumDisplayDevices(null, devNum, ref d, 0))
                {
                    // Check if attached to desktop (StateFlags & 0x1)
                    if ((d.StateFlags & 1) != 0)
                    {
                        var dm = new NativeMethods.DEVMODE();
                        dm.dmSize = (short)Marshal.SizeOf(dm);
                        bool hasSettings = NativeMethods.EnumDisplaySettings(d.DeviceName, -1, ref dm);

                        // Query the monitor attached to this video output
                        var m = new NativeMethods.DISPLAY_DEVICE();
                        m.cb = Marshal.SizeOf(m);
                        NativeMethods.EnumDisplayDevices(d.DeviceName, 0, ref m, 0);

                        string monitorName = CleanMonitorString(m.DeviceString);
                        string pnpId = m.DeviceID ?? "";
                        string manufacturer = "";

                        // Parse EDID or Registry for true commercial model
                        string edidModel = QueryEdidModelFromRegistry(pnpId, out manufacturer);
                        if (!string.IsNullOrEmpty(edidModel))
                        {
                            monitorName = edidModel;
                        }

                        if (string.IsNullOrEmpty(monitorName)) monitorName = "Generic PnP Monitor";

                        list.Add(new DisplayMonitorInfo
                        {
                            DeviceName = d.DeviceName,
                            AdapterName = d.DeviceString,
                            MonitorModel = monitorName,
                            Manufacturer = manufacturer,
                            Width = hasSettings ? dm.dmPelsWidth : 1920,
                            Height = hasSettings ? dm.dmPelsHeight : 1080,
                            RefreshRateHz = hasSettings ? dm.dmDisplayFrequency : 60,
                            BitsPerPixel = hasSettings ? dm.dmBitsPerPel : 32,
                            IsPrimary = ((d.StateFlags & 4) != 0), // Primary device
                            ConnectionType = GuessConnectionType(d.DeviceName, pnpId)
                        });
                    }
                    devNum++;
                }
            }
            catch { }

            return list;
        }

        private static string CleanMonitorString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Trim();
        }

        private static string GuessConnectionType(string devName, string pnpId)
        {
            string pnpUpper = (pnpId ?? "").ToUpperInvariant();
            if (pnpUpper.Contains("INTERNAL") || pnpUpper.Contains("EDP")) return "Internal eDP";
            if (pnpUpper.Contains("HDMI")) return "HDMI";
            if (pnpUpper.Contains("DP") || pnpUpper.Contains("DISPLAYPORT")) return "DisplayPort";
            if (pnpUpper.Contains("DVI")) return "DVI";
            if (pnpUpper.Contains("VGA")) return "VGA";
            return "Digital";
        }

        private static string QueryEdidModelFromRegistry(string pnpId, out string manufacturer)
        {
            manufacturer = "";
            if (string.IsNullOrEmpty(pnpId)) return null;

            try
            {
                // Format: MONITOR\DEL40A3\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001
                string[] parts = pnpId.Split('\\');
                if (parts.Length < 2) return null;

                string monitorKey = parts[1]; // e.g. DEL40A3
                if (monitorKey.Length >= 3)
                {
                    string mfgCode = monitorKey.Substring(0, 3).ToUpperInvariant();
                    manufacturer = DecodeManufacturerCode(mfgCode);
                }

                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\" + monitorKey))
                {
                    if (baseKey != null)
                    {
                        foreach (string subName in baseKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = baseKey.OpenSubKey(subName + @"\Device Parameters"))
                            {
                                if (subKey != null)
                                {
                                    byte[] edid = subKey.GetValue("EDID") as byte[];
                                    if (edid != null && edid.Length >= 128)
                                    {
                                        string model = ParseEdidModel(edid);
                                        if (!string.IsNullOrEmpty(model))
                                        {
                                            return model;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static string ParseEdidModel(byte[] edid)
        {
            // VESA EDID descriptor blocks start at bytes 54, 72, 90, 108
            int[] descriptorOffsets = new int[] { 54, 72, 90, 108 };
            foreach (int offset in descriptorOffsets)
            {
                if (offset + 18 <= edid.Length)
                {
                    // Block flag: 0x00, 0x00, 0x00 and type 0xFC (Monitor Name)
                    if (edid[offset] == 0x00 && edid[offset + 1] == 0x00 && edid[offset + 2] == 0x00 && edid[offset + 3] == 0xFC)
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = offset + 5; i < offset + 18; i++)
                        {
                            byte b = edid[i];
                            if (b == 0x0A || b == 0x00) break;
                            if (b >= 32 && b <= 126) sb.Append((char)b);
                        }
                        string res = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(res)) return res;
                    }
                }
            }
            return null;
        }

        private static string DecodeManufacturerCode(string code)
        {
            switch (code)
            {
                case "DEL": return "Dell";
                case "SAM": return "Samsung";
                case "LGD":
                case "GSM": return "LG";
                case "ACR": return "Acer";
                case "ASU": return "ASUS";
                case "AOC": return "AOC";
                case "BNQ": return "BenQ";
                case "HPN":
                case "HWP": return "HP";
                case "LEN": return "Lenovo";
                case "MSI": return "MSI";
                case "PHL": return "Philips";
                case "SNY": return "Sony";
                case "VSC": return "ViewSonic";
                default: return code;
            }
        }
    }
}
