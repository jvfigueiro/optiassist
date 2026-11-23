using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace OptiAssist.Platform
{
    public class PlatformDiagnosticService
    {
        private static PlatformDiagnosticService _instance;
        private static readonly object _lock = new object();

        private PlatformDiagnosticSnapshot _cachedSnapshot;
        private readonly object _snapshotLock = new object();

        public static PlatformDiagnosticService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PlatformDiagnosticService();
                        }
                    }
                }
                return _instance;
            }
        }

        public PlatformDiagnosticSnapshot GetSnapshot(bool forceRefresh = false, string userOverrideProfile = null, Action<string> log = null)
        {
            lock (_snapshotLock)
            {
                if (_cachedSnapshot != null && !forceRefresh && string.IsNullOrEmpty(userOverrideProfile))
                {
                    return _cachedSnapshot;
                }

                PlatformDiagnosticSnapshot snapshot = new PlatformDiagnosticSnapshot();
                snapshot.GeneratedAtUtc = DateTime.UtcNow;

                // 1. Collect Hardware and OS Platform Info
                snapshot.Platform = PlatformProfiler.CollectPlatformInfo(log);

                // 2. Evaluate Profile Recommendation
                snapshot.ProfileRecommendation = ProfileRecommender.RecommendProfile(snapshot.Platform, userOverrideProfile);

                // 3. Evaluate Windows 11 Compatibility Report
                snapshot.Win11Compat = Win11CompatAdvisor.Evaluate(snapshot.Platform);

                // 4. Evaluate Known Hardware Issues & Quirks
                List<DiagnosticRecommendationItem> quirkItems = KnownIssuesEngine.Default.EvaluateIssues(snapshot.Platform);

                // 5. Build Unified Recommendations List for Dashboard and Addons
                BuildUnifiedRecommendations(snapshot, quirkItems);

                _cachedSnapshot = snapshot;
                return snapshot;
            }
        }

        private static bool IsConstrainedLegacyEntryHardware(PlatformInfo info)
        {
            if (info == null) return false;

            // Rule 1: OS drive <= 32 GB (e.g. 32GB eMMC netbooks/tablets)
            if (info.Storage != null && info.Storage.SystemDriveTotalGb > 0 && info.Storage.SystemDriveTotalGb <= 32.5)
            {
                return true;
            }

            // Rule 2: Low-power / legacy entry CPU (Atom, Silvermont, Airmont, Bonnell, Bobcat, AMD C/E-Series, legacy Celeron N)
            if (info.Cpu != null)
            {
                string cpuName = (info.Cpu.ProcessorNameString ?? "").ToLowerInvariant();
                string arch = (info.Cpu.Microarchitecture ?? "").ToLowerInvariant();

                if (cpuName.Contains("atom") ||
                    arch.Contains("bonnell") ||
                    arch.Contains("saltwell") ||
                    arch.Contains("silvermont") ||
                    arch.Contains("airmont") ||
                    arch.Contains("bobcat") ||
                    arch.Contains("jaguar") ||
                    arch.Contains("kabini") ||
                    arch.Contains("stoney"))
                {
                    return true;
                }

                // AMD C-Series / E-Series (e.g. AMD E-350, AMD E1-2100, AMD E2-1800, AMD C-60)
                if (cpuName.Contains("amd e-") || cpuName.Contains("amd e1-") || cpuName.Contains("amd e2-") ||
                    cpuName.Contains("amd c-") || cpuName.Contains("amd a4-1") || cpuName.Contains("amd a4-5"))
                {
                    return true;
                }

                // Legacy Intel Celeron N / Pentium N (< 2019)
                if ((cpuName.Contains("celeron") || cpuName.Contains("pentium")) &&
                    (cpuName.Contains(" n2") || cpuName.Contains(" n3") || cpuName.Contains(" j1") || cpuName.Contains(" j2")) &&
                    info.Cpu.ReleaseYear > 0 && info.Cpu.ReleaseYear < 2019)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldShowWin11CompatWarningOnDashboard(PlatformDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Platform == null || snapshot.Win11Compat == null) return false;

            // Only show when Windows 11 is actually installed (Build >= 22000)
            bool isRunningWin11 = snapshot.Platform.OsFirmware != null && snapshot.Platform.OsFirmware.CurrentBuild >= 22000;
            if (!isRunningWin11) return false;

            // Critical hard-gate failure on Win11
            if (!snapshot.Win11Compat.HardGatesPassed) return true;

            // Hardware older than 2017 running Windows 11
            int hwYear = (snapshot.Platform.Cpu != null && snapshot.Platform.Cpu.ReleaseYear > 0)
                ? snapshot.Platform.Cpu.ReleaseYear
                : (snapshot.Platform.Oem != null ? snapshot.Platform.Oem.BiosReleaseYear : 0);

            if (hwYear > 0 && hwYear < 2017) return true;

            return false;
        }

        private static void BuildUnifiedRecommendations(PlatformDiagnosticSnapshot snapshot, List<DiagnosticRecommendationItem> quirkItems)
        {
            List<DiagnosticRecommendationItem> list = snapshot.Recommendations;
            list.Clear();

            bool isPt = (OptiAssist.LocalizationHelper.CurrentLanguage ?? "").StartsWith("pt", StringComparison.OrdinalIgnoreCase);

            // 1. OptiBoost Profile Recommendation — only surfaced on Dashboard for constrained storage (<= 32 GB) or low-power/legacy entry CPUs
            if (snapshot.ProfileRecommendation != null && IsConstrainedLegacyEntryHardware(snapshot.Platform))
            {
                DiagnosticRecommendationItem profItem = new DiagnosticRecommendationItem();
                profItem.Id = "rec-debloater-profile";
                profItem.Category = "Profile";
                profItem.Severity = RecommendationSeverity.Info;
                profItem.Title = "OptiBoost: " + snapshot.ProfileRecommendation.Title;

                string reasonsStr = string.Join(" ", snapshot.ProfileRecommendation.Reasons.ToArray());
                profItem.Description = reasonsStr;
                profItem.ActionLabel = isPt ? "Abrir OptiBoost" : "Open OptiBoost";
                profItem.ActionTag = "open_debloater";
                profItem.TargetProfileId = snapshot.ProfileRecommendation.ProfileCode;
                list.Add(profItem);
            }

            // 2. Windows 11 Compatibility Warning — only surfaced on Dashboard when hardware < 2017 runs Win11 or fails Win11 hard-gates
            if (ShouldShowWin11CompatWarningOnDashboard(snapshot))
            {
                DiagnosticRecommendationItem w11Item = new DiagnosticRecommendationItem();
                w11Item.Id = "rec-win11-compat";
                w11Item.Category = "Win11";
                w11Item.Severity = RecommendationSeverity.Warning;

                w11Item.Title = snapshot.Win11Compat.Title;
                string desc = snapshot.Win11Compat.LifecycleSummary;
                if (snapshot.Win11Compat.Warnings.Count > 0)
                {
                    desc = desc + (isPt ? " Aviso: " : " Warning: ") + string.Join(" ", snapshot.Win11Compat.Warnings.ToArray());
                }
                if (snapshot.Win11Compat.HardGateFailures.Count > 0)
                {
                    desc = desc + (isPt ? " Bloqueado por: " : " Blocked by: ") + string.Join(" ", snapshot.Win11Compat.HardGateFailures.ToArray());
                }
                w11Item.Description = desc;
                w11Item.ActionLabel = isPt ? "Verificador de Compatibilidade" : "Compatibility Checker";
                w11Item.ActionTag = "open_compat_checker";
                w11Item.Notes = snapshot.Win11Compat.RecommendedOsEdition;
                list.Add(w11Item);
            }

            // 3. Known Issues & Hardware Quirks
            if (quirkItems != null && quirkItems.Count > 0)
            {
                for (int i = 0; i < quirkItems.Count; i++)
                {
                    list.Add(quirkItems[i]);
                }
            }
        }

        public bool ApplyQuickRegistryFix(DiagnosticRecommendationItem item, Action<string> log = null, string customBackupDir = null)
        {
            if (item == null || string.IsNullOrEmpty(item.TargetRegistryPath) || string.IsNullOrEmpty(item.TargetValueName))
            {
                return false;
            }

            try
            {
                string safeId = string.IsNullOrEmpty(item.Id) ? "quickfix" : item.Id.Replace(" ", "_");
                string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_quickfix_" + safeId;
                string fullKey = item.TargetRegistryPath;
                string vName = item.TargetValueName;
                string kStr = item.TargetValueKind ?? "DWord";
                string targetVal = item.TargetValueData ?? "0";

                // 1. Read current state for snapshot backup
                object prevVal = null;
                RegistryValueKind prevKind = RegistryValueKind.DWord;
                bool existedBefore = false;

                string subKeyPath;
                RegistryKey rootHive = GetHiveFromPath(fullKey, out subKeyPath);
                if (rootHive != null)
                {
                    using (RegistryKey k = rootHive.OpenSubKey(subKeyPath, false))
                    {
                        if (k != null)
                        {
                            prevVal = k.GetValue(vName, null);
                            if (prevVal != null)
                            {
                                existedBefore = true;
                                prevKind = k.GetValueKind(vName);
                            }
                        }
                    }

                    // 2. Save DebloatBackupSession-compatible JSON snapshot before writing
                    SaveQuickFixBackupSnapshot(sessionId, safeId, fullKey, vName, existedBefore, prevKind, prevVal, kStr, customBackupDir, log);

                    // 3. Apply new value
                    using (RegistryKey kWrite = rootHive.CreateSubKey(subKeyPath))
                    {
                        if (kWrite != null)
                        {
                            if (string.Equals(kStr, "DWord", StringComparison.OrdinalIgnoreCase))
                            {
                                int dv;
                                int.TryParse(targetVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out dv);
                                kWrite.SetValue(vName, dv, RegistryValueKind.DWord);
                            }
                            else if (string.Equals(kStr, "Binary", StringComparison.OrdinalIgnoreCase))
                            {
                                byte[] bytes = HexStringToByteArray(targetVal);
                                kWrite.SetValue(vName, bytes, RegistryValueKind.Binary);
                            }
                            else
                            {
                                kWrite.SetValue(vName, targetVal, RegistryValueKind.String);
                            }

                            if (log != null)
                            {
                                log(string.Format("[QuickFix] Set {0}\\{1} = {2} ({3}) [Snapshot: {4}.json]", fullKey, vName, targetVal, kStr, sessionId));
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("[QuickFix:ERROR] Failed: " + ex.Message);
            }

            return false;
        }

        private static void SaveQuickFixBackupSnapshot(
            string sessionId,
            string tweakId,
            string fullKey,
            string valueName,
            bool existedBefore,
            RegistryValueKind prevKind,
            object prevVal,
            string fallbackKindStr,
            string customBackupDir,
            Action<string> log)
        {
            try
            {
                string dir = !string.IsNullOrEmpty(customBackupDir)
                    ? customBackupDir
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Config\Debloater\backups");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string kindStr = fallbackKindStr ?? "DWord";
                string prevValStr = "";
                if (existedBefore && prevVal != null)
                {
                    if (prevKind == RegistryValueKind.Binary)
                    {
                        kindStr = "Binary";
                        byte[] b = prevVal as byte[];
                        prevValStr = (b != null) ? Convert.ToBase64String(b) : "";
                    }
                    else if (prevKind == RegistryValueKind.DWord)
                    {
                        kindStr = "DWord";
                        prevValStr = Convert.ToInt32(prevVal, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (prevKind == RegistryValueKind.QWord)
                    {
                        kindStr = "QWord";
                        prevValStr = Convert.ToInt64(prevVal, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        kindStr = "String";
                        prevValStr = Convert.ToString(prevVal, CultureInfo.InvariantCulture);
                    }
                }

                int build = 0;
                string edition = "";
                try
                {
                    PlatformDiagnosticSnapshot snap = Instance._cachedSnapshot;
                    if (snap != null && snap.Platform != null && snap.Platform.OsFirmware != null)
                    {
                        build = snap.Platform.OsFirmware.CurrentBuild;
                        edition = snap.Platform.OsFirmware.EditionId ?? "";
                    }
                }
                catch { }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"sessionId\": \"{0}\",", EscapeJsonStr(sessionId)));
                sb.AppendLine(string.Format("  \"timestamp\": \"{0}\",", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
                sb.AppendLine(string.Format("  \"osBuild\": {0},", build));
                sb.AppendLine(string.Format("  \"osEdition\": \"{0}\",", EscapeJsonStr(edition)));
                sb.AppendLine(string.Format("  \"tweakIds\": [\"{0}\"],", EscapeJsonStr(tweakId)));
                sb.AppendLine("  \"entries\": [");
                sb.AppendLine("    {");
                sb.AppendLine(string.Format("      \"tweakId\": \"{0}\",", EscapeJsonStr(tweakId)));
                sb.AppendLine("      \"entryType\": \"registry\",");
                sb.AppendLine(string.Format("      \"targetPath\": \"{0}\",", EscapeJsonStr(fullKey)));
                sb.AppendLine(string.Format("      \"valueName\": \"{0}\",", EscapeJsonStr(valueName)));
                sb.AppendLine(string.Format("      \"existedBefore\": {0},", existedBefore ? "true" : "false"));
                sb.AppendLine(string.Format("      \"previousKind\": \"{0}\",", EscapeJsonStr(kindStr)));
                sb.AppendLine(string.Format("      \"previousValue\": \"{0}\",", EscapeJsonStr(prevValStr)));
                sb.AppendLine("      \"wasRunning\": false");
                sb.AppendLine("    }");
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                string outPath = Path.Combine(dir, sessionId + ".json");
                File.WriteAllText(outPath, sb.ToString(), new System.Text.UTF8Encoding(false));
                if (log != null) log("[QuickFix:SNAPSHOT] Saved rollback snapshot to " + outPath);
            }
            catch (Exception ex)
            {
                if (log != null) log("[QuickFix:SNAPSHOT:WARN] " + ex.Message);
            }
        }

        private static string EscapeJsonStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static RegistryKey GetHiveFromPath(string fullPath, out string subKeyPath)
        {
            subKeyPath = "";
            if (string.IsNullOrEmpty(fullPath)) return null;

            int slashIdx = fullPath.IndexOf('\\');
            string hiveName = (slashIdx >= 0) ? fullPath.Substring(0, slashIdx).Trim().ToUpperInvariant() : fullPath.Trim().ToUpperInvariant();
            subKeyPath = (slashIdx >= 0) ? fullPath.Substring(slashIdx + 1) : "";

            if (hiveName == "HKLM" || hiveName == "HKEY_LOCAL_MACHINE") return Registry.LocalMachine;
            if (hiveName == "HKCU" || hiveName == "HKEY_CURRENT_USER") return Registry.CurrentUser;
            if (hiveName == "HKU" || hiveName == "HKEY_USERS") return Registry.Users;
            return Registry.LocalMachine;
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            int numChars = hex.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
