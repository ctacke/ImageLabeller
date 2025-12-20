using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ImageLabeller.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ImageLabeller.Views
{
    public partial class ModelView : UserControl
    {
        private ModelViewModel? _viewModel;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;
        private const double ZoomStep = 0.1;

        public ModelView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            InferenceImage.SizeChanged += OnImageSizeChanged;
            InferenceCanvas.SizeChanged += OnImageSizeChanged;
            Loaded += (s, e) => Focus();

            RedrawDetections();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not ModelViewModel viewModel)
                return;

            // Handle Left/Right arrow keys for navigation
            if (e.Key == Key.Left)
            {
                if (viewModel.PreviousImageCommand.CanExecute(null))
                {
                    viewModel.PreviousImageCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Right)
            {
                if (viewModel.NextImageCommand.CanExecute(null))
                {
                    viewModel.NextImageCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            // Unsubscribe from old view model
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Detections.CollectionChanged -= OnDetectionsChanged;
            }

            if (DataContext is ModelViewModel viewModel)
            {
                _viewModel = viewModel;
                viewModel.SetFilePickerCallbacks(BrowseForModel, BrowseForTestImage);
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
                viewModel.Detections.CollectionChanged += OnDetectionsChanged;
            }

            RedrawDetections();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelViewModel.CurrentImage))
            {
                // Reset zoom when a new image is loaded
                ResetZoom();
                RedrawDetections();
            }
        }

        private void OnDetectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RedrawDetections();
        }

        private void OnImageSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RedrawDetections();
        }

        private string? BrowseForModel()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var result = topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select ONNX Model",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("ONNX Model Files")
                    {
                        Patterns = new[] { "*.onnx" }
                    }
                }
            }).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        private string? BrowseForTestImage()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return null;

            var result = topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Test Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png" }
                    }
                }
            }).GetAwaiter().GetResult();

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        private void RedrawDetections()
        {
            InferenceCanvas.Children.Clear();

            if (_viewModel == null || _viewModel.CurrentImage == null || _viewModel.Detections.Count == 0)
                return;

            // Get the image bounds (accounting for Uniform stretch)
            var imageWidth = _viewModel.CurrentImage.PixelSize.Width;
            var imageHeight = _viewModel.CurrentImage.PixelSize.Height;
            var canvasWidth = InferenceCanvas.Bounds.Width;
            var canvasHeight = InferenceCanvas.Bounds.Height;

            if (canvasWidth == 0 || canvasHeight == 0)
                return;

            // Calculate the actual rendered image size (respecting Uniform stretch)
            var imageAspect = (double)imageWidth / imageHeight;
            var canvasAspect = canvasWidth / canvasHeight;

            double renderedWidth, renderedHeight, offsetX, offsetY;

            if (imageAspect > canvasAspect)
            {
                // Image is wider - fit to width
                renderedWidth = canvasWidth;
                renderedHeight = canvasWidth / imageAspect;
                offsetX = 0;
                offsetY = (canvasHeight - renderedHeight) / 2;
            }
            else
            {
                // Image is taller - fit to height
                renderedHeight = canvasHeight;
                renderedWidth = canvasHeight * imageAspect;
                offsetX = (canvasWidth - renderedWidth) / 2;
                offsetY = 0;
            }

            // Draw each detection
            foreach (var detection in _viewModel.Detections)
            {
                // Convert normalized coordinates to canvas coordinates
                var centerX = detection.X * renderedWidth + offsetX;
                var centerY = detection.Y * renderedHeight + offsetY;
                var width = detection.Width * renderedWidth;
                var height = detection.Height * renderedHeight;

                var x = centerX - width / 2;
                var y = centerY - height / 2;

                // Draw bounding box
                var rect = new Rectangle
                {
                    Stroke = Brushes.Lime,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    Width = width,
                    Height = height
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                InferenceCanvas.Children.Add(rect);

                // Draw label background
                var labelText = $"{detection.ClassName} ==> {detection.Confidence:P0}";
                var label = new TextBlock
                {
                    Text = labelText,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontWeight = FontWeight.Bold
                };

                // Measure the text
                label.Measure(Size.Infinity);
                var labelWidth = label.DesiredSize.Width + 8;
                var labelHeight = label.DesiredSize.Height + 4;

                var labelBg = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(200, 0, 255, 0)),
                    Width = labelWidth,
                    Height = labelHeight
                };

                Canvas.SetLeft(labelBg, x);
                Canvas.SetTop(labelBg, y - labelHeight);
                InferenceCanvas.Children.Add(labelBg);

                // Draw label text
                Canvas.SetLeft(label, x + 4);
                Canvas.SetTop(label, y - labelHeight + 2);
                InferenceCanvas.Children.Add(label);
            }
        }

        private void ResetZoom()
        {
            var zoomContainer = this.FindControl<Grid>("ZoomContainer");
            if (zoomContainer != null)
            {
                if (zoomContainer.RenderTransform is ScaleTransform scaleTransform)
                {
                    scaleTransform.ScaleX = 1.0;
                    scaleTransform.ScaleY = 1.0;
                }

                // Reset transform origin to default (center)
                zoomContainer.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
            }
        }

        private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Only zoom if Ctrl is pressed
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                return;

            var zoomContainer = this.FindControl<Grid>("ZoomContainer");
            if (zoomContainer == null)
                return;

            var scaleTransform = zoomContainer.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
                return;

            var delta = e.Delta.Y;
            var currentScale = scaleTransform.ScaleX;
            var newScale = currentScale + (delta * ZoomStep);

            // Clamp zoom level
            newScale = Math.Clamp(newScale, MinZoom, MaxZoom);

            if (Math.Abs(newScale - currentScale) < 0.001)
                return;

            // Get mouse position relative to the ZoomContainer
            var mousePosition = e.GetPosition(zoomContainer);

            // Calculate the transform origin as a ratio of the container size
            // This tells the ScaleTransform where to zoom from
            var bounds = zoomContainer.Bounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                var originX = mousePosition.X / bounds.Width;
                var originY = mousePosition.Y / bounds.Height;

                // Set the transform origin to the mouse position
                zoomContainer.RenderTransformOrigin = new Avalonia.RelativePoint(originX, originY, Avalonia.RelativeUnit.Relative);
            }

            // Apply the new scale
            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            // Redraw detections to match new zoom level
            RedrawDetections();

            e.Handled = true;
        }
    }
}
