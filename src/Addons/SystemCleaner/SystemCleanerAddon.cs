using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using OptiAssist;

namespace OptiAssist.Addons.SystemCleaner
{
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
                        size = shellbagCount * 1024;
                        break;

                    case "clipboard":
                        size = 1024;
                        break;

                    case "dns_cache":
                        size = 0;
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
                            if (logger != null) logger("[CLEANER] Explorer Shellbags & folder view history cleared.");
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
    }

    /// <summary>
    /// Self-contained System Cleaner & Optimizer Add-on Module compiled into SystemCleaner.dll.
    /// </summary>
    public class SystemCleanerAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private ObservableCollection<CleanerItem> _items;
        private bool _isBusy = false;

        // UI controls held by the module
        private TextBlock _txtTitle;
        private TextBlock _txtSubtitle;
        private Button _btnAnalyze;
        private Button _btnClean;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnSelectRecommended;
        private TextBlock _lblStatus;
        private TextBlock _lblTotalSize;
        private ProgressBar _pbProgress;

        public string Id
        {
            get { return "system-cleaner"; }
        }

        public string Name
        {
            get { return T("Nav_SystemCleaner", "System Cleaner"); }
        }

        public string Description
        {
            get { return T("Cleaner_Subtitle", "Deep cleaning of temporary files, caches, memory dumps, logs, and privacy traces"); }
        }

        public string Version
        {
            get { return "1.0.0"; }
        }

        public string Author
        {
            get { return "OptiAssist"; }
        }

        public string Icon
        {
            get { return "🧹"; }
        }

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            InitItems();
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

        private void InitItems()
        {
            _items = new ObservableCollection<CleanerItem>()
            {
                new CleanerItem("user_temp",
                    T("Cleaner_Item_UserTemp", "User Temporary Files"),
                    T("Cleaner_Desc_UserTemp", "Temporary files and caches in %TEMP% and local app temp folders"),
                    "Temporary Files", "📁", true),

                new CleanerItem("system_temp",
                    T("Cleaner_Item_SystemTemp", "Windows System Temp"),
                    T("Cleaner_Desc_SystemTemp", "Temporary system files located in C:\\Windows\\Temp"),
                    "Temporary Files", "📁", true),

                new CleanerItem("win_update",
                    T("Cleaner_Item_WinUpdate", "Windows Update Cache"),
                    T("Cleaner_Desc_WinUpdate", "Downloaded Windows Update installation files in SoftwareDistribution\\Download"),
                    "System Caches", "🔄", true),

                new CleanerItem("thumbnails",
                    T("Cleaner_Item_Thumbnails", "Thumbnail Cache"),
                    T("Cleaner_Desc_Thumbnails", "Explorer thumbnail databases (thumbcache_*.db) and icon cache"),
                    "Explorer & UI", "🖼️", true),

                new CleanerItem("recycle_bin",
                    T("Cleaner_Item_RecycleBin", "Empty Recycle Bin"),
                    T("Cleaner_Desc_RecycleBin", "Permanently deletes all files from recycle bins on all connected drives"),
                    "Storage", "🗑️", true),

                new CleanerItem("memory_dumps",
                    T("Cleaner_Item_MemoryDumps", "Memory Dumps & BSOD Reports"),
                    T("Cleaner_Desc_MemoryDumps", "Crash memory dumps (MEMORY.DMP) and minidumps from blue screens"),
                    "System Dumps", "💥", true),

                new CleanerItem("defender_temp",
                    T("Cleaner_Item_DefenderTemp", "Windows Defender Temp & Scans"),
                    T("Cleaner_Desc_DefenderTemp", "Temporary detection histories, operational support logs, and engine cache"),
                    "Security", "🛡️", true),

                new CleanerItem("dns_cache",
                    T("Cleaner_Item_DnsCache", "DNS Resolver Cache"),
                    T("Cleaner_Desc_DnsCache", "Flushes the local DNS cache to resolve hostname and connection issues"),
                    "Network", "🌐", true),

                new CleanerItem("error_reports",
                    T("Cleaner_Item_ErrorReports", "Windows Error Reporting (WER)"),
                    T("Cleaner_Desc_ErrorReports", "Archived crash reports, mini-dumps, and error reporting queues"),
                    "System Logs", "📊", true),

                new CleanerItem("delivery_opt",
                    T("Cleaner_Item_DeliveryOpt", "Delivery Optimization Cache"),
                    T("Cleaner_Desc_DeliveryOpt", "Peer-to-peer Windows Update delivery cache files"),
                    "System Caches", "📦", true),

                new CleanerItem("clipboard",
                    T("Cleaner_Item_Clipboard", "Windows Clipboard"),
                    T("Cleaner_Desc_Clipboard", "Clears all currently copied text, images, and data from memory clipboard"),
                    "Privacy & Registry", "📋", false),

                new CleanerItem("shellbags",
                    T("Cleaner_Item_Shellbags", "Explorer Shellbags"),
                    T("Cleaner_Desc_Shellbags", "Registry folder view settings, window positions, and explorer browsing history"),
                    "Privacy & Registry", "🗃️", false),

                new CleanerItem("mui_cache",
                    T("Cleaner_Item_MuiCache", "MUICache"),
                    T("Cleaner_Desc_MuiCache", "Registry cache of application names and executable strings"),
                    "Privacy & Registry", "🗂️", false),

                new CleanerItem("prefetch",
                    T("Cleaner_Item_Prefetch", "Windows Prefetch"),
                    T("Cleaner_Desc_Prefetch", "Execution cache files in C:\\Windows\\Prefetch"),
                    "System Caches", "🚀", false),

                new CleanerItem("system_logs",
                    T("Cleaner_Item_SystemLogs", "System Event Logs"),
                    T("Cleaner_Desc_SystemLogs", "Clears Windows event logs (Application, System, Security, Setup)"),
                    "System Logs", "📑", false)
            };
        }

        public FrameworkElement CreateView()
        {
            if (_items == null)
            {
                InitItems();
            }

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(0);

            // 1. Header Section
            StackPanel headerPanel = new StackPanel();
            headerPanel.Margin = new Thickness(0, 0, 0, 10);

            _txtTitle = new TextBlock();
            _txtTitle.Text = T("Cleaner_Title", "System Cleaner & Optimizer");
            _txtTitle.FontSize = 15;
            _txtTitle.FontWeight = FontWeights.Bold;
            _txtTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _txtTitle.TextTrimming = TextTrimming.CharacterEllipsis;

            _txtSubtitle = new TextBlock();
            _txtSubtitle.Text = T("Cleaner_Subtitle", "Deep cleaning of temporary files, caches, memory dumps, logs, and privacy traces");
            _txtSubtitle.FontSize = 11;
            _txtSubtitle.Margin = new Thickness(0, 2, 0, 0);
            _txtSubtitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            _txtSubtitle.TextTrimming = TextTrimming.CharacterEllipsis;

            headerPanel.Children.Add(_txtTitle);
            headerPanel.Children.Add(_txtSubtitle);
            root.Children.Add(headerPanel);

            // 2. Action Toolbar Card
            Border actionCard = new Border();
            actionCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            actionCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            actionCard.BorderThickness = new Thickness(1);
            actionCard.CornerRadius = new CornerRadius(6);
            actionCard.Padding = new Thickness(10);
            actionCard.Margin = new Thickness(0, 0, 0, 10);

            Grid actionGrid = new Grid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel leftBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnAnalyze = new Button();
            _btnAnalyze.Content = T("Btn_CleanerAnalyze", "🔍 Scan & Analyze");
            _btnAnalyze.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnAnalyze.Padding = new Thickness(14, 0, 14, 0);
            _btnAnalyze.Height = 32;
            _btnAnalyze.Margin = new Thickness(0, 0, 8, 0);
            _btnAnalyze.Click += BtnAnalyze_Click;

            _btnClean = new Button();
            _btnClean.Content = T("Btn_CleanerClean", "🧹 Clean Selected");
            _btnClean.SetResourceReference(Button.StyleProperty, "BtnAccent");
            _btnClean.Padding = new Thickness(16, 0, 16, 0);
            _btnClean.Height = 32;
            _btnClean.Click += BtnClean_Click;

            leftBtns.Children.Add(_btnAnalyze);
            leftBtns.Children.Add(_btnClean);
            Grid.SetColumn(leftBtns, 0);
            actionGrid.Children.Add(leftBtns);

            StackPanel rightBtns = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _btnSelectAll = new Button();
            _btnSelectAll.Content = T("Btn_CleanerSelectAll", "Select All");
            _btnSelectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectAll.Padding = new Thickness(10, 0, 10, 0);
            _btnSelectAll.Height = 30;
            _btnSelectAll.Margin = new Thickness(0, 0, 4, 0);
            _btnSelectAll.FontSize = 11;
            _btnSelectAll.Click += (s, e) =>
            {
                foreach (var it in _items) it.IsSelected = true;
                UpdateSelectedSummary();
            };

            _btnDeselectAll = new Button();
            _btnDeselectAll.Content = T("Btn_CleanerDeselectAll", "Deselect All");
            _btnDeselectAll.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnDeselectAll.Padding = new Thickness(10, 0, 10, 0);
            _btnDeselectAll.Height = 30;
            _btnDeselectAll.Margin = new Thickness(0, 0, 4, 0);
            _btnDeselectAll.FontSize = 11;
            _btnDeselectAll.Click += (s, e) =>
            {
                foreach (var it in _items) it.IsSelected = false;
                UpdateSelectedSummary();
            };

            _btnSelectRecommended = new Button();
            _btnSelectRecommended.Content = T("Btn_CleanerSelectRecommended", "Recommended");
            _btnSelectRecommended.SetResourceReference(Button.StyleProperty, "BtnDefault");
            _btnSelectRecommended.Padding = new Thickness(10, 0, 10, 0);
            _btnSelectRecommended.Height = 30;
            _btnSelectRecommended.FontSize = 11;
            _btnSelectRecommended.Click += (s, e) =>
            {
                foreach (var it in _items) it.IsSelected = it.IsRecommended;
                UpdateSelectedSummary();
            };

            rightBtns.Children.Add(_btnSelectAll);
            rightBtns.Children.Add(_btnDeselectAll);
            rightBtns.Children.Add(_btnSelectRecommended);
            Grid.SetColumn(rightBtns, 1);
            actionGrid.Children.Add(rightBtns);

            actionCard.Child = actionGrid;
            root.Children.Add(actionCard);

            // 3. Status & Progress Summary Card
            Border statusCard = new Border();
            statusCard.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            statusCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            statusCard.BorderThickness = new Thickness(1);
            statusCard.CornerRadius = new CornerRadius(6);
            statusCard.Padding = new Thickness(12);
            statusCard.Margin = new Thickness(0, 0, 0, 10);

            StackPanel statusStack = new StackPanel();
            Grid statusGrid = new Grid();
            statusGrid.Margin = new Thickness(0, 0, 0, 4);
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            _lblStatus = new TextBlock();
            _lblStatus.Text = T("Cleaner_Status_Ready", "Ready to scan or clean. Select desired categories above.");
            _lblStatus.FontSize = 12;
            _lblStatus.FontWeight = FontWeights.SemiBold;
            _lblStatus.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            _lblStatus.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_lblStatus, 0);

            _lblTotalSize = new TextBlock();
            _lblTotalSize.Text = "0 B";
            _lblTotalSize.FontSize = 12;
            _lblTotalSize.FontWeight = FontWeights.Bold;
            _lblTotalSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            _lblTotalSize.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_lblTotalSize, 1);

            statusGrid.Children.Add(_lblStatus);
            statusGrid.Children.Add(_lblTotalSize);

            _pbProgress = new ProgressBar();
            _pbProgress.Height = 4;
            _pbProgress.Margin = new Thickness(0, 4, 0, 0);
            _pbProgress.Visibility = Visibility.Collapsed;
            _pbProgress.IsIndeterminate = true;

            statusStack.Children.Add(statusGrid);
            statusStack.Children.Add(_pbProgress);
            statusCard.Child = statusStack;
            root.Children.Add(statusCard);

            // 4. Categories 2-Column Grid
            UniformGrid cardsGrid = new UniformGrid();
            cardsGrid.Columns = 2;

            foreach (CleanerItem item in _items)
            {
                cardsGrid.Children.Add(BuildCleanerItemCard(item));
            }

            root.Children.Add(cardsGrid);

            UpdateSelectedSummary();
            return root;
        }

        private UIElement BuildCleanerItemCard(CleanerItem item)
        {
            Border card = new Border();
            card.SetResourceReference(Border.BackgroundProperty, "BrushBgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(6);
            card.Padding = new Thickness(10, 8, 10, 8);
            card.Margin = new Thickness(3);
            card.DataContext = item;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            CheckBox cb = new CheckBox();
            cb.VerticalAlignment = VerticalAlignment.Top;
            cb.Margin = new Thickness(0, 3, 8, 0);
            Binding cbBinding = new Binding("IsSelected") { Mode = BindingMode.TwoWay };
            cb.SetBinding(CheckBox.IsCheckedProperty, cbBinding);
            cb.Checked += (s, e) => UpdateSelectedSummary();
            cb.Unchecked += (s, e) => UpdateSelectedSummary();
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

            StackPanel details = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            StackPanel titleRow = new StackPanel() { Orientation = Orientation.Horizontal };

            TextBlock txtIcon = new TextBlock() { FontSize = 12, Margin = new Thickness(0, 0, 5, 0) };
            txtIcon.SetBinding(TextBlock.TextProperty, new Binding("Icon"));

            TextBlock txtName = new TextBlock()
            {
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            txtName.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            txtName.SetBinding(TextBlock.TextProperty, new Binding("Name"));

            titleRow.Children.Add(txtIcon);
            titleRow.Children.Add(txtName);

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

            Border badge = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            badge.SetResourceReference(Border.BackgroundProperty, "BrushBtnBg");
            badge.SetResourceReference(Border.BorderBrushProperty, "BrushBorderCard");

            TextBlock txtSize = new TextBlock()
            {
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            txtSize.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            txtSize.SetBinding(TextBlock.TextProperty, new Binding("SizeDisplay"));

            badge.Child = txtSize;
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            card.Child = grid;
            return card;
        }

        private void UpdateSelectedSummary()
        {
            if (_items == null) return;
            long totalBytes = 0;
            int count = 0;
            foreach (var it in _items)
            {
                if (it.IsSelected)
                {
                    count++;
                    totalBytes += it.SizeBytes;
                }
            }
            if (_lblTotalSize != null)
            {
                _lblTotalSize.Text = string.Format("{0} ({1})", CleanerEngine.FormatBytes(totalBytes), count);
            }
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _items == null) return;

            List<CleanerItem> selected = new List<CleanerItem>();
            foreach (var item in _items)
            {
                if (item.IsSelected) selected.Add(item);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isBusy = true;
            _btnAnalyze.IsEnabled = false;
            _btnClean.IsEnabled = false;
            _pbProgress.Visibility = Visibility.Visible;
            _lblStatus.Text = T("Cleaner_Status_Scanning", "Scanning selected categories...");

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFound = 0;
                Log("=================================================");
                Log("   STARTING SYSTEM CLEANER ANALYSIS             ");
                Log(string.Format("   Selected categories: {0}", selected.Count));
                Log("=================================================");

                foreach (var item in selected)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        item.StatusDisplay = "Scanning...";
                        item.SizeDisplay = "...";
                    }));

                    long bytes = CleanerEngine.ScanItem(item, msg => Log(msg));
                    totalFound += bytes;

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        item.SizeBytes = bytes;
                        item.StatusDisplay = bytes > 0 ? "Scanned" : "Clean";
                    }));
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    _btnAnalyze.IsEnabled = true;
                    _btnClean.IsEnabled = true;
                    _pbProgress.Visibility = Visibility.Collapsed;

                    string summaryMsg = string.Format(
                        T("Cleaner_Status_Scanned", "Analysis complete: Found {0} of cleanable data across {1} categories."),
                        CleanerEngine.FormatBytes(totalFound), selected.Count);

                    _lblStatus.Text = summaryMsg;
                    UpdateSelectedSummary();
                    Log("[CLEANER ANALYSIS] " + summaryMsg);
                }));
            });
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _items == null) return;

            List<CleanerItem> selected = new List<CleanerItem>();
            long estimatedSpace = 0;
            foreach (var item in _items)
            {
                if (item.IsSelected)
                {
                    selected.Add(item);
                    estimatedSpace += item.SizeBytes;
                }
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(T("Msg_NoCleanerItemsSelected", "Please select at least one category to clean."), "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMsg = string.Format(
                T("Msg_CleanerConfirm", "Do you want to clean the selected items now?\nTotal estimated space to free: {0}"),
                CleanerEngine.FormatBytes(estimatedSpace));

            var res = MessageBox.Show(confirmMsg, "OptiAssist - System Cleaner", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            _isBusy = true;
            _btnAnalyze.IsEnabled = false;
            _btnClean.IsEnabled = false;
            _pbProgress.Visibility = Visibility.Visible;

            ThreadPool.QueueUserWorkItem(state =>
            {
                long totalFreed = 0;
                Log("=================================================");
                Log("   STARTING SYSTEM CLEANING & OPTIMIZATION       ");
                Log(string.Format("   Categories to clean: {0}", selected.Count));
                Log("=================================================");

                foreach (var item in selected)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        _lblStatus.Text = string.Format(T("Cleaner_Status_Cleaning", "Cleaning {0}..."), item.Name);
                        item.StatusDisplay = "Cleaning...";
                    }));

                    long freed = CleanerEngine.CleanItem(item, msg => Log(msg));
                    totalFreed += freed;

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        item.SizeBytes = 0;
                        item.SizeDisplay = "Cleaned";
                        item.StatusDisplay = "Cleaned";
                    }));
                }

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    _isBusy = false;
                    _btnAnalyze.IsEnabled = true;
                    _btnClean.IsEnabled = true;
                    _pbProgress.Visibility = Visibility.Collapsed;

                    string doneMsg = string.Format(
                        T("Cleaner_Status_Cleaned", "Cleaning completed: {0} freed across {1} categories!"),
                        CleanerEngine.FormatBytes(totalFreed), selected.Count, 0);

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
            if (_txtSubtitle != null) _txtSubtitle.Text = T("Cleaner_Subtitle", "Deep cleaning of temporary files, caches, memory dumps, logs, and privacy traces");
            if (_btnAnalyze != null) _btnAnalyze.Content = T("Btn_CleanerAnalyze", "🔍 Scan & Analyze");
            if (_btnClean != null) _btnClean.Content = T("Btn_CleanerClean", "🧹 Clean Selected");
            if (_btnSelectAll != null) _btnSelectAll.Content = T("Btn_CleanerSelectAll", "Select All");
            if (_btnDeselectAll != null) _btnDeselectAll.Content = T("Btn_CleanerDeselectAll", "Deselect All");
            if (_btnSelectRecommended != null) _btnSelectRecommended.Content = T("Btn_CleanerSelectRecommended", "Recommended");
            if (_lblStatus != null && !_isBusy) _lblStatus.Text = T("Cleaner_Status_Ready", "Ready to scan or clean. Select desired categories above.");

            if (_items != null)
            {
                foreach (var item in _items)
                {
                    switch (item.Id)
                    {
                        case "user_temp":
                            item.Name = T("Cleaner_Item_UserTemp", "User Temporary Files");
                            item.Description = T("Cleaner_Desc_UserTemp", "Temporary files and caches in %TEMP% and local app temp folders");
                            break;
                        case "system_temp":
                            item.Name = T("Cleaner_Item_SystemTemp", "Windows System Temp");
                            item.Description = T("Cleaner_Desc_SystemTemp", "Temporary system files located in C:\\Windows\\Temp");
                            break;
                        case "win_update":
                            item.Name = T("Cleaner_Item_WinUpdate", "Windows Update Cache");
                            item.Description = T("Cleaner_Desc_WinUpdate", "Downloaded Windows Update installation files in SoftwareDistribution\\Download");
                            break;
                        case "thumbnails":
                            item.Name = T("Cleaner_Item_Thumbnails", "Thumbnail Cache");
                            item.Description = T("Cleaner_Desc_Thumbnails", "Explorer thumbnail databases (thumbcache_*.db) and icon cache");
                            break;
                        case "recycle_bin":
                            item.Name = T("Cleaner_Item_RecycleBin", "Empty Recycle Bin");
                            item.Description = T("Cleaner_Desc_RecycleBin", "Permanently deletes all files from recycle bins on all connected drives");
                            break;
                        case "memory_dumps":
                            item.Name = T("Cleaner_Item_MemoryDumps", "Memory Dumps & BSOD Reports");
                            item.Description = T("Cleaner_Desc_MemoryDumps", "Crash memory dumps (MEMORY.DMP) and minidumps from blue screens");
                            break;
                        case "defender_temp":
                            item.Name = T("Cleaner_Item_DefenderTemp", "Windows Defender Temp & Scans");
                            item.Description = T("Cleaner_Desc_DefenderTemp", "Temporary detection histories, operational support logs, and engine cache");
                            break;
                        case "dns_cache":
                            item.Name = T("Cleaner_Item_DnsCache", "DNS Resolver Cache");
                            item.Description = T("Cleaner_Desc_DnsCache", "Flushes the local DNS cache to resolve hostname and connection issues");
                            break;
                        case "error_reports":
                            item.Name = T("Cleaner_Item_ErrorReports", "Windows Error Reporting (WER)");
                            item.Description = T("Cleaner_Desc_ErrorReports", "Archived crash reports, mini-dumps, and error reporting queues");
                            break;
                        case "delivery_opt":
                            item.Name = T("Cleaner_Item_DeliveryOpt", "Delivery Optimization Cache");
                            item.Description = T("Cleaner_Desc_DeliveryOpt", "Peer-to-peer Windows Update delivery cache files");
                            break;
                        case "clipboard":
                            item.Name = T("Cleaner_Item_Clipboard", "Windows Clipboard");
                            item.Description = T("Cleaner_Desc_Clipboard", "Clears all currently copied text, images, and data from memory clipboard");
                            break;
                        case "shellbags":
                            item.Name = T("Cleaner_Item_Shellbags", "Explorer Shellbags");
                            item.Description = T("Cleaner_Desc_Shellbags", "Registry folder view settings, window positions, and explorer browsing history");
                            break;
                        case "mui_cache":
                            item.Name = T("Cleaner_Item_MuiCache", "MUICache");
                            item.Description = T("Cleaner_Desc_MuiCache", "Registry cache of application names and executable strings");
                            break;
                        case "prefetch":
                            item.Name = T("Cleaner_Item_Prefetch", "Windows Prefetch");
                            item.Description = T("Cleaner_Desc_Prefetch", "Execution cache files in C:\\Windows\\Prefetch");
                            break;
                        case "system_logs":
                            item.Name = T("Cleaner_Item_SystemLogs", "System Event Logs");
                            item.Description = T("Cleaner_Desc_SystemLogs", "Clears Windows event logs (Application, System, Security, Setup)");
                            break;
                    }
                }
            }

            UpdateSelectedSummary();
        }
    }
}
