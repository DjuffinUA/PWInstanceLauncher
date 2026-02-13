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
            EnsureGamePath();
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

            if (window.ShowDialog() != true)
            {
                return;
            }

            Characters.Add(profile);
            Save();
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

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            Characters.Remove(profile);
            Save();
        }

        private void LaunchCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                MessageBox.Show("Character profile is not selected.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!_configService.IsGamePathValid(Config.GamePath))
                {
                    EnsureGamePath();
                }

                if (string.IsNullOrWhiteSpace(profile.Login))
                {
                    MessageBox.Show("Login is empty.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(profile.EncryptedPassword))
                {
                    MessageBox.Show("Password is missing for this profile.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(
                    "Saved password data is invalid. Please edit profile and re-enter password.",
                    "Launch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Launch failed: {ex.Message}",
                    "Launch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
                MessageBox.Show("Running process found, but window handle is unavailable.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Config.LaunchMode == LaunchMode.SeparateDesktop)
            {
                var switched = _desktopService.TrySwitchToCharacterDesktop(login, windowHandle);
                if (!switched)
                {
                    switched = _desktopService.SwitchToDesktopWithWindow(windowHandle);
                }

                if (!switched)
                {
                    MessageBox.Show("Could not switch to character desktop, but window was activated.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _desktopService.ActivateWindow(windowHandle);
                }
            }
            else
            {
                _desktopService.ActivateWindow(windowHandle);
            }
        }

        private void LaunchNewCharacter(CharacterProfile profile)
        {
            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            var process = _processService.Launch(Config.GamePath, profile.Login, password);

            var windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(30));
            if (windowHandle == IntPtr.Zero)
            {
                MessageBox.Show(
                    "Process started, but main window handle was not detected within timeout.",
                    "Launch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

        private void EnsureGamePath()
        {
            if (_configService.IsGamePathValid(Config.GamePath))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe",
                Title = "Select elementclient.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                Config.GamePath = dialog.FileName;
                Save();
                return;
            }

            throw new Exception("Game path not selected.");
        }

        public void Save()
        {
            Config.Characters = Characters.ToList();
            _configService.Save(Config);
        }
    }
}
