using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Threading;

namespace OptiAssist.Addons.Toolbox
{
    /// <summary>
    /// Asynchronous process execution engine for Toolbox commands with real-time stdout/stderr streaming,
    /// exit code tracking, timeout enforcement, and UAC / missing-binary exception handling.
    /// </summary>
    public class ToolboxProcessRunner
    {
        private readonly object _syncLock = new object();
        private Process _activeProcess;
        private volatile bool _cancelRequested;
        private volatile bool _isRunning;

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public static bool IsCurrentProcessElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executes a Toolbox command asynchronously on a worker thread so the UI never freezes.
        /// </summary>
        public void RunAsync(
            ToolboxCommandItem item,
            Action<string, bool> onOutputLine,
            Action<ToolboxExecutionResult> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                ToolboxExecutionResult result = Execute(item, onOutputLine);
                if (onCompleted != null)
                {
                    onCompleted(result);
                }
            });
        }

        /// <summary>
        /// Requests termination of the currently running Toolbox process (if any).
        /// </summary>
        public void StopCurrentProcess()
        {
            _cancelRequested = true;
            Process procToKill = null;
            lock (_syncLock)
            {
                procToKill = _activeProcess;
            }

            if (procToKill != null)
            {
                try
                {
                    if (!procToKill.HasExited)
                    {
                        KillProcessTree(procToKill.Id);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Executes the given ToolboxCommandItem synchronously on the calling thread, streaming output lines
        /// via onOutputLine(lineText, isStdErr) and returning a structured ToolboxExecutionResult.
        /// </summary>
        public ToolboxExecutionResult Execute(ToolboxCommandItem item, Action<string, bool> onOutputLine)
        {
            ToolboxExecutionResult result = new ToolboxExecutionResult();
            if (item == null)
            {
                result.ErrorMessage = "Command item is null.";
                return result;
            }

            result.CommandId = item.id;
            result.Title = item.title;

            string validationError;
            if (!item.Validate(out validationError))
            {
                result.ErrorMessage = validationError;
                EmitLine(onOutputLine, result, "[ERROR] " + validationError, true);
                return result;
            }

            _cancelRequested = false;
            _isRunning = true;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                string fileName;
                string arguments;
                BuildProcessCommandLine(item, out fileName, out arguments);

                bool isElevated = IsCurrentProcessElevated();
                bool needsRunAsVerb = item.requires_admin && !isElevated;

                // Note: Win32 ProcessStartInfo only allows stdout/stderr redirection when UseShellExecute == false.
                // When OptiAssist runs as Administrator (default via app.manifest), UseShellExecute = false
                // already runs elevated AND captures real-time output. If the host is NOT elevated and the command
                // explicitly requires UAC elevation without output capture (or forces UAC prompt), we use Verb = "runas".
                bool canRedirectOutput = item.capture_output && !needsRunAsVerb;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = arguments;
                psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

                if (canRedirectOutput)
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                }
                else if (needsRunAsVerb)
                {
                    psi.UseShellExecute = true;
                    psi.CreateNoWindow = false;
                    psi.Verb = "runas";
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }
                else
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }

                using (Process proc = new Process())
                {
                    proc.StartInfo = psi;

                    if (canRedirectOutput)
                    {
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (e.Data != null)
                            {
                                EmitLine(onOutputLine, result, e.Data, false);
                            }
                        };

                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (e.Data != null)
                            {
                                EmitLine(onOutputLine, result, e.Data, true);
                            }
                        };
                    }

                    proc.Start();

                    lock (_syncLock)
                    {
                        _activeProcess = proc;
                    }

                    try { result.ProcessId = proc.Id; } catch { }

                    if (canRedirectOutput)
                    {
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                    }

                    // Fire-and-forget mode when wait_for_exit is false
                    if (!item.wait_for_exit)
                    {
                        sw.Stop();
                        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                        result.LaunchedInBackground = true;
                        result.ExitCode = 0;
                        result.Success = true;
                        EmitLine(onOutputLine, result,
                            string.Format("[INFO] Process launched in background (PID: {0}).", result.ProcessId), false);
                        return result;
                    }

                    int timeoutMs = item.timeout_seconds > 0 ? item.timeout_seconds * 1000 : -1;
                    int waitedMs = 0;
                    const int pollSliceMs = 150;

                    while (!proc.WaitForExit(pollSliceMs))
                    {
                        if (_cancelRequested)
                        {
                            KillProcessTree(result.ProcessId);
                            sw.Stop();
                            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                            result.CanceledByUser = true;
                            result.ExitCode = -1;
                            result.Success = false;
                            result.ErrorMessage = "Execution stopped by user.";
                            EmitLine(onOutputLine, result, "[WARN] Execution canceled by user.", true);
                            return result;
                        }

                        if (timeoutMs > 0)
                        {
                            waitedMs += pollSliceMs;
                            if (waitedMs >= timeoutMs)
                            {
                                KillProcessTree(result.ProcessId);
                                sw.Stop();
                                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                                result.TimedOut = true;
                                result.ExitCode = -2;
                                result.Success = false;
                                result.ErrorMessage = string.Format("Command timed out after {0} seconds.", item.timeout_seconds);
                                EmitLine(onOutputLine, result, "[TIMEOUT] " + result.ErrorMessage, true);
                                return result;
                            }
                        }
                    }

                    // Flush remaining async output buffers after process exit
                    if (canRedirectOutput)
                    {
                        proc.WaitForExit();
                    }

                    sw.Stop();
                    result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                    result.ExitCode = proc.ExitCode;
                    result.Success = (result.ExitCode == 0);

                    // CMD returns 9009 when a command/binary is not recognized
                    if (result.ExitCode == 9009)
                    {
                        result.CommandNotFound = true;
                        result.ErrorMessage = "Command or executable was not recognized (Exit Code 9009).";
                    }
                    else if (!result.Success)
                    {
                        result.ErrorMessage = string.Format("Process exited with non-zero code: {0}.", result.ExitCode);
                    }
                }
            }
            catch (Win32Exception wex)
            {
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                result.Success = false;
                result.ExitCode = wex.NativeErrorCode;

                // 1223 = ERROR_CANCELLED (User declined UAC elevation prompt)
                if (wex.NativeErrorCode == 1223)
                {
                    result.UacCanceled = true;
                    result.ErrorMessage = "UAC prompt was canceled by the user (Error 1223).";
                    EmitLine(onOutputLine, result, "[UAC CANCELED] " + result.ErrorMessage, true);
                }
                // 2 = ERROR_FILE_NOT_FOUND, 3 = ERROR_PATH_NOT_FOUND
                else if (wex.NativeErrorCode == 2 || wex.NativeErrorCode == 3)
                {
                    result.CommandNotFound = true;
                    result.ErrorMessage = string.Format("Executable or file not found (Win32 Error {0}): {1}", wex.NativeErrorCode, wex.Message);
                    EmitLine(onOutputLine, result, "[NOT FOUND] " + result.ErrorMessage, true);
                }
                else
                {
                    result.ErrorMessage = string.Format("Win32 error ({0}): {1}", wex.NativeErrorCode, wex.Message);
                    EmitLine(onOutputLine, result, "[WIN32 ERROR] " + result.ErrorMessage, true);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                EmitLine(onOutputLine, result, "[EXCEPTION] " + ex.Message, true);
            }
            finally
            {
                lock (_syncLock)
                {
                    _activeProcess = null;
                }
                _isRunning = false;
            }

            return result;
        }

        private static void EmitLine(Action<string, bool> callback, ToolboxExecutionResult result, string line, bool isError)
        {
            if (result != null && result.OutputLines != null)
            {
                lock (result.OutputLines)
                {
                    result.OutputLines.Add(line);
                }
            }

            if (callback != null)
            {
                try { callback(line, isError); } catch { }
            }
        }

        public static void BuildProcessCommandLine(ToolboxCommandItem item, out string fileName, out string arguments)
        {
            string shell = ToolboxShellType.Normalize(item.shell);
            string rawCmd = (item.command ?? "").Trim();

            if (shell == ToolboxShellType.PowerShell)
            {
                fileName = "powershell.exe";
                // Encode command as Base64 UTF-16LE (-EncodedCommand) or escaped command string so quotes/pipes work reliably
                string wrappedScript = "$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + rawCmd;
                byte[] utf16Bytes = Encoding.Unicode.GetBytes(wrappedScript);
                string base64Cmd = Convert.ToBase64String(utf16Bytes);
                arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + base64Cmd;
                return;
            }

            if (shell == ToolboxShellType.Binary)
            {
                SplitBinaryCommandLine(rawCmd, out fileName, out arguments);
                return;
            }

            // Default: CMD
            fileName = "cmd.exe";
            arguments = "/c " + rawCmd;
        }

        public static void SplitBinaryCommandLine(string commandLine, out string exePath, out string args)
        {
            exePath = "";
            args = "";
            if (string.IsNullOrWhiteSpace(commandLine)) return;

            string trimmed = commandLine.Trim();
            if (trimmed.StartsWith("\""))
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    exePath = trimmed.Substring(1, closingQuote - 1).Trim();
                    args = trimmed.Substring(closingQuote + 1).Trim();
                    return;
                }
            }

            int firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                exePath = trimmed.Substring(0, firstSpace).Trim();
                args = trimmed.Substring(firstSpace + 1).Trim();
            }
            else
            {
                exePath = trimmed;
                args = "";
            }
        }

        private static void KillProcessTree(int pid)
        {
            if (pid <= 0) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "taskkill.exe";
                psi.Arguments = string.Format("/PID {0} /T /F", pid);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process killer = Process.Start(psi))
                {
                    if (killer != null) killer.WaitForExit(3000);
                }
            }
            catch { }
        }
    }
}

