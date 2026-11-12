using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace OptiAssist.Addons.PCAssist.Core
{
    public enum BatteryHealthGrade
    {
        DesktopNoBattery,
        Excellent,
        Good,
        ModerateWear,
        DegradedReplaceSoon
    }

    public class BatteryReport
    {
        public bool HasBattery { get; set; }
        public bool IsAcConnected { get; set; }
        public bool IsCharging { get; set; }
        public bool IsDischarging { get; set; }
        public int ChargePercent { get; set; }

        public long DesignCapacityMwh { get; set; }
        public long FullChargeCapacityMwh { get; set; }
        public long RemainingCapacityMwh { get; set; }

        public double HealthPercent { get; set; }
        public double WearPercent { get; set; }
        public double VoltageVolts { get; set; }
        public double RateWatts { get; set; }
        public int EstimatedRuntimeMinutes { get; set; }
        public int? CycleCount { get; set; }

        public string Manufacturer { get; set; }
        public string DeviceName { get; set; }
        public string SerialNumber { get; set; }
        public string Chemistry { get; set; }

        public BatteryHealthGrade Grade { get; set; }
        public string DiagnosticSummary { get; set; }
    }

    public class BatteryDiagnosticService
    {
        public BatteryReport AnalyzeBattery()
        {
            var report = new BatteryReport();

            // 1. Initial check via Win32 GetSystemPowerStatus
            NativeMethods.SYSTEM_POWER_STATUS powerStatus;
            bool hasApiPower = NativeMethods.GetSystemPowerStatus(out powerStatus);

            if (hasApiPower)
            {
                report.IsAcConnected = (powerStatus.ACLineStatus == 1);
                report.ChargePercent = (powerStatus.BatteryLifePercent <= 100) ? powerStatus.BatteryLifePercent : 0;
                report.IsCharging = ((powerStatus.BatteryFlag & 8) != 0);

                if (powerStatus.BatteryFlag == 128 || powerStatus.BatteryFlag == 255)
                {
                    report.HasBattery = false;
                    report.Grade = BatteryHealthGrade.DesktopNoBattery;
                    report.DiagnosticSummary = "Desktop PC or No Battery Installed (Direct AC Power)";
                    return report;
                }
            }

            // 2. Query WMI root\wmi for Deep ACPI Battery Details
            bool foundWmiBattery = false;
            try
            {
                // BatteryStaticData: DesignCapacity, DeviceName, ManufactureName, SerialNumber, Chemistry
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryStaticData"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        foundWmiBattery = true;
                        if (obj["DesignedCapacity"] != null)
                        {
                            long cap = 0;
                            long.TryParse(obj["DesignedCapacity"].ToString(), out cap);
                            report.DesignCapacityMwh = cap;
                        }

                        report.DeviceName = CleanWmiString(obj["DeviceName"]);
                        report.Manufacturer = CleanWmiString(obj["ManufactureName"]);
                        report.SerialNumber = CleanWmiString(obj["SerialNumber"]);
                        report.Chemistry = CleanWmiString(obj["Chemistry"]);
                        break;
                    }
                }

                // BatteryFullChargedCapacity: FullChargedCapacity
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryFullChargedCapacity"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["FullChargedCapacity"] != null)
                        {
                            long full = 0;
                            long.TryParse(obj["FullChargedCapacity"].ToString(), out full);
                            report.FullChargeCapacityMwh = full;
                            break;
                        }
                    }
                }

                // BatteryStatus: RemainingCapacity, Voltage, DischargeRate, Charging, Discharging
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryStatus"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["RemainingCapacity"] != null)
                        {
                            long rem = 0;
                            long.TryParse(obj["RemainingCapacity"].ToString(), out rem);
                            report.RemainingCapacityMwh = rem;
                        }

                        if (obj["Voltage"] != null)
                        {
                            double mv = 0;
                            double.TryParse(obj["Voltage"].ToString(), out mv);
                            report.VoltageVolts = Math.Round(mv / 1000.0, 2);
                        }

                        if (obj["DischargeRate"] != null)
                        {
                            double mw = 0;
                            double.TryParse(obj["DischargeRate"].ToString(), out mw);
                            report.RateWatts = Math.Round(Math.Abs(mw) / 1000.0, 2);
                        }

                        if (obj["Charging"] != null) report.IsCharging = Convert.ToBoolean(obj["Charging"]);
                        if (obj["Discharging"] != null) report.IsDischarging = Convert.ToBoolean(obj["Discharging"]);
                        if (obj["PowerOnline"] != null) report.IsAcConnected = Convert.ToBoolean(obj["PowerOnline"]);
                        break;
                    }
                }

                // BatteryCycleCount
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryCycleCount"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["CycleCount"] != null)
                        {
                            int cycles = 0;
                            if (int.TryParse(obj["CycleCount"].ToString(), out cycles))
                            {
                                report.CycleCount = cycles;
                            }
                            break;
                        }
                    }
                }
            }
            catch { }

            // 3. Fallback to standard Win32_Battery if WMI root\wmi returned zero or wasn't accessible
            if (!foundWmiBattery || report.DesignCapacityMwh == 0)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            foundWmiBattery = true;
                            if (report.DesignCapacityMwh == 0 && obj["DesignCapacity"] != null)
                            {
                                long d = 0;
                                long.TryParse(obj["DesignCapacity"].ToString(), out d);
                                report.DesignCapacityMwh = d;
                            }

                            if (report.FullChargeCapacityMwh == 0 && obj["FullChargeCapacity"] != null)
                            {
                                long f = 0;
                                long.TryParse(obj["FullChargeCapacity"].ToString(), out f);
                                report.FullChargeCapacityMwh = f;
                            }

                            if (string.IsNullOrEmpty(report.DeviceName) && obj["Name"] != null)
                            {
                                report.DeviceName = obj["Name"].ToString().Trim();
                            }

                            if (string.IsNullOrEmpty(report.Chemistry) && obj["Chemistry"] != null)
                            {
                                report.Chemistry = obj["Chemistry"].ToString().Trim();
                            }

                            if (report.EstimatedRuntimeMinutes == 0 && obj["EstimatedRunTime"] != null)
                            {
                                int mins = 0;
                                if (int.TryParse(obj["EstimatedRunTime"].ToString(), out mins) && mins > 0 && mins < 71582)
                                {
                                    report.EstimatedRuntimeMinutes = mins;
                                }
                            }
                            break;
                        }
                    }
                }
                catch { }
            }

            report.HasBattery = foundWmiBattery && (report.DesignCapacityMwh > 0 || report.ChargePercent > 0 || IsMobileFormFactor());
            if (!report.HasBattery)
            {
                report.Grade = BatteryHealthGrade.DesktopNoBattery;
                report.DiagnosticSummary = "Desktop PC (No Battery Installed)";
                return report;
            }

            // 4. Calculate Health & Wear Level
            if (report.DesignCapacityMwh > 0 && report.FullChargeCapacityMwh > 0)
            {
                double health = ((double)report.FullChargeCapacityMwh / (double)report.DesignCapacityMwh) * 100.0;
                report.HealthPercent = Math.Round(Math.Min(100.0, health), 1);
                report.WearPercent = Math.Round(Math.Max(0.0, 100.0 - report.HealthPercent), 1);
            }
            else if (report.ChargePercent > 0)
            {
                // Partial estimate if firmware hides design capacity
                report.HealthPercent = 100.0;
                report.WearPercent = 0.0;
            }

            // 5. Calculate Runtime Autonomy
            if (report.EstimatedRuntimeMinutes == 0 && report.RateWatts > 0 && report.RemainingCapacityMwh > 0)
            {
                double remainingWh = report.RemainingCapacityMwh / 1000.0;
                double hours = remainingWh / report.RateWatts;
                report.EstimatedRuntimeMinutes = (int)Math.Round(hours * 60.0);
            }

            // 6. Technical Grading
            if (report.HealthPercent >= 85.0)
            {
                report.Grade = BatteryHealthGrade.Excellent;
                report.DiagnosticSummary = "Excellent Battery Health. Minimal cell wear.";
            }
            else if (report.HealthPercent >= 70.0)
            {
                report.Grade = BatteryHealthGrade.Good;
                report.DiagnosticSummary = "Good Battery Condition. Normal expected usage wear.";
            }
            else if (report.HealthPercent >= 50.0)
            {
                report.Grade = BatteryHealthGrade.ModerateWear;
                report.DiagnosticSummary = "Moderate Wear. Reduced autonomy under high loads.";
            }
            else
            {
                report.Grade = BatteryHealthGrade.DegradedReplaceSoon;
                report.DiagnosticSummary = "Degraded Battery. Replacement recommended due to severe capacity loss.";
            }

            return report;
        }

        private static string CleanWmiString(object val)
        {
            if (val == null) return "";
            string s = val.ToString().Trim();
            if (string.Equals(s, "To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "System Serial Number", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return s;
        }

        public string GenerateOfficialBatteryReport()
        {
            try
            {
                string outputPath = Path.Combine(Path.GetTempPath(), "optiassist_battery_report.html");
                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = string.Format("/batteryreport /output \"{0}\"", outputPath),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit(15000);
                    }
                }

                if (File.Exists(outputPath))
                {
                    Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                    return outputPath;
                }
            }
            catch { }

            return null;
        }

        public static bool IsMobileFormFactor()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        ushort[] types = obj["ChassisTypes"] as ushort[];
                        if (types != null)
                        {
                            foreach (ushort t in types)
                            {
                                // 8: Portable, 9: Laptop, 10: Notebook, 11: Handheld, 12: Docking, 14: SubNotebook, 30: Tablet, 31: Convertible, 32: Detachable
                                if (t == 8 || t == 9 || t == 10 || t == 11 || t == 12 || t == 14 || t == 30 || t == 31 || t == 32)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
