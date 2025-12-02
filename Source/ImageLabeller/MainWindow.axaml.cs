using Avalonia.Controls;
using ImageLabeller.ViewModels;

namespace ImageLabeller
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }
}