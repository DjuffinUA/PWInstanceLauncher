using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PWInstanceLauncher.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
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
        public ICommand ChangeGamePathCommand { get; }

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

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _monitorTimer.Tick += (_, _) => MonitorRunningProcesses();

            InitializeRuntimeState();
            _monitorTimer.Start();
        }

        private void InitializeRuntimeState()
        {
            foreach (var profile in Characters)
            {
                SetStatus(profile, "Offline");

                if (string.IsNullOrWhiteSpace(profile.Login))
                {
                    continue;
                }

                var process = _processService.TryFindRunningByLogin(profile.Login);
                if (process is null || process.HasExited)
                {
                    continue;
                }

                RegisterRunningProcess(profile.Login, process.Id);
                SetStatus(profile, "Running");
            }
        }

        private void MonitorRunningProcesses()
        {
            foreach (var login in _runningProcessByLogin.Keys.ToList())
            {
                if (!IsProcessAlive(_runningProcessByLogin[login]))
                {
                    _runningProcessByLogin.Remove(login);
                    SetStatusByLogin(login, "Offline");
                }
            }

            foreach (var profile in Characters)
            {
                if (string.IsNullOrWhiteSpace(profile.Login) || _runningProcessByLogin.ContainsKey(profile.Login))
                {
                    continue;
                }

                var process = _processService.TryFindRunningByLogin(profile.Login);
                if (process is null || process.HasExited)
                {
                    SetStatus(profile, "Offline");
                    continue;
                }

                RegisterRunningProcess(profile.Login, process.Id);
                SetStatus(profile, "Running");
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private void RegisterRunningProcess(string login, int processId)
        {
            _runningProcessByLogin[login] = processId;
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

        private void LaunchCharacter(CharacterProfile? profile)
        {
            if (profile is null)
            {
                MessageBox.Show("Character profile is not selected.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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

                    if (!_configService.IsGamePathValid(Config.GamePath))
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

                if (string.IsNullOrWhiteSpace(profile.EncryptedPassword))
                {
                    MessageBox.Show("Password is missing for this profile.", "Launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingProcess = _processService.TryFindRunningByLogin(profile.Login);
                if (existingProcess is not null && !existingProcess.HasExited)
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
