using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Win32;

namespace OptiAssist.Platform
{
    public class KnownIssueDefinition
    {
        public string Id { get; set; }
        public string Severity { get; set; }
        public string TitleKey { get; set; }
        public string TitleFallback { get; set; }
        public string DescKey { get; set; }
        public string DescFallback { get; set; }
        public List<string> PciVendors { get; set; }
        public List<string> PciDevices { get; set; }
        public List<string> ChipsetKeywords { get; set; }
        public List<string> OemKeywords { get; set; }
        public List<string> ModelKeywords { get; set; }
        public List<string> FormFactors { get; set; }
        public string CpuMicroarch { get; set; }
        public string SystemLocale { get; set; }
        public bool IsLaptopOnly { get; set; }
        public int MinBuild { get; set; }
        public string ActionType { get; set; }
        public string ActionLabel { get; set; }
        public string ActionTag { get; set; }
        public string TargetPath { get; set; }
        public string ValueName { get; set; }
        public string Kind { get; set; }
        public string TargetValue { get; set; }
        public string Notes { get; set; }

        public KnownIssueDefinition()
        {
            Id = "";
            Severity = "Info";
            TitleKey = "";
            TitleFallback = "";
            DescKey = "";
            DescFallback = "";
            PciVendors = new List<string>();
            PciDevices = new List<string>();
            ChipsetKeywords = new List<string>();
            OemKeywords = new List<string>();
            ModelKeywords = new List<string>();
            FormFactors = new List<string>();
            ActionType = "info";
            ActionLabel = "View";
            ActionTag = "inspect";
            TargetPath = "";
            ValueName = "";
            Kind = "DWord";
            TargetValue = "0";
            Notes = "";
        }
    }

    public class KnownIssuesEngine
    {
        private readonly List<KnownIssueDefinition> _catalog;
        private static KnownIssuesEngine _defaultInstance;
        private static readonly object _lock = new object();

        public static KnownIssuesEngine Default
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

        public KnownIssuesEngine(List<KnownIssueDefinition> catalog)
        {
            _catalog = catalog ?? new List<KnownIssueDefinition>();
        }

