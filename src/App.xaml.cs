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
                    string lang = settings != null && !string.IsNullOrEmpty(settings.Language) ? settings.Language : "pt-BR";
                    LocalizationHelper.LoadLanguage(lang);
                }
                catch { }

                string title = LocalizationHelper.Get("Msg_AdminRequiredTitle", "OptiAssist - Permissão de Administrador Necessária");
                string message = LocalizationHelper.Get("Msg_AdminRequiredBody",
                    "O OptiAssist é uma aplicação de manutenção, diagnóstico e pós-instalação que roda exclusivamente com privilégios elevados de Administrador.\n\n" +
                    "A permissão de execução como Administrador não foi autorizada ou o aplicativo foi iniciado sem privilégios elevados.\n\n" +
                    "Por favor, execute o OptiAssist clicando com o botão direito sobre o executável e selecionando 'Executar como administrador'.");

                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
                return;
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
