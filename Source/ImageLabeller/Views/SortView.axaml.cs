using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImageLabeller.ViewModels;
using System.Linq;

namespace ImageLabeller.Views
{
    public partial class SortView : UserControl
    {
        public SortView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is SortViewModel viewModel)
            {
                viewModel.SetFolderPickerCallback(
                    BrowseForSourceFolder,
                    BrowseForDestinationFolder
                );
            }
        }

        private string? BrowseForSourceFolder()
        {
            return BrowseForFolder("Select Source Folder");
        }

        private string? BrowseForDestinationFolder()
        {
            return BrowseForFolder("Select Destination Folder");
        }

        private string? BrowseForFolder(string title)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var result = topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            }).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }
    }
}
