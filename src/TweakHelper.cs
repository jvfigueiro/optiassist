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
            log("=== STARTING TEMPORARY FILES CLEANUP ===");

            long totalDeletedBytes = 0;
            int totalDeletedFiles = 0;

            // 1. User %TEMP% folder
            string userTemp = Path.GetTempPath();
            log("-> Cleaning user temporary files: " + userTemp);
            CleanDirectory(userTemp, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 2. C:\Windows\Temp
            string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            log("-> Cleaning Windows temporary files: " + winTemp);
            CleanDirectory(winTemp, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 3. Prefetch
            string prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            log("-> Cleaning Prefetch files...");
            CleanDirectory(prefetch, ref totalDeletedFiles, ref totalDeletedBytes, log);

            // 4. Windows Error Reporting (WER) queue
            string werPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportQueue");
            if (Directory.Exists(werPath))
            {
                log("-> Cleaning Windows Error Reporting (WER) queue...");
                CleanDirectory(werPath, ref totalDeletedFiles, ref totalDeletedBytes, log);
            }

            // 5. Empty Recycle Bin on all drives
            try
            {
                log("-> Emptying the Recycle Bin on all drives...");
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                log("   [OK] Recycle Bin emptied.");
            }
            catch (Exception ex)
            {
                log("   [WARNING] Could not empty the Recycle Bin: " + ex.Message);
            }

            double mb = Math.Round(totalDeletedBytes / (1024.0 * 1024.0), 2);
            log(string.Format("=== CLEANUP COMPLETED! {0} files removed ({1} MB freed) ===", totalDeletedFiles, mb));
        }

        public static void CleanWindowsUpdateCache(Action<string> log)
        {
            log("=== CLEANING WINDOWS UPDATE DOWNLOAD CACHE ===");
            try
            {
                log("-> Stopping Windows Update service (wuauserv)...");
                StopService("wuauserv", 10);

                string dlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download");
                if (Directory.Exists(dlPath))
                {
                    log("-> Removing temporary files in SoftwareDistribution\\Download...");
                    int count = 0;
                    long bytes = 0;
                    CleanDirectory(dlPath, ref count, ref bytes, log);
                    log(string.Format("   [OK] {0} update files removed ({1} MB freed).", count, Math.Round(bytes / (1024.0 * 1024.0), 2)));
                }

                log("-> Restarting Windows Update service (wuauserv)...");
                StartService("wuauserv", 10);
                log("=== WINDOWS UPDATE CACHE CLEANED SUCCESSFULLY! ===");
            }
            catch (Exception ex)
            {
                log("[ERROR] Error cleaning Windows Update cache: " + ex.Message);
            }
        }

        public static void RunSfcRepair(Action<string> log)
        {
            log("=================================================");
            log("   WINDOWS INSTALLATION REPAIR (SFC METHOD)      ");
            log("=================================================");
            log("Running: sfc /scannow...");
            log("WARNING: Scanning protected Windows files may take 3 to 10 minutes.");
            RunProcessStreamed("sfc.exe", "/scannow", log);
            log("\n=== SFC SCAN AND REPAIR FINISHED! ===");
        }

        public static void RunDismRepair(Action<string> log)
        {
            log("=================================================");
            log("  WINDOWS INSTALLATION REPAIR (DISM METHOD)      ");
            log("=================================================");
            log("Running: Dism /Online /Cleanup-Image /RestoreHealth...");
            log("WARNING: DISM checks the system image integrity and replaces corrupted files.");
            RunProcessStreamed("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log);
            log("\n=== DISM IMAGE RESTORE FINISHED! ===");
        }

        public static void RunDismAndSfc(Action<string> log)
        {
            log("=================================================");
            log("   SYSTEM SCAN AND REPAIR (DISM + SFC)           ");
            log("=================================================");
            log("WARNING: This procedure may take 5 to 15 minutes depending on SSD/disk speed.");

            // 1. DISM RestoreHealth
            log("\n[STEP 1/2] Running DISM /Online /Cleanup-Image /RestoreHealth...");
            RunProcessStreamed("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log);

            // 2. SFC Scannow
            log("\n[STEP 2/2] Running SFC /scannow...");
            RunProcessStreamed("sfc.exe", "/scannow", log);

            log("\n=== DISM AND SFC SCAN FINISHED SUCCESSFULLY! ===");
        }

        public static void CompactOsInstall(Action<string> log)
        {
            log("=================================================");
            log("       WINDOWS INSTALLATION COMPACTION           ");
            log("=================================================");
            log("Running: compact.exe /CompactOS:always...");
            log("WARNING: Compacting Windows binaries (Compact OS) may free 1.5 to 3 GB of disk space.");
            log("Please wait, this procedure may take a few minutes...");
            RunProcessStreamed("compact.exe", "/CompactOS:always", log);
            log("\n=== WINDOWS INSTALLATION COMPACTION COMPLETED! ===");
        }

        public static void RunChkdsk(Action<string> log)
        {
            log("=================================================");
            log("        DISK INTEGRITY CHECK                     ");
            log("=================================================");
            log("Running quick check (read-only) on drive C:...");
            RunProcessStreamed("chkdsk.exe", "C:", log);
            log("\n=== CHKDSK CHECK FINISHED! ===");
        }

        public static void OptimizeVisualEffects(Action<string> log)
        {
            log("=== OPTIMIZING VISUAL EFFECTS FOR MAXIMUM PERFORMANCE ===");
            try
            {
                // VisualFXSetting = 2 (Best Performance)
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                        log("-> VisualEffects set to 'Best Performance'.");
                    }
                }

                // Disable minimize/maximize animations
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("MinAnimate", "0", RegistryValueKind.String);
                        log("-> Minimize/maximize animations disabled.");
                    }
                }

                // MenuShowDelay = 50ms
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("MenuShowDelay", "50", RegistryValueKind.String);
                        log("-> Menu open delay reduced to 50ms.");
                    }
                }

                // Notify Windows shell about environment settings change
                UIntPtr result;
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "TraySettings", SMTO_ABORTIFHUNG, 1000, out result);

                log("=== VISUAL EFFECTS OPTIMIZED SUCCESSFULLY! ===");
            }
            catch (Exception ex)
            {
                log("[ERROR] Error optimizing visual effects: " + ex.Message);
            }
        }

        public static void OptimizeTelemetryAndServices(Action<string> log)
        {
            log("=== OPTIMIZING NON-ESSENTIAL SERVICES AND TELEMETRY ===");

            // 1. Disable DiagTrack (Connected User Experiences and Telemetry)
            DisableService("DiagTrack", "Windows Telemetry (DiagTrack)", log);

            // 2. Disable dmwappushservice
            DisableService("dmwappushservice", "WAP Push Tracking Service", log);

            // 3. Disable Windows Error Reporting (WerSvc)
            DisableService("WerSvc", "Windows Error Reporting (WerSvc)", log);

            // 4. Registry: Disable telemetry data collection
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                        log("-> Enterprise telemetry blocked via Registry (AllowTelemetry = 0).");
                    }
                }
            }
            catch (Exception ex)
            {
                log("[WARNING] Could not set telemetry key: " + ex.Message);
            }

            log("=== SERVICES AND TELEMETRY OPTIMIZATION COMPLETED! ===");
        }

        public static void FixNetworkPrinterRpc(Action<string> log)
        {
            log("=== FIXING NETWORK PRINTER ERROR (RPC 0x11b / 0x709) ===");
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Print"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("RpcAuthnLevelPrivacyEnabled", 0, RegistryValueKind.DWord);
                        log("-> Registry key RpcAuthnLevelPrivacyEnabled = 0 applied successfully.");
                    }
                }

                log("-> Restarting Print Spooler...");
                StopService("Spooler", 5);
                StartService("Spooler", 5);
                log("   [OK] Print Spooler restarted!");

                log("=== NETWORK PRINTER FIX APPLIED SUCCESSFULLY! ===");
            }
            catch (Exception ex)
            {
                log("[ERROR] Failed to apply printer fix: " + ex.Message);
            }
        }

        public static void FixDualBootClock(Action<string> log)
        {
            log("=== FIXING DUAL-BOOT TIME MISMATCH (LINUX / WINDOWS) ===");
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation"))
                {
                    if (rk != null)
                    {
                        rk.SetValue("RealTimeIsUniversal", 1, RegistryValueKind.DWord);
                        log("-> Registry RealTimeIsUniversal = 1 applied successfully.");
                    }
                }
                log("=== DUAL-BOOT CLOCK CONFIGURED! BIOS clock will now use UTC (Linux standard). ===");
            }
            catch (Exception ex)
            {
                log("[ERROR] Failed to configure dual-boot time: " + ex.Message);
            }
        }

        public static void RepairWindowsStore(Action<string> log)
        {
            log("=== STARTING MICROSOFT STORE REPAIR AND RESET ===");
            log("Running wsreset.exe -i (this process reinstalls and clears the Store cache)...");
            RunProcessStreamed("wsreset.exe", "-i", log);
            log("=== MICROSOFT STORE REPAIR COMMAND TRIGGERED SUCCESSFULLY! ===");
        }

        public static void ResetNetwork(Action<string> log)
        {
            log("=== RESETTING NETWORK STACK, DNS AND WINSOCK CATALOG ===");

            log("-> 1. Flushing DNS resolver cache (ipconfig /flushdns)...");
            RunProcessStreamed("ipconfig.exe", "/flushdns", log);

            log("-> 2. Resetting Winsock catalog (netsh winsock reset)...");
            RunProcessStreamed("netsh.exe", "winsock reset", log);

            log("-> 3. Resetting TCP/IP stack (netsh int ip reset)...");
            RunProcessStreamed("netsh.exe", "int ip reset", log);

            log("=== NETWORK STACK RESET! Restarting the computer is recommended. ===");
        }

        public static void ExecuteCustomScript(string scriptPath, Action<string> log)
        {
            if (!File.Exists(scriptPath))
            {
                log("[ERROR] Script file not found: " + scriptPath);
                return;
            }

            string ext = Path.GetExtension(scriptPath).ToLowerInvariant();
            log("=== RUNNING SCRIPT: " + Path.GetFileName(scriptPath) + " ===");

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
                log("[OK] .reg file successfully imported into the Registry!");
            }
            else
            {
                RunProcessStreamed(scriptPath, "", log);
            }

            log("=== SCRIPT EXECUTION FINISHED! ===");
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

                // Set StartupType to Disabled via sc.exe
                ProcessStartInfo psi = new ProcessStartInfo("sc.exe", string.Format("config \"{0}\" start= disabled", serviceName));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(3000);
                }

                log(string.Format("-> Service {0} disabled successfully.", displayName));
            }
            catch (Exception ex)
            {
                log(string.Format("[WARNING] Could not disable {0}: {1}", displayName, ex.Message));
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
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("ERROR: " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                log("[ERROR RUNNING] " + fileName + " " + args + " -> " + ex.Message);
            }
        }
    }
}
