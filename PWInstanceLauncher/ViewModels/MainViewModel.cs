using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace PWInstanceLauncher.ViewModels
{
    internal class MainViewModel
    {
        private readonly ConfigService _configService = new();
        private readonly CredentialService _credentialService = new();
        private readonly ProcessService _processService = new();
        private readonly DesktopService _desktopService = new();

        public AppConfig Config { get; }
        public ObservableCollection<CharacterProfile> Characters { get; }
        public Array LaunchModes { get; } = Enum.GetValues(typeof(LaunchMode));

        public ICommand AddCharacterCommand { get; }
        public ICommand EditCharacterCommand { get; }
        public ICommand RemoveCharacterCommand { get; }
        public ICommand LaunchCommand { get; }

        public MainViewModel()
        {
            Config = _configService.Load();
            if (!TryEnsureGamePath())
            {
                throw new Exception("Game path not selected.");
            }

            Characters = new ObservableCollection<CharacterProfile>(Config.Characters);

            AddCharacterCommand = new RelayCommand(AddCharacter);
            EditCharacterCommand = new RelayCommand<CharacterProfile>(EditCharacter);
            RemoveCharacterCommand = new RelayCommand<CharacterProfile>(RemoveCharacter);
            LaunchCommand = new RelayCommand<CharacterProfile>(LaunchCharacter);
        }

        private void AddCharacter()
        {
            var profile = new CharacterProfile();
            var window = new EditCharacterWindow(profile);

            if (window.ShowDialog() == true)
            {
                Characters.Add(profile);
                Save();
            }
        }

        private void EditCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                return;
            }

            var window = new EditCharacterWindow(profile);
            if (window.ShowDialog() == true)
            {
                Save();
            }
        }

        private void RemoveCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Remove character '{profile.Name}'?",
                "Remove Character",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Characters.Remove(profile);
                Save();
            }
        }

        private void LaunchCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                ShowWarning("Character profile is not selected.");
                return;
            }

            try
            {
                if (!ValidateLaunchInput(profile))
                {
                    return;
                }

                var existingProcess = _processService.TryFindRunningByLogin(profile.Login);
                if (existingProcess is not null && !existingProcess.HasExited)
                {
                    FocusExistingCharacter(existingProcess, profile.Login);
                    return;
                }

                LaunchNewCharacter(profile);
            }
            catch (FormatException)
            {
                ShowError("Saved password data is invalid. Please edit profile and re-enter password.");
            }
            catch (Exception ex)
            {
                ShowError($"Launch failed: {ex.Message}");
            }
        }

        private bool ValidateLaunchInput(CharacterProfile profile)
        {
            if (!_configService.IsGamePathValid(Config.GamePath) && !TryEnsureGamePath())
            {
                ShowWarning("Game executable is not selected.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.Login))
            {
                ShowWarning("Login is empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.EncryptedPassword))
            {
                ShowWarning("Password is missing for this profile.");
                return false;
            }

            return true;
        }

        private void FocusExistingCharacter(System.Diagnostics.Process process, string login)
        {
            var windowHandle = process.MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(5));
            }

            if (windowHandle == IntPtr.Zero)
            {
                ShowWarning("Running process found, but window handle is unavailable.");
                return;
            }

            if (Config.LaunchMode == LaunchMode.SeparateDesktop)
            {
                var switched = _desktopService.TrySwitchToCharacterDesktop(login, windowHandle)
                               || _desktopService.SwitchToDesktopWithWindow(windowHandle);

                if (!switched)
                {
                    _desktopService.ActivateWindow(windowHandle);
                    ShowWarning("Could not switch desktop. Window has been activated on current desktop.");
                }

                return;
            }

            _desktopService.MoveWindowToCurrentDesktop(windowHandle);
        }

        private void LaunchNewCharacter(CharacterProfile profile)
        {
            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            var process = _processService.Launch(Config.GamePath, profile.Login, password);

            var windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(30));
            if (windowHandle == IntPtr.Zero)
            {
                ShowWarning("Process started, but main window handle was not detected within timeout.");
                return;
            }

            if (Config.LaunchMode == LaunchMode.SeparateDesktop)
            {
                _desktopService.PlaceWindowOnCharacterDesktop(profile.Login, windowHandle);
            }
            else
            {
                _desktopService.MoveWindowToCurrentDesktop(windowHandle);
            }

            MessageBox.Show(
                $"Character '{profile.Name}' launched successfully (PID: {process.Id}).",
                "Launch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private bool TryEnsureGamePath()
        {
            if (_configService.IsGamePathValid(Config.GamePath))
            {
                return true;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe",
                Title = "Select elementclient.exe"
            };

            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            Config.GamePath = dialog.FileName;
            Save();
            return true;
        }

        private static void ShowWarning(string text)
        {
            MessageBox.Show(text, "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowError(string text)
        {
            MessageBox.Show(text, "Launch", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void Save()
        {
            Config.Characters = Characters.ToList();
            _configService.Save(Config);
        }
    }
}
