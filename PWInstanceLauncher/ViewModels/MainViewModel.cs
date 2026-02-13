using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;

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
            Characters = new ObservableCollection<CharacterProfile>(Config.Characters);

            AddCharacterCommand = new RelayCommand(AddCharacter);
            LaunchCommand = new RelayCommand<CharacterProfile>(LaunchCharacter);
        }

        private void AddCharacter()
        {
            var profile = new CharacterProfile
            {
                Name = "New Character",
                Login = "",
                EncryptedPassword = ""
            };

            Characters.Add(profile);
            Config.Characters = Characters.ToList();
            _configService.Save(Config);
        }

        private void LaunchCharacter(CharacterProfile profile)
        {
            var password = _credentialService.Decrypt(profile.EncryptedPassword);
            _processService.Launch(Config.GamePath, profile.Login, password);
        }
    }
}
