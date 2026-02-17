using PWInstanceLauncher.Models;

namespace PWInstanceLauncher.Services
{
    internal sealed class LaunchValidationService
    {
        private readonly ConfigService _configService;
        private readonly IUserDialogService _dialogService;

        public LaunchValidationService(ConfigService configService, IUserDialogService dialogService)
        {
            _configService = configService;
            _dialogService = dialogService;
        }

        public bool EnsureGamePath(AppConfig config, Action saveAction, bool forcePrompt = false)
        {
            if (!forcePrompt && _configService.IsGamePathValid(config.GamePath))
            {
                return true;
            }

            if (!_dialogService.TrySelectGameExecutable(out var selectedPath))
            {
                return false;
            }

            config.GamePath = selectedPath;
            saveAction();
            return true;
        }

        public bool ValidateLaunchInput(CharacterProfile profile, AppConfig config, Action<string> warnAction, Action saveAction)
        {
            if (!_configService.IsGamePathValid(config.GamePath) && !EnsureGamePath(config, saveAction))
            {
                warnAction("Game executable is not selected.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.Login))
            {
                warnAction("Login is empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.EncryptedPassword))
            {
                warnAction("Password is missing for this profile.");
                return false;
            }

            return true;
        }
    }
}
