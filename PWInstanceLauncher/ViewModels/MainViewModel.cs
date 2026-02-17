using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PWInstanceLauncher.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private readonly ConfigService _configService = new();
        private readonly LogService _logService = new();
        private readonly LauncherCoordinator _launcherCoordinator;
        private readonly DispatcherTimer _monitorTimer;

        private string _statusMessage = "Ready";

        public AppConfig Config { get; }
        public ObservableCollection<CharacterProfile> Characters { get; }
        public Array LaunchModes { get; } = Enum.GetValues(typeof(LaunchMode));

        public LaunchMode SelectedLaunchMode
        {
            get => Config.LaunchMode;
            set
            {
                if (Config.LaunchMode == value)
                {
                    return;
                }

                Config.LaunchMode = value;
                Save();
                OnPropertyChanged();
                SetInfo($"Launch mode changed to '{value}'.");
                _logService.Info($"Launch mode changed to '{value}'.");
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public string GamePathDisplay => Config.GamePath;

        public ICommand AddCharacterCommand { get; }
        public ICommand EditCharacterCommand { get; }
        public ICommand RemoveCharacterCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand ChangeGamePathCommand { get; }

        public MainViewModel()
        {
            var processService = new ProcessService();
            var desktopService = new DesktopService();
            var credentialService = new CredentialService();
            _launcherCoordinator = new LauncherCoordinator(processService, desktopService, credentialService);

            Config = _configService.Load();
            Characters = new ObservableCollection<CharacterProfile>(Config.Characters);
            _logService.Info("Application started. Config loaded.");

            if (!TryEnsureGamePath())
            {
                SetInfo("Game executable is not selected.");
                _logService.Warn("Game path is not selected on startup.");
            }

            AddCharacterCommand = new RelayCommand(AddCharacter);
            EditCharacterCommand = new RelayCommand<CharacterProfile>(EditCharacter);
            RemoveCharacterCommand = new RelayCommand<CharacterProfile>(RemoveCharacter);
            LaunchCommand = new RelayCommand<CharacterProfile>(LaunchCharacter);
            ChangeGamePathCommand = new RelayCommand(ChangeGamePath);

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _monitorTimer.Tick += (_, _) => MonitorRunningProcessesSafe();

            _launcherCoordinator.InitializeRuntimeState(Characters);
            _logService.Info("Runtime state initialized.");

            _monitorTimer.Start();
            if (_configService.IsGamePathValid(Config.GamePath))
            {
                SetInfo("Monitoring started.");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void MonitorRunningProcessesSafe()
        {
            try
            {
                var updates = _launcherCoordinator.MonitorRunningProcesses(Characters);
                if (updates.Count > 0)
                {
                    SetInfo(updates[^1]);
                }
            }
            catch (Exception ex)
            {
                _logService.Error("Process monitor tick failed", ex);
            }
        }

        private void AddCharacter()
        {
            var profile = new CharacterProfile();
            var window = new EditCharacterWindow(profile);

            if (window.ShowDialog() == true)
            {
                if (!IsLoginUnique(profile.Login))
                {
                    ShowWarning($"Login '{profile.Login}' already exists. Use unique login.");
                    return;
                }

                profile.RuntimeStatus = "Offline";
                Characters.Add(profile);
                Save();
                SetInfo($"Character '{profile.Name}' added.");
                _logService.Info($"Character '{profile.Name}' added.");
            }
        }

        private void EditCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                return;
            }

            var oldName = profile.Name;
            var oldLogin = profile.Login;
            var oldEncryptedPassword = profile.EncryptedPassword;
            var oldImagePath = profile.ImagePath;

            var window = new EditCharacterWindow(profile);
            if (window.ShowDialog() == true)
            {
                if (!IsLoginUnique(profile.Login, profile))
                {
                    ShowWarning($"Login '{profile.Login}' already exists. Use unique login.");
                    profile.Name = oldName;
                    profile.Login = oldLogin;
                    profile.EncryptedPassword = oldEncryptedPassword;
                    profile.ImagePath = oldImagePath;
                    return;
                }

                if (!string.Equals(oldLogin, profile.Login, StringComparison.OrdinalIgnoreCase))
                {
                    _launcherCoordinator.HandleLoginChange(oldLogin, profile.Login);
                    profile.RuntimeStatus = "Offline";
                }

                Save();
                SetInfo($"Character '{profile.Name}' updated.");
                _logService.Info($"Character '{profile.Name}' updated.");
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
                _launcherCoordinator.CleanupRuntimeMappings(profile.Login, forceDesktopUnassign: true);
                Characters.Remove(profile);
                Save();
                SetInfo($"Character '{profile.Name}' removed.");
                _logService.Info($"Character '{profile.Name}' removed.");
            }
        }

        private void ChangeGamePath()
        {
            var oldPath = Config.GamePath;
            if (!TryEnsureGamePath(forcePrompt: true))
            {
                return;
            }

            OnPropertyChanged(nameof(GamePathDisplay));
            SetInfo("Game path updated.");
            _logService.Info($"Game path changed from '{oldPath}' to '{Config.GamePath}'.");
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

                var result = _launcherCoordinator.LaunchOrFocus(profile, Config.GamePath, Config.LaunchMode);
                SetInfo(result.Message);

                if (result.ActionType == LaunchActionType.Warning)
                {
                    ShowWarning(result.Message);
                    return;
                }

                if (result.ActionType == LaunchActionType.LaunchedNew)
                {
                    _logService.Info($"Character '{profile.Name}' launched with PID {result.ProcessId}.");
                    MessageBox.Show(
                        $"Character '{profile.Name}' launched successfully (PID: {result.ProcessId}).",
                        "Launch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SetInfo($"Focused running character '{profile.Name}'.");
            }
            catch (FormatException)
            {
                ShowError("Saved password data is invalid. Please edit profile and re-enter password.");
            }
            catch (Exception ex)
            {
                ShowError($"Launch failed: {ex.Message}");
                _logService.Error($"Launch failed for login '{profile.Login}'", ex);
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

        private bool IsLoginUnique(string login, CharacterProfile? current = null)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return true;
            }

            return !Characters.Any(c =>
                !ReferenceEquals(c, current) &&
                string.Equals(c.Login, login, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryEnsureGamePath(bool forcePrompt = false)
        {
            if (!forcePrompt && _configService.IsGamePathValid(Config.GamePath))
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

        private void SetInfo(string text)
        {
            StatusMessage = text;
        }

        private void ShowWarning(string text)
        {
            SetInfo(text);
            MessageBox.Show(text, "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
            _logService.Warn(text);
        }

        private void ShowError(string text)
        {
            SetInfo(text);
            MessageBox.Show(text, "Launch", MessageBoxButton.OK, MessageBoxImage.Error);
            _logService.Error(text);
        }

        public void Save()
        {
            Config.Characters = Characters.ToList();
            _configService.Save(Config);
            _launcherCoordinator.PruneUnknownLogins(Characters.Select(c => c.Login));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
