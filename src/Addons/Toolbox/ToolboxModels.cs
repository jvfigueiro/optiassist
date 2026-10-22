using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OptiAssist.Addons.Toolbox
{
    /// <summary>
    /// Supported interpreter shells for Toolbox custom actions.
    /// </summary>
    public static class ToolboxShellType
    {
        public const string Cmd = "cmd";
        public const string PowerShell = "powershell";
        public const string Binary = "binary";

        public static bool IsValid(string shell)
        {
            if (string.IsNullOrWhiteSpace(shell)) return false;
            string s = shell.Trim().ToLowerInvariant();
            return s == Cmd || s == PowerShell || s == Binary;
        }

        public static string Normalize(string shell)
        {
            if (string.IsNullOrWhiteSpace(shell)) return Cmd;
            string s = shell.Trim().ToLowerInvariant();
            if (s == PowerShell || s == "pwsh" || s == "ps1") return PowerShell;
            if (s == Binary || s == "exe" || s == "direct") return Binary;
            return Cmd;
        }
    }

    /// <summary>
    /// Data Model (Schema) representing a persisted Toolbox command action in Config\Addons\Toolbox\toolbox.json.
    /// </summary>
    public class ToolboxCommandItem : INotifyPropertyChanged
    {
        private string _id;
        private string _title;
        private string _description;
        private string _command;
        private string _category;
        private string _shell;
        private bool _waitForExit;
        private bool _captureOutput;
        private bool _requiresAdmin;
        private int _timeoutSeconds;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// UUID or unique string identifier.
        /// </summary>
        public string id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged("id"); }
        }

        /// <summary>
        /// Human-readable action name (e.g., "Check Disk Integrity C:").
        /// </summary>
        public string title
        {
            get { return _title; }
            set { _title = value; OnPropertyChanged("title"); }
        }

        /// <summary>
        /// Optional brief description of what the command does.
        /// </summary>
        public string description
        {
            get { return _description; }
            set
            {
                _description = value;
                OnPropertyChanged("description");
                OnPropertyChanged("HasDescription");
            }
        }

        /// <summary>
        /// Exact command line, script snippet, or binary path + arguments (e.g., "chkdsk /f /r C:").
        /// </summary>
        public string command
        {
            get { return _command; }
            set { _command = value; OnPropertyChanged("command"); }
        }

        /// <summary>
        /// Category name used for dynamic grouping in the UI (e.g., "Correção de Erros", "Rede", "Otimização").
        /// </summary>
        public string category
        {
            get { return _category; }
            set { _category = value; OnPropertyChanged("category"); }
        }

        /// <summary>
        /// Interpreter type: "cmd", "powershell", or "binary".
        /// </summary>
        public string shell
        {
            get { return _shell; }
            set
            {
                _shell = ToolboxShellType.Normalize(value);
                OnPropertyChanged("shell");
                OnPropertyChanged("ShellDisplayBadge");
            }
        }

        /// <summary>
        /// Whether execution should wait for process termination (true) or fire in background (false).
        /// </summary>
        public bool wait_for_exit
        {
            get { return _waitForExit; }
            set
            {
                _waitForExit = value;
                OnPropertyChanged("wait_for_exit");
            }
        }

        /// <summary>
        /// Whether stdout/stderr should be captured and streamed to the built-in console viewer and app logs.
        /// </summary>
        public bool capture_output
        {
            get { return _captureOutput; }
            set
            {
                _captureOutput = value;
                OnPropertyChanged("capture_output");
            }
        }

        /// <summary>
        /// Indicates whether the command requires Administrator privileges (displays the shield icon in UI).
        /// </summary>
        public bool requires_admin
        {
            get { return _requiresAdmin; }
            set
            {
                _requiresAdmin = value;
                OnPropertyChanged("requires_admin");
            }
        }

        /// <summary>
        /// Optional execution timeout in seconds (0 = no timeout limit).
        /// </summary>
        public int timeout_seconds
        {
            get { return _timeoutSeconds; }
            set
            {
                _timeoutSeconds = value < 0 ? 0 : value;
                OnPropertyChanged("timeout_seconds");
            }
        }

        public bool HasDescription
        {
            get { return !string.IsNullOrWhiteSpace(_description); }
        }

        public string ShellDisplayBadge
        {
            get
            {
                string norm = ToolboxShellType.Normalize(_shell);
                if (norm == ToolboxShellType.PowerShell) return "PowerShell";
                if (norm == ToolboxShellType.Binary) return "Binary (.exe)";
                return "CMD";
            }
        }

        public ToolboxCommandItem()
        {
            _id = Guid.NewGuid().ToString("N");
            _title = "";
            _description = "";
            _command = "";
            _category = "General";
            _shell = ToolboxShellType.Cmd;
            _waitForExit = true;
            _captureOutput = true;
            _requiresAdmin = false;
            _timeoutSeconds = 0;
        }

        public ToolboxCommandItem Clone()
        {
            return new ToolboxCommandItem()
            {
                id = this.id,
                title = this.title,
                description = this.description,
                command = this.command,
                category = this.category,
                shell = this.shell,
                wait_for_exit = this.wait_for_exit,
                capture_output = this.capture_output,
                requires_admin = this.requires_admin,
                timeout_seconds = this.timeout_seconds
            };
        }

        /// <summary>
        /// Validates all required schema fields before saving.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(this.id))
            {
                this.id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(this.title))
            {
                errorMessage = "The 'Title' field is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(this.command))
            {
                errorMessage = "The 'Command' field is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(this.category))
            {
                errorMessage = "The 'Category' field is required.";
                return false;
            }

            if (!ToolboxShellType.IsValid(this.shell))
            {
                errorMessage = "The 'Shell' field must be 'cmd', 'powershell', or 'binary'.";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// Heuristically checks if a command string invokes well-known Windows administrative tools.
        /// </summary>
        public static bool InferRequiresAdmin(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText)) return false;
            string lower = commandText.Trim().ToLowerInvariant();
            string[] adminKeywords = new string[]
            {
                "sfc", "dism", "chkdsk", "diskpart", "bcdedit", "wevtutil",
                "netsh", "sc ", "compact", "takeown", "icacls", "reg add \"hklm",
                "reg delete \"hklm", "stop-service", "restart-service", "set-service",
                "clear-eventlog", "disable-pnpdevice", "enable-pnpdevice", "format-volume"
            };

            foreach (string kw in adminKeywords)
            {
                if (lower.Contains(kw)) return true;
            }
            return false;
        }

        protected void OnPropertyChanged(string propName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propName));
            }
        }
    }

    /// <summary>
    /// Encapsulates the final status of a Toolbox command execution.
    /// </summary>
    public class ToolboxExecutionResult
    {
        public string CommandId { get; set; }
        public string Title { get; set; }
        public int ExitCode { get; set; }
        public bool Success { get; set; }
        public bool LaunchedInBackground { get; set; }
        public bool TimedOut { get; set; }
        public bool CanceledByUser { get; set; }
        public bool UacCanceled { get; set; }
        public bool CommandNotFound { get; set; }
        public int ProcessId { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> OutputLines { get; set; }

        public ToolboxExecutionResult()
        {
            OutputLines = new List<string>();
            ErrorMessage = "";
        }
    }
}
