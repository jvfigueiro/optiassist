using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace OptiAssist.Platform
{
    public class MicroarchEntry
    {
        public string Vendor { get; set; }
        public int Family { get; set; }
        public List<int> Models { get; set; }
        public List<int> Steppings { get; set; }
        public string Uarch { get; set; }
        public int Year { get; set; }
        public string MarketingGen { get; set; }
        public bool HasSse42 { get; set; }
        public bool HasCmpxchg16b { get; set; }
        public string ClassName { get; set; }

        public MicroarchEntry()
        {
            Vendor = "";
            Models = new List<int>();
            Steppings = new List<int>();
            Uarch = "";
            MarketingGen = "";
            ClassName = "desktop";
        }
    }

    public class MicroarchMatchResult
    {
        public string Microarchitecture { get; set; }
        public int ReleaseYear { get; set; }
        public string MarketingGen { get; set; }
        public bool HasSse42 { get; set; }
        public bool HasCmpxchg16b { get; set; }
        public string Suffix { get; set; }
        public string ClassName { get; set; }

        public MicroarchMatchResult()
        {
            Microarchitecture = "Unknown";
            ReleaseYear = 0;
            MarketingGen = "";
            Suffix = "";
            ClassName = "unknown";
        }
    }

    public class MicroarchDatabase
    {
        private readonly List<MicroarchEntry> _entries;
        private static MicroarchDatabase _defaultInstance;
        private static readonly object _lock = new object();

        public static MicroarchDatabase Default
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

        public MicroarchDatabase(List<MicroarchEntry> entries)
        {
            _entries = entries ?? new List<MicroarchEntry>();
        }

        public static MicroarchDatabase LoadDefault()
        {
            string dbPath = GetDatabaseFilePath();
            if (File.Exists(dbPath))
            {
                try
                {
                    string json = File.ReadAllText(dbPath, Encoding.UTF8);
                    return ParseFromJson(json);
                }
                catch { }
            }
            return new MicroarchDatabase(new List<MicroarchEntry>());
        }

        public static string GetDatabaseFilePath()
        {
            try
            {
                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    string candidate = Path.Combine(asmDir, @"Config\Data\microarch.json");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, @"Config\Data\microarch.json");
        }

        public MicroarchMatchResult Match(string vendor, int family, int model, int stepping, string nameString)
        {
            MicroarchMatchResult res = new MicroarchMatchResult();
            string vClean = (vendor ?? "").Trim();
            string nameClean = (nameString ?? "").Trim();
            res.Suffix = DetectCpuSuffix(nameClean);

            // Disambiguation for Intel Family 6 Models 142 (0x8E) and 158 (0x9E)
            if (vClean.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 && family == 6 && (model == 142 || model == 158))
            {
                MicroarchMatchResult disambiguated = DisambiguateIntel8E9E(model, stepping, nameClean);
                if (disambiguated != null)
                {
                    disambiguated.Suffix = res.Suffix;
                    return disambiguated;
                }
            }

            // Standard Lookup
            MicroarchEntry bestMatch = null;
            for (int i = 0; i < _entries.Count; i++)
            {
                MicroarchEntry e = _entries[i];
                if (e == null) continue;

                if (!string.IsNullOrEmpty(e.Vendor) &&
                    vClean.IndexOf(e.Vendor, StringComparison.OrdinalIgnoreCase) < 0 &&
                    e.Vendor.IndexOf(vClean, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (e.Family != family) continue;

                if (e.Models.Contains(model))
                {
                    if (e.Steppings != null && e.Steppings.Count > 0)
                    {
                        if (e.Steppings.Contains(stepping))
                        {
                            bestMatch = e;
                            break;
                        }
                    }
                    else if (bestMatch == null)
                    {
                        bestMatch = e;
                    }
                }
            }

            if (bestMatch != null)
            {
                res.Microarchitecture = bestMatch.Uarch;
                res.ReleaseYear = bestMatch.Year;
                res.MarketingGen = bestMatch.MarketingGen;
                res.HasSse42 = bestMatch.HasSse42;
                res.HasCmpxchg16b = bestMatch.HasCmpxchg16b;
                res.ClassName = bestMatch.ClassName;
                return res;
            }

            // Fallback heuristics based on nameString
            res = FallbackByName(nameClean, vClean);
            res.Suffix = DetectCpuSuffix(nameClean);
            return res;
        }

        private static MicroarchMatchResult DisambiguateIntel8E9E(int model, int stepping, string nameString)
        {
            // Name matching: i[3579]-(\d{4,5})
            Match m = Regex.Match(nameString, @"i[3579]-(\d{4,5})", RegexOptions.IgnoreCase);
            int genNum = 0;
            if (m.Success)
            {
                string numStr = m.Groups[1].Value;
                if (numStr.Length == 4)
                {
                    int.TryParse(numStr.Substring(0, 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out genNum);
                }
                else if (numStr.Length == 5)
                {
                    int.TryParse(numStr.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out genNum);
                }
            }

            MicroarchMatchResult res = new MicroarchMatchResult();
            res.HasSse42 = true;
            res.HasCmpxchg16b = true;

            if (model == 142) // Mobile (0x8E)
            {
                res.ClassName = "mobile";
                if (stepping >= 11 || genNum >= 10)
                {
                    res.Microarchitecture = "Whiskey Lake";
                    res.ReleaseYear = 2018;
                    res.MarketingGen = "8th/10th Gen Core Mobile (Whiskey Lake)";
                    return res;
                }
                if (stepping == 10 || genNum == 8)
                {
                    res.Microarchitecture = "Coffee Lake";
                    res.ReleaseYear = 2018;
                    res.MarketingGen = "8th Gen Core Mobile (Kaby Lake-R / Coffee Lake)";
                    return res;
                }
                if (stepping == 9 || genNum == 7)
                {
                    res.Microarchitecture = "Kaby Lake";
                    res.ReleaseYear = 2016;
                    res.MarketingGen = "7th Gen Core Mobile";
                    return res;
                }
            }
            else if (model == 158) // Desktop (0x9E)
            {
                res.ClassName = "desktop";
                if (stepping >= 11 || genNum == 9)
                {
                    res.Microarchitecture = "Coffee Lake";
                    res.ReleaseYear = 2018;
                    res.MarketingGen = "9th Gen Core Desktop (Coffee Lake Refresh)";
                    return res;
                }
                if (stepping == 10 || genNum == 8)
                {
                    res.Microarchitecture = "Coffee Lake";
                    res.ReleaseYear = 2017;
                    res.MarketingGen = "8th Gen Core Desktop";
                    return res;
                }
                if (stepping == 9 || genNum == 7)
                {
                    res.Microarchitecture = "Kaby Lake";
                    res.ReleaseYear = 2017;
                    res.MarketingGen = "7th Gen Core Desktop";
                    return res;
                }
            }

            return null;
        }

        public static string DetectCpuSuffix(string nameString)
        {
            if (string.IsNullOrWhiteSpace(nameString)) return "";
            string s = nameString.ToUpperInvariant();

            if (s.IndexOf("X3D", StringComparison.Ordinal) >= 0) return "X3D";
            if (s.IndexOf("HX", StringComparison.Ordinal) >= 0) return "HX";
            if (s.IndexOf("HK", StringComparison.Ordinal) >= 0) return "HK";
            if (s.IndexOf("KF", StringComparison.Ordinal) >= 0) return "KF";

            Match mSuffix = Regex.Match(s, @"\b\d{3,5}([A-Z]{1,3})\b");
            if (mSuffix.Success)
            {
                string suf = mSuffix.Groups[1].Value;
                if (suf == "U" || suf == "Y" || suf == "P" || suf == "H" || suf == "K" || suf == "F" || suf == "T" || suf == "G")
                {
                    return suf;
                }
            }

            Match mIntelN = Regex.Match(s, @"\bN\d{2,3}\b");
            if (mIntelN.Success) return "N";

            return "";
        }

        private static MicroarchMatchResult FallbackByName(string name, string vendor)
        {
            MicroarchMatchResult res = new MicroarchMatchResult();
            string upper = name.ToUpperInvariant();

            if (upper.IndexOf("RYZEN", StringComparison.Ordinal) >= 0)
            {
                res.HasSse42 = true;
                res.HasCmpxchg16b = true;
                if (upper.IndexOf(" 9", StringComparison.Ordinal) >= 0 || upper.IndexOf("9000", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Zen 5";
                    res.ReleaseYear = 2024;
                    res.MarketingGen = "Ryzen 9000";
                }
                else if (upper.IndexOf("7000", StringComparison.Ordinal) >= 0 || upper.IndexOf("8000", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Zen 4";
                    res.ReleaseYear = 2022;
                    res.MarketingGen = "Ryzen 7000/8000";
                }
                else if (upper.IndexOf("5000", StringComparison.Ordinal) >= 0 || upper.IndexOf("6000", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Zen 3";
                    res.ReleaseYear = 2020;
                    res.MarketingGen = "Ryzen 5000";
                }
                else if (upper.IndexOf("3000", StringComparison.Ordinal) >= 0 || upper.IndexOf("4000", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Zen 2";
                    res.ReleaseYear = 2019;
                    res.MarketingGen = "Ryzen 3000/4000";
                }
                else
                {
                    res.Microarchitecture = "Zen";
                    res.ReleaseYear = 2017;
                    res.MarketingGen = "Ryzen 1000/2000";
                }
                res.ClassName = "desktop/mobile";
                return res;
            }

            if (upper.IndexOf("CORE(TM)", StringComparison.Ordinal) >= 0 || upper.IndexOf("INTEL", StringComparison.Ordinal) >= 0)
            {
                res.HasSse42 = true;
                res.HasCmpxchg16b = true;
                if (upper.IndexOf("14TH GEN", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Raptor Lake";
                    res.ReleaseYear = 2023;
                    res.MarketingGen = "14th Gen Core";
                    res.ClassName = "hybrid";
                }
                else if (upper.IndexOf("13TH GEN", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Raptor Lake";
                    res.ReleaseYear = 2022;
                    res.MarketingGen = "13th Gen Core";
                    res.ClassName = "hybrid";
                }
                else if (upper.IndexOf("12TH GEN", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Alder Lake";
                    res.ReleaseYear = 2021;
                    res.MarketingGen = "12th Gen Core";
                    res.ClassName = "hybrid";
                }
                else if (upper.IndexOf("11TH GEN", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Tiger Lake";
                    res.ReleaseYear = 2020;
                    res.MarketingGen = "11th Gen Core";
                }
                else if (upper.IndexOf("10TH GEN", StringComparison.Ordinal) >= 0)
                {
                    res.Microarchitecture = "Comet Lake";
                    res.ReleaseYear = 2020;
                    res.MarketingGen = "10th Gen Core";
                }
                else
                {
                    res.Microarchitecture = "Core";
                    res.ReleaseYear = 2015;
                    res.MarketingGen = "Intel Core";
                }
                return res;
            }

            return res;
        }

        public static MicroarchDatabase ParseFromJson(string json)
        {
            List<MicroarchEntry> list = new List<MicroarchEntry>();
            if (string.IsNullOrWhiteSpace(json)) return new MicroarchDatabase(list);

            try
            {
                // Simple regex/block parser for microarch.json without external deps
                MatchCollection blocks = Regex.Matches(json, @"\{[^{}]*""uarch""[^{}]*\}", RegexOptions.Singleline);
                for (int i = 0; i < blocks.Count; i++)
                {
                    string block = blocks[i].Value;
                    MicroarchEntry e = new MicroarchEntry();
                    e.Vendor = ExtractJsonString(block, "vendor");
                    e.Uarch = ExtractJsonString(block, "uarch");
                    e.MarketingGen = ExtractJsonString(block, "marketingGen");
                    e.ClassName = ExtractJsonString(block, "class");
                    e.Family = ExtractJsonInt(block, "family");
                    e.Year = ExtractJsonInt(block, "year");
                    e.HasSse42 = ExtractJsonBool(block, "hasSSE42");
                    e.HasCmpxchg16b = ExtractJsonBool(block, "hasCMPXCHG16B");

                    Match mModels = Regex.Match(block, @"""models""\s*:\s*\[([^\]]*)\]");
                    if (mModels.Success)
                    {
                        string[] parts = mModels.Groups[1].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int p = 0; p < parts.Length; p++)
                        {
                            int val;
                            if (int.TryParse(parts[p].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
                            {
                                e.Models.Add(val);
                            }
                        }
                    }

                    Match mSteppings = Regex.Match(block, @"""steppings""\s*:\s*\[([^\]]*)\]");
                    if (mSteppings.Success)
                    {
                        string[] parts = mSteppings.Groups[1].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int p = 0; p < parts.Length; p++)
                        {
                            int val;
                            if (int.TryParse(parts[p].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
                            {
                                e.Steppings.Add(val);
                            }
                        }
                    }

                    list.Add(e);
                }
            }
            catch { }

            return new MicroarchDatabase(list);
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
    }
}
