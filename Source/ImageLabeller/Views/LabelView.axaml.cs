using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
    public partial class LabelView : UserControl
    {
        private Point? _startPoint;
        private Rectangle? _currentRectangle;
        private bool _isDrawing;
        private LabelViewModel? _viewModel;
        private System.Collections.ObjectModel.ObservableCollection<Models.YoloAnnotation>? _subscribedCollection;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;
        private const double ZoomStep = 0.1;

        public LabelView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            AttachedToVisualTree += OnAttachedToVisualTree;
            AnnotationCanvas.SizeChanged += OnCanvasSizeChanged;
            Loaded += (s, e) => Focus();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not LabelViewModel viewModel)
                return;

            // Handle Delete key for removing selected annotation
            if (e.Key == Key.Delete && viewModel.SelectedAnnotation != null)
            {
                viewModel.DeleteAnnotation(viewModel.SelectedAnnotation);
                e.Handled = true;
                return;
            }

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

            // Handle digit keys (0-9) for selecting class by ID
            var classId = e.Key switch
            {
                Key.D0 or Key.NumPad0 => 0,
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                Key.D4 or Key.NumPad4 => 4,
                Key.D5 or Key.NumPad5 => 5,
                Key.D6 or Key.NumPad6 => 6,
                Key.D7 or Key.NumPad7 => 7,
                Key.D8 or Key.NumPad8 => 8,
                Key.D9 or Key.NumPad9 => 9,
                _ => -1
            };

            if (classId >= 0)
            {
                var matchingClass = viewModel.GetClassById(classId);
                if (matchingClass != null)
                {
                    viewModel.SelectedClass = matchingClass;
                    e.Handled = true;
                }
            }
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            // Unsubscribe from old view model
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                UnsubscribeFromAnnotationsCollection();
            }

            if (DataContext is LabelViewModel viewModel)
            {
                _viewModel = viewModel;
                viewModel.SetFolderPickerCallback(BrowseForSourceFolder);
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
                SubscribeToAnnotationsCollection();
            }
        }

        private void SubscribeToAnnotationsCollection()
        {
            if (_viewModel == null)
                return;

            UnsubscribeFromAnnotationsCollection();
            _subscribedCollection = _viewModel.CurrentAnnotations;
            _subscribedCollection.CollectionChanged += OnAnnotationsCollectionChanged;
        }

        private void UnsubscribeFromAnnotationsCollection()
        {
            if (_subscribedCollection != null)
            {
                _subscribedCollection.CollectionChanged -= OnAnnotationsCollectionChanged;
                _subscribedCollection = null;
            }
        }

        private void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RedrawAnnotations();
        }

        private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is LabelViewModel viewModel)
            {
                viewModel.OnViewActivated();
                // Delay the button state update to ensure controls are rendered
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateClassButtonStates(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LabelViewModel.CurrentAnnotations))
            {
                // Re-subscribe to the new collection
                SubscribeToAnnotationsCollection();
                RedrawAnnotations();
            }
            else if (e.PropertyName == nameof(LabelViewModel.CurrentImage))
            {
                // Reset zoom when a new image is loaded
                ResetZoom();
                RedrawAnnotations();
            }
            else if (e.PropertyName == nameof(LabelViewModel.SelectedAnnotation))
            {
                RedrawAnnotations();
            }
            else if (e.PropertyName == nameof(LabelViewModel.SelectedClass))
            {
                UpdateClassButtonStates();
            }
            else if (e.PropertyName == nameof(LabelViewModel.ImageClasses))
            {
                // Update button states when classes are refreshed
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateClassButtonStates(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ResetZoom()
        {
            var zoomContainer = this.FindControl<Grid>("ZoomContainer");
            if (zoomContainer?.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

                if (scaleTransform != null)
                {
                    scaleTransform.ScaleX = 1.0;
                    scaleTransform.ScaleY = 1.0;
                }

                if (translateTransform != null)
                {
                    translateTransform.X = 0;
                    translateTransform.Y = 0;
                }
            }
        }

        private void UpdateClassButtonStates()
        {
            if (DataContext is not LabelViewModel viewModel)
                return;

            // Find all class buttons and update their Tag based on selection
            var itemsControl = this.FindControl<ItemsControl>("ClassButtonsItemsControl");
            if (itemsControl == null)
                return;

            foreach (var item in itemsControl.GetRealizedContainers())
            {
                if (item is ContentPresenter presenter &&
                    presenter.Child is Button button &&
                    button.DataContext is Models.ImageClass imageClass)
                {
                    button.Tag = imageClass.Id == viewModel.SelectedClass.Id ? "Selected" : null;
                }
            }
        }

        private void OnAnnotationTextBlockLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock &&
                textBlock.DataContext is Models.YoloAnnotation annotation &&
                DataContext is LabelViewModel viewModel)
            {
                // Set the text to "[{ClassId}] {ClassName}"
                var imageClass = viewModel.GetClassById(annotation.ClassId);
                var className = imageClass?.Name ?? "Unknown";
                textBlock.Text = $"[{annotation.ClassId}] {className}";
            }
        }

        private void OnAnnotationClassComboBoxLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox &&
                comboBox.DataContext is Models.YoloAnnotation annotation &&
                DataContext is LabelViewModel viewModel)
            {
                // Set the selected item based on the annotation's ClassId
                var selectedClass = viewModel.ImageClasses.FirstOrDefault(c => c.Id == annotation.ClassId);
                if (selectedClass != null)
                {
                    comboBox.SelectedItem = selectedClass;
                }
            }
        }

        private void OnAnnotationClassChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox &&
                comboBox.DataContext is Models.YoloAnnotation annotation &&
                comboBox.SelectedItem is Models.ImageClass selectedClass &&
                DataContext is LabelViewModel viewModel)
            {
                // Update the annotation's ClassId
                if (annotation.ClassId != selectedClass.Id)
                {
                    annotation.ClassId = selectedClass.Id;

                    // Update the TextBlock to show the new class name
                    if (comboBox.Parent is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Grid headerGrid)
                    {
                        var textBlock = headerGrid.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBlock.Text = $"[{annotation.ClassId}] {selectedClass.Name}";
                        }
                    }

                    // Save the annotations when a class is changed
                    viewModel.SaveCurrentAnnotations();

                    // Redraw annotations to update the color
                    RedrawAnnotations();
                }
            }
        }

        private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RedrawAnnotations();
        }

        private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Only zoom if Ctrl is pressed
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                return;

            var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
            var zoomContainer = this.FindControl<Grid>("ZoomContainer");

            if (scrollViewer == null || zoomContainer?.RenderTransform is not TransformGroup transformGroup)
                return;

            var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

            if (scaleTransform == null || translateTransform == null)
                return;

            var delta = e.Delta.Y;
            var currentZoom = scaleTransform.ScaleX;
            var newZoom = currentZoom + (delta * ZoomStep);

            // Clamp zoom level
            newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);

            if (Math.Abs(newZoom - currentZoom) > 0.001)
            {
                // Get mouse position relative to the ZoomContainer
                var mousePos = e.GetPosition(zoomContainer);

                // The current mouse position in transformed space is:
                // transformedX = (contentX * currentZoom) + currentTranslateX
                // Solve for contentX: contentX = (transformedX - currentTranslateX) / currentZoom
                var currentTranslateX = translateTransform.X;
                var currentTranslateY = translateTransform.Y;

                var contentX = (mousePos.X - currentTranslateX) / currentZoom;
                var contentY = (mousePos.Y - currentTranslateY) / currentZoom;

                // Apply new zoom
                scaleTransform.ScaleX = newZoom;
                scaleTransform.ScaleY = newZoom;

                // Calculate new translation to keep the content point under the mouse
                // mousePos = (contentX * newZoom) + newTranslateX
                // newTranslateX = mousePos - (contentX * newZoom)
                translateTransform.X = mousePos.X - (contentX * newZoom);
                translateTransform.Y = mousePos.Y - (contentY * newZoom);

                // Redraw annotations to match new zoom level
                RedrawAnnotations();
            }

            e.Handled = true;
        }

        private void RedrawAnnotations()
        {
            // Clear existing annotation rectangles (but not the current drawing rectangle)
            var childrenToRemove = AnnotationCanvas.Children
                .OfType<Rectangle>()
                .Where(r => r != _currentRectangle)
                .ToList();

            foreach (var child in childrenToRemove)
            {
                AnnotationCanvas.Children.Remove(child);
            }

            if (DataContext is not LabelViewModel viewModel || viewModel.CurrentImage == null)
                return;

            var imageBounds = GetImageBounds();
            if (!imageBounds.HasValue)
                return;

            // Draw each annotation
            foreach (var annotation in viewModel.CurrentAnnotations)
            {
                var classColor = viewModel.GetClassById(annotation.ClassId)?.Color ?? "#FFFFFF";
                var isSelected = annotation == viewModel.SelectedAnnotation;

                // Convert YOLO coordinates to canvas coordinates
                var centerX = annotation.CenterX * imageBounds.Value.Width + imageBounds.Value.X;
                var centerY = annotation.CenterY * imageBounds.Value.Height + imageBounds.Value.Y;
                var width = annotation.Width * imageBounds.Value.Width;
                var height = annotation.Height * imageBounds.Value.Height;

                var x = centerX - width / 2;
                var y = centerY - height / 2;

                var rect = new Rectangle
                {
                    Stroke = Brush.Parse(classColor),
                    StrokeThickness = isSelected ? 3 : 2,
                    StrokeDashArray = isSelected ? new Avalonia.Collections.AvaloniaList<double> { 4, 2 } : null,
                    Fill = Brushes.Transparent,
                    Width = width,
                    Height = height
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                AnnotationCanvas.Children.Add(rect);
            }
        }

        private string? BrowseForSourceFolder()
        {
            return BrowseForFolder("Select Source Folder");
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

        private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not LabelViewModel viewModel || viewModel.CurrentImage == null)
                return;

            var point = e.GetPosition(AnnotationCanvas);
            _startPoint = point;
            _isDrawing = true;

            // Create a new rectangle for visual feedback
            _currentRectangle = new Rectangle
            {
                Stroke = Brush.Parse(viewModel.SelectedClass.Color),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(_currentRectangle, point.X);
            Canvas.SetTop(_currentRectangle, point.Y);
            AnnotationCanvas.Children.Add(_currentRectangle);

            e.Handled = true;
        }

        private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDrawing || _startPoint == null || _currentRectangle == null)
                return;

            var currentPoint = e.GetPosition(AnnotationCanvas);
            var x = Math.Min(_startPoint.Value.X, currentPoint.X);
            var y = Math.Min(_startPoint.Value.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.Value.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Value.Y);

            Canvas.SetLeft(_currentRectangle, x);
            Canvas.SetTop(_currentRectangle, y);
            _currentRectangle.Width = width;
            _currentRectangle.Height = height;
        }

        private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDrawing || _startPoint == null || _currentRectangle == null)
                return;

            var endPoint = e.GetPosition(AnnotationCanvas);

            // Calculate bounding box in canvas coordinates
            var x1 = Math.Min(_startPoint.Value.X, endPoint.X);
            var y1 = Math.Min(_startPoint.Value.Y, endPoint.Y);
            var x2 = Math.Max(_startPoint.Value.X, endPoint.X);
            var y2 = Math.Max(_startPoint.Value.Y, endPoint.Y);

            var width = x2 - x1;
            var height = y2 - y1;

            // Only save if the box has meaningful size (at least 5 pixels in each dimension)
            if (width >= 5 && height >= 5 && DataContext is LabelViewModel viewModel)
            {
                // Get the actual image bounds on screen
                var imageBounds = GetImageBounds();
                if (imageBounds.HasValue)
                {
                    // Convert canvas coordinates to image coordinates and then to YOLO format
                    var annotation = ConvertToYoloAnnotation(
                        x1, y1, x2, y2,
                        imageBounds.Value,
                        viewModel.SelectedClass.Id);

                    if (annotation != null)
                    {
                        viewModel.AddAnnotation(annotation);
                    }
                }
            }

            // Clean up
            if (_currentRectangle != null)
            {
                AnnotationCanvas.Children.Remove(_currentRectangle);
            }

            _startPoint = null;
            _currentRectangle = null;
            _isDrawing = false;
            e.Handled = true;
        }

        private Rect? GetImageBounds()
        {
            if (DataContext is not LabelViewModel viewModel || viewModel.CurrentImage == null)
                return null;

            var imageWidth = viewModel.CurrentImage.PixelSize.Width;
            var imageHeight = viewModel.CurrentImage.PixelSize.Height;
            var canvasWidth = AnnotationCanvas.Bounds.Width;
            var canvasHeight = AnnotationCanvas.Bounds.Height;

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

            return new Rect(offsetX, offsetY, renderedWidth, renderedHeight);
        }

        private Models.YoloAnnotation? ConvertToYoloAnnotation(
            double x1, double y1, double x2, double y2,
            Rect imageBounds,
            int classId)
        {
            if (DataContext is not LabelViewModel viewModel || viewModel.CurrentImage == null)
                return null;

            // Adjust coordinates to be relative to the actual image bounds
            x1 = Math.Max(0, x1 - imageBounds.X);
            y1 = Math.Max(0, y1 - imageBounds.Y);
            x2 = Math.Min(imageBounds.Width, x2 - imageBounds.X);
            y2 = Math.Min(imageBounds.Height, y2 - imageBounds.Y);

            // Convert to normalized coordinates (0-1 range)
            var centerX = (x1 + x2) / 2 / imageBounds.Width;
            var centerY = (y1 + y2) / 2 / imageBounds.Height;
            var width = (x2 - x1) / imageBounds.Width;
            var height = (y2 - y1) / imageBounds.Height;

            // Clamp values to valid range
            centerX = Math.Clamp(centerX, 0, 1);
            centerY = Math.Clamp(centerY, 0, 1);
            width = Math.Clamp(width, 0, 1);
            height = Math.Clamp(height, 0, 1);

            return new Models.YoloAnnotation(classId, centerX, centerY, width, height);
        }
    }
}
