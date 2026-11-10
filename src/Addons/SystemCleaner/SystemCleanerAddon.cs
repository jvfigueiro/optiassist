using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using Microsoft.Win32;

namespace OptiAssist.Addons.SystemCleaner
{
    #region 1. RESULTS, CONTEXT & PATH SANITIZER (WITH VISUAL C++ & WEBVIEW2 PROTECTION)

    /// <summary>
    /// Represents an individual scanned file or item marked for cleaning with its size.
    /// </summary>
    public class ScannedFileEntry
    {
        public string Path { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay { get { return BaseCleaner.FormatBytes(SizeBytes); } }
        public string FileName
        {
            get
            {
                try { return System.IO.Path.GetFileName(Path); }
                catch { return Path; }
            }
        }
    }

    /// <summary>
    /// Result returned by BaseCleaner.Scan() during a non-destructive dry-run analysis.
    /// </summary>
    public class ScanResult
    {
        public string CleanerId { get; set; }
        public List<string> FoundItems { get; set; }
        public List<ScannedFileEntry> ScannedFiles { get; set; }
        public long EstimatedBytes { get; set; }
        public int SkippedOrLockedCount { get; set; }
        public List<string> Warnings { get; set; }
        private readonly HashSet<string> _seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ScanResult()
        {
            FoundItems = new List<string>();
            ScannedFiles = new List<ScannedFileEntry>();
            Warnings = new List<string>();
        }

        public void AddFile(string path, long size)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!_seenPaths.Add(path)) return;
            FoundItems.Add(path);
            ScannedFiles.Add(new ScannedFileEntry { Path = path, SizeBytes = size });
            EstimatedBytes += size;
        }
    }

    /// <summary>
    /// Result returned by BaseCleaner.Clean() after executing cleanup or maintenance.
    /// </summary>
    public class CleanResult
    {
        public string CleanerId { get; set; }
        public long FreedBytes { get; set; }
        public int DeletedCount { get; set; }
        public int ErrorOrLockedCount { get; set; }
        public List<string> Errors { get; set; }

        public CleanResult()
        {
            Errors = new List<string>();
        }
    }

    /// <summary>
    /// Execution context passed to BaseCleaner.Scan() and BaseCleaner.Clean().
    /// </summary>
    public class CleanerContext
    {
        public string DeepScanRootPath { get; set; }
        public int DeepScanMaxDepth { get; set; }
        public bool IsProcessElevated { get; set; }
        public Action<string> Logger { get; set; }

        public CleanerContext()
        {
            DeepScanRootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            DeepScanMaxDepth = 5;
            IsProcessElevated = CheckIsAdministrator();
        }

        public void Log(string message)
        {
            if (Logger != null)
            {
                Logger(message);
            }
        }

        public static bool CheckIsAdministrator()
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
    }

    /// <summary>
    /// Strict path sanitizer that prevents accidental deletion outside allowed scopes
    /// and explicitly protects Microsoft Visual C++ Redistributable caches, Windows Installer
    /// registration stores, and Microsoft Edge WebView2 runtime components.
    /// </summary>
    public static class PathSanitizer
    {
        private static readonly string[] ProtectedPathFragments = new string[]
        {
            @"\Microsoft\EdgeWebView",
            @"\EdgeWebView\",
            @"\EBWebView",
            @"\WebView2",
            @"msedgewebview2",
            @"\Package Cache",
            @"\Windows\Installer",
            @"\Windows\WinSxS",
            @"\Windows\System32",
            @"\Windows\SysWOW64",
            @"Microsoft Visual C++",
            @"vcredist",
            @"vc_runtime",
            @"vc_redist"
        };

        /// <summary>
        /// Validates whether a target file or directory path is safe for deletion.
        /// Rejects root drives, critical OS directories, Visual C++ / WebView2 components, and the OptiAssist directory.
        /// </summary>
        public static bool IsSafeToDelete(string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath)) return false;

            try
            {
                string full = Path.GetFullPath(candidatePath).TrimEnd('\\', '/');
                if (full.Length <= 3) return false; // Never allow drive root like "C:" or "C:\"

                string root = Path.GetPathRoot(full);
                if (!string.IsNullOrEmpty(root) && string.Equals(full, root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Explicitly protect Visual C++ Redistributable, Windows Installer, and WebView2 runtime paths
                foreach (string frag in ProtectedPathFragments)
                {
                    if (full.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }

                // Never allow deleting inside the running OptiAssist application directory
                string appBase = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                if (full.StartsWith(appBase, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');
                string sysDir = Environment.SystemDirectory.TrimEnd('\\', '/');
                string usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/'));
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\', '/');
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\', '/');
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\', '/');
                string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).TrimEnd('\\', '/');

                // Never allow deleting top-level system or user profile roots directly
                if (string.Equals(full, winDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, sysDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, progFiles, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(progFilesX86) && string.Equals(full, progFilesX86, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(usersRoot) && string.Equals(full, usersRoot, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(full, userProfile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, appData, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, localAppData, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, progData, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, docsDir, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Never allow deleting inside System32 or SysWOW64 (except systemprofile IE cache/logs if specified by XML)
                if (full.StartsWith(sysDir + "\\", StringComparison.OrdinalIgnoreCase) &&
                    full.IndexOf(@"\config\systemprofile\", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether a directory should be excluded during Deep Scan traversal.
        /// </summary>
        public static bool IsExcludedDeepScanDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath)) return true;
            try
            {
                string full = Path.GetFullPath(dirPath).TrimEnd('\\', '/');
                string name = Path.GetFileName(full);

                if (string.Equals(name, "$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Windows", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Program Files", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Program Files (x86)", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "ProgramData", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "AppData", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "EdgeWebView", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "EBWebView", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Package Cache", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!IsSafeToDelete(full))
                {
                    return true;
                }

                FileAttributes attrs = File.GetAttributes(full);
                if ((attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    return true; // Do not follow junctions or symlinks during deep scan
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    #endregion

    #region 2. BASE CLEANER ABSTRACT ARCHITECTURE

    /// <summary>
    /// Abstract base class modeled after BleachBit's modular cleaner option architecture.
    /// Every cleaner option exposes metadata, dry-run Scan(), and safe Clean() operations.
    /// </summary>
    public abstract class BaseCleaner : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private bool _isSelected;
        private long _sizeBytes;
        private string _sizeDisplay;
        private string _statusDisplay;
        private int _foundCount;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; protected set; }
        public string CategoryId { get; protected set; }
        public string Category { get; set; }
        public string Icon { get; protected set; }
        public bool RequiresAdmin { get; protected set; }
        public bool IsDangerous { get; protected set; }
        public string WarningMessage { get; set; }
        public bool IsRecommended { get; protected set; }
        public string TabId { get; set; }
        public string SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string SectionIcon { get; set; }
        public bool IsInstalledOnSystem { get; set; }
        public List<string> RunningExecutables { get; set; }
        public ScanResult LastScanResult { get; set; }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; OnPropertyChanged("Description"); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; OnPropertyChanged("IsSelected"); }
        }

        public long SizeBytes
        {
            get { return _sizeBytes; }
            set
            {
                _sizeBytes = value;
                _sizeDisplay = FormatBytes(value);
                OnPropertyChanged("SizeBytes");
                OnPropertyChanged("SizeDisplay");
            }
        }

        public string SizeDisplay
        {
            get { return _sizeDisplay; }
            set { _sizeDisplay = value; OnPropertyChanged("SizeDisplay"); }
        }

        public string StatusDisplay
        {
            get { return _statusDisplay; }
            set { _statusDisplay = value; OnPropertyChanged("StatusDisplay"); }
        }

        public int FoundCount
        {
            get { return _foundCount; }
            set { _foundCount = value; OnPropertyChanged("FoundCount"); }
        }

        protected BaseCleaner(
            string id,
            string categoryId,
            string category,
            string name,
            string description,
            string icon,
            bool requiresAdmin,
            bool isDangerous,
            string warningMessage,
            bool isRecommended)
        {
            Id = id;
            CategoryId = categoryId;
            Category = category;
            _name = name;
            _description = description;
            Icon = icon;
            RequiresAdmin = requiresAdmin;
            IsDangerous = isDangerous;
            WarningMessage = warningMessage;
            IsRecommended = isRecommended;
            TabId = "windows";
            SectionId = categoryId;
            SectionTitle = category;
            SectionIcon = icon;
            IsInstalledOnSystem = true;
            RunningExecutables = new List<string>();
            _isSelected = isRecommended && !isDangerous;
            _sizeBytes = 0;
            _sizeDisplay = "—";
            _statusDisplay = "Ready";
        }

        public abstract ScanResult Scan(CleanerContext context);
        public abstract CleanResult Clean(CleanerContext context);

        protected void OnPropertyChanged(string propName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propName));
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = new string[] { "B", "KB", "MB", "GB", "TB" };
            int idx = 0;
            double d = bytes;
            while (d >= 1024 && idx < suffixes.Length - 1)
            {
                d /= 1024;
                idx++;
            }
            return string.Format("{0:0.##} {1}", d, suffixes[idx]);
        }

        public static void DeleteFileSafe(string filePath, CleanResult result)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            if (!PathSanitizer.IsSafeToDelete(filePath))
            {
                result.ErrorOrLockedCount++;
                return;
            }

            try
            {
                FileInfo fi = new FileInfo(filePath);
                long len = fi.Exists ? fi.Length : 0;
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
                result.FreedBytes += len;
                result.DeletedCount++;
            }
            catch (IOException)
            {
                result.ErrorOrLockedCount++;
            }
            catch (UnauthorizedAccessException)
            {
                result.ErrorOrLockedCount++;
            }
            catch
            {
                result.ErrorOrLockedCount++;
            }
        }

        public static void TryRemoveEmptyDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;
            if (!PathSanitizer.IsSafeToDelete(dirPath)) return;
            try
            {
                if (Directory.GetFiles(dirPath).Length == 0 && Directory.GetDirectories(dirPath).Length == 0)
                {
                    Directory.Delete(dirPath, false);
                }
            }
            catch { }
        }
    }

    #endregion

    #region 3. NATIVE SQLITE & SHELL HELPER FOR CLEANERML ACTIONS

    public static class NativeCleanerHelper
    {
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open(byte[] filenameUtf8, out IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sqlUtf8, IntPtr callback, IntPtr arg, out IntPtr errMsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static void NotifyShellChanged()
        {
            try
            {
                // SHCNE_ASSOCCHANGED = 0x08000000, SHCNF_IDLIST = 0x0000
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        public static bool IsValidSqliteFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length < 100) return false;

                byte[] header = new byte[16];
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int read = fs.Read(header, 0, 16);
                    if (read < 16) return false;
                }
                string magic = Encoding.ASCII.GetString(header, 0, 15);
                return magic == "SQLite format 3";
            }
            catch
            {
                return false;
            }
        }

        public static bool IsFileLocked(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        public static bool ExecuteSqliteStatements(string dbPath, string sqlStatements, out long bytesSaved)
        {
            bytesSaved = 0;
            if (!IsValidSqliteFile(dbPath) || IsFileLocked(dbPath)) return false;

            long sizeBefore = 0;
            try { sizeBefore = new FileInfo(dbPath).Length; } catch { return false; }

            try
            {
                IntPtr db = IntPtr.Zero;
                byte[] pathBytes = Encoding.UTF8.GetBytes(dbPath + "\0");
                int rcOpen = sqlite3_open(pathBytes, out db);
                if (rcOpen == 0 && db != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr errMsg;
                        byte[] sqlBytes = Encoding.UTF8.GetBytes(sqlStatements + "\0");
                        int rcExec = sqlite3_exec(db, sqlBytes, IntPtr.Zero, IntPtr.Zero, out errMsg);
                        sqlite3_close(db);
                        db = IntPtr.Zero;

                        if (rcExec == 0)
                        {
                            long sizeAfter = new FileInfo(dbPath).Length;
                            if (sizeBefore > sizeAfter)
                            {
                                bytesSaved = sizeBefore - sizeAfter;
                            }
                            return true;
                        }
                    }
                    finally
                    {
                        if (db != IntPtr.Zero)
                        {
                            sqlite3_close(db);
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool VacuumSqliteDatabase(string dbPath, out long bytesSaved)
        {
            return ExecuteSqliteStatements(dbPath, "VACUUM;", out bytesSaved);
        }
    }

    #endregion

    #region 4. BLEACHBIT CLEANERML XML ENGINE (WINDOWS-ONLY)

    /// <summary>
    /// Represents a single parsed <action> instruction from a BleachBit CleanerML XML file.
    /// </summary>
    public class CleanerMlAction
    {
        public string Command { get; set; }
        public string Search { get; set; }
        public List<string> ExpandedPaths { get; set; }
        public string RawPath { get; set; }
        public string RegistryValueName { get; set; }
        public string JsonAddress { get; set; }
        public string IniSection { get; set; }
        public string IniParameter { get; set; }
        public string RegexPattern { get; set; }
        public string NRegexPattern { get; set; }
        public string WholeRegexPattern { get; set; }
        public string NWholeRegexPattern { get; set; }
        public string FileTypeFilter { get; set; } // "f" or "d" or empty
        public string ProcessCmd { get; set; }
        public bool ProcessWait { get; set; }

        public CleanerMlAction()
        {
            ExpandedPaths = new List<string>();
        }

        public bool MatchesRegexFilters(string fullPath, bool isDirectory)
        {
            if (!string.IsNullOrEmpty(FileTypeFilter))
            {
                if (string.Equals(FileTypeFilter, "f", StringComparison.OrdinalIgnoreCase) && isDirectory) return false;
                if (string.Equals(FileTypeFilter, "d", StringComparison.OrdinalIgnoreCase) && !isDirectory) return false;
            }

            string fileName = "";
            try { fileName = Path.GetFileName(fullPath); } catch { fileName = fullPath; }

            if (!string.IsNullOrEmpty(RegexPattern))
            {
                try
                {
                    if (!Regex.IsMatch(fileName, RegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return false;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(NRegexPattern))
            {
                try
                {
                    if (Regex.IsMatch(fileName, NRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return false;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(WholeRegexPattern))
            {
                try
                {
                    string normalized = fullPath.Replace('/', '\\');
                    if (!Regex.IsMatch(normalized, WholeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return false;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(NWholeRegexPattern))
            {
                try
                {
                    string normalized = fullPath.Replace('/', '\\');
                    if (Regex.IsMatch(normalized, NWholeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return false;
                }
                catch { }
            }

            return true;
        }
    }

    /// <summary>
    /// Represents a <cleaner>'s <option> loaded from a CleanerML XML file.
    /// Executes all Windows-compatible <action> rules defined for that option.
    /// </summary>
    public class CleanerMlOptionCleaner : BaseCleaner
    {
        public string CleanerXmlId { get; set; }
        public string OptionXmlId { get; set; }
        public string RawCleanerLabel { get; set; }
        public string RawOptionLabel { get; set; }
        public string RawOptionDescription { get; set; }
        public string RawWarning { get; set; }
        public List<CleanerMlAction> Actions { get; set; }

        public CleanerMlOptionCleaner(
            string cleanerXmlId,
            string optionXmlId,
            string cleanerLabel,
            string optionLabel,
            string optionDesc,
            string warning,
            string tabId,
            string icon,
            bool requiresAdmin,
            bool isDangerous,
            bool isRecommended,
            bool isInstalledOnSystem,
            List<string> runningExes,
            List<CleanerMlAction> actions)
            : base(
                cleanerXmlId + "." + optionXmlId,
                cleanerXmlId,
                cleanerLabel,
                optionLabel,
                optionDesc,
                icon,
                requiresAdmin,
                isDangerous,
                warning,
                isRecommended)
        {
            CleanerXmlId = cleanerXmlId;
            OptionXmlId = optionXmlId;
            RawCleanerLabel = cleanerLabel;
            RawOptionLabel = optionLabel;
            RawOptionDescription = optionDesc;
            RawWarning = warning ?? "";
            TabId = tabId;
            SectionId = cleanerXmlId;
            SectionTitle = cleanerLabel;
            SectionIcon = icon;
            IsInstalledOnSystem = isInstalledOnSystem;
            RunningExecutables = runningExes ?? new List<string>();
            Actions = actions ?? new List<CleanerMlAction>();
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };

            foreach (CleanerMlAction act in Actions)
            {
                try
                {
                    ExecuteActionScan(act, context, res);
                }
                catch
                {
                    res.SkippedOrLockedCount++;
                }
            }

            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };

            foreach (CleanerMlAction act in Actions)
            {
                try
                {
                    ExecuteActionClean(act, context, res);
                }
                catch
                {
                    res.ErrorOrLockedCount++;
                }
            }

            context.Log(string.Format("[CLEANERML] {0} - {1}: Freed {2} ({3} cleaned, {4} locked/skipped).",
                SectionTitle, Name, FormatBytes(res.FreedBytes), res.DeletedCount, res.ErrorOrLockedCount));
            return res;
        }

        #region ACTION SCAN & CLEAN IMPLEMENTATIONS

        private void ExecuteActionScan(CleanerMlAction act, CleanerContext context, ScanResult res)
        {
            string cmd = (act.Command ?? "delete").Trim().ToLowerInvariant();
            string search = (act.Search ?? "").Trim().ToLowerInvariant();

            if (cmd == "winreg")
            {
                ScanWinReg(act.RawPath, act.RegistryValueName, res);
                return;
            }

            if (cmd == "win.shell.change.notify" || cmd == "process" || cmd == "process.terminate")
            {
                return;
            }

            if (search == "deep")
            {
                string root = context != null ? context.DeepScanRootPath : null;
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    int maxDepth = context != null ? context.DeepScanMaxDepth : 5;
                    ScanDeepRecursive(root, 0, maxDepth, act, res);
                }
                return;
            }

            List<string> matchedFiles = new List<string>();
            List<string> matchedDirs = new List<string>();
            ResolveActionTargets(act, matchedFiles, matchedDirs, res);

            if (cmd == "delete")
            {
                foreach (string f in matchedFiles)
                {
                    if (!PathSanitizer.IsSafeToDelete(f)) continue;
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if (fi.Exists) res.AddFile(f, fi.Length);
                    }
                    catch { res.SkippedOrLockedCount++; }
                }

                if (string.Equals(act.FileTypeFilter, "d", StringComparison.OrdinalIgnoreCase) && matchedFiles.Count == 0)
                {
                    foreach (string d in matchedDirs)
                    {
                        if (PathSanitizer.IsSafeToDelete(d) && Directory.Exists(d))
                        {
                            res.AddFile(d, 0);
                        }
                    }
                }
            }
            else if (cmd == "sqlite.vacuum")
            {
                foreach (string f in matchedFiles)
                {
                    if (NativeCleanerHelper.IsValidSqliteFile(f))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            res.AddFile(f + " [SQLite Vacuum]", Math.Max(4096L, fi.Length / 15L));
                        }
                        catch { res.SkippedOrLockedCount++; }
                    }
                }
            }
            else if (cmd.StartsWith("chrome.") || cmd.StartsWith("mozilla.") || cmd == "cookie")
            {
                foreach (string f in matchedFiles)
                {
                    if (File.Exists(f))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            long est = cmd == "cookie" ? fi.Length : Math.Max(4096L, fi.Length / 4L);
                            res.AddFile(f, est);
                        }
                        catch { res.SkippedOrLockedCount++; }
                    }
                }
            }
            else if (cmd == "json")
            {
                foreach (string f in matchedFiles)
                {
                    if (File.Exists(f) && JsonContainsAddressKey(f, act.JsonAddress))
                    {
                        res.AddFile(f + " [JSON: " + act.JsonAddress + "]", 512L);
                    }
                }
            }
            else if (cmd == "ini")
            {
                foreach (string f in matchedFiles)
                {
                    if (File.Exists(f) && IniContainsTarget(f, act.IniSection, act.IniParameter))
                    {
                        res.AddFile(f + " [INI: " + act.IniSection + "]", 256L);
                    }
                }
            }
            else if (cmd == "office_registrymodifications")
            {
                foreach (string f in matchedFiles)
                {
                    if (File.Exists(f) && OfficeXcuContainsHistory(f))
                    {
                        res.AddFile(f + " [History]", 1024L);
                    }
                }
            }
        }

        private void ExecuteActionClean(CleanerMlAction act, CleanerContext context, CleanResult res)
        {
            string cmd = (act.Command ?? "delete").Trim().ToLowerInvariant();
            string search = (act.Search ?? "").Trim().ToLowerInvariant();

            if (cmd == "winreg")
            {
                CleanWinReg(act.RawPath, act.RegistryValueName, res);
                return;
            }

            if (cmd == "win.shell.change.notify")
            {
                NativeCleanerHelper.NotifyShellChanged();
                return;
            }

            if (cmd == "process.terminate" || cmd == "process")
            {
                // Avoid forcefully killing explorer.exe to prevent desktop shell disruption; notify shell instead
                NativeCleanerHelper.NotifyShellChanged();
                return;
            }

            if (search == "deep")
            {
                ScanResult sr = new ScanResult();
                string root = context != null ? context.DeepScanRootPath : null;
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    int maxDepth = context != null ? context.DeepScanMaxDepth : 5;
                    ScanDeepRecursive(root, 0, maxDepth, act, sr);
                    foreach (string f in sr.FoundItems)
                    {
                        DeleteFileSafe(f, res);
                    }
                }
                return;
            }

            List<string> matchedFiles = new List<string>();
            List<string> matchedDirs = new List<string>();
            ScanResult dummyScan = new ScanResult();
            ResolveActionTargets(act, matchedFiles, matchedDirs, dummyScan);

            if (cmd == "delete")
            {
                foreach (string f in matchedFiles)
                {
                    DeleteFileSafe(f, res);
                }

                // Remove subdirectories bottom-up when walk.all, walk.top, or glob directory deletion is specified
                if (search == "walk.all" || search == "walk.top" || search == "glob" ||
                    string.Equals(act.FileTypeFilter, "d", StringComparison.OrdinalIgnoreCase))
                {
                    matchedDirs.Sort((a, b) => b.Length.CompareTo(a.Length));
                    foreach (string d in matchedDirs)
                    {
                        TryRemoveEmptyDirectory(d);
                    }
                }
            }
            else if (cmd == "sqlite.vacuum")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.VacuumSqliteDatabase(f, out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "cookie")
            {
                foreach (string f in matchedFiles)
                {
                    if (!File.Exists(f)) continue;
                    long saved;
                    if (NativeCleanerHelper.IsValidSqliteFile(f))
                    {
                        if (NativeCleanerHelper.ExecuteSqliteStatements(f, "DELETE FROM cookies; DELETE FROM moz_cookies; VACUUM;", out saved))
                        {
                            res.FreedBytes += saved;
                            res.DeletedCount++;
                        }
                        else if (!NativeCleanerHelper.IsFileLocked(f))
                        {
                            DeleteFileSafe(f, res);
                        }
                        else
                        {
                            res.ErrorOrLockedCount++;
                        }
                    }
                    else
                    {
                        DeleteFileSafe(f, res);
                    }
                }
            }
            else if (cmd == "chrome.autofill")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f,
                        "DELETE FROM autofill; DELETE FROM autofill_dates; DELETE FROM autofill_profiles; DELETE FROM autofill_profile_names; DELETE FROM autofill_profile_emails; DELETE FROM autofill_profile_phones; DELETE FROM local_addresses; DELETE FROM local_addresses_type_tokens; VACUUM;",
                        out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "chrome.history")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f,
                        "DELETE FROM urls; DELETE FROM visits; DELETE FROM visit_source; DELETE FROM downloads; DELETE FROM downloads_url_chains; DELETE FROM downloads_slices; DELETE FROM segments; DELETE FROM segment_usage; DELETE FROM keyword_search_terms; DELETE FROM content_annotations; DELETE FROM context_annotations; VACUUM;",
                        out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "chrome.favicons")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f,
                        "DELETE FROM favicons; DELETE FROM favicon_bitmaps; DELETE FROM icon_mapping; VACUUM;",
                        out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "chrome.databases_db")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f, "DELETE FROM Databases; VACUUM;", out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "chrome.keywords")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f, "DELETE FROM keywords WHERE prepopulate_id = 0; VACUUM;", out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "mozilla.url.history")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f,
                        "DELETE FROM moz_historyvisits; DELETE FROM moz_inputhistory; DELETE FROM moz_hosts; DELETE FROM moz_places WHERE id NOT IN (SELECT fk FROM moz_bookmarks WHERE fk IS NOT NULL); DELETE FROM moz_origins WHERE id NOT IN (SELECT origin_id FROM moz_places); VACUUM;",
                        out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "mozilla.favicons")
            {
                foreach (string f in matchedFiles)
                {
                    long saved;
                    if (NativeCleanerHelper.ExecuteSqliteStatements(f,
                        "DELETE FROM moz_icons_to_pages; DELETE FROM moz_pages_w_icons; DELETE FROM moz_icons; VACUUM;",
                        out saved))
                    {
                        res.FreedBytes += saved;
                        res.DeletedCount++;
                    }
                    else
                    {
                        res.ErrorOrLockedCount++;
                    }
                }
            }
            else if (cmd == "json")
            {
                foreach (string f in matchedFiles)
                {
                    if (CleanJsonAddressKey(f, act.JsonAddress, ref res))
                    {
                        res.DeletedCount++;
                    }
                }
            }
            else if (cmd == "ini")
            {
                foreach (string f in matchedFiles)
                {
                    if (CleanIniTarget(f, act.IniSection, act.IniParameter, ref res))
                    {
                        res.DeletedCount++;
                    }
                }
            }
            else if (cmd == "office_registrymodifications")
            {
                foreach (string f in matchedFiles)
                {
                    if (CleanOfficeXcuHistory(f, ref res))
                    {
                        res.DeletedCount++;
                    }
                }
            }
        }

        private static void ResolveActionTargets(CleanerMlAction act, List<string> matchedFiles, List<string> matchedDirs, ScanResult res)
        {
            string search = (act.Search ?? "file").Trim().ToLowerInvariant();

            foreach (string expanded in act.ExpandedPaths)
            {
                if (string.IsNullOrWhiteSpace(expanded)) continue;

                // Resolve any wildcards in intermediate or leaf segments
                List<string> resolvedTargets = CleanerMlEngine.ResolvePathWithWildcards(expanded);

                foreach (string target in resolvedTargets)
                {
                    if (!PathSanitizer.IsSafeToDelete(target)) continue;

                    if (search == "file")
                    {
                        if (File.Exists(target) && act.MatchesRegexFilters(target, false))
                        {
                            matchedFiles.Add(target);
                        }
                        else if (Directory.Exists(target) && string.Equals(act.FileTypeFilter, "d", StringComparison.OrdinalIgnoreCase))
                        {
                            if (act.MatchesRegexFilters(target, true)) matchedDirs.Add(target);
                        }
                    }
                    else if (search == "glob")
                    {
                        if (File.Exists(target))
                        {
                            if (act.MatchesRegexFilters(target, false)) matchedFiles.Add(target);
                        }
                        else if (Directory.Exists(target))
                        {
                            if (act.MatchesRegexFilters(target, true))
                            {
                                matchedDirs.Add(target);
                                if (!string.Equals(act.FileTypeFilter, "d", StringComparison.OrdinalIgnoreCase))
                                {
                                    WalkDirectory(target, act, true, matchedFiles, matchedDirs, res);
                                }
                            }
                        }
                    }
                    else if (search == "walk.files")
                    {
                        if (Directory.Exists(target))
                        {
                            WalkDirectory(target, act, false, matchedFiles, matchedDirs, res);
                        }
                    }
                    else if (search == "walk.all")
                    {
                        if (Directory.Exists(target))
                        {
                            WalkDirectory(target, act, true, matchedFiles, matchedDirs, res);
                        }
                    }
                    else if (search == "walk.top")
                    {
                        if (Directory.Exists(target))
                        {
                            WalkDirectory(target, act, true, matchedFiles, matchedDirs, res);
                            if (act.MatchesRegexFilters(target, true))
                            {
                                matchedDirs.Add(target);
                            }
                        }
                    }
                }
            }
        }

        private static void WalkDirectory(string dirPath, CleanerMlAction act, bool collectSubDirs, List<string> matchedFiles, List<string> matchedDirs, ScanResult res)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;
            if (!PathSanitizer.IsSafeToDelete(dirPath)) return;

            try
            {
                string[] files = Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    if (!PathSanitizer.IsSafeToDelete(f)) continue;
                    if (act.MatchesRegexFilters(f, false))
                    {
                        matchedFiles.Add(f);
                    }
                }

                string[] subDirs = Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly);
                foreach (string sub in subDirs)
                {
                    try
                    {
                        if (!PathSanitizer.IsSafeToDelete(sub)) continue;
                        FileAttributes attr = File.GetAttributes(sub);
                        if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                            continue;

                        WalkDirectory(sub, act, collectSubDirs, matchedFiles, matchedDirs, res);
                        if (collectSubDirs && act.MatchesRegexFilters(sub, true))
                        {
                            matchedDirs.Add(sub);
                        }
                    }
                    catch
                    {
                        res.SkippedOrLockedCount++;
                    }
                }
            }
            catch
            {
                res.SkippedOrLockedCount++;
            }
        }

        private static void ScanDeepRecursive(string currentDir, int depth, int maxDepth, CleanerMlAction act, ScanResult res)
        {
            if (depth > maxDepth) return;
            if (PathSanitizer.IsExcludedDeepScanDirectory(currentDir)) return;

            try
            {
                string[] files = Directory.GetFiles(currentDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    if (act.MatchesRegexFilters(f, false) && PathSanitizer.IsSafeToDelete(f))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            res.AddFile(f, fi.Length);
                        }
                        catch { res.SkippedOrLockedCount++; }
                    }
                }

                string[] subDirs = Directory.GetDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string sub in subDirs)
                {
                    if (!PathSanitizer.IsExcludedDeepScanDirectory(sub))
                    {
                        ScanDeepRecursive(sub, depth + 1, maxDepth, act, res);
                    }
                }
            }
            catch
            {
                res.SkippedOrLockedCount++;
            }
        }

        #region REGISTRY, JSON, INI & OFFICE XCU HELPERS

        private static RegistryKey OpenHive(string fullRegPath, out string subKeyPath)
        {
            subKeyPath = "";
            if (string.IsNullOrWhiteSpace(fullRegPath)) return null;
            string p = fullRegPath.Trim().Replace('/', '\\');
            int slash = p.IndexOf('\\');
            if (slash <= 0 || slash >= p.Length - 1) return null;

            string hiveName = p.Substring(0, slash).ToUpperInvariant();
            subKeyPath = p.Substring(slash + 1);

            switch (hiveName)
            {
                case "HKCU":
                case "HKEY_CURRENT_USER":
                    return Registry.CurrentUser;
                case "HKLM":
                case "HKEY_LOCAL_MACHINE":
                    return Registry.LocalMachine;
                case "HKCR":
                case "HKEY_CLASSES_ROOT":
                    return Registry.ClassesRoot;
                case "HKU":
                case "HKEY_USERS":
                    return Registry.Users;
                default:
                    return null;
            }
        }

        private static void ScanWinReg(string regPath, string valueName, ScanResult res)
        {
            string subKey;
            RegistryKey hive = OpenHive(regPath, out subKey);
            if (hive == null || string.IsNullOrEmpty(subKey)) return;

            try
            {
                using (RegistryKey k = hive.OpenSubKey(subKey, false))
                {
                    if (k == null) return;
                    if (!string.IsNullOrEmpty(valueName))
                    {
                        object val = k.GetValue(valueName, null);
                        if (val != null)
                        {
                            res.AddFile("[Registry] " + regPath + " -> " + valueName, 256L);
                        }
                    }
                    else
                    {
                        int count = k.ValueCount + k.SubKeyCount;
                        if (count > 0)
                        {
                            res.AddFile("[Registry] " + regPath, Math.Max(256L, count * 256L));
                        }
                    }
                }
            }
            catch { res.SkippedOrLockedCount++; }
        }

        private static void CleanWinReg(string regPath, string valueName, CleanResult res)
        {
            string subKey;
            RegistryKey hive = OpenHive(regPath, out subKey);
            if (hive == null || string.IsNullOrEmpty(subKey)) return;

            try
            {
                if (!string.IsNullOrEmpty(valueName))
                {
                    using (RegistryKey k = hive.OpenSubKey(subKey, true))
                    {
                        if (k != null && k.GetValue(valueName, null) != null)
                        {
                            k.DeleteValue(valueName, false);
                            res.DeletedCount++;
                            res.FreedBytes += 256L;
                        }
                    }
                }
                else
                {
                    using (RegistryKey check = hive.OpenSubKey(subKey, false))
                    {
                        if (check == null) return;
                    }
                    hive.DeleteSubKeyTree(subKey, false);
                    res.DeletedCount++;
                    res.FreedBytes += 512L;
                }
            }
            catch
            {
                res.ErrorOrLockedCount++;
            }
        }

        private static bool JsonContainsAddressKey(string filePath, string address)
        {
            if (string.IsNullOrWhiteSpace(address) || !File.Exists(filePath)) return false;
            try
            {
                string[] parts = address.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return false;
                string leafKey = parts[parts.Length - 1];
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                return content.IndexOf("\"" + leafKey + "\"", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CleanJsonAddressKey(string filePath, string address, ref CleanResult res)
        {
            if (string.IsNullOrWhiteSpace(address) || !File.Exists(filePath) || NativeCleanerHelper.IsFileLocked(filePath))
                return false;

            try
            {
                string[] parts = address.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return false;
                string leafKey = parts[parts.Length - 1];
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                if (content.IndexOf("\"" + leafKey + "\"", StringComparison.Ordinal) < 0) return false;

                // Safe regex removal of simple JSON property or object/array value for leafKey when unambiguous
                string pattern = "\"" + Regex.Escape(leafKey) + "\"\\s*:\\s*(\\[[^\\[\\]]*\\]|\\{[^{}]*\\}|\"(?:\\\\.|[^\"\\\\])*\"|true|false|null|-?\\d+(?:\\.\\d+)?)\\s*,?";
                string updated = Regex.Replace(content, pattern, "");
                updated = Regex.Replace(updated, ",(\\s*[\\}\\]])", "$1");

                if (!string.Equals(content, updated, StringComparison.Ordinal))
                {
                    long diff = Math.Max(128L, Encoding.UTF8.GetByteCount(content) - Encoding.UTF8.GetByteCount(updated));
                    File.WriteAllText(filePath, updated, new UTF8Encoding(false));
                    res.FreedBytes += diff;
                    return true;
                }
            }
            catch
            {
                res.ErrorOrLockedCount++;
            }
            return false;
        }

        private static bool IniContainsTarget(string filePath, string section, string parameter)
        {
            if (string.IsNullOrWhiteSpace(section) || !File.Exists(filePath)) return false;
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                bool inSection = false;
                string secHeader = "[" + section.Trim() + "]";
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inSection = string.Equals(line, secHeader, StringComparison.OrdinalIgnoreCase);
                        if (inSection && string.IsNullOrEmpty(parameter)) return true;
                        continue;
                    }
                    if (inSection && !string.IsNullOrEmpty(parameter))
                    {
                        if (line.StartsWith(parameter + "=", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith(parameter + " =", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool CleanIniTarget(string filePath, string section, string parameter, ref CleanResult res)
        {
            if (string.IsNullOrWhiteSpace(section) || !File.Exists(filePath) || NativeCleanerHelper.IsFileLocked(filePath))
                return false;

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                List<string> kept = new List<string>();
                bool inSection = false;
                bool modified = false;
                string secHeader = "[" + section.Trim() + "]";

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inSection = string.Equals(line, secHeader, StringComparison.OrdinalIgnoreCase);
                        if (inSection && string.IsNullOrEmpty(parameter))
                        {
                            modified = true;
                            continue;
                        }
                        kept.Add(rawLine);
                        continue;
                    }

                    if (inSection)
                    {
                        if (string.IsNullOrEmpty(parameter))
                        {
                            modified = true;
                            continue;
                        }
                        if (line.StartsWith(parameter + "=", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith(parameter + " =", StringComparison.OrdinalIgnoreCase))
                        {
                            modified = true;
                            continue;
                        }
                    }
                    kept.Add(rawLine);
                }

                if (modified)
                {
                    File.WriteAllLines(filePath, kept.ToArray(), new UTF8Encoding(false));
                    res.FreedBytes += 256L;
                    return true;
                }
            }
            catch
            {
                res.ErrorOrLockedCount++;
            }
            return false;
        }

        private static bool OfficeXcuContainsHistory(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                return content.IndexOf("org.openoffice.Office.Histories", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       content.IndexOf("org.openoffice.Office.Common/History", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool CleanOfficeXcuHistory(string filePath, ref CleanResult res)
        {
            if (!File.Exists(filePath) || NativeCleanerHelper.IsFileLocked(filePath)) return false;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XmlNode root = doc.DocumentElement;
                if (root == null) return false;

                List<XmlNode> toRemove = new List<XmlNode>();
                foreach (XmlNode child in root.ChildNodes)
                {
                    if (child.Attributes != null)
                    {
                        foreach (XmlAttribute attr in child.Attributes)
                        {
                            if (attr.Value != null &&
                                (attr.Value.IndexOf("org.openoffice.Office.Histories", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 attr.Value.IndexOf("org.openoffice.Office.Common/History", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                toRemove.Add(child);
                                break;
                            }
                        }
                    }
                }

                if (toRemove.Count > 0)
                {
                    foreach (XmlNode n in toRemove) root.RemoveChild(n);
                    doc.Save(filePath);
                    res.FreedBytes += toRemove.Count * 256L;
                    return true;
                }
            }
            catch
            {
                res.ErrorOrLockedCount++;
            }
            return false;
        }

        #endregion

        #endregion
    }

    /// <summary>
    /// Parses BleachBit CleanerML XML files from Config\Addons\SystemCleaner\cleaners\*.xml,
    /// filtering strictly for Windows-compatible cleaners, options, variables, and actions.
    /// </summary>
    public static class CleanerMlEngine
    {
        private static readonly HashSet<string> WindowsCoreCleanerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "windows_explorer",
            "windows_defender",
            "microsoft_edge",
            "internet_explorer",
            "windows_media_player",
            "paint",
            "wordpad",
            "deepscan"
        };

        private static readonly HashSet<string> SafeRecommendedOptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cache",
            "temp",
            "tmp",
            "logs",
            "log",
            "debug_logs",
            "crash_reports",
            "thumbnails",
            "mru",
            "recent_documents",
            "run",
            "search_history",
            "history"
        };

        private static readonly HashSet<string> SensitiveOptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "passwords",
            "cookies",
            "session",
            "sessionjson",
            "site_data",
            "sync",
            "quarantine",
            "backup"
        };

        public static string GetCleanersDirectory()
        {
            try
            {
                string dllDir = Path.GetDirectoryName(typeof(SystemCleanerAddon).Assembly.Location);
                if (!string.IsNullOrEmpty(dllDir))
                {
                    string candidate = Path.Combine(dllDir, "cleaners");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { }

            string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons", "SystemCleaner", "cleaners");
            return fallback;
        }

        /// <summary>
        /// Checks whether an XML `os` attribute is compatible with Windows.
        /// Explicitly rejects linux, unix, macos, bsd, posix, or !windows.
        /// </summary>
        public static bool IsWindowsCompatibleOs(string osAttr)
        {
            if (string.IsNullOrWhiteSpace(osAttr)) return true;

            string[] tokens = osAttr.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasWindows = false;
            foreach (string raw in tokens)
            {
                string t = raw.Trim().ToLowerInvariant();
                if (t == "!windows" || t == "!win32") return false;
                if (t == "windows" || t == "win32") hasWindows = true;
            }
            return hasWindows;
        }

        /// <summary>
        /// Rejects Linux/Unix/macOS paths and commands even when the `os` attribute was omitted in XML.
        /// </summary>
        public static bool IsNonWindowsPathOrCommand(string command, string rawPath)
        {
            if (!string.IsNullOrEmpty(command))
            {
                string c = command.Trim().ToLowerInvariant();
                if (c.StartsWith("apt.") ||
                    c.StartsWith("dnf.") ||
                    c.StartsWith("yum.") ||
                    c.StartsWith("pacman.") ||
                    c.StartsWith("snap.") ||
                    c.StartsWith("flatpak.") ||
                    c.StartsWith("journald.") ||
                    c.StartsWith("macos.") ||
                    c.StartsWith("x11."))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(rawPath))
            {
                string p = rawPath.Trim();
                if (p.StartsWith("/") ||
                    p.StartsWith("~") ||
                    p.StartsWith("$HOME", StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith("$LOGNAME", StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith("$XDG_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Expands Windows environment variables and BleachBit-specific Windows placeholders.
        /// </summary>
        public static string ExpandWindowsPathVariables(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string s = input.Trim();

            // Expand single-dollar Windows variables used in some CleanerML XMLs ($APPDATA, $LOCALAPPDATA, $PROGRAMDATA, $USERPROFILE)
            s = Regex.Replace(s, @"(?<!\$)\$APPDATA(?!\$)", "%AppData%", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(?<!\$)\$LOCALAPPDATA(?!\$)", "%LocalAppData%", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(?<!\$)\$PROGRAMDATA(?!\$)", "%CommonAppData%", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(?<!\$)\$USERPROFILE(?!\$)", "%UserProfile%", RegexOptions.IgnoreCase);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData) || localAppData.IndexOf(@"\Packages\sandbox.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                localAppData = Path.Combine(userProfile, @"AppData\Local");
            }
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string localAppDataLow = Path.Combine(userProfile, @"AppData\LocalLow");
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string winSys = Environment.SystemDirectory;
            string tempDir = Path.GetTempPath().TrimEnd('\\', '/');

            s = ReplaceInsensitive(s, "%CommonAppData%", commonAppData);
            s = ReplaceInsensitive(s, "%ProgramData%", commonAppData);
            s = ReplaceInsensitive(s, "%AppData%", appData);
            s = ReplaceInsensitive(s, "%LocalAppData%", localAppData);
            s = ReplaceInsensitive(s, "%LocalAppDataLow%", localAppDataLow);
            s = ReplaceInsensitive(s, "%UserProfile%", userProfile);
            s = ReplaceInsensitive(s, "%Documents%", documents);
            s = ReplaceInsensitive(s, "%Music%", music);
            s = ReplaceInsensitive(s, "%Pictures%", pictures);
            s = ReplaceInsensitive(s, "%Videos%", videos);
            s = ReplaceInsensitive(s, "%WinDir%", winDir);
            s = ReplaceInsensitive(s, "%WindowsSystem%", winSys);
            s = ReplaceInsensitive(s, "%Temp%", tempDir);
            s = ReplaceInsensitive(s, "%Tmp%", tempDir);

            s = Environment.ExpandEnvironmentVariables(s);
            s = s.Replace('/', '\\');
            while (s.Contains("\\\\") && !s.StartsWith("\\\\"))
            {
                s = s.Replace("\\\\", "\\");
            }
            return s;
        }

        private static string ReplaceInsensitive(string input, string search, string replacement)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search)) return input;
            return Regex.Replace(input, Regex.Escape(search), (replacement ?? "").Replace("$", "$$"), RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Resolves a Windows path that may contain wildcard segments (* or ?) in intermediate directories or leaf file/folder names.
        /// </summary>
        public static List<string> ResolvePathWithWildcards(string rawExpandedPath)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(rawExpandedPath)) return results;

            string normalized = rawExpandedPath.Trim().Replace('/', '\\').TrimEnd('\\');
            if (normalized.Length <= 3) return results;

            // Fast path when no wildcard characters are present
            if (normalized.IndexOf('*') < 0 && normalized.IndexOf('?') < 0)
            {
                if (File.Exists(normalized) || Directory.Exists(normalized))
                {
                    results.Add(normalized);
                }
                return results;
            }

            string root = Path.GetPathRoot(normalized);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return results;

            string remainder = normalized.Substring(root.Length).TrimStart('\\');
            string[] segments = remainder.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return results;

            List<string> currentDirs = new List<string>() { root.TrimEnd('\\') + "\\" };

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                bool isLast = (i == segments.Length - 1);
                bool hasWildcard = (seg.IndexOf('*') >= 0 || seg.IndexOf('?') >= 0);
                List<string> nextMatches = new List<string>();

                foreach (string parent in currentDirs)
                {
                    if (!Directory.Exists(parent)) continue;

                    if (!hasWildcard)
                    {
                        string combined = Path.Combine(parent, seg);
                        if (isLast)
                        {
                            if (File.Exists(combined) || Directory.Exists(combined))
                            {
                                nextMatches.Add(combined);
                            }
                        }
                        else if (Directory.Exists(combined))
                        {
                            nextMatches.Add(combined);
                        }
                    }
                    else
                    {
                        // Wildcard segment
                        try
                        {
                            string[] matchedDirs = Directory.GetDirectories(parent, seg, SearchOption.TopDirectoryOnly);
                            foreach (string md in matchedDirs)
                            {
                                nextMatches.Add(md);
                            }
                        }
                        catch { }

                        if (isLast)
                        {
                            try
                            {
                                string[] matchedFiles = Directory.GetFiles(parent, seg, SearchOption.TopDirectoryOnly);
                                foreach (string mf in matchedFiles)
                                {
                                    nextMatches.Add(mf);
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (nextMatches.Count == 0)
                {
                    return results;
                }

                if (isLast)
                {
                    results.AddRange(nextMatches);
                }
                else
                {
                    currentDirs = nextMatches;
                }
            }

            return results;
        }

        private static List<string> GetBuiltInProgramFilesList()
        {
            List<string> pf = new List<string>();
            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pfw6432 = Environment.GetEnvironmentVariable("ProgramW6432");

            if (!string.IsNullOrEmpty(pf64) && !pf.Contains(pf64)) pf.Add(pf64.TrimEnd('\\'));
            if (!string.IsNullOrEmpty(pf86) && !pf.Contains(pf86)) pf.Add(pf86.TrimEnd('\\'));
            if (!string.IsNullOrEmpty(pfw6432) && !pf.Contains(pfw6432.TrimEnd('\\'))) pf.Add(pfw6432.TrimEnd('\\'));
            return pf;
        }

        /// <summary>
        /// Expands all $$varname$$ placeholders in a raw path using the cleaner's <var> dictionary.
        /// </summary>
        private static List<string> ExpandPathWithVariables(string rawPath, Dictionary<string, List<string>> varTable)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(rawPath)) return results;

            List<string> working = new List<string>() { rawPath.Trim() };

            // Handle %ProgramFiles% across both 64-bit and 32-bit Program Files directories
            if (rawPath.IndexOf("%ProgramFiles%", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                List<string> pfExpanded = new List<string>();
                foreach (string w in working)
                {
                    foreach (string pf in GetBuiltInProgramFilesList())
                    {
                        pfExpanded.Add(ReplaceInsensitive(w, "%ProgramFiles%", pf));
                    }
                }
                working = pfExpanded;
            }

            int safetyIterations = 0;
            while (safetyIterations++ < 5)
            {
                bool replacedAny = false;
                List<string> nextPass = new List<string>();

                foreach (string item in working)
                {
                    Match m = Regex.Match(item, @"\$\$([A-Za-z0-9_-]+)\$\$");
                    if (!m.Success)
                    {
                        nextPass.Add(item);
                        continue;
                    }

                    replacedAny = true;
                    string varName = m.Groups[1].Value;
                    List<string> vals;
                    if (varTable.TryGetValue(varName, out vals) && vals != null && vals.Count > 0)
                    {
                        foreach (string v in vals)
                        {
                            string substituted = item.Substring(0, m.Index) + v + item.Substring(m.Index + m.Length);
                            nextPass.Add(substituted);
                        }
                    }
                }

                working = nextPass;
                if (!replacedAny || working.Count == 0) break;
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string w in working)
            {
                if (w.IndexOf("$$", StringComparison.Ordinal) >= 0) continue; // Unresolved non-Windows variable
                if (IsNonWindowsPathOrCommand(null, w)) continue;

                string expanded = ExpandWindowsPathVariables(w);
                if (!string.IsNullOrWhiteSpace(expanded) && !IsNonWindowsPathOrCommand(null, expanded))
                {
                    if (unique.Add(expanded))
                    {
                        results.Add(expanded);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Loads all Windows-compatible CleanerML XML files and creates their option cleaners.
        /// </summary>
        public static List<BaseCleaner> LoadAllCleanersFromXml(Func<string, string, string> localizer, string currentLangCode)
        {
            List<BaseCleaner> allCleaners = new List<BaseCleaner>();
            string cleanersDir = GetCleanersDirectory();
            if (string.IsNullOrEmpty(cleanersDir) || !Directory.Exists(cleanersDir))
            {
                return allCleaners;
            }

            string[] xmlFiles = Directory.GetFiles(cleanersDir, "*.xml", SearchOption.TopDirectoryOnly);
            Array.Sort(xmlFiles, StringComparer.OrdinalIgnoreCase);

            List<BaseCleaner> windowsTabList = new List<BaseCleaner>();
            List<BaseCleaner> appsTabList = new List<BaseCleaner>();

            foreach (string xmlFile in xmlFiles)
            {
                try
                {
                    List<BaseCleaner> parsedOptions = ParseSingleXmlFile(xmlFile, localizer, currentLangCode);
                    foreach (BaseCleaner opt in parsedOptions)
                    {
                        if (string.Equals(opt.TabId, "windows", StringComparison.OrdinalIgnoreCase))
                        {
                            windowsTabList.Add(opt);
                        }
                        else
                        {
                            appsTabList.Add(opt);
                        }
                    }
                }
                catch { }
            }

            // Order Windows tab sections logically: Microsoft Edge, Windows Explorer, Windows Defender, Internet Explorer, Media Player, Paint, WordPad, Deep Scan
            string[] preferredWinOrder = new string[]
            {
                "microsoft_edge",
                "windows_explorer",
                "windows_defender",
                "internet_explorer",
                "windows_media_player",
                "paint",
                "wordpad",
                "deepscan"
            };

            foreach (string prefId in preferredWinOrder)
            {
                foreach (BaseCleaner c in windowsTabList)
                {
                    if (string.Equals(c.SectionId, prefId, StringComparison.OrdinalIgnoreCase))
                    {
                        allCleaners.Add(c);
                    }
                }
            }
            foreach (BaseCleaner c in windowsTabList)
            {
                if (!allCleaners.Contains(c))
                {
                    allCleaners.Add(c);
                }
            }

            // Sort Applications tab alphabetically by SectionTitle
            appsTabList.Sort((a, b) =>
            {
                int cmp = string.Compare(a.SectionTitle, b.SectionTitle, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            allCleaners.AddRange(appsTabList);

            return allCleaners;
        }

        private static List<BaseCleaner> ParseSingleXmlFile(string xmlFilePath, Func<string, string, string> localizer, string currentLangCode)
        {
            List<BaseCleaner> optionsList = new List<BaseCleaner>();

            XmlDocument doc = new XmlDocument();
            doc.Load(xmlFilePath);

            XmlElement cleanerEl = doc.DocumentElement;
            if (cleanerEl == null || !string.Equals(cleanerEl.Name, "cleaner", StringComparison.OrdinalIgnoreCase))
            {
                return optionsList;
            }

            string cleanerOs = cleanerEl.GetAttribute("os");
            if (!IsWindowsCompatibleOs(cleanerOs))
            {
                return optionsList;
            }

            string cleanerId = cleanerEl.GetAttribute("id");
            if (string.IsNullOrWhiteSpace(cleanerId))
            {
                cleanerId = Path.GetFileNameWithoutExtension(xmlFilePath);
            }

            string rawCleanerLabel = GetChildElementText(cleanerEl, "label", cleanerId);

            // Parse <running type="exe" os="windows">
            List<string> runningExes = new List<string>();
            foreach (XmlNode node in cleanerEl.ChildNodes)
            {
                XmlElement el = node as XmlElement;
                if (el == null || !string.Equals(el.Name, "running", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsWindowsCompatibleOs(el.GetAttribute("os"))) continue;

                string runType = el.GetAttribute("type");
                string exeName = (el.InnerText ?? "").Trim();
                if (string.Equals(runType, "exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(exeName))
                {
                    if (!IsNonWindowsPathOrCommand(null, exeName) && !runningExes.Contains(exeName))
                    {
                        runningExes.Add(exeName);
                    }
                }
            }

            // Parse <var name="...">
            Dictionary<string, List<string>> varTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            varTable["ProgramFiles"] = GetBuiltInProgramFilesList();

            foreach (XmlNode node in cleanerEl.ChildNodes)
            {
                XmlElement varEl = node as XmlElement;
                if (varEl == null || !string.Equals(varEl.Name, "var", StringComparison.OrdinalIgnoreCase)) continue;

                string varName = varEl.GetAttribute("name");
                if (string.IsNullOrWhiteSpace(varName)) continue;

                List<string> values = new List<string>();
                foreach (XmlNode valNode in varEl.ChildNodes)
                {
                    XmlElement valEl = valNode as XmlElement;
                    if (valEl == null || !string.Equals(valEl.Name, "value", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsWindowsCompatibleOs(valEl.GetAttribute("os"))) continue;

                    string rawVal = (valEl.InnerText ?? "").Trim();
                    if (string.IsNullOrEmpty(rawVal) || IsNonWindowsPathOrCommand(null, rawVal)) continue;

                    string expandedVal = ExpandWindowsPathVariables(rawVal);
                    if (!string.IsNullOrEmpty(expandedVal) && !IsNonWindowsPathOrCommand(null, expandedVal))
                    {
                        values.Add(expandedVal);
                    }
                }

                if (values.Count > 0)
                {
                    varTable[varName] = values;
                }
            }

            // Parse <option id="...">
            List<CleanerMlAction> allCleanerActions = new List<CleanerMlAction>();
            bool isWinCore = WindowsCoreCleanerIds.Contains(cleanerId);
            string tabId = isWinCore ? "windows" : "apps";
            string sectionId = string.Equals(cleanerId, "deepscan", StringComparison.OrdinalIgnoreCase) ? "deep_scan" : cleanerId;
            string sectionIcon = GetCleanerIcon(cleanerId);

            foreach (XmlNode node in cleanerEl.ChildNodes)
            {
                XmlElement optEl = node as XmlElement;
                if (optEl == null || !string.Equals(optEl.Name, "option", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsWindowsCompatibleOs(optEl.GetAttribute("os"))) continue;

                string optId = optEl.GetAttribute("id");
                if (string.IsNullOrWhiteSpace(optId)) continue;

                string rawOptLabel = GetChildElementText(optEl, "label", optId);
                string rawOptDesc = GetChildElementText(optEl, "description", "");
                string rawWarning = GetChildElementText(optEl, "warning", "");

                List<CleanerMlAction> validActions = new List<CleanerMlAction>();
                bool requiresAdmin = string.Equals(cleanerId, "windows_defender", StringComparison.OrdinalIgnoreCase);

                foreach (XmlNode actNode in optEl.ChildNodes)
                {
                    XmlElement actEl = actNode as XmlElement;
                    if (actEl == null || !string.Equals(actEl.Name, "action", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsWindowsCompatibleOs(actEl.GetAttribute("os"))) continue;

                    string cmd = actEl.GetAttribute("command");
                    string search = actEl.GetAttribute("search");
                    string rawPath = actEl.GetAttribute("path");

                    if (IsNonWindowsPathOrCommand(cmd, rawPath)) continue;

                    CleanerMlAction action = new CleanerMlAction
                    {
                        Command = string.IsNullOrEmpty(cmd) ? "delete" : cmd.Trim(),
                        Search = (search ?? "").Trim(),
                        RawPath = (rawPath ?? "").Trim(),
                        RegistryValueName = actEl.GetAttribute("name"),
                        JsonAddress = actEl.GetAttribute("address"),
                        IniSection = actEl.GetAttribute("section"),
                        IniParameter = actEl.GetAttribute("parameter"),
                        RegexPattern = actEl.GetAttribute("regex"),
                        NRegexPattern = actEl.GetAttribute("nregex"),
                        WholeRegexPattern = actEl.GetAttribute("wholeregex"),
                        NWholeRegexPattern = actEl.GetAttribute("nwholeregex"),
                        FileTypeFilter = actEl.GetAttribute("type"),
                        ProcessCmd = actEl.GetAttribute("cmd"),
                        ProcessWait = !string.Equals(actEl.GetAttribute("wait"), "false", StringComparison.OrdinalIgnoreCase)
                    };

                    string cmdLower = action.Command.ToLowerInvariant();
                    if (cmdLower == "winreg" || cmdLower == "win.shell.change.notify" ||
                        cmdLower == "process" || cmdLower == "process.terminate" ||
                        string.Equals(action.Search, "deep", StringComparison.OrdinalIgnoreCase))
                    {
                        if (cmdLower == "winreg" && action.RawPath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                        {
                            requiresAdmin = true;
                        }
                        validActions.Add(action);
                        allCleanerActions.Add(action);
                    }
                    else if (!string.IsNullOrEmpty(action.RawPath))
                    {
                        List<string> expandedPaths = ExpandPathWithVariables(action.RawPath, varTable);
                        if (expandedPaths.Count > 0)
                        {
                            action.ExpandedPaths = expandedPaths;
                            validActions.Add(action);
                            allCleanerActions.Add(action);
                        }
                    }
                }

                if (validActions.Count == 0) continue;

                bool isDangerous = !string.IsNullOrEmpty(rawWarning) || SensitiveOptionIds.Contains(optId);
                bool isRecommended = !isDangerous &&
                    !string.Equals(cleanerId, "deepscan", StringComparison.OrdinalIgnoreCase) &&
                    SafeRecommendedOptionIds.Contains(optId);

                string locSectionTitle = TranslateCleanerLabel(rawCleanerLabel, currentLangCode);
                string locOptLabel = TranslateOptionLabel(rawOptLabel, currentLangCode);
                string locOptDesc = TranslateOptionDescription(rawOptDesc, currentLangCode);
                string locWarning = TranslateWarningText(rawWarning, optId, currentLangCode);

                CleanerMlOptionCleaner optCleaner = new CleanerMlOptionCleaner(
                    cleanerId,
                    optId,
                    locSectionTitle,
                    locOptLabel,
                    locOptDesc,
                    locWarning,
                    tabId,
                    sectionIcon,
                    requiresAdmin,
                    isDangerous,
                    isRecommended,
                    true,
                    runningExes,
                    validActions)
                {
                    RawCleanerLabel = rawCleanerLabel,
                    RawOptionLabel = rawOptLabel,
                    RawOptionDescription = rawOptDesc,
                    RawWarning = rawWarning,
                    SectionId = sectionId
                };

                optionsList.Add(optCleaner);
            }

            if (optionsList.Count > 0)
            {
                bool isInstalled = isWinCore || DetectCleanerPresenceOnSystem(varTable, allCleanerActions);
                foreach (BaseCleaner opt in optionsList)
                {
                    opt.IsInstalledOnSystem = isInstalled;
                }
            }

            return optionsList;
        }

        /// <summary>
        /// Checks whether an application defined in a CleanerML file is actually installed/present on this Windows PC.
        /// </summary>
        private static bool DetectCleanerPresenceOnSystem(Dictionary<string, List<string>> varTable, List<CleanerMlAction> actions)
        {
            try
            {
                // 1. Check <var> directories/globs (excluding built-in ProgramFiles)
                foreach (KeyValuePair<string, List<string>> kvp in varTable)
                {
                    if (string.Equals(kvp.Key, "ProgramFiles", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string val in kvp.Value)
                    {
                        if (string.IsNullOrWhiteSpace(val)) continue;
                        if (val.IndexOf('*') >= 0 || val.IndexOf('?') >= 0)
                        {
                            if (ResolvePathWithWildcards(val).Count > 0) return true;
                        }
                        else if (Directory.Exists(val) || File.Exists(val))
                        {
                            return true;
                        }
                    }
                }

                // 2. Check action paths or parent application directories & registry keys
                foreach (CleanerMlAction act in actions)
                {
                    if (string.Equals(act.Command, "winreg", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(act.RawPath))
                    {
                        string p = act.RawPath.Replace('/', '\\');
                        int slash = p.IndexOf('\\');
                        if (slash > 0 && slash < p.Length - 1)
                        {
                            string hiveStr = p.Substring(0, slash).ToUpperInvariant();
                            string subKey = p.Substring(slash + 1);
                            RegistryKey hive = hiveStr == "HKCU" ? Registry.CurrentUser : (hiveStr == "HKLM" ? Registry.LocalMachine : null);
                            if (hive != null)
                            {
                                using (RegistryKey k = hive.OpenSubKey(subKey, false))
                                {
                                    if (k != null) return true;
                                }
                                string parentKey = Path.GetDirectoryName(subKey);
                                if (!string.IsNullOrEmpty(parentKey) && parentKey.IndexOf('\\') > 0)
                                {
                                    using (RegistryKey pk = hive.OpenSubKey(parentKey, false))
                                    {
                                        if (pk != null) return true;
                                    }
                                }
                            }
                        }
                    }

                    foreach (string exp in act.ExpandedPaths)
                    {
                        if (string.IsNullOrWhiteSpace(exp)) continue;
                        if (exp.IndexOf('*') >= 0 || exp.IndexOf('?') >= 0)
                        {
                            if (ResolvePathWithWildcards(exp).Count > 0) return true;
                        }
                        else
                        {
                            if (File.Exists(exp) || Directory.Exists(exp)) return true;
                            if (string.Equals(act.Search, "file", StringComparison.OrdinalIgnoreCase))
                            {
                                string parentDir = Path.GetDirectoryName(exp.TrimEnd('\\'));
                                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                                {
                                    if (PathSanitizer.IsSafeToDelete(parentDir) &&
                                        !string.Equals(parentDir.TrimEnd('\\'), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static string GetChildElementText(XmlElement parent, string tagName, string fallback)
        {
            if (parent == null) return fallback;
            foreach (XmlNode n in parent.ChildNodes)
            {
                XmlElement el = n as XmlElement;
                if (el != null && string.Equals(el.Name, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return (el.InnerText ?? "").Trim();
                }
            }
            return fallback;
        }

        public static string GetCleanerIcon(string cleanerId)
        {
            if (string.IsNullOrEmpty(cleanerId)) return "🧹";
            switch (cleanerId.ToLowerInvariant())
            {
                case "windows_explorer": return "📁";
                case "windows_defender": return "🛡️";
                case "microsoft_edge":
                case "google_chrome":
                case "firefox":
                case "brave":
                case "chromium":
                case "opera":
                case "vivaldi":
                case "librewolf":
                case "waterfox":
                case "palemoon":
                case "zen":
                case "helium":
                case "internet_explorer":
                case "safari_windows":
                case "seamonkey":
                    return "🌐";
                case "deepscan": return "🔎";
                case "vscode":
                case "android_studio":
                case "vim":
                case "tortoisesvn":
                case "docker":
                case "java":
                    return "💻";
                case "discord":
                case "slack":
                case "zoom":
                case "skype":
                case "teamviewer":
                case "pidgin":
                case "hexchat":
                case "thunderbird":
                    return "💬";
                case "vlc":
                case "windows_media_player":
                case "winamp":
                case "realplayer":
                case "miro":
                case "gpodder":
                    return "🎬";
                case "microsoft_office":
                case "libreoffice":
                case "openoffice":
                case "adobe_reader":
                case "wordpad":
                    return "📄";
                case "winrar":
                case "winzip":
                    return "📦";
                case "gimp":
                case "paint":
                    return "🎨";
                default:
                    return "🧩";
            }
        }

        #region BILINGUAL TRANSLATION FOR CLEANERML METADATA (EN-US / PT-BR)

        private static bool IsPortuguese(string langCode)
        {
            return !string.IsNullOrEmpty(langCode) && langCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
        }

        public static string TranslateCleanerLabel(string rawLabel, string langCode)
        {
            if (!IsPortuguese(langCode) || string.IsNullOrEmpty(rawLabel)) return rawLabel;

            switch (rawLabel.Trim())
            {
                case "Deep scan": return "Varredura Profunda (Deep Scan)";
                case "Visual Studio Code and forks": return "Visual Studio Code e derivados";
                default: return rawLabel;
            }
        }

        public static string TranslateOptionLabel(string rawLabel, string langCode)
        {
            if (!IsPortuguese(langCode) || string.IsNullOrEmpty(rawLabel)) return rawLabel;

            switch (rawLabel.Trim())
            {
                case "Cache": return "Cache";
                case "Cookies": return "Cookies";
                case "History": return "Histórico";
                case "URL history": return "Histórico de URLs";
                case "Download history": return "Histórico de downloads";
                case "Form history": return "Histórico de formulários";
                case "Search history": return "Histórico de pesquisa";
                case "Search engines": return "Mecanismos de pesquisa";
                case "Session": return "Sessão";
                case "Site data": return "Dados de sites (LocalStorage / IndexedDB)";
                case "Site preferences": return "Preferências de sites";
                case "Passwords": return "Senhas salvas";
                case "Sync": return "Sincronização (Sync)";
                case "Vacuum": return "Compactar bancos SQLite (Vacuum)";
                case "Crash reports": return "Relatórios de falhas (Crash reports)";
                case "Logs": return "Arquivos de log";
                case "Debug logs": return "Logs de depuração";
                case "Temporary files": return "Arquivos temporários";
                case "Backup files": return "Arquivos de backup";
                case "Most recently used": return "Arquivos usados recentemente (MRU)";
                case "Recent documents": return "Documentos recentes";
                case "Recent documents list": return "Lista de documentos recentes";
                case "Run": return "Executar (Win+R)";
                case "Shellbags": return "Shellbags (Visualização de pastas)";
                case "Thumbnails": return "Cache de miniaturas (Thumbnails)";
                case "Quarantine": return "Arquivos em quarentena";
                case "Installers": return "Instaladores em cache";
                case "Statistics": return "Estatísticas";
                case "Memory dump": return "Despejo de memória";
                case "AI": return "Modelos locais de IA";
                case "Gradle cache": return "Cache do Gradle";
                case "IDE cache": return "Cache da IDE";
                default: return rawLabel;
            }
        }

        public static string TranslateOptionDescription(string rawDesc, string langCode)
        {
            if (!IsPortuguese(langCode) || string.IsNullOrEmpty(rawDesc)) return rawDesc;

            switch (rawDesc.Trim())
            {
                case "Delete the cache": return "Excluir os arquivos de cache";
                case "Delete the web cache, which reduces time to display revisited pages": return "Excluir o cache da web, que reduz o tempo de exibição de páginas revisitadas";
                case "Delete cookies, which store login information, website preferences, and tracking identifiers": return "Excluir cookies, que armazenam sessões de login, preferências de sites e identificadores";
                case "Delete the files": return "Excluir os arquivos";
                case "Delete the logs": return "Excluir os registros de log";
                case "Delete the debug logs": return "Excluir os logs de depuração";
                case "Delete the temporary files": return "Excluir os arquivos temporários";
                case "Delete the backup files": return "Excluir os arquivos de backup";
                case "Delete the usage history": return "Excluir o histórico de uso";
                case "Delete the history which includes visited sites, downloads, and thumbnails": return "Excluir o histórico de sites visitados, downloads e miniaturas";
                case "List of visited web pages": return "Lista de páginas da web visitadas";
                case "A history of forms entered in websites": return "Histórico de formulários preenchidos em sites";
                case "Saved form entries and search bar history": return "Entradas de formulários salvas e histórico da barra de pesquisa";
                case "Previously open tabs and windows": return "Guias e janelas abertas anteriormente";
                case "Delete LocalStorage, IndexedDB, and other data stored by websites": return "Excluir LocalStorage, IndexedDB e outros dados armazenados por sites";
                case "Settings for individual sites": return "Configurações específicas de sites individuais";
                case "Delete the sync data (including web apps) and sign out of the browser": return "Excluir dados de sincronização e desconectar a conta do navegador";
                case "A database of usernames and passwords as well as a list of sites that should not store passwords": return "Banco de dados de usuários e senhas salvas no navegador";
                case "Clean database fragmentation to reduce space and improve speed without removing any data": return "Desfragmentar bancos de dados SQLite para reduzir espaço e melhorar a velocidade sem remover dados";
                case "Reset the search engine usage history and delete non-factory search engines, some of which are added automatically": return "Redefinir o histórico de mecanismos de busca e excluir mecanismos adicionados automaticamente";
                case "Delete the most recently used list": return "Excluir a lista de arquivos usados recentemente (MRU)";
                case "Delete the list of recently used documents": return "Excluir a lista de documentos abertos recentemente";
                case "Delete the Start menu's Run history": return "Excluir o histórico do comando Executar (Win+R)";
                case "Delete the search history": return "Excluir o histórico de pesquisas";
                case "Delete the folder view settings, window sizes, and positions": return "Excluir configurações de visualização de pastas, tamanhos e posições de janelas";
                case "Delete the thumbnails of pictures and videos": return "Excluir o cache de miniaturas de imagens e vídeos";
                case "Delete quarantined files": return "Excluir arquivos colocados em quarentena pelo Windows Defender";
                case "Delete the AI models": return "Excluir modelos locais de inteligência artificial";
                default: return rawDesc;
            }
        }

        public static string TranslateWarningText(string rawWarning, string optId, string langCode)
        {
            bool pt = IsPortuguese(langCode);
            if (!string.IsNullOrEmpty(rawWarning))
            {
                if (pt)
                {
                    if (rawWarning.IndexOf("delete your saved passwords", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Esta opção excluirá suas senhas salvas.";
                    if (rawWarning.IndexOf("slow", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Esta operação realiza uma varredura profunda e pode levar alguns minutos.";
                }
                return rawWarning;
            }

            if (SensitiveOptionIds.Contains(optId ?? ""))
            {
                return pt
                    ? "Esta opção pode remover sessões ativas de login, preferências ou dados sensíveis."
                    : "This option may clear active login sessions, preferences, or sensitive data.";
            }

            return "";
        }

        #endregion
    }

    #endregion

    #region 5. OPTIASSIST ADDON UI & CONTROLLER

    /// <summary>
    /// BleachBit CleanerML-powered System Cleaner & Optimizer Add-on Module compiled into SystemCleaner.dll.
    /// </summary>
    public class SystemCleanerAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ObservableCollection<BaseCleaner> _allCleaners;
        private string _activeTab = "windows"; // "windows" or "apps"
        private string _currentLang = "en-US";
        private bool _isBusy = false;
        private bool _disableSelectionWarnings = false;
        private bool _hideUninstalledApps = true;
        private CancellationTokenSource _cts;

        // UI Controls - Left Pane
        private Border _tabWindowsBorder;
        private TextBlock _tabWindowsText;
        private Border _tabAppsBorder;
        private TextBlock _tabAppsText;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRecommended;
        private CheckBox _chkHideUninstalled;
        private CheckBox _chkDisableWarnings;
        private StackPanel _pnlCategoriesContainer;
        private TextBox _txtDeepScanRoot;
        private Button _btnBrowseDeepScan;

        // UI Controls - Right Pane Top Bar
        private ProgressBar _pbProgress;
        private TextBlock _txtPercent;

        // UI Controls - Right Pane Center (4 States)
        private enum ViewMode { Idle, Running, Scanned, Cleaned }
        private StackPanel _pnlStateIdle;
        private TextBlock _txtIdleTitle;
        private TextBlock _txtIdleDesc;
        private TextBlock _txtIdleSummaryBadge;

        private StackPanel _pnlStateRunning;
        private TextBlock _txtSpinner;
        private TextBlock _txtRunningCategory;
        private TextBlock _txtRunningItem;
        private TextBlock _txtRunningStats;

        private ScrollViewer _pnlStateScanned;
        private TextBlock _txtScanHeader;
        private TextBlock _txtScanSub;
        private StackPanel _pnlScannedItemsList;

        private ScrollViewer _pnlStateCleaned;
        private TextBlock _txtCleanHeader;
        private TextBlock _txtCleanSub;
        private StackPanel _pnlCleanedItemsList;

        // UI Controls - Right Pane Bottom Action Bar
        private Button _btnLeftAction;  // "Analyze" or "Cancel"
        private Button _btnRightAction; // "Run Cleaner"

        public string Id { get { return "system-cleaner"; } }
        public string Name { get { return T("Nav_SystemCleaner", "System Cleaner"); } }
        public string Description { get { return T("Cleaner_Subtitle", "BleachBit CleanerML-driven dry-run analysis and safe cleaning for Windows and applications"); } }
        public string Version { get { return "1.1.0.0"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "🧹"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            DetectCurrentLanguage();
            InitCleaners();
        }

        private void DetectCurrentLanguage()
        {
            string probe = T("Cleaner_Quick_All", "Select All");
            if (probe.IndexOf("Selecionar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _currentLang = "pt-BR";
            }
            else
            {
                _currentLang = "en-US";
            }
        }

        private string T(string key, string fallback)
        {
            if (_host != null)
            {
                return _host.GetLocalizedString(key, fallback);
            }
            return fallback;
        }

        private void Log(string msg)
        {
            if (_host != null)
            {
                _host.Log(msg);
            }
        }

        private void InitCleaners()
        {
            LoadCleanerSettings();
            List<BaseCleaner> list = CleanerMlEngine.LoadAllCleanersFromXml(T, _currentLang);
            _allCleaners = new ObservableCollection<BaseCleaner>(list);
        }

        private bool IsCleanerVisible(BaseCleaner cleaner)
        {
            if (cleaner == null) return false;
            if (string.Equals(cleaner.TabId, "windows", StringComparison.OrdinalIgnoreCase)) return true;
            if (!_hideUninstalledApps) return true;
            return cleaner.IsInstalledOnSystem;
        }

        private string GetCleanerSettingsPath()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Addons", "SystemCleaner");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
            catch
            {
                return null;
            }
        }

        private void LoadCleanerSettings()
        {
            try
            {
                string path = GetCleanerSettingsPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    if (json.IndexOf("\"DisableSelectionWarnings\": true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        json.IndexOf("\"DisableSelectionWarnings\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _disableSelectionWarnings = true;
                    }
                    else if (json.IndexOf("\"DisableSelectionWarnings\": false", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             json.IndexOf("\"DisableSelectionWarnings\":false", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _disableSelectionWarnings = false;
                    }

                    if (json.IndexOf("\"HideUninstalledApps\": false", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        json.IndexOf("\"HideUninstalledApps\":false", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _hideUninstalledApps = false;
                    }
                    else if (json.IndexOf("\"HideUninstalledApps\": true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             json.IndexOf("\"HideUninstalledApps\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _hideUninstalledApps = true;
                    }
                }
            }
            catch { }
        }

        private void SaveCleanerSettings()
        {
            try
            {
                string path = GetCleanerSettingsPath();
                if (!string.IsNullOrEmpty(path))
                {
                    string json = "{\r\n  \"DisableSelectionWarnings\": " + (_disableSelectionWarnings ? "true" : "false") +
                                  ",\r\n  \"HideUninstalledApps\": " + (_hideUninstalledApps ? "true" : "false") + "\r\n}";
                    File.WriteAllText(path, json, new UTF8Encoding(false));
                }
            }
            catch { }
        }

        public FrameworkElement CreateView()
        {
            if (_allCleaners == null)
            {
                InitCleaners();
            }

            Grid root = new Grid
            {
                Margin = new Thickness(0)
            };

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // LEFT COLUMN: TABS + QUICK ACTIONS + CATEGORY TREE
            // -------------------------------------------------------------
            Border leftBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0)
            };
            leftBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            leftBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Tabs
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Quick actions
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 2: Categories tree

            // Row 0: Tabs Header ("Windows" vs "Applications")
            Grid tabsGrid = new Grid();
            tabsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _tabWindowsBorder = new Border
            {
                Height = 36,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0, 3, 0, 0),
                CornerRadius = new CornerRadius(5, 0, 0, 0)
            };
            _tabWindowsBorder.SetResourceReference(Border.BorderBrushProperty, "BrushAccent");
            _tabWindowsBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");

            _tabWindowsText = new TextBlock
            {
                Text = T("Cleaner_Tab_Windows", "Windows"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tabWindowsText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _tabWindowsBorder.Child = _tabWindowsText;
            _tabWindowsBorder.MouseLeftButtonDown += (s, e) => SetActiveTab("windows");
            Grid.SetColumn(_tabWindowsBorder, 0);
            tabsGrid.Children.Add(_tabWindowsBorder);

            _tabAppsBorder = new Border
            {
                Height = 36,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0, 5, 0, 0)
            };
            _tabAppsBorder.SetResourceReference(Border.BorderBrushProperty, "Transparent");
            _tabAppsBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");

            _tabAppsText = new TextBlock
            {
                Text = T("Cleaner_Tab_Apps", "Applications"),
                FontSize = 12,
                FontWeight = FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tabAppsText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _tabAppsBorder.Child = _tabAppsText;
            _tabAppsBorder.MouseLeftButtonDown += (s, e) => SetActiveTab("apps");
            Grid.SetColumn(_tabAppsBorder, 1);
            tabsGrid.Children.Add(_tabAppsBorder);

            Grid.SetRow(tabsGrid, 0);
            leftGrid.Children.Add(tabsGrid);

            // Row 1: Quick Actions Toolbar
            Border quickBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(6, 6, 6, 6)
            };
            quickBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            quickBar.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");

            StackPanel spQuickContainer = new StackPanel();

            StackPanel spQuick = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _btnSelectAll = new Button
            {
                Content = T("Cleaner_Quick_All", "Select All"),
                FontSize = 10,
                Height = 22,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0)
            };
            _btnSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectAll.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _allCleaners)
                {
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase) && IsCleanerVisible(c))
                    {
                        c.IsSelected = true;
                    }
                }
                UpdateSelectedSummary();
            };

            _btnDeselectAll = new Button
            {
                Content = T("Cleaner_Quick_None", "Deselect All"),
                FontSize = 10,
                Height = 22,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0)
            };
            _btnDeselectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeselectAll.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _allCleaners)
                {
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase) && IsCleanerVisible(c))
                    {
                        c.IsSelected = false;
                    }
                }
                UpdateSelectedSummary();
            };

            _btnSelectRecommended = new Button
            {
                Content = T("Cleaner_Quick_Default", "Defaults"),
                FontSize = 10,
                Height = 22,
                Padding = new Thickness(8, 0, 8, 0)
            };
            _btnSelectRecommended.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectRecommended.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _allCleaners)
                {
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase) && IsCleanerVisible(c))
                    {
                        c.IsSelected = c.IsRecommended && !c.IsDangerous;
                    }
                }
                UpdateSelectedSummary();
            };

            spQuick.Children.Add(_btnSelectAll);
            spQuick.Children.Add(_btnDeselectAll);
            spQuick.Children.Add(_btnSelectRecommended);
            spQuickContainer.Children.Add(spQuick);

            _chkHideUninstalled = new CheckBox
            {
                Content = _currentLang.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? "Ocultar apps não instalados"
                    : "Hide uninstalled applications",
                FontSize = 10.5,
                IsChecked = _hideUninstalledApps,
                Margin = new Thickness(4, 6, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            _chkHideUninstalled.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextSecondary");
            _chkHideUninstalled.Checked += (s, e) =>
            {
                _hideUninstalledApps = true;
                SaveCleanerSettings();
                BuildCategoriesTree();
                UpdateSelectedSummary();
            };
            _chkHideUninstalled.Unchecked += (s, e) =>
            {
                _hideUninstalledApps = false;
                SaveCleanerSettings();
                BuildCategoriesTree();
                UpdateSelectedSummary();
            };
            spQuickContainer.Children.Add(_chkHideUninstalled);

            quickBar.Child = spQuickContainer;

            Grid.SetRow(quickBar, 1);
            leftGrid.Children.Add(quickBar);

            // Row 2: Scrollable Categories Tree
            ScrollViewer svCategories = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8, 6, 8, 8)
            };
            _pnlCategoriesContainer = new StackPanel();
            svCategories.Content = _pnlCategoriesContainer;
            Grid.SetRow(svCategories, 2);
            leftGrid.Children.Add(svCategories);

            leftBorder.Child = leftGrid;
            Grid.SetColumn(leftBorder, 0);
            root.Children.Add(leftBorder);

            // -------------------------------------------------------------
            // RIGHT COLUMN: TOP PROGRESS BAR + CENTER DISPLAY + BOTTOM ACTIONS
            // -------------------------------------------------------------
            Border rightBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1)
            };
            rightBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            rightBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Progress Bar
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Central Display
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Action Bar

            // Row 0: Progress Bar & Percentage
            Border topProgressBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(5, 5, 0, 0)
            };
            topProgressBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            topProgressBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");

            Grid topProgressGrid = new Grid();
            topProgressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topProgressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _pbProgress = new ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _pbProgress.SetResourceReference(ProgressBar.ForegroundProperty, "BrushAccent");
            _pbProgress.SetResourceReference(ProgressBar.BackgroundProperty, "BrushBgPrimary");
            Grid.SetColumn(_pbProgress, 0);
            topProgressGrid.Children.Add(_pbProgress);

            _txtPercent = new TextBlock
            {
                Text = "0%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                MinWidth = 36,
                TextAlignment = TextAlignment.Right
            };
            _txtPercent.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(_txtPercent, 1);
            topProgressGrid.Children.Add(_txtPercent);

            topProgressBorder.Child = topProgressGrid;
            Grid.SetRow(topProgressBorder, 0);
            rightGrid.Children.Add(topProgressBorder);

            // Row 1: Center Display
            Grid centerGrid = new Grid { Margin = new Thickness(16, 12, 16, 12) };

            // 1.1 Idle State
            _pnlStateIdle = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Visible
            };

            TextBlock idleIcon = new TextBlock
            {
                Text = "🧹",
                FontSize = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _txtIdleTitle = new TextBlock
            {
                Text = T("Cleaner_Status_Idle_Title", "System Cleaner & Optimizer"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _txtIdleTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtIdleDesc = new TextBlock
            {
                Text = T("Cleaner_Status_Idle_Desc", "Select the items you wish to clean on the left, then click 'Analyze' to check cleanable space or 'Run Cleaner' to begin."),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 440,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _txtIdleDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            _txtIdleSummaryBadge = new TextBlock
            {
                Text = "0 items selected",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _txtIdleSummaryBadge.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _pnlStateIdle.Children.Add(idleIcon);
            _pnlStateIdle.Children.Add(_txtIdleTitle);
            _pnlStateIdle.Children.Add(_txtIdleDesc);
            _pnlStateIdle.Children.Add(_txtIdleSummaryBadge);
            centerGrid.Children.Add(_pnlStateIdle);

            // 1.2 Running State (with animated rotating spinner)
            _pnlStateRunning = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 16, 12, 12),
                Visibility = Visibility.Collapsed
            };

            StackPanel spSpinnerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _txtSpinner = new TextBlock
            {
                Text = "🔄",
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Margin = new Thickness(0, 0, 14, 0)
            };
            RotateTransform rt = new RotateTransform(0);
            _txtSpinner.RenderTransform = rt;
            DoubleAnimation da = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, da);

            StackPanel spRunningText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _txtRunningCategory = new TextBlock
            {
                Text = "Windows Explorer",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            _txtRunningCategory.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtRunningItem = new TextBlock
            {
                Text = "Thumbnails",
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _txtRunningItem.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            spRunningText.Children.Add(_txtRunningCategory);
            spRunningText.Children.Add(_txtRunningItem);
            spSpinnerRow.Children.Add(_txtSpinner);
            spSpinnerRow.Children.Add(spRunningText);
            _pnlStateRunning.Children.Add(spSpinnerRow);

            Border runningDivider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 14, 0, 10)
            };
            runningDivider.SetResourceReference(Border.BackgroundProperty, "BrushBorderCard");
            _pnlStateRunning.Children.Add(runningDivider);

            _txtRunningStats = new TextBlock
            {
                Text = "Processing...",
                FontSize = 11
            };
            _txtRunningStats.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
            _pnlStateRunning.Children.Add(_txtRunningStats);

            centerGrid.Children.Add(_pnlStateRunning);

            // 1.3 Scanned State (Results Breakdown)
            _pnlStateScanned = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Visibility = Visibility.Collapsed
            };
            StackPanel spScannedContent = new StackPanel();

            _txtScanHeader = new TextBlock
            {
                Text = "Analysis complete",
                FontSize = 15,
                FontWeight = FontWeights.Bold
            };
            _txtScanHeader.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _txtScanSub = new TextBlock
            {
                Text = "Click 'Run Cleaner' to remove these files.",
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 10)
            };
            _txtScanSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            _pnlScannedItemsList = new StackPanel();

            spScannedContent.Children.Add(_txtScanHeader);
            spScannedContent.Children.Add(_txtScanSub);
            spScannedContent.Children.Add(_pnlScannedItemsList);
            _pnlStateScanned.Content = spScannedContent;
            centerGrid.Children.Add(_pnlStateScanned);

            // 1.4 Cleaned State (Summary Breakdown)
            _pnlStateCleaned = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Visibility = Visibility.Collapsed
            };
            StackPanel spCleanedContent = new StackPanel { Margin = new Thickness(4) };

            _txtCleanHeader = new TextBlock
            {
                Text = "Cleaning complete!",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
            };

            _txtCleanSub = new TextBlock
            {
                Text = "Your system has been cleaned.",
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 10)
            };
            _txtCleanSub.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            _pnlCleanedItemsList = new StackPanel();

            spCleanedContent.Children.Add(_txtCleanHeader);
            spCleanedContent.Children.Add(_txtCleanSub);
            spCleanedContent.Children.Add(_pnlCleanedItemsList);
            _pnlStateCleaned.Content = spCleanedContent;
            centerGrid.Children.Add(_pnlStateCleaned);

            Grid.SetRow(centerGrid, 1);
            rightGrid.Children.Add(centerGrid);

            // Row 2: Bottom Action Bar
            Border bottomBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(0, 0, 5, 5)
            };
            bottomBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            bottomBar.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");

            Grid bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnLeftAction = new Button
            {
                Content = T("Btn_CleanerAnalyze", "🔍 Analyze"),
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            _btnLeftAction.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnLeftAction.Click += (s, e) =>
            {
                if (_isBusy)
                {
                    BtnCancel_Click(s, e);
                }
                else
                {
                    BtnAnalyze_Click(s, e);
                }
            };
            Grid.SetColumn(_btnLeftAction, 0);

            _btnRightAction = new Button
            {
                Content = T("Btn_CleanerClean", "🧹 Run Cleaner"),
                Height = 32,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            _btnRightAction.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnRightAction.Click += BtnClean_Click;
            Grid.SetColumn(_btnRightAction, 2);

            bottomGrid.Children.Add(_btnLeftAction);
            bottomGrid.Children.Add(_btnRightAction);
            bottomBar.Child = bottomGrid;

            Grid.SetRow(bottomBar, 2);
            rightGrid.Children.Add(bottomBar);

            rightBorder.Child = rightGrid;
            Grid.SetColumn(rightBorder, 1);
            root.Children.Add(rightBorder);

            SetActiveTab("windows");
            UpdateSelectedSummary();
            return root;
        }

        private void SetActiveTab(string tabId)
        {
            _activeTab = tabId;
            bool isWindows = string.Equals(tabId, "windows", StringComparison.OrdinalIgnoreCase);

            if (_tabWindowsBorder != null)
            {
                _tabWindowsBorder.BorderThickness = isWindows ? new Thickness(0, 3, 0, 0) : new Thickness(0);
                _tabWindowsBorder.SetResourceReference(Border.BorderBrushProperty, isWindows ? "BrushAccent" : "Transparent");
                _tabWindowsBorder.SetResourceReference(Border.BackgroundProperty, isWindows ? "BrushBgCard" : "BrushBgSidebar");
            }
            if (_tabWindowsText != null)
            {
                _tabWindowsText.FontWeight = isWindows ? FontWeights.Bold : FontWeights.Normal;
                _tabWindowsText.SetResourceReference(TextBlock.ForegroundProperty, isWindows ? "BrushTextPrimary" : "BrushTextSecondary");
            }

            if (_tabAppsBorder != null)
            {
                _tabAppsBorder.BorderThickness = !isWindows ? new Thickness(0, 3, 0, 0) : new Thickness(0);
                _tabAppsBorder.SetResourceReference(Border.BorderBrushProperty, !isWindows ? "BrushAccent" : "Transparent");
                _tabAppsBorder.SetResourceReference(Border.BackgroundProperty, !isWindows ? "BrushBgCard" : "BrushBgSidebar");
            }
            if (_tabAppsText != null)
            {
                _tabAppsText.FontWeight = !isWindows ? FontWeights.Bold : FontWeights.Normal;
                _tabAppsText.SetResourceReference(TextBlock.ForegroundProperty, !isWindows ? "BrushTextPrimary" : "BrushTextSecondary");
            }

            if (_chkHideUninstalled != null)
            {
                _chkHideUninstalled.Visibility = isWindows ? Visibility.Collapsed : Visibility.Visible;
            }

            BuildCategoriesTree();
        }

        private void BuildCategoriesTree()
        {
            if (_pnlCategoriesContainer == null || _allCleaners == null) return;
            _pnlCategoriesContainer.Children.Clear();

            // Group visible cleaners of the active tab by SectionId
            List<string> sections = new List<string>();
            foreach (BaseCleaner c in _allCleaners)
            {
                if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase) && IsCleanerVisible(c))
                {
                    if (!sections.Contains(c.SectionId))
                    {
                        sections.Add(c.SectionId);
                    }
                }
            }

            foreach (string secId in sections)
            {
                List<BaseCleaner> items = new List<BaseCleaner>();
                string secTitle = "";
                string secIcon = "";

                foreach (BaseCleaner c in _allCleaners)
                {
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.SectionId, secId, StringComparison.OrdinalIgnoreCase) &&
                        IsCleanerVisible(c))
                    {
                        items.Add(c);
                        secTitle = c.SectionTitle;
                        secIcon = c.SectionIcon;
                    }
                }

                // Section Header
                StackPanel secHeader = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 8, 0, 4)
                };

                TextBlock iconTb = new TextBlock
                {
                    Text = secIcon,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock titleTb = new TextBlock
                {
                    Text = secTitle,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleTb.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

                secHeader.Children.Add(iconTb);
                secHeader.Children.Add(titleTb);
                _pnlCategoriesContainer.Children.Add(secHeader);

                // Checkboxes for each option
                foreach (BaseCleaner cleaner in items)
                {
                    CheckBox cb = new CheckBox
                    {
                        Content = cleaner.IsDangerous ? (cleaner.Name + " ⚠️") : cleaner.Name,
                        FontSize = 11,
                        Margin = new Thickness(18, 2, 0, 2),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = string.IsNullOrEmpty(cleaner.WarningMessage)
                            ? (object)cleaner.Description
                            : string.Format("{0}\n⚠️ {1}", cleaner.Description, cleaner.WarningMessage)
                    };
                    cb.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                    cb.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsSelected")
                    {
                        Source = cleaner,
                        Mode = BindingMode.TwoWay
                    });
                    cb.Checked += (s, e) => UpdateSelectedSummary();
                    cb.Unchecked += (s, e) => UpdateSelectedSummary();

                    if (cleaner.IsDangerous)
                    {
                        Action handleDangerousConfirm = () =>
                        {
                            if (_disableSelectionWarnings)
                            {
                                cb.IsChecked = true;
                                cleaner.IsSelected = true;
                                UpdateSelectedSummary();
                                return;
                            }

                            bool confirmed = ShowDangerousItemDialog(cleaner);
                            if (confirmed)
                            {
                                cb.IsChecked = true;
                                cleaner.IsSelected = true;
                            }
                            else
                            {
                                cb.IsChecked = false;
                                cleaner.IsSelected = false;
                            }
                            UpdateSelectedSummary();
                        };

                        cb.PreviewMouseLeftButtonDown += (s, e) =>
                        {
                            if (cb.IsChecked != true && !_disableSelectionWarnings)
                            {
                                e.Handled = true;
                                handleDangerousConfirm();
                            }
                        };

                        cb.PreviewKeyDown += (s, e) =>
                        {
                            if (e.Key == System.Windows.Input.Key.Space && cb.IsChecked != true && !_disableSelectionWarnings)
                            {
                                e.Handled = true;
                                handleDangerousConfirm();
                            }
                        };
                    }

                    _pnlCategoriesContainer.Children.Add(cb);
                }

                // If Deep Scan section, add root folder picker row and Disable Warnings checkbox
                if (string.Equals(secId, "deep_scan", StringComparison.OrdinalIgnoreCase))
                {
                    _chkDisableWarnings = new CheckBox
                    {
                        Content = T("Cleaner_Chk_DisableWarnings", "Disable warnings when selecting items"),
                        FontSize = 11,
                        IsChecked = _disableSelectionWarnings,
                        Margin = new Thickness(18, 4, 0, 2),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    _chkDisableWarnings.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextPrimary");
                    _chkDisableWarnings.Checked += (s, e) =>
                    {
                        _disableSelectionWarnings = true;
                        SaveCleanerSettings();
                    };
                    _chkDisableWarnings.Unchecked += (s, e) =>
                    {
                        _disableSelectionWarnings = false;
                        SaveCleanerSettings();
                    };
                    _pnlCategoriesContainer.Children.Add(_chkDisableWarnings);

                    Border deepScanBox = new Border
                    {
                        Margin = new Thickness(18, 6, 0, 4),
                        Padding = new Thickness(8),
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(1)
                    };
                    deepScanBox.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
                    deepScanBox.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                    StackPanel spDeep = new StackPanel();
                    TextBlock lblRoot = new TextBlock
                    {
                        Text = T("Cleaner_Label_DeepScanRoot", "Deep Scan Root Directory:"),
                        FontSize = 10,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    lblRoot.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
                    spDeep.Children.Add(lblRoot);

                    Grid gridPicker = new Grid();
                    gridPicker.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    gridPicker.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    _txtDeepScanRoot = new TextBox
                    {
                        Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        FontSize = 10,
                        Height = 22,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    Grid.SetColumn(_txtDeepScanRoot, 0);

                    _btnBrowseDeepScan = new Button
                    {
                        Content = "📁",
                        Width = 26,
                        Height = 22,
                        Padding = new Thickness(0)
                    };
                    _btnBrowseDeepScan.SetResourceReference(Button.StyleProperty, "BtnDefault");
                    _btnBrowseDeepScan.Click += BtnBrowseDeepScan_Click;
                    Grid.SetColumn(_btnBrowseDeepScan, 1);

                    gridPicker.Children.Add(_txtDeepScanRoot);
                    gridPicker.Children.Add(_btnBrowseDeepScan);
                    spDeep.Children.Add(gridPicker);

                    deepScanBox.Child = spDeep;
                    _pnlCategoriesContainer.Children.Add(deepScanBox);
                }
            }
        }

        private void ShowViewMode(ViewMode mode)
        {
            if (_pnlStateIdle != null) _pnlStateIdle.Visibility = mode == ViewMode.Idle ? Visibility.Visible : Visibility.Collapsed;
            if (_pnlStateRunning != null) _pnlStateRunning.Visibility = mode == ViewMode.Running ? Visibility.Visible : Visibility.Collapsed;
            if (_pnlStateScanned != null) _pnlStateScanned.Visibility = mode == ViewMode.Scanned ? Visibility.Visible : Visibility.Collapsed;
            if (_pnlStateCleaned != null) _pnlStateCleaned.Visibility = mode == ViewMode.Cleaned ? Visibility.Visible : Visibility.Collapsed;

            if (mode == ViewMode.Running)
            {
                if (_btnLeftAction != null)
                {
                    _btnLeftAction.Content = T("Btn_CleanerCancel", "✕ Cancel");
                    _btnLeftAction.BorderBrush = (Brush)Application.Current.FindResource("BrushAccent");
                    _btnLeftAction.BorderThickness = new Thickness(1.5);
                }
                if (_btnRightAction != null)
                {
                    _btnRightAction.IsEnabled = false;
                }
            }
            else
            {
                if (_btnLeftAction != null)
                {
                    _btnLeftAction.Content = T("Btn_CleanerAnalyze", "🔍 Analyze");
                    _btnLeftAction.ClearValue(Button.BorderBrushProperty);
                    _btnLeftAction.ClearValue(Button.BorderThicknessProperty);
                    _btnLeftAction.IsEnabled = true;
                }
                if (_btnRightAction != null)
                {
                    _btnRightAction.IsEnabled = true;
                }
            }
        }

        private void UpdateSelectedSummary()
        {
            if (_allCleaners == null) return;
            long totalBytes = 0;
            int count = 0;
            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected && IsCleanerVisible(c))
                {
                    count++;
                    totalBytes += c.SizeBytes;
                }
            }
            if (_txtIdleSummaryBadge != null)
            {
                _txtIdleSummaryBadge.Text = string.Format("{0} {1} • {2}",
                    count, T("Cleaner_Results_Category", "modules selected"), BaseCleaner.FormatBytes(totalBytes));
            }
        }

        private void BtnBrowseDeepScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Title = T("Cleaner_Label_DeepScanRoot", "Select a folder by picking any file or typing path");
                dlg.CheckFileExists = false;
                dlg.CheckPathExists = true;
                dlg.FileName = "Select Folder";
                if (dlg.ShowDialog() == true)
                {
                    string dir = Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && _txtDeepScanRoot != null)
                    {
                        _txtDeepScanRoot.Text = dir;
                    }
                }
            }
            catch { }
        }

        private CleanerContext CreateExecutionContext()
        {
            CleanerContext ctx = new CleanerContext();
            ctx.Logger = msg => Log(msg);
            if (_txtDeepScanRoot != null && !string.IsNullOrWhiteSpace(_txtDeepScanRoot.Text))
            {
                ctx.DeepScanRootPath = _txtDeepScanRoot.Text.Trim();
            }
            return ctx;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy && _cts != null)
            {
                _cts.Cancel();
                if (_btnLeftAction != null) _btnLeftAction.IsEnabled = false;
            }
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _allCleaners == null) return;

            List<BaseCleaner> selected = new List<BaseCleaner>();
            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected && IsCleanerVisible(c)) selected.Add(c);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to scan or clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CleanerContext ctx = CreateExecutionContext();
            _isBusy = true;
            _cts = new CancellationTokenSource();
            _pbProgress.Value = 0;
            _txtPercent.Text = "0%";
            ShowViewMode(ViewMode.Running);

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFoundBytes = 0;
                int totalFoundItems = 0;
                int totalSkipped = 0;
                List<BaseCleaner> withFindings = new List<BaseCleaner>();

                Log("=================================================");
                Log("   STARTING SYSTEM CLEANER DRY-RUN SCAN (XML)    ");
                Log(string.Format("   Selected options: {0} | Elevated: {1}", selected.Count, ctx.IsProcessElevated));
                Log("=================================================");

                for (int i = 0; i < selected.Count; i++)
                {
                    if (_cts.IsCancellationRequested) break;

                    BaseCleaner cleaner = selected[i];
                    int pct = (int)Math.Min(100, Math.Round((i / (double)selected.Count) * 100));

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        _pbProgress.Value = pct;
                        _txtPercent.Text = pct + "%";
                        _txtRunningCategory.Text = cleaner.SectionTitle;
                        _txtRunningItem.Text = cleaner.Name;
                        _txtRunningStats.Text = string.Format("{0} • {1} {2}",
                            BaseCleaner.FormatBytes(totalFoundBytes), totalFoundItems, T("Cleaner_Results_Found", "files found"));
                    }));

                    ScanResult sr = cleaner.Scan(ctx);
                    cleaner.LastScanResult = sr;
                    totalFoundBytes += sr.EstimatedBytes;
                    totalFoundItems += sr.FoundItems.Count;
                    totalSkipped += sr.SkippedOrLockedCount;

                    cleaner.SizeBytes = sr.EstimatedBytes;
                    cleaner.FoundCount = sr.FoundItems.Count;
                    cleaner.StatusDisplay = sr.EstimatedBytes > 0 ? "Scanned" : "Clean";

                    if (sr.EstimatedBytes > 0 || sr.FoundItems.Count > 0)
                    {
                        withFindings.Add(cleaner);
                    }

                    Log(string.Format("[SCAN] {0} ({1}): {2} ({3} items found, {4} skipped/locked)",
                        cleaner.SectionTitle, cleaner.Name, BaseCleaner.FormatBytes(sr.EstimatedBytes), sr.FoundItems.Count, sr.SkippedOrLockedCount));
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    if (_cts.IsCancellationRequested)
                    {
                        _pbProgress.Value = 0;
                        _txtPercent.Text = "0%";
                        ShowViewMode(ViewMode.Idle);
                        Log("[SCAN CANCELLED] Operation cancelled by user.");
                        MessageBox.Show(T("Cleaner_Status_Cancelled", "Operation cancelled by user."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    _pbProgress.Value = 100;
                    _txtPercent.Text = "100%";
                    ShowScannedResults(totalFoundBytes, totalFoundItems, withFindings, selected.Count);
                }));
            });
        }

        private void ShowScannedResults(long totalBytes, int totalItems, List<BaseCleaner> withFindings, int totalChecked)
        {
            ShowViewMode(ViewMode.Scanned);

            if (_txtScanHeader != null)
            {
                _txtScanHeader.Text = string.Format(
                    T("Cleaner_Status_ScanDone", "Analysis complete - {0} to be removed (approximate)"),
                    BaseCleaner.FormatBytes(totalBytes));
            }
            if (_txtScanSub != null)
            {
                _txtScanSub.Text = string.Format(
                    T("Cleaner_Status_Scanned", "Dry-run complete: Found {0} ({2} items) across {1} modules."),
                    BaseCleaner.FormatBytes(totalBytes), totalChecked, totalItems);
            }

            if (_pnlScannedItemsList != null)
            {
                _pnlScannedItemsList.Children.Clear();

                if (withFindings == null || withFindings.Count == 0)
                {
                    Border cleanBox = new Border
                    {
                        Background = (Brush)Application.Current.FindResource("BrushBgSidebar"),
                        BorderBrush = (Brush)Application.Current.FindResource("BrushBorderCard"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(16),
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    TextBlock tbClean = new TextBlock
                    {
                        Text = T("Cleaner_Results_NoJunk", "✔️ System is already clean! No unnecessary files were found in selected categories."),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    tbClean.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                    cleanBox.Child = tbClean;
                    _pnlScannedItemsList.Children.Add(cleanBox);
                }
                else
                {
                    Dictionary<string, List<BaseCleaner>> grouped = new Dictionary<string, List<BaseCleaner>>();
                    foreach (BaseCleaner c in withFindings)
                    {
                        string key = c.SectionTitle;
                        if (!grouped.ContainsKey(key)) grouped[key] = new List<BaseCleaner>();
                        grouped[key].Add(c);
                    }

                    foreach (KeyValuePair<string, List<BaseCleaner>> grp in grouped)
                    {
                        TextBlock grpHeader = new TextBlock
                        {
                            Text = grp.Key,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        grpHeader.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                        _pnlScannedItemsList.Children.Add(grpHeader);

                        foreach (BaseCleaner item in grp.Value)
                        {
                            Border card = new Border
                            {
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(5),
                                Padding = new Thickness(10, 8, 10, 8),
                                Margin = new Thickness(0, 3, 0, 4)
                            };
                            card.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");
                            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                            StackPanel cardStack = new StackPanel();

                            Grid g = new Grid();
                            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            StackPanel spLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                            TextBlock txtName = new TextBlock
                            {
                                Text = (item.Icon != null ? (item.Icon + " ") : "") + item.Name,
                                FontSize = 11.5,
                                FontWeight = FontWeights.SemiBold
                            };
                            txtName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

                            TextBlock txtCount = new TextBlock
                            {
                                Text = string.Format("{0} {1}", item.FoundCount, T("Cleaner_Results_Found", "files found")),
                                FontSize = 10,
                                Margin = new Thickness(0, 1, 0, 0)
                            };
                            txtCount.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

                            spLeft.Children.Add(txtName);
                            spLeft.Children.Add(txtCount);
                            Grid.SetColumn(spLeft, 0);
                            g.Children.Add(spLeft);

                            Border pnlFiles = new Border
                            {
                                Visibility = Visibility.Collapsed,
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(8),
                                Margin = new Thickness(0, 8, 0, 2)
                            };
                            pnlFiles.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
                            pnlFiles.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

                            bool hasFiles = item.LastScanResult != null && item.LastScanResult.ScannedFiles != null && item.LastScanResult.ScannedFiles.Count > 0;
                            if (hasFiles)
                            {
                                Button btnToggleFiles = new Button
                                {
                                    Content = "👁️ " + T("Cleaner_Btn_ViewFiles", "View Files") + " (" + item.LastScanResult.ScannedFiles.Count + ")",
                                    FontSize = 10.5,
                                    Height = 24,
                                    Padding = new Thickness(8, 0, 8, 0),
                                    Margin = new Thickness(8, 0, 12, 0),
                                    Cursor = Cursors.Hand,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                btnToggleFiles.SetResourceReference(Button.StyleProperty, "BtnDefault");

                                StackPanel filesStack = new StackPanel();

                                Grid tbGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                TextBox txtFilter = new TextBox
                                {
                                    Height = 24,
                                    FontSize = 10.5,
                                    Padding = new Thickness(6, 2, 6, 2),
                                    VerticalContentAlignment = VerticalAlignment.Center,
                                    ToolTip = T("Cleaner_Filter_Placeholder", "Filter files by name...")
                                };
                                txtFilter.SetResourceReference(TextBox.BackgroundProperty, "BrushBgInput");
                                txtFilter.SetResourceReference(TextBox.ForegroundProperty, "BrushTextPrimary");
                                txtFilter.SetResourceReference(TextBox.BorderBrushProperty, "BrushBorderInput");
                                Grid.SetColumn(txtFilter, 0);
                                tbGrid.Children.Add(txtFilter);

                                Button btnCopy = new Button
                                {
                                    Content = T("Cleaner_Btn_CopyList", "📋 Copy List"),
                                    Height = 24,
                                    FontSize = 10,
                                    Padding = new Thickness(8, 0, 8, 0),
                                    Margin = new Thickness(6, 0, 0, 0),
                                    Cursor = Cursors.Hand
                                };
                                btnCopy.SetResourceReference(Button.StyleProperty, "BtnDefault");
                                btnCopy.Click += (s, e) =>
                                {
                                    try
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        foreach (var fe in item.LastScanResult.ScannedFiles)
                                        {
                                            sb.AppendLine(fe.SizeDisplay + "\t" + fe.Path);
                                        }
                                        Clipboard.SetText(sb.ToString());
                                        MessageBox.Show(T("Cleaner_Msg_Copied", "File paths copied to clipboard!"), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    catch { }
                                };
                                Grid.SetColumn(btnCopy, 1);
                                tbGrid.Children.Add(btnCopy);
                                filesStack.Children.Add(tbGrid);

                                ScrollViewer svFiles = new ScrollViewer
                                {
                                    MaxHeight = 200,
                                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                                };
                                StackPanel spList = new StackPanel();

                                Action<string> renderList = (filter) =>
                                {
                                    spList.Children.Clear();
                                    string q = (filter ?? "").Trim().ToLowerInvariant();
                                    int count = 0;
                                    foreach (var f in item.LastScanResult.ScannedFiles)
                                    {
                                        if (!string.IsNullOrEmpty(q) && (f.Path == null || !f.Path.ToLowerInvariant().Contains(q)))
                                            continue;

                                        if (count >= 500)
                                        {
                                            TextBlock tbMore = new TextBlock
                                            {
                                                Text = string.Format(T("Cleaner_Msg_MoreFiles", "... and {0} more files (use search filter)"), item.LastScanResult.ScannedFiles.Count - count),
                                                FontSize = 10,
                                                FontStyle = FontStyles.Italic,
                                                Margin = new Thickness(4, 4, 0, 2)
                                            };
                                            tbMore.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
                                            spList.Children.Add(tbMore);
                                            break;
                                        }

                                        Grid fileRow = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                                        fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                        fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                        TextBlock tbPath = new TextBlock
                                        {
                                            Text = f.Path,
                                            FontSize = 10,
                                            FontFamily = new FontFamily("Consolas, Segoe UI"),
                                            TextTrimming = TextTrimming.CharacterEllipsis,
                                            ToolTip = f.Path,
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        tbPath.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                                        Grid.SetColumn(tbPath, 0);
                                        fileRow.Children.Add(tbPath);

                                        TextBlock tbSize = new TextBlock
                                        {
                                            Text = f.SizeDisplay,
                                            FontSize = 10,
                                            FontWeight = FontWeights.Bold,
                                            Margin = new Thickness(8, 0, 8, 0),
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        tbSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                                        Grid.SetColumn(tbSize, 1);
                                        fileRow.Children.Add(tbSize);

                                        Button btnOpen = new Button
                                        {
                                            Content = "📂",
                                            Width = 22,
                                            Height = 18,
                                            FontSize = 9,
                                            Padding = new Thickness(0),
                                            ToolTip = T("Cleaner_Tooltip_OpenFolder", "Open folder in Windows Explorer"),
                                            Cursor = Cursors.Hand
                                        };
                                        btnOpen.SetResourceReference(Button.StyleProperty, "BtnDefault");
                                        string capturedPath = f.Path;
                                        btnOpen.Click += (s, e) =>
                                        {
                                            try
                                            {
                                                string cleanPath = capturedPath;
                                                int bracketIdx = cleanPath.IndexOf(" [", StringComparison.Ordinal);
                                                if (bracketIdx > 0) cleanPath = cleanPath.Substring(0, bracketIdx);

                                                if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
                                                {
                                                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", cleanPath));
                                                }
                                                else
                                                {
                                                    string dir = Path.GetDirectoryName(cleanPath);
                                                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                                    {
                                                        Process.Start("explorer.exe", dir);
                                                    }
                                                }
                                            }
                                            catch { }
                                        };
                                        Grid.SetColumn(btnOpen, 2);
                                        fileRow.Children.Add(btnOpen);

                                        ContextMenu cm = new ContextMenu();
                                        MenuItem miCopy = new MenuItem { Header = T("Cleaner_Ctx_CopyPath", "Copy file path") };
                                        miCopy.Click += (s, e) => { try { Clipboard.SetText(capturedPath); } catch { } };
                                        cm.Items.Add(miCopy);
                                        fileRow.ContextMenu = cm;

                                        spList.Children.Add(fileRow);
                                        count++;
                                    }
                                };

                                txtFilter.TextChanged += (s, e) => renderList(txtFilter.Text);
                                svFiles.Content = spList;
                                filesStack.Children.Add(svFiles);
                                pnlFiles.Child = filesStack;

                                btnToggleFiles.Click += (s, e) =>
                                {
                                    if (pnlFiles.Visibility == Visibility.Visible)
                                    {
                                        pnlFiles.Visibility = Visibility.Collapsed;
                                        btnToggleFiles.Content = "👁️ " + T("Cleaner_Btn_ViewFiles", "View Files") + " (" + item.LastScanResult.ScannedFiles.Count + ")";
                                    }
                                    else
                                    {
                                        renderList(txtFilter.Text);
                                        pnlFiles.Visibility = Visibility.Visible;
                                        btnToggleFiles.Content = "✕ " + T("Cleaner_Btn_HideFiles", "Hide Files");
                                    }
                                };

                                Grid.SetColumn(btnToggleFiles, 1);
                                g.Children.Add(btnToggleFiles);
                            }

                            TextBlock txtSize = new TextBlock
                            {
                                Text = item.SizeDisplay,
                                FontSize = 12,
                                FontWeight = FontWeights.Bold,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            txtSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                            Grid.SetColumn(txtSize, 2);
                            g.Children.Add(txtSize);

                            cardStack.Children.Add(g);
                            cardStack.Children.Add(pnlFiles);
                            card.Child = cardStack;
                            _pnlScannedItemsList.Children.Add(card);
                        }
                    }
                }
            }
        }

        private List<string> CheckRunningProcessesForSelected(List<BaseCleaner> selected)
        {
            List<string> activeApps = new List<string>();
            HashSet<string> checkedExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (BaseCleaner c in selected)
            {
                if (c.RunningExecutables == null) continue;
                foreach (string exe in c.RunningExecutables)
                {
                    if (string.IsNullOrWhiteSpace(exe) || !checkedExes.Add(exe)) continue;
                    try
                    {
                        string procName = Path.GetFileNameWithoutExtension(exe);
                        if (!string.IsNullOrEmpty(procName) && Process.GetProcessesByName(procName).Length > 0)
                        {
                            activeApps.Add(string.Format("{0} ({1})", c.SectionTitle, exe));
                        }
                    }
                    catch { }
                }
            }

            return activeApps;
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _allCleaners == null) return;

            List<BaseCleaner> selected = new List<BaseCleaner>();
            List<string> warnings = new List<string>();
            long estimatedSpace = 0;

            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected && IsCleanerVisible(c))
                {
                    selected.Add(c);
                    estimatedSpace += c.SizeBytes;
                    if (c.IsDangerous && !string.IsNullOrEmpty(c.WarningMessage))
                    {
                        warnings.Add("• " + c.SectionTitle + " - " + c.Name + ": " + c.WarningMessage);
                    }
                }
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check running processes defined in CleanerML <running type="exe" os="windows">
            List<string> runningApps = CheckRunningProcessesForSelected(selected);
            if (runningApps.Count > 0)
            {
                string browserMsg = string.Format(
                    T("Msg_CleanerBrowserRunning", "The following applications are currently running:\n• {0}\n\nOpen databases (Cookies, History, SQLite Vacuum) may be locked. Close the application first for a complete cleanup, or click Yes to continue and skip locked files."),
                    string.Join("\n• ", runningApps.ToArray()));
                if (MessageBox.Show(browserMsg, "OptiAssist", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            string confirmTitle = T("Msg_CleanerConfirmTitle", "Confirm System Cleanup");
            StringBuilder confirmSb = new StringBuilder();
            confirmSb.AppendLine(T("Msg_CleanerConfirmPrompt", "Are you sure you want to perform the selected and reviewed cleaning actions?"));
            confirmSb.AppendLine();
            confirmSb.AppendLine(string.Format(
                T("Msg_CleanerConfirmDetails", "• Modules to process: {0}\n• Estimated space to reclaim: {1}"),
                selected.Count, BaseCleaner.FormatBytes(estimatedSpace)));
            confirmSb.AppendLine();
            confirmSb.AppendLine(T("Msg_CleanerConfirmNote", "All identified temporary files, caches, and items will be permanently removed."));

            if (warnings.Count > 0)
            {
                confirmSb.AppendLine();
                confirmSb.AppendLine(T("Cleaner_Warn_Header", "⚠️ Warning / Sensitive Items Selected:"));
                foreach (string w in warnings)
                {
                    confirmSb.AppendLine(w);
                }
            }

            if (MessageBox.Show(confirmSb.ToString(), confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            CleanerContext ctx = CreateExecutionContext();
            _isBusy = true;
            _cts = new CancellationTokenSource();
            _pbProgress.Value = 0;
            _txtPercent.Text = "0%";
            ShowViewMode(ViewMode.Running);

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFreed = 0;
                int totalDeleted = 0;
                int totalSkipped = 0;
                List<BaseCleaner> cleanedItems = new List<BaseCleaner>();

                Log("=================================================");
                Log("   STARTING SYSTEM CLEANING & OPTIMIZATION (XML) ");
                Log(string.Format("   Options to clean: {0}", selected.Count));
                Log("=================================================");

                for (int i = 0; i < selected.Count; i++)
                {
                    if (_cts.IsCancellationRequested) break;

                    BaseCleaner cleaner = selected[i];
                    int pct = (int)Math.Min(100, Math.Round((i / (double)selected.Count) * 100));

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        _pbProgress.Value = pct;
                        _txtPercent.Text = pct + "%";
                        _txtRunningCategory.Text = cleaner.SectionTitle;
                        _txtRunningItem.Text = cleaner.Name;
                        _txtRunningStats.Text = string.Format("{0} freed • {1} items cleaned",
                            BaseCleaner.FormatBytes(totalFreed), totalDeleted);
                    }));

                    CleanResult cr = cleaner.Clean(ctx);
                    totalFreed += cr.FreedBytes;
                    totalDeleted += cr.DeletedCount;
                    totalSkipped += cr.ErrorOrLockedCount;

                    cleaner.SizeBytes = 0;
                    cleaner.FoundCount = 0;
                    cleaner.LastScanResult = null;
                    cleaner.SizeDisplay = "Cleaned";
                    cleaner.StatusDisplay = "Cleaned";
                    cleanedItems.Add(cleaner);
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    if (_cts.IsCancellationRequested)
                    {
                        _pbProgress.Value = 0;
                        _txtPercent.Text = "0%";
                        ShowViewMode(ViewMode.Idle);
                        Log("[CLEAN CANCELLED] Operation cancelled by user.");
                        MessageBox.Show(T("Cleaner_Status_Cancelled", "Operation cancelled by user."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    _pbProgress.Value = 100;
                    _txtPercent.Text = "100%";
                    ShowCleanedResults(totalFreed, totalDeleted, totalSkipped, cleanedItems);
                }));
            });
        }

        private void ShowCleanedResults(long totalFreed, int totalDeleted, int totalSkipped, List<BaseCleaner> cleanedItems)
        {
            ShowViewMode(ViewMode.Cleaned);

            if (_txtCleanHeader != null)
            {
                _txtCleanHeader.Text = string.Format(
                    T("Cleaner_Status_CleanDone", "Cleaning complete - {0} freed successfully!"),
                    BaseCleaner.FormatBytes(totalFreed));
            }
            if (_txtCleanSub != null)
            {
                _txtCleanSub.Text = string.Format(
                    T("Cleaner_Status_Cleaned", "Cleaning completed: {0} freed across {1} modules! ({2} items skipped/locked)"),
                    BaseCleaner.FormatBytes(totalFreed), cleanedItems.Count, totalSkipped);
            }

            if (_pnlCleanedItemsList != null)
            {
                _pnlCleanedItemsList.Children.Clear();

                Border successCard = new Border
                {
                    Background = (Brush)Application.Current.FindResource("BrushBgSidebar"),
                    BorderBrush = (Brush)Application.Current.FindResource("BrushBorderCard"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 8, 0, 0)
                };

                StackPanel sp = new StackPanel();
                TextBlock tb = new TextBlock
                {
                    Text = string.Format("✔️ {0} items cleaned • {1} reclaimable space freed.", totalDeleted, BaseCleaner.FormatBytes(totalFreed)),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
                sp.Children.Add(tb);

                if (totalSkipped > 0)
                {
                    TextBlock tbSkipped = new TextBlock
                    {
                        Text = string.Format("ℹ️ {0} items were skipped because they were currently locked or in use.", totalSkipped),
                        FontSize = 10,
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    tbSkipped.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextMuted");
                    sp.Children.Add(tbSkipped);
                }

                successCard.Child = sp;
                _pnlCleanedItemsList.Children.Add(successCard);
            }

            UpdateSelectedSummary();
        }

        private bool ShowDangerousItemDialog(BaseCleaner cleaner)
        {
            string warn = !string.IsNullOrEmpty(cleaner.WarningMessage)
                ? cleaner.WarningMessage
                : cleaner.Description;
            string title = T("Msg_CleanerDangerousTitle", "OptiAssist - Caution Warning");
            string msg = string.Format(
                T("Msg_CleanerDangerousPrompt",
                  "⚠️ CAUTION WARNING\n\nSelected item: {0}\n\nRisks and Implications:\n{1}\n\nThis option may clear active login sessions, browsing preferences, or sensitive caches.\n\nAre you sure you want to enable this option for cleaning anyway?"),
                cleaner.SectionTitle + " - " + cleaner.Name, warn);

            Window win = new Window
            {
                Title = title,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                win.Owner = Application.Current.MainWindow;
            }
            win.SetResourceReference(Window.BackgroundProperty, "BrushBgPrimary");

            Border rootBorder = new Border
            {
                Padding = new Thickness(20),
                BorderThickness = new Thickness(1)
            };
            rootBorder.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            rootBorder.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel sp = new StackPanel();

            TextBlock tbMsg = new TextBlock
            {
                Text = msg,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };
            tbMsg.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            sp.Children.Add(tbMsg);

            CheckBox chkSuppress = new CheckBox
            {
                Content = T("Cleaner_Chk_DisableWarnings", "Disable warnings when selecting items"),
                FontSize = 11,
                IsChecked = _disableSelectionWarnings,
                Margin = new Thickness(0, 0, 0, 16),
                Cursor = Cursors.Hand
            };
            chkSuppress.SetResourceReference(CheckBox.ForegroundProperty, "BrushTextSecondary");
            sp.Children.Add(chkSuppress);

            StackPanel spBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            bool result = false;

            Button btnNo = new Button
            {
                Content = T("Cleaner_Btn_No", "No"),
                MinWidth = 80,
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                IsCancel = true
            };
            btnNo.SetResourceReference(Button.StyleProperty, "BtnDefault");
            btnNo.Click += (s, e) =>
            {
                result = false;
                win.Close();
            };

            Button btnYes = new Button
            {
                Content = T("Cleaner_Btn_Yes", "Yes"),
                MinWidth = 80,
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            btnYes.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnYes.Click += (s, e) =>
            {
                result = true;
                win.Close();
            };

            spBtns.Children.Add(btnNo);
            spBtns.Children.Add(btnYes);
            sp.Children.Add(spBtns);

            rootBorder.Child = sp;
            win.Content = rootBorder;
            win.ShowDialog();

            if (chkSuppress.IsChecked == true && !_disableSelectionWarnings)
            {
                _disableSelectionWarnings = true;
                if (_chkDisableWarnings != null) _chkDisableWarnings.IsChecked = true;
                SaveCleanerSettings();
            }

            return result;
        }

        public void OnLanguageChanged(string langCode)
        {
            _currentLang = string.IsNullOrEmpty(langCode) ? "en-US" : langCode;

            if (_tabWindowsText != null) _tabWindowsText.Text = T("Cleaner_Tab_Windows", "Windows");
            if (_tabAppsText != null) _tabAppsText.Text = T("Cleaner_Tab_Apps", "Applications");
            if (_btnSelectAll != null) _btnSelectAll.Content = T("Cleaner_Quick_All", "Select All");
            if (_btnDeselectAll != null) _btnDeselectAll.Content = T("Cleaner_Quick_None", "Deselect All");
            if (_btnSelectRecommended != null) _btnSelectRecommended.Content = T("Cleaner_Quick_Default", "Defaults");
            if (_chkHideUninstalled != null)
            {
                _chkHideUninstalled.Content = _currentLang.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? "Ocultar apps não instalados"
                    : "Hide uninstalled applications";
            }
            if (_chkDisableWarnings != null) _chkDisableWarnings.Content = T("Cleaner_Chk_DisableWarnings", "Disable warnings when selecting items");
            if (_txtIdleTitle != null) _txtIdleTitle.Text = T("Cleaner_Status_Idle_Title", "System Cleaner & Optimizer");
            if (_txtIdleDesc != null) _txtIdleDesc.Text = T("Cleaner_Status_Idle_Desc", "Select the items you wish to clean on the left, then click 'Analyze' to check cleanable space or 'Run Cleaner' to begin.");
            if (_btnLeftAction != null && !_isBusy) _btnLeftAction.Content = T("Btn_CleanerAnalyze", "🔍 Analyze");
            if (_btnRightAction != null) _btnRightAction.Content = T("Btn_CleanerClean", "🧹 Run Cleaner");

            ApplyLocalizedMetadata();
            BuildCategoriesTree();
            UpdateSelectedSummary();
        }

        private void ApplyLocalizedMetadata()
        {
            if (_allCleaners == null) return;

            foreach (BaseCleaner item in _allCleaners)
            {
                CleanerMlOptionCleaner xmlOpt = item as CleanerMlOptionCleaner;
                if (xmlOpt != null)
                {
                    xmlOpt.SectionTitle = CleanerMlEngine.TranslateCleanerLabel(xmlOpt.RawCleanerLabel, _currentLang);
                    xmlOpt.Category = xmlOpt.SectionTitle;
                    xmlOpt.Name = CleanerMlEngine.TranslateOptionLabel(xmlOpt.RawOptionLabel, _currentLang);
                    xmlOpt.Description = CleanerMlEngine.TranslateOptionDescription(xmlOpt.RawOptionDescription, _currentLang);
                    xmlOpt.WarningMessage = CleanerMlEngine.TranslateWarningText(xmlOpt.RawWarning, xmlOpt.OptionXmlId, _currentLang);
                }
            }
        }
    }

    #endregion
}
