using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ImageLabeller.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace ImageLabeller.Views
{
    public partial class ExtractView : UserControl
    {
        private ExtractViewModel? _viewModel;
        private Slider? _timelineSlider;
        private Canvas? _markerCanvas;
        private Path? _inMarker;
        private Path? _outMarker;

        public ExtractView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) =>
            {
                // Set focus to enable keyboard input
                Focus();

                // Get references to named elements
                _timelineSlider = this.FindControl<Slider>("TimelineSlider");
                _markerCanvas = this.FindControl<Canvas>("MarkerCanvas");
                _inMarker = this.FindControl<Path>("InMarker");
                _outMarker = this.FindControl<Path>("OutMarker");

                // Subscribe to size changes to update marker positions
                if (_timelineSlider != null)
                {
                    _timelineSlider.PropertyChanged += OnSliderPropertyChanged;
                }

                // Initial marker update
                UpdateMarkerPositions();
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ExtractViewModel viewModel)
                return;

            // Don't handle keyboard shortcuts if a TextBox has focus
            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focusedElement is TextBox)
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
                // Unsubscribe from old view model
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (DataContext is ExtractViewModel viewModel)
            {
                _viewModel = viewModel;
                viewModel.SetFilePickerCallback(BrowseForVideoFile);
                viewModel.SetFolderPickerCallback(BrowseForOutputFolder);

                // Subscribe to property changes to update markers
                viewModel.PropertyChanged += OnViewModelPropertyChanged;

                // Update marker positions when view model changes
                UpdateMarkerPositions();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExtractViewModel.InPointFrame) ||
                e.PropertyName == nameof(ExtractViewModel.OutPointFrame) ||
                e.PropertyName == nameof(ExtractViewModel.TotalFrames))
            {
                UpdateMarkerPositions();
            }
        }

        private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(Slider.Bounds))
            {
                UpdateMarkerPositions();
            }
        }

        private void UpdateMarkerPositions()
        {
            if (_viewModel == null || _timelineSlider == null || _markerCanvas == null ||
                _inMarker == null || _outMarker == null)
                return;

            var sliderWidth = _timelineSlider.Bounds.Width;
            if (sliderWidth <= 0 || _viewModel.TotalFrames <= 0)
                return;

            // Position In marker
            if (_viewModel.InPointFrame.HasValue)
            {
                var inPosition = (_viewModel.InPointFrame.Value / _viewModel.TotalFrames) * sliderWidth;
                Canvas.SetLeft(_inMarker, Math.Max(0, inPosition - 6)); // Center the marker (12 pixels wide)
            }

            // Position Out marker
            if (_viewModel.OutPointFrame.HasValue)
            {
                var outPosition = (_viewModel.OutPointFrame.Value / _viewModel.TotalFrames) * sliderWidth;
                Canvas.SetLeft(_outMarker, Math.Max(0, outPosition - 6)); // Center the marker (12 pixels wide)
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
