using Avalonia.Controls;
using ImageLabeller.ViewModels;
using System;
using System.Reflection;

namespace ImageLabeller
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            // Set window title with version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"Image Labeller v{version?.Major}.{version?.Minor}.{version?.Build}";

            // Subscribe to window events
            Opened += OnWindowOpened;
            Closing += OnWindowClosing;
            PropertyChanged += OnWindowPropertyChanged;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                // Restore window size
                Width = _viewModel.Settings.WindowWidth;
                Height = _viewModel.Settings.WindowHeight;

                // Restore window position if valid
                if (!double.IsNaN(_viewModel.Settings.WindowX) && !double.IsNaN(_viewModel.Settings.WindowY))
                {
                    Position = new Avalonia.PixelPoint(
                        (int)_viewModel.Settings.WindowX,
                        (int)_viewModel.Settings.WindowY
                    );
                }
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowBounds();
        }

        private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            // Save window bounds when size changes
            if (e.Property == WidthProperty || e.Property == HeightProperty)
            {
                SaveWindowBounds();
            }
        }

        private void SaveWindowBounds()
        {
            if (_viewModel != null)
            {
                _viewModel.Settings.WindowWidth = Width;
                _viewModel.Settings.WindowHeight = Height;
                _viewModel.Settings.WindowX = Position.X;
                _viewModel.Settings.WindowY = Position.Y;
                _viewModel.SaveSettings();
            }
        }
    }
}