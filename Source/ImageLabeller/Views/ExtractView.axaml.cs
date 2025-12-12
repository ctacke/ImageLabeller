using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ImageLabeller.ViewModels;
using System.Linq;

namespace ImageLabeller.Views
{
    public partial class ExtractView : UserControl
    {
        private ExtractViewModel? _viewModel;

        public ExtractView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) =>
            {
                // Set focus to enable keyboard input
                Focus();
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ExtractViewModel viewModel)
                return;

            switch (e.Key)
            {
                case Key.I:
                    if (viewModel.SetInPointCommand.CanExecute(null))
                    {
                        viewModel.SetInPointCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.O:
                    if (viewModel.SetOutPointCommand.CanExecute(null))
                    {
                        viewModel.SetOutPointCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Space:
                    if (viewModel.PlayPauseCommand.CanExecute(null))
                    {
                        viewModel.PlayPauseCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Left:
                    if (viewModel.StepBackwardCommand.CanExecute(null))
                    {
                        viewModel.StepBackwardCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Right:
                    if (viewModel.StepForwardCommand.CanExecute(null))
                    {
                        viewModel.StepForwardCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_viewModel != null)
            {
                // Unsubscribe from old view model if needed
            }

            if (DataContext is ExtractViewModel viewModel)
            {
                _viewModel = viewModel;
                viewModel.SetFilePickerCallback(BrowseForVideoFile);
                viewModel.SetFolderPickerCallback(BrowseForOutputFolder);
            }
        }

        private string? BrowseForVideoFile()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var fileTypes = new FilePickerFileType[]
            {
                new FilePickerFileType("Video Files")
                {
                    Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            };

            var result = topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Video File",
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            }).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        private string? BrowseForOutputFolder()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var result = topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Folder for Extracted Frames",
                AllowMultiple = false
            }).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }
    }
}
