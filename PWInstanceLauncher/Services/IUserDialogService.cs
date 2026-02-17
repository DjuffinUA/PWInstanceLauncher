using System.Windows;

namespace PWInstanceLauncher.Services
{
    internal interface IUserDialogService
    {
        bool TrySelectGameExecutable(out string path);
        MessageBoxResult Show(string text, string caption, MessageBoxButton buttons, MessageBoxImage image);
    }
}
