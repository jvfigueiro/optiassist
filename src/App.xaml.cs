using System;
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
                MessageBox.Show("Erro crítico no sistema:\n\n" + (ex != null ? ex.Message : "Desconhecido"),
                    "OptiAssist - Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("Erro não tratado:\n\n" + args.Exception.Message,
                    "OptiAssist - Exceção", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
