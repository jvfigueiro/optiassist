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

            // 1. Ferramentas da Comunidade
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-christitus-winutil",
                title = "Chris Titus WinUtil (Ferramenta de Otimização)",
                description = "Utilitário open-source Chris Titus Tech para ajustes rápidos, tweaks e configurações essenciais do Windows",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"irm https://christitus.com/win | iex\"",
                category = "Ferramentas da Comunidade",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = false,
                capture_output = false,
                requires_admin = true,
                timeout_seconds = 0
            });

            // 2. Integridade e Reparos do Sistema
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-sfc-scannow",
                title = "Verificar e Reparar Arquivos do Sistema (SFC /scannow)",
                description = "Escaneia e repara arquivos protegidos corrompidos do Windows usando o System File Checker",
                command = "sfc /scannow",
                category = "Integridade e Reparos",
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
                description = "Verifica e repara a integridade da loja de componentes do Windows (WinSxS) via DISM",
                command = "DISM /Online /Cleanup-Image /RestoreHealth",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 1800
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-dism-sfc-combo",
                title = "Reparação Completa do Sistema (DISM + SFC)",
                description = "Executa DISM RestoreHealth seguido de SFC /scannow para verificação e reparo aprofundados",
                command = "DISM /Online /Cleanup-Image /RestoreHealth && sfc /scannow",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 3600
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-chkdsk-c",
                title = "Verificar Integridade do Disco (CHKDSK C:)",
                description = "Executa verificação somente-leitura de erros lógicos no sistema de arquivos da unidade C:",
                command = "chkdsk C:",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 600
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-fix-printer-rpc",
                title = "Corrigir Erro de Impressora em Rede (RPC 0x11b / 0x709)",
                description = "Ajusta a chave RpcAuthnLevelPrivacyEnabled no Spooler para restaurar impressoras em rede após atualizações do Windows",
                command = "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Print\" /v RpcAuthnLevelPrivacyEnabled /t REG_DWORD /d 0 /f && net stop Spooler && net start Spooler",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-dualboot-clock",
                title = "Corrigir Relógio do Windows em Dual Boot (UTC / Linux)",
                description = "Configura o relógio de hardware do Windows em UTC (RealTimeIsUniversal) para evitar dessincronização de horário com Linux",
                command = "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\TimeZoneInformation\" /v RealTimeIsUniversal /t REG_DWORD /d 1 /f",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-wsreset-store",
                title = "Instalar ou Reparar Microsoft Store (wsreset -i)",
                description = "Reinstala e reseta os componentes e caches corrompidos da Microsoft Store",
                command = "wsreset.exe -i",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.Binary,
                wait_for_exit = false,
                capture_output = false,
                requires_admin = true,
                timeout_seconds = 0
            });

            // 3. Rede e Conectividade
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-ipconfig-flushdns",
                title = "Limpar Cache DNS (Flush DNS)",
                description = "Esvazia o cache de resolução de nomes DNS do Windows",
                command = "ipconfig /flushdns",
                category = "Rede e Conectividade",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-netsh-winsock-reset",
                title = "Redefinir Catálogo Winsock e Pilha TCP/IP",
                description = "Restaura o catálogo Winsock e a pilha TCP/IP para corrigir falhas de conectividade",
                command = "netsh winsock reset && netsh int ip reset",
                category = "Rede e Conectividade",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-net-reset-full",
                title = "Redefinição Completa de Conectividade e DHCP",
                description = "Libera e renova concessões DHCP, esvazia DNS e redefine pilha TCP/IP e Winsock",
                command = "ipconfig /release && ipconfig /flushdns && netsh winsock reset && netsh int ip reset && ipconfig /renew",
                category = "Rede e Conectividade",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 90
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-ps-listening-ports",
                title = "Listar Portas TCP Abertas (Listening)",
                description = "Consulta via PowerShell todas as portas TCP em estado Listen e os respectivos PIDs",
                command = "Get-NetTCPConnection -State Listen | Select-Object LocalAddress, LocalPort, OwningProcess | Sort-Object LocalPort | Format-Table -AutoSize",
                category = "Rede e Conectividade",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            // 4. Otimização e Desempenho
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-clean-temp-files",
                title = "Limpar Pastas de Arquivos Temporários",
                description = "Remove arquivos temporários residuais em %TEMP% e C:\\Windows\\Temp",
                command = "cmd.exe /c \"del /q /f /s \\\"%TEMP%\\*.*\\\" 2>nul & del /q /f /s \\\"C:\\Windows\\Temp\\*.*\\\" 2>nul & echo Limpeza de temporarios concluida!\"",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-clean-update-cache",
                title = "Limpar Cache de Download do Windows Update",
                description = "Para serviços wuauserv e bits, limpa a pasta SoftwareDistribution\\Download e reinicia os serviços",
                command = "net stop wuauserv && net stop bits && cmd /c \"rd /s /q \\\"%windir%\\SoftwareDistribution\\Download\\\" 2>nul & md \\\"%windir%\\SoftwareDistribution\\Download\\\" 2>nul\" && net start wuauserv && net start bits",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-optimize-visual-fx",
                title = "Otimizar Efeitos Visuais para Máximo Desempenho",
                description = "Configura animações para melhor desempenho, reduz MenuShowDelay para 50ms e melhora a resposta do sistema",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name MinAnimate -Value 0; Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -Value 50; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name VisualFXSetting -Value 2; Write-Host 'Efeitos visuais otimizados com sucesso!'\"",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-optimize-telemetry-services",
                title = "Desativar Telemetria e Otimizar Serviços Não Essenciais",
                description = "Desativa serviços DiagTrack, dmwappushservice e telemetria para reduzir consumo em segundo plano",
                command = "sc config DiagTrack start= disabled && sc stop DiagTrack && sc config dmwappushservice start= disabled && sc stop dmwappushservice && reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-compactos-query",
                title = "Consultar Estado de Compactação do Sistema (CompactOS)",
                description = "Verifica se os binários do sistema operacional estão compactados via compact.exe",
                command = "compact.exe /CompactOS:query",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Binary,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-compactos-always",
                title = "Compactar Binários do Windows (CompactOS)",
                description = "Reduz o tamanho em disco da instalação do Windows compactando binários do sistema sem perda de performance",
                command = "compact.exe /CompactOS:always",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Binary,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 1800
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-winsat-formal",
                title = "Executar Benchmark de Desempenho do Sistema (WinSAT Formal)",
                description = "Executa a avaliação completa do Windows System Assessment Tool para CPU, memória, gráficos e disco",
                command = "winsat.exe formal",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Binary,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                timeout_seconds = 900
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-powercfg-list",
                title = "Listar Planos de Energia Ativos (PowerCfg)",
                description = "Exibe todos os esquemas de energia disponíveis e destaca o plano ativo no sistema",
                command = "powercfg.exe /list",
                category = "Otimização e Desempenho",
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

