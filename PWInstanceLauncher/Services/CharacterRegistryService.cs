using PWInstanceLauncher.Models;
using System.Collections.ObjectModel;

namespace PWInstanceLauncher.Services
{
    internal sealed class CharacterRegistryService
    {
        public bool IsLoginUnique(ObservableCollection<CharacterProfile> characters, string login, CharacterProfile? current = null)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return true;
            }

            return !characters.Any(c =>
                !ReferenceEquals(c, current) &&
                string.Equals(c.Login, login, StringComparison.OrdinalIgnoreCase));
        }

        public CharacterProfileSnapshot CreateSnapshot(CharacterProfile profile)
        {
            return new CharacterProfileSnapshot(profile.Name, profile.Login, profile.EncryptedPassword, profile.ImagePath);
        }

        public void Restore(CharacterProfile profile, CharacterProfileSnapshot snapshot)
        {
            profile.Name = snapshot.Name;
            profile.Login = snapshot.Login;
            profile.EncryptedPassword = snapshot.EncryptedPassword;
            profile.ImagePath = snapshot.ImagePath;
        }
    }

    internal readonly record struct CharacterProfileSnapshot(
        string Name,
        string Login,
        string EncryptedPassword,
        string ImagePath);
}
