using PWInstanceLauncher.ViewModels;
using System.Windows;

namespace PWInstanceLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
