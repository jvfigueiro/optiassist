using System;
using System.IO;
using System.Text;
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
using Microsoft.Win32;

namespace OptiAssist.Addons.SystemCleaner
{
    #region RESULTS, CONTEXT & PATH SANITIZER

    /// <summary>
    /// Result returned by BaseCleaner.Scan() during a non-destructive dry-run analysis.
    /// </summary>
    public class ScanResult
    {
        public string CleanerId { get; set; }
        public List<string> FoundItems { get; set; }
        public long EstimatedBytes { get; set; }
        public int SkippedOrLockedCount { get; set; }
        public List<string> Warnings { get; set; }

        public ScanResult()
        {
            FoundItems = new List<string>();
            Warnings = new List<string>();
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
    /// and protects critical operating system and application directories.
    /// </summary>
    public static class PathSanitizer
    {
        private static readonly string[]ExplicitAllowedWindowsSubpaths = new string[]
        {
            @"Windows\Temp",
            @"Windows\Prefetch",
            @"Windows\Minidump",
            @"Windows\LiveKernelReports",
            @"Windows\SoftwareDistribution\Download",
            @"Windows\SoftwareDistribution\DeliveryOptimization",
            @"Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache",
            @"Windows\Logs\CBS",
            @"Windows\Panther",
            @"Windows\MEMORY.DMP"
        };

        /// <summary>
        /// Validates whether a target file or directory path is safe for deletion.
        /// Rejects root drives, critical OS directories, Program Files, and the OptiAssist directory.
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

                // Never allow deleting top-level system or user profile roots directly
                if (string.Equals(full, winDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, sysDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, progFiles, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(progFilesX86) && string.Equals(full, progFilesX86, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(usersRoot) && string.Equals(full, usersRoot, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(full, userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Never allow deleting anything inside Program Files or System32
                if (full.StartsWith(progFiles + "\\", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(progFilesX86) && full.StartsWith(progFilesX86 + "\\", StringComparison.OrdinalIgnoreCase)) ||
                    full.StartsWith(sysDir + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // If path is inside C:\Windows, it must match one of the explicitly whitelisted temp/cache subpaths
                if (full.StartsWith(winDir + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    string sysRoot = Path.GetPathRoot(winDir);
                    foreach (string allowedSub in ExplicitAllowedWindowsSubpaths)
                    {
                        string allowedFull = Path.Combine(sysRoot, allowedSub).TrimEnd('\\', '/');
                        if (string.Equals(full, allowedFull, StringComparison.OrdinalIgnoreCase) ||
                            full.StartsWith(allowedFull + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
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
                    string.Equals(name, "AppData", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string appBase = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                if (full.StartsWith(appBase, StringComparison.OrdinalIgnoreCase))
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

    #region BASE CLEANER ABSTRACT ARCHITECTURE

    /// <summary>
    /// Abstract base class modeled after BleachBit's modular cleaner architecture.
    /// Every cleaner exposes metadata, dry-run Scan(), and safe Clean() operations.
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
            _isSelected = isRecommended && !isDangerous;
            _sizeBytes = 0;
            _sizeDisplay = "—";
            _statusDisplay = "Ready";
        }

        /// <summary>
        /// Dry-run scan: discovers target files/keys and calculates estimated reclaimable bytes without deleting anything.
        /// </summary>
        public abstract ScanResult Scan(CleanerContext context);

        /// <summary>
        /// Safe cleanup execution: deletes discovered items or runs maintenance, gracefully skipping locked/in-use files.
        /// </summary>
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

        #region SHARED SAFE FILE & DIRECTORY HELPERS

        protected static void ScanDirectoryFiles(string dirPath, string pattern, bool recursive, ScanResult result)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;

            try
            {
                string[] files = Directory.GetFiles(dirPath, pattern, SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        result.FoundItems.Add(f);
                        result.EstimatedBytes += fi.Length;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        result.SkippedOrLockedCount++;
                    }
                    catch (IOException)
                    {
                        result.SkippedOrLockedCount++;
                    }
                    catch { }
                }

                if (recursive)
                {
                    string[] subDirs = Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly);
                    foreach (string sub in subDirs)
                    {
                        try
                        {
                            FileAttributes attr = File.GetAttributes(sub);
                            if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                                continue;
                            ScanDirectoryFiles(sub, pattern, true, result);
                        }
                        catch
                        {
                            result.SkippedOrLockedCount++;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.SkippedOrLockedCount++;
            }
            catch (IOException)
            {
                result.SkippedOrLockedCount++;
            }
            catch { }
        }

        protected static void CleanDirectoryContents(string dirPath, string pattern, bool recursive, CleanResult result, CleanerContext context)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;
            if (!PathSanitizer.IsSafeToDelete(dirPath))
            {
                result.Errors.Add("Blocked unsafe path: " + dirPath);
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(dirPath, pattern, SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    DeleteFileSafe(f, result);
                }

                if (recursive)
                {
                    string[] subDirs = Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly);
                    foreach (string sub in subDirs)
                    {
                        try
                        {
                            FileAttributes attr = File.GetAttributes(sub);
                            if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                                continue;

                            CleanDirectoryContents(sub, pattern, true, result, context);

                            if (pattern == "*" && PathSanitizer.IsSafeToDelete(sub))
                            {
                                try
                                {
                                    if (Directory.GetFiles(sub).Length == 0 && Directory.GetDirectories(sub).Length == 0)
                                    {
                                        Directory.Delete(sub, false);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch
                        {
                            result.ErrorOrLockedCount++;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.ErrorOrLockedCount++;
            }
            catch (IOException)
            {
                result.ErrorOrLockedCount++;
            }
            catch { }
        }

        protected static void DeleteFileSafe(string filePath, CleanResult result)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            if (!PathSanitizer.IsSafeToDelete(filePath))
            {
                result.Errors.Add("Blocked unsafe file path: " + filePath);
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
                // EBUSY / File in use by another process
                result.ErrorOrLockedCount++;
            }
            catch (UnauthorizedAccessException)
            {
                // PermissionError
                result.ErrorOrLockedCount++;
            }
            catch
            {
                result.ErrorOrLockedCount++;
            }
        }

        protected static void DeleteEntireDirectorySafe(string dirPath, CleanResult result, CleanerContext context)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;
            if (!PathSanitizer.IsSafeToDelete(dirPath))
            {
                result.Errors.Add("Blocked unsafe directory deletion: " + dirPath);
                return;
            }

            CleanDirectoryContents(dirPath, "*", true, result, context);
            try
            {
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                }
            }
            catch
            {
                result.ErrorOrLockedCount++;
            }
        }

        protected static long GetDirectorySizeSafe(string dirPath)
        {
            ScanResult sr = new ScanResult();
            ScanDirectoryFiles(dirPath, "*", true, sr);
            return sr.EstimatedBytes;
        }

        #endregion
    }

    #endregion

    #region 1. WINDOWS CORE CLEANERS

    /// <summary>
    /// Declarative path-based cleaner for standard OS directories and file patterns.
    /// </summary>
    public class DirectoryPatternCleaner : BaseCleaner
    {
        private readonly Func<List<string>> _targetDirsProvider;
        private readonly string _filePattern;
        private readonly bool _recursive;
        private readonly List<string> _explicitFiles;

        public DirectoryPatternCleaner(
            string id,
            string categoryId,
            string category,
            string name,
            string description,
            string icon,
            bool requiresAdmin,
            bool isDangerous,
            string warningMessage,
            bool isRecommended,
            Func<List<string>> targetDirsProvider,
            string filePattern = "*",
            bool recursive = true,
            List<string> explicitFiles = null)
            : base(id, categoryId, category, name, description, icon, requiresAdmin, isDangerous, warningMessage, isRecommended)
        {
            _targetDirsProvider = targetDirsProvider;
            _filePattern = filePattern;
            _recursive = recursive;
            _explicitFiles = explicitFiles;
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            if (_explicitFiles != null)
            {
                foreach (string file in _explicitFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            FileInfo fi = new FileInfo(file);
                            res.FoundItems.Add(file);
                            res.EstimatedBytes += fi.Length;
                        }
                    }
                    catch { res.SkippedOrLockedCount++; }
                }
            }

            if (_targetDirsProvider != null)
            {
                foreach (string dir in _targetDirsProvider())
                {
                    ScanDirectoryFiles(dir, _filePattern, _recursive, res);
                }
            }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            if (_explicitFiles != null)
            {
                foreach (string file in _explicitFiles)
                {
                    DeleteFileSafe(file, res);
                }
            }

            if (_targetDirsProvider != null)
            {
                foreach (string dir in _targetDirsProvider())
                {
                    CleanDirectoryContents(dir, _filePattern, _recursive, res, context);
                }
            }

            context.Log(string.Format("[CLEANER] {0}: Freed {1} ({2} deleted, {3} locked/skipped).",
                Name, FormatBytes(res.FreedBytes), res.DeletedCount, res.ErrorOrLockedCount));
            return res;
        }
    }

    public class RecycleBinCleaner : BaseCleaner
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public RecycleBinCleaner()
            : base("recycle_bin", "win_core", "Windows Core",
                   "Empty Recycle Bin",
                   "Permanently deletes all files from recycle bins on all connected drives via SHEmptyRecycleBin",
                   "🗑️", false, false, "", true)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                int hr = SHQueryRecycleBin(null, ref info);
                if (hr == 0 && info.i64Size > 0)
                {
                    res.EstimatedBytes = info.i64Size;
                    for (int i = 0; i < (int)Math.Min(info.i64NumItems, 500); i++)
                    {
                        res.FoundItems.Add("RecycleBinItem#" + i);
                    }
                }
            }
            catch { }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            ScanResult before = Scan(context);
            try
            {
                uint hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (hr == 0 || before.EstimatedBytes > 0)
                {
                    res.FreedBytes = before.EstimatedBytes;
                    res.DeletedCount = Math.Max(1, before.FoundItems.Count);
                    context.Log(string.Format("[CLEANER] Recycle Bin emptied: {0} freed.", FormatBytes(res.FreedBytes)));
                }
            }
            catch (Exception ex)
            {
                res.ErrorOrLockedCount++;
                res.Errors.Add(ex.Message);
            }
            return res;
        }
    }

    public class ClipboardCleaner : BaseCleaner
    {
        public ClipboardCleaner()
            : base("clipboard", "win_core", "Windows Core",
                   "Windows Clipboard",
                   "Clears all currently copied text, images, and data from system memory clipboard",
                   "📋", false, false, "", false)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (Clipboard.ContainsText() || Clipboard.ContainsImage() || Clipboard.ContainsFileDropList())
                        {
                            res.FoundItems.Add("SystemClipboardData");
                            res.EstimatedBytes = 4096;
                        }
                    }));
                }
            }
            catch { }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        Clipboard.Clear();
                    }));
                }
                res.FreedBytes = 4096;
                res.DeletedCount = 1;
                context.Log("[CLEANER] Windows Clipboard memory cleared.");
            }
            catch
            {
                res.ErrorOrLockedCount++;
            }
            return res;
        }
    }

    public class DnsCacheCleaner : BaseCleaner
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern uint DnsFlushResolverCache();

        public DnsCacheCleaner()
            : base("dns_cache", "win_core", "Windows Core",
                   "DNS Resolver Cache",
                   "Flushes the local DNS resolver cache via DnsFlushResolverCache API and ipconfig /flushdns",
                   "🌐", false, false, "", true)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            res.FoundItems.Add("DnsResolverCache");
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            try
            {
                DnsFlushResolverCache();
                ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3000);
                }
                res.DeletedCount = 1;
                context.Log("[CLEANER] DNS Resolver Cache flushed successfully.");
            }
            catch (Exception ex)
            {
                res.ErrorOrLockedCount++;
                res.Errors.Add(ex.Message);
            }
            return res;
        }
    }

    public class EventLogsCleaner : BaseCleaner
    {
        public EventLogsCleaner()
            : base("system_logs", "win_core", "Windows Core",
                   "System Event & Setup Logs",
                   "Clears Windows Event Logs (Application, System, Security, Setup) and CBS/Panther logs",
                   "📑", true, true,
                   "Clearing Windows Event Logs permanently erases system diagnostic history.",
                   false)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            ScanDirectoryFiles(Path.Combine(winDir, "System32", "winevt", "Logs"), "*.evtx", false, res);
            ScanDirectoryFiles(Path.Combine(winDir, "Logs", "CBS"), "*.log", true, res);
            ScanDirectoryFiles(Path.Combine(winDir, "Panther"), "*.log", true, res);
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            ScanResult before = Scan(context);

            try
            {
                string[] standardLogs = new string[] { "Application", "System", "Security", "Setup" };
                foreach (string logName in standardLogs)
                {
                    try
                    {
                        using (EventLog el = new EventLog(logName))
                        {
                            el.Clear();
                            res.DeletedCount++;
                        }
                    }
                    catch
                    {
                        res.ErrorOrLockedCount++;
                    }

                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("wevtutil", "cl " + logName)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (Process p = Process.Start(psi))
                        {
                            if (p != null) p.WaitForExit(3000);
                        }
                    }
                    catch { }
                }

                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                CleanDirectoryContents(Path.Combine(winDir, "Logs", "CBS"), "*.log", true, res, context);
                CleanDirectoryContents(Path.Combine(winDir, "Panther"), "*.log", true, res, context);

                if (res.DeletedCount > 0 && res.FreedBytes == 0)
                {
                    res.FreedBytes = before.EstimatedBytes;
                }
                context.Log("[CLEANER] Windows Event Logs and CBS/Panther setup logs cleared.");
            }
            catch (Exception ex)
            {
                res.ErrorOrLockedCount++;
                res.Errors.Add(ex.Message);
            }
            return res;
        }
    }

    public class MuiCacheCleaner : BaseCleaner
    {
        private const string MuiRegistryPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";

        public MuiCacheCleaner()
            : base("mui_cache", "win_core", "Windows Core",
                   "Registry MUICache",
                   "Clears cached application names and executable strings in HKCU\\...\\Shell\\MuiCache",
                   "🗂️", false, false, "", false)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(MuiRegistryPath, false))
                {
                    if (key != null)
                    {
                        string[] names = key.GetValueNames();
                        foreach (string n in names)
                        {
                            res.FoundItems.Add("HKCU\\" + MuiRegistryPath + "\\" + n);
                            res.EstimatedBytes += 256;
                        }
                    }
                }
            }
            catch { res.SkippedOrLockedCount++; }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(MuiRegistryPath, true))
                {
                    if (key != null)
                    {
                        foreach (string valName in key.GetValueNames())
                        {
                            try
                            {
                                key.DeleteValue(valName, false);
                                res.DeletedCount++;
                                res.FreedBytes += 256;
                            }
                            catch { res.ErrorOrLockedCount++; }
                        }
                    }
                }
                context.Log(string.Format("[CLEANER] Registry MUICache cleared ({0} entries removed).", res.DeletedCount));
            }
            catch (Exception ex)
            {
                res.ErrorOrLockedCount++;
                res.Errors.Add(ex.Message);
            }
            return res;
        }
    }

    #endregion

    #region 2. WINDOWS EXPLORER & SHELL CLEANERS

    public class ExplorerRunMruCleaner : BaseCleaner
    {
        private const string RunMruPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";

        public ExplorerRunMruCleaner()
            : base("explorer_run_mru", "explorer_shell", "Windows Explorer & Shell",
                   "Run Dialog MRU History",
                   "Clears command history in the Windows Run (Win+R) dialog (HKCU\\...\\Explorer\\RunMRU)",
                   "⌨️", false, false, "", true)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunMruPath, false))
                {
                    if (key != null)
                    {
                        foreach (string valName in key.GetValueNames())
                        {
                            res.FoundItems.Add("HKCU\\" + RunMruPath + "\\" + valName);
                            res.EstimatedBytes += 256;
                        }
                    }
                }
            }
            catch { res.SkippedOrLockedCount++; }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunMruPath, true))
                {
                    if (key != null)
                    {
                        foreach (string valName in key.GetValueNames())
                        {
                            try
                            {
                                key.DeleteValue(valName, false);
                                res.DeletedCount++;
                                res.FreedBytes += 256;
                            }
                            catch { res.ErrorOrLockedCount++; }
                        }
                    }
                }
                context.Log(string.Format("[CLEANER] Explorer RunMRU history cleared ({0} entries).", res.DeletedCount));
            }
            catch (Exception ex)
            {
                res.ErrorOrLockedCount++;
                res.Errors.Add(ex.Message);
            }
            return res;
        }
    }

    public class ExplorerRecentDocsCleaner : BaseCleaner
    {
        private const string RecentDocsRegPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs";

        public ExplorerRecentDocsCleaner()
            : base("explorer_recent", "explorer_shell", "Windows Explorer & Shell",
                   "Recent Documents & Jump Lists",
                   "Clears %APPDATA%\\Microsoft\\Windows\\Recent shortcuts, Jump Lists, and RecentDocs registry",
                   "🕘", false, false, "", false)
        {
        }

        private static List<string> GetRecentDirs()
        {
            List<string> dirs = new List<string>();
            string recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (string.IsNullOrEmpty(recent))
            {
                recent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent");
            }
            dirs.Add(recent);
            dirs.Add(Path.Combine(recent, "AutomaticDestinations"));
            dirs.Add(Path.Combine(recent, "CustomDestinations"));
            return dirs;
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            foreach (string d in GetRecentDirs())
            {
                ScanDirectoryFiles(d, "*", false, res);
            }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            foreach (string d in GetRecentDirs())
            {
                CleanDirectoryContents(d, "*", false, res, context);
            }
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RecentDocsRegPath, false);
            }
            catch { }
            context.Log(string.Format("[CLEANER] Explorer Recent Documents cleared: {0} freed.", FormatBytes(res.FreedBytes)));
            return res;
        }
    }

    public class ExplorerShellbagsCleaner : BaseCleaner
    {
        private static readonly string[] ShellbagKeys = new string[]
        {
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags",
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU",
            @"Software\Microsoft\Windows\Shell\Bags",
            @"Software\Microsoft\Windows\Shell\BagMRU",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery"
        };

        public ExplorerShellbagsCleaner()
            : base("shellbags", "explorer_shell", "Windows Explorer & Shell",
                   "Explorer Shellbags & Search History",
                   "Clears folder view preferences, window size/position history (BagMRU/Bags), and Explorer search history",
                   "🗃️", false, false, "", false)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            foreach (string keyPath in ShellbagKeys)
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(keyPath, false))
                    {
                        if (k != null)
                        {
                            int count = k.SubKeyCount + k.ValueCount;
                            if (count > 0)
                            {
                                res.FoundItems.Add("HKCU\\" + keyPath);
                                res.EstimatedBytes += count * 256L;
                            }
                        }
                    }
                }
                catch { res.SkippedOrLockedCount++; }
            }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            ScanResult before = Scan(context);
            foreach (string keyPath in ShellbagKeys)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                    res.DeletedCount++;
                }
                catch { res.ErrorOrLockedCount++; }
            }
            res.FreedBytes = before.EstimatedBytes;
            context.Log("[CLEANER] Explorer Shellbags and WordWheelQuery search history cleared.");
            return res;
        }
    }

    #endregion

    #region 3. CHROMIUM BROWSERS (GOOGLE CHROME & MICROSOFT EDGE)

    public static class ChromiumHelper
    {
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open(byte[] filenameUtf8, out IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sqlUtf8, IntPtr callback, IntPtr arg, out IntPtr errMsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        public static List<string> GetRunningChromiumBrowsers()
        {
            List<string> running = new List<string>();
            try
            {
                if (Process.GetProcessesByName("chrome").Length > 0)
                    running.Add("Google Chrome (chrome.exe)");
                if (Process.GetProcessesByName("msedge").Length > 0)
                    running.Add("Microsoft Edge (msedge.exe)");
            }
            catch { }
            return running;
        }

        public static List<string> GetBrowserUserDataRoots()
        {
            List<string> roots = new List<string>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeRoot = Path.Combine(localAppData, "Google", "Chrome", "User Data");
            string edgeRoot = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");

            if (Directory.Exists(chromeRoot)) roots.Add(chromeRoot);
            if (Directory.Exists(edgeRoot)) roots.Add(edgeRoot);
            return roots;
        }

        public static List<string> DiscoverAllProfiles()
        {
            List<string> profiles = new List<string>();
            foreach (string userDataDir in GetBrowserUserDataRoots())
            {
                try
                {
                    string defaultProfile = Path.Combine(userDataDir, "Default");
                    if (Directory.Exists(defaultProfile))
                    {
                        profiles.Add(defaultProfile);
                    }

                    foreach (string subDir in Directory.GetDirectories(userDataDir))
                    {
                        string folderName = Path.GetFileName(subDir);
                        if (folderName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "Guest Profile", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "System Profile", StringComparison.OrdinalIgnoreCase))
                        {
                            profiles.Add(subDir);
                        }
                    }
                }
                catch { }
            }
            return profiles;
        }

        public static bool IsValidSqliteFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
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

        /// <summary>
        /// Executes SQLite VACUUM on a database file using Windows native winsqlite3.dll (with sqlite3.exe fallback),
        /// verifying the SQLite header first and ensuring no corruption occurs.
        /// </summary>
        public static bool VacuumSqliteDatabase(string dbPath, out long bytesSaved, out string error)
        {
            bytesSaved = 0;
            error = "";

            if (!IsValidSqliteFile(dbPath))
            {
                error = "Not a valid or accessible SQLite database.";
                return false;
            }

            long sizeBefore = 0;
            try
            {
                sizeBefore = new FileInfo(dbPath).Length;
                // Ensure file is not locked by browser before vacuuming
                using (FileStream fs = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
            catch (Exception ex)
            {
                error = "Database is locked by a running process: " + ex.Message;
                return false;
            }

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
                        byte[] sqlBytes = Encoding.UTF8.GetBytes("VACUUM;\0");
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
                        else
                        {
                            error = "SQLite VACUUM returned code " + rcExec;
                            return false;
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
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return false;
        }

        public static bool ExecuteSql(string dbPath, string sql)
        {
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
                        byte[] sqlBytes = Encoding.UTF8.GetBytes(sql + "\0");
                        int rcExec = sqlite3_exec(db, sqlBytes, IntPtr.Zero, IntPtr.Zero, out errMsg);
                        return rcExec == 0;
                    }
                    finally
                    {
                        sqlite3_close(db);
                    }
                }
            }
            catch { }
            return false;
        }
    }

    public class ChromiumSubpathCleaner : BaseCleaner
    {
        private readonly string[] _profileRelativeDirs;
        private readonly string[] _profileRelativeFiles;
        private readonly string[] _userDataRelativeDirs;

        public ChromiumSubpathCleaner(
            string id,
            string name,
            string description,
            string icon,
            bool isDangerous,
            string warningMessage,
            bool isRecommended,
            string[] profileRelativeDirs,
            string[] profileRelativeFiles,
            string[] userDataRelativeDirs = null)
            : base(id, "chromium", "Chromium Browsers", name, description, icon, false, isDangerous, warningMessage, isRecommended)
        {
            _profileRelativeDirs = profileRelativeDirs;
            _profileRelativeFiles = profileRelativeFiles;
            _userDataRelativeDirs = userDataRelativeDirs;
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers();
            if (running.Count > 0)
            {
                res.Warnings.Add("Running browsers detected: " + string.Join(", ", running.ToArray()));
            }

            if (_userDataRelativeDirs != null)
            {
                foreach (string root in ChromiumHelper.GetBrowserUserDataRoots())
                {
                    foreach (string relDir in _userDataRelativeDirs)
                    {
                        ScanDirectoryFiles(Path.Combine(root, relDir), "*", true, res);
                    }
                }
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles())
            {
                if (_profileRelativeDirs != null)
                {
                    foreach (string relDir in _profileRelativeDirs)
                    {
                        ScanDirectoryFiles(Path.Combine(profileDir, relDir), "*", true, res);
                    }
                }

                if (_profileRelativeFiles != null)
                {
                    foreach (string relFile in _profileRelativeFiles)
                    {
                        string dirPart = Path.GetDirectoryName(relFile);
                        string filePattern = Path.GetFileName(relFile);
                        string targetFolder = string.IsNullOrEmpty(dirPart) ? profileDir : Path.Combine(profileDir, dirPart);
                        ScanDirectoryFiles(targetFolder, filePattern, false, res);
                    }
                }
            }

            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers();
            if (running.Count > 0)
            {
                context.Log("[CLEANER WARNING] Browser process active (" + string.Join(", ", running.ToArray()) + "). Locked files will be skipped safely.");
            }

            if (_userDataRelativeDirs != null)
            {
                foreach (string root in ChromiumHelper.GetBrowserUserDataRoots())
                {
                    foreach (string relDir in _userDataRelativeDirs)
                    {
                        CleanDirectoryContents(Path.Combine(root, relDir), "*", true, res, context);
                    }
                }
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles())
            {
                if (_profileRelativeDirs != null)
                {
                    foreach (string relDir in _profileRelativeDirs)
                    {
                        CleanDirectoryContents(Path.Combine(profileDir, relDir), "*", true, res, context);
                    }
                }

                if (_profileRelativeFiles != null)
                {
                    foreach (string relFile in _profileRelativeFiles)
                    {
                        string dirPart = Path.GetDirectoryName(relFile);
                        string filePattern = Path.GetFileName(relFile);
                        string targetFolder = string.IsNullOrEmpty(dirPart) ? profileDir : Path.Combine(profileDir, dirPart);
                        CleanDirectoryContents(targetFolder, filePattern, false, res, context);
                    }
                }
            }

            context.Log(string.Format("[CLEANER] {0}: Freed {1} ({2} deleted, {3} locked/skipped).",
                Name, FormatBytes(res.FreedBytes), res.DeletedCount, res.ErrorOrLockedCount));
            return res;
        }
    }

    public class ChromiumSqliteVacuumCleaner : BaseCleaner
    {
        private static readonly string[] SqliteTargets = new string[]
        {
            "History",
            "Web Data",
            "Favicons",
            "Top Sites",
            @"Network\Cookies",
            "Cookies"
        };

        public ChromiumSqliteVacuumCleaner()
            : base("chromium_vacuum", "chromium", "Chromium Browsers",
                   "Browser SQLite Database Vacuum",
                   "Executes SQLite 'VACUUM;' on Chrome & Edge databases (History, Web Data, Favicons, Cookies) to defragment and compact without data loss",
                   "🗜️", false, false, "", true)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles())
            {
                foreach (string relDb in SqliteTargets)
                {
                    string dbPath = Path.Combine(profileDir, relDb);
                    if (ChromiumHelper.IsValidSqliteFile(dbPath))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(dbPath);
                            res.FoundItems.Add(dbPath);
                            // Estimate ~10% reclaimable fragmentation page space on non-vacuumed DBs
                            res.EstimatedBytes += Math.Max(4096L, fi.Length / 10L);
                        }
                        catch { res.SkippedOrLockedCount++; }
                    }
                }
            }
            return res;
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers();
            if (running.Count > 0)
            {
                string warn = "Cannot vacuum locked SQLite databases while browser is open: " + string.Join(", ", running.ToArray());
                context.Log("[CLEANER WARNING] " + warn);
                res.Errors.Add(warn);
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles())
            {
                foreach (string relDb in SqliteTargets)
                {
                    string dbPath = Path.Combine(profileDir, relDb);
                    if (File.Exists(dbPath))
                    {
                        long saved;
                        string err;
                        if (ChromiumHelper.VacuumSqliteDatabase(dbPath, out saved, out err))
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
            }

            context.Log(string.Format("[CLEANER] Browser SQLite Vacuum completed: {0} compacted across {1} databases ({2} locked/skipped).",
                FormatBytes(res.FreedBytes), res.DeletedCount, res.ErrorOrLockedCount));
            return res;
        }
    }

    #endregion

    #region 4. DEEP SCAN CLEANERS (DEVELOPER & ORPHANED JUNK)

    public class DeepScanDirectoryCleaner : BaseCleaner
    {
        private readonly HashSet<string> _targetFolderNames;

        public DeepScanDirectoryCleaner(
            string id,
            string name,
            string description,
            string icon,
            string warningMessage,
            string[] folderNames)
            : base(id, "deep_scan", "Deep Scan", name, description, icon, false, true, warningMessage, false)
        {
            _targetFolderNames = new HashSet<string>(folderNames, StringComparer.OrdinalIgnoreCase);
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            string root = context.DeepScanRootPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return res;

            ScanRecursive(root, 0, context.DeepScanMaxDepth, res);
            return res;
        }

        private void ScanRecursive(string currentDir, int depth, int maxDepth, ScanResult res)
        {
            if (depth > maxDepth) return;

            try
            {
                string[] subDirs = Directory.GetDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string sub in subDirs)
                {
                    string folderName = Path.GetFileName(sub);
                    if (_targetFolderNames.Contains(folderName))
                    {
                        if (PathSanitizer.IsSafeToDelete(sub))
                        {
                            res.FoundItems.Add(sub);
                            res.EstimatedBytes += GetDirectorySizeSafe(sub);
                        }
                        // Do not recurse inside a matched node_modules / venv folder
                        continue;
                    }

                    if (!PathSanitizer.IsExcludedDeepScanDirectory(sub))
                    {
                        ScanRecursive(sub, depth + 1, maxDepth, res);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                res.SkippedOrLockedCount++;
            }
            catch (IOException)
            {
                res.SkippedOrLockedCount++;
            }
            catch { }
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            ScanResult scanRes = Scan(context);

            foreach (string matchedDir in scanRes.FoundItems)
            {
                DeleteEntireDirectorySafe(matchedDir, res, context);
            }

            context.Log(string.Format("[DEEP SCAN] {0}: Freed {1} across {2} directories ({3} errors/locked).",
                Name, FormatBytes(res.FreedBytes), scanRes.FoundItems.Count, res.ErrorOrLockedCount));
            return res;
        }
    }

    public class DeepScanOrphanFilesCleaner : BaseCleaner
    {
        private static readonly HashSet<string> ExactFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".DS_Store",
            "Thumbs.db"
        };

        private static readonly string[] Extensions = new string[]
        {
            ".swp",
            ".swo",
            ".tmp"
        };

        public DeepScanOrphanFilesCleaner()
            : base("deep_junk_files", "deep_scan", "Deep Scan",
                   "Orphaned Metadata & Editor Swap Files",
                   "Deep scans for .DS_Store, Thumbs.db, and Vim swap/temp files (*.swp, *.swo, *.tmp) within the selected root folder",
                   "🔎", false, false, "", false)
        {
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            string root = context.DeepScanRootPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return res;

            ScanFilesRecursive(root, 0, context.DeepScanMaxDepth, res);
            return res;
        }

        private void ScanFilesRecursive(string currentDir, int depth, int maxDepth, ScanResult res)
        {
            if (depth > maxDepth) return;

            try
            {
                string[] files = Directory.GetFiles(currentDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    string fileName = Path.GetFileName(f);
                    string ext = Path.GetExtension(f);

                    bool match = ExactFileNames.Contains(fileName);
                    if (!match && !string.IsNullOrEmpty(ext))
                    {
                        foreach (string targetExt in Extensions)
                        {
                            if (string.Equals(ext, targetExt, StringComparison.OrdinalIgnoreCase))
                            {
                                match = true;
                                break;
                            }
                        }
                    }

                    if (match && PathSanitizer.IsSafeToDelete(f))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            res.FoundItems.Add(f);
                            res.EstimatedBytes += fi.Length;
                        }
                        catch { res.SkippedOrLockedCount++; }
                    }
                }

                string[] subDirs = Directory.GetDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string sub in subDirs)
                {
                    if (!PathSanitizer.IsExcludedDeepScanDirectory(sub))
                    {
                        ScanFilesRecursive(sub, depth + 1, maxDepth, res);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                res.SkippedOrLockedCount++;
            }
            catch (IOException)
            {
                res.SkippedOrLockedCount++;
            }
            catch { }
        }

        public override CleanResult Clean(CleanerContext context)
        {
            CleanResult res = new CleanResult() { CleanerId = Id };
            ScanResult scanRes = Scan(context);
            foreach (string file in scanRes.FoundItems)
            {
                DeleteFileSafe(file, res);
            }

            context.Log(string.Format("[DEEP SCAN] {0}: Freed {1} ({2} files deleted, {3} locked/skipped).",
                Name, FormatBytes(res.FreedBytes), res.DeletedCount, res.ErrorOrLockedCount));
            return res;
        }
    }

    #endregion

    #region 5. CLEANER REGISTRY / FACTORY

    public static class CleanerRegistry
    {
        public static List<BaseCleaner> CreateAllCleaners(Func<string, string, string> localizer)
        {
            Func<string, string, string> T = localizer ?? ((k, d) => d);
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            List<BaseCleaner> list = new List<BaseCleaner>();

            // =================================================================
            // CATEGORY 1: WINDOWS CORE
            // =================================================================
            list.Add(new DirectoryPatternCleaner(
                "user_temp", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_UserTemp", "User Temporary Files (%TEMP%)"),
                T("Cleaner_Desc_UserTemp", "Temporary files and caches in %TEMP% and local app temp folders"),
                "📁", false, false, "", true,
                () => new List<string>() { Path.GetTempPath(), Path.Combine(localAppData, "Temp") }));

            list.Add(new DirectoryPatternCleaner(
                "system_temp", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_SystemTemp", "Windows System Temp (C:\\Windows\\Temp)"),
                T("Cleaner_Desc_SystemTemp", "Temporary system files located in C:\\Windows\\Temp"),
                "📁", true, false, "", true,
                () => new List<string>() { Path.Combine(winDir, "Temp") }));

            list.Add(new DirectoryPatternCleaner(
                "win_update", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_WinUpdate", "Windows Update & Delivery Cache"),
                T("Cleaner_Desc_WinUpdate", "Downloaded Windows Update packages in SoftwareDistribution\\Download and DeliveryOptimization"),
                "🔄", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(winDir, "SoftwareDistribution", "Download"),
                    Path.Combine(winDir, "SoftwareDistribution", "DeliveryOptimization"),
                    Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache")
                }));

            list.Add(new DirectoryPatternCleaner(
                "memory_dumps", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_MemoryDumps", "Memory Dumps & BSOD Reports (*.dmp)"),
                T("Cleaner_Desc_MemoryDumps", "Crash memory dumps (MEMORY.DMP), Minidump, LiveKernelReports, and user CrashDumps"),
                "💥", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(winDir, "Minidump"),
                    Path.Combine(winDir, "LiveKernelReports"),
                    Path.Combine(localAppData, "CrashDumps")
                },
                "*", true,
                new List<string>() { Path.Combine(winDir, "MEMORY.DMP") }));

            list.Add(new DirectoryPatternCleaner(
                "error_reports", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_ErrorReports", "Windows Error Reporting (WER)"),
                T("Cleaner_Desc_ErrorReports", "Archived crash reports, mini-dumps, and error reporting queues"),
                "📊", false, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue"),
                    Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue")
                }));

            list.Add(new DirectoryPatternCleaner(
                "prefetch", "win_core", T("Cleaner_Cat_WinCore", "Windows Core"),
                T("Cleaner_Item_Prefetch", "Windows Prefetch (C:\\Windows\\Prefetch)"),
                T("Cleaner_Desc_Prefetch", "Application pre-launch cache files (*.pf) in C:\\Windows\\Prefetch"),
                "🚀", true, false, "", false,
                () => new List<string>() { Path.Combine(winDir, "Prefetch") }, "*.pf", false));

            list.Add(new RecycleBinCleaner()
            {
                Name = T("Cleaner_Item_RecycleBin", "Empty Recycle Bin"),
                Description = T("Cleaner_Desc_RecycleBin", "Permanently deletes all files from recycle bins on all connected drives via SHEmptyRecycleBin")
            });
            list.Add(new ClipboardCleaner()
            {
                Name = T("Cleaner_Item_Clipboard", "Windows Clipboard"),
                Description = T("Cleaner_Desc_Clipboard", "Clears all currently copied text, images, and data from system memory clipboard")
            });
            list.Add(new DnsCacheCleaner()
            {
                Name = T("Cleaner_Item_DnsCache", "DNS Resolver Cache"),
                Description = T("Cleaner_Desc_DnsCache", "Flushes the local DNS resolver cache via DnsFlushResolverCache API and ipconfig /flushdns")
            });
            list.Add(new MuiCacheCleaner()
            {
                Name = T("Cleaner_Item_MuiCache", "Registry MUICache"),
                Description = T("Cleaner_Desc_MuiCache", "Clears cached application names and executable strings in HKCU\\...\\Shell\\MuiCache")
            });
            list.Add(new EventLogsCleaner()
            {
                Name = T("Cleaner_Item_SystemLogs", "System Event & Setup Logs"),
                Description = T("Cleaner_Desc_SystemLogs", "Clears Windows Event Logs (Application, System, Security, Setup) and CBS/Panther logs")
            });

            // =================================================================
            // CATEGORY 2: WINDOWS EXPLORER & SHELL
            // =================================================================
            list.Add(new DirectoryPatternCleaner(
                "thumbnails", "explorer_shell", T("Cleaner_Cat_Explorer", "Windows Explorer & Shell"),
                T("Cleaner_Item_Thumbnails", "Thumbnail Cache (thumbcache_*.db)"),
                T("Cleaner_Desc_Thumbnails", "Explorer thumbnail cache databases in %LOCALAPPDATA%\\Microsoft\\Windows\\Explorer"),
                "🖼️", false, false, "", true,
                () => new List<string>() { Path.Combine(localAppData, "Microsoft", "Windows", "Explorer") },
                "thumbcache_*.db", false));

            list.Add(new ExplorerRunMruCleaner()
            {
                Name = T("Cleaner_Item_RunMru", "Run Dialog MRU History"),
                Description = T("Cleaner_Desc_RunMru", "Clears command history in the Windows Run (Win+R) dialog (HKCU\\...\\Explorer\\RunMRU)")
            });
            list.Add(new ExplorerRecentDocsCleaner()
            {
                Name = T("Cleaner_Item_RecentDocs", "Recent Documents & Jump Lists"),
                Description = T("Cleaner_Desc_RecentDocs", "Clears %APPDATA%\\Microsoft\\Windows\\Recent shortcuts, Jump Lists, and RecentDocs registry")
            });
            list.Add(new ExplorerShellbagsCleaner()
            {
                Name = T("Cleaner_Item_Shellbags", "Explorer Shellbags & Search History"),
                Description = T("Cleaner_Desc_Shellbags", "Clears folder view preferences, window size/position history (BagMRU/Bags), and Explorer search history")
            });

            // =================================================================
            // CATEGORY 3: CHROMIUM BROWSERS (CHROME & EDGE)
            // =================================================================
            list.Add(new ChromiumSubpathCleaner(
                "chromium_cache",
                T("Cleaner_Item_ChromCache", "Chromium Network & GPU Cache (Chrome & Edge)"),
                T("Cleaner_Desc_ChromCache", "Clears Cache_Data, Code Cache, GPUCache, ShaderCache, and Service Worker Cache across all profiles"),
                "🌐", false, "", true,
                new string[] { @"Cache\Cache_Data", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "DawnCache", "GrShaderCache", "ShaderCache" },
                null,
                new string[] { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" }));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_crash_reports",
                T("Cleaner_Item_ChromCrash", "Browser Crashpad & Crash Reports"),
                T("Cleaner_Desc_ChromCrash", "Deletes diagnostic crash dumps in Crashpad\\reports and pending queues for Chrome and Edge"),
                "🚑", false, "", true,
                null, null,
                new string[] { @"Crashpad\reports", @"Crashpad\completed", @"Crashpad\pending" }));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_ai_models",
                T("Cleaner_Item_ChromAiModels", "Browser Local AI Models (Gemini Nano / On-Device AI)"),
                T("Cleaner_Desc_ChromAiModels", "Removes downloaded local AI model weights (OptGuideOnDeviceModel & optimization_guide_model_store)"),
                "🤖", false, "", false,
                new string[] { "optimization_guide_model_store", "optimization_guide_prediction_model_downloads" },
                null,
                new string[] { "OptGuideOnDeviceModel" }));

            list.Add(new ChromiumSqliteVacuumCleaner()
            {
                Name = T("Cleaner_Item_ChromVacuum", "Browser SQLite Database Vacuum"),
                Description = T("Cleaner_Desc_ChromVacuum", "Executes SQLite 'VACUUM;' on Chrome & Edge databases (History, Web Data, Favicons, Cookies) to defragment and compact without data loss")
            });

            list.Add(new ChromiumSubpathCleaner(
                "chromium_cookies",
                T("Cleaner_Item_ChromCookies", "Browser Cookies (Chrome & Edge)"),
                T("Cleaner_Desc_ChromCookies", "Deletes browser cookies across all profiles (will sign you out of websites)"),
                "🍪", true,
                "Cleaning Cookies will sign you out of active web sessions.",
                false,
                null,
                new string[] { "Cookies", "Cookies-journal", @"Network\Cookies", @"Network\Cookies-journal" }));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_history",
                T("Cleaner_Item_ChromHistory", "Browsing & Download History"),
                T("Cleaner_Desc_ChromHistory", "Deletes browsing history, download logs, visited links, and top sites across all profiles"),
                "📜", true,
                "Cleaning Browsing History permanently removes your visited URLs and download history.",
                false,
                null,
                new string[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal" }));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_form_data",
                T("Cleaner_Item_ChromFormData", "Autofill Form Data (Web Data)"),
                T("Cleaner_Desc_ChromFormData", "Clears saved autofill form entries and search suggestions"),
                "📝", true,
                "Cleaning Form Data removes saved autofill text entries in Chrome and Edge.",
                false,
                null,
                new string[] { "Web Data", "Web Data-journal" }));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_site_data",
                T("Cleaner_Item_ChromSiteData", "Site Data (LocalStorage & IndexedDB)"),
                T("Cleaner_Desc_ChromSiteData", "Clears HTML5 LocalStorage, IndexedDB, Session Storage, and File System databases"),
                "🗄️", true,
                "Cleaning Site Data deletes offline web app storage (LocalStorage/IndexedDB).",
                false,
                new string[] { @"Local Storage\leveldb", "IndexedDB", "Session Storage", "File System" },
                null));

            list.Add(new ChromiumSubpathCleaner(
                "chromium_sessions",
                T("Cleaner_Item_ChromSessions", "Saved Browser Sessions & Open Tabs"),
                T("Cleaner_Desc_ChromSessions", "Deletes saved session state and last open tabs in Chrome and Edge"),
                "🗂️", true,
                "Cleaning Sessions will clear your previously open browser tabs.",
                false,
                new string[] { "Sessions" },
                new string[] { "Current Session", "Current Tabs", "Last Session", "Last Tabs" }));

            // =================================================================
            // CATEGORY 4: WINDOWS DEFENDER
            // =================================================================
            list.Add(new DirectoryPatternCleaner(
                "defender_temp", "defender", T("Cleaner_Cat_Defender", "Windows Defender"),
                T("Cleaner_Item_DefenderTemp", "Windows Defender Scan Logs & Support Temp"),
                T("Cleaner_Desc_DefenderTemp", "Temporary detection histories, operational support logs, and engine cache in ProgramData"),
                "🛡️", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Service", "DetectionHistory"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Support"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "MetaStore")
                }));

            list.Add(new DirectoryPatternCleaner(
                "defender_quarantine", "defender", T("Cleaner_Cat_Defender", "Windows Defender"),
                T("Cleaner_Item_DefenderQuarantine", "Windows Defender Quarantine Files"),
                T("Cleaner_Desc_DefenderQuarantine", "Deletes quarantined malware/threat files in C:\\ProgramData\\Microsoft\\Windows Defender\\Quarantine"),
                "☣️", true, true,
                "Deleting Quarantined files permanently removes isolated items so they can no longer be restored.",
                false,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Quarantine", "ResourceData"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Quarantine", "Entries")
                }));

            // =================================================================
            // CATEGORY 5: DEEP SCAN (DEVELOPER & ORPHANED FILES)
            // =================================================================
            list.Add(new DeepScanOrphanFilesCleaner()
            {
                Name = T("Cleaner_Item_DeepOrphans", "Orphaned Metadata & Editor Swap Files"),
                Description = T("Cleaner_Desc_DeepOrphans", "Deep scans for .DS_Store, Thumbs.db, and Vim swap/temp files (*.swp, *.swo, *.tmp) within the selected root folder")
            });

            list.Add(new DeepScanDirectoryCleaner(
                "deep_node_modules",
                T("Cleaner_Item_DeepNode", "Node.js & Angular Cache (node_modules / .angular)"),
                T("Cleaner_Desc_DeepNode", "Finds and removes heavy node_modules and .angular build folders inside the Deep Scan root path"),
                "📦",
                "Deleting node_modules or .angular folders requires running 'npm install' again in those projects.",
                new string[] { "node_modules", ".angular" }));

            list.Add(new DeepScanDirectoryCleaner(
                "deep_python_cache",
                T("Cleaner_Item_DeepPython", "Python Bytecode & VirtualEnvs (__pycache__ / venv)"),
                T("Cleaner_Desc_DeepPython", "Finds and removes __pycache__ and Python virtual environments (venv / .venv) in the Deep Scan root path"),
                "🐍",
                "Deleting 'venv' or '.venv' directories removes installed Python packages for those virtual environments.",
                new string[] { "__pycache__", "venv", ".venv" }));

            return list;
        }
    }

    #endregion

    #region 6. OPTIASSIST ADDON UI & CONTROLLER

    /// <summary>
    /// BleachBit-inspired System Cleaner & Optimizer Add-on Module compiled into SystemCleaner.dll.
    /// </summary>
    public class SystemCleanerAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ObservableCollection<BaseCleaner> _allCleaners;
        private ObservableCollection<BaseCleaner> _visibleCleaners;
        private string _activeCategoryFilter = "all";
        private bool _isBusy = false;

        // UI Controls
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnAnalyze;
        private Button _btnClean;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRecommended;
        private TextBlock _lblDeepScanPath;
        private TextBox _txtDeepScanRoot;
        private Button _btnBrowseDeepScan;
        private TextBlock _lblStatus;
        private TextBlock _lblTotalSize;
        private ProgressBar _pbProgress;
        private Dictionary<string, Button> _categoryButtons;

        public string Id { get { return "system-cleaner"; } }
        public string Name { get { return T("Nav_SystemCleaner", "System Cleaner"); } }
        public string Description { get { return T("Cleaner_Subtitle", "BleachBit-style modular dry-run analysis and safe cleaning for Windows, Browsers, Defender, and Developer caches"); } }
        public string Version { get { return "1.1.0.0"; } }
        public string Author { get { return "João Figueiró"; } }
        public string Icon { get { return "🧹"; } }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            InitCleaners();
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
            List<BaseCleaner> list = CleanerRegistry.CreateAllCleaners(T);
            _allCleaners = new ObservableCollection<BaseCleaner>(list);
            _visibleCleaners = new ObservableCollection<BaseCleaner>(list);
            ApplyLocalizedMetadata();
        }

        public FrameworkElement CreateView()
        {
            if (_allCleaners == null)
            {
                InitCleaners();
            }

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(0);

            // 1. Header Section
            StackPanel headerPanel = new StackPanel();
            headerPanel.Margin = new Thickness(0, 0, 0, 8);

            _txtTitle = new TextBlock()
            {
                Text = T("Cleaner_Title", "System Cleaner & Optimizer"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");

            _txtSubtitle = new TextBlock()
            {
                Text = T("Cleaner_Subtitle", "Dry-run scan and safe cleaning of OS caches, Chromium browsers (with SQLite Vacuum), Explorer, Defender, and Deep Scan"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");

            headerPanel.Children.Add(_txtTitle);
            headerPanel.Children.Add(_txtSubtitle);
            root.Children.Add(headerPanel);

            // 2. Main Action Toolbar Card
            Border actionCard = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            actionCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            actionCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel actionStack = new StackPanel();

            Grid topBarGrid = new Grid();
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel leftBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnAnalyze = new Button()
            {
                Content = T("Btn_CleanerAnalyze", "🔍 Scan (Dry-Run)"),
                Padding = new Thickness(14, 0, 14, 0),
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnAnalyze.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnAnalyze.Click += BtnAnalyze_Click;

            _btnClean = new Button()
            {
                Content = T("Btn_CleanerClean", "🧹 Clean Selected"),
                Padding = new Thickness(16, 0, 16, 0),
                Height = 32
            };
            _btnClean.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnClean.Click += BtnClean_Click;

            leftBtns.Children.Add(_btnAnalyze);
            leftBtns.Children.Add(_btnClean);
            Grid.SetColumn(leftBtns, 0);
            topBarGrid.Children.Add(leftBtns);

            StackPanel rightBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnSelectAll = new Button()
            {
                Content = T("Btn_CleanerSelectAll", "Select All"),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 28,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 11
            };
            _btnSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectAll.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _visibleCleaners) c.IsSelected = true;
                UpdateSelectedSummary();
            };

            _btnDeselectAll = new Button()
            {
                Content = T("Btn_CleanerDeselectAll", "Deselect All"),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 28,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 11
            };
            _btnDeselectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeselectAll.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _visibleCleaners) c.IsSelected = false;
                UpdateSelectedSummary();
            };

            _btnSelectRecommended = new Button()
            {
                Content = T("Btn_CleanerSelectRecommended", "Safe Recommended"),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 28,
                FontSize = 11
            };
            _btnSelectRecommended.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectRecommended.Click += (s, e) =>
            {
                foreach (BaseCleaner c in _allCleaners) c.IsSelected = c.IsRecommended && !c.IsDangerous;
                UpdateSelectedSummary();
            };

            rightBtns.Children.Add(_btnSelectAll);
            rightBtns.Children.Add(_btnDeselectAll);
            rightBtns.Children.Add(_btnSelectRecommended);
            Grid.SetColumn(rightBtns, 1);
            topBarGrid.Children.Add(rightBtns);

            actionStack.Children.Add(topBarGrid);

            // Category Filter Tabs Row
            WrapPanel catTabs = new WrapPanel() { Margin = new Thickness(0, 8, 0, 0) };
            _categoryButtons = new Dictionary<string, Button>();

            AddCategoryFilterButton(catTabs, "all", T("Cleaner_Cat_All", "All Modules"));
            AddCategoryFilterButton(catTabs, "win_core", "🪟 " + T("Cleaner_Cat_WinCore", "Windows Core"));
            AddCategoryFilterButton(catTabs, "explorer_shell", "🗂️ " + T("Cleaner_Cat_Explorer", "Explorer & Shell"));
            AddCategoryFilterButton(catTabs, "chromium", "🌐 " + T("Cleaner_Cat_Chromium", "Chrome & Edge"));
            AddCategoryFilterButton(catTabs, "defender", "🛡️ " + T("Cleaner_Cat_Defender", "Windows Defender"));
            AddCategoryFilterButton(catTabs, "deep_scan", "🔎 " + T("Cleaner_Cat_DeepScan", "Deep Scan"));

            actionStack.Children.Add(catTabs);

            // Deep Scan Root Directory Configuration Row
            Grid deepScanGrid = new Grid() { Margin = new Thickness(0, 8, 0, 0) };
            deepScanGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            deepScanGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            deepScanGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _lblDeepScanPath = new TextBlock()
            {
                Text = T("Cleaner_Label_DeepScanRoot", "Deep Scan Root Directory:"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _lblDeepScanPath.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            Grid.SetColumn(_lblDeepScanPath, 0);

            _txtDeepScanRoot = new TextBox()
            {
                Height = 24,
                FontSize = 11,
                Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(_txtDeepScanRoot, 1);

            _btnBrowseDeepScan = new Button()
            {
                Content = "📁",
                Width = 30,
                Height = 24,
                Padding = new Thickness(0)
            };
            _btnBrowseDeepScan.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnBrowseDeepScan.Click += BtnBrowseDeepScan_Click;
            Grid.SetColumn(_btnBrowseDeepScan, 2);

            deepScanGrid.Children.Add(_lblDeepScanPath);
            deepScanGrid.Children.Add(_txtDeepScanRoot);
            deepScanGrid.Children.Add(_btnBrowseDeepScan);
            actionStack.Children.Add(deepScanGrid);

            actionCard.Child = actionStack;
            root.Children.Add(actionCard);

            // 3. Status & Progress Summary Card
            Border statusCard = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            statusCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            statusCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            StackPanel statusStack = new StackPanel();
            Grid statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _lblStatus = new TextBlock()
            {
                Text = T("Cleaner_Status_Ready", "Ready for dry-run scan or cleanup. Select desired modules below."),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _lblStatus.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            Grid.SetColumn(_lblStatus, 0);

            _lblTotalSize = new TextBlock()
            {
                Text = "0 B",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _lblTotalSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            Grid.SetColumn(_lblTotalSize, 1);

            statusGrid.Children.Add(_lblStatus);
            statusGrid.Children.Add(_lblTotalSize);

            _pbProgress = new ProgressBar()
            {
                Height = 4,
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed,
                IsIndeterminate = true
            };

            statusStack.Children.Add(statusGrid);
            statusStack.Children.Add(_pbProgress);
            statusCard.Child = statusStack;
            root.Children.Add(statusCard);

            // 4. Modules ItemsControl (2-column UniformGrid)
            ItemsControl icCleaners = new ItemsControl();
            icCleaners.ItemsSource = _visibleCleaners;

            FrameworkElementFactory panelFactory = new FrameworkElementFactory(typeof(UniformGrid));
            panelFactory.SetValue(UniformGrid.ColumnsProperty, 2);
            icCleaners.ItemsPanel = new ItemsPanelTemplate(panelFactory);

            root.Children.Add( BuildCleanersGridContainer() );

            UpdateCategoryButtonStyles();
            UpdateSelectedSummary();
            return root;
        }

        private UniformGrid _cardsUniformGrid;

        private UIElement BuildCleanersGridContainer()
        {
            _cardsUniformGrid = new UniformGrid() { Columns = 2 };
            RebuildCardsGrid();
            return _cardsUniformGrid;
        }

        private void RebuildCardsGrid()
        {
            if (_cardsUniformGrid == null || _visibleCleaners == null) return;
            _cardsUniformGrid.Children.Clear();
            foreach (BaseCleaner cleaner in _visibleCleaners)
            {
                _cardsUniformGrid.Children.Add(BuildCleanerItemCard(cleaner));
            }
        }

        private void AddCategoryFilterButton(WrapPanel parent, string catId, string title)
        {
            Button btn = new Button()
            {
                Content = title,
                Height = 25,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 5, 4),
                FontSize = 10,
                Tag = catId
            };
            btn.Click += (s, e) =>
            {
                _activeCategoryFilter = catId;
                FilterVisibleCleaners();
                UpdateCategoryButtonStyles();
            };
            _categoryButtons[catId] = btn;
            parent.Children.Add(btn);
        }

        private void FilterVisibleCleaners()
        {
            if (_allCleaners == null || _visibleCleaners == null) return;
            _visibleCleaners.Clear();
            foreach (BaseCleaner c in _allCleaners)
            {
                if (_activeCategoryFilter == "all" || string.Equals(c.CategoryId, _activeCategoryFilter, StringComparison.OrdinalIgnoreCase))
                {
                    _visibleCleaners.Add(c);
                }
            }
            RebuildCardsGrid();
        }

        private void UpdateCategoryButtonStyles()
        {
            if (_categoryButtons == null) return;
            foreach (KeyValuePair<string, Button> kvp in _categoryButtons)
            {
                kvp.Value.SetResourceReference(
                    Button.StyleProperty,
                    string.Equals(kvp.Key, _activeCategoryFilter, StringComparison.OrdinalIgnoreCase) ? "BtnAccent" : "BtnDefault");
            }
        }

        private UIElement BuildCleanerItemCard(BaseCleaner item)
        {
            Border card = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(3),
                DataContext = item
            };
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            CheckBox cb = new CheckBox()
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 8, 0)
            };
            cb.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay });
            cb.Checked += (s, e) => UpdateSelectedSummary();
            cb.Unchecked += (s, e) => UpdateSelectedSummary();
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

            StackPanel details = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            WrapPanel titleRow = new WrapPanel() { Orientation = Orientation.Horizontal };

            TextBlock txtIcon = new TextBlock() { FontSize = 12, Margin = new Thickness(0, 0, 5, 0) };
            txtIcon.SetBinding(TextBlock.TextProperty, new Binding("Icon"));

            TextBlock txtName = new TextBlock()
            {
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            txtName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            txtName.SetBinding(TextBlock.TextProperty, new Binding("Name"));

            titleRow.Children.Add(txtIcon);
            titleRow.Children.Add(txtName);

            if (item.RequiresAdmin)
            {
                TextBlock badgeAdmin = new TextBlock()
                {
                    Text = "🛡️ Admin",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 1, 5, 0),
                    ToolTip = "Requires Administrator privileges for full access"
                };
                badgeAdmin.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                titleRow.Children.Add(badgeAdmin);
            }

            if (item.IsDangerous)
            {
                TextBlock badgeWarn = new TextBlock()
                {
                    Text = "⚠️ Caution",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 1, 0, 0),
                    ToolTip = item.WarningMessage
                };
                badgeWarn.SetResourceReference(TextBlock.ForegroundProperty, "BrushWarning");
                titleRow.Children.Add(badgeWarn);
            }

            TextBlock txtDesc = new TextBlock()
            {
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            txtDesc.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            txtDesc.SetBinding(TextBlock.TextProperty, new Binding("Description"));

            details.Children.Add(titleRow);
            details.Children.Add(txtDesc);
            Grid.SetColumn(details, 1);
            grid.Children.Add(details);

            Border sizeBadge = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            sizeBadge.SetResourceReference(Border.BackgroundProperty, "BrushBtnBg");
            sizeBadge.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            TextBlock txtSize = new TextBlock()
            {
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            txtSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            txtSize.SetBinding(TextBlock.TextProperty, new Binding("SizeDisplay"));

            sizeBadge.Child = txtSize;
            Grid.SetColumn(sizeBadge, 2);
            grid.Children.Add(sizeBadge);

            card.Child = grid;
            return card;
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

        private void UpdateSelectedSummary()
        {
            if (_allCleaners == null) return;
            long totalBytes = 0;
            int count = 0;
            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected)
                {
                    count++;
                    totalBytes += c.SizeBytes;
                }
            }
            if (_lblTotalSize != null)
            {
                _lblTotalSize.Text = string.Format("{0} ({1})", BaseCleaner.FormatBytes(totalBytes), count);
            }
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _allCleaners == null) return;

            List<BaseCleaner> selected = new List<BaseCleaner>();
            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected) selected.Add(c);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to scan or clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CleanerContext ctx = CreateExecutionContext();

            _isBusy = true;
            _btnAnalyze.IsEnabled = false;
            _btnClean.IsEnabled = false;
            _pbProgress.Visibility = Visibility.Visible;
            _lblStatus.Text = T("Cleaner_Status_Scanning", "Running dry-run scan on selected modules...");

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFoundBytes = 0;
                int totalFoundItems = 0;
                int totalSkipped = 0;

                Log("=================================================");
                Log("   STARTING SYSTEM CLEANER DRY-RUN SCAN          ");
                Log(string.Format("   Selected modules: {0} | Elevated: {1}", selected.Count, ctx.IsProcessElevated));
                Log("=================================================");

                foreach (BaseCleaner cleaner in selected)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        cleaner.StatusDisplay = "Scanning...";
                        cleaner.SizeDisplay = "...";
                    }));

                    ScanResult sr = cleaner.Scan(ctx);
                    totalFoundBytes += sr.EstimatedBytes;
                    totalFoundItems += sr.FoundItems.Count;
                    totalSkipped += sr.SkippedOrLockedCount;

                    Log(string.Format("[SCAN] {0}: {1} ({2} items found, {3} skipped/locked)",
                        cleaner.Name, BaseCleaner.FormatBytes(sr.EstimatedBytes), sr.FoundItems.Count, sr.SkippedOrLockedCount));

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        cleaner.SizeBytes = sr.EstimatedBytes;
                        cleaner.FoundCount = sr.FoundItems.Count;
                        cleaner.StatusDisplay = sr.EstimatedBytes > 0 ? "Scanned" : "Clean";
                    }));
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    _btnAnalyze.IsEnabled = true;
                    _btnClean.IsEnabled = true;
                    _pbProgress.Visibility = Visibility.Collapsed;

                    string summaryMsg = string.Format(
                        T("Cleaner_Status_Scanned", "Dry-run complete: Found {0} ({2} items) across {1} modules."),
                        BaseCleaner.FormatBytes(totalFoundBytes), selected.Count, totalFoundItems);

                    _lblStatus.Text = summaryMsg;
                    UpdateSelectedSummary();
                    Log("[SCAN COMPLETE] " + summaryMsg);
                }));
            });
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _allCleaners == null) return;

            List<BaseCleaner> selected = new List<BaseCleaner>();
            List<string> warnings = new List<string>();
            bool hasChromiumSelected = false;
            long estimatedSpace = 0;

            foreach (BaseCleaner c in _allCleaners)
            {
                if (c.IsSelected)
                {
                    selected.Add(c);
                    estimatedSpace += c.SizeBytes;
                    if (c.IsDangerous && !string.IsNullOrEmpty(c.WarningMessage))
                    {
                        warnings.Add("• " + c.Name + ": " + c.WarningMessage);
                    }
                    if (c.CategoryId == "chromium")
                    {
                        hasChromiumSelected = true;
                    }
                }
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Alert if Chrome or Edge is open when cleaning Chromium targets
            if (hasChromiumSelected)
            {
                List<string> runningBrowsers = ChromiumHelper.GetRunningChromiumBrowsers();
                if (runningBrowsers.Count > 0)
                {
                    string browserMsg = string.Format(
                        T("Msg_CleanerBrowserRunning", "The following browsers are currently running:\n• {0}\n\nOpen browser databases (Cookies, History, SQLite Vacuum) may be locked. Close the browser first for a complete cleanup, or click Yes to continue and skip locked files."),
                        string.Join("\n• ", runningBrowsers.ToArray()));
                    if (MessageBox.Show(browserMsg, "OptiAssist - Browser Running", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            StringBuilder confirmSb = new StringBuilder();
            confirmSb.AppendFormat(
                T("Msg_CleanerConfirm", "Do you want to clean the {1} selected modules now?\nEstimated reclaimable space: {0}"),
                BaseCleaner.FormatBytes(estimatedSpace), selected.Count);

            if (warnings.Count > 0)
            {
                confirmSb.AppendLine();
                confirmSb.AppendLine();
                confirmSb.AppendLine(T("Cleaner_Warn_Header", "⚠️ Sensitive / Caution Items Selected:"));
                foreach (string w in warnings)
                {
                    confirmSb.AppendLine(w);
                }
            }

            if (MessageBox.Show(confirmSb.ToString(), "OptiAssist - System Cleaner", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            CleanerContext ctx = CreateExecutionContext();

            _isBusy = true;
            _btnAnalyze.IsEnabled = false;
            _btnClean.IsEnabled = false;
            _pbProgress.Visibility = Visibility.Visible;

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFreed = 0;
                int totalDeleted = 0;
                int totalSkipped = 0;

                Log("=================================================");
                Log("   STARTING SYSTEM CLEANING & OPTIMIZATION       ");
                Log(string.Format("   Modules to clean: {0}", selected.Count));
                Log("=================================================");

                foreach (BaseCleaner cleaner in selected)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        _lblStatus.Text = string.Format(T("Cleaner_Status_Cleaning", "Cleaning {0}..."), cleaner.Name);
                        cleaner.StatusDisplay = "Cleaning...";
                    }));

                    CleanResult cr = cleaner.Clean(ctx);
                    totalFreed += cr.FreedBytes;
                    totalDeleted += cr.DeletedCount;
                    totalSkipped += cr.ErrorOrLockedCount;

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        cleaner.SizeBytes = 0;
                        cleaner.SizeDisplay = "Cleaned";
                        cleaner.StatusDisplay = "Cleaned";
                    }));
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    _btnAnalyze.IsEnabled = true;
                    _btnClean.IsEnabled = true;
                    _pbProgress.Visibility = Visibility.Collapsed;

                    string doneMsg = string.Format(
                        T("Cleaner_Status_Cleaned", "Cleaning completed: {0} freed across {1} modules! ({2} locked/skipped)"),
                        BaseCleaner.FormatBytes(totalFreed), selected.Count, totalSkipped);

                    _lblStatus.Text = doneMsg;
                    UpdateSelectedSummary();
                    Log("[CLEANER COMPLETE] " + doneMsg);

                    MessageBox.Show(doneMsg, "System Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            });
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_txtTitle != null) _txtTitle.Text = T("Cleaner_Title", "System Cleaner & Optimizer");
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Cleaner_Subtitle", "Dry-run scan and safe cleaning of OS caches, Chromium browsers (with SQLite Vacuum), Explorer, Defender, and Deep Scan");
            if (_btnAnalyze != null) _btnAnalyze.Content = T("Btn_CleanerAnalyze", "🔍 Scan (Dry-Run)");
            if (_btnClean != null) _btnClean.Content = T("Btn_CleanerClean", "🧹 Clean Selected");
            if (_btnSelectAll != null) _btnSelectAll.Content = T("Btn_CleanerSelectAll", "Select All");
            if (_btnDeselectAll != null) _btnDeselectAll.Content = T("Btn_CleanerDeselectAll", "Deselect All");
            if (_btnSelectRecommended != null) _btnSelectRecommended.Content = T("Btn_CleanerSelectRecommended", "Safe Recommended");
            if (_lblDeepScanPath != null) _lblDeepScanPath.Text = T("Cleaner_Label_DeepScanRoot", "Deep Scan Root Directory:");
            if (_lblStatus != null && !_isBusy) _lblStatus.Text = T("Cleaner_Status_Ready", "Ready for dry-run scan or cleanup. Select desired modules below.");

            if (_categoryButtons != null)
            {
                if (_categoryButtons.ContainsKey("all")) _categoryButtons["all"].Content = T("Cleaner_Cat_All", "All Modules");
                if (_categoryButtons.ContainsKey("win_core")) _categoryButtons["win_core"].Content = "🪟 " + T("Cleaner_Cat_WinCore", "Windows Core");
                if (_categoryButtons.ContainsKey("explorer_shell")) _categoryButtons["explorer_shell"].Content = "🗂️ " + T("Cleaner_Cat_Explorer", "Explorer & Shell");
                if (_categoryButtons.ContainsKey("chromium")) _categoryButtons["chromium"].Content = "🌐 " + T("Cleaner_Cat_Chromium", "Chrome & Edge");
                if (_categoryButtons.ContainsKey("defender")) _categoryButtons["defender"].Content = "🛡️ " + T("Cleaner_Cat_Defender", "Windows Defender");
                if (_categoryButtons.ContainsKey("deep_scan")) _categoryButtons["deep_scan"].Content = "🔎 " + T("Cleaner_Cat_DeepScan", "Deep Scan");
            }

            ApplyLocalizedMetadata();
            UpdateSelectedSummary();
        }

        private void ApplyLocalizedMetadata()
        {
            if (_allCleaners == null) return;

            // Rebuild localized strings by matching cleaner IDs against a freshly localized template list
            List<BaseCleaner> localizedTemplates = CleanerRegistry.CreateAllCleaners(T);
            Dictionary<string, BaseCleaner> map = new Dictionary<string, BaseCleaner>();
            foreach (BaseCleaner tmpl in localizedTemplates)
            {
                map[tmpl.Id] = tmpl;
            }

            foreach (BaseCleaner item in _allCleaners)
            {
                BaseCleaner tmpl;
                if (map.TryGetValue(item.Id, out tmpl))
                {
                    item.Name = tmpl.Name;
                    item.Description = tmpl.Description;
                    item.Category = tmpl.Category;
                    item.WarningMessage = tmpl.WarningMessage;
                }
            }
        }
    }

    #endregion
}
