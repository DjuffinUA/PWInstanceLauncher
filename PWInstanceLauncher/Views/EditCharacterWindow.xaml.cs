using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PWInstanceLauncher.Models;
using PWInstanceLauncher.Services;

namespace PWInstanceLauncher.Views
{
    /// <summary>
    /// Логика взаимодействия для EditCharacterWindow.xaml
    /// </summary>
    public partial class EditCharacterWindow : Window
    {
        public CharacterProfile Profile { get; private set; }
        private readonly CredentialService _credentialService = new();

        public EditCharacterWindow(CharacterProfile profile)
        {
            InitializeComponent();
            Profile = profile;

            NameBox.Text = profile.Name;
            LoginBox.Text = profile.Login;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Profile.Name = NameBox.Text;
            Profile.Login = LoginBox.Text;
            Profile.EncryptedPassword = _credentialService.Encrypt(PasswordBox.Password);

            DialogResult = true;
            Close();
        }
    }
}
