using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PWInstanceLauncher.Models
{
    public class CharacterProfile : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _login = string.Empty;
        private string _encryptedPassword = string.Empty;
        private string _runtimeStatus = "Offline";

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string Login
        {
            get => _login;
            set => SetField(ref _login, value);
        }

        public string EncryptedPassword
        {
            get => _encryptedPassword;
            set => SetField(ref _encryptedPassword, value);
        }

        [JsonIgnore]
        public string RuntimeStatus
        {
            get => _runtimeStatus;
            set => SetField(ref _runtimeStatus, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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
    }
}
