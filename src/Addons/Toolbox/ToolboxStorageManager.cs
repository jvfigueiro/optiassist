using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OptiAssist.Addons.Toolbox
{
    /// <summary>
    /// Persistence manager responsible for reading, validating, and writing Toolbox commands
    /// to Config\Addons\Toolbox\toolbox.json.
    /// </summary>
    public class ToolboxStorageManager
    {
        private readonly string _storageFilePath;

        public string StorageFilePath
        {
            get { return _storageFilePath; }
        }

        public ToolboxStorageManager()
            : this(GetDefaultStorageFilePath())
        {
        }

        public ToolboxStorageManager(string customFilePath)
        {
            _storageFilePath = string.IsNullOrWhiteSpace(customFilePath)
                ? GetDefaultStorageFilePath()
                : customFilePath;
        }

        public static string GetDefaultStorageFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string toolboxDir = Path.Combine(baseDir, "Config", "Addons", "Toolbox");
            if (!Directory.Exists(toolboxDir))
            {
                try { Directory.CreateDirectory(toolboxDir); } catch { }
            }
            return Path.Combine(toolboxDir, "toolbox.json");
        }

        /// <summary>
        /// Loads all persisted commands from toolbox.json. If the file does not exist yet,
        /// creates it populated with the default presets.
        /// </summary>
        public List<ToolboxCommandItem> LoadCommands()
        {
            try
            {
                if (!File.Exists(_storageFilePath))
                {
                    List<ToolboxCommandItem> defaults = CreateDefaultPresets();
                    SaveCommands(defaults);
                    return defaults;
                }

                string json = File.ReadAllText(_storageFilePath, Encoding.UTF8);
                List<ToolboxCommandItem> parsed = ParseJsonArray(json);
                if (parsed == null)
                {
                    return new List<ToolboxCommandItem>();
                }
                return parsed;
            }
            catch
            {
                return CreateDefaultPresets();
            }
        }

        /// <summary>
        /// Saves the full list of Toolbox commands to toolbox.json with clean indentation.
        /// </summary>
        public void SaveCommands(IEnumerable<ToolboxCommandItem> commands)
        {
            string dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = SerializeToJsonArray(commands);
            File.WriteAllText(_storageFilePath, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// Adds a new command or updates an existing command (matched by id) and persists immediately.
        /// </summary>
        public List<ToolboxCommandItem> UpsertCommand(ToolboxCommandItem item)
        {
            if (item == null) throw new ArgumentNullException("item");

            string validationError;
            if (!item.Validate(out validationError))
            {
                throw new ArgumentException(validationError);
            }

            List<ToolboxCommandItem> list = LoadCommands();
            int existingIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].id, item.id, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                list[existingIndex] = item.Clone();
            }
            else
            {
                list.Add(item.Clone());
            }

            SaveCommands(list);
            return list;
        }

        /// <summary>
        /// Deletes a command by its ID and persists the updated list.
        /// </summary>
        public List<ToolboxCommandItem> DeleteCommand(string id)
        {
            List<ToolboxCommandItem> list = LoadCommands();
            list.RemoveAll(c => string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));
            SaveCommands(list);
            return list;
        }

        /// <summary>
        /// Restores the built-in default presets (merging or resetting) and persists to disk.
        /// </summary>
        public List<ToolboxCommandItem> RestoreDefaultPresets()
        {
            List<ToolboxCommandItem> defaults = CreateDefaultPresets();
            SaveCommands(defaults);
            return defaults;
        }

        /// <summary>
        /// Returns the default factory preset commands across categories.
        /// </summary>
        public static List<ToolboxCommandItem> CreateDefaultPresets()
        {
            List<ToolboxCommandItem> presets = new List<ToolboxCommandItem>();

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-chkdsk-c",
                title = "Verificar Integridade do Disco (CHKDSK C:)",
                description = "Executa verificação somente-leitura de erros lógicos no sistema de arquivos da unidade C:",
                command = "chkdsk C:",
                category = "Correção de Erros",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 600
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-sfc-scannow",
                title = "Verificar Arquivos do Sistema (SFC /scannow)",
                description = "Escaneia e repara arquivos protegidos corrompidos do Windows usando o System File Checker",
                command = "sfc /scannow",
                category = "Correção de Erros",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 1800
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-dism-restorehealth",
                title = "Reparar Imagem do Windows (DISM RestoreHealth)",
                description = "Verifica e repara a loja de componentes do Windows (WinSxS) via DISM",
                command = "DISM /Online /Cleanup-Image /RestoreHealth",
                category = "Correção de Erros",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 1800
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-ipconfig-flushdns",
                title = "Limpar Cache DNS (Flush DNS)",
                description = "Esvazia o cache de resolução de nomes DNS do Windows",
                command = "ipconfig /flushdns",
                category = "Rede",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-netsh-winsock-reset",
                title = "Redefinir Catálogo Winsock e TCP/IP",
                description = "Restaura o catálogo Winsock e a pilha TCP/IP para corrigir falhas de conectividade",
                command = "netsh winsock reset && netsh int ip reset",
                category = "Rede",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-ps-listening-ports",
                title = "Listar Portas TCP Abertas (Listening)",
                description = "Consulta via PowerShell todas as portas TCP em estado Listen e os respectivos PIDs",
                command = "Get-NetTCPConnection -State Listen | Select-Object LocalAddress, LocalPort, OwningProcess | Sort-Object LocalPort | Format-Table -AutoSize",
                category = "Rede",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-compactos-query",
                title = "Consultar Estado de Compactação (CompactOS)",
                description = "Verifica se os binários do sistema operacional estão compactados via compact.exe",
                command = "compact.exe /CompactOS:query",
                category = "Otimização",
                shell = ToolboxShellType.Binary,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-powercfg-list",
                title = "Listar Planos de Energia Ativos (PowerCfg)",
                description = "Exibe todos os esquemas de energia disponíveis e destaca o plano ativo no sistema",
                command = "powercfg.exe /list",
                category = "Otimização",
                shell = ToolboxShellType.Binary,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            return presets;
        }

        #region RFC-8259 JSON SERIALIZER & PARSER

        public static string SerializeToJsonArray(IEnumerable<ToolboxCommandItem> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");

            List<ToolboxCommandItem> list = new List<ToolboxCommandItem>();
            if (items != null)
            {
                foreach (ToolboxCommandItem it in items)
                {
                    if (it != null) list.Add(it);
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                ToolboxCommandItem item = list[i];
                sb.AppendLine("  {");
                sb.AppendFormat("    \"id\": \"{0}\",\r\n", EscapeJson(item.id));
                sb.AppendFormat("    \"title\": \"{0}\",\r\n", EscapeJson(item.title));
                sb.AppendFormat("    \"description\": \"{0}\",\r\n", EscapeJson(item.description));
                sb.AppendFormat("    \"command\": \"{0}\",\r\n", EscapeJson(item.command));
                sb.AppendFormat("    \"category\": \"{0}\",\r\n", EscapeJson(item.category));
                sb.AppendFormat("    \"shell\": \"{0}\",\r\n", EscapeJson(ToolboxShellType.Normalize(item.shell)));
                sb.AppendFormat("    \"wait_for_exit\": {0},\r\n", item.wait_for_exit ? "true" : "false");
                sb.AppendFormat("    \"capture_output\": {0},\r\n", item.capture_output ? "true" : "false");
                sb.AppendFormat("    \"requires_admin\": {0},\r\n", item.requires_admin ? "true" : "false");
                sb.AppendFormat("    \"timeout_seconds\": {0}\r\n", item.timeout_seconds);
                sb.Append("  }");
                if (i < list.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.Append("]");
            return sb.ToString();
        }

        public static List<ToolboxCommandItem> ParseJsonArray(string json)
        {
            List<ToolboxCommandItem> results = new List<ToolboxCommandItem>();
            if (string.IsNullOrWhiteSpace(json)) return results;

            int idx = 0;
            int len = json.Length;

            while (idx < len)
            {
                int objStart = json.IndexOf('{', idx);
                if (objStart < 0) break;

                int objEnd = FindMatchingBrace(json, objStart);
                if (objEnd < 0) break;

                string objText = json.Substring(objStart + 1, objEnd - objStart - 1);
                Dictionary<string, string> fields = ParseObjectFields(objText);

                ToolboxCommandItem item = new ToolboxCommandItem();
                if (fields.ContainsKey("id") && !string.IsNullOrWhiteSpace(fields["id"]))
                    item.id = fields["id"];
                if (fields.ContainsKey("title"))
                    item.title = fields["title"];
                if (fields.ContainsKey("description"))
                    item.description = fields["description"];
                if (fields.ContainsKey("command"))
                    item.command = fields["command"];
                if (fields.ContainsKey("category") && !string.IsNullOrWhiteSpace(fields["category"]))
                    item.category = fields["category"];
                if (fields.ContainsKey("shell"))
                    item.shell = ToolboxShellType.Normalize(fields["shell"]);

                if (fields.ContainsKey("wait_for_exit"))
                    item.wait_for_exit = ParseBool(fields["wait_for_exit"], true);
                if (fields.ContainsKey("capture_output"))
                    item.capture_output = ParseBool(fields["capture_output"], true);

                if (fields.ContainsKey("requires_admin"))
                    item.requires_admin = ParseBool(fields["requires_admin"], false);
                else
                    item.requires_admin = ToolboxCommandItem.InferRequiresAdmin(item.command);

                if (fields.ContainsKey("timeout_seconds"))
                {
                    int t;
                    if (int.TryParse(fields["timeout_seconds"], out t) && t >= 0)
                        item.timeout_seconds = t;
                }

                if (!string.IsNullOrWhiteSpace(item.title) && !string.IsNullOrWhiteSpace(item.command))
                {
                    results.Add(item);
                }

                idx = objEnd + 1;
            }

            return results;
        }

        private static int FindMatchingBrace(string s, int openBraceIdx)
        {
            int depth = 0;
            bool inQuotes = false;
            bool escaped = false;

            for (int i = openBraceIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\' && inQuotes)
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes)
                {
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return -1;
        }

        private static Dictionary<string, string> ParseObjectFields(string objContent)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            int n = objContent.Length;

            while (i < n)
            {
                // Find key opening quote
                int keyStart = objContent.IndexOf('"', i);
                if (keyStart < 0) break;

                int keyEnd;
                string key = ReadJsonString(objContent, keyStart, out keyEnd);
                if (keyEnd < 0) break;

                int colon = objContent.IndexOf(':', keyEnd + 1);
                if (colon < 0) break;

                int valIdx = colon + 1;
                while (valIdx < n && char.IsWhiteSpace(objContent[valIdx])) valIdx++;
                if (valIdx >= n) break;

                if (objContent[valIdx] == '"')
                {
                    int valEnd;
                    string val = ReadJsonString(objContent, valIdx, out valEnd);
                    if (valEnd < 0) break;
                    dict[key] = val;
                    i = valEnd + 1;
                }
                else
                {
                    int valEnd = valIdx;
                    while (valEnd < n && objContent[valEnd] != ',' && objContent[valEnd] != '\r' && objContent[valEnd] != '\n' && objContent[valEnd] != '}')
                    {
                        valEnd++;
                    }
                    string rawVal = objContent.Substring(valIdx, valEnd - valIdx).Trim();
                    dict[key] = rawVal;
                    i = valEnd + 1;
                }
            }

            return dict;
        }

        private static string ReadJsonString(string s, int openQuoteIdx, out int closeQuoteIdx)
        {
            StringBuilder sb = new StringBuilder();
            bool escaped = false;
            for (int i = openQuoteIdx + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (escaped)
                {
                    if (c == 'n') sb.Append('\n');
                    else if (c == 'r') sb.Append('\r');
                    else if (c == 't') sb.Append('\t');
                    else if (c == '"' || c == '\\' || c == '/') sb.Append(c);
                    else if (c == 'u' && i + 4 < s.Length)
                    {
                        string hex = s.Substring(i + 1, 4);
                        int code;
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                    }
                    else sb.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    closeQuoteIdx = i;
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
            }
            closeQuoteIdx = -1;
            return sb.ToString();
        }

        private static bool ParseBool(string raw, bool defaultVal)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultVal;
            string norm = raw.Trim().ToLowerInvariant();
            if (norm == "true" || norm == "1") return true;
            if (norm == "false" || norm == "0") return false;
            return defaultVal;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        #endregion
    }
}
