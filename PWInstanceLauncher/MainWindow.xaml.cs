using PWInstanceLauncher.Models;
using PWInstanceLauncher.ViewModels;
using PWInstanceLauncher.Views;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PWInstanceLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CharacterProfile profile)
            {
                var window = new EditCharacterWindow(profile);
                if (window.ShowDialog() == true)
                {
                    ((MainViewModel)DataContext).Save();
                }
            }
        }
    }
}