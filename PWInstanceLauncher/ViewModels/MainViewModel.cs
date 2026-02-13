using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PWInstanceLauncher.ViewModels
{
    internal class MainViewModel
    {
        private readonly ConfigService _configService = new();
        private readonly CredentialService _credentialService = new();
        private readonly ProcessService _processService = new();

        public AppConfig Config { get; set; }

        public ObservableCollection<CharacterProfile> Characters { get; set; }

        public ICommand AddCharacterCommand { get; }
        public ICommand LaunchCommand { get; }

        public MainViewModel()
        {
            Config = _configService.Load();
            EnsureGamePath();
            Characters = new ObservableCollection<CharacterProfile>(Config.Characters);

            AddCharacterCommand = new RelayCommand(AddCharacter);
            LaunchCommand = new RelayCommand<CharacterProfile>(LaunchCharacter);
        }

        private void AddCharacter()
        {
            var profile = new CharacterProfile();

            var window = new EditCharacterWindow(profile);

            if (window.ShowDialog() == true)
            {
                Characters.Add(profile);
                Config.Characters = Characters.ToList();
                _configService.Save(Config);
            }
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
                if (!_processService.IsGameExecutableValid(Config.GamePath))
                {
                    EnsureGamePath();

                    if (!_processService.IsGameExecutableValid(Config.GamePath))
                    {
                        MessageBox.Show("Game executable is invalid.", "Launch", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(profile.Login))
                {
                    MessageBox.Show("Login is empty.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingProcess = _processService.TryFindRunningByLogin(profile.Login);
                if (existingProcess is not null)
                {
                    MessageBox.Show(
                        $"Character with login '{profile.Login}' is already running (PID: {existingProcess.Id}).\n" +
                        "Desktop switch/activation will be added on Stage 4.",
                        "Launch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

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

                MessageBox.Show(
                    $"Character '{profile.Name}' launched successfully (PID: {process.Id}).",
                    "Launch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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

        private void EnsureGamePath()
        {
            if (string.IsNullOrWhiteSpace(Config.GamePath) || !File.Exists(Config.GamePath))
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Executable (*.exe)|*.exe",
                    Title = "Select elementclient.exe"
                };

                if (dialog.ShowDialog() == true)
                {
                    Config.GamePath = dialog.FileName;
                    _configService.Save(Config);
                }
                else
                {
                    throw new Exception("Game path not selected.");
                }
            }
        }

        public void Save()
        {
            Config.Characters = Characters.ToList();
            _configService.Save(Config);
        }
    }
}
