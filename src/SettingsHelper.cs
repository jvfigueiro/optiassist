using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OptiAssist
{
    public class AppSettings
    {
        public string CustomerName { get; set; }
        public string ContactInfo { get; set; }
        public string Language { get; set; }
        public string ReportSavePath { get; set; }
        public bool AutoRefreshHardware { get; set; }
        public bool EnableWingetFallback { get; set; }
        public string Theme { get; set; }
        public string AccentColor { get; set; }
        public bool ServiceProviderMode { get; set; }
        public bool EnableMaintenanceReport { get; set; }
        public bool OpenExternalLogsByDefault { get; set; }
        public bool AlwaysOnTop { get; set; }
        public List<string> DisabledAddons { get; set; }
        public string CustomLogoPath { get; set; }
        public string FreshStart { get; set; }

        // A new instance always holds the factory default values
        public AppSettings()
        {
            CustomerName = "OptiAssist";
            ContactInfo = "https://github.com/jvfigueiro/optiassist";
            Language = "en-US";
            ReportSavePath = @"C:\MaintenanceReport.txt";
            AutoRefreshHardware = true;
            EnableWingetFallback = true;
            Theme = "Light";
            AccentColor = "#0EA5E9";
            ServiceProviderMode = false;
            EnableMaintenanceReport = false;
            OpenExternalLogsByDefault = false;
            AlwaysOnTop = false;
            DisabledAddons = new List<string>();
            CustomLogoPath = "";
            FreshStart = "true";
        }
    }

    public static class SettingsHelper
    {
        public static string GetConfigDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configDir = Path.Combine(baseDir, "Config");
            if (!Directory.Exists(configDir))
            {
                try { Directory.CreateDirectory(configDir); } catch { }
            }
            return configDir;
        }

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetConfigDirectory(), "settings.json");
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            string path = GetSettingsFilePath();

            if (!File.Exists(path))
            {
                Save(settings);
                return settings;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var dict = ParseSimpleJson(json);
                if (dict.ContainsKey("CustomerName")) settings.CustomerName = dict["CustomerName"];
                else if (dict.ContainsKey("StoreName")) settings.CustomerName = dict["StoreName"]; // Legacy key
                if (dict.ContainsKey("ContactInfo")) settings.ContactInfo = dict["ContactInfo"];
                else if (dict.ContainsKey("Phone")) settings.ContactInfo = dict["Phone"]; // Legacy key
                if (dict.ContainsKey("Language")) settings.Language = dict["Language"];
                if (dict.ContainsKey("ReportSavePath")) settings.ReportSavePath = dict["ReportSavePath"];
                if (dict.ContainsKey("Theme")) settings.Theme = dict["Theme"];
                if (dict.ContainsKey("AccentColor")) settings.AccentColor = dict["AccentColor"];
                if (dict.ContainsKey("AutoRefreshHardware"))
                {
                    bool b;
                    if (bool.TryParse(dict["AutoRefreshHardware"], out b)) settings.AutoRefreshHardware = b;
                }
                if (dict.ContainsKey("EnableWingetFallback"))
                {
                    bool b;
                    if (bool.TryParse(dict["EnableWingetFallback"], out b)) settings.EnableWingetFallback = b;
                }
                if (dict.ContainsKey("ServiceProviderMode"))
                {
                    bool b;
                    if (bool.TryParse(dict["ServiceProviderMode"], out b)) settings.ServiceProviderMode = b;
                }
                if (dict.ContainsKey("EnableMaintenanceReport"))
                {
                    bool b;
                    if (bool.TryParse(dict["EnableMaintenanceReport"], out b)) settings.EnableMaintenanceReport = b;
                }
                if (dict.ContainsKey("OpenExternalLogsByDefault"))
                {
                    bool b;
                    if (bool.TryParse(dict["OpenExternalLogsByDefault"], out b)) settings.OpenExternalLogsByDefault = b;
                }
                if (dict.ContainsKey("AlwaysOnTop"))
                {
                    bool b;
                    if (bool.TryParse(dict["AlwaysOnTop"], out b)) settings.AlwaysOnTop = b;
                }
                if (dict.ContainsKey("CustomLogoPath"))
                {
                    settings.CustomLogoPath = dict["CustomLogoPath"];
                }
                if (dict.ContainsKey("freshstart"))
                {
                    settings.FreshStart = dict["freshstart"];
                }
                else if (dict.ContainsKey("FreshStart"))
                {
                    settings.FreshStart = dict["FreshStart"];
                }
                else
                {
                    settings.FreshStart = "true";
                }
                if (dict.ContainsKey("DisabledAddons"))
                {
                    string val = dict["DisabledAddons"];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        string[] parts = val.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string p in parts)
                        {
                            string trim = p.Trim();
                            if (!string.IsNullOrEmpty(trim) && !settings.DisabledAddons.Contains(trim))
                            {
                                settings.DisabledAddons.Add(trim);
                            }
                        }
                    }
                }
            }
            catch
            {
                // On error, fallback to default values
            }

            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string path = GetSettingsFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"CustomerName\": \"{0}\",", EscapeJson(settings.CustomerName)));
                sb.AppendLine(string.Format("  \"ContactInfo\": \"{0}\",", EscapeJson(settings.ContactInfo)));
                sb.AppendLine(string.Format("  \"Language\": \"{0}\",", EscapeJson(settings.Language)));
                sb.AppendLine(string.Format("  \"ReportSavePath\": \"{0}\",", EscapeJson(settings.ReportSavePath)));
                sb.AppendLine(string.Format("  \"AutoRefreshHardware\": {0},", settings.AutoRefreshHardware ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EnableWingetFallback\": {0},", settings.EnableWingetFallback ? "true" : "false"));
                sb.AppendLine(string.Format("  \"Theme\": \"{0}\",", EscapeJson(settings.Theme)));
                sb.AppendLine(string.Format("  \"AccentColor\": \"{0}\",", EscapeJson(settings.AccentColor)));
                sb.AppendLine(string.Format("  \"ServiceProviderMode\": {0},", settings.ServiceProviderMode ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EnableMaintenanceReport\": {0},", settings.EnableMaintenanceReport ? "true" : "false"));
                sb.AppendLine(string.Format("  \"OpenExternalLogsByDefault\": {0},", settings.OpenExternalLogsByDefault ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AlwaysOnTop\": {0},", settings.AlwaysOnTop ? "true" : "false"));
                sb.AppendLine(string.Format("  \"CustomLogoPath\": \"{0}\",", EscapeJson(settings.CustomLogoPath)));
                sb.AppendLine(string.Format("  \"freshstart\": \"{0}\",", EscapeJson(string.IsNullOrEmpty(settings.FreshStart) ? "true" : settings.FreshStart)));
                string disabledStr = settings.DisabledAddons != null ? string.Join(",", settings.DisabledAddons.ToArray()) : "";
                sb.AppendLine(string.Format("  \"DisabledAddons\": \"{0}\"", EscapeJson(disabledStr)));
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return result;

            string[] lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim().TrimEnd(',');
                if (trimmed.StartsWith("{") || trimmed.StartsWith("}")) continue;

                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = trimmed.Substring(0, colonIdx).Trim().Trim('\"');
                    string val = trimmed.Substring(colonIdx + 1).Trim().Trim('\"');
                    val = val.Replace("\\\\", "\\").Replace("\\\"", "\"");
                    result[key] = val;
                }
            }
            return result;
        }
    }

    public class CustomShortcutItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public string TargetType { get; set; } // "Section" or "Custom"
        public string TargetValue { get; set; }
        public string Arguments { get; set; }

        public CustomShortcutItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = "";
            Icon = "⭐";
            TargetType = "Section";
            TargetValue = "";
            Arguments = "";
        }
    }

    public static class CustomShortcutsHelper
    {
        public static string GetShortcutsFilePath()
        {
            return Path.Combine(SettingsHelper.GetConfigDirectory(), "custom_shortcuts.json");
        }

        public static List<CustomShortcutItem> CreateDefaultShortcuts()
        {
            return new List<CustomShortcutItem>
            {
                new CustomShortcutItem
                {
                    Id = "default-toolbox",
                    Title = "Toolbox",
                    Icon = "🧰",
                    TargetType = "Section",
                    TargetValue = "toolbox",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-app-manager",
                    Title = "App Manager",
                    Icon = "📦",
                    TargetType = "Section",
                    TargetValue = "app-manager",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-run-post-install",
                    Title = "Run Post-Install",
                    Icon = "🚀",
                    TargetType = "Section",
                    TargetValue = "post-install-run",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-maintenance-folder",
                    Title = "Maintenance Folder",
                    Icon = "📁",
                    TargetType = "Section",
                    TargetValue = "maintenance-folder",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-chris-titus",
                    Title = "Chris Titus WinUtil",
                    Icon = "🚀",
                    TargetType = "Section",
                    TargetValue = "chris-titus",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-memory-diag",
                    Title = "Memory Diagnostic",
                    Icon = "🧠",
                    TargetType = "Section",
                    TargetValue = "memory-diag",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-dev-mgmt",
                    Title = "Device Manager",
                    Icon = "🖥️",
                    TargetType = "Section",
                    TargetValue = "dev-mgmt",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-sys-dm",
                    Title = "System Properties",
                    Icon = "⚙️",
                    TargetType = "Section",
                    TargetValue = "sys-dm",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-reboot-uefi",
                    Title = "Reboot to UEFI",
                    Icon = "⚡",
                    TargetType = "Section",
                    TargetValue = "reboot-uefi",
                    Arguments = ""
                },
                new CustomShortcutItem
                {
                    Id = "default-installers-folder",
                    Title = "Installers Folder",
                    Icon = "📂",
                    TargetType = "Section",
                    TargetValue = "installers-folder",
                    Arguments = ""
                }
            };
        }

        public static List<CustomShortcutItem> Load()
        {
            var list = new List<CustomShortcutItem>();
            string path = GetShortcutsFilePath();
            if (!File.Exists(path))
            {
                list = CreateDefaultShortcuts();
                Save(list);
                return list;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                string[] blocks = json.Split(new[] { '{' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string block in blocks)
                {
                    int closeIdx = block.IndexOf('}');
                    if (closeIdx < 0) continue;
                    string content = block.Substring(0, closeIdx);
                    var item = new CustomShortcutItem();
                    bool hasField = false;

                    string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim().TrimEnd(',');
                        int colonIdx = trimmed.IndexOf(':');
                        if (colonIdx <= 0) continue;

                        string key = trimmed.Substring(0, colonIdx).Trim().Trim('\"');
                        string val = trimmed.Substring(colonIdx + 1).Trim().Trim('\"');
                        val = val.Replace("\\\"", "\"").Replace("\\\\", "\\");

                        if (key.Equals("Id", StringComparison.OrdinalIgnoreCase)) { item.Id = val; hasField = true; }
                        else if (key.Equals("Title", StringComparison.OrdinalIgnoreCase)) { item.Title = val; hasField = true; }
                        else if (key.Equals("Icon", StringComparison.OrdinalIgnoreCase)) { item.Icon = val; }
                        else if (key.Equals("TargetType", StringComparison.OrdinalIgnoreCase)) { item.TargetType = val; }
                        else if (key.Equals("TargetValue", StringComparison.OrdinalIgnoreCase)) { item.TargetValue = val; hasField = true; }
                        else if (key.Equals("Arguments", StringComparison.OrdinalIgnoreCase)) { item.Arguments = val; }
                    }

                    if (hasField && !string.IsNullOrWhiteSpace(item.TargetValue))
                    {
                        list.Add(item);
                    }
                }
            }
            catch { }

            return list;
        }

        public static void Save(List<CustomShortcutItem> items)
        {
            try
            {
                string path = GetShortcutsFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var it = items[i];
                        sb.AppendLine("  {");
                        sb.AppendLine(string.Format("    \"Id\": \"{0}\",", Escape(it.Id)));
                        sb.AppendLine(string.Format("    \"Title\": \"{0}\",", Escape(it.Title)));
                        sb.AppendLine(string.Format("    \"Icon\": \"{0}\",", Escape(it.Icon)));
                        sb.AppendLine(string.Format("    \"TargetType\": \"{0}\",", Escape(it.TargetType)));
                        sb.AppendLine(string.Format("    \"TargetValue\": \"{0}\",", Escape(it.TargetValue)));
                        sb.AppendLine(string.Format("    \"Arguments\": \"{0}\"", Escape(it.Arguments)));
                        sb.Append("  }");
                        if (i < items.Count - 1) sb.AppendLine(",");
                        else sb.AppendLine();
                    }
                }
                sb.AppendLine("]");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }
    }
}
