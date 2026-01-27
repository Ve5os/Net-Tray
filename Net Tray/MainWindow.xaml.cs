using System.Windows;

namespace NetTray
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Это окно нужно только как владелец для трея
        }
    }
}