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
        /// Retrieves all unique categories present among the stored commands, sorted alphabetically.
        /// </summary>
        public List<string> GetDistinctCategories()
        {
            List<ToolboxCommandItem> list = LoadCommands();
            List<string> categories = new List<string>();
            foreach (ToolboxCommandItem item in list)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.category))
                {
                    string cat = item.category.Trim();
                    bool exists = false;
                    for (int i = 0; i < categories.Count; i++)
                    {
                        if (string.Equals(categories[i], cat, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) categories.Add(cat);
                }
            }
            categories.Sort(StringComparer.OrdinalIgnoreCase);
            return categories;
        }

        /// <summary>
        /// Renames an existing category across all stored commands and persists immediately.
        /// Returns the count of affected commands.
        /// </summary>
        public int RenameCategory(string oldCategory, string newCategory)
        {
            if (string.IsNullOrWhiteSpace(oldCategory) || string.IsNullOrWhiteSpace(newCategory)) return 0;
            string trimmedOld = oldCategory.Trim();
            string trimmedNew = newCategory.Trim();
            List<ToolboxCommandItem> list = LoadCommands();
            int count = 0;
            foreach (ToolboxCommandItem item in list)
            {
                if (item != null && string.Equals(item.category, trimmedOld, StringComparison.OrdinalIgnoreCase))
                {
                    item.category = trimmedNew;
                    count++;
                }
            }
            if (count > 0)
            {
                SaveCommands(list);
            }
            return count;
        }

        /// <summary>
        /// Deletes all commands under the specified category and persists immediately.
        /// Returns the count of deleted commands.
        /// </summary>
        public int DeleteCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return 0;
            string trimmed = category.Trim();
            List<ToolboxCommandItem> list = LoadCommands();
            int count = list.RemoveAll(item => item != null && string.Equals(item.category, trimmed, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                SaveCommands(list);
            }
            return count;
        }

        /// <summary>
        /// Returns the default factory preset commands across categories.
        /// </summary>
        public static List<ToolboxCommandItem> CreateDefaultPresets()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string jsonPath = Path.Combine(baseDir, "Config", "Addons", "Toolbox", "toolbox.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                    List<ToolboxCommandItem> fromFile = ParseJsonArray(json);
                    if (fromFile != null && fromFile.Count > 0)
                    {
                        return fromFile;
                    }
                }
            }
            catch { }

            List<ToolboxCommandItem> presets = new List<ToolboxCommandItem>();

            // Ferramentas da Comunidade
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
                open_external_logs = true,
                timeout_seconds = 0
            });

            // Integridade e Reparos
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-sync-internet-time",
                title = "Sincronizar Data e Hora via Internet (NTP)",
                description = "Inicia o serviço Horário do Windows (w32time) e força a sincronização imediata do relógio com servidores NTP na internet",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Write-Host 'Iniciando servico de Horario do Windows (w32time)...'; Set-Service -Name w32time -StartupType Automatic -ErrorAction SilentlyContinue; Start-Service w32time -ErrorAction SilentlyContinue; Write-Host 'Sincronizando relogio via Internet (NTP)...'; w32tm /resync /rediscover; Write-Host 'Data e hora atualizadas:'; Get-Date -Format 'yyyy-MM-dd HH:mm:ss'\"",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
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
                open_external_logs = true,
                timeout_seconds = 0
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-reinstall-winget",
                title = "Reparar e Registrar Novamente o WinGet (Package Manager)",
                description = "Registra novamente o pacote Microsoft.DesktopAppInstaller para restaurar o funcionamento do comando winget",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Write-Host 'Registrando novamente Microsoft.DesktopAppInstaller (WinGet)...'; Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction SilentlyContinue; Write-Host 'Verificando WinGet:'; winget --version\"",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-reset-windows-update",
                title = "Redefinir e Reparar o Windows Update",
                description = "Para serviços de atualização, renomeia pastas de download SoftwareDistribution, redefine Winsock e reinicia descoberta",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Write-Host 'Parando serviços do Windows Update...'; Stop-Service -Name wuauserv, bits, appidsvc, cryptsvc -Force -ErrorAction SilentlyContinue; Remove-Item \\\"$env:allusersprofile\\Application Data\\Microsoft\\Network\\Downloader\\qmgr*.dat\\\" -Force -ErrorAction SilentlyContinue; Rename-Item \\\"$env:systemroot\\SoftwareDistribution\\Download\\\" \\\"Download.bak\\\" -ErrorAction SilentlyContinue; Remove-Item \\\"$env:systemroot\\WindowsUpdate.log\\\" -Force -ErrorAction SilentlyContinue; netsh winsock reset; netsh winhttp reset proxy; Get-Service bits, wuauserv, cryptsvc | Set-Service -StartupType Manual -PassThru | Start-Service; Start-Process wuauclt -ArgumentList '/resetauthorization', '/detectnow' -NoNewWindow; Write-Host 'Windows Update redefinido com sucesso!'\"",
                category = "Integridade e Reparos",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 180
            });

            // Rede e Conectividade
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
                timeout_seconds = 30
            });

            // Tweaks Essenciais
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-consumer-features",
                title = "Desativar Recursos de Consumidor e Sugestões (Consumer Features)",
                description = "Impede a instalação automática de apps patrocinados e sugestões da Microsoft Store",
                command = "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-delivery-optimization",
                title = "Desativar Otimização de Entrega (Delivery Optimization)",
                description = "Impede o Windows de usar a sua conexão para enviar atualizações a computadores na internet",
                command = "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /t REG_DWORD /d 0 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-bitlocker",
                title = "Desativar Criptografia BitLocker (Unidade C:)",
                description = "Descriptografa a partição do sistema e desativa a proteção BitLocker no disco C:",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Disable-BitLocker -MountPoint $Env:SystemDrive; Write-Host 'Descriptografia BitLocker solicitada com sucesso na unidade C:.'\"",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-end-task-rightclick",
                title = "Ativar \"Finalizar Tarefa\" no Clique Direito da Barra de Tarefas",
                description = "Habilita a opção nativa do Windows 11 para encerrar programas diretamente pela barra de tarefas",
                command = "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\TaskbarDeveloperSettings\" /v TaskbarEndTask /t REG_DWORD /d 1 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-hibernation",
                title = "Desativar Hibernação do Sistema (Liberar hiberfil.sys)",
                description = "Desliga a hibernação, remove o arquivo hiberfil.sys e libera gigabytes de espaço no disco C:",
                command = "powercfg.exe /hibernate off && reg add \"HKLM\\System\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HibernateEnabled /t REG_DWORD /d 0 /f && reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FlyoutMenuSettings\" /v ShowHibernateOption /t REG_DWORD /d 0 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-telemetry-full",
                title = "Desativar Telemetria Completa e Coleta de Dados do Windows",
                description = "Desativa envio de diagnósticos, ID de anúncios, experiências personalizadas e serviços DiagTrack e wermgr",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$paths = @('HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo','HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy','HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy','HKCU:\\Software\\Microsoft\\Input\\TIPC','HKCU:\\Software\\Microsoft\\InputPersonalization','HKCU:\\Software\\Microsoft\\Personalization\\Settings','HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection','HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System','HKCU:\\Software\\Microsoft\\Siuf\\Rules'); foreach ($p in $paths) { if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null } }; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Input\\TIPC' -Name Enabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\InputPersonalization' -Name RestrictImplicitInkCollection -Value 1 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\InputPersonalization' -Name RestrictImplicitTextCollection -Value 1 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Personalization\\Settings' -Name AcceptedPrivacyPolicy -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection' -Name AllowTelemetry -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Name PublishUserActivities -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -Value 0 -Type DWord -Force; Set-MpPreference -SubmitSamplesConsent 2 -ErrorAction SilentlyContinue; Get-Service diagtrack, wermgr -ErrorAction SilentlyContinue | Set-Service -StartupType Disabled -ErrorAction SilentlyContinue; Stop-Service diagtrack, wermgr -Force -ErrorAction SilentlyContinue; [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine'); Write-Host 'Telemetria do Windows desativada com sucesso!'\"",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-remove-widgets",
                title = "Remover Widgets da Barra de Tarefas (WebExperience)",
                description = "Encerra e desinstala os pacotes de Widgets e WebExperience para liberar memória e painel da barra de tarefas",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-Process *Widget* -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Get-AppxPackage Microsoft.WidgetsPlatformRuntime -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Get-AppxPackage MicrosoftWindows.Client.WebExperience -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Write-Host 'Widgets removidos com sucesso!'\"",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-wpbt",
                title = "Desativar Execução WPBT (Bloatware de BIOS/OEM)",
                description = "Bloqueia a execução automática de binários e drivers de fabricantes (OEM) injetados via tabela ACPI WPBT no boot",
                command = "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\" /v DisableWpbtExecution /t REG_DWORD /d 1 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-optimize-services-manual",
                title = "Otimizar Serviços em Segundo Plano e Limite SvcHost",
                description = "Configura serviços não essenciais (CscService, DiagTrack, MapsBroker, StorSvc) e ajusta SvcHostSplitThresholdInKB conforme a RAM",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-Service CscService, SharedAccess -ErrorAction SilentlyContinue | Set-Service -StartupType Disabled -ErrorAction SilentlyContinue; Get-Service DiagTrack -ErrorAction SilentlyContinue | Set-Service -StartupType Disabled -ErrorAction SilentlyContinue; Get-Service MapsBroker, StorSvc -ErrorAction SilentlyContinue | Set-Service -StartupType Manual -ErrorAction SilentlyContinue; $memKB = [math]::Round(((Get-CimInstance Win32_PhysicalMemory | Measure-Object Capacity -Sum).Sum / 1KB)); Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name SvcHostSplitThresholdInKB -Value $memKB -Type DWord -Force; Write-Host \\\"Servicos configurados e SvcHostSplitThreshold ajustado para $memKB KB.\\\"\"",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 60
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-activity-history",
                title = "Desativar Histórico de Atividades do Usuário",
                description = "Impede o Windows de publicar ou fazer upload do histórico de atividades de navegação e programas do usuário",
                command = "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /t REG_DWORD /d 0 /f && reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v UploadUserActivities /t REG_DWORD /d 0 /f",
                category = "Tweaks Essenciais",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            // Tweaks Avançados
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-bg-apps",
                title = "Desativar Aplicativos em Segundo Plano (Background Apps)",
                description = "Desativa globalmente a execução em segundo plano para todos os aplicativos da Microsoft Store no Windows",
                command = "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications\" /v GlobalUserDisabled /t REG_DWORD /d 1 /f",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-windows-ai",
                title = "Desativar e Remover Recursos de IA do Windows (Copilot / Recall)",
                description = "Remove o Copilot, oculta componentes de IA nas Configurações, desativa o Recall e recursos de IA do Bloco de Notas",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"reg add \\\"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\\" /v SettingsPageVisibility /t REG_SZ /d \\\"hide:aicomponents\\\" /f; reg add \\\"HKLM\\SOFTWARE\\Policies\\WindowsNotepad\\\" /v DisableAIFeatures /t REG_DWORD /d 1 /f; Get-AppxPackage -AllUsers *Copilot* -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; winget uninstall -e --name 'Copilot' --silent --force --accept-source-agreements 2>$null; Get-AppxPackage -AllUsers MicrosoftWindows.Client.CoreAI -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Set-Service -Name WSAIFabricSvc -StartupType Disabled -ErrorAction SilentlyContinue; Disable-WindowsOptionalFeature -FeatureName Recall -Online -NoRestart -ErrorAction SilentlyContinue; Write-Host 'Recursos de IA do Windows desativados com sucesso!'\"",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-storage-sense",
                title = "Desativar Sensor de Armazenamento Automático (Storage Sense)",
                description = "Desativa a exclusão automática de arquivos temporários e downloads executada pelo Sensor de Armazenamento",
                command = "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicy\" /v 01 /t REG_DWORD /d 0 /f",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-reserved-storage",
                title = "Desativar Armazenamento Reservado (Liberar 7-10 GB)",
                description = "Desativa o Armazenamento Reservado do Windows via DISM para liberar de 7 a 10 GB de espaço em disco",
                command = "DISM /Online /Set-ReservedStorageState /State:Disabled",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-debloat-brave",
                title = "Debloat do Navegador Brave (Desativar Crypto, VPN e Leo AI)",
                description = "Aplica políticas corporativas no Brave para desativar carteira cripto, VPN integrada, IA Leo, notícias e telemetria",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$path = 'HKLM:\\SOFTWARE\\Policies\\BraveSoftware\\Brave'; if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }; @('BraveRewardsDisabled','BraveWalletDisabled','BraveVPNDisabled','BraveNewsDisabled','BraveTalkDisabled','TorDisabled') | ForEach-Object { Set-ItemProperty -Path $path -Name $_ -Value 1 -Type DWord -Force }; @('BraveAIChatEnabled','BraveStatsPingEnabled','BraveP3AEnabled','UrlKeyedAnonymizedDataCollectionEnabled','SafeBrowsingExtendedReportingEnabled','MetricsReportingEnabled') | ForEach-Object { Set-ItemProperty -Path $path -Name $_ -Value 0 -Type DWord -Force }; Write-Host 'Políticas de otimização do Brave aplicadas com sucesso!'\"",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-debloat-edge",
                title = "Debloat do Microsoft Edge (Desativar Shopping, Telemetria e Popups)",
                description = "Desativa assistente de compras, popups de primeiro uso, promoções, widgets e telemetria no Microsoft Edge",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$edge = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge'; if (-not (Test-Path $edge)) { New-Item -Path $edge -Force | Out-Null }; @('PersonalizationReportingEnabled','ShowRecommendationsEnabled','UserFeedbackAllowed','AlternateErrorPagesEnabled','EdgeCollectionsEnabled','EdgeShoppingAssistantEnabled','MicrosoftEdgeInsiderPromotionEnabled','ShowMicrosoftRewards','WebWidgetAllowed','DiagnosticData','EdgeAssetDeliveryServiceEnabled','WalletDonationEnabled','DefaultBrowserSettingsCampaignEnabled') | ForEach-Object { Set-ItemProperty -Path $edge -Name $_ -Value 0 -Type DWord -Force }; @('HideFirstRunExperience','ConfigureDoNotTrack') | ForEach-Object { Set-ItemProperty -Path $edge -Name $_ -Value 1 -Type DWord -Force }; if (-not (Test-Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\EdgeUpdate')) { New-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\EdgeUpdate' -Force | Out-Null }; Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\EdgeUpdate' -Name CreateDesktopShortcutDefault -Value 0 -Type DWord -Force; Write-Host 'Políticas de otimização do Edge aplicadas com sucesso!'\"",
                category = "Tweaks Avançados",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            // Preferências e Interface
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-optimize-visual-fx",
                title = "Otimizar Efeitos Visuais para Máximo Desempenho",
                description = "Configura animações para melhor desempenho, reduz MenuShowDelay para 50ms e melhora a resposta do sistema",
                command = "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name MinAnimate -Value 0; Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -Value 50; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name VisualFXSetting -Value 2; Write-Host 'Efeitos visuais otimizados com sucesso!'\"",
                category = "Preferências e Interface",
                shell = ToolboxShellType.PowerShell,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = false,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-bing-search",
                title = "Desativar Busca do Bing na Barra de Pesquisa do Windows",
                description = "Desativa resultados de pesquisa na internet (Bing) no Menu Iniciar, acelerando as buscas por arquivos locais",
                command = "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 0 /f && reg add \"HKCU\\Software\\Policies\\Microsoft\\Windows\\Explorer\" /v DisableSearchBoxSuggestions /t REG_DWORD /d 1 /f",
                category = "Preferências e Interface",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-disable-mouse-acceleration",
                title = "Desativar Aceleração do Mouse (Movimento 1:1 Preciso)",
                description = "Remove a aceleração de ponteiro para garantir movimento linear e precisão consistente no cursor",
                command = "reg add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d \"0\" /f && reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d \"0\" /f && reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d \"0\" /f",
                category = "Preferências e Interface",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
                timeout_seconds = 30
            });

            // Otimização e Desempenho
            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-clean-temp-files",
                title = "Limpar Pastas de Arquivos Temporários (%TEMP% e Temp)",
                description = "Remove arquivos temporários residuais em %TEMP% e C:\\Windows\\Temp",
                command = "cmd.exe /c \"del /q /f /s \\\"%TEMP%\\*.*\\\" 2>nul & del /q /f /s \\\"C:\\Windows\\Temp\\*.*\\\" 2>nul & echo Limpeza de temporarios concluida!\"",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
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
                open_external_logs = true,
                timeout_seconds = 120
            });

            presets.Add(new ToolboxCommandItem()
            {
                id = "preset-optimize-telemetry-services",
                title = "Desativar Serviços de Telemetria (DiagTrack / dmwappushservice)",
                description = "Desativa serviços DiagTrack, dmwappushservice e telemetria para reduzir consumo em segundo plano",
                command = "sc config DiagTrack start= disabled && sc stop DiagTrack && sc config dmwappushservice start= disabled && sc stop dmwappushservice && reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f",
                category = "Otimização e Desempenho",
                shell = ToolboxShellType.Cmd,
                wait_for_exit = true,
                capture_output = true,
                requires_admin = true,
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                open_external_logs = true,
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
                sb.AppendFormat("    \"open_external_logs\": {0},\r\n", item.open_external_logs ? "true" : "false");
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

                if (fields.ContainsKey("open_external_logs"))
                    item.open_external_logs = ParseBool(fields["open_external_logs"], true);
                else
                    item.open_external_logs = true;

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

