using System.IO;
using System.Text.Json;
using PWInstanceLauncher.Models;

namespace PWInstanceLauncher.Services
{
    internal class ConfigService
    {
        private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        public AppConfig Load()
        {
            if (!File.Exists(_configPath))
            {
                var newConfig = CreateDefault();
                Save(newConfig);
                return newConfig;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefault();
                var validated = ValidateAndNormalize(config);

                Save(validated);
                return validated;
            }
            catch
            {
                var fallback = CreateDefault();
                Save(fallback);
                return fallback;
            }
        }

        public void Save(AppConfig config)
        {
            var validated = ValidateAndNormalize(config);
            var json = JsonSerializer.Serialize(validated, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configPath, json);
        }

        public bool IsGamePathValid(string? gamePath)
        {
            return !string.IsNullOrWhiteSpace(gamePath) &&
                   File.Exists(gamePath) &&
                   Path.GetExtension(gamePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static AppConfig CreateDefault()
        {
            return new AppConfig();
        }

        private static AppConfig ValidateAndNormalize(AppConfig config)
        {
            config.GamePath ??= string.Empty;
            config.Characters ??= new List<CharacterProfile>();

            var normalizedCharacters = new List<CharacterProfile>();
            foreach (var character in config.Characters)
            {
                if (character is null)
                {
                    continue;
                }

                normalizedCharacters.Add(new CharacterProfile
                {
                    Name = character.Name?.Trim() ?? string.Empty,
                    Login = character.Login?.Trim() ?? string.Empty,
                    EncryptedPassword = character.EncryptedPassword ?? string.Empty
                });
            }

            config.Characters = normalizedCharacters;
            return config;
        }
    }
}
