using System;
using System.ComponentModel;
using System.IO;

namespace OptiAssist.Addons.StartupManager
{
    public enum StartupLocationType
    {
        RegistryCurrentUser,
        RegistryLocalMachine,
        RegistryLocalMachine32,
        StartupFolderUser,
        StartupFolderCommon
    }

    /// <summary>
    /// Represents a program configured to run at user logon or Windows startup.
    /// </summary>
    public class StartupProgramItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isEnabled;
        private string _name;
        private string _command;

        public string Id { get; set; }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public string Command
        {
            get { return _command; }
            set { _command = value; OnPropertyChanged("Command"); }
        }

        public string ExePath { get; set; }
        public string Publisher { get; set; }
        public string FileDescription { get; set; }
        public StartupLocationType LocationType { get; set; }
        public string LocationDisplay { get; set; }
        public string SourcePath { get; set; }
        public bool FileExists { get; set; }
        public string FileSizeDisplay { get; set; }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                OnPropertyChanged("IsEnabled");
                OnPropertyChanged("StatusDisplay");
            }
        }

        public string StatusDisplay
        {
            get { return _isEnabled ? "Enabled" : "Disabled"; }
        }

        protected void OnPropertyChanged(string propName)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>
    /// Represents a Windows Scheduled Task triggered at logon or system boot.
    /// </summary>
    public class StartupTaskItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isEnabled;

        public string Name { get; set; }
        public string FolderPath { get; set; }
        public string FullTaskPath { get; set; }
        public string ActionPath { get; set; }
        public string Arguments { get; set; }
        public string TriggerType { get; set; }
        public string TriggerDisplay { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string FileDescription { get; set; }
        public bool IsUserProgram { get; set; }
        public bool FileExists { get; set; }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                OnPropertyChanged("IsEnabled");
                OnPropertyChanged("StatusDisplay");
            }
        }

        public string StatusDisplay
        {
            get { return _isEnabled ? "Enabled" : "Disabled"; }
        }

        protected void OnPropertyChanged(string propName)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(propName));
        }
    }
}

