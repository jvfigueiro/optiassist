using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;
using OptiAssist.Platform;

namespace OptiAssist.Debloater
{
    public class DebloaterEngine
    {
        private readonly IOptiAssistHost _host;

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

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        public DebloaterEngine()
            : this(null)
        {
        }

        public DebloaterEngine(IOptiAssistHost host)
        {
            _host = host;
        }

        #region SYSTEM & PLATFORM INSPECTION

        public static int GetWindowsBuildNumber()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        string buildStr = Convert.ToString(key.GetValue("CurrentBuildNumber", key.GetValue("CurrentBuild", "0"))).Trim();
                        int b;
                        if (int.TryParse(buildStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b > 0)
                        {
                            return b;
                        }
                    }
                }
            }
            catch { }
            return Environment.OSVersion.Version.Build;
        }

        public static string GetWindowsEditionId()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        string ed = Convert.ToString(key.GetValue("EditionID", "")).Trim();
                        if (!string.IsNullOrEmpty(ed)) return ed;
                    }
                }
            }
            catch { }
            return "Professional";
        }

        public static bool IsEnterpriseOrIoTEdition()
        {
            string ed = GetWindowsEditionId();
            if (string.IsNullOrEmpty(ed)) return false;
            return ed.IndexOf("Enterprise", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ed.IndexOf("Education", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ed.IndexOf("IoT", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GetEditionAwareTelemetryValue()
        {
            return IsEnterpriseOrIoTEdition() ? "0" : "1";
        }

        public bool IsLaptopSystem()
        {
            try
            {
                SYSTEM_POWER_STATUS sps;
                if (GetSystemPowerStatus(out sps))
                {
                    if (sps.BatteryFlag != 128 && sps.BatteryFlag != 255)
                    {
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                using (RegistryKey biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false))
                {
                    if (biosKey != null)
                    {
                        string prod = Convert.ToString(biosKey.GetValue("SystemProductName", "")).ToLowerInvariant();
                        string fam = Convert.ToString(biosKey.GetValue("SystemFamily", "")).ToLowerInvariant();
                        string combined = prod + " " + fam;
                        string[] mobileHints = new string[]
                        {
                            "laptop", "notebook", "thinkpad", "ideapad", "latitude", "inspiron",
                            "vostro", "xps", "pavilion", "elitebook", "probook", "aspire",
                            "nitro", "zenbook", "vivobook", "macbook", "surface"
                        };
                        for (int i = 0; i < mobileHints.Length; i++)
                        {
                            if (combined.IndexOf(mobileHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public string GetSystemOemManufacturer()
        {
            try
            {
                using (RegistryKey biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false))
                {
                    if (biosKey != null)
                    {
                        string mfg = Convert.ToString(biosKey.GetValue("SystemManufacturer", "")).Trim();
                        string boardMfg = Convert.ToString(biosKey.GetValue("BaseBoardManufacturer", "")).Trim();
                        string prod = Convert.ToString(biosKey.GetValue("SystemProductName", "")).Trim();
                        if (!string.IsNullOrEmpty(mfg) && !string.Equals(mfg, "System manufacturer", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(prod) && !string.Equals(prod, "System Product Name", StringComparison.OrdinalIgnoreCase))
                            {
                                return mfg + " (" + prod + ")";
                            }
                            return mfg;
                        }
                        if (!string.IsNullOrEmpty(boardMfg))
                        {
                            return boardMfg;
                        }
                    }
                }
            }
            catch { }

            if (_host != null && _host.Hardware != null && !string.IsNullOrWhiteSpace(_host.Hardware.Motherboard))
            {
                return _host.Hardware.Motherboard;
            }
            return "Generic / Custom PC";
        }

        public static List<string> GetInteractiveUserSids()
        {
            List<string> sids = new List<string>();
            try
            {
                using (RegistryKey logonKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI", false))
                {
                    if (logonKey != null)
                    {
                        string lastSid = Convert.ToString(logonKey.GetValue("LastLoggedOnUserSID", "")).Trim();
                        if (!string.IsNullOrEmpty(lastSid) && lastSid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase))
                        {
                            using (RegistryKey userHive = Registry.Users.OpenSubKey(lastSid, false))
                            {
                                if (userHive != null)
                                {
                                    sids.Add(lastSid);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                string[] subKeys = Registry.Users.GetSubKeyNames();
                if (subKeys != null)
                {
                    for (int i = 0; i < subKeys.Length; i++)
                    {
                        string sk = subKeys[i];
                        if (!string.IsNullOrEmpty(sk) &&
                            sk.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase) &&
                            !sk.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!sids.Contains(sk))
                            {
                                sids.Add(sk);
                            }
                        }
                    }
                }
            }
            catch { }

            if (sids.Count == 0)
            {
                try
                {
                    WindowsIdentity id = WindowsIdentity.GetCurrent();
                    if (id != null && id.User != null)
                    {
                        string curSid = id.User.Value;
                        if (!string.IsNullOrEmpty(curSid) && curSid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase))
                        {
                            sids.Add(curSid);
                        }
                    }
                }
                catch { }
            }

            return sids;
        }

        public static bool IsEdgeInstalled()
        {
            try
            {
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(pf86) && File.Exists(Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe")))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(pf) && File.Exists(Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe")))
                {
                    return true;
                }
                using (RegistryKey appPath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", false))
                {
                    if (appPath != null) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool IsBraveInstalled()
        {
            try
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(pf) && File.Exists(Path.Combine(pf, @"BraveSoftware\Brave-Browser\Application\brave.exe")))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(pf86) && File.Exists(Path.Combine(pf86, @"BraveSoftware\Brave-Browser\Application\brave.exe")))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(localApp) && File.Exists(Path.Combine(localApp, @"BraveSoftware\Brave-Browser\Application\brave.exe")))
                {
                    return true;
                }
                using (RegistryKey appPath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\brave.exe", false))
                {
                    if (appPath != null) return true;
                }
                List<string> sids = GetInteractiveUserSids();
                for (int i = 0; i < sids.Count; i++)
                {
                    using (RegistryKey uKey = Registry.Users.OpenSubKey(sids[i] + @"\Software\BraveSoftware\Brave-Browser", false))
                    {
                        if (uKey != null) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool IsRequirementInstalled(string req)
        {
            if (string.IsNullOrWhiteSpace(req)) return true;
            string r = req.Trim().ToLowerInvariant();
            if (r == "edge" || r == "msedge") return IsEdgeInstalled();
            if (r == "brave") return IsBraveInstalled();
            return true;
        }

        #endregion

        #region GATING & STATE DETECTION

        public static bool IsBuildInRange(int minBuild, int maxBuild, int currentBuild)
        {
            if (minBuild > 0 && currentBuild < minBuild) return false;
            if (maxBuild > 0 && currentBuild > maxBuild) return false;
            return true;
        }

        public bool EvaluateGating(DebloatTweakDefinition tweak, out string reason)
        {
            reason = "";
            if (tweak == null)
            {
                reason = "Null tweak";
                return false;
            }

            DebloatAppliesTo gate = tweak.appliesTo;
            if (gate == null) return true;

            int build = GetWindowsBuildNumber();
            if (!IsBuildInRange(gate.minBuild, gate.maxBuild, build))
            {
                reason = string.Format("OS Build {0} outside supported range [{1}..{2}]", build, gate.minBuild, gate.maxBuild);
                return false;
            }

            if (gate.editions != null && gate.editions.Count > 0 && !gate.editions.Contains("*"))
            {
                string curEd = GetWindowsEditionId();
                bool matchEd = false;
                for (int i = 0; i < gate.editions.Count; i++)
                {
                    if (curEd.IndexOf(gate.editions[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchEd = true;
                        break;
                    }
                }
                if (!matchEd)
                {
                    reason = "Windows Edition '" + curEd + "' not in target editions";
                    return false;
                }
            }

            if (gate.formFactors != null && gate.formFactors.Count > 0 && !gate.formFactors.Contains("*"))
            {
                string curFf = GetCurrentFormFactorToken();
                bool matchFf = false;
                for (int i = 0; i < gate.formFactors.Count; i++)
                {
                    string targetFf = gate.formFactors[i];
                    if (string.Equals(targetFf, curFf, StringComparison.OrdinalIgnoreCase))
                    {
                        matchFf = true;
                        break;
                    }
                    // Ultraportable also matches generic "laptop" formFactor when not explicitly excluded
                    if (string.Equals(curFf, "ultraportable", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(targetFf, "laptop", StringComparison.OrdinalIgnoreCase))
                    {
                        matchFf = true;
                        break;
                    }
                }
                if (!matchFf)
                {
                    reason = "Form factor '" + curFf + "' not in target form factors";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(gate.requiresInstalled) && !IsRequirementInstalled(gate.requiresInstalled))
            {
                reason = "Required application '" + gate.requiresInstalled + "' is not installed";
                return false;
            }

            // Reset runtime condition annotations before evaluating condition
            tweak.HardwareConditionBadge = "";
            tweak.HardwareConditionReason = "";
            tweak.QuickActionUri = "";
            tweak.QuickActionLabel = "";
            tweak.ForceUncheckedByCondition = false;

            if (!string.IsNullOrWhiteSpace(gate.condition))
            {
                if (!EvaluateHardwareCondition(tweak, gate.condition, out reason))
                {
                    return false;
                }
            }

            return true;
        }

        public PlatformInfo GetCachedPlatformInfo()
        {
            if (_host != null && _host.PlatformDiagnostic != null && _host.PlatformDiagnostic.Platform != null)
            {
                return _host.PlatformDiagnostic.Platform;
            }
            PlatformDiagnosticSnapshot snap = PlatformDiagnosticService.Instance.GetSnapshot(false, null, null);
            return snap != null ? snap.Platform : null;
        }

        private string GetCurrentFormFactorToken()
        {
            PlatformInfo p = GetCachedPlatformInfo();
            if (p != null)
            {
                if (p.FormFactor == ChassisFormFactor.Ultraportable) return "ultraportable";
                if (p.FormFactor == ChassisFormFactor.Laptop || p.HasBattery) return "laptop";
                return "desktop";
            }
            return IsLaptopSystem() ? "laptop" : "desktop";
        }

        private bool EvaluateHardwareCondition(DebloatTweakDefinition tweak, string condition, out string blockReason)
        {
            blockReason = "";
            string cond = (condition ?? "").Trim().ToLowerInvariant();
            PlatformInfo platform = GetCachedPlatformInfo();

            if (cond == "ssd_only")
            {
                string media = (platform != null && platform.Storage != null) ? (platform.Storage.SystemDiskMediaType ?? "") : "";
                bool isEmmc = (platform != null && platform.Storage != null && platform.Storage.IsSystemEmmcOrSd);
                bool isSsd = string.Equals(media, "SSD", StringComparison.OrdinalIgnoreCase) && !isEmmc;
                if (!isSsd)
                {
                    string diskLabel = isEmmc ? "eMMC/SD" : (string.IsNullOrEmpty(media) ? "HDD/Unknown" : media);
                    tweak.HardwareConditionBadge = "Preserved (" + diskLabel + ")";
                    tweak.HardwareConditionReason = "SysMain kept active because the system drive is " + diskLabel + " (disabled only on SSD/NVMe).";
                    blockReason = tweak.HardwareConditionReason;
                    return false;
                }
                tweak.HardwareConditionBadge = "SSD/NVMe";
                tweak.HardwareConditionReason = "System drive identified as SSD/NVMe — safe to disable SysMain.";
                return true;
            }

            if (cond == "transparency_auto")
            {
                int cpuYear = (platform != null && platform.Cpu != null) ? platform.Cpu.ReleaseYear : 0;
                int biosYear = (platform != null && platform.Oem != null) ? platform.Oem.BiosReleaseYear : 0;
                int effectiveYear = (cpuYear > 0) ? cpuYear : biosYear;
                bool legacyGpu = IsLegacyOrProblematicIgpu(platform);
                bool isUltra = (platform != null && platform.FormFactor == ChassisFormFactor.Ultraportable);

                if (isUltra)
                {
                    tweak.HardwareConditionBadge = "Auto (Ultraportable)";
                    tweak.HardwareConditionReason = "Ultraportable profile: transparency automatically disabled to reduce GPU/battery usage.";
                    return true;
                }
                if (legacyGpu)
                {
                    tweak.HardwareConditionBadge = "Auto (Legacy iGPU)";
                    tweak.HardwareConditionReason = "Legacy/problematic iGPU detected (HD 2000/2500/3000, GMA, or nForce): transparency automatically disabled.";
                    return true;
                }
                if (effectiveYear > 0 && effectiveYear < 2019)
                {
                    tweak.HardwareConditionBadge = "Auto (< 2019)";
                    tweak.HardwareConditionReason = string.Format("Pre-2019 hardware (base year: {0}): transparency automatically disabled.", effectiveYear);
                    return true;
                }

                tweak.ForceUncheckedByCondition = true;
                tweak.HardwareConditionBadge = "Kept On (>= 2019)";
                tweak.HardwareConditionReason = effectiveYear > 0
                    ? string.Format("CPU/BIOS from {0} (>= 2019) with no problematic iGPU: transparency kept enabled by default (manual opt-in).", effectiveYear)
                    : "Modern hardware (>= 2019): transparency kept enabled by default (manual opt-in).";
                return true;
            }

            if (cond == "compact_os_eligible")
            {
                bool hasSse42 = (platform == null || platform.Cpu == null) ? true : platform.Cpu.HasSse42;
                if (!hasSse42)
                {
                    blockReason = "CPU lacks SSE4.2 instruction set — CompactOS blocked to prevent severe CPU overhead.";
                    tweak.HardwareConditionBadge = "No SSE4.2";
                    tweak.HardwareConditionReason = blockReason;
                    return false;
                }

                double freeGb = GetSystemDriveFreeGb();
                if (freeGb >= 15.0)
                {
                    tweak.ForceUncheckedByCondition = true;
                    tweak.HardwareConditionBadge = string.Format("Free: {0:F1} GB (>=15 GB)", freeGb);
                    tweak.HardwareConditionReason = string.Format("CompactOS recommended only when < 15 GB free space with SSE4.2 (current: {0:F1} GB free). Manual opt-in with confirmation.", freeGb);
                    return true;
                }

                tweak.HardwareConditionBadge = string.Format("Eligible ({0:F1} GB free)", freeGb);
                tweak.HardwareConditionReason = string.Format("CPU has SSE4.2 and only {0:F1} GB free on system drive (< 15 GB). Requires confirmation.", freeGb);
                return true;
            }

            if (cond == "no_ir_camera")
            {
                bool hasIr = HasIrCameraDevice(platform);
                if (hasIr)
                {
                    tweak.ForceUncheckedByCondition = true;
                    tweak.HardwareConditionBadge = "IR Camera Detected";
                    tweak.HardwareConditionReason = "Infrared camera (Windows Hello) detected — kept unchecked by default (manual opt-in).";
                    return true;
                }
                tweak.HardwareConditionBadge = "No IR Camera";
                tweak.HardwareConditionReason = "No IR camera detected — safe to remove Windows Hello Face capability.";
                return true;
            }

            if (cond == "logitech_hardware")
            {
                if (!HasLogitechHardware(platform))
                {
                    blockReason = "No Logitech peripheral (VID_046D) detected on this computer.";
                    return false;
                }
                tweak.HardwareConditionBadge = "Logitech Hardware";
                tweak.HardwareConditionReason = "Logitech device detected — blocking Logitech Download Assistant enabled.";
                return true;
            }

            if (cond == "onedrive_safe_removal")
            {
                string odDetails;
                bool safe = CheckOneDriveSafeRemoval(out odDetails);
                if (!safe)
                {
                    tweak.ForceUncheckedByCondition = true;
                    tweak.HardwareConditionBadge = "OneDrive Protected";
                    tweak.HardwareConditionReason = "Automatic removal blocked for safety: " + odDetails;
                    return true;
                }
                tweak.HardwareConditionBadge = "OneDrive Eligible";
                tweak.HardwareConditionReason = "Safety criteria met: " + odDetails;
                return true;
            }

            if (cond == "tamper_protection_best_effort")
            {
                bool tamper = (platform != null && platform.OsFirmware != null && platform.OsFirmware.IsTamperProtectionActive);
                if (tamper)
                {
                    tweak.HardwareConditionBadge = "Tamper Protection Active";
                    tweak.HardwareConditionReason = "Best-effort mode: CPU/scan policies (AvgCPULoadFactor=5, LowCpuPriority=1, catch-up) will be applied; cloud/sample settings require manual action in Windows Security.";
                    tweak.QuickActionUri = "windowsdefender://threatsettings/";
                    tweak.QuickActionLabel = "🛡️ Open Defender";
                }
                else
                {
                    tweak.HardwareConditionBadge = "Tamper Protection Off";
                    tweak.HardwareConditionReason = "Tamper Protection inactive: all Windows Defender policies can be applied directly.";
                }
                return true;
            }

            return true;
        }

        private static bool IsLegacyOrProblematicIgpu(PlatformInfo platform)
        {
            if (platform == null) return false;
            if (platform.Gpus != null)
            {
                for (int i = 0; i < platform.Gpus.Count; i++)
                {
                    GpuDeviceInfo g = platform.Gpus[i];
                    if (g == null) continue;
                    string n = (g.Name ?? "").ToUpperInvariant();
                    string arch = (g.ArchitectureFamily ?? "").ToUpperInvariant();
                    if (n.IndexOf("HD GRAPHICS 2000", StringComparison.Ordinal) >= 0 ||
                        n.IndexOf("HD GRAPHICS 2500", StringComparison.Ordinal) >= 0 ||
                        n.IndexOf("HD GRAPHICS 3000", StringComparison.Ordinal) >= 0 ||
                        n.IndexOf("GMA", StringComparison.Ordinal) >= 0 ||
                        n.IndexOf("NFORCE", StringComparison.Ordinal) >= 0 ||
                        arch.IndexOf("LEGACY GMA", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                    if (string.Equals(g.VendorId, "8086", StringComparison.OrdinalIgnoreCase) &&
                        (g.DeviceId == "0102" || g.DeviceId == "0106" || g.DeviceId == "0112" || g.DeviceId == "0116" || g.DeviceId == "0122" || g.DeviceId == "0126" || g.DeviceId == "0152" || g.DeviceId == "0156"))
                    {
                        return true;
                    }
                }
            }
            if (platform.PnpDevices != null)
            {
                for (int i = 0; i < platform.PnpDevices.Count; i++)
                {
                    PnpDeviceItem d = platform.PnpDevices[i];
                    if (d != null && d.Name != null && d.Name.IndexOf("nForce", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static double GetSystemDriveFreeGb()
        {
            try
            {
                string sysDrive = Environment.GetEnvironmentVariable("SystemDrive");
                if (string.IsNullOrEmpty(sysDrive)) sysDrive = "C:";
                DriveInfo di = new DriveInfo(sysDrive.Substring(0, 1));
                if (di.IsReady)
                {
                    return (double)di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                }
            }
            catch { }
            return 100.0;
        }

        private static bool HasIrCameraDevice(PlatformInfo platform)
        {
            if (platform != null && platform.PnpDevices != null)
            {
                for (int i = 0; i < platform.PnpDevices.Count; i++)
                {
                    PnpDeviceItem d = platform.PnpDevices[i];
                    if (d == null || string.IsNullOrEmpty(d.Name)) continue;
                    if (d.Name.IndexOf("IR Camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Name.IndexOf("Infrared", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Name.IndexOf("Hello IR", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasLogitechHardware(PlatformInfo platform)
        {
            try
            {
                using (RegistryKey usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", false))
                {
                    if (usb != null)
                    {
                        string[] names = usb.GetSubKeyNames();
                        if (names != null)
                        {
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (names[i].IndexOf("VID_046D", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (platform != null && platform.PnpDevices != null)
            {
                for (int i = 0; i < platform.PnpDevices.Count; i++)
                {
                    PnpDeviceItem d = platform.PnpDevices[i];
                    if (d != null && d.Name != null && d.Name.IndexOf("Logitech", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool CheckOneDriveSafeRemoval(out string details)
        {
            List<string> failures = new List<string>();
            int installAgeDays = -1;

            // 1. InstallDate < 14 days
            try
            {
                using (RegistryKey cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (cv != null)
                    {
                        object val = cv.GetValue("InstallDate", null);
                        if (val != null)
                        {
                            long unixSec = Convert.ToInt64(val, CultureInfo.InvariantCulture);
                            DateTime installDt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSec);
                            installAgeDays = (int)(DateTime.UtcNow - installDt).TotalDays;
                            if (installAgeDays >= 14)
                            {
                                failures.Add(string.Format("installation is {0} days old (>= 14 days)", installAgeDays));
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Zero accounts under HKU\<SID>\Software\Microsoft\OneDrive\Accounts
            int configuredAccounts = 0;
            try
            {
                List<string> sids = GetInteractiveUserSids();
                for (int i = 0; i < sids.Count; i++)
                {
                    using (RegistryKey accRoot = Registry.Users.OpenSubKey(sids[i] + @"\Software\Microsoft\OneDrive\Accounts", false))
                    {
                        if (accRoot != null)
                        {
                            string[] subs = accRoot.GetSubKeyNames();
                            if (subs != null)
                            {
                                for (int s = 0; s < subs.Length; s++)
                                {
                                    using (RegistryKey acc = accRoot.OpenSubKey(subs[s], false))
                                    {
                                        if (acc != null)
                                        {
                                            string email = Convert.ToString(acc.GetValue("UserEmail", "")).Trim();
                                            string folder = Convert.ToString(acc.GetValue("UserFolder", "")).Trim();
                                            if (!string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(folder))
                                            {
                                                configuredAccounts++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (configuredAccounts > 0)
            {
                failures.Add(string.Format("{0} OneDrive account(s) configured", configuredAccounts));
            }

            // 3. %UserProfile%\OneDrive empty or missing
            try
            {
                string odDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
                if (Directory.Exists(odDir))
                {
                    string[] entries = Directory.GetFileSystemEntries(odDir);
                    if (entries != null && entries.Length > 0)
                    {
                        failures.Add(string.Format("OneDrive folder contains {0} item(s)", entries.Length));
                    }
                }
            }
            catch { }

            if (failures.Count > 0)
            {
                details = string.Join("; ", failures.ToArray()) + ".";
                return false;
            }

            details = string.Format("Fresh installation ({0} days), 0 accounts configured, and empty OneDrive folder.", installAgeDays >= 0 ? installAgeDays : 0);
            return true;
        }

        public void RefreshAllStates(List<DebloatTweakDefinition> tweaks)
        {
            if (tweaks == null) return;
            HashSet<string> backedUpIds = GetTweakIdsWithSnapshots();
            int build = GetWindowsBuildNumber();

            for (int i = 0; i < tweaks.Count; i++)
            {
                DebloatTweakDefinition t = tweaks[i];
                if (t == null) continue;

                string reason;
                t.IsApplicable = EvaluateGating(t, out reason);
                t.InapplicableReason = reason;
                t.HasBackupSnapshot = backedUpIds.Contains(t.id);

                if (!t.IsApplicable)
                {
                    t.CurrentState = DebloatTweakState.NotApplicable;
                    continue;
                }

                t.CurrentState = EvaluateTweakState(t, build);
            }
        }

        public DebloatTweakState EvaluateTweakState(DebloatTweakDefinition tweak, int currentBuild)
        {
            if (tweak == null || tweak.detect == null || tweak.detect.Count == 0)
            {
                return DebloatTweakState.Pending;
            }

            int applicableRules = 0;
            int matchedRules = 0;

            for (int i = 0; i < tweak.detect.Count; i++)
            {
                DebloatDetectRule rule = tweak.detect[i];
                if (rule == null) continue;
                if (!IsBuildInRange(rule.minBuild, rule.maxBuild, currentBuild))
                {
                    continue;
                }

                applicableRules++;
                if (EvaluateSingleDetectRule(rule))
                {
                    matchedRules++;
                }
            }

            if (applicableRules == 0) return DebloatTweakState.Pending;
            if (matchedRules == applicableRules) return DebloatTweakState.Applied;
            if (matchedRules > 0) return DebloatTweakState.Partial;
            return DebloatTweakState.Pending;
        }

        private bool EvaluateSingleDetectRule(DebloatDetectRule rule)
        {
            string rType = (rule.type ?? "registry").Trim().ToLowerInvariant();
            string expected = rule.editionAwareTelemetry ? GetEditionAwareTelemetryValue() : (rule.equals ?? "");

            if (rType == "registry")
            {
                List<string> resolvedPaths = ExpandRegistryPathForUsers(rule.path, false);
                if (resolvedPaths.Count == 0) return false;

                for (int i = 0; i < resolvedPaths.Count; i++)
                {
                    object val;
                    RegistryValueKind kind;
                    if (!TryReadRegistryValue(resolvedPaths[i], rule.name, out val, out kind))
                    {
                        return false;
                    }
                    string actualStr = FormatRegistryValueToString(val, kind);
                    if (expected.StartsWith("Contains:", StringComparison.OrdinalIgnoreCase))
                    {
                        string token = expected.Substring("Contains:".Length).Trim();
                        if (actualStr.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            return false;
                        }
                    }
                    else if (!string.Equals(actualStr, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (rType == "service")
            {
                int startVal;
                if (!TryGetServiceStartValue(rule.name, out startVal))
                {
                    // If the service does not exist on this system, treat "Disabled" as satisfied
                    return string.Equals(expected, "Disabled", StringComparison.OrdinalIgnoreCase) || expected == "4";
                }
                string startLabel = ServiceStartIntToName(startVal);
                return string.Equals(startLabel, expected, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(startVal.ToString(CultureInfo.InvariantCulture), expected, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        #endregion

        #region REGISTRY & SERVICE HELPERS

        public static List<string> ExpandRegistryPathForUsers(string rawPath, bool includeInteractiveAll)
        {
            List<string> paths = new List<string>();
            if (string.IsNullOrWhiteSpace(rawPath)) return paths;

            string p = rawPath.Trim();
            if (p.IndexOf("<SID>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                List<string> sids = GetInteractiveUserSids();
                for (int i = 0; i < sids.Count; i++)
                {
                    string replaced = p.Replace("<SID>", sids[i]).Replace("<sid>", sids[i]);
                    paths.Add(replaced);
                    if (!includeInteractiveAll) break; // For detect, checking primary interactive user SID is fast and deterministic
                }
            }
            else if (p.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase) || p.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
            {
                int slash = p.IndexOf('\\');
                string sub = slash >= 0 ? p.Substring(slash + 1) : "";
                List<string> sids = GetInteractiveUserSids();
                if (sids.Count > 0)
                {
                    for (int i = 0; i < sids.Count; i++)
                    {
                        paths.Add(@"HKU\" + sids[i] + @"\" + sub);
                        if (!includeInteractiveAll) break;
                    }
                }
                else
                {
                    paths.Add(p);
                }
            }
            else
            {
                paths.Add(p);
            }
            return paths;
        }

        public static RegistryKey OpenHiveAndSubKey(string fullPath, bool writable, bool createIfMissing, out string subKeyPath)
        {
            subKeyPath = "";
            if (string.IsNullOrWhiteSpace(fullPath)) return null;

            string p = fullPath.Trim();
            int slash = p.IndexOf('\\');
            if (slash < 0) return null;

            string hiveName = p.Substring(0, slash).Trim().ToUpperInvariant();
            subKeyPath = p.Substring(slash + 1).Trim();

            RegistryKey rootHive = null;
            if (hiveName == "HKLM" || hiveName == "HKEY_LOCAL_MACHINE") rootHive = Registry.LocalMachine;
            else if (hiveName == "HKU" || hiveName == "HKEY_USERS") rootHive = Registry.Users;
            else if (hiveName == "HKCU" || hiveName == "HKEY_CURRENT_USER") rootHive = Registry.CurrentUser;
            else if (hiveName == "HKCR" || hiveName == "HKEY_CLASSES_ROOT") rootHive = Registry.ClassesRoot;

            if (rootHive == null || string.IsNullOrEmpty(subKeyPath)) return null;

            if (createIfMissing && writable)
            {
                return rootHive.CreateSubKey(subKeyPath);
            }
            return rootHive.OpenSubKey(subKeyPath, writable);
        }

        public static bool TryReadRegistryValue(string fullKeyPath, string valueName, out object value, out RegistryValueKind kind)
        {
            value = null;
            kind = RegistryValueKind.Unknown;
            try
            {
                string subKeyPath;
                using (RegistryKey key = OpenHiveAndSubKey(fullKeyPath, false, false, out subKeyPath))
                {
                    if (key == null) return false;
                    object v = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (v == null) return false;
                    value = v;
                    kind = key.GetValueKind(valueName);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string FormatRegistryValueToString(object val, RegistryValueKind kind)
        {
            if (val == null) return "";
            if (kind == RegistryValueKind.Binary && val is byte[])
            {
                return Convert.ToBase64String((byte[])val);
            }
            if (kind == RegistryValueKind.MultiString && val is string[])
            {
                return string.Join("\n", (string[])val);
            }
            return Convert.ToString(val, CultureInfo.InvariantCulture);
        }

        public static string RegistryKindToString(RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.DWord) return "DWord";
            if (kind == RegistryValueKind.QWord) return "QWord";
            if (kind == RegistryValueKind.ExpandString) return "ExpandString";
            if (kind == RegistryValueKind.MultiString) return "MultiString";
            if (kind == RegistryValueKind.Binary) return "Binary";
            return "String";
        }

        public static void WriteRegistryValue(string fullKeyPath, string valueName, string kindStr, string valueStr)
        {
            string subKeyPath;
            using (RegistryKey key = OpenHiveAndSubKey(fullKeyPath, true, true, out subKeyPath))
            {
                if (key == null) throw new InvalidOperationException("Cannot open or create registry key: " + fullKeyPath);

                string k = (kindStr ?? "DWord").Trim().ToLowerInvariant();
                if (k == "dword")
                {
                    int dv;
                    if (!int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out dv)) dv = 0;
                    key.SetValue(valueName, dv, RegistryValueKind.DWord);
                }
                else if (k == "qword")
                {
                    long qv;
                    if (!long.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out qv)) qv = 0L;
                    key.SetValue(valueName, qv, RegistryValueKind.QWord);
                }
                else if (k == "expandstring")
                {
                    key.SetValue(valueName, valueStr ?? "", RegistryValueKind.ExpandString);
                }
                else if (k == "multistring")
                {
                    string[] parts = (valueStr ?? "").Split(new char[] { '\n' }, StringSplitOptions.None);
                    key.SetValue(valueName, parts, RegistryValueKind.MultiString);
                }
                else if (k == "binary")
                {
                    byte[] bytes = string.IsNullOrEmpty(valueStr) ? new byte[0] : Convert.FromBase64String(valueStr);
                    key.SetValue(valueName, bytes, RegistryValueKind.Binary);
                }
                else
                {
                    key.SetValue(valueName, valueStr ?? "", RegistryValueKind.String);
                }
            }
        }

        public static bool TryGetServiceStartValue(string serviceName, out int startVal)
        {
            startVal = -1;
            if (string.IsNullOrWhiteSpace(serviceName)) return false;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName.Trim(), false))
                {
                    if (key == null) return false;
                    object v = key.GetValue("Start", null);
                    if (v == null) return false;
                    startVal = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static int ServiceStartNameToInt(string startupType)
        {
            if (string.IsNullOrWhiteSpace(startupType)) return 4;
            string s = startupType.Trim().ToLowerInvariant();
            if (s == "disabled" || s == "4") return 4;
            if (s == "manual" || s == "demand" || s == "3") return 3;
            if (s == "automatic" || s == "auto" || s == "2") return 2;
            return 4;
        }

        public static string ServiceStartIntToName(int startVal)
        {
            if (startVal == 2) return "Automatic";
            if (startVal == 3) return "Manual";
            if (startVal == 4) return "Disabled";
            return startVal.ToString(CultureInfo.InvariantCulture);
        }

        #endregion

        #region DRY-RUN & MAINTENANCE REPORT SUMMARY

        public string GenerateDryRunReport(List<DebloatTweakDefinition> selectedTweaks, Func<string, string, string> localizer)
        {
            StringBuilder sb = new StringBuilder();
            int build = GetWindowsBuildNumber();
            string edition = GetWindowsEditionId();
            string formFactor = IsLaptopSystem() ? "Laptop" : "Desktop";
            string oem = GetSystemOemManufacturer();

            sb.AppendLine("=============================================================");
            sb.AppendLine("   OPTIASSIST WINDOWS DEBLOATER 2.0 — DRY-RUN PREVIEW");
            sb.AppendLine("=============================================================");
            sb.AppendLine(string.Format("OS Build: {0} ({1}) | Form Factor: {2} | OEM: {3}", build, edition, formFactor, oem));
            sb.AppendLine(string.Format("Selected Tweaks: {0}", selectedTweaks != null ? selectedTweaks.Count : 0));
            sb.AppendLine("-------------------------------------------------------------");
            sb.AppendLine();

            if (selectedTweaks == null || selectedTweaks.Count == 0)
            {
                sb.AppendLine("No tweaks selected.");
                return sb.ToString();
            }

            bool anyReboot = false;
            for (int i = 0; i < selectedTweaks.Count; i++)
            {
                DebloatTweakDefinition t = selectedTweaks[i];
                if (t == null) continue;

                string title = localizer != null ? localizer(t.titleKey, t.titleFallback) : t.titleFallback;
                sb.AppendLine(string.Format("[{0}/{1}] {2} (ID: {3} | Risk: {4} | State: {5})",
                    i + 1, selectedTweaks.Count, title, t.id, (t.risk ?? "low").ToUpperInvariant(), t.CurrentState));

                if (t.needsReboot) anyReboot = true;

                if (t.warnings != null && t.warnings.Count > 0)
                {
                    for (int w = 0; w < t.warnings.Count; w++)
                    {
                        sb.AppendLine("   [!] Warning Key: " + t.warnings[w]);
                    }
                }

                for (int a = 0; a < t.apply.Count; a++)
                {
                    DebloatActionStep step = t.apply[a];
                    if (step == null) continue;
                    if (!IsBuildInRange(step.minBuild, step.maxBuild, build))
                    {
                        sb.AppendLine(string.Format("   - [Skipped by Build Gate {0}..{1}] {2} {3}\\{4}",
                            step.minBuild, step.maxBuild, step.type, step.path, step.name));
                        continue;
                    }

                    string sType = (step.type ?? "registry").ToLowerInvariant();
                    if (sType == "registry")
                    {
                        string targetVal = step.editionAwareTelemetry ? GetEditionAwareTelemetryValue() : step.value;
                        List<string> paths = ExpandRegistryPathForUsers(step.path, true);
                        for (int p = 0; p < paths.Count; p++)
                        {
                            object curObj;
                            RegistryValueKind curKind;
                            bool exists = TryReadRegistryValue(paths[p], step.name, out curObj, out curKind);
                            string curDisp = exists ? FormatRegistryValueToString(curObj, curKind) : "<not set>";
                            sb.AppendLine(string.Format("   • [Registry:{0}] {1}\\{2}: {3} -> {4} ({5}){6}",
                                step.operation, paths[p], step.name, curDisp, targetVal, step.kind,
                                step.includeDefaultUser ? " [+DefaultUser]" : ""));
                        }
                    }
                    else if (sType == "service")
                    {
                        int curStart;
                        bool exists = TryGetServiceStartValue(step.name, out curStart);
                        string curDisp = exists ? ServiceStartIntToName(curStart) : "<not installed>";
                        sb.AppendLine(string.Format("   • [Service] {0}: Startup {1} -> {2} (Stop={3})",
                            step.name, curDisp, step.startupType, step.stopService));
                    }
                    else if (sType == "appx")
                    {
                        sb.AppendLine(string.Format("   • [AppxPackage] Remove '{0}' (AllUsers={1}{2})",
                            step.pattern, step.allUsers,
                            !string.IsNullOrEmpty(step.stopProcesses) ? ", StopProcess=" + step.stopProcesses : ""));
                    }
                    else if (sType == "optionalFeature")
                    {
                        sb.AppendLine(string.Format("   • [OptionalFeature] {0}: {1}", step.name, step.state));
                    }
                    else if (sType == "capability")
                    {
                        sb.AppendLine(string.Format("   • [WindowsCapability] Pattern '{0}': {1}", step.pattern, step.state));
                    }
                    else if (sType == "powercfg")
                    {
                        sb.AppendLine(string.Format("   • [PowerCfg] {0} {1} {2} = {3}",
                            step.operation, step.subGroupGuid, step.settingGuid, step.value));
                    }
                    else if (sType == "scheduledTask")
                    {
                        sb.AppendLine(string.Format("   • [ScheduledTask] {0}: {1}", step.path, step.state));
                    }
                    else if (sType == "spi")
                    {
                        sb.AppendLine(string.Format("   • [SystemParametersInfo] {0} -> {1}", step.spiAction, step.pvParamBool));
                    }
                    else if (sType == "command")
                    {
                        sb.AppendLine(string.Format("   • [Command:{0}] {1}", step.shell, step.command));
                    }
                    else if (sType == "systemcleaner")
                    {
                        sb.AppendLine("   • [SystemCleaner] Safe Temp cleanup with quarantine backup in Config\\Addons\\SystemCleaner\\backups (no cleanmgr.exe)");
                    }
                }
                sb.AppendLine();
            }

            if (anyReboot)
            {
                sb.AppendLine("NOTE: One or more selected tweaks recommend a system restart to take full effect.");
            }
            return sb.ToString();
        }

        public string GenerateMaintenanceReportSummary(
            List<DebloatTweakDefinition> appliedTweaks,
            string sessionId,
            Func<string, string, string> localizer,
            string profileName = null,
            List<string> skippedItems = null)
        {
            PlatformInfo plat = GetCachedPlatformInfo();
            string resolvedProfile = profileName;
            if (string.IsNullOrEmpty(resolvedProfile) && _host != null && _host.PlatformDiagnostic != null && _host.PlatformDiagnostic.ProfileRecommendation != null)
            {
                resolvedProfile = _host.PlatformDiagnostic.ProfileRecommendation.ProfileCode;
            }
            if (string.IsNullOrEmpty(resolvedProfile)) resolvedProfile = "custom";

            bool pendingReboot = false;
            if (appliedTweaks != null)
            {
                for (int i = 0; i < appliedTweaks.Count; i++)
                {
                    if (appliedTweaks[i] != null && appliedTweaks[i].needsReboot)
                    {
                        pendingReboot = true;
                        break;
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("WINDOWS DEBLOATER 2.0 — OPTIMIZATION SUMMARY");
            sb.AppendLine("-------------------------------------------------------------");
            sb.AppendLine(string.Format("Session Snapshot ID: {0}", string.IsNullOrEmpty(sessionId) ? "N/A" : sessionId));
            sb.AppendLine(string.Format("Execution Timestamp: {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
            sb.AppendLine(string.Format("Applied Profile    : {0}", resolvedProfile));
            sb.AppendLine(string.Format("OS Build / Edition : {0} ({1})", GetWindowsBuildNumber(), GetWindowsEditionId()));
            if (plat != null)
            {
                string cpuDesc = (plat.Cpu != null) ? string.Format("{0} ({1}, {2})", plat.Cpu.ProcessorNameString, plat.Cpu.Microarchitecture, plat.Cpu.ReleaseYear) : "N/A";
                string ramDesc = (plat.Memory != null) ? string.Format("{0:F1} GB {1}", plat.Memory.TotalPhysicalGb, plat.Memory.SmbiosMemoryType) : "N/A";
                string diskDesc = (plat.Storage != null) ? string.Format("{0} ({1})", plat.Storage.SystemDiskMediaType, plat.Storage.SystemDiskBusType) : "N/A";
                sb.AppendLine(string.Format("Detected Hardware  : CPU: {0} | RAM: {1} | Disk: {2} | FormFactor: {3}", cpuDesc, ramDesc, diskDesc, plat.FormFactor));
            }
            sb.AppendLine(string.Format("Pending Reboot     : {0}", pendingReboot ? "YES (Restart recommended)" : "NO"));
            sb.AppendLine("Applied Optimizations:");
            if (appliedTweaks != null && appliedTweaks.Count > 0)
            {
                for (int i = 0; i < appliedTweaks.Count; i++)
                {
                    DebloatTweakDefinition t = appliedTweaks[i];
                    if (t == null) continue;
                    string title = localizer != null ? localizer(t.titleKey, t.titleFallback) : t.titleFallback;
                    sb.AppendLine(string.Format(" [X] {0} ({1})", title, t.id));
                }
            }
            else
            {
                sb.AppendLine(" None");
            }

            if (skippedItems != null && skippedItems.Count > 0)
            {
                sb.AppendLine("Skipped (Gating / Tamper Protection):");
                for (int i = 0; i < skippedItems.Count; i++)
                {
                    sb.AppendLine(" [-] " + skippedItems[i]);
                }
            }
            sb.AppendLine("-------------------------------------------------------------");

            try
            {
                string reportPath = Path.Combine(DebloaterCatalogLoader.GetBackupsDirectory(), "last_debloat_report.txt");
                File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }

            return sb.ToString();
        }

        #endregion

        #region EXECUTION & SNAPSHOT CAPTURE

        public DebloatBackupSession ApplyTweaksWithSnapshot(
            List<DebloatTweakDefinition> tweaks,
            Action<string> log,
            Func<string, string, string> localizer,
            string profileName = null)
        {
            DebloatBackupSession session = new DebloatBackupSession();
            session.osBuild = GetWindowsBuildNumber();
            session.osEdition = GetWindowsEditionId();
            List<string> skippedLog = new List<string>();

            if (tweaks == null || tweaks.Count == 0) return session;

            int build = session.osBuild;
            bool needDefaultUserHive = false;

            for (int i = 0; i < tweaks.Count; i++)
            {
                DebloatTweakDefinition t = tweaks[i];
                if (t == null) continue;
                for (int a = 0; a < t.apply.Count; a++)
                {
                    if (t.apply[a] != null && t.apply[a].includeDefaultUser)
                    {
                        needDefaultUserHive = true;
                        break;
                    }
                }
            }

            bool defaultUserMounted = false;
            if (needDefaultUserHive)
            {
                defaultUserMounted = TryMountDefaultUserHive(log);
            }

            try
            {
                for (int i = 0; i < tweaks.Count; i++)
                {
                    DebloatTweakDefinition tweak = tweaks[i];
                    if (tweak == null) continue;

                    string title = localizer != null ? localizer(tweak.titleKey, tweak.titleFallback) : tweak.titleFallback;
                    if (log != null)
                    {
                        log(string.Format("[DEBLOATER {0}/{1}] Applying '{2}' ({3})...", i + 1, tweaks.Count, title, tweak.id));
                    }

                    if (!session.tweakIds.Contains(tweak.id))
                    {
                        session.tweakIds.Add(tweak.id);
                    }

                    for (int a = 0; a < tweak.apply.Count; a++)
                    {
                        DebloatActionStep step = tweak.apply[a];
                        if (step == null) continue;
                        if (!IsBuildInRange(step.minBuild, step.maxBuild, build))
                        {
                            skippedLog.Add(string.Format("{0}: step '{1}' gated for build {2}", tweak.id, step.name, build));
                            continue;
                        }

                        ExecuteSingleStepWithSnapshot(tweak.id, step, session, defaultUserMounted, log, skippedLog);
                    }

                    tweak.CurrentState = EvaluateTweakState(tweak, build);
                    tweak.HasBackupSnapshot = true;
                }
            }
            finally
            {
                if (defaultUserMounted)
                {
                    TryUnmountDefaultUserHive(log);
                }
            }

            SaveBackupSession(session, log);
            GenerateMaintenanceReportSummary(tweaks, session.sessionId, localizer, profileName, skippedLog);
            return session;
        }

        private void ExecuteSingleStepWithSnapshot(
            string tweakId,
            DebloatActionStep step,
            DebloatBackupSession session,
            bool defaultUserMounted,
            Action<string> log,
            List<string> skippedLog = null)
        {
            if (step.skipIfTamperProtected)
            {
                PlatformInfo plat = GetCachedPlatformInfo();
                if (plat != null && plat.OsFirmware != null && plat.OsFirmware.IsTamperProtectionActive)
                {
                    string msg = string.Format("{0}\\{1} skipped (Tamper Protection active — requires manual action)", step.path, step.name);
                    if (skippedLog != null) skippedLog.Add(msg);
                    if (log != null) log("   [Defender:TamperProtected] " + msg);
                    return;
                }
            }

            string sType = (step.type ?? "registry").Trim().ToLowerInvariant();

            if (sType == "registry")
            {
                string targetVal = step.editionAwareTelemetry ? GetEditionAwareTelemetryValue() : (step.value ?? "");
                List<string> paths = ExpandRegistryPathForUsers(step.path, true);

                if (step.includeDefaultUser && defaultUserMounted &&
                    step.path != null && step.path.IndexOf("<SID>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string defPath = step.path.Replace("<SID>", "OptiDefaultUser").Replace("<sid>", "OptiDefaultUser");
                    paths.Add(defPath);
                }

                for (int i = 0; i < paths.Count; i++)
                {
                    string fullKeyPath = paths[i];
                    try
                    {
                        // 1. Capture Snapshot before writing
                        object prevObj;
                        RegistryValueKind prevKind;
                        bool existed = TryReadRegistryValue(fullKeyPath, step.name, out prevObj, out prevKind);

                        DebloatBackupEntry entry = new DebloatBackupEntry();
                        entry.tweakId = tweakId;
                        entry.entryType = "registry";
                        entry.targetPath = fullKeyPath;
                        entry.valueName = step.name;
                        entry.existedBefore = existed;
                        entry.previousKind = existed ? RegistryKindToString(prevKind) : (step.kind ?? "DWord");
                        entry.previousValue = existed ? FormatRegistryValueToString(prevObj, prevKind) : "";
                        session.entries.Add(entry);

                        // 2. Apply operation
                        string op = (step.operation ?? "set").Trim().ToLowerInvariant();
                        if (op == "deletevalue")
                        {
                            string sub;
                            using (RegistryKey k = OpenHiveAndSubKey(fullKeyPath, true, false, out sub))
                            {
                                if (k != null) k.DeleteValue(step.name, false);
                            }
                        }
                        else if (op == "mergehidelist")
                        {
                            string curVis = existed ? Convert.ToString(prevObj, CultureInfo.InvariantCulture) : "";
                            string tokenToAdd = (targetVal ?? "aicomponents").Trim();
                            string newVis;
                            if (string.IsNullOrWhiteSpace(curVis))
                            {
                                newVis = "hide:" + tokenToAdd;
                            }
                            else if (curVis.StartsWith("hide:", StringComparison.OrdinalIgnoreCase))
                            {
                                string listPart = curVis.Substring(5);
                                string[] parts = listPart.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                List<string> tokens = new List<string>();
                                bool found = false;
                                for (int t = 0; t < parts.Length; t++)
                                {
                                    string clean = parts[t].Trim();
                                    if (!string.IsNullOrEmpty(clean))
                                    {
                                        tokens.Add(clean);
                                        if (string.Equals(clean, tokenToAdd, StringComparison.OrdinalIgnoreCase)) found = true;
                                    }
                                }
                                if (!found) tokens.Add(tokenToAdd);
                                newVis = "hide:" + string.Join(";", tokens.ToArray());
                            }
                            else
                            {
                                newVis = curVis;
                            }
                            WriteRegistryValue(fullKeyPath, step.name, "String", newVis);
                            if (log != null) log(string.Format("   [Registry:Merge] {0}\\{1} = '{2}'", fullKeyPath, step.name, newVis));
                        }
                        else
                        {
                            WriteRegistryValue(fullKeyPath, step.name, step.kind, targetVal);
                            if (log != null) log(string.Format("   [Registry] {0}\\{1} = {2} ({3})", fullKeyPath, step.name, targetVal, step.kind));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log(string.Format("   [Registry:WARN] {0}\\{1}: {2}", fullKeyPath, step.name, ex.Message));
                    }
                }
                return;
            }

            if (sType == "service")
            {
                try
                {
                    int prevStart;
                    bool existed = TryGetServiceStartValue(step.name, out prevStart);
                    if (!existed)
                    {
                        if (log != null) log(string.Format("   [Service] '{0}' is not present on this system (skipped).", step.name));
                        return;
                    }

                    bool wasRunning = false;
                    try
                    {
                        using (ServiceController sc = new ServiceController(step.name))
                        {
                            wasRunning = (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending);
                            if (step.stopService && wasRunning && sc.CanStop)
                            {
                                sc.Stop();
                                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                            }
                        }
                    }
                    catch { }

                    DebloatBackupEntry entry = new DebloatBackupEntry();
                    entry.tweakId = tweakId;
                    entry.entryType = "service";
                    entry.targetPath = step.name;
                    entry.valueName = "Start";
                    entry.existedBefore = true;
                    entry.previousKind = "DWord";
                    entry.previousValue = prevStart.ToString(CultureInfo.InvariantCulture);
                    entry.wasRunning = wasRunning;
                    session.entries.Add(entry);

                    int targetStart = ServiceStartNameToInt(step.startupType);
                    WriteRegistryValue(@"HKLM\SYSTEM\CurrentControlSet\Services\" + step.name, "Start", "DWord", targetStart.ToString(CultureInfo.InvariantCulture));
                    if (log != null) log(string.Format("   [Service] {0} -> Start={1} ({2})", step.name, targetStart, step.startupType));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [Service:WARN] {0}: {1}", step.name, ex.Message));
                }
                return;
            }

            if (sType == "appx")
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(step.stopProcesses))
                    {
                        RunHiddenPowerShell("Get-Process " + step.stopProcesses + " -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue", 15);
                    }
                    string allUsersFlag = step.allUsers ? "-AllUsers" : "";
                    string ps = string.Format("Get-AppxPackage {0} {1} -ErrorAction SilentlyContinue | Remove-AppxPackage {0} -ErrorAction SilentlyContinue",
                        allUsersFlag, step.pattern);
                    RunHiddenPowerShell(ps, 60);
                    if (log != null) log(string.Format("   [Appx] Processed removal for '{0}'.", step.pattern));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [Appx:WARN] {0}: {1}", step.pattern, ex.Message));
                }
                return;
            }

            if (sType == "optionalFeature")
            {
                try
                {
                    bool enable = string.Equals(step.state, "Enable", StringComparison.OrdinalIgnoreCase);
                    DebloatBackupEntry entry = new DebloatBackupEntry();
                    entry.tweakId = tweakId;
                    entry.entryType = "optionalFeature";
                    entry.targetPath = step.name;
                    entry.valueName = "State";
                    entry.existedBefore = true;
                    entry.previousKind = "String";
                    entry.previousValue = enable ? "Disable" : "Enable";
                    session.entries.Add(entry);

                    string dismArgs = enable
                        ? string.Format("/Online /Enable-Feature /FeatureName:{0} /All /NoRestart /Quiet", step.name)
                        : string.Format("/Online /Disable-Feature /FeatureName:{0} /NoRestart /Quiet", step.name);
                    RunHiddenProcess("dism.exe", dismArgs, 90);
                    if (log != null) log(string.Format("   [OptionalFeature] {0} -> {1}", step.name, step.state));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [OptionalFeature:WARN] {0}: {1}", step.name, ex.Message));
                }
                return;
            }

            if (sType == "capability")
            {
                try
                {
                    bool add = string.Equals(step.state, "Add", StringComparison.OrdinalIgnoreCase);
                    string ps = add
                        ? string.Format("Get-WindowsCapability -Online | Where-Object {{ $_.Name -like '{0}' -and $_.State -ne 'Installed' }} | ForEach-Object {{ Add-WindowsCapability -Online -Name $_.Name -ErrorAction SilentlyContinue | Out-Null }}", step.pattern)
                        : string.Format("Get-WindowsCapability -Online | Where-Object {{ $_.Name -like '{0}' -and $_.State -eq 'Installed' }} | ForEach-Object {{ Remove-WindowsCapability -Online -Name $_.Name -ErrorAction SilentlyContinue | Out-Null }}", step.pattern);
                    RunHiddenPowerShell(ps, 120);
                    if (log != null) log(string.Format("   [Capability] {0} -> {1}", step.pattern, step.state));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [Capability:WARN] {0}: {1}", step.pattern, ex.Message));
                }
                return;
            }

            if (sType == "powercfg")
            {
                try
                {
                    string op = (step.operation ?? "").Trim().ToLowerInvariant();
                    string activeGuid = GetActivePowerSchemeGuid();
                    DebloatBackupEntry entry = new DebloatBackupEntry();
                    entry.tweakId = tweakId;
                    entry.entryType = "powercfg";
                    entry.targetPath = "powercfg";
                    entry.valueName = op;
                    entry.existedBefore = true;
                    entry.previousKind = "String";

                    if (op == "hibernate")
                    {
                        bool hib = IsHibernateEnabled();
                        entry.previousValue = hib ? "1" : "0";
                        session.entries.Add(entry);

                        string arg = (step.value == "0" || string.Equals(step.value, "off", StringComparison.OrdinalIgnoreCase)) ? "/h off" : "/h on";
                        RunHiddenProcess("powercfg.exe", arg, 20);
                    }
                    else if (op == "create_hybrid_plan")
                    {
                        entry.previousValue = activeGuid;
                        session.entries.Add(entry);
                        CreateAndActivateOptiAssistHybridPlan(log);
                    }
                    else if (op == "overlay_best_performance")
                    {
                        entry.previousValue = activeGuid;
                        session.entries.Add(entry);
                        WriteRegistryValue(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayAcPowerScheme", "String", "ded574b5-45a0-4f42-8737-46345c09c238");
                        RunHiddenProcess("powercfg.exe", "/setactive SCHEME_CURRENT", 20);
                    }
                    else if (op == "setacdcvalue")
                    {
                        entry.previousValue = activeGuid;
                        session.entries.Add(entry);
                        RunHiddenProcess("powercfg.exe", string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", step.subGroupGuid, step.settingGuid, step.value), 20);
                        RunHiddenProcess("powercfg.exe", string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", step.subGroupGuid, step.settingGuid, step.value), 20);
                        RunHiddenProcess("powercfg.exe", "/setactive SCHEME_CURRENT", 20);
                    }
                    else if (op == "setacvalue" || op == "setdcvalue")
                    {
                        entry.previousValue = activeGuid;
                        session.entries.Add(entry);

                        if (op == "setacvalue")
                        {
                            RunHiddenProcess("powercfg.exe", string.Format("/setacvalueindex SCHEME_CURRENT {0} {1} {2}", step.subGroupGuid, step.settingGuid, step.value), 20);
                            RunHiddenProcess("powercfg.exe", "/setactive SCHEME_CURRENT", 20);
                        }
                        else
                        {
                            RunHiddenProcess("powercfg.exe", string.Format("/setdcvalueindex SCHEME_CURRENT {0} {1} {2}", step.subGroupGuid, step.settingGuid, step.value), 20);
                            RunHiddenProcess("powercfg.exe", "/setactive SCHEME_CURRENT", 20);
                        }
                    }
                    else if (op == "setactive")
                    {
                        entry.previousValue = activeGuid;
                        session.entries.Add(entry);
                        RunHiddenProcess("powercfg.exe", string.Format("/setactive {0}", step.value), 20);
                    }
                    if (log != null) log(string.Format("   [PowerCfg] {0} applied.", step.operation));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [PowerCfg:WARN] {0}: {1}", step.operation, ex.Message));
                }
                return;
            }

            if (sType == "scheduledTask")
            {
                try
                {
                    bool disable = !string.Equals(step.state, "Enable", StringComparison.OrdinalIgnoreCase);
                    DebloatBackupEntry entry = new DebloatBackupEntry();
                    entry.tweakId = tweakId;
                    entry.entryType = "scheduledTask";
                    entry.targetPath = step.path;
                    entry.valueName = "State";
                    entry.existedBefore = true;
                    entry.previousKind = "String";
                    entry.previousValue = disable ? "Enable" : "Disable";
                    session.entries.Add(entry);

                    string flag = disable ? "/DISABLE" : "/ENABLE";
                    RunHiddenProcess("schtasks.exe", string.Format("/Change /TN \"{0}\" {1}", step.path, flag), 20);
                    if (log != null) log(string.Format("   [ScheduledTask] {0} -> {1}", step.path, flag));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [ScheduledTask:WARN] {0}: {1}", step.path, ex.Message));
                }
                return;
            }

            if (sType == "spi")
            {
                try
                {
                    uint setCode;
                    uint getCode;
                    if (ResolveSpiActionCodes(step.spiAction, out setCode, out getCode))
                    {
                        bool prevBool = false;
                        SystemParametersInfo(getCode, 0, ref prevBool, 0);

                        DebloatBackupEntry entry = new DebloatBackupEntry();
                        entry.tweakId = tweakId;
                        entry.entryType = "spi";
                        entry.targetPath = step.spiAction;
                        entry.valueName = "pvParamBool";
                        entry.existedBefore = true;
                        entry.previousKind = "Bool";
                        entry.previousValue = prevBool ? "true" : "false";
                        session.entries.Add(entry);

                        IntPtr pv = step.pvParamBool ? new IntPtr(1) : IntPtr.Zero;
                        SystemParametersInfo(setCode, (uint)step.uiParam, pv, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                        if (log != null) log(string.Format("   [SPI] {0} -> {1}", step.spiAction, step.pvParamBool));
                    }
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [SPI:WARN] {0}: {1}", step.spiAction, ex.Message));
                }
                return;
            }

            if (sType == "systemcleaner")
            {
                try
                {
                    ExecuteSafeTempCleanupWithQuarantine(log);
                }
                catch (Exception ex)
                {
                    if (log != null) log("   [SystemCleaner:WARN] " + ex.Message);
                }
                return;
            }

            if (sType == "command" && !string.IsNullOrWhiteSpace(step.command))
            {
                try
                {
                    if (string.Equals(step.shell, "cmd", StringComparison.OrdinalIgnoreCase))
                    {
                        RunHiddenProcess("cmd.exe", "/c " + step.command, 60);
                    }
                    else
                    {
                        RunHiddenPowerShell(step.command, 60);
                    }
                    if (log != null) log(string.Format("   [Command] Executed ({0}).", step.shell));
                }
                catch (Exception ex)
                {
                    if (log != null) log(string.Format("   [Command:WARN] {0}", ex.Message));
                }
            }
        }

        private static void ExecuteSafeTempCleanupWithQuarantine(Action<string> log)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string quarantineRoot = Path.Combine(baseDir, @"Config\Addons\SystemCleaner\backups");
            string sessionFolder = Path.Combine(quarantineRoot, "debloater_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));

            List<string> targetDirs = new List<string>();
            string userTemp = Path.GetTempPath();
            if (!string.IsNullOrEmpty(userTemp) && Directory.Exists(userTemp)) targetDirs.Add(userTemp);
            string winRoot = Environment.GetEnvironmentVariable("SystemRoot");
            if (!string.IsNullOrEmpty(winRoot))
            {
                string winTemp = Path.Combine(winRoot, "Temp");
                if (Directory.Exists(winTemp) && !targetDirs.Contains(winTemp)) targetDirs.Add(winTemp);
            }

            int movedFiles = 0;
            long movedBytes = 0;
            StringBuilder manifest = new StringBuilder();

            for (int d = 0; d < targetDirs.Count; d++)
            {
                string dir = targetDirs[d];
                try
                {
                    string[] files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
                    for (int f = 0; f < files.Length; f++)
                    {
                        string src = files[f];
                        try
                        {
                            FileInfo fi = new FileInfo(src);
                            // Skip locked or very recent files (< 5 minutes old) to avoid interfering with active processes
                            if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalMinutes < 5) continue;

                            if (!Directory.Exists(sessionFolder)) Directory.CreateDirectory(sessionFolder);
                            string destName = string.Format("{0:D5}_{1}", movedFiles, fi.Name);
                            string dest = Path.Combine(sessionFolder, destName);
                            File.Move(src, dest);
                            movedFiles++;
                            movedBytes += fi.Length;
                            manifest.AppendLine(destName + "|" + src);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (movedFiles > 0)
            {
                try { File.WriteAllText(Path.Combine(sessionFolder, "manifest.txt"), manifest.ToString(), new UTF8Encoding(false)); } catch { }
            }

            if (log != null)
            {
                log(string.Format("   [SystemCleaner] Quarantined {0} temp file(s) ({1:F2} MB) into SystemCleaner backups (no cleanmgr.exe).",
                    movedFiles, (double)movedBytes / (1024.0 * 1024.0)));
            }
        }

        public bool EvaluateGatingSimulated(
            DebloatTweakDefinition tweak,
            int simulatedBuild,
            string simulatedEdition,
            PlatformInfo simulatedPlatform,
            out string reason)
        {
            reason = "";
            if (tweak == null) { reason = "Null tweak"; return false; }
            DebloatAppliesTo gate = tweak.appliesTo;
            if (gate == null) return true;

            if (!IsBuildInRange(gate.minBuild, gate.maxBuild, simulatedBuild))
            {
                reason = string.Format("OS Build {0} outside supported range [{1}..{2}]", simulatedBuild, gate.minBuild, gate.maxBuild);
                return false;
            }

            if (gate.editions != null && gate.editions.Count > 0 && !gate.editions.Contains("*"))
            {
                bool matchEd = false;
                for (int i = 0; i < gate.editions.Count; i++)
                {
                    if ((simulatedEdition ?? "").IndexOf(gate.editions[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchEd = true;
                        break;
                    }
                }
                if (!matchEd)
                {
                    reason = "Windows Edition '" + simulatedEdition + "' not in target editions";
                    return false;
                }
            }

            if (gate.formFactors != null && gate.formFactors.Count > 0 && !gate.formFactors.Contains("*") && simulatedPlatform != null)
            {
                string curFf = "desktop";
                if (simulatedPlatform.FormFactor == ChassisFormFactor.Ultraportable) curFf = "ultraportable";
                else if (simulatedPlatform.FormFactor == ChassisFormFactor.Laptop || simulatedPlatform.HasBattery) curFf = "laptop";

                bool matchFf = false;
                for (int i = 0; i < gate.formFactors.Count; i++)
                {
                    string targetFf = gate.formFactors[i];
                    if (string.Equals(targetFf, curFf, StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(curFf, "ultraportable", StringComparison.OrdinalIgnoreCase) && string.Equals(targetFf, "laptop", StringComparison.OrdinalIgnoreCase)))
                    {
                        matchFf = true;
                        break;
                    }
                }
                if (!matchFf)
                {
                    reason = "Form factor '" + curFf + "' not in target form factors";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(gate.condition) && simulatedPlatform != null)
            {
                string cond = gate.condition.Trim().ToLowerInvariant();
                if (cond == "ssd_only")
                {
                    string media = simulatedPlatform.Storage != null ? (simulatedPlatform.Storage.SystemDiskMediaType ?? "") : "";
                    bool isEmmc = simulatedPlatform.Storage != null && simulatedPlatform.Storage.IsSystemEmmcOrSd;
                    if (!string.Equals(media, "SSD", StringComparison.OrdinalIgnoreCase) || isEmmc)
                    {
                        reason = "SysMain blocked on non-SSD/eMMC disk (" + (isEmmc ? "eMMC" : media) + ")";
                        return false;
                    }
                }
            }

            return true;
        }

        private static void CreateAndActivateOptiAssistHybridPlan(Action<string> log)
        {
            string ps =
                "$existing = (powercfg /list | Select-String 'OptiAssist Hybrid' | ForEach-Object { if ($_ -match '([0-9a-fA-F-]{36})') { $matches[1] } } | Select-Object -First 1); " +
                "if (-not $existing) { " +
                "  $dup = (powercfg /duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e); " +
                "  if ($dup -match '([0-9a-fA-F-]{36})') { $existing = $matches[1]; powercfg /changename $existing 'OptiAssist Hybrid' 'Balanced base with zero disk idle, max Wi-Fi performance, and curated AC/DC timeouts.' | Out-Null } " +
                "}; " +
                "if ($existing) { " +
                "  powercfg /setacvalueindex $existing SUB_DISK 6738e2c4-e8a5-4a42-b16a-e040e769756e 0 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_DISK 6738e2c4-e8a5-4a42-b16a-e040e769756e 0 | Out-Null; " +
                "  powercfg /setacvalueindex $existing 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0 | Out-Null; " +
                "  powercfg /setacvalueindex $existing SUB_PROCESSOR PROCTHROTTLEMIN 5 | Out-Null; " +
                "  powercfg /setacvalueindex $existing SUB_PROCESSOR PROCTHROTTLEMAX 100 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_PROCESSOR PROCTHROTTLEMIN 5 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_PROCESSOR PROCTHROTTLEMAX 100 | Out-Null; " +
                "  powercfg /setacvalueindex $existing SUB_VIDEO VIDEOIDLE 1200 | Out-Null; " +
                "  powercfg /setacvalueindex $existing SUB_SLEEP STANDBYIDLE 0 | Out-Null; " +
                "  powercfg /setacvalueindex $existing SUB_SLEEP HIBERNATEIDLE 0 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_VIDEO VIDEOIDLE 600 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_SLEEP STANDBYIDLE 1800 | Out-Null; " +
                "  powercfg /setdcvalueindex $existing SUB_SLEEP HIBERNATEIDLE 3600 | Out-Null; " +
                "  powercfg /setactive $existing | Out-Null; " +
                "}";
            RunHiddenPowerShell(ps, 30);
            if (log != null) log("   [PowerCfg] Created/activated 'OptiAssist Hybrid' scheme (Balanced base preserved).");
        }

        private static bool ResolveSpiActionCodes(string name, out uint setCode, out uint getCode)
        {
            setCode = 0;
            getCode = 0;
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToUpperInvariant();
            if (n == "SPI_SETMENUANIMATION") { setCode = 0x1003; getCode = 0x1002; return true; }
            if (n == "SPI_SETCOMBOBOXANIMATION") { setCode = 0x1005; getCode = 0x1004; return true; }
            if (n == "SPI_SETLISTBOXSMOOTHSCROLLING") { setCode = 0x1007; getCode = 0x1006; return true; }
            if (n == "SPI_SETMENUFADE") { setCode = 0x1013; getCode = 0x1012; return true; }
            if (n == "SPI_SETSELECTIONFADE") { setCode = 0x1015; getCode = 0x1014; return true; }
            if (n == "SPI_SETTOOLTIPANIMATION") { setCode = 0x1017; getCode = 0x1016; return true; }
            if (n == "SPI_SETCURSORSHADOW") { setCode = 0x101B; getCode = 0x101A; return true; }
            if (n == "SPI_SETDROPSHADOW") { setCode = 0x1025; getCode = 0x1024; return true; }
            if (n == "SPI_SETCLIENTAREAANIMATION") { setCode = 0x1043; getCode = 0x1042; return true; }
            return false;
        }

        #endregion

        #region SNAPSHOT STORAGE & ROLLBACK (GRANULAR + SESSION)

        public static void SaveBackupSession(DebloatBackupSession session, Action<string> log)
        {
            if (session == null || session.entries == null || session.entries.Count == 0) return;
            try
            {
                string dir = DebloaterCatalogLoader.GetBackupsDirectory();
                string path = Path.Combine(dir, session.sessionId + ".json");
                string json = DebloaterCatalogLoader.SerializeBackupSession(session);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                if (log != null)
                {
                    log(string.Format("[SNAPSHOT] Saved {0} state entries to Config\\Addons\\Toolbox\\backups\\{1}.json",
                        session.entries.Count, session.sessionId));
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("[SNAPSHOT:WARN] Failed to save snapshot: " + ex.Message);
            }
        }

        public static List<string> GetBackupSessionFilesNewestFirst()
        {
            List<string> list = new List<string>();
            try
            {
                string dir = DebloaterCatalogLoader.GetBackupsDirectory();
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir, "*.json");
                    if (files != null && files.Length > 0)
                    {
                        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                        Array.Reverse(files);
                        list.AddRange(files);
                    }
                }
            }
            catch { }
            return list;
        }

        public static HashSet<string> GetTweakIdsWithSnapshots()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> files = GetBackupSessionFilesNewestFirst();
            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    DebloatBackupSession s = DebloaterCatalogLoader.ParseBackupSession(File.ReadAllText(files[i], Encoding.UTF8));
                    if (s != null && s.entries != null)
                    {
                        for (int j = 0; j < s.entries.Count; j++)
                        {
                            if (!string.IsNullOrEmpty(s.entries[j].tweakId))
                            {
                                ids.Add(s.entries[j].tweakId);
                            }
                        }
                    }
                }
                catch { }
            }
            return ids;
        }

        public bool RevertSingleTweak(string tweakId, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(tweakId)) return false;
            List<string> files = GetBackupSessionFilesNewestFirst();

            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    DebloatBackupSession session = DebloaterCatalogLoader.ParseBackupSession(File.ReadAllText(files[i], Encoding.UTF8));
                    if (session == null || session.entries == null) continue;

                    List<DebloatBackupEntry> matching = new List<DebloatBackupEntry>();
                    for (int j = 0; j < session.entries.Count; j++)
                    {
                        if (string.Equals(session.entries[j].tweakId, tweakId, StringComparison.OrdinalIgnoreCase))
                        {
                            matching.Add(session.entries[j]);
                        }
                    }

                    if (matching.Count > 0)
                    {
                        if (log != null)
                        {
                            log(string.Format("[ROLLBACK] Reverting tweak '{0}' from snapshot {1} ({2} entries)...",
                                tweakId, session.sessionId, matching.Count));
                        }
                        RestoreBackupEntries(matching, log);
                        AppendRollbackNoteToReport(string.Format("Reverted tweak '{0}' from snapshot {1} at {2}", tweakId, session.sessionId, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
                        return true;
                    }
                }
                catch { }
            }

            if (log != null) log(string.Format("[ROLLBACK] No snapshot found for tweak '{0}'.", tweakId));
            return false;
        }

        public bool RevertLatestBackupSession(Action<string> log, out string revertedSessionId)
        {
            revertedSessionId = "";
            List<string> files = GetBackupSessionFilesNewestFirst();
            if (files.Count == 0)
            {
                if (log != null) log("[ROLLBACK] No backup sessions found in Config\\Addons\\Toolbox\\backups.");
                return false;
            }

            try
            {
                string latestFile = files[0];
                DebloatBackupSession session = DebloaterCatalogLoader.ParseBackupSession(File.ReadAllText(latestFile, Encoding.UTF8));
                if (session == null || session.entries == null || session.entries.Count == 0)
                {
                    return false;
                }

                revertedSessionId = session.sessionId;
                if (log != null)
                {
                    log(string.Format("[ROLLBACK] Reverting entire session '{0}' ({1} entries)...",
                        session.sessionId, session.entries.Count));
                }

                RestoreBackupEntries(session.entries, log);
                AppendRollbackNoteToReport(string.Format("Reverted entire session '{0}' ({1} entries) at {2}", session.sessionId, session.entries.Count, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("[ROLLBACK:ERROR] " + ex.Message);
                return false;
            }
        }

        private static void AppendRollbackNoteToReport(string note)
        {
            try
            {
                string reportPath = Path.Combine(DebloaterCatalogLoader.GetBackupsDirectory(), "last_debloat_report.txt");
                string line = "[ROLLBACK] " + note + Environment.NewLine;
                File.AppendAllText(reportPath, line, new UTF8Encoding(false));
            }
            catch { }
        }

        private void RestoreBackupEntries(List<DebloatBackupEntry> entries, Action<string> log)
        {
            if (entries == null || entries.Count == 0) return;

            bool needsDefaultUser = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].targetPath != null &&
                    entries[i].targetPath.IndexOf("OptiDefaultUser", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    needsDefaultUser = true;
                    break;
                }
            }

            bool mounted = false;
            if (needsDefaultUser)
            {
                mounted = TryMountDefaultUserHive(log);
            }

            try
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    DebloatBackupEntry e = entries[i];
                    if (e == null) continue;
                    string eType = (e.entryType ?? "registry").ToLowerInvariant();

                    if (eType == "registry")
                    {
                        try
                        {
                            if (!e.existedBefore)
                            {
                                string sub;
                                using (RegistryKey k = OpenHiveAndSubKey(e.targetPath, true, false, out sub))
                                {
                                    if (k != null)
                                    {
                                        k.DeleteValue(e.valueName, false);
                                        if (log != null) log(string.Format("   [Rollback:Registry] Deleted value {0}\\{1}", e.targetPath, e.valueName));
                                    }
                                }
                            }
                            else
                            {
                                WriteRegistryValue(e.targetPath, e.valueName, e.previousKind, e.previousValue);
                                if (log != null) log(string.Format("   [Rollback:Registry] Restored {0}\\{1} = {2}", e.targetPath, e.valueName, e.previousValue));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (log != null) log(string.Format("   [Rollback:WARN] {0}\\{1}: {2}", e.targetPath, e.valueName, ex.Message));
                        }
                    }
                    else if (eType == "service")
                    {
                        try
                        {
                            WriteRegistryValue(@"HKLM\SYSTEM\CurrentControlSet\Services\" + e.targetPath, "Start", "DWord", e.previousValue);
                            if (e.wasRunning)
                            {
                                try
                                {
                                    using (ServiceController sc = new ServiceController(e.targetPath))
                                    {
                                        if (sc.Status == ServiceControllerStatus.Stopped)
                                        {
                                            sc.Start();
                                        }
                                    }
                                }
                                catch { }
                            }
                            if (log != null) log(string.Format("   [Rollback:Service] Restored {0} -> Start={1}", e.targetPath, e.previousValue));
                        }
                        catch (Exception ex)
                        {
                            if (log != null) log(string.Format("   [Rollback:WARN] Service {0}: {1}", e.targetPath, ex.Message));
                        }
                    }
                    else if (eType == "powercfg")
                    {
                        try
                        {
                            string op = (e.valueName ?? "").Trim().ToLowerInvariant();
                            if (op == "hibernate")
                            {
                                string arg = (e.previousValue == "0") ? "/h off" : "/h on";
                                RunHiddenProcess("powercfg.exe", arg, 20);
                                if (log != null) log(string.Format("   [Rollback:PowerCfg] Hibernate -> {0}", e.previousValue));
                            }
                            else
                            {
                                string scheme = e.previousValue;
                                if (!string.IsNullOrEmpty(scheme))
                                {
                                    RunHiddenProcess("powercfg.exe", string.Format("/setactive {0}", scheme), 20);
                                    if (log != null) log(string.Format("   [Rollback:PowerCfg] Restored active power scheme {0}", scheme));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (log != null) log(string.Format("   [Rollback:WARN] PowerCfg: {0}", ex.Message));
                        }
                    }
                    else if (eType == "scheduledTask")
                    {
                        try
                        {
                            string flag = string.Equals(e.previousValue, "Enable", StringComparison.OrdinalIgnoreCase) ? "/ENABLE" : "/DISABLE";
                            RunHiddenProcess("schtasks.exe", string.Format("/Change /TN \"{0}\" {1}", e.targetPath, flag), 20);
                            if (log != null) log(string.Format("   [Rollback:ScheduledTask] {0} -> {1}", e.targetPath, flag));
                        }
                        catch { }
                    }
                    else if (eType == "spi")
                    {
                        try
                        {
                            uint setCode;
                            uint getCode;
                            if (ResolveSpiActionCodes(e.targetPath, out setCode, out getCode))
                            {
                                bool bVal = string.Equals(e.previousValue, "true", StringComparison.OrdinalIgnoreCase);
                                IntPtr pv = bVal ? new IntPtr(1) : IntPtr.Zero;
                                SystemParametersInfo(setCode, 0, pv, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                                if (log != null) log(string.Format("   [Rollback:SPI] {0} -> {1}", e.targetPath, bVal));
                            }
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                if (mounted)
                {
                    TryUnmountDefaultUserHive(log);
                }
            }
        }

        #endregion

        #region PROCESS & DEFAULT USER HIVE UTILITIES

        private static bool TryMountDefaultUserHive(Action<string> log)
        {
            try
            {
                string sysDrive = Environment.GetEnvironmentVariable("SystemDrive");
                if (string.IsNullOrEmpty(sysDrive)) sysDrive = "C:";
                string datPath = Path.Combine(sysDrive, @"Users\Default\NTUSER.DAT");
                if (!File.Exists(datPath)) return false;

                using (RegistryKey check = Registry.Users.OpenSubKey("OptiDefaultUser", false))
                {
                    if (check != null) return false;
                }

                int code = RunHiddenProcess("reg.exe", string.Format("load \"HKU\\OptiDefaultUser\" \"{0}\"", datPath), 15);
                return code == 0;
            }
            catch
            {
                return false;
            }
        }

        public static string GetActivePowerSchemeGuid()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("ActivePowerScheme", null);
                        if (val != null) return val.ToString().Trim();
                    }
                }
            }
            catch { }
            return "381b4222-f694-41f0-9685-ff5bb260df2e";
        }

        public static bool IsHibernateEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("HibernateEnabled", null);
                        if (val != null) return Convert.ToInt32(val, CultureInfo.InvariantCulture) != 0;
                    }
                }
            }
            catch { }
            return true;
        }

        private static void TryUnmountDefaultUserHive(Action<string> log)
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                int code = RunHiddenProcess("reg.exe", "unload \"HKU\\OptiDefaultUser\"", 15);
                if (code != 0)
                {
                    System.Threading.Thread.Sleep(500);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    code = RunHiddenProcess("reg.exe", "unload \"HKU\\OptiDefaultUser\"", 15);
                }
                if (code == 0)
                {
                    if (log != null) log("   [DefaultUser] Hive unmounted successfully.");
                }
                else
                {
                    if (log != null) log("   [DefaultUser:WARN] Unload returned exit code " + code);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("   [DefaultUser:WARN] Exception during unmount: " + ex.Message);
            }
        }

        private static int RunHiddenProcess(string fileName, string arguments, int timeoutSec)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    p.WaitForExit(timeoutSec * 1000);
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch
            {
                return -1;
            }
        }

        private static int RunHiddenPowerShell(string script, int timeoutSec)
        {
            string wrapped = "$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + script;
            byte[] bytes = Encoding.Unicode.GetBytes(wrapped);
            string b64 = Convert.ToBase64String(bytes);
            return RunHiddenProcess("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + b64, timeoutSec);
        }

        #endregion
    }
}
