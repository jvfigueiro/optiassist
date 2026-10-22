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
        public List<string> DisabledAddons { get; set; }
        public string CustomLogoPath { get; set; }

        // A new instance always holds the factory default values
        public AppSettings()
        {
            CustomerName = "OptiAssist";
            ContactInfo = "https://github.com/jvfigueiro/optiassist";
            Language = "en-US";
            ReportSavePath = @"C:\MaintenanceReport.txt";
            AutoRefreshHardware = true;
            EnableWingetFallback = true;
            Theme = "Dark";
            AccentColor = "#0EA5E9";
            ServiceProviderMode = false;
            EnableMaintenanceReport = false;
            OpenExternalLogsByDefault = false;
            DisabledAddons = new List<string>();
            CustomLogoPath = "";
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
                if (dict.ContainsKey("CustomLogoPath"))
                {
                    settings.CustomLogoPath = dict["CustomLogoPath"];
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
                sb.AppendLine(string.Format("  \"CustomLogoPath\": \"{0}\",", EscapeJson(settings.CustomLogoPath)));
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
}
