using Microsoft.Win32;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;
using PWInstanceLauncher.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private void LaunchCharacter(CharacterProfile profile)
        {
            var existing = Process.GetProcessesByName("elementclient");

            if (existing.Any())
            {
                return; // тут пізніше буде логіка Desktop
            }

            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            _processService.Launch(Config.GamePath, profile.Login, password);
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