        public static KnownIssuesEngine LoadDefault()
        {
            string path = GetCatalogFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    return ParseFromJson(json);
                }
                catch { }
            }
            return new KnownIssuesEngine(new List<KnownIssueDefinition>());
        }

        public static string GetCatalogFilePath()
        {
            try
            {
                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    string candidate = Path.Combine(asmDir, @"Config\Data\known_issues.json");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, @"Config\Data\known_issues.json");
        }

        public List<DiagnosticRecommendationItem> EvaluateIssues(PlatformInfo info)
        {
            List<DiagnosticRecommendationItem> items = new List<DiagnosticRecommendationItem>();
            if (info == null || _catalog == null) return items;

            for (int i = 0; i < _catalog.Count; i++)
            {
                KnownIssueDefinition def = _catalog[i];
                if (def == null) continue;

                if (MatchesIssue(def, info))
                {
                    DiagnosticRecommendationItem item = CreateRecommendationItem(def, info);
                    if (item != null) items.Add(item);
                }
            }

            return items;
        }

        private bool MatchesIssue(KnownIssueDefinition def, PlatformInfo info)
        {
            // Build filter
            int curBuild = (info.OsFirmware != null) ? info.OsFirmware.CurrentBuild : 0;
            if (def.MinBuild > 0 && curBuild > 0 && curBuild < def.MinBuild)
            {
                return false;
            }

            // Laptop requirement
            bool isMobile = (info.FormFactor == ChassisFormFactor.Laptop || info.FormFactor == ChassisFormFactor.Ultraportable || info.HasBattery);
            if (def.IsLaptopOnly && !isMobile)
            {
                return false;
            }

            // FormFactors filter
            if (def.FormFactors != null && def.FormFactors.Count > 0)
            {
                string curFf = info.FormFactor.ToString().ToLowerInvariant();
                bool ffMatch = false;
                for (int f = 0; f < def.FormFactors.Count; f++)
                {
                    if (string.Equals(def.FormFactors[f], curFf, StringComparison.OrdinalIgnoreCase))
                    {
                        ffMatch = true;
                        break;
                    }
                }
                if (!ffMatch) return false;
            }

            // Locale filter
            if (!string.IsNullOrEmpty(def.SystemLocale))
            {
                string curLoc = CultureInfo.CurrentCulture.Name;
                if (!string.Equals(curLoc, def.SystemLocale, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Microarchitecture filter
            if (!string.IsNullOrEmpty(def.CpuMicroarch))
            {
                string curUarch = (info.Cpu != null && info.Cpu.Microarchitecture != null) ? info.Cpu.Microarchitecture : "";
                if (!string.Equals(curUarch, def.CpuMicroarch, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // OEM and Model keywords
            string oemStr = (info.Oem != null) ? (info.Oem.Manufacturer + " " + info.Oem.MotherboardManufacturer).ToLowerInvariant() : "";
            string modelStr = (info.Oem != null) ? (info.Oem.ProductName + " " + info.Oem.MotherboardProduct + " " + info.Oem.Family).ToLowerInvariant() : "";

            if (def.OemKeywords != null && def.OemKeywords.Count > 0)
            {
                bool oemMatch = false;
                for (int k = 0; k < def.OemKeywords.Count; k++)
                {
                    if (oemStr.IndexOf(def.OemKeywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        oemMatch = true;
                        break;
                    }
                }
                if (!oemMatch) return false;
            }

            if (def.ModelKeywords != null && def.ModelKeywords.Count > 0)
            {
                bool modelMatch = false;
                for (int m = 0; m < def.ModelKeywords.Count; m++)
                {
                    if (modelStr.IndexOf(def.ModelKeywords[m], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        modelMatch = true;
                        break;
                    }
                }
                if (!modelMatch) return false;
            }

            // PCI Vendors and Devices check
            if (def.PciVendors != null && def.PciVendors.Count > 0)
            {
                bool pciMatched = false;
                if (info.PnpDevices != null)
                {
                    for (int p = 0; p < info.PnpDevices.Count; p++)
                    {
                        PnpDeviceItem dev = info.PnpDevices[p];
                        if (dev == null) continue;

                        bool vendorOk = false;
                        for (int v = 0; v < def.PciVendors.Count; v++)
                        {
                            if (string.Equals(dev.VendorId, def.PciVendors[v], StringComparison.OrdinalIgnoreCase))
                            {
                                vendorOk = true;
                                break;
                            }
                        }

                        if (!vendorOk) continue;

                        if (def.PciDevices == null || def.PciDevices.Count == 0)
                        {
                            pciMatched = true;
                            break;
                        }

                        for (int d = 0; d < def.PciDevices.Count; d++)
                        {
                            if (string.Equals(dev.PciDeviceId, def.PciDevices[d], StringComparison.OrdinalIgnoreCase))
                            {
                                pciMatched = true;
                                break;
                            }
                        }

                        if (pciMatched) break;
                    }
                }

                if (!pciMatched) return false;
            }

            return true;
        }

        private DiagnosticRecommendationItem CreateRecommendationItem(KnownIssueDefinition def, PlatformInfo info)
        {
            DiagnosticRecommendationItem item = new DiagnosticRecommendationItem();
            item.Id = def.Id;
            item.Category = "HardwareQuirk";

            RecommendationSeverity sev;
            if (string.Equals(def.Severity, "Critical", StringComparison.OrdinalIgnoreCase)) sev = RecommendationSeverity.Critical;
            else if (string.Equals(def.Severity, "Warning", StringComparison.OrdinalIgnoreCase)) sev = RecommendationSeverity.Warning;
            else sev = RecommendationSeverity.Info;
            item.Severity = sev;

            item.Title = def.TitleFallback;
            item.Description = def.DescFallback;
            item.ActionTag = def.ActionTag;
            item.ActionLabel = def.ActionLabel;
            item.TargetRegistryPath = def.TargetPath;
            item.TargetValueName = def.ValueName;
            item.TargetValueKind = def.Kind;
            item.TargetValueData = def.TargetValue;
            item.Notes = def.Notes;

            // Handle Dynamic ULPS Registry Key Search for AMD Display subkeys
            if (string.Equals(def.ActionType, "registry_ulps", StringComparison.OrdinalIgnoreCase))
            {
                string foundKey = FindAmdUlpsRegistryKey();
                if (!string.IsNullOrEmpty(foundKey))
                {
                    item.TargetRegistryPath = foundKey;
                }
            }

            return item;
        }

        public static string FindAmdUlpsRegistryKey()
        {
            string classBase = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(classBase, false))
                {
                    if (baseKey != null)
                    {
                        string[] subNames = baseKey.GetSubKeyNames();
                        if (subNames != null)
                        {
                            for (int i = 0; i < subNames.Length; i++)
                            {
                                string sub = subNames[i];
                                if (sub.Length == 4 && char.IsDigit(sub[0]))
                                {
                                    using (RegistryKey sk = baseKey.OpenSubKey(sub, false))
                                    {
                                        if (sk != null)
                                        {
                                            object ulpsVal = sk.GetValue("EnableUlps", null);
                                            object desc = sk.GetValue("DriverDesc", null);
                                            string descStr = (desc != null) ? desc.ToString() : "";

                                            if (ulpsVal != null || descStr.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 || descStr.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                return @"HKLM\" + classBase + @"\" + sub;
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

            return @"HKLM\" + classBase + @"\0000";
        }

        public static KnownIssuesEngine ParseFromJson(string json)
        {
            List<KnownIssueDefinition> list = new List<KnownIssueDefinition>();
            if (string.IsNullOrWhiteSpace(json)) return new KnownIssuesEngine(list);

            try
            {
                int issuesIdx = json.IndexOf("\"issues\"");
                if (issuesIdx < 0) return new KnownIssuesEngine(list);

                int arrayStart = json.IndexOf('[', issuesIdx);
                if (arrayStart < 0) return new KnownIssuesEngine(list);

                int depth = 0;
                int objStart = -1;
                bool inQuotes = false;
                bool escape = false;

                for (int i = arrayStart + 1; i < json.Length; i++)
                {
                    char c = json[i];
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') { inQuotes = !inQuotes; continue; }
                    if (inQuotes) continue;

                    if (c == '{')
                    {
                        if (depth == 0) objStart = i;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0 && objStart >= 0)
                        {
                            string b = json.Substring(objStart, i - objStart + 1);
                            KnownIssueDefinition def = ParseIssueBlock(b);
                            if (def != null && !string.IsNullOrEmpty(def.Id))
                            {
                                list.Add(def);
                            }
                            objStart = -1;
                        }
                    }
                    else if (c == ']' && depth == 0)
                    {
                        break;
                    }
                }
            }
            catch { }

            return new KnownIssuesEngine(list);
        }

        private static KnownIssueDefinition ParseIssueBlock(string b)
        {
            KnownIssueDefinition def = new KnownIssueDefinition();
            def.Id = ExtractJsonString(b, "id");
            def.Severity = ExtractJsonString(b, "severity");
            def.TitleKey = ExtractJsonString(b, "titleKey");
            def.TitleFallback = ExtractJsonString(b, "titleFallback");
            def.DescKey = ExtractJsonString(b, "descKey");
            def.DescFallback = ExtractJsonString(b, "descFallback");
            def.ActionType = ExtractJsonString(b, "actionType");
            def.ActionLabel = ExtractJsonString(b, "actionLabel");
            def.ActionTag = ExtractJsonString(b, "actionTag");
            def.TargetPath = ExtractJsonString(b, "targetPath");
            def.ValueName = ExtractJsonString(b, "valueName");
            def.Kind = ExtractJsonString(b, "kind");
            def.TargetValue = ExtractJsonString(b, "value");
            def.Notes = ExtractJsonString(b, "notes");
            def.CpuMicroarch = ExtractJsonString(b, "cpuMicroarch");
            def.SystemLocale = ExtractJsonString(b, "systemLocale");
            def.MinBuild = ExtractJsonInt(b, "minBuild");
            def.IsLaptopOnly = ExtractJsonBool(b, "isLaptop");

            // Arrays
            ExtractJsonStringArray(b, "pciVendors", def.PciVendors);
            ExtractJsonStringArray(b, "pciDevices", def.PciDevices);
            ExtractJsonStringArray(b, "chipsetKeywords", def.ChipsetKeywords);
            ExtractJsonStringArray(b, "oemKeywords", def.OemKeywords);
            ExtractJsonStringArray(b, "modelKeywords", def.ModelKeywords);
            ExtractJsonStringArray(b, "formFactors", def.FormFactors);

            return def;
        }

        private static string ExtractJsonString(string block, string key)
        {
            Match m = Regex.Match(block, "\"" + key + @"\""\s*:\s*\""([^\""]*)\""");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static int ExtractJsonInt(string block, string key)
        {
            Match m = Regex.Match(block, "\"" + key + @"\""\s*:\s*(-?\d+)");
            if (m.Success)
            {
                int v;
                if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            }
            return 0;
        }

        private static bool ExtractJsonBool(string block, string key)
        {
            Match m = Regex.Match(block, "\"" + key + @"\""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            return m.Success && string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void ExtractJsonStringArray(string block, string key, List<string> targetList)
        {
            Match m = Regex.Match(block, "\"" + key + @"\""\s*:\s*\[([^\]]*)\]");
            if (m.Success)
            {
                string inner = m.Groups[1].Value;
                MatchCollection matches = Regex.Matches(inner, @"""([^""]+)""");
                for (int i = 0; i < matches.Count; i++)
                {
                    targetList.Add(matches[i].Groups[1].Value);
                }
            }
        }
    }
}
