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
    /// Represents a discovered and loaded Add-on DLL module in the application,
    /// including both runtime IOptiAssistAddon properties and compiled AssemblyInfo metadata.
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
        private bool _canModify;
        private string _activeLabel;
        private string _removeLabel;
        private string _statusBadgeText;

        // Compiled AssemblyInfo Metadata
        private string _assemblyTitle;
        private string _assemblyDescription;
        private string _assemblyCompany;
        private string _assemblyProduct;
        private string _assemblyCopyright;
        private string _assemblyVersion;

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
            set
            {
                _isEnabled = value;
                OnPropertyChanged("IsEnabled");
                UpdateStatusBadgeText();
            }
        }

        public bool CanModify
        {
            get { return _canModify; }
            set
            {
                _canModify = value;
                OnPropertyChanged("CanModify");
                UpdateStatusBadgeText();
            }
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

        public string StatusBadgeText
        {
            get { return _statusBadgeText; }
            set { _statusBadgeText = value; OnPropertyChanged("StatusBadgeText"); }
        }

        public string AssemblyTitle
        {
            get { return _assemblyTitle; }
            set { _assemblyTitle = value; OnPropertyChanged("AssemblyTitle"); }
        }

        public string AssemblyDescription
        {
            get { return _assemblyDescription; }
            set { _assemblyDescription = value; OnPropertyChanged("AssemblyDescription"); }
        }

        public string AssemblyCompany
        {
            get { return _assemblyCompany; }
            set { _assemblyCompany = value; OnPropertyChanged("AssemblyCompany"); }
        }

        public string AssemblyProduct
        {
            get { return _assemblyProduct; }
            set { _assemblyProduct = value; OnPropertyChanged("AssemblyProduct"); }
        }

        public string AssemblyCopyright
        {
            get { return _assemblyCopyright; }
            set { _assemblyCopyright = value; OnPropertyChanged("AssemblyCopyright"); }
        }

        public string AssemblyVersion
        {
            get { return _assemblyVersion; }
            set { _assemblyVersion = value; OnPropertyChanged("AssemblyVersion"); }
        }

        public AddonEntry()
        {
            _id = "";
            _name = "";
            _description = "";
            _version = "1.1.0.0";
            _author = "João Figueiró";
            _icon = "🧩";
            _dllPath = "";
            _displayPath = "";
            _isEnabled = true;
            _canModify = true;
            _activeLabel = "Active";
            _removeLabel = "🗑️ Remove";
            _statusBadgeText = "Active";

            _assemblyTitle = "";
            _assemblyDescription = "";
            _assemblyCompany = "";
            _assemblyProduct = "";
            _assemblyCopyright = "";
            _assemblyVersion = "1.1.0.0";
        }

        public void UpdateStatusBadgeText()
        {
            if (!_canModify)
            {
                StatusBadgeText = LocalizationHelper.Get("Addons_Badge_Core", "Core Module");
            }
            else if (_isEnabled)
            {
                StatusBadgeText = LocalizationHelper.Get("Addons_Badge_Active", "Active");
            }
            else
            {
                StatusBadgeText = LocalizationHelper.Get("Addons_Badge_Disabled", "Disabled");
            }
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
                Version = !string.IsNullOrEmpty(AssemblyVersion) ? AssemblyVersion : Instance.Version;
                Author = !string.IsNullOrEmpty(AssemblyCompany) ? AssemblyCompany : Instance.Author;
                Icon = Instance.Icon;
                if (SidebarButton != null)
                {
                    SidebarButton.Content = string.Format("{0}  {1}", Icon, Name);
                }
            }
            ActiveLabel = LocalizationHelper.Get("Addons_Label_Active", "Active");
            RemoveLabel = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");
            UpdateStatusBadgeText();
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

        private static TAttr GetAssemblyAttribute<TAttr>(Assembly asm) where TAttr : Attribute
        {
            try
            {
                object[] attrs = asm.GetCustomAttributes(typeof(TAttr), false);
                if (attrs != null && attrs.Length > 0)
                {
                    return attrs[0] as TAttr;
                }
            }
            catch { }
            return null;
        }

        private static void PopulateAssemblyMetadata(Assembly asm, IOptiAssistAddon addon, AddonEntry entry)
        {
            var titleAttr = GetAssemblyAttribute<AssemblyTitleAttribute>(asm);
            var descAttr = GetAssemblyAttribute<AssemblyDescriptionAttribute>(asm);
            var companyAttr = GetAssemblyAttribute<AssemblyCompanyAttribute>(asm);
            var productAttr = GetAssemblyAttribute<AssemblyProductAttribute>(asm);
            var copyrightAttr = GetAssemblyAttribute<AssemblyCopyrightAttribute>(asm);
            var fileVerAttr = GetAssemblyAttribute<AssemblyFileVersionAttribute>(asm);

            string asmVer = "";
            if (fileVerAttr != null && !string.IsNullOrWhiteSpace(fileVerAttr.Version))
            {
                asmVer = fileVerAttr.Version;
            }
            else if (asm.GetName().Version != null)
            {
                asmVer = asm.GetName().Version.ToString();
            }
            else
            {
                asmVer = addon.Version;
            }

            entry.AssemblyTitle = (titleAttr != null && !string.IsNullOrWhiteSpace(titleAttr.Title)) ? titleAttr.Title : addon.Name;
            entry.AssemblyDescription = (descAttr != null && !string.IsNullOrWhiteSpace(descAttr.Description)) ? descAttr.Description : addon.Description;
            entry.AssemblyCompany = (companyAttr != null && !string.IsNullOrWhiteSpace(companyAttr.Company)) ? companyAttr.Company : addon.Author;
            entry.AssemblyProduct = (productAttr != null && !string.IsNullOrWhiteSpace(productAttr.Product)) ? productAttr.Product : "OptiAssist Modules";
            entry.AssemblyCopyright = (copyrightAttr != null && !string.IsNullOrWhiteSpace(copyrightAttr.Copyright)) ? copyrightAttr.Copyright : "Copyright (C) 2026 " + entry.AssemblyCompany;
            entry.AssemblyVersion = asmVer;

            entry.Version = entry.AssemblyVersion;
            entry.Author = entry.AssemblyCompany;
        }

        private static int GetModuleSortOrder(string addonId)
        {
            if (string.IsNullOrEmpty(addonId)) return 100;
            switch (addonId.ToLowerInvariant())
            {
                case "app-manager":
                case "app_manager":
                case "appmanager":
                    return 1;
                case "toolbox":
                    return 2;
                case "system-cleaner":
                case "system_cleaner":
                    return 3;
                case "maintenance-report":
                case "maintenance_report":
                    return 4;
                default:
                    return 50;
            }
        }

        public static List<AddonEntry> LoadDiscoveredAddons(IOptiAssistHost host, List<string> disabledAddonIds)
        {
            // Ensure per-module language files are merged into LocalizationHelper before initializing addons
            LocalizationHelper.LoadAddonLanguageFiles(host != null ? host.CurrentLanguage : LocalizationHelper.CurrentLanguage);

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

                            bool isCoreToolbox = string.Equals(addon.Id, "toolbox", StringComparison.OrdinalIgnoreCase);

                            bool enabled = true;
                            if (!isCoreToolbox && disabledAddonIds != null && disabledAddonIds.Contains(addon.Id))
                            {
                                enabled = false;
                            }

                            AddonEntry entry = new AddonEntry();
                            entry.Instance = addon;
                            entry.Id = addon.Id;
                            entry.Name = addon.Name;
                            entry.Description = addon.Description;
                            entry.Icon = addon.Icon;
                            entry.DllPath = dllPath;
                            entry.DisplayPath = relPath;
                            entry.CanModify = !isCoreToolbox;
                            entry.IsEnabled = enabled;
                            entry.ActiveLabel = LocalizationHelper.Get("Addons_Label_Active", "Active");
                            entry.RemoveLabel = LocalizationHelper.Get("Btn_RemoveAddon", "🗑️ Remove");

                            PopulateAssemblyMetadata(asm, addon, entry);
                            entry.UpdateStatusBadgeText();

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

            list.Sort((a, b) =>
            {
                int cmp = GetModuleSortOrder(a.Id).CompareTo(GetModuleSortOrder(b.Id));
                if (cmp != 0) return cmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

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

                // Also copy any companion language json files located alongside the source DLL
                string sourceDir = Path.GetDirectoryName(sourceDllPath);
                if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir) &&
                    !string.Equals(Path.GetFullPath(sourceDir).TrimEnd('\\'), Path.GetFullPath(targetDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string jsonFile in Directory.GetFiles(sourceDir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            string destJson = Path.Combine(targetDir, Path.GetFileName(jsonFile));
                            File.Copy(jsonFile, destJson, true);
                        }
                        catch { }
                    }
                }

                LocalizationHelper.LoadAddonLanguageFiles(host != null ? host.CurrentLanguage : LocalizationHelper.CurrentLanguage);

                if (host != null)
                {
                    validAddon.Initialize(host);
                }

                installedAddonName = validAddon.Name;
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

            if (!entry.CanModify || string.Equals(entry.Id, "toolbox", StringComparison.OrdinalIgnoreCase))
            {
                error = "The Toolbox module is an integral part of OptiAssist and cannot be removed.";
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
