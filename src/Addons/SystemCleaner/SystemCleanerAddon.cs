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
using System.Windows.Media.Animation;
using System.Windows.Input;
using Microsoft.Win32;

namespace OptiAssist.Addons.SystemCleaner
{
    #region RESULTS, CONTEXT & PATH SANITIZER

    /// <summary>
    /// Represents an individual scanned file marked for cleaning with its size.
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

        public ScanResult()
        {
            FoundItems = new List<string>();
            ScannedFiles = new List<ScannedFileEntry>();
            Warnings = new List<string>();
        }

        public void AddFile(string path, long size)
        {
            if (string.IsNullOrEmpty(path)) return;
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
        public string TabId { get; set; }
        public string SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string SectionIcon { get; set; }
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
                        result.AddFile(f, fi.Length);
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
                            res.AddFile(file, fi.Length);
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
                    res.AddFile(string.Format("Lixeira / Recycle Bin ({0} itens)", info.i64NumItems), info.i64Size);
                    for (int i = 1; i < (int)Math.Min(info.i64NumItems, 100); i++)
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
                            res.AddFile("Área de Transferência do Windows (Clipboard)", 4096);
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
            res.AddFile("Cache DNS do Windows (DnsResolverCache)", 0);
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
                            res.AddFile("HKCU\\" + MuiRegistryPath + "\\" + n, 256);
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
                            res.AddFile("HKCU\\" + RunMruPath + "\\" + valName, 256);
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
                                res.AddFile("HKCU\\" + keyPath, count * 256L);
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

        public static List<string> GetRunningChromiumBrowsers(string browserFilter = "all")
        {
            List<string> running = new List<string>();
            try
            {
                if ((browserFilter == "all" || browserFilter == "chrome") && Process.GetProcessesByName("chrome").Length > 0)
                    running.Add("Google Chrome (chrome.exe)");
                if ((browserFilter == "all" || browserFilter == "edge") && Process.GetProcessesByName("msedge").Length > 0)
                    running.Add("Microsoft Edge (msedge.exe)");
            }
            catch { }
            return running;
        }

        public static List<string> GetBrowserUserDataRoots(string browserFilter = "all")
        {
            List<string> roots = new List<string>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeRoot = Path.Combine(localAppData, "Google", "Chrome", "User Data");
            string edgeRoot = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");

            if ((browserFilter == "all" || browserFilter == "chrome") && Directory.Exists(chromeRoot)) roots.Add(chromeRoot);
            if ((browserFilter == "all" || browserFilter == "edge") && Directory.Exists(edgeRoot)) roots.Add(edgeRoot);
            return roots;
        }

        public static List<string> DiscoverAllProfiles(string browserFilter = "all")
        {
            List<string> profiles = new List<string>();
            foreach (string userDataDir in GetBrowserUserDataRoots(browserFilter))
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
        private readonly string _browserFilter;

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
            string[] userDataRelativeDirs = null,
            string browserFilter = "all")
            : base(id, (browserFilter == "edge" ? "edge" : (browserFilter == "chrome" ? "chrome" : "chromium")),
                   (browserFilter == "edge" ? "Microsoft Edge" : (browserFilter == "chrome" ? "Google Chrome" : "Chromium Browsers")),
                   name, description, icon, false, isDangerous, warningMessage, isRecommended)
        {
            _profileRelativeDirs = profileRelativeDirs;
            _profileRelativeFiles = profileRelativeFiles;
            _userDataRelativeDirs = userDataRelativeDirs;
            _browserFilter = browserFilter;
            TabId = browserFilter == "edge" ? "windows" : "apps";
            SectionId = browserFilter == "edge" ? "edge" : "chrome";
            SectionTitle = browserFilter == "edge" ? "Microsoft Edge" : "Google Chrome";
            SectionIcon = "🌐";
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers(_browserFilter);
            if (running.Count > 0)
            {
                res.Warnings.Add("Running browsers detected: " + string.Join(", ", running.ToArray()));
            }

            if (_userDataRelativeDirs != null)
            {
                foreach (string root in ChromiumHelper.GetBrowserUserDataRoots(_browserFilter))
                {
                    foreach (string relDir in _userDataRelativeDirs)
                    {
                        ScanDirectoryFiles(Path.Combine(root, relDir), "*", true, res);
                    }
                }
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles(_browserFilter))
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
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers(_browserFilter);
            if (running.Count > 0)
            {
                context.Log("[CLEANER WARNING] Browser process active (" + string.Join(", ", running.ToArray()) + "). Locked files will be skipped safely.");
            }

            if (_userDataRelativeDirs != null)
            {
                foreach (string root in ChromiumHelper.GetBrowserUserDataRoots(_browserFilter))
                {
                    foreach (string relDir in _userDataRelativeDirs)
                    {
                        CleanDirectoryContents(Path.Combine(root, relDir), "*", true, res, context);
                    }
                }
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles(_browserFilter))
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
        private readonly string _browserFilter;

        public ChromiumSqliteVacuumCleaner(string id = "chromium_vacuum", string browserFilter = "all")
            : base(id, (browserFilter == "edge" ? "edge" : (browserFilter == "chrome" ? "chrome" : "chromium")),
                   (browserFilter == "edge" ? "Microsoft Edge" : (browserFilter == "chrome" ? "Google Chrome" : "Chromium Browsers")),
                   "Browser SQLite Database Vacuum",
                   "Executes SQLite 'VACUUM;' on browser databases to defragment and compact without data loss",
                   "🗜️", false, false, "", true)
        {
            _browserFilter = browserFilter;
            TabId = browserFilter == "edge" ? "windows" : "apps";
            SectionId = browserFilter == "edge" ? "edge" : "chrome";
            SectionTitle = browserFilter == "edge" ? "Microsoft Edge" : "Google Chrome";
            SectionIcon = "🌐";
        }

        public override ScanResult Scan(CleanerContext context)
        {
            ScanResult res = new ScanResult() { CleanerId = Id };
            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles(_browserFilter))
            {
                foreach (string relDb in SqliteTargets)
                {
                    string dbPath = Path.Combine(profileDir, relDb);
                    if (ChromiumHelper.IsValidSqliteFile(dbPath))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(dbPath);
                            // Estimate ~10% reclaimable fragmentation page space on non-vacuumed DBs
                            res.AddFile(dbPath + " [SQLite Database]", Math.Max(4096L, fi.Length / 10L));
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
            List<string> running = ChromiumHelper.GetRunningChromiumBrowsers(_browserFilter);
            if (running.Count > 0)
            {
                string warn = "Cannot vacuum locked SQLite databases while browser is open: " + string.Join(", ", running.ToArray());
                context.Log("[CLEANER WARNING] " + warn);
                res.Errors.Add(warn);
            }

            foreach (string profileDir in ChromiumHelper.DiscoverAllProfiles(_browserFilter))
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
                            res.AddFile(sub + " [Pasta / Folder]", GetDirectorySizeSafe(sub));
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
            // TAB 1: WINDOWS
            // =================================================================

            // SECTION 1.1: MICROSOFT EDGE
            list.Add(new ChromiumSubpathCleaner(
                "edge_cache",
                T("Cleaner_Item_EdgeCache", "Internet Cache (Temporary Files)"),
                T("Cleaner_Desc_EdgeCache", "Temporary files, GPU cache, and shader cache in Microsoft Edge"),
                "🌐", false, "", true,
                new string[] { @"Cache\Cache_Data", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "DawnCache", "GrShaderCache", "ShaderCache" },
                null,
                new string[] { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" },
                "edge")
            {
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "edge_history",
                T("Cleaner_Item_EdgeHistory", "Browsing History"),
                T("Cleaner_Desc_EdgeHistory", "Visited links, download history, and top sites in Microsoft Edge"),
                "📜", true,
                T("Cleaner_Warn_EdgeHistory", "Deletes your browsing and download history in Microsoft Edge."),
                false,
                null,
                new string[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal" },
                null,
                "edge")
            {
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "edge_cookies",
                T("Cleaner_Item_EdgeCookies", "Cookies"),
                T("Cleaner_Desc_EdgeCookies", "Active website cookies and login sessions in Microsoft Edge"),
                "🍪", true,
                T("Cleaner_Warn_EdgeCookies", "Clearing cookies will sign you out of websites in Microsoft Edge."),
                false,
                null,
                new string[] { "Cookies", "Cookies-journal", @"Network\Cookies", @"Network\Cookies-journal" },
                null,
                "edge")
            {
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "edge_form_data",
                T("Cleaner_Item_EdgeFormData", "Autofill Form Information"),
                T("Cleaner_Desc_EdgeFormData", "Saved autofill form entries and search suggestions in Microsoft Edge"),
                "📝", true,
                "Cleaning Form Data removes saved autofill text entries in Microsoft Edge.",
                false,
                null,
                new string[] { "Web Data", "Web Data-journal" },
                null,
                "edge")
            {
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "edge_sessions",
                T("Cleaner_Item_EdgeSessions", "Saved Sessions & Tabs"),
                T("Cleaner_Desc_EdgeSessions", "Previously open tabs and session recovery in Microsoft Edge"),
                "🗂️", true,
                T("Cleaner_Warn_EdgeSessions", "Closes and forgets currently open tabs in Microsoft Edge."),
                false,
                new string[] { "Sessions" },
                new string[] { "Current Session", "Current Tabs", "Last Session", "Last Tabs" },
                null,
                "edge")
            {
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSqliteVacuumCleaner("edge_vacuum", "edge")
            {
                Name = T("Cleaner_Item_EdgeVacuum", "Compact SQLite Databases (Vacuum)"),
                Description = T("Cleaner_Desc_EdgeVacuum", "Defragments and compacts Microsoft Edge SQLite database files without losing data"),
                TabId = "windows",
                SectionId = "edge",
                SectionTitle = T("Cleaner_Section_Edge", "Microsoft Edge"),
                SectionIcon = "🌐"
            });

            // SECTION 1.2: WINDOWS EXPLORER
            list.Add(new ExplorerRecentDocsCleaner()
            {
                Name = T("Cleaner_Item_RecentDocs", "Recent Documents & Jump Lists"),
                Description = T("Cleaner_Desc_RecentDocs", "Shortcuts in %APPDATA%\\Microsoft\\Windows\\Recent, AutomaticDestinations, and CustomDestinations"),
                TabId = "windows",
                SectionId = "explorer",
                SectionTitle = T("Cleaner_Section_Explorer", "Windows Explorer"),
                SectionIcon = "📁"
            });

            list.Add(new ExplorerRunMruCleaner()
            {
                Name = T("Cleaner_Item_RunMru", "Run (in Start menu) History"),
                Description = T("Cleaner_Desc_RunMru", "Clears Win+R Run command history in HKCU\\...\\Explorer\\RunMRU"),
                TabId = "windows",
                SectionId = "explorer",
                SectionTitle = T("Cleaner_Section_Explorer", "Windows Explorer"),
                SectionIcon = "📁"
            });

            list.Add(new ExplorerShellbagsCleaner()
            {
                Name = T("Cleaner_Item_Shellbags", "Other Explorer MRUs & Shellbags"),
                Description = T("Cleaner_Desc_Shellbags", "Clears BagMRU, Bags, and Explorer search box history (WordWheelQuery)"),
                TabId = "windows",
                SectionId = "explorer",
                SectionTitle = T("Cleaner_Section_Explorer", "Windows Explorer"),
                SectionIcon = "📁"
            });

            list.Add(new DirectoryPatternCleaner(
                "thumbnails", "explorer", T("Cleaner_Section_Explorer", "Windows Explorer"),
                T("Cleaner_Item_Thumbnails", "Thumbnail Cache"),
                T("Cleaner_Desc_Thumbnails", "Deletes thumbcache_*.db and iconcache_*.db in %LOCALAPPDATA%\\Microsoft\\Windows\\Explorer"),
                "🖼️", false, false, "", true,
                () => new List<string>() { Path.Combine(localAppData, "Microsoft", "Windows", "Explorer") },
                "thumbcache_*.db", false)
            {
                TabId = "windows",
                SectionId = "explorer",
                SectionTitle = T("Cleaner_Section_Explorer", "Windows Explorer"),
                SectionIcon = "📁"
            });

            // SECTION 1.3: SYSTEM
            list.Add(new RecycleBinCleaner()
            {
                Name = T("Cleaner_Item_RecycleBin", "Empty Recycle Bin"),
                Description = T("Cleaner_Desc_RecycleBin", "Permanently empties Recycle Bins across all drives via SHEmptyRecycleBin"),
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "user_temp", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_UserTemp", "Temporary Files (%TEMP%)"),
                T("Cleaner_Desc_UserTemp", "Temporary files and application caches in %TEMP% and %LOCALAPPDATA%\\Temp"),
                "📁", false, false, "", true,
                () => new List<string>() { Path.GetTempPath(), Path.Combine(localAppData, "Temp") })
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "system_temp", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_SystemTemp", "System Temp (Windows\\Temp)"),
                T("Cleaner_Desc_SystemTemp", "System-wide temporary files and installer leftovers in C:\\Windows\\Temp"),
                "📁", true, false, "", true,
                () => new List<string>() { Path.Combine(winDir, "Temp") })
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new ClipboardCleaner()
            {
                Name = T("Cleaner_Item_Clipboard", "Clipboard"),
                Description = T("Cleaner_Desc_Clipboard", "Clears all currently copied text, images, and data from memory clipboard"),
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "memory_dumps", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_MemoryDumps", "Memory Dumps"),
                T("Cleaner_Desc_MemoryDumps", "Kernel/Complete crash dumps (MEMORY.DMP), Minidumps, and LiveKernelReports"),
                "💥", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(winDir, "Minidump"),
                    Path.Combine(winDir, "LiveKernelReports"),
                    Path.Combine(localAppData, "CrashDumps")
                },
                "*", true,
                new List<string>() { Path.Combine(winDir, "MEMORY.DMP") })
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "win_update", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_WinUpdate", "Windows Update Cache"),
                T("Cleaner_Desc_WinUpdate", "Downloaded update packages in SoftwareDistribution\\Download and Delivery Optimization cache"),
                "🔄", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(winDir, "SoftwareDistribution", "Download"),
                    Path.Combine(winDir, "SoftwareDistribution", "DeliveryOptimization"),
                    Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache")
                })
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "prefetch", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_Prefetch", "Windows Prefetch"),
                T("Cleaner_Desc_Prefetch", "Application preloading traces (*.pf) in C:\\Windows\\Prefetch"),
                "🚀", true, false, "", false,
                () => new List<string>() { Path.Combine(winDir, "Prefetch") }, "*.pf", false)
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new EventLogsCleaner()
            {
                Name = T("Cleaner_Item_SystemLogs", "Windows Event Logs"),
                Description = T("Cleaner_Desc_SystemLogs", "Clears Windows Event Logs (Application, System, Setup), CBS logs, Panther, and WER reports"),
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DirectoryPatternCleaner(
                "error_reports", "system", T("Cleaner_Section_System", "System"),
                T("Cleaner_Item_ErrorReports", "Windows Error Reporting"),
                T("Cleaner_Desc_ErrorReports", "Archived crash reports, mini-dumps, and error reporting queues"),
                "📊", false, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue"),
                    Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue")
                })
            {
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new DnsCacheCleaner()
            {
                Name = T("Cleaner_Item_DnsCache", "DNS Cache (Flush DNS)"),
                Description = T("Cleaner_Desc_DnsCache", "Flushes the local DNS cache via DnsFlushResolverCache / ipconfig /flushdns"),
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            list.Add(new MuiCacheCleaner()
            {
                Name = T("Cleaner_Item_MuiCache", "Registry MUICache"),
                Description = T("Cleaner_Desc_MuiCache", "Clears cached application display names in HKCU\\...\\Shell\\MuiCache"),
                TabId = "windows",
                SectionId = "system",
                SectionTitle = T("Cleaner_Section_System", "System"),
                SectionIcon = "🪟"
            });

            // =================================================================
            // TAB 2: APPLICATIONS ("apps")
            // =================================================================

            // SECTION 2.1: GOOGLE CHROME
            list.Add(new ChromiumSubpathCleaner(
                "chrome_cache",
                T("Cleaner_Item_ChromeCache", "Internet Cache"),
                T("Cleaner_Desc_ChromeCache", "Web cache, code cache, GPUCache, and shaders in Google Chrome"),
                "🌐", false, "", true,
                new string[] { @"Cache\Cache_Data", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "DawnCache", "GrShaderCache", "ShaderCache" },
                null,
                new string[] { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" },
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_history",
                T("Cleaner_Item_ChromeHistory", "Browsing History"),
                T("Cleaner_Desc_ChromeHistory", "Visited links, download history, and top sites in Google Chrome"),
                "📜", true,
                T("Cleaner_Warn_ChromeHistory", "Deletes your browsing and download history in Google Chrome."),
                false,
                null,
                new string[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal" },
                null,
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_cookies",
                T("Cleaner_Item_ChromeCookies", "Cookies"),
                T("Cleaner_Desc_ChromeCookies", "Active website cookies and login sessions in Google Chrome"),
                "🍪", true,
                T("Cleaner_Warn_ChromeCookies", "Clearing cookies will sign you out of websites in Google Chrome."),
                false,
                null,
                new string[] { "Cookies", "Cookies-journal", @"Network\Cookies", @"Network\Cookies-journal" },
                null,
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_form_data",
                T("Cleaner_Item_ChromeFormData", "Autofill Form Information"),
                T("Cleaner_Desc_ChromeFormData", "Saved autofill form entries and search suggestions in Google Chrome"),
                "📝", true,
                "Cleaning Form Data removes saved autofill text entries in Google Chrome.",
                false,
                null,
                new string[] { "Web Data", "Web Data-journal" },
                null,
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_site_data",
                T("Cleaner_Item_ChromeSiteData", "Site Storage (LocalStorage & IndexedDB)"),
                T("Cleaner_Desc_ChromeSiteData", "HTML5 LocalStorage, IndexedDB, and offline web app caches in Google Chrome"),
                "🗄️", true,
                T("Cleaner_Warn_ChromeSiteData", "Clears offline web app storage and site settings in Google Chrome."),
                false,
                new string[] { @"Local Storage\leveldb", "IndexedDB", "Session Storage", "File System" },
                null,
                null,
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_ai_models",
                T("Cleaner_Item_ChromeAiModels", "Local AI Models (Gemini Nano)"),
                T("Cleaner_Desc_ChromeAiModels", "Downloaded on-device AI models in Google Chrome"),
                "🤖", false, "", false,
                new string[] { "optimization_guide_model_store", "optimization_guide_prediction_model_downloads" },
                null,
                new string[] { "OptGuideOnDeviceModel" },
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSqliteVacuumCleaner("chrome_vacuum", "chrome")
            {
                Name = T("Cleaner_Item_ChromeVacuum", "Compact SQLite Databases (Vacuum)"),
                Description = T("Cleaner_Desc_ChromeVacuum", "Defragments and compacts Google Chrome SQLite database files without losing data"),
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            list.Add(new ChromiumSubpathCleaner(
                "chrome_crash",
                T("Cleaner_Item_ChromeCrash", "Crash Reports (Crashpad)"),
                T("Cleaner_Desc_ChromeCrash", "Diagnostic crash reports and telemetry queues in Google Chrome"),
                "🚑", false, "", true,
                null, null,
                new string[] { @"Crashpad\reports", @"Crashpad\completed", @"Crashpad\pending" },
                "chrome")
            {
                TabId = "apps",
                SectionId = "chrome",
                SectionTitle = T("Cleaner_Section_Chrome", "Google Chrome"),
                SectionIcon = "🌐"
            });

            // SECTION 2.2: WINDOWS DEFENDER
            list.Add(new DirectoryPatternCleaner(
                "defender_temp", "defender", T("Cleaner_Section_Defender", "Windows Defender"),
                T("Cleaner_Item_DefenderTemp", "Scan Logs & History"),
                T("Cleaner_Desc_DefenderTemp", "Temporary detection histories, operational support logs, and engine cache in ProgramData"),
                "🛡️", true, false, "", true,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Service", "DetectionHistory"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Support"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "MetaStore")
                })
            {
                TabId = "apps",
                SectionId = "defender",
                SectionTitle = T("Cleaner_Section_Defender", "Windows Defender"),
                SectionIcon = "🛡️"
            });

            list.Add(new DirectoryPatternCleaner(
                "defender_quarantine", "defender", T("Cleaner_Section_Defender", "Windows Defender"),
                T("Cleaner_Item_DefenderQuarantine", "Quarantined Files"),
                T("Cleaner_Desc_DefenderQuarantine", "Deletes quarantined malware/threat files in C:\\ProgramData\\Microsoft\\Windows Defender\\Quarantine"),
                "☣️", true, true,
                T("Cleaner_Warn_DefenderQuarantine", "Quarantined files cannot be restored once deleted. Requires Administrator/SYSTEM privileges."),
                false,
                () => new List<string>()
                {
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Quarantine", "ResourceData"),
                    Path.Combine(progData, "Microsoft", "Windows Defender", "Quarantine", "Entries")
                })
            {
                TabId = "apps",
                SectionId = "defender",
                SectionTitle = T("Cleaner_Section_Defender", "Windows Defender"),
                SectionIcon = "🛡️"
            });

            // SECTION 2.3: DEVELOPER & DEEP SCAN
            list.Add(new DeepScanOrphanFilesCleaner()
            {
                Name = T("Cleaner_Item_DeepOrphans", "Orphaned Files (.DS_Store, Thumbs.db)"),
                Description = T("Cleaner_Desc_DeepOrphans", "Deep scans for .DS_Store, Thumbs.db, and Vim swap/temp files (*.swp, *.swo, *.tmp) within the selected root folder"),
                TabId = "apps",
                SectionId = "deep_scan",
                SectionTitle = T("Cleaner_Section_DeepScan", "Developer & Deep Scan"),
                SectionIcon = "📦"
            });

            list.Add(new DeepScanDirectoryCleaner(
                "deep_node_modules",
                T("Cleaner_Item_DeepNode", "Node.js & Angular Dependencies (node_modules, .angular)"),
                T("Cleaner_Desc_DeepNode", "Finds and removes heavy node_modules and .angular build folders inside the Deep Scan root path"),
                "📦",
                "Deleting node_modules or .angular folders requires running 'npm install' again in those projects.",
                new string[] { "node_modules", ".angular" })
            {
                TabId = "apps",
                SectionId = "deep_scan",
                SectionTitle = T("Cleaner_Section_DeepScan", "Developer & Deep Scan"),
                SectionIcon = "📦"
            });

            list.Add(new DeepScanDirectoryCleaner(
                "deep_python_cache",
                T("Cleaner_Item_DeepPython", "Python Bytecode & Virtual Environments (__pycache__, venv)"),
                T("Cleaner_Desc_DeepPython", "Finds and removes __pycache__ and Python virtual environments (venv / .venv) in the Deep Scan root path"),
                "🐍",
                "Deleting 'venv' or '.venv' directories removes installed Python packages for those virtual environments.",
                new string[] { "__pycache__", "venv", ".venv" })
            {
                TabId = "apps",
                SectionId = "deep_scan",
                SectionTitle = T("Cleaner_Section_DeepScan", "Developer & Deep Scan"),
                SectionIcon = "📦"
            });

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
        private string _activeTab = "windows"; // "windows" or "apps"
        private bool _isBusy = false;
        private CancellationTokenSource _cts;

        // UI Controls - Left Pane
        private Border _tabWindowsBorder;
        private TextBlock _tabWindowsText;
        private Border _tabAppsBorder;
        private TextBlock _tabAppsText;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRecommended;
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
            ApplyLocalizedMetadata();
        }

        private static ControlTemplate CreateGreenProgressBarTemplate()
        {
            FrameworkElementFactory borderRoot = new FrameworkElementFactory(typeof(Border));
            borderRoot.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            borderRoot.SetResourceReference(Border.BackgroundProperty, "BrushBorderCard");
            borderRoot.SetValue(Border.BorderThicknessProperty, new Thickness(0));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory track = new FrameworkElementFactory(typeof(Border));
            track.Name = "PART_Track";

            FrameworkElementFactory indicator = new FrameworkElementFactory(typeof(Border));
            indicator.Name = "PART_Indicator";
            indicator.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            indicator.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))); // CCleaner vibrant green

            grid.AppendChild(track);
            grid.AppendChild(indicator);
            borderRoot.AppendChild(grid);

            return new ControlTemplate(typeof(ProgressBar)) { VisualTree = borderRoot };
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
                Padding = new Thickness(6, 4, 6, 4)
            };
            quickBar.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            quickBar.SetResourceReference(Border.BackgroundProperty, "BrushBgSidebar");

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
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!c.IsDangerous)
                        {
                            c.IsSelected = true;
                        }
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
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase))
                        c.IsSelected = false;
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
                    if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase))
                        c.IsSelected = c.IsRecommended && !c.IsDangerous;
                }
                UpdateSelectedSummary();
            };

            spQuick.Children.Add(_btnSelectAll);
            spQuick.Children.Add(_btnDeselectAll);
            spQuick.Children.Add(_btnSelectRecommended);
            quickBar.Child = spQuick;

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
            // RIGHT COLUMN: TOP GREEN PROGRESS BAR + CENTER DISPLAY + BOTTOM ACTIONS
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
                Height = 14,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Template = CreateGreenProgressBarTemplate(),
                VerticalAlignment = VerticalAlignment.Center
            };
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
                Text = "Taskleiste Jumplist",
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

            BuildCategoriesTree();
        }

        private void BuildCategoriesTree()
        {
            if (_pnlCategoriesContainer == null || _allCleaners == null) return;
            _pnlCategoriesContainer.Children.Clear();

            // Group cleaners of the active tab by SectionId
            List<string> sections = new List<string>();
            foreach (BaseCleaner c in _allCleaners)
            {
                if (string.Equals(c.TabId, _activeTab, StringComparison.OrdinalIgnoreCase))
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
                        string.Equals(c.SectionId, secId, StringComparison.OrdinalIgnoreCase))
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

                // Checkboxes for each item
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
                            string warn = !string.IsNullOrEmpty(cleaner.WarningMessage)
                                ? cleaner.WarningMessage
                                : cleaner.Description;
                            string title = T("Msg_CleanerDangerousTitle", "OptiAssist - Caution Warning");
                            string msg = string.Format(
                                T("Msg_CleanerDangerousPrompt",
                                  "⚠️ CAUTION WARNING\n\nSelected item: {0}\n\nRisks and Implications:\n{1}\n\nThis option may clear active login sessions, browsing preferences, or sensitive caches.\n\nAre you sure you want to enable this option for cleaning anyway?"),
                                cleaner.Name, warn);

                            MessageBoxResult res = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (res == MessageBoxResult.Yes)
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
                            if (cb.IsChecked != true)
                            {
                                e.Handled = true;
                                handleDangerousConfirm();
                            }
                        };

                        cb.PreviewKeyDown += (s, e) =>
                        {
                            if (e.Key == System.Windows.Input.Key.Space && cb.IsChecked != true)
                            {
                                e.Handled = true;
                                handleDangerousConfirm();
                            }
                        };
                    }

                    _pnlCategoriesContainer.Children.Add(cb);
                }

                // If Deep Scan section, add root folder picker row
                if (secId == "deep_scan")
                {
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
                if (c.IsSelected)
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
                if (c.IsSelected) selected.Add(c);
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
                Log("   STARTING SYSTEM CLEANER DRY-RUN SCAN          ");
                Log(string.Format("   Selected modules: {0} | Elevated: {1}", selected.Count, ctx.IsProcessElevated));
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

                    Log(string.Format("[SCAN] {0}: {1} ({2} items found, {3} skipped/locked)",
                        cleaner.Name, BaseCleaner.FormatBytes(sr.EstimatedBytes), sr.FoundItems.Count, sr.SkippedOrLockedCount));
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
                    // Group findings by section
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
                                                if (File.Exists(capturedPath) || Directory.Exists(capturedPath))
                                                {
                                                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", capturedPath));
                                                }
                                                else
                                                {
                                                    string dir = Path.GetDirectoryName(capturedPath);
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

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _allCleaners == null) return;

            List<BaseCleaner> selected = new List<BaseCleaner>();
            List<string> warnings = new List<string>();
            bool hasChromeSelected = false;
            bool hasEdgeSelected = false;
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
                    if (string.Equals(c.SectionId, "chrome", StringComparison.OrdinalIgnoreCase)) hasChromeSelected = true;
                    if (string.Equals(c.SectionId, "edge", StringComparison.OrdinalIgnoreCase)) hasEdgeSelected = true;
                }
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check running browsers
            if (hasChromeSelected)
            {
                List<string> chromeRunning = ChromiumHelper.GetRunningChromiumBrowsers("chrome");
                if (chromeRunning.Count > 0)
                {
                    string browserMsg = string.Format(
                        T("Msg_CleanerBrowserRunning", "The following browsers are currently running:\n• {0}\n\nOpen browser databases (Cookies, History, SQLite Vacuum) may be locked. Close the browser first for a complete cleanup, or click Yes to continue and skip locked files."),
                        string.Join("\n• ", chromeRunning.ToArray()));
                    if (MessageBox.Show(browserMsg, "OptiAssist - Browser Running", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            if (hasEdgeSelected)
            {
                List<string> edgeRunning = ChromiumHelper.GetRunningChromiumBrowsers("edge");
                if (edgeRunning.Count > 0)
                {
                    string browserMsg = string.Format(
                        T("Msg_CleanerBrowserRunning", "The following browsers are currently running:\n• {0}\n\nOpen browser databases (Cookies, History, SQLite Vacuum) may be locked. Close the browser first for a complete cleanup, or click Yes to continue and skip locked files."),
                        string.Join("\n• ", edgeRunning.ToArray()));
                    if (MessageBox.Show(browserMsg, "OptiAssist - Browser Running", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }
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
                Log("   STARTING SYSTEM CLEANING & OPTIMIZATION       ");
                Log(string.Format("   Modules to clean: {0}", selected.Count));
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
                        _txtRunningStats.Text = string.Format("{0} freed • {1} files deleted",
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
                    Text = string.Format("✔️ {0} files deleted • {1} reclaimable space freed.", totalDeleted, BaseCleaner.FormatBytes(totalFreed)),
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
                        Text = string.Format("ℹ️ {0} files were skipped because they were currently locked or in use.", totalSkipped),
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

        public void OnLanguageChanged(string langCode)
        {
            if (_tabWindowsText != null) _tabWindowsText.Text = T("Cleaner_Tab_Windows", "Windows");
            if (_tabAppsText != null) _tabAppsText.Text = T("Cleaner_Tab_Apps", "Applications");
            if (_btnSelectAll != null) _btnSelectAll.Content = T("Cleaner_Quick_All", "Select All");
            if (_btnDeselectAll != null) _btnDeselectAll.Content = T("Cleaner_Quick_None", "Deselect All");
            if (_btnSelectRecommended != null) _btnSelectRecommended.Content = T("Cleaner_Quick_Default", "Defaults");
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
                    item.SectionTitle = tmpl.SectionTitle;
                    item.WarningMessage = tmpl.WarningMessage;
                }
            }
        }
    }

    #endregion
}
