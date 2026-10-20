using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace OptiAssist
{
    public class InstallAppItem : INotifyPropertyChanged
    {
        private string _name;
        private bool _preSelected;
        private string _installType; // "Local" or "Winget"
        private string _wingetId;
        private string _silentArgs;
        private bool _selected;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propName));
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public bool PreSelected
        {
            get { return _preSelected; }
            set { _preSelected = value; OnPropertyChanged("PreSelected"); }
        }

        public string InstallType
        {
            get { return string.IsNullOrEmpty(_installType) ? "Local" : _installType; }
            set
            {
                _installType = value;
                OnPropertyChanged("InstallType");
                OnPropertyChanged("IsLocalType");
                OnPropertyChanged("IsWingetType");
                OnPropertyChanged("DisplayTypeInfo");
            }
        }

        public string WingetId
        {
            get { return _wingetId ?? ""; }
            set { _wingetId = value; OnPropertyChanged("WingetId"); }
        }

        public string SilentArgs
        {
            get { return _silentArgs ?? ""; }
            set { _silentArgs = value; OnPropertyChanged("SilentArgs"); }
        }

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; OnPropertyChanged("Selected"); }
        }

        public bool IsLocalType
        {
            get { return string.Equals(InstallType, "Local", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsWingetType
        {
            get { return string.Equals(InstallType, "Winget", StringComparison.OrdinalIgnoreCase); }
        }

        private string _localInstallerFile;
        public string LocalInstallerFile
        {
            get { return _localInstallerFile ?? ""; }
            set
            {
                _localInstallerFile = value;
                OnPropertyChanged("LocalInstallerFile");
                OnPropertyChanged("DetailInfo");
                OnPropertyChanged("DisplayTypeInfo");
            }
        }

        public string DetailInfo
        {
            get
            {
                if (IsLocalType)
                {
                    return !string.IsNullOrEmpty(LocalInstallerFile) ? LocalInstallerFile : "";
                }
                return WingetId ?? "";
            }
        }

        public string DisplayTypeInfo
        {
            get
            {
                if (IsLocalType)
                {
                    if (!string.IsNullOrEmpty(LocalInstallerFile))
                    {
                        return string.Format("📁 Local ({0})", LocalInstallerFile);
                    }
                    string clean = !string.IsNullOrEmpty(Name) ? Name.Replace(" ", "") : "App";
                    return string.Format("📁 Local ({0}Setup.exe)", clean);
                }
                return "🌐 Winget";
            }
        }

        public string AutomateLabel
        {
            get { return LocalizationHelper.Get("Custom_Chk_Automate", "Automate"); }
        }

        public string ToolTipMoveUp
        {
            get { return LocalizationHelper.Get("Tooltip_MoveUp", "Move up"); }
        }

        public string ToolTipMoveDown
        {
            get { return LocalizationHelper.Get("Tooltip_MoveDown", "Move down"); }
        }

        public string ToolTipEdit
        {
            get { return LocalizationHelper.Get("Tooltip_Edit", "Edit"); }
        }

        public string ToolTipRemove
        {
            get { return LocalizationHelper.Get("Tooltip_Remove", "Remove"); }
        }

        public void RefreshLocalization()
        {
            OnPropertyChanged("AutomateLabel");
            OnPropertyChanged("DisplayTypeInfo");
            OnPropertyChanged("ToolTipMoveUp");
            OnPropertyChanged("ToolTipMoveDown");
            OnPropertyChanged("ToolTipEdit");
            OnPropertyChanged("ToolTipRemove");
        }

        // Legacy properties for backwards compatibility
        public string Id
        {
            get { return !string.IsNullOrEmpty(Name) ? Name.ToLowerInvariant().Replace(" ", "") : ""; }
        }

        public bool IsEssential
        {
            get { return PreSelected; }
            set { PreSelected = value; }
        }

        public string Category { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public bool PrioritizeWinget
        {
            get { return IsWingetType; }
            set { InstallType = value ? "Winget" : "Local"; }
        }

        public InstallAppItem()
        {
            _name = "";
            _preSelected = false;
            _installType = "Local";
            _localInstallerFile = "";
            _wingetId = "";
            _silentArgs = "";
            _selected = false;
            Status = "Pronto";
        }

        public InstallAppItem(string name, bool preSelected, string installType, string wingetId = "", string silentArgs = "", string localInstallerFile = "")
        {
            _name = name;
            _preSelected = preSelected;
            _installType = string.IsNullOrEmpty(installType) ? "Local" : installType;
            _wingetId = wingetId ?? "";
            _silentArgs = silentArgs ?? "";
            _localInstallerFile = localInstallerFile ?? "";
            _selected = preSelected;
            Status = "Pronto";
        }
    }

    public static class InstallerHelper
    {
        public static string GetPostInstallDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string postDir = Path.Combine(baseDir, "PostInstall");
            if (!Directory.Exists(postDir))
            {
                try { Directory.CreateDirectory(postDir); } catch { }
            }
            return postDir;
        }

        public static string GetPostInstallJsonPath()
        {
            string configDir = SettingsHelper.GetConfigDirectory();
            string newPath = Path.Combine(configDir, "autoinstall.json");
            string legacyPath = Path.Combine(configDir, "PostInstall.json");

            if (!File.Exists(newPath) && File.Exists(legacyPath))
            {
                try { File.Move(legacyPath, newPath); } catch { }
            }

            return newPath;
        }

        public static List<InstallAppItem> GetDefaultApps()
        {
            List<InstallAppItem> list = new List<InstallAppItem>();

            // Default application catalog entries
            list.Add(new InstallAppItem("Google Chrome", true, "Local", "Google.Chrome", "/silent /install"));
            list.Add(new InstallAppItem("7-Zip", true, "Local", "7zip.7zip", "/S"));
            list.Add(new InstallAppItem("Microsoft Office", true, "Local", "Microsoft.Office", "/configure configuration.xml"));
            list.Add(new InstallAppItem("WinRAR", false, "Local", "RARLab.WinRAR", "/s"));
            list.Add(new InstallAppItem("NanaZip", false, "Winget", "M2Team.NanaZip", ""));
            list.Add(new InstallAppItem("VLC Media Player", false, "Local", "VideoLAN.VLC", "/S"));
            list.Add(new InstallAppItem("Adobe Acrobat Reader", false, "Local", "Adobe.Acrobat.Reader.64-bit", "/sAll /rs /msi EULA_ACCEPT=YES"));
            list.Add(new InstallAppItem("AnyDesk", false, "Local", "AnyDeskSoftwareGmbH.AnyDesk", "--install \"C:\\Program Files (x86)\\AnyDesk\" --silent"));
            list.Add(new InstallAppItem("Mozilla Firefox", false, "Local", "Mozilla.Firefox", "-ms"));
            list.Add(new InstallAppItem("Notepad++", false, "Local", "Notepad++.Notepad++", "/S"));

            return list;
        }

        public static List<InstallAppItem> LoadApps()
        {
            string path = GetPostInstallJsonPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var apps = ParseJsonApps(json);
                    if (apps != null && apps.Count > 0)
                    {
                        return apps;
                    }
                }
                catch { }
            }

            // If missing, empty or invalid, generate defaults and persist to autoinstall.json
            var defaultList = GetDefaultApps();
            SaveApps(defaultList);
            return defaultList;
        }

        public static void SaveApps(IEnumerable<InstallAppItem> items)
        {
            try
            {
                string path = GetPostInstallJsonPath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                var list = new List<InstallAppItem>(items);
                for (int i = 0; i < list.Count; i++)
                {
                    var app = list[i];
                    sb.AppendLine("  {");
                    sb.AppendLine(string.Format("    \"Name\": \"{0}\",", EscapeJson(app.Name)));
                    sb.AppendLine(string.Format("    \"PreSelected\": {0},", app.PreSelected ? "true" : "false"));
                    sb.AppendLine(string.Format("    \"InstallType\": \"{0}\",", EscapeJson(app.InstallType ?? "Local")));
                    sb.AppendLine(string.Format("    \"LocalInstallerFile\": \"{0}\",", EscapeJson(app.LocalInstallerFile ?? "")));
                    sb.AppendLine(string.Format("    \"WingetId\": \"{0}\",", EscapeJson(app.WingetId ?? "")));
                    sb.AppendLine(string.Format("    \"SilentArgs\": \"{0}\"", EscapeJson(app.SilentArgs ?? "")));
                    sb.Append("  }");
                    if (i < list.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("]");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static string FindLocalInstaller(InstallAppItem app)
        {
            if (app == null || string.IsNullOrEmpty(app.Name)) return null;

            string postDir = GetPostInstallDirectory();
            if (!Directory.Exists(postDir)) return null;

            // 0. Explicitly selected or configured executable file
            if (!string.IsNullOrEmpty(app.LocalInstallerFile))
            {
                string explicitPath = Path.Combine(postDir, app.LocalInstallerFile);
                if (File.Exists(explicitPath)) return explicitPath;
            }

            string rawName = app.Name.Trim();
            string nameNoSpace = rawName.Replace(" ", "");
            string nameNoHyphen = nameNoSpace.Replace("-", "");

            // 1. Strict priority candidates based on pattern: <Name>Setup.exe
            List<string> candidateNames = new List<string>
            {
                rawName + "Setup.exe",
                nameNoSpace + "Setup.exe",
                nameNoHyphen + "Setup.exe",
                rawName + " Setup.exe",
                nameNoSpace + "Setup.msi",
                rawName + "Setup.msi",
                nameNoSpace + ".exe",
                rawName + ".exe"
            };

            // Known special candidate names
            if (rawName.IndexOf("Office", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidateNames.Insert(0, "OfficeSetup.exe");
            }
            if (rawName.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidateNames.Insert(0, "ChromeSetup.exe");
            }
            if (rawName.IndexOf("7-Zip", StringComparison.OrdinalIgnoreCase) >= 0 || rawName.IndexOf("7Zip", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidateNames.Insert(0, "7ZipSetup.exe");
            }

            foreach (var cand in candidateNames)
            {
                string fullPath = Path.Combine(postDir, cand);
                if (File.Exists(fullPath)) return fullPath;
            }

            // 2. Secondary search in PostInstall folder for matching executable files
            try
            {
                string[] allFiles = Directory.GetFiles(postDir, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in allFiles)
                {
                    string fname = Path.GetFileNameWithoutExtension(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".exe" || ext == ".msi" || ext == ".msixbundle" || ext == ".appxbundle")
                    {
                        if (fname.IndexOf(nameNoSpace, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return file;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static string FindOfflineInstaller(InstallAppItem app)
        {
            return FindLocalInstaller(app);
        }

        public static void OpenPostInstallFolder()
        {
            string dir = GetPostInstallDirectory();
            try
            {
                Process.Start("explorer.exe", dir);
            }
            catch { }
        }

        public static void OpenPostInstallJson()
        {
            string path = GetPostInstallJsonPath();
            if (!File.Exists(path))
            {
                var defaults = GetDefaultApps();
                SaveApps(defaults);
            }
            try
            {
                Process.Start("notepad.exe", path);
            }
            catch { }
        }

        public static void InstallApp(InstallAppItem app, bool enableWingetFallback, Action<string> log)
        {
            if (app == null) return;

            // 1. Configured as Winget installation type
            if (app.IsWingetType)
            {
                string packageId = !string.IsNullOrEmpty(app.WingetId) ? app.WingetId : app.Name;
                log(string.Format("==> [WINGET METHOD] Installing '{0}' via Winget ({1})...", app.Name, packageId));
                bool ok = InstallViaWinget(packageId, log);
                if (ok)
                {
                    log(string.Format("[SUCCESS] '{0}' successfully installed via Winget!", app.Name));
                    return;
                }

                // Fallback to local installer if Winget fails
                log(string.Format("[WARNING] Winget failed for '{0}'. Checking for a local fallback installer...", app.Name));
                string fallbackLocal = FindLocalInstaller(app);
                if (!string.IsNullOrEmpty(fallbackLocal))
                {
                    InstallOfflineFile(app, fallbackLocal, log);
                }
                else
                {
                    log(string.Format("[FAILED] Could not install '{0}' via Winget and no local installer was found.", app.Name));
                }
                return;
            }

            // 2. Configured as Local installation type (searches for <Name>Setup.exe)
            log(string.Format("==> [LOCAL METHOD] Checking installer for '{0}' in the PostInstall\\ folder...", app.Name));
            string localFile = FindLocalInstaller(app);

            if (!string.IsNullOrEmpty(localFile) && File.Exists(localFile))
            {
                InstallOfflineFile(app, localFile, log);
                return;
            }

            // Local installer not found: try Winget fallback if enabled
            if (enableWingetFallback)
            {
                string packageId = !string.IsNullOrEmpty(app.WingetId) ? app.WingetId : app.Name;
                string cleanExpected = (!string.IsNullOrEmpty(app.Name) ? app.Name.Replace(" ", "") : "App") + "Setup.exe";
                log(string.Format("==> [WINGET FALLBACK] Local installer '{0}' not found. Trying to install {1} via Winget ({2})...",
                    cleanExpected, app.Name, packageId));

                bool wingetOk = InstallViaWinget(packageId, log);
                if (wingetOk)
                {
                    log(string.Format("[SUCCESS] {0} successfully installed via Winget!", app.Name));
                }
                else
                {
                    log(string.Format("[ERROR] Failed to install {0} via Winget. Check your internet connection.", app.Name));
                }
            }
            else
            {
                string cleanExpected = (!string.IsNullOrEmpty(app.Name) ? app.Name.Replace(" ", "") : "App") + "Setup.exe";
                log(string.Format("[SKIPPED] {0}: Local installer '{1}' missing in the PostInstall\\ folder and Winget fallback is disabled.",
                    app.Name, cleanExpected));
            }
        }

        private static bool InstallOfflineFile(InstallAppItem app, string offlineFile, Action<string> log)
        {
            log(string.Format("==> [RUNNING LOCAL] File: {0}...", Path.GetFileName(offlineFile)));

            string ext = Path.GetExtension(offlineFile).ToLowerInvariant();
            ProcessStartInfo psi = new ProcessStartInfo();

            if (ext == ".msi")
            {
                psi.FileName = "msiexec.exe";
                psi.Arguments = string.Format("/i \"{0}\" /qn /norestart", offlineFile);
            }
            else if (ext == ".msixbundle" || ext == ".appxbundle" || ext == ".msix")
            {
                psi.FileName = "powershell.exe";
                psi.Arguments = string.Format("-ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{0}'\"", offlineFile);
            }
            else
            {
                psi.FileName = offlineFile;
                psi.Arguments = !string.IsNullOrEmpty(app.SilentArgs) ? app.SilentArgs : "";
            }

            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    if (proc.ExitCode == 0 || proc.ExitCode == 3010)
                    {
                        log(string.Format("[OK] {0} successfully installed via local installer!", app.Name));
                        return true;
                    }
                    else
                    {
                        log(string.Format("[WARNING] {0} finished with exit code {1}.", app.Name, proc.ExitCode));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                log(string.Format("[ERROR] Failed to run installer for {0}: {1}", app.Name, ex.Message));
                return false;
            }
        }

        private static bool InstallViaWinget(string wingetIdOrName, Action<string> log)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "winget.exe";
            psi.Arguments = string.Format("install --id \"{0}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", wingetIdOrName);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    if (proc.ExitCode == 0) return true;

                    // If --id failed, try fallback command matching directly by name
                    log(string.Format("    [Winget] Trying fallback search by name: {0}...", wingetIdOrName));
                    ProcessStartInfo psiFallback = new ProcessStartInfo();
                    psiFallback.FileName = "winget.exe";
                    psiFallback.Arguments = string.Format("install \"{0}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", wingetIdOrName);
                    psiFallback.UseShellExecute = false;
                    psiFallback.CreateNoWindow = true;
                    psiFallback.RedirectStandardOutput = true;
                    psiFallback.RedirectStandardError = true;

                    using (Process procFb = Process.Start(psiFallback))
                    {
                        procFb.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                        procFb.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log("    " + e.Data); };
                        procFb.BeginOutputReadLine();
                        procFb.BeginErrorReadLine();
                        procFb.WaitForExit();
                        return (procFb.ExitCode == 0);
                    }
                }
            }
            catch (Exception ex)
            {
                log(string.Format("[ERROR] Failed to invoke Winget for {0}: {1}", wingetIdOrName, ex.Message));
                return false;
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static List<InstallAppItem> ParseJsonApps(string json)
        {
            var list = new List<InstallAppItem>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            int i = 0;
            while (i < json.Length)
            {
                // Find object opening '{'
                while (i < json.Length && json[i] != '{') i++;
                if (i >= json.Length) break;
                i++; // Skip '{'

                Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                while (i < json.Length && json[i] != '}')
                {
                    // Skip whitespace and commas
                    while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
                    if (i >= json.Length || json[i] == '}') break;

                    // Read key (quoted string)
                    if (json[i] == '\"')
                    {
                        i++;
                        int keyStart = i;
                        while (i < json.Length && json[i] != '\"')
                        {
                            if (json[i] == '\\' && i + 1 < json.Length) i += 2;
                            else i++;
                        }
                        string key = json.Substring(keyStart, i - keyStart);
                        if (i < json.Length) i++; // Skip closing quote

                        // Find ':'
                        while (i < json.Length && json[i] != ':') i++;
                        if (i < json.Length) i++; // Skip ':'

                        // Skip whitespace
                        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                        // Read value
                        string val = "";
                        if (i < json.Length && json[i] == '\"')
                        {
                            i++;
                            StringBuilder valSb = new StringBuilder();
                            while (i < json.Length && json[i] != '\"')
                            {
                                if (json[i] == '\\' && i + 1 < json.Length)
                                {
                                    i++;
                                    if (json[i] == 'n') valSb.Append('\n');
                                    else if (json[i] == 'r') valSb.Append('\r');
                                    else if (json[i] == 't') valSb.Append('\t');
                                    else valSb.Append(json[i]);
                                    i++;
                                }
                                else
                                {
                                    valSb.Append(json[i]);
                                    i++;
                                }
                            }
                            val = valSb.ToString();
                            if (i < json.Length) i++; // Skip closing quote
                        }
                        else
                        {
                            // Boolean, number or null
                            int valStart = i;
                            while (i < json.Length && json[i] != ',' && json[i] != '}' && !char.IsWhiteSpace(json[i])) i++;
                            val = json.Substring(valStart, i - valStart).Trim();
                        }

                        dict[key] = val;
                    }
                    else
                    {
                        i++;
                    }
                }

                if (i < json.Length && json[i] == '}') i++;

                if (dict.ContainsKey("Name") && !string.IsNullOrWhiteSpace(dict["Name"]))
                {
                    string name = dict["Name"];
                    bool preSelected = false;
                    if (dict.ContainsKey("PreSelected"))
                    {
                        bool b;
                        if (bool.TryParse(dict["PreSelected"], out b)) preSelected = b;
                    }
                    else if (dict.ContainsKey("IsEssential"))
                    {
                        bool b;
                        if (bool.TryParse(dict["IsEssential"], out b)) preSelected = b;
                    }

                    string installType = dict.ContainsKey("InstallType") ? dict["InstallType"] : "Local";
                    string localInstallerFile = dict.ContainsKey("LocalInstallerFile") ? dict["LocalInstallerFile"] : "";
                    string wingetId = dict.ContainsKey("WingetId") ? dict["WingetId"] : "";
                    string silentArgs = dict.ContainsKey("SilentArgs") ? dict["SilentArgs"] : "";

                    list.Add(new InstallAppItem(name, preSelected, installType, wingetId, silentArgs, localInstallerFile));
                }
            }

            return list;
        }

        #region WINGET UPGRADE
        public static List<WingetUpgradeItem> CheckAvailableUpgrades(Action<string> log)
        {
            var list = new List<WingetUpgradeItem>();
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "winget.exe";
            psi.Arguments = "upgrade";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;

            StringBuilder sbOutput = new StringBuilder();

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            sbOutput.AppendLine(e.Data);
                            if (log != null) log("    " + e.Data);
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && log != null) log("    " + e.Data);
                    };

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }

                list = ParseWingetUpgradeOutput(sbOutput.ToString());
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[ERROR] Failed to check for updates via Winget: {0}", ex.Message));
            }

            return list;
        }

        public static List<WingetUpgradeItem> ParseWingetUpgradeOutput(string output)
        {
            var list = new List<WingetUpgradeItem>();
            if (string.IsNullOrWhiteSpace(output)) return list;

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool tableStarted = false;
            int idCol = -1, verCol = -1, availCol = -1, sourceCol = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Contains("---") && i > 0)
                {
                    string header = lines[i - 1];
                    idCol = IndexOfAny(header, "Id", "Identificador");
                    verCol = IndexOfAny(header, "Version", "Versão");
                    availCol = IndexOfAny(header, "Available", "Disponível");
                    sourceCol = IndexOfAny(header, "Source", "Origem");
                    tableStarted = true;
                    continue;
                }

                if (!tableStarted) continue;

                if (line.Contains("upgrades available") || line.Contains("atualizações disponíveis") ||
                    line.Contains("have version numbers") || line.Trim().Length == 0)
                {
                    continue;
                }

                if (idCol > 0 && line.Length > idCol)
                {
                    string name = line.Substring(0, idCol).Trim();
                    string id = "";
                    string version = "";
                    string available = "";

                    if (verCol > idCol && line.Length > verCol)
                    {
                        id = line.Substring(idCol, verCol - idCol).Trim();
                        if (availCol > verCol && line.Length > availCol)
                        {
                            version = line.Substring(verCol, availCol - verCol).Trim();
                            if (sourceCol > availCol && line.Length > sourceCol)
                            {
                                available = line.Substring(availCol, sourceCol - availCol).Trim();
                            }
                            else
                            {
                                available = line.Substring(availCol).Trim();
                            }
                        }
                        else
                        {
                            version = line.Substring(verCol).Trim();
                        }
                    }
                    else
                    {
                        id = line.Substring(idCol).Trim();
                    }

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id) && !name.StartsWith("-"))
                    {
                        list.Add(new WingetUpgradeItem(name, id, version, available));
                    }
                }
                else
                {
                    string[] parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s{2,}");
                    if (parts.Length >= 4 && !parts[0].StartsWith("-"))
                    {
                        list.Add(new WingetUpgradeItem(parts[0], parts[1], parts[2], parts[3]));
                    }
                }
            }

            return list;
        }

        private static int IndexOfAny(string str, params string[] values)
        {
            foreach (var val in values)
            {
                int idx = str.IndexOf(val, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return idx;
            }
            return -1;
        }

        public static bool UpgradeWingetPackage(string packageId, Action<string> log)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "winget.exe";
            psi.Arguments = string.Format("upgrade --id \"{0}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", packageId);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data) && log != null) log("    " + e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data) && log != null) log("    " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    return (proc.ExitCode == 0);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[ERROR] Failed to update {0}: {1}", packageId, ex.Message));
                return false;
            }
        }

        public static bool UpgradeAllWingetPackages(Action<string> log)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "winget.exe";
            psi.Arguments = "upgrade --all --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data) && log != null) log("    " + e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data) && log != null) log("    " + e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    return (proc.ExitCode == 0);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log(string.Format("[ERROR] Failed to run Winget bulk upgrade: {0}", ex.Message));
                return false;
            }
        }
        #endregion
    }

    public class WingetUpgradeItem : INotifyPropertyChanged
    {
        private bool _selected;
        private string _status;

        public string Name { get; set; }
        public string Id { get; set; }
        public string InstalledVersion { get; set; }
        public string AvailableVersion { get; set; }

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; OnPropertyChanged("Selected"); }
        }

        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged("Status"); }
        }

        public string VersionDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(InstalledVersion) && !string.IsNullOrEmpty(AvailableVersion))
                {
                    return string.Format("{0}  ➔  {1}", InstalledVersion, AvailableVersion);
                }
                return AvailableVersion ?? "";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(prop));
        }

        public WingetUpgradeItem(string name, string id, string installed, string available)
        {
            Name = name ?? "";
            Id = id ?? "";
            InstalledVersion = installed ?? "";
            AvailableVersion = available ?? "";
            Selected = true;
            Status = "Available";
        }
    }
}
