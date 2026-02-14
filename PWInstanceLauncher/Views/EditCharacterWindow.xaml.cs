using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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

            var imageOptions = BuildImageOptions();
            ImagePathBox.ItemsSource = imageOptions;
            if (!string.IsNullOrWhiteSpace(profile.ImagePath))
            {
                var selectedOption = imageOptions.FirstOrDefault(option =>
                    string.Equals(option.Path, profile.ImagePath, StringComparison.OrdinalIgnoreCase));
                ImagePathBox.SelectedValue = selectedOption?.Path ?? profile.ImagePath;
            }

            UpdateImagePreview();

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
            Profile.ImagePath = (ImagePathBox.SelectedValue as string) ?? Profile.ImagePath;

            if (!string.IsNullOrWhiteSpace(passwordInput))
            {
                Profile.EncryptedPassword = _credentialService.Encrypt(passwordInput);
            }

            DialogResult = true;
            Close();
        }

        private void ImagePathBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateImagePreview();
        }

        private void UpdateImagePreview()
        {
            var selectedPath = ImagePathBox.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                SelectedImagePreview.Source = null;
                return;
            }

            SelectedImagePreview.Source = new BitmapImage(new Uri(selectedPath, UriKind.Relative));
        }

        private static List<ImageOption> BuildImageOptions()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.g.resources";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return [];
            }

            using var reader = new ResourceReader(stream);
            return reader.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => entry.Key?.ToString())
                .Where(key => !string.IsNullOrWhiteSpace(key) && key.StartsWith("imeges/clas/", StringComparison.OrdinalIgnoreCase) && key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Select(key => key!)
                .OrderBy(key => key)
                .Select(key => new ImageOption
                {
                    Path = $"/{key}",
                    DisplayName = Path.GetFileNameWithoutExtension(key)
                })
                .ToList();
        }

        private sealed class ImageOption
        {
            public string Path { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
        }
    }
}
