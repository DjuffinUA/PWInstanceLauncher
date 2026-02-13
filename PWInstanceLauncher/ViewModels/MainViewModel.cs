using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        private readonly LogService _logService = new();
        private readonly Dictionary<string, int> _runningProcessByLogin = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _monitorTimer;

        private string _statusMessage = "Ready";

        public AppConfig Config { get; }
        public ObservableCollection<CharacterProfile> Characters { get; }
        public Array LaunchModes { get; } = Enum.GetValues(typeof(LaunchMode));

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
            Config = _configService.Load();
            _logService.Info("Application started. Config loaded.");

            if (!TryEnsureGamePath())
            {
                _logService.Warn("Game path is not selected on startup.");
                throw new Exception("Game path not selected.");
            }

            Characters = new ObservableCollection<CharacterProfile>(Config.Characters);

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

            InitializeRuntimeState();
            _monitorTimer.Start();
            SetInfo("Monitoring started.");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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

            _logService.Info("Runtime state initialized.");
        }

        private void MonitorRunningProcessesSafe()
        {
            try
            {
                MonitorRunningProcesses();
            }
            catch (Exception ex)
            {
                _logService.Error("Process monitor tick failed", ex);
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
                    SetInfo($"{login} switched to Offline.");
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

            if (window.ShowDialog() == true)
            {
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

            var oldLogin = profile.Login;
            var window = new EditCharacterWindow(profile);
            if (window.ShowDialog() == true)
            {
                if (!string.Equals(oldLogin, profile.Login, StringComparison.OrdinalIgnoreCase))
                {
                    _runningProcessByLogin.Remove(oldLogin);
                    SetStatus(profile, "Offline");
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
                _runningProcessByLogin.Remove(profile.Login);
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

                var existingProcess = _processService.TryFindRunningByLogin(profile.Login);
                if (existingProcess is not null && !existingProcess.HasExited)
                {
                    RegisterRunningProcess(profile.Login, existingProcess.Id);
                    SetStatus(profile, "Running");
                    FocusExistingCharacter(existingProcess, profile.Login);
                    SetInfo($"Focused running character '{profile.Name}'.");
                    return;
                }

                LaunchNewCharacter(profile);
            }
            catch (FormatException)
            {
                ShowError("Saved password data is invalid. Please edit profile and re-enter password.");
                _logService.Warn($"Invalid encrypted password format for login '{profile.Login}'.");
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

        private void FocusExistingCharacter(Process process, string login)
        {
            var windowHandle = process.MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(5));
            }

            if (windowHandle == IntPtr.Zero)
            {
                ShowWarning("Running process found, but window handle is unavailable.");
                _logService.Warn($"Window handle unavailable for running login '{login}'.");
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
                    _logService.Warn($"Desktop switch fallback used for login '{login}'.");
                }

                return;
            }

            _desktopService.MoveWindowToCurrentDesktop(windowHandle);
        }

        private void LaunchNewCharacter(CharacterProfile profile)
        {
            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            var process = _processService.Launch(Config.GamePath, profile.Login, password);
            RegisterRunningProcess(profile.Login, process.Id);
            SetStatus(profile, "Running");

            var windowHandle = _processService.WaitForMainWindowHandle(process, TimeSpan.FromSeconds(30));
            if (windowHandle == IntPtr.Zero)
            {
                ShowWarning("Process started, but main window handle was not detected within timeout.");
                _logService.Warn($"Main window handle timeout for login '{profile.Login}'.");
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

            SetInfo($"Character '{profile.Name}' launched.");
            _logService.Info($"Character '{profile.Name}' launched with PID {process.Id}.");
            MessageBox.Show(
                $"Character '{profile.Name}' launched successfully (PID: {process.Id}).",
                "Launch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

        private void SetStatusByLogin(string login, string status)
        {
            var profile = Characters.FirstOrDefault(c => string.Equals(c.Login, login, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                SetStatus(profile, status);
            }
        }

        private static void SetStatus(CharacterProfile profile, string status)
        {
            profile.RuntimeStatus = status;
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
