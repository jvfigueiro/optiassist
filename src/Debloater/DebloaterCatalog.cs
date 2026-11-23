using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace OptiAssist.Debloater
{
    public enum DebloatTweakState
    {
        Pending = 0,
        Applied = 1,
        Partial = 2,
        NotApplicable = 3
    }

    public class DebloatAppliesTo
    {
        public int minBuild { get; set; }
        public int maxBuild { get; set; }
        public List<string> editions { get; set; }
        public List<string> formFactors { get; set; }
        public string requiresInstalled { get; set; }
        public string condition { get; set; }

        public DebloatAppliesTo()
        {
            minBuild = 17763;
            maxBuild = 0;
            editions = new List<string>() { "*" };
            formFactors = new List<string>() { "desktop", "laptop", "ultraportable" };
            requiresInstalled = "";
            condition = "";
        }
    }

    public class DebloatProfiles
    {
        public string gaming { get; set; }
        public string workstation { get; set; }
        public string hybrid { get; set; }
        public string conventional { get; set; }
        public string laptop { get; set; }
        public string laptopGaming { get; set; }
        public string ultraportable { get; set; }

        public DebloatProfiles()
        {
            gaming = "off";
            workstation = "off";
            hybrid = "off";
            conventional = "off";
            laptop = "off";
            laptopGaming = "off";
            ultraportable = "off";
        }

        public string GetModeForProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return "off";
            string p = profileId.Trim().ToLowerInvariant();
            if (p == "gaming") return gaming ?? "off";
            if (p == "workstation") return workstation ?? "off";
            if (p == "hybrid") return hybrid ?? "off";
            if (p == "conventional") return conventional ?? "off";
            if (p == "laptop") return laptop ?? "off";
            if (p == "laptopgaming" || p == "laptop_gaming") return laptopGaming ?? "off";
            if (p == "ultraportable") return ultraportable ?? "off";
            return "off";
        }
    }

    public class DebloatDetectRule
    {
        public string type { get; set; }
        public string path { get; set; }
        public string name { get; set; }
        public string equals { get; set; }
        public bool editionAwareTelemetry { get; set; }
        public int minBuild { get; set; }
        public int maxBuild { get; set; }

        public DebloatDetectRule()
        {
            type = "registry";
            path = "";
            name = "";
            equals = "";
            editionAwareTelemetry = false;
            minBuild = 0;
            maxBuild = 0;
        }
    }

    public class DebloatActionStep
    {
        public string type { get; set; }
        public string operation { get; set; }
        public string path { get; set; }
        public string name { get; set; }
        public string kind { get; set; }
        public string value { get; set; }
        public bool includeDefaultUser { get; set; }
        public bool editionAwareTelemetry { get; set; }
        public string startupType { get; set; }
        public bool stopService { get; set; }
        public string subGroupGuid { get; set; }
        public string settingGuid { get; set; }
        public string state { get; set; }
        public string pattern { get; set; }
        public bool allUsers { get; set; }
        public string stopProcesses { get; set; }
        public string spiAction { get; set; }
        public int uiParam { get; set; }
        public bool pvParamBool { get; set; }
        public string shell { get; set; }
        public string command { get; set; }
        public bool skipIfTamperProtected { get; set; }
        public int minBuild { get; set; }
        public int maxBuild { get; set; }

        public DebloatActionStep()
        {
            type = "registry";
            operation = "set";
            path = "";
            name = "";
            kind = "DWord";
            value = "";
            includeDefaultUser = false;
            editionAwareTelemetry = false;
            startupType = "Disabled";
            stopService = true;
            subGroupGuid = "";
            settingGuid = "";
            state = "";
            pattern = "";
            allUsers = true;
            stopProcesses = "";
            spiAction = "";
            uiParam = 0;
            pvParamBool = false;
            shell = "powershell";
            command = "";
            skipIfTamperProtected = false;
            minBuild = 0;
            maxBuild = 0;
        }
    }

    public class DebloatTweakDefinition
    {
        public string id { get; set; }
        public string category { get; set; }
        public string titleKey { get; set; }
        public string titleFallback { get; set; }
        public string descKey { get; set; }
        public string descFallback { get; set; }
        public string risk { get; set; }
        public DebloatAppliesTo appliesTo { get; set; }
        public DebloatProfiles profiles { get; set; }
        public List<DebloatDetectRule> detect { get; set; }
        public List<DebloatActionStep> apply { get; set; }
        public string revert { get; set; }
        public List<string> warnings { get; set; }
        public bool needsReboot { get; set; }
        public bool doubleConfirm { get; set; }

        // Runtime evaluated state
        public DebloatTweakState CurrentState { get; set; }
        public bool HasBackupSnapshot { get; set; }
        public bool IsApplicable { get; set; }
        public string InapplicableReason { get; set; }
        public string HardwareConditionBadge { get; set; }
        public string HardwareConditionReason { get; set; }
        public string QuickActionUri { get; set; }
        public string QuickActionLabel { get; set; }
        public bool ForceUncheckedByCondition { get; set; }

        public DebloatTweakDefinition()
        {
            id = "";
            category = "general";
            titleKey = "";
            titleFallback = "";
            descKey = "";
            descFallback = "";
            risk = "low";
            appliesTo = new DebloatAppliesTo();
            profiles = new DebloatProfiles();
            detect = new List<DebloatDetectRule>();
            apply = new List<DebloatActionStep>();
            revert = "from-snapshot";
            warnings = new List<string>();
            needsReboot = false;
            doubleConfirm = false;
            CurrentState = DebloatTweakState.Pending;
            HasBackupSnapshot = false;
            IsApplicable = true;
            InapplicableReason = "";
            HardwareConditionBadge = "";
            HardwareConditionReason = "";
            QuickActionUri = "";
            QuickActionLabel = "";
            ForceUncheckedByCondition = false;
        }
    }

    public class DebloatBackupEntry
    {
        public string tweakId { get; set; }
        public string entryType { get; set; }
        public string targetPath { get; set; }
        public string valueName { get; set; }
        public bool existedBefore { get; set; }
        public string previousKind { get; set; }
        public string previousValue { get; set; }
        public bool wasRunning { get; set; }

        public DebloatBackupEntry()
        {
            tweakId = "";
            entryType = "registry";
            targetPath = "";
            valueName = "";
            existedBefore = false;
            previousKind = "DWord";
            previousValue = "";
            wasRunning = false;
        }
    }

    public class DebloatBackupSession
    {
        public string sessionId { get; set; }
        public string timestamp { get; set; }
        public int osBuild { get; set; }
        public string osEdition { get; set; }
        public List<string> tweakIds { get; set; }
        public List<DebloatBackupEntry> entries { get; set; }

        public DebloatBackupSession()
        {
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            osBuild = 0;
            osEdition = "";
            tweakIds = new List<string>();
            entries = new List<DebloatBackupEntry>();
        }
    }

    /// <summary>
    /// Lightweight recursive-descent JSON DOM node and parser for C# 5 (.NET 4.8).
    /// </summary>
    public class DebloatJsonNode
    {
        public bool IsObject { get; set; }
        public bool IsArray { get; set; }
        public string StringValue { get; set; }
        public Dictionary<string, DebloatJsonNode> ObjectFields { get; set; }
        public List<DebloatJsonNode> ArrayItems { get; set; }

        public DebloatJsonNode()
        {
            ObjectFields = new Dictionary<string, DebloatJsonNode>(StringComparer.OrdinalIgnoreCase);
            ArrayItems = new List<DebloatJsonNode>();
            StringValue = "";
        }

        public string GetString(string key, string defaultVal)
        {
            DebloatJsonNode n;
            if (ObjectFields != null && ObjectFields.TryGetValue(key, out n) && n != null && n.StringValue != null)
            {
                return n.StringValue;
            }
            return defaultVal;
        }

        public int GetInt(string key, int defaultVal)
        {
            string s = GetString(key, "");
            int v;
            if (!string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }
            return defaultVal;
        }

        public bool GetBool(string key, bool defaultVal)
        {
            string s = GetString(key, "");
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
            if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
            return defaultVal;
        }

        public DebloatJsonNode GetObject(string key)
        {
            DebloatJsonNode n;
            if (ObjectFields != null && ObjectFields.TryGetValue(key, out n) && n != null && n.IsObject)
            {
                return n;
            }
            return null;
        }

        public List<DebloatJsonNode> GetArray(string key)
        {
            DebloatJsonNode n;
            if (ObjectFields != null && ObjectFields.TryGetValue(key, out n) && n != null && n.IsArray)
            {
                return n.ArrayItems;
            }
            return null;
        }

        public List<string> GetStringList(string key)
        {
            List<string> list = new List<string>();
            List<DebloatJsonNode> arr = GetArray(key);
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] != null && arr[i].StringValue != null)
                    {
                        list.Add(arr[i].StringValue);
                    }
                }
            }
            return list;
        }
    }

    public static class DebloaterCatalogLoader
    {
        public static string GetDebloaterBaseDirectory()
        {
            try
            {
                string asmLoc = typeof(DebloaterCatalogLoader).Assembly.Location;
                if (!string.IsNullOrEmpty(asmLoc))
                {
                    string asmDir = Path.GetDirectoryName(asmLoc);
                    if (!string.IsNullOrEmpty(asmDir))
                    {
                        string candidate = Path.Combine(asmDir, @"Config\Data\OptiBoost");
                        if (Directory.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, @"Config\Data\OptiBoost");
        }

        public static string GetDebloaterCatalogDirectory()
        {
            string dir = GetDebloaterBaseDirectory();
            if (Directory.Exists(dir))
            {
                return dir;
            }
            string legacyFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Config\Debloater");
            if (Directory.Exists(legacyFallback))
            {
                return legacyFallback;
            }
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        public static string CustomBackupsDirectoryOverride { get; set; }

        public static string GetBackupsDirectory()
        {
            string dir = !string.IsNullOrEmpty(CustomBackupsDirectoryOverride)
                ? CustomBackupsDirectoryOverride
                : Path.Combine(GetDebloaterBaseDirectory(), "backups");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public static List<DebloatTweakDefinition> LoadAllTweaks()
        {
            string dir = GetDebloaterCatalogDirectory();
            string[] files = new string[0];
            try
            {
                if (Directory.Exists(dir))
                {
                    files = Directory.GetFiles(dir, "*.json");
                }
            }
            catch { }

            List<DebloatTweakDefinition> all = new List<DebloatTweakDefinition>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (files != null)
            {
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string json = File.ReadAllText(files[i], Encoding.UTF8);
                        List<DebloatTweakDefinition> parsed = ParseCatalogJson(json);
                        for (int j = 0; j < parsed.Count; j++)
                        {
                            DebloatTweakDefinition item = parsed[j];
                            if (item != null && !string.IsNullOrEmpty(item.id) && !seenIds.Contains(item.id))
                            {
                                seenIds.Add(item.id);
                                all.Add(item);
                            }
                        }
                    }
                    catch { }
                }
            }

            return all;
        }

        public static List<DebloatTweakDefinition> ParseCatalogJson(string json)
        {
            List<DebloatTweakDefinition> list = new List<DebloatTweakDefinition>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            int idx = 0;
            DebloatJsonNode root = ParseJsonValue(json, ref idx);
            if (root == null) return list;

            List<DebloatJsonNode> items = null;
            if (root.IsArray)
            {
                items = root.ArrayItems;
            }
            else if (root.IsObject)
            {
                items = root.GetArray("tweaks");
                if (items == null && !string.IsNullOrEmpty(root.GetString("id", "")))
                {
                    items = new List<DebloatJsonNode>() { root };
                }
            }

            if (items == null) return list;

            for (int i = 0; i < items.Count; i++)
            {
                DebloatJsonNode node = items[i];
                if (node == null || !node.IsObject) continue;

                DebloatTweakDefinition def = new DebloatTweakDefinition();
                def.id = node.GetString("id", "");
                if (string.IsNullOrEmpty(def.id)) continue;

                def.category = node.GetString("category", "general");
                def.titleKey = node.GetString("titleKey", "");
                def.titleFallback = node.GetString("titleFallback", def.titleKey);
                def.descKey = node.GetString("descKey", "");
                def.descFallback = node.GetString("descFallback", "");
                def.risk = node.GetString("risk", "low");
                def.revert = node.GetString("revert", "from-snapshot");
                def.needsReboot = node.GetBool("needsReboot", false);
                def.doubleConfirm = node.GetBool("doubleConfirm", false);
                def.warnings = node.GetStringList("warnings");

                DebloatJsonNode appNode = node.GetObject("appliesTo");
                if (appNode != null)
                {
                    def.appliesTo.minBuild = appNode.GetInt("minBuild", 17763);
                    def.appliesTo.maxBuild = appNode.GetInt("maxBuild", 0);
                    List<string> eds = appNode.GetStringList("editions");
                    if (eds.Count > 0) def.appliesTo.editions = eds;
                    List<string> ffs = appNode.GetStringList("formFactors");
                    if (ffs.Count > 0) def.appliesTo.formFactors = ffs;
                    def.appliesTo.requiresInstalled = appNode.GetString("requiresInstalled", "");
                    def.appliesTo.condition = appNode.GetString("condition", "");
                }

                DebloatJsonNode profNode = node.GetObject("profiles");
                if (profNode != null)
                {
                    def.profiles.gaming = profNode.GetString("gaming", "off");
                    def.profiles.workstation = profNode.GetString("workstation", "off");
                    def.profiles.hybrid = profNode.GetString("hybrid", "off");
                    def.profiles.conventional = profNode.GetString("conventional", "off");
                    def.profiles.laptop = profNode.GetString("laptop", "off");
                    def.profiles.laptopGaming = profNode.GetString("laptopGaming", "off");
                    def.profiles.ultraportable = profNode.GetString("ultraportable", "off");
                }

                List<DebloatJsonNode> detectNodes = node.GetArray("detect");
                if (detectNodes == null)
                {
                    DebloatJsonNode singleDetect = node.GetObject("detect");
                    if (singleDetect != null)
                    {
                        detectNodes = new List<DebloatJsonNode>() { singleDetect };
                    }
                }
                if (detectNodes != null)
                {
                    for (int d = 0; d < detectNodes.Count; d++)
                    {
                        DebloatJsonNode dn = detectNodes[d];
                        if (dn == null || !dn.IsObject) continue;
                        DebloatDetectRule rule = new DebloatDetectRule();
                        rule.type = dn.GetString("type", "registry");
                        rule.path = dn.GetString("path", "");
                        rule.name = dn.GetString("name", "");
                        rule.equals = dn.GetString("equals", "");
                        rule.editionAwareTelemetry = dn.GetBool("editionAwareTelemetry", false);
                        rule.minBuild = dn.GetInt("minBuild", 0);
                        rule.maxBuild = dn.GetInt("maxBuild", 0);
                        def.detect.Add(rule);
                    }
                }

                List<DebloatJsonNode> applyNodes = node.GetArray("apply");
                if (applyNodes != null)
                {
                    for (int a = 0; a < applyNodes.Count; a++)
                    {
                        DebloatJsonNode an = applyNodes[a];
                        if (an == null || !an.IsObject) continue;
                        DebloatActionStep step = new DebloatActionStep();
                        step.type = an.GetString("type", "registry");
                        step.operation = an.GetString("operation", "set");
                        step.path = an.GetString("path", "");
                        step.name = an.GetString("name", "");
                        step.kind = an.GetString("kind", "DWord");
                        step.value = an.GetString("value", "");
                        step.includeDefaultUser = an.GetBool("includeDefaultUser", false);
                        step.editionAwareTelemetry = an.GetBool("editionAwareTelemetry", false);
                        step.startupType = an.GetString("startupType", "Disabled");
                        step.stopService = an.GetBool("stopService", true);
                        step.subGroupGuid = an.GetString("subGroupGuid", "");
                        step.settingGuid = an.GetString("settingGuid", "");
                        step.state = an.GetString("state", "");
                        step.pattern = an.GetString("pattern", "");
                        step.allUsers = an.GetBool("allUsers", true);
                        step.stopProcesses = an.GetString("stopProcesses", "");
                        step.spiAction = an.GetString("spiAction", "");
                        step.uiParam = an.GetInt("uiParam", 0);
                        step.pvParamBool = an.GetBool("pvParamBool", false);
                        step.shell = an.GetString("shell", "powershell");
                        step.command = an.GetString("command", "");
                        step.skipIfTamperProtected = an.GetBool("skipIfTamperProtected", false);
                        step.minBuild = an.GetInt("minBuild", 0);
                        step.maxBuild = an.GetInt("maxBuild", 0);
                        def.apply.Add(step);
                    }
                }

                list.Add(def);
            }

            return list;
        }

        public static string SerializeBackupSession(DebloatBackupSession session)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"sessionId\": \"" + EscapeJson(session.sessionId) + "\",");
            sb.AppendLine("  \"timestamp\": \"" + EscapeJson(session.timestamp) + "\",");
            sb.AppendLine("  \"osBuild\": " + session.osBuild.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"osEdition\": \"" + EscapeJson(session.osEdition) + "\",");
            sb.Append("  \"tweakIds\": [");
            for (int i = 0; i < session.tweakIds.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"" + EscapeJson(session.tweakIds[i]) + "\"");
            }
            sb.AppendLine("],");
            sb.AppendLine("  \"entries\": [");
            for (int i = 0; i < session.entries.Count; i++)
            {
                DebloatBackupEntry e = session.entries[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"tweakId\": \"" + EscapeJson(e.tweakId) + "\",");
                sb.AppendLine("      \"entryType\": \"" + EscapeJson(e.entryType) + "\",");
                sb.AppendLine("      \"targetPath\": \"" + EscapeJson(e.targetPath) + "\",");
                sb.AppendLine("      \"valueName\": \"" + EscapeJson(e.valueName) + "\",");
                sb.AppendLine("      \"existedBefore\": " + (e.existedBefore ? "true" : "false") + ",");
                sb.AppendLine("      \"previousKind\": \"" + EscapeJson(e.previousKind) + "\",");
                sb.AppendLine("      \"previousValue\": \"" + EscapeJson(e.previousValue) + "\",");
                sb.AppendLine("      \"wasRunning\": " + (e.wasRunning ? "true" : "false"));
                sb.Append("    }");
                if (i < session.entries.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static DebloatBackupSession ParseBackupSession(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            int idx = 0;
            DebloatJsonNode root = ParseJsonValue(json, ref idx);
            if (root == null || !root.IsObject) return null;

            DebloatBackupSession s = new DebloatBackupSession();
            s.sessionId = root.GetString("sessionId", "");
            s.timestamp = root.GetString("timestamp", "");
            s.osBuild = root.GetInt("osBuild", 0);
            s.osEdition = root.GetString("osEdition", "");
            s.tweakIds = root.GetStringList("tweakIds");

            List<DebloatJsonNode> arr = root.GetArray("entries");
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    DebloatJsonNode en = arr[i];
                    if (en == null || !en.IsObject) continue;
                    DebloatBackupEntry entry = new DebloatBackupEntry();
                    entry.tweakId = en.GetString("tweakId", "");
                    entry.entryType = en.GetString("entryType", "registry");
                    entry.targetPath = en.GetString("targetPath", "");
                    entry.valueName = en.GetString("valueName", "");
                    entry.existedBefore = en.GetBool("existedBefore", false);
                    entry.previousKind = en.GetString("previousKind", "DWord");
                    entry.previousValue = en.GetString("previousValue", "");
                    entry.wasRunning = en.GetBool("wasRunning", false);
                    s.entries.Add(entry);
                }
            }
            return s;
        }

        public static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') sb.Append("\\\\");
                else if (c == '\"') sb.Append("\\\"");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        #region RECURSIVE-DESCENT JSON PARSER

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    i++;
                }
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    i += 2;
                    while (i < s.Length && s[i] != '\n') i++;
                }
                else
                {
                    break;
                }
            }
        }

        public static DebloatJsonNode ParseJsonValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return null;

            char c = s[i];
            if (c == '{') return ParseJsonObject(s, ref i);
            if (c == '[') return ParseJsonArray(s, ref i);
            if (c == '\"')
            {
                DebloatJsonNode strNode = new DebloatJsonNode();
                strNode.StringValue = ParseJsonString(s, ref i);
                return strNode;
            }

            int start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && s[i] != ' ' && s[i] != '\t' && s[i] != '\r' && s[i] != '\n')
            {
                i++;
            }
            string raw = s.Substring(start, i - start).Trim();
            DebloatJsonNode prim = new DebloatJsonNode();
            prim.StringValue = (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase)) ? "" : raw;
            return prim;
        }

        private static DebloatJsonNode ParseJsonObject(string s, ref int i)
        {
            DebloatJsonNode obj = new DebloatJsonNode();
            obj.IsObject = true;
            i++; // skip '{'

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}')
                {
                    i++;
                    break;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                string key = "";
                if (s[i] == '\"')
                {
                    key = ParseJsonString(s, ref i);
                }
                else
                {
                    break;
                }

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ':') i++;

                DebloatJsonNode val = ParseJsonValue(s, ref i);
                if (!string.IsNullOrEmpty(key) && val != null)
                {
                    obj.ObjectFields[key] = val;
                }
            }
            return obj;
        }

        private static DebloatJsonNode ParseJsonArray(string s, ref int i)
        {
            DebloatJsonNode arr = new DebloatJsonNode();
            arr.IsArray = true;
            i++; // skip '['

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']')
                {
                    i++;
                    break;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                DebloatJsonNode item = ParseJsonValue(s, ref i);
                if (item != null)
                {
                    arr.ArrayItems.Add(item);
                }
            }
            return arr;
        }

        private static string ParseJsonString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '\"') return "";
            i++; // skip opening quote
            StringBuilder sb = new StringBuilder();

            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '\"')
                {
                    break;
                }
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    if (esc == '\"') sb.Append('\"');
                    else if (esc == '\\') sb.Append('\\');
                    else if (esc == '/') sb.Append('/');
                    else if (esc == 'b') sb.Append('\b');
                    else if (esc == 'f') sb.Append('\f');
                    else if (esc == 'n') sb.Append('\n');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == 't') sb.Append('\t');
                    else if (esc == 'u' && i + 4 <= s.Length)
                    {
                        string hex = s.Substring(i, 4);
                        int code;
                        if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                        }
                        i += 4;
                    }
                    else sb.Append(esc);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        #endregion
    }
}
