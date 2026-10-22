using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace OptiAssist
{
    /// <summary>
    /// Host contract exposed by OptiAssist.exe to external Add-on DLL modules.
    /// </summary>
    public interface IOptiAssistHost
    {
        void Log(string message);
        string GetLocalizedString(string key, string defaultValue);
        string CurrentLanguage { get; }
        AppSettings Settings { get; }
        HardwareSummary Hardware { get; }
        void SaveSettings();
        void OpenLogWindow();
    }

    /// <summary>
    /// Plugin contract that every OptiAssist Add-on DLL module must implement.
    /// </summary>
    public interface IOptiAssistAddon
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Version { get; }
        string Author { get; }
        string Icon { get; }

        void Initialize(IOptiAssistHost host);
        FrameworkElement CreateView();
        void OnLanguageChanged(string langCode);
    }

    /// <summary>
    /// Represents a discovered and loaded Add-on DLL module in the application.
    /// </summary>
    public class AddonEntry : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _description;
        private string _version;
        private string _author;
        private string _icon;
        private string _dllPath;
        private string _displayPath;
        private bool _isEnabled;
        private string _activeLabel;
        private string _removeLabel;

        public event PropertyChangedEventHandler PropertyChanged;

        public IOptiAssistAddon Instance { get; set; }
        public FrameworkElement CachedView { get; set; }
        public RadioButton SidebarButton { get; set; }

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

        public string Version
        {
            get { return _version; }
            set { _version = value; OnPropertyChanged("Version"); }
        }

        public string Author
        {
            get { return _author; }
            set { _author = value; OnPropertyChanged("Author"); }
        }

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; OnPropertyChanged("Icon"); }
        }

        public string DllPath
        {
            get { return _dllPath; }
            set { _dllPath = value; OnPropertyChanged("DllPath"); }
        }

        public string DisplayPath
        {
            get { return _displayPath; }
            set { _displayPath = value; OnPropertyChanged("DisplayPath"); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; OnPropertyChanged("IsEnabled"); }
        }

        public string ActiveLabel
        {
            get { return _activeLabel; }
            set { _activeLabel = value; OnPropertyChanged("ActiveLabel"); }
        }

        public string RemoveLabel
        {
            get { return _removeLabel; }
            set { _removeLabel = value; OnPropertyChanged("RemoveLabel"); }
        }

        public AddonEntry()
        {
            _id = "";
            _name = "";
            _description = "";
            _version = "1.0.0";
            _author = "OptiAssist";
            _icon = "🧩";
            _dllPath = "";
            _displayPath = "";
            _isEnabled = true;
            _activeLabel = "Active";
            _removeLabel = "🗑️ Remove";
        }

        public FrameworkElement GetOrCreateView()
        {
            if (CachedView == null && Instance != null)
            {
                CachedView = Instance.CreateView();
            }
            return CachedView;
        }

        public void RefreshMetadataFromInstance()
        {
            if (Instance != null)
            {
                Name = Instance.Name;
                Description = Instance.Description;
                Version = Instance.Version;
                Author = Instance.Author;
                Icon = Instance.Icon;
                if (SidebarButton != null)
                {
                    SidebarButton.Content = string.Format("{0}  {1}", Icon, Name);
                }
            }
            ActiveLabel = LocalizationHelper.Get("Addons_Label_Active", "Active");
            RemoveLabel = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");
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
    /// Discovers, loads, installs, and removes compiled .dll Add-on modules from Config\Addons.
    /// </summary>
    public static class AddonHelper
    {
        static AddonHelper()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly hostAsm = typeof(AddonHelper).Assembly;
            if (args != null && !string.IsNullOrEmpty(args.Name))
            {
                if (args.Name.StartsWith("OptiAssist,", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args.Name, hostAsm.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return hostAsm;
                }
            }
            return null;
        }

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

        public static List<AddonEntry> LoadDiscoveredAddons(IOptiAssistHost host, List<string> disabledAddonIds)
        {
            List<AddonEntry> list = new List<AddonEntry>();
            string addonsDir = GetAddonsDirectory();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            List<string> dllFiles = new List<string>();
            try
            {
                // Scan top-level Config\Addons\*.dll
                foreach (string f in Directory.GetFiles(addonsDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    dllFiles.Add(f);
                }

                // Scan subdirectories Config\Addons\<ModuleName>\*.dll
                foreach (string subDir in Directory.GetDirectories(addonsDir))
                {
                    foreach (string f in Directory.GetFiles(subDir, "*.dll", SearchOption.TopDirectoryOnly))
                    {
                        dllFiles.Add(f);
                    }
                }
            }
            catch { }

            foreach (string dllPath in dllFiles)
            {
                try
                {
                    // Load assembly from byte array so the .dll file on disk is never locked by the process
                    byte[] rawBytes = File.ReadAllBytes(dllPath);
                    Assembly asm = Assembly.Load(rawBytes);

                    Type[] types = asm.GetExportedTypes();
                    foreach (Type t in types)
                    {
                        if (typeof(IOptiAssistAddon).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            IOptiAssistAddon addon = (IOptiAssistAddon)Activator.CreateInstance(t);
                            if (host != null)
                            {
                                addon.Initialize(host);
                            }

                            string relPath = dllPath;
                            if (dllPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                            {
                                relPath = dllPath.Substring(baseDir.Length).TrimStart('\\', '/');
                            }

                            bool enabled = true;
                            if (disabledAddonIds != null && disabledAddonIds.Contains(addon.Id))
                            {
                                enabled = false;
                            }

                            AddonEntry entry = new AddonEntry();
                            entry.Instance = addon;
                            entry.Id = addon.Id;
                            entry.Name = addon.Name;
                            entry.Description = addon.Description;
                            entry.Version = addon.Version;
                            entry.Author = addon.Author;
                            entry.Icon = addon.Icon;
                            entry.DllPath = dllPath;
                            entry.DisplayPath = relPath;
                            entry.IsEnabled = enabled;
                            entry.ActiveLabel = LocalizationHelper.Get("Addons_Label_Active", "Active");
                            entry.RemoveLabel = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");

                            list.Add(entry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (host != null)
                    {
                        host.Log(string.Format("[ADDON LOAD ERROR] {0}: {1}", Path.GetFileName(dllPath), ex.Message));
                    }
                }
            }

            return list;
        }

        public static bool InstallAddonFromDll(string sourceDllPath, IOptiAssistHost host, out string installedAddonName, out string error)
        {
            installedAddonName = "";
            error = "";

            if (string.IsNullOrEmpty(sourceDllPath) || !File.Exists(sourceDllPath))
            {
                error = "The selected DLL file does not exist.";
                return false;
            }

            try
            {
                byte[] rawBytes = File.ReadAllBytes(sourceDllPath);
                Assembly asm = Assembly.Load(rawBytes);

                IOptiAssistAddon validAddon = null;
                foreach (Type t in asm.GetExportedTypes())
                {
                    if (typeof(IOptiAssistAddon).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    {
                        validAddon = (IOptiAssistAddon)Activator.CreateInstance(t);
                        break;
                    }
                }

                if (validAddon == null)
                {
                    error = "The selected DLL does not contain a valid IOptiAssistAddon module implementation.";
                    return false;
                }

                if (host != null)
                {
                    validAddon.Initialize(host);
                }

                installedAddonName = validAddon.Name;
                string moduleFolderName = Path.GetFileNameWithoutExtension(sourceDllPath);
                string targetDir = Path.Combine(GetAddonsDirectory(), moduleFolderName);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string targetDllPath = Path.Combine(targetDir, Path.GetFileName(sourceDllPath));
                if (!string.Equals(Path.GetFullPath(sourceDllPath), Path.GetFullPath(targetDllPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceDllPath, targetDllPath, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RemoveAddon(AddonEntry entry, out string error)
        {
            error = "";
            if (entry == null || string.IsNullOrEmpty(entry.DllPath))
            {
                error = "Invalid module entry.";
                return false;
            }

            try
            {
                if (File.Exists(entry.DllPath))
                {
                    File.SetAttributes(entry.DllPath, FileAttributes.Normal);
                    File.Delete(entry.DllPath);
                }

                string parentDir = Path.GetDirectoryName(entry.DllPath);
                string addonsRoot = GetAddonsDirectory();

                // If the module was inside its own subdirectory in Config\Addons\<ModuleName> and that directory is now empty, remove it
                if (!string.IsNullOrEmpty(parentDir) &&
                    !string.Equals(Path.GetFullPath(parentDir).TrimEnd('\\'), Path.GetFullPath(addonsRoot).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(parentDir))
                {
                    string[] remainingFiles = Directory.GetFiles(parentDir);
                    string[] remainingDirs = Directory.GetDirectories(parentDir);
                    if (remainingFiles.Length == 0 && remainingDirs.Length == 0)
                    {
                        Directory.Delete(parentDir, false);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
