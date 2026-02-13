using PWInstanceLauncher.Services;
using System.Windows;
using System.Windows.Threading;

namespace PWInstanceLauncher
{
    public partial class App : Application
    {
        private readonly LogService _logService = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            _logService.Info("Application startup completed.");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logService.Error("Unhandled UI exception", e.Exception);
            MessageBox.Show(
                "Unexpected error occurred. Check logs/app.log for details.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
            Shutdown();
        }
    }
}
