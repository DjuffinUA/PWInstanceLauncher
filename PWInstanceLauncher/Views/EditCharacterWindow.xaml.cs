using System.Windows;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;

namespace PWInstanceLauncher.Views
{
    public partial class EditCharacterWindow : Window
    {
        public CharacterProfile Profile { get; }
        private readonly CredentialService _credentialService = new();
        private readonly bool _isNewProfile;

        public EditCharacterWindow(CharacterProfile profile)
        {
            InitializeComponent();
            Profile = profile;

            _isNewProfile = string.IsNullOrWhiteSpace(profile.Name) &&
                            string.IsNullOrWhiteSpace(profile.Login) &&
                            string.IsNullOrWhiteSpace(profile.EncryptedPassword);

            NameBox.Text = profile.Name;
            LoginBox.Text = profile.Login;

            if (_isNewProfile)
            {
                PasswordHint.Text = "Password is required for new profile";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var login = LoginBox.Text.Trim();
            var passwordInput = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(login))
            {
                MessageBox.Show("Name and login are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isNewProfile && string.IsNullOrWhiteSpace(passwordInput))
            {
                MessageBox.Show("Password is required for a new profile.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Profile.Name = name;
            Profile.Login = login;

            if (!string.IsNullOrWhiteSpace(passwordInput))
            {
                Profile.EncryptedPassword = _credentialService.Encrypt(passwordInput);
            }

            DialogResult = true;
            Close();
        }
    }
}
