using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ImageLabeller.ViewModels;
using System.Linq;

namespace ImageLabeller.Views
{
    public partial class LabelCheckView : UserControl
    {
        public LabelCheckView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is LabelCheckViewModel viewModel)
            {
                viewModel.SetImageFolderPickerCallback(BrowseForImageFolder);
                viewModel.SetLabelFolderPickerCallback(BrowseForLabelFolder);
            }
        }

        private string? BrowseForImageFolder()
        {
            var viewModel = DataContext as LabelCheckViewModel;
            var suggestedPath = viewModel?.SourceImageFolder;
            return BrowseForFolder("Select Source Image Folder", suggestedPath);
        }

        private string? BrowseForLabelFolder()
        {
            var viewModel = DataContext as LabelCheckViewModel;
            var suggestedPath = viewModel?.SourceLabelFolder;
            return BrowseForFolder("Select Source Label Folder", suggestedPath);
        }

        private string? BrowseForFolder(string title, string? suggestedStartLocation = null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            // Set suggested start location if valid path exists
            if (!string.IsNullOrEmpty(suggestedStartLocation))
            {
                try
                {
                    var folder = topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedStartLocation).GetAwaiter().GetResult();
                    if (folder != null)
                    {
                        if (System.IO.Directory.Exists(folder.Path.LocalPath))
                        {
                            options.SuggestedStartLocation = folder;
                        }
                        else
                        {
                            // Notify user that the manually entered folder doesn't exist
                            ShowFolderNotFoundMessage((Window)topLevel, suggestedStartLocation);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // If we can't get the folder, show error and use default location
                    System.Diagnostics.Debug.WriteLine($"Error setting suggested folder: {ex.Message}");
                }
            }

            var result = topLevel.StorageProvider.OpenFolderPickerAsync(options).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        private void ShowFolderNotFoundMessage(Window owner, string folderPath)
        {
            var messageBox = new Window
            {
                Title = "Folder Not Found",
                Width = 450,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Width = 100,
                Padding = new Avalonia.Thickness(10, 8),
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            okButton.Click += (s, e) => messageBox.Close();

            messageBox.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = "The specified folder does not exist:",
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        FontSize = 14
                    },
                    new TextBlock
                    {
                        Text = folderPath,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        Margin = new Avalonia.Thickness(0, 0, 0, 10)
                    },
                    okButton
                }
            };

            messageBox.ShowDialog(owner).GetAwaiter().GetResult();
        }
    }
}
