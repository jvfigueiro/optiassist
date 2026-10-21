using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace OptiAssist
{
    public class AddonManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Icon { get; set; }
        public string Category { get; set; }
        public string FolderPath { get; set; }
        public bool IsEnabled { get; set; }

        public AddonManifest()
        {
            Id = "";
            Name = "";
            Description = "";
            Version = "1.0.0";
            Author = "OptiAssist";
            Icon = "🧩";
            Category = "General";
            FolderPath = "";
            IsEnabled = true;
        }
    }

    public class CleanerItem : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _description;
        private string _category;
        private string _icon;
        private bool _isSelected;
        private bool _isRecommended;
        private long _sizeBytes;
        private string _sizeDisplay;
        private string _statusDisplay;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged("Id"); }
        }

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

        public string Category
        {
            get { return _category; }
            set { _category = value; OnPropertyChanged("Category"); }
        }

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; OnPropertyChanged("Icon"); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; OnPropertyChanged("IsSelected"); }
        }

        public bool IsRecommended
        {
            get { return _isRecommended; }
            set { _isRecommended = value; OnPropertyChanged("IsRecommended"); }
        }

        public long SizeBytes
        {
            get { return _sizeBytes; }
            set
            {
                _sizeBytes = value;
                OnPropertyChanged("SizeBytes");
                SizeDisplay = CleanerEngine.FormatBytes(value);
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

        public CleanerItem(string id, string name, string description, string category, string icon, bool isRecommended)
        {
            _id = id;
            _name = name;
            _description = description;
            _category = category;
            _icon = icon;
            _isRecommended = isRecommended;
            _isSelected = isRecommended;
            _sizeBytes = 0;
            _sizeDisplay = "-";
            _statusDisplay = "Ready";
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

    public static class CleanerEngine
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        public static extern int DnsFlushResolverCache();

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return string.Format("{0} B", bytes);
            if (bytes < 1024 * 1024) return string.Format("{0:F1} KB", bytes / 1024.0);
            if (bytes < 1024 * 1024 * 1024) return string.Format("{0:F2} MB", bytes / (1024.0 * 1024.0));
            return string.Format("{0:F2} GB", bytes / (1024.0 * 1024.0 * 1024.0));
        }

        public static long GetDirectorySizeSafe(string path, string searchPattern = "*")
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            long total = 0;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);
                foreach (FileInfo fi in dir.EnumerateFiles(searchPattern, SearchOption.AllDirectories))
                {
                    try { total += fi.Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static long CleanDirectorySafe(string path, string searchPattern = "*", bool deleteSubfolders = true)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            long freed = 0;
            DirectoryInfo dir = new DirectoryInfo(path);

            try
            {
                foreach (FileInfo file in dir.GetFiles(searchPattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        long len = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        freed += len;
                    }
                    catch { }
                }
            }
            catch { }

            if (deleteSubfolders)
            {
                try
                {
                    foreach (DirectoryInfo sub in dir.GetDirectories())
                    {
                        freed += CleanDirectorySafe(sub.FullName, searchPattern, true);
                        try
                        {
                            sub.Attributes = FileAttributes.Normal;
                            sub.Delete(true);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return freed;
        }

        public static long ScanItem(CleanerItem item, Action<string> logger)
        {
            if (item == null) return 0;
            long size = 0;

            try
            {
                switch (item.Id)
                {
                    case "user_temp":
                        size += GetDirectorySizeSafe(Path.GetTempPath());
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        size += GetDirectorySizeSafe(Path.Combine(localAppData, "Temp"));
                        break;

                    case "system_temp":
                        string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                        size += GetDirectorySizeSafe(winTemp);
                        break;

                    case "win_update":
                        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string downloadDir = Path.Combine(winDir, "SoftwareDistribution", "Download");
                        size += GetDirectorySizeSafe(downloadDir);
                        break;

                    case "thumbnails":
                        string explorerCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
                        if (Directory.Exists(explorerCache))
                        {
                            DirectoryInfo di = new DirectoryInfo(explorerCache);
                            foreach (FileInfo fi in di.GetFiles("thumbcache_*.db"))
                            {
                                try { size += fi.Length; } catch { }
                            }
                            foreach (FileInfo fi in di.GetFiles("iconcache_*.db"))
                            {
                                try { size += fi.Length; } catch { }
                            }
                        }
                        break;

                    case "shellbags":
                        // Registry size estimate: calculate count of stored explorer keys
                        int shellbagCount = 0;
                        try
                        {
                            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags"))
                            {
                                if (key != null) shellbagCount += key.SubKeyCount;
                            }
                            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU"))
                            {
                                if (key != null) shellbagCount += key.SubKeyCount;
                            }
                        }
                        catch { }
                        size = shellbagCount * 1024; // Representative weight
                        break;

                    case "clipboard":
                        size = 1024; // Representative active buffer
                        break;

                    case "dns_cache":
                        size = 0; // Operational cache
                        break;

                    case "system_logs":
                        string evtDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "winevt", "Logs");
                        size += GetDirectorySizeSafe(evtDir);
                        break;

                    case "memory_dumps":
                        string winPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string memoryDmp = Path.Combine(winPath, "MEMORY.DMP");
                        if (File.Exists(memoryDmp))
                        {
                            try { size += new FileInfo(memoryDmp).Length; } catch { }
                        }
                        size += GetDirectorySizeSafe(Path.Combine(winPath, "Minidump"));
                        size += GetDirectorySizeSafe(Path.Combine(winPath, "LiveKernelReports"));
                        break;

                    case "mui_cache":
                        int muiCount = 0;
                        try
                        {
                            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"))
                            {
                                if (key != null) muiCount = key.ValueCount;
                            }
                        }
                        catch { }
                        size = muiCount * 512;
                        break;

                    case "prefetch":
                        string prefetchDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                        size += GetDirectorySizeSafe(prefetchDir);
                        break;

                    case "recycle_bin":
                        try
                        {
                            SHQUERYRBINFO info = new SHQUERYRBINFO();
                            info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                            int hr = SHQueryRecycleBin(null, ref info);
                            if (hr == 0)
                            {
                                size = info.i64Size;
                            }
                        }
                        catch { }
                        break;

                    case "defender_temp":
                        string defPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows Defender");
                        size += GetDirectorySizeSafe(Path.Combine(defPath, "Scans", "History", "Service", "DetectionHistory"));
                        size += GetDirectorySizeSafe(Path.Combine(defPath, "Support"));
                        size += GetDirectorySizeSafe(Path.Combine(defPath, "Scans", "MetaStore"));
                        break;

                    case "error_reports":
                        string progDataWer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER");
                        string localWer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER");
                        size += GetDirectorySizeSafe(Path.Combine(progDataWer, "ReportArchive"));
                        size += GetDirectorySizeSafe(Path.Combine(progDataWer, "ReportQueue"));
                        size += GetDirectorySizeSafe(Path.Combine(localWer, "ReportArchive"));
                        size += GetDirectorySizeSafe(Path.Combine(localWer, "ReportQueue"));
                        break;

                    case "delivery_opt":
                        string winP = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string delivOpt1 = Path.Combine(winP, "SoftwareDistribution", "DeliveryOptimization");
                        string delivOpt2 = Path.Combine(winP, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache");
                        size += GetDirectorySizeSafe(delivOpt1);
                        size += GetDirectorySizeSafe(delivOpt2);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (logger != null) logger(string.Format("[CLEANER SCAN ERROR] {0}: {1}", item.Name, ex.Message));
            }

            return size;
        }

        public static long CleanItem(CleanerItem item, Action<string> logger)
        {
            if (item == null) return 0;
            long freed = 0;

            try
            {
                switch (item.Id)
                {
                    case "user_temp":
                        freed += CleanDirectorySafe(Path.GetTempPath());
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        freed += CleanDirectorySafe(Path.Combine(localAppData, "Temp"));
                        if (logger != null) logger(string.Format("[CLEANER] User temp cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "system_temp":
                        string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                        freed += CleanDirectorySafe(winTemp);
                        if (logger != null) logger(string.Format("[CLEANER] Windows system temp cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "win_update":
                        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string downloadDir = Path.Combine(winDir, "SoftwareDistribution", "Download");
                        freed += CleanDirectorySafe(downloadDir);
                        if (logger != null) logger(string.Format("[CLEANER] Windows Update cache cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "thumbnails":
                        string explorerCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
                        if (Directory.Exists(explorerCache))
                        {
                            DirectoryInfo di = new DirectoryInfo(explorerCache);
                            foreach (FileInfo fi in di.GetFiles("thumbcache_*.db"))
                            {
                                try
                                {
                                    long len = fi.Length;
                                    fi.Attributes = FileAttributes.Normal;
                                    fi.Delete();
                                    freed += len;
                                }
                                catch { }
                            }
                            foreach (FileInfo fi in di.GetFiles("iconcache_*.db"))
                            {
                                try
                                {
                                    long len = fi.Length;
                                    fi.Attributes = FileAttributes.Normal;
                                    fi.Delete();
                                    freed += len;
                                }
                                catch { }
                            }
                        }
                        if (logger != null) logger(string.Format("[CLEANER] Thumbnail cache cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "shellbags":
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags", false);
                            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU", false);
                            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\Shell\Bags", false);
                            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\Shell\BagMRU", false);
                            freed += 1024 * 16;
                            if (logger != null) logger("[CLEANER] Explorer Shellbags & folder view history successfully cleared.");
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger("[CLEANER WARNING] Shellbags clear partially skipped: " + ex.Message);
                        }
                        break;

                    case "clipboard":
                        try
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                Clipboard.Clear();
                            }));
                            freed += 1024;
                            if (logger != null) logger("[CLEANER] System clipboard contents flushed.");
                        }
                        catch { }
                        break;

                    case "dns_cache":
                        try
                        {
                            DnsFlushResolverCache();
                            Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
                            if (logger != null) logger("[CLEANER] DNS Resolver Cache flushed successfully.");
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger("[CLEANER WARNING] DNS flush: " + ex.Message);
                        }
                        break;

                    case "system_logs":
                        try
                        {
                            string[] standardLogs = new string[] { "Application", "System", "Security", "Setup" };
                            foreach (string logName in standardLogs)
                            {
                                try
                                {
                                    using (var el = new EventLog(logName))
                                    {
                                        el.Clear();
                                    }
                                }
                                catch { }
                            }
                            // Also call wevtutil for thorough log clearing
                            ProcessStartInfo psi = new ProcessStartInfo("wevtutil", "cl Application") { CreateNoWindow = true, UseShellExecute = false };
                            Process.Start(psi);
                            psi.Arguments = "cl System"; Process.Start(psi);
                            psi.Arguments = "cl Setup"; Process.Start(psi);
                            if (logger != null) logger("[CLEANER] System event logs cleared.");
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger("[CLEANER WARNING] Event logs clear: " + ex.Message);
                        }
                        break;

                    case "memory_dumps":
                        string winP = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string memDmp = Path.Combine(winP, "MEMORY.DMP");
                        if (File.Exists(memDmp))
                        {
                            try
                            {
                                long len = new FileInfo(memDmp).Length;
                                File.SetAttributes(memDmp, FileAttributes.Normal);
                                File.Delete(memDmp);
                                freed += len;
                            }
                            catch { }
                        }
                        freed += CleanDirectorySafe(Path.Combine(winP, "Minidump"));
                        freed += CleanDirectorySafe(Path.Combine(winP, "LiveKernelReports"));
                        if (logger != null) logger(string.Format("[CLEANER] Memory and crash dump files cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "mui_cache":
                        try
                        {
                            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache", true))
                            {
                                if (key != null)
                                {
                                    foreach (string valName in key.GetValueNames())
                                    {
                                        try { key.DeleteValue(valName); } catch { }
                                    }
                                }
                            }
                            if (logger != null) logger("[CLEANER] Registry MUICache cleared.");
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger("[CLEANER WARNING] MUICache: " + ex.Message);
                        }
                        break;

                    case "prefetch":
                        string prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                        freed += CleanDirectorySafe(prefetch, "*.pf", false);
                        if (logger != null) logger(string.Format("[CLEANER] Windows Prefetch cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "recycle_bin":
                        try
                        {
                            uint res = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                            if (res == 0)
                            {
                                if (logger != null) logger("[CLEANER] Recycle bin emptied across all drives.");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger("[CLEANER WARNING] Empty Recycle Bin: " + ex.Message);
                        }
                        break;

                    case "defender_temp":
                        string defPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows Defender");
                        freed += CleanDirectorySafe(Path.Combine(defPath, "Scans", "History", "Service", "DetectionHistory"));
                        freed += CleanDirectorySafe(Path.Combine(defPath, "Support"));
                        freed += CleanDirectorySafe(Path.Combine(defPath, "Scans", "MetaStore"));
                        if (logger != null) logger(string.Format("[CLEANER] Windows Defender temp and histories cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "error_reports":
                        string progDataWer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER");
                        string localWer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER");
                        freed += CleanDirectorySafe(Path.Combine(progDataWer, "ReportArchive"));
                        freed += CleanDirectorySafe(Path.Combine(progDataWer, "ReportQueue"));
                        freed += CleanDirectorySafe(Path.Combine(localWer, "ReportArchive"));
                        freed += CleanDirectorySafe(Path.Combine(localWer, "ReportQueue"));
                        if (logger != null) logger(string.Format("[CLEANER] Windows Error Reporting queues cleared: {0} freed.", FormatBytes(freed)));
                        break;

                    case "delivery_opt":
                        string winRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        string deliv1 = Path.Combine(winRoot, "SoftwareDistribution", "DeliveryOptimization");
                        string deliv2 = Path.Combine(winRoot, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache");
                        freed += CleanDirectorySafe(deliv1);
                        freed += CleanDirectorySafe(deliv2);
                        if (logger != null) logger(string.Format("[CLEANER] Delivery Optimization cache cleared: {0} freed.", FormatBytes(freed)));
                        break;
                }
            }
            catch (Exception ex)
            {
                if (logger != null) logger(string.Format("[CLEANER ERROR] {0}: {1}", item.Name, ex.Message));
            }

            return freed;
        }

        public static List<CleanerItem> GetDefaultCleanerItems()
        {
            return new List<CleanerItem>()
            {
                new CleanerItem("user_temp",
                    LocalizationHelper.Get("Cleaner_Item_UserTemp", "User Temporary Files"),
                    LocalizationHelper.Get("Cleaner_Desc_UserTemp", "Temporary files and app caches in %TEMP% and user temp directories"),
                    "Temporary Files", "📁", true),

                new CleanerItem("system_temp",
                    LocalizationHelper.Get("Cleaner_Item_SystemTemp", "Windows System Temp"),
                    LocalizationHelper.Get("Cleaner_Desc_SystemTemp", "Temporary system files located in C:\\Windows\\Temp"),
                    "Temporary Files", "📁", true),

                new CleanerItem("win_update",
                    LocalizationHelper.Get("Cleaner_Item_WinUpdate", "Windows Update Cache"),
                    LocalizationHelper.Get("Cleaner_Desc_WinUpdate", "Downloaded Windows Update patch files in SoftwareDistribution\\Download"),
                    "System Caches", "🔄", true),

                new CleanerItem("thumbnails",
                    LocalizationHelper.Get("Cleaner_Item_Thumbnails", "Thumbnail Cache"),
                    LocalizationHelper.Get("Cleaner_Desc_Thumbnails", "Windows Explorer thumbnail cache databases (thumbcache_*.db)"),
                    "Explorer & UI", "🖼️", true),

                new CleanerItem("recycle_bin",
                    LocalizationHelper.Get("Cleaner_Item_RecycleBin", "Empty Recycle Bin"),
                    LocalizationHelper.Get("Cleaner_Desc_RecycleBin", "Permanently deletes all files from recycle bins on all drives"),
                    "Storage", "🗑️", true),

                new CleanerItem("memory_dumps",
                    LocalizationHelper.Get("Cleaner_Item_MemoryDumps", "Memory Dumps & BSOD Reports"),
                    LocalizationHelper.Get("Cleaner_Desc_MemoryDumps", "Crash memory dumps (MEMORY.DMP) and minidumps from blue screens"),
                    "System Dumps", "💥", true),

                new CleanerItem("defender_temp",
                    LocalizationHelper.Get("Cleaner_Item_DefenderTemp", "Windows Defender Temp & Scans"),
                    LocalizationHelper.Get("Cleaner_Desc_DefenderTemp", "Temporary detection histories, operational support logs, and engine cache"),
                    "Security", "🛡️", true),

                new CleanerItem("dns_cache",
                    LocalizationHelper.Get("Cleaner_Item_DnsCache", "DNS Resolver Cache"),
                    LocalizationHelper.Get("Cleaner_Desc_DnsCache", "Flushes local DNS resolver cache to fix connection issues"),
                    "Network", "🌐", true),

                new CleanerItem("error_reports",
                    LocalizationHelper.Get("Cleaner_Item_ErrorReports", "Windows Error Reporting (WER)"),
                    LocalizationHelper.Get("Cleaner_Desc_ErrorReports", "Archived crash reports, mini-dumps, and error reporting queues"),
                    "System Logs", "📊", true),

                new CleanerItem("delivery_opt",
                    LocalizationHelper.Get("Cleaner_Item_DeliveryOpt", "Delivery Optimization Cache"),
                    LocalizationHelper.Get("Cleaner_Desc_DeliveryOpt", "Peer-to-peer Windows Update delivery cache files"),
                    "System Caches", "📦", true),

                new CleanerItem("clipboard",
                    LocalizationHelper.Get("Cleaner_Item_Clipboard", "Windows Clipboard"),
                    LocalizationHelper.Get("Cleaner_Desc_Clipboard", "Clears all currently copied text, images, and data from memory clipboard"),
                    "Privacy & Registry", "📋", false),

                new CleanerItem("shellbags",
                    LocalizationHelper.Get("Cleaner_Item_Shellbags", "Explorer Shellbags"),
                    LocalizationHelper.Get("Cleaner_Desc_Shellbags", "Registry folder view settings, window positions, and explorer browsing history"),
                    "Privacy & Registry", "🗃️", false),

                new CleanerItem("mui_cache",
                    LocalizationHelper.Get("Cleaner_Item_MuiCache", "MUICache"),
                    LocalizationHelper.Get("Cleaner_Desc_MuiCache", "Registry cache of application names and executable strings"),
                    "Privacy & Registry", "🗂️", false),

                new CleanerItem("prefetch",
                    LocalizationHelper.Get("Cleaner_Item_Prefetch", "Windows Prefetch"),
                    LocalizationHelper.Get("Cleaner_Desc_Prefetch", "Execution cache files in C:\\Windows\\Prefetch"),
                    "System Caches", "🚀", false),

                new CleanerItem("system_logs",
                    LocalizationHelper.Get("Cleaner_Item_SystemLogs", "System Event Logs"),
                    LocalizationHelper.Get("Cleaner_Desc_SystemLogs", "Clears Windows event logs (Application, System, Security, Setup)"),
                    "System Logs", "📑", false)
            };
        }
    }

    public static class AddonHelper
    {
        public static string GetAddonsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string addonsDir = Path.Combine(baseDir, "Config", "Addons");
            if (!Directory.Exists(addonsDir))
            {
                try { Directory.CreateDirectory(addonsDir); } catch { }
            }
            return addonsDir;
        }

        public static void EnsureDefaultAddons()
        {
            string addonsDir = GetAddonsDirectory();

            // Create SystemCleaner addon folder if not present
            string cleanerDir = Path.Combine(addonsDir, "SystemCleaner");
            if (!Directory.Exists(cleanerDir))
            {
                try { Directory.CreateDirectory(cleanerDir); } catch { }
            }

            string cleanerManifest = Path.Combine(cleanerDir, "addon.json");
            if (!File.Exists(cleanerManifest))
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("{");
                    sb.AppendLine("  \"id\": \"system-cleaner\",");
                    sb.AppendLine("  \"name\": \"System Cleaner & Optimizer\",");
                    sb.AppendLine("  \"description\": \"Deep cleaner for temporary files, Windows update cache, memory dumps, logs, and privacy traces.\",");
                    sb.AppendLine("  \"version\": \"1.0.0\",");
                    sb.AppendLine("  \"author\": \"OptiAssist\",");
                    sb.AppendLine("  \"icon\": \"🧹\",");
                    sb.AppendLine("  \"category\": \"Maintenance\",");
                    sb.AppendLine("  \"enabled\": true");
                    sb.AppendLine("}");
                    File.WriteAllText(cleanerManifest, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }

            // Create README explaining the modular plugin architecture
            string readmePath = Path.Combine(addonsDir, "README.md");
            if (!File.Exists(readmePath))
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# OptiAssist Modular Add-ons Directory");
                    sb.AppendLine();
                    sb.AppendLine("This directory contains extensible plug-in modules for OptiAssist.");
                    sb.AppendLine();
                    sb.AppendLine("## How to Install an Add-on");
                    sb.AppendLine("1. Create a subdirectory inside `Config/Addons/` (e.g., `Config/Addons/MyTool/`).");
                    sb.AppendLine("2. Add an `addon.json` file defining module properties:");
                    sb.AppendLine("```json");
                    sb.AppendLine("{");
                    sb.AppendLine("  \"id\": \"my-tool\",");
                    sb.AppendLine("  \"name\": \"My Custom Tool\",");
                    sb.AppendLine("  \"description\": \"Description of the module functionality\",");
                    sb.AppendLine("  \"version\": \"1.0.0\",");
                    sb.AppendLine("  \"author\": \"Technician Name\",");
                    sb.AppendLine("  \"icon\": \"🛠️\",");
                    sb.AppendLine("  \"category\": \"Utilities\",");
                    sb.AppendLine("  \"enabled\": true");
                    sb.AppendLine("}");
                    sb.AppendLine("```");
                    sb.AppendLine("3. Reload add-ons from the OptiAssist interface.");
                    File.WriteAllText(readmePath, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }

        public static List<AddonManifest> LoadDiscoveredAddons(List<string> disabledAddonIds)
        {
            EnsureDefaultAddons();
            List<AddonManifest> list = new List<AddonManifest>();
            string addonsDir = GetAddonsDirectory();

            try
            {
                foreach (string dir in Directory.GetDirectories(addonsDir))
                {
                    string manifestPath = Path.Combine(dir, "addon.json");
                    if (File.Exists(manifestPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
                            var dict = ParseSimpleJson(json);
                            AddonManifest addon = new AddonManifest();
                            addon.FolderPath = dir;

                            if (dict.ContainsKey("id")) addon.Id = dict["id"];
                            else addon.Id = Path.GetFileName(dir).ToLowerInvariant();

                            if (dict.ContainsKey("name")) addon.Name = dict["name"];
                            else addon.Name = Path.GetFileName(dir);

                            if (dict.ContainsKey("description")) addon.Description = dict["description"];
                            if (dict.ContainsKey("version")) addon.Version = dict["version"];
                            if (dict.ContainsKey("author")) addon.Author = dict["author"];
                            if (dict.ContainsKey("icon")) addon.Icon = dict["icon"];
                            if (dict.ContainsKey("category")) addon.Category = dict["category"];

                            bool enabled = true;
                            if (dict.ContainsKey("enabled"))
                            {
                                bool b;
                                if (bool.TryParse(dict["enabled"], out b)) enabled = b;
                            }

                            if (disabledAddonIds != null && disabledAddonIds.Contains(addon.Id))
                            {
                                enabled = false;
                            }
                            addon.IsEnabled = enabled;

                            list.Add(addon);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return list;
        }

        private static Dictionary<string, string> ParseSimpleJson(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            int i = 0;
            while (i < json.Length)
            {
                if (json[i] == '\"')
                {
                    int keyStart = i + 1;
                    int keyEnd = json.IndexOf('\"', keyStart);
                    if (keyEnd == -1) break;
                    string key = json.Substring(keyStart, keyEnd - keyStart);

                    int colon = json.IndexOf(':', keyEnd);
                    if (colon == -1) break;
                    i = colon + 1;

                    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                    if (i < json.Length && json[i] == '\"')
                    {
                        int valStart = i + 1;
                        int valEnd = valStart;
                        while (valEnd < json.Length)
                        {
                            if (json[valEnd] == '\"' && json[valEnd - 1] != '\\') break;
                            valEnd++;
                        }
                        string val = json.Substring(valStart, valEnd - valStart);
                        dict[key] = val.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        i = valEnd + 1;
                    }
                    else
                    {
                        int valStart = i;
                        while (i < json.Length && json[i] != ',' && json[i] != '}' && !char.IsWhiteSpace(json[i])) i++;
                        string val = json.Substring(valStart, i - valStart);
                        dict[key] = val;
                    }
                }
                else
                {
                    i++;
                }
            }
            return dict;
        }
    }
}
