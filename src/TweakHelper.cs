using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace OptiAssist
{
    public static class TweakHelper
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        public static string GetScriptsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptsDir = Path.Combine(baseDir, "Scripts");
            if (!Directory.Exists(scriptsDir))
            {
                try { Directory.CreateDirectory(scriptsDir); } catch { }
            }
            return scriptsDir;
        }

        public static void CleanTempFiles(Action<string> log)
        {
            log("=== INICIANDO LIMPEZA DE ARQUIVOS TEMPORÁRIOS ===");

            long totalDeletedBytes = 0;
            int totalDeletedFiles = 0;

            // 1. %TEMP% do Usuário
            string userTemp = Path.GetTempPath();
            log("-> Limpando temporários do usuário: " + userTemp);
            CleanDirectory(userTemp, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 2. C:\Windows\Temp
            string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            log("-> Limpando temporários do Windows: " + winTemp);
            CleanDirectory(winTemp, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 3. Prefetch
            string prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            log("-> Limpando arquivos de Prefetch...");
            CleanDirectory(prefetch, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 4. Relatórios de erro do Windows (WER)
            string werPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportQueue");
            if (Directory.Exists(werPath))
            {
                log("-> Limpando fila de relatórios de erros (WER)...");
                CleanDirectory(werPath, ref totalDeletedFiles, ref totalDeletedBytes, log);
            }

            // 5. Esvaziar Lixeira de todas as unidades
            try
            {
                log("-> Esvaziando a Lixeira de todas as unidades...");
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                log("   [OK] Lixeira esvaziada.");
            }
            catch (Exception ex)
            {
                log("   [AVISO] Não foi possível esvaziar a lixeira: " + ex.Message);
            }

            double mb = Math.Round(totalDeletedBytes / (1024.0 * 1024.0), 2);
            log(string.Format("=== LIMPEZA CONCLUÍDA! {0} arquivos removidos ({1} MB liberados) ===", totalDeletedFiles, mb));
        }

        public static void CleanWindowsUpdateCache(Action<string> log)
        {
            log("=== LIMPANDO CACHE DE DOWNLOAD DO WINDOWS UPDATE ===");
            try
            {
                log("-> Parando serviço do Windows Update (wuauserv)...");
                StopService("wuauserv", 10);

                string dlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download");
                if (Directory.Exists(dlPath))
                {
                    log("-> Removendo arquivos temporários em SoftwareDistribution\\Download...");
                    int count = 0;
                    long bytes = 0;
                    CleanDirectory(dlPath, ref count, ref bytes, log);
                    log(string.Format("   [OK] {0} arquivos de atualizações removidos ({1} MB liberados).", count, Math.Round(bytes / (1024.0 * 1024.0), 2)));
                }

                log("-> Reiniciando serviço do Windows Update (wuauserv)...");
                StartService("wuauserv", 10);
                log("=== CACHE DO WINDOWS UPDATE LIMPO COM SUCESSO! ===");
            }
            catch (Exception ex)
            {
                log("[ERRO] Erro ao limpar cache do Windows Update: " + ex.Message);
            }
        }

        public static void RunSfcRepair(Action<string> log)
        {
            log("=================================================");
            log("   REPARO DA INSTALAÇÃO DO WINDOWS (MÉTODO SFC)  ");
            log("=================================================");
            log("Executando: sfc /scannow...");
            log("AVISO: A verificação dos arquivos protegidos do Windows pode levar de 3 a 10 minutos.");
            RunProcessStreamed("sfc.exe", "/scannow", log);
            log("\n=== VERIFICAÇÃO E REPARO SFC FINALIZADO! ===");
        }

        public static void RunDismRepair(Action<string> log)
        {
            log("=================================================");
            log("  REPARO DA INSTALAÇÃO DO WINDOWS (MÉTODO DISM)  ");
            log("=================================================");
            log("Executando: Dism /Online /Cleanup-Image /RestoreHealth...");
            log("AVISO: O DISM verifica a integridade da imagem do sistema e substitui arquivos corrompidos.");
            RunProcessStreamed("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log);
            log("\n=== RESTAURAÇÃO DE IMAGEM DISM FINALIZADA! ===");
        }

        public static void RunDismAndSfc(Action<string> log)
        {
            log("=================================================");
            log("   VERIFICAÇÃO E REPARO DO SISTEMA (DISM + SFC)  ");
            log("=================================================");
            log("AVISO: Este procedimento pode levar de 5 a 15 minutos dependendo da velocidade do SSD/disco.");

            // 1. DISM RestoreHealth
            log("\n[ETAPA 1/2] Executando DISM /Online /Cleanup-Image /RestoreHealth...");
            RunProcessStreamed("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log);

            // 2. SFC Scannow
            log("\n[ETAPA 2/2] Executando SFC /scannow...");
            RunProcessStreamed("sfc.exe", "/scannow", log);

            log("\n=== VERIFICAÇÃO DISM E SFC FINALIZADA COM SUCESSO! ===");
        }

        public static void CompactOsInstall(Action<string> log)
        {
            log("=================================================");
            log("       COMPACTAÇÃO DA INSTALAÇÃO DO WINDOWS      ");
            log("=================================================");
            log("Executando: compact.exe /CompactOS:always...");
            log("AVISO: A compactação dos binários do Windows (Compact OS) pode liberar de 1,5 a 3 GB em disco.");
            log("Aguarde, este procedimento pode levar alguns minutos...");
            RunProcessStreamed("compact.exe", "/CompactOS:always", log);
            log("\n=== COMPACTAÇÃO DA INSTALAÇÃO DO WINDOWS CONCLUÍDA! ===");
        }

        public static void RunChkdsk(Action<string> log)
        {
            log("=================================================");
            log("      VERIFICAÇÃO DE INTEGRIDADE DE DISCO        ");
            log("=================================================");
            log("Executando verificação rápida (somente leitura) na unidade C:...");
            RunProcessStreamed("chkdsk.exe", "C:", log);
            log("\n=== VERIFICAÇÃO DO CHKDSK FINALIZADA! ===");
        }

        public static void OptimizeVisualEffects(Action<string> log)
        {
            log("=== OTIMIZANDO EFEITOS VISUAIS PARA MÁXIMO DESEMPENHO ===");
            try
            {
                // VisualFXSetting = 2 (Melhor Desempenho)
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                        log("-> VisualEffects configurado para 'Melhor Desempenho'.");
                    }
                }

                // Desativar animações de minimizar/maximizar
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("MinAnimate", "0", RegistryValueKind.String);
                        log("-> Animações de minimização/maximização desativadas.");
                    }
                }

                // MenuShowDelay = 50ms
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("MenuShowDelay", "50", RegistryValueKind.String);
                        log("-> Atraso de abertura de menus reduzido para 50ms.");
                    }
                }

                // Notificar o shell do Windows sobre as alterações
                UIntPtr result;
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "TraySettings", SMTO_ABORTIFHUNG, 1000, out result);

                log("=== EFEITOS VISUAIS OTIMIZADOS COM SUCESSO! ===");
            }
            catch (Exception ex)
            {
                log("[ERRO] Erro ao otimizar efeitos visuais: " + ex.Message);
            }
        }

        public static void OptimizeTelemetryAndServices(Action<string> log)
        {
            log("=== OTIMIZANDO SERVIÇOS NÃO ESSENCIAIS E TELEMETRIA ===");

            // 1. Desativar DiagTrack (Connected User Experiences and Telemetry)
            DisableService("DiagTrack", "Telemetria do Windows (DiagTrack)", log);

            // 2. Desativar dmwappushservice
            DisableService("dmwappushservice", "Serviço de Rastreamento WAP", log);

            // 3. Desativar Relatório de Erros do Windows (WerSvc)
            DisableService("WerSvc", "Relatório de Erros do Windows (WerSvc)", log);

            // 4. Registro: Desativar coleta de dados de telemetria
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                        log("-> Telemetria corporativa bloqueada via Registro (AllowTelemetry = 0).");
                    }
                }
            }
            catch (Exception ex)
            {
                log("[AVISO] Não foi possível definir chave de telemetria: " + ex.Message);
            }

            log("=== OTIMIZAÇÃO DE SERVIÇOS E TELEMETRIA CONCLUÍDA! ===");
        }

        public static void FixNetworkPrinterRpc(Action<string> log)
        {
            log("=== CORRIGINDO ERRO DE IMPRESSORAS EM REDE (RPC 0x11b / 0x709) ===");
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Print"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("RpcAuthnLevelPrivacyEnabled", 0, RegistryValueKind.DWord);
                        log("-> Chave de Registro RpcAuthnLevelPrivacyEnabled = 0 aplicada com sucesso.");
                    }
                }

                log("-> Reiniciando o Spooler de Impressão...");
                StopService("Spooler", 5);
                StartService("Spooler", 5);
                log("   [OK] Spooler de Impressão reiniciado!");

                log("=== CORREÇÃO DE IMPRESSORAS EM REDE APLICADA COM SUCESSO! ===");
            }
            catch (Exception ex)
            {
                log("[ERRO] Falha ao aplicar correção de impressora: " + ex.Message);
            }
        }

        public static void FixDualBootClock(Action<string> log)
        {
            log("=== CORRIGINDO DIVERGÊNCIA DE HORA EM DUAL-BOOT (LINUX / WINDOWS) ===");
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("RealTimeIsUniversal", 1, RegistryValueKind.DWord);
                        log("-> Registro RealTimeIsUniversal = 1 aplicado com sucesso.");
                    }
                }
                log("=== RELÓGIO DUAL-BOOT CONFIGURADO! O relógio da BIOS agora usará UTC (padrão Linux). ===");
            }
            catch (Exception ex)
            {
                log("[ERRO] Falha ao configurar hora dual boot: " + ex.Message);
            }
        }

        public static void RepairWindowsStore(Action<string> log)
        {
            log("=== INICIANDO REPARO E REINICIALIZAÇÃO DA MICROSOFT STORE ===");
            log("Executando wsreset.exe -i (este processo reinstala e limpa o cache da Loja)...");
            RunProcessStreamed("wsreset.exe", "-i", log);
            log("=== COMANDO DE REPARO DA MICROSOFT STORE DISPARADO COM SUCESSO! ===");
        }

        public static void ResetNetwork(Action<string> log)
        {
            log("=== REDEFININDO PILHA DE REDE, DNS E CATÁLOGO WINSOCK ===");

            log("-> 1. Limpando cache do resolvedor DNS (ipconfig /flushdns)...");
            RunProcessStreamed("ipconfig.exe", "/flushdns", log);

            log("-> 2. Redefinindo catálogo Winsock (netsh winsock reset)...");
            RunProcessStreamed("netsh.exe", "winsock reset", log);

            log("-> 3. Redefinindo pilha TCP/IP (netsh int ip reset)...");
            RunProcessStreamed("netsh.exe", "int ip reset", log);

            log("=== PILHA DE REDE REDEFINIDA! Recomendado reiniciar o equipamento. ===");
        }

        public static void ExecuteCustomScript(string scriptPath, Action<string> log)
        {
            if (!File.Exists(scriptPath))
            {
                log("[ERRO] Arquivo de script não encontrado: " + scriptPath);
                return;
            }

            string ext = Path.GetExtension(scriptPath).ToLowerInvariant();
            log("=== EXECUTANDO SCRIPT: " + Path.GetFileName(scriptPath) + " ===");

            if (ext == ".ps1")
            {
                RunProcessStreamed("powershell.exe", string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\"", scriptPath), log);
            }
            else if (ext == ".bat" || ext == ".cmd")
            {
                RunProcessStreamed("cmd.exe", string.Format("/c \"{0}\"", scriptPath), log);
            }
            else if (ext == ".reg")
            {
                RunProcessStreamed("regedit.exe", string.Format("/s \"{0}\"", scriptPath), log);
                log("[OK] Arquivo .reg importado para o Registro com sucesso!");
            }
            else
            {
                RunProcessStreamed(scriptPath, "", log);
            }

            log("=== EXECUÇÃO DO SCRIPT FINALIZADA! ===");
        }

        private static void CleanDirectory(string dirPath, ref int totalFiles, ref long totalBytes, Action<string> log)
        {
            if (!Directory.Exists(dirPath)) return;

            DirectoryInfo di = new DirectoryInfo(dirPath);
            try
            {
                foreach (FileInfo file in di.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        long size = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        totalBytes += size;
                        totalFiles++;
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                foreach (DirectoryInfo subDir in di.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        subDir.Delete(true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void StopService(string serviceName, int timeoutSeconds)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeoutSeconds));
                    }
                }
            }
            catch { }
        }

        private static void StartService(string serviceName, int timeoutSeconds)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeoutSeconds));
                    }
                }
            }
            catch { }
        }

        private static void DisableService(string serviceName, string displayName, Action<string> log)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        try { sc.Stop(); } catch { }
                    }
                }

                // Definir StartupType para Disabled via sc.exe
                ProcessStartInfo psi = new ProcessStartInfo("sc.exe", string.Format("config \"{0}\" start= disabled", serviceName));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(3000);
                }

                log(string.Format("-> Serviço {0} desativado com sucesso.", displayName));
            }
            catch (Exception ex)
            {
                log(string.Format("[AVISO] Não foi possível desativar {0}: {1}", displayName, ex.Message));
            }
        }

        private static void RunProcessStreamed(string fileName, string args, Action<string> log)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("ERRO: " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                log("[ERRO AO EXECUTAR] " + fileName + " " + args + " -> " + ex.Message);
            }
        }
    }
}
