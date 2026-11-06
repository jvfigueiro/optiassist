using System;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace OptiAssist
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                MessageBox.Show("Critical system error:\n\n" + (ex != null ? ex.Message : "Unknown"),
                    "OptiAssist - Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("Unhandled exception:\n\n" + args.Exception.Message,
                    "OptiAssist - Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            if (!IsRunningAsAdministrator())
            {
                // Attempt to load language preference
                try
                {
                    AppSettings settings = SettingsHelper.Load();
                    string lang = settings != null && !string.IsNullOrEmpty(settings.Language) ? settings.Language : "en-US";
                    LocalizationHelper.LoadLanguage(lang);
                }
                catch { }

                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    if (e.Args != null && e.Args.Length > 0)
                    {
                        psi.Arguments = string.Join(" ", e.Args);
                    }

                    System.Diagnostics.Process.Start(psi);
                    this.Shutdown();
                    return;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User denied the UAC elevation prompt (NativeErrorCode 1223: ERROR_CANCELLED)
                    string title = LocalizationHelper.Get("Msg_AdminRequiredTitle", "OptiAssist - Administrator Permission Required");
                    string message = LocalizationHelper.Get("Msg_AdminRequiredBody",
                        "Administrator permission was refused or cancelled.\n\n" +
                        "OptiAssist is a system maintenance, diagnostic, and post-installation utility that requires administrative privileges to operate.\n\n" +
                        "Please grant Administrator privileges when prompted to use OptiAssist.");

                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.Shutdown();
                    return;
                }
                catch (Exception ex)
                {
                    string title = LocalizationHelper.Get("Msg_AdminRequiredTitle", "OptiAssist - Administrator Permission Required");
                    MessageBox.Show("Failed to elevate to Administrator:\n\n" + ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Shutdown();
                    return;
                }
            }

            // Check for command-line arguments (e.g. uninstaller registration in Windows Programs & Features)
            if (e.Args != null && e.Args.Length > 0)
            {
                foreach (string arg in e.Args)
                {
                    if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            AppSettings settings = SettingsHelper.Load();
                            string lang = settings != null && !string.IsNullOrEmpty(settings.Language) ? settings.Language : "en-US";
                            LocalizationHelper.LoadLanguage(lang);
                        }
                        catch { }

                        AppDeploymentHelper.UninstallOptiAssist();
                        this.Shutdown();
                        return;
                    }
                }
            }
        }

        private static bool IsRunningAsAdministrator()
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
}
