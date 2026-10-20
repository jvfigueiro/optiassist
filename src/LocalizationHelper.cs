using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OptiAssist
{
    public static class LocalizationHelper
    {
        private static Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string _currentLanguage = "en-US";
        public static string CurrentLanguage
        {
            get { return _currentLanguage; }
            private set { _currentLanguage = value; }
        }

        public static string GetLanguagesDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.Combine(baseDir, "Config", "Languages");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public static string GetLanguageFilePath(string langCode)
        {
            return Path.Combine(GetLanguagesDirectory(), langCode + ".json");
        }

        public static List<string> GetAvailableLanguages()
        {
            var list = new List<string>();
            try
            {
                string dir = GetLanguagesDirectory();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.json");
                    foreach (var f in files)
                    {
                        list.Add(Path.GetFileNameWithoutExtension(f));
                    }
                }
            }
            catch { }

            if (list.Count == 0)
            {
                list.Add("en-US");
                list.Add("pt-BR");
            }

            return list;
        }

        public static string GetLanguageDisplayName(string langCode)
        {
            try
            {
                string filePath = GetLanguageFilePath(langCode);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var dict = ParseLanguageJson(json);
                    if (dict.ContainsKey("Lang_Name") && !string.IsNullOrEmpty(dict["Lang_Name"]))
                    {
                        return dict["Lang_Name"];
                    }
                }
            }
            catch { }
            return langCode;
        }

        public static void LoadLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) langCode = "en-US";

            CurrentLanguage = langCode;
            string filePath = GetLanguageFilePath(langCode);

            if (!File.Exists(filePath))
            {
                // Fallback to en-US
                filePath = GetLanguageFilePath("en-US");
            }

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    _strings = ParseLanguageJson(json);
                }
                catch
                {
                    _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string val;
            if (_strings != null && _strings.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }
            return fallback ?? key;
        }

        private static Dictionary<string, string> ParseLanguageJson(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            // Robust parser for flat JSON dictionary { "key": "value" }
            int i = 0;
            int len = json.Length;

            while (i < len)
            {
                // Find next quote (key)
                while (i < len && json[i] != '\"') i++;
                if (i >= len) break;

                string key = ReadJsonString(json, ref i);
                if (key == null) break;

                // Find colon ':'
                while (i < len && json[i] != ':') i++;
                if (i >= len) break;
                i++; // Skip ':'

                // Find next quote (value)
                while (i < len && json[i] != '\"' && json[i] != '}' && json[i] != ',') i++;
                if (i >= len || json[i] == '}') break;

                if (json[i] == '\"')
                {
                    string val = ReadJsonString(json, ref i);
                    if (val != null)
                    {
                        dict[key] = val;
                    }
                }
            }

            return dict;
        }

        private static string ReadJsonString(string json, ref int i)
        {
            if (i >= json.Length || json[i] != '\"') return null;
            i++; // Skip opening quote

            StringBuilder sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= json.Length) break;
                    char esc = json[i];
                    switch (esc)
                    {
                        case '\"': sb.Append('\"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < json.Length)
                            {
                                string hex = json.Substring(i + 1, 4);
                                int code;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else if (c == '\"')
                {
                    i++; // Skip closing quote
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }

            return sb.ToString();
        }
    }
}
