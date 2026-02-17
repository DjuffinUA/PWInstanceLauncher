using Microsoft.Win32;
using System.Windows;

namespace PWInstanceLauncher.Services
{
    internal sealed class UserDialogService : IUserDialogService
    {
        public bool TrySelectGameExecutable(out string path)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe",
                Title = "Select elementclient.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                path = dialog.FileName;
                return true;
            }

            path = string.Empty;
            return false;
        }

        public MessageBoxResult Show(string text, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            return MessageBox.Show(text, caption, buttons, image);
        }
    }
}
