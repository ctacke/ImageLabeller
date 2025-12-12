using Avalonia.Media.Imaging;
using ImageLabeller.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class LabelViewModel : ViewModelBase
    {
        // Color palette for auto-assigning colors to classes
        private static readonly string[] ColorPalette = new[]
        {
            "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
            "#FFEAA7", "#DFE6E9", "#74B9FF", "#A29BFE",
            "#FD79A8", "#FDCB6E", "#6C5CE7", "#00B894",
            "#E17055", "#D63031", "#F39C12", "#8E44AD"
        };

        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };

        private string _sourceFolderPath = string.Empty;
        private string _labeledImageDestination = string.Empty;
        private string _labelFileDestination = string.Empty;
        private Bitmap? _currentImage;
        private int _currentImageIndex = -1;
        private List<string> _sourceImageFiles = new();
        private string _imageCountText = "0 of 0";
        private ImageClass _selectedClass;
        private ObservableCollection<YoloAnnotation> _currentAnnotations = new();
        private YoloAnnotation? _selectedAnnotation;
        private MainWindowViewModel? _mainViewModel;
        private string _modelStatus = "Ready";
        private bool _autoMoveEnabled = false;

        public string SourceFolderPath
        {
            get => _sourceFolderPath;
            set
            {
                if (SetProperty(ref _sourceFolderPath, value))
                {
                    // Auto-fill destination folders if they're empty
                    if (string.IsNullOrEmpty(_labeledImageDestination))
                    {
                        LabeledImageDestination = value;
                    }
                    if (string.IsNullOrEmpty(_labelFileDestination))
                    {
                        LabelFileDestination = value;
                    }

                    // Auto-select class if folder name matches a class name
                    if (!string.IsNullOrEmpty(value))
                    {
                        var folderName = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar));
                        var matchingClass = ImageClasses.FirstOrDefault(c =>
                            c.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                        if (matchingClass != null)
                        {
                            SelectedClass = matchingClass;
                            OnPropertyChanged(nameof(CurrentClassLabel));
                        }
                    }

                    LoadImagesFromSource();
                    SaveSettings();
                }
            }
        }

        public string LabeledImageDestination
        {
            get => _labeledImageDestination;
            set
            {
                if (SetProperty(ref _labeledImageDestination, value))
                {
                    SaveSettings();
                    (MoveCurrentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string LabelFileDestination
        {
            get => _labelFileDestination;
            set
            {
                if (SetProperty(ref _labelFileDestination, value))
                {
                    SaveSettings();
                    (MoveCurrentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public Bitmap? CurrentImage
        {
            get => _currentImage;
            private set => SetProperty(ref _currentImage, value);
        }

        public string ImageCountText
        {
            get => _imageCountText;
            private set => SetProperty(ref _imageCountText, value);
        }

        public ImageClass SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetProperty(ref _selectedClass, value))
                {
                    SaveSettings();
                }
            }
        }

        public string CurrentClassLabel => $"Current Class: {SelectedClass.Id} - {SelectedClass.Name}";

        public ImageClass[] ImageClasses
        {
            get
            {
                if (_mainViewModel == null)
                    return Array.Empty<ImageClass>();

                return _mainViewModel.Settings.ClassNames
                    .Select((name, index) => new ImageClass(
                        index,
                        name,
                        ColorPalette[index % ColorPalette.Length]
                    ))
                    .ToArray();
            }
        }

        public ObservableCollection<YoloAnnotation> CurrentAnnotations
        {
            get => _currentAnnotations;
            private set
            {
                if (SetProperty(ref _currentAnnotations, value))
                {
                    (MoveCurrentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public YoloAnnotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set => SetProperty(ref _selectedAnnotation, value);
        }

        public string ModelStatus
        {
            get => _modelStatus;
            private set => SetProperty(ref _modelStatus, value);
        }

        public bool AutoMoveEnabled
        {
            get => _autoMoveEnabled;
            set
            {
                if (SetProperty(ref _autoMoveEnabled, value))
                {
                    SaveSettings();
                }
            }
        }

        public bool HasPreviousImage => _currentImageIndex > 0;
        public bool HasNextImage => _currentImageIndex >= 0 && _currentImageIndex < _sourceImageFiles.Count - 1;
        public bool CanApplyModel => _mainViewModel?.ModelViewModel?.HasModel == true && CurrentImage != null;

        public ICommand BrowseSourceFolderCommand { get; }
        public ICommand BrowseLabeledImageDestinationCommand { get; }
        public ICommand BrowseLabelFileDestinationCommand { get; }
        public ICommand SelectClassCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand PreviousImageCommand { get; }
        public ICommand SelectAnnotationCommand { get; }
        public ICommand DeleteAnnotationCommand { get; }
        public ICommand ApplyModelCommand { get; }
        public ICommand MoveCurrentFileCommand { get; }

        public LabelViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseSourceFolderCommand = new RelayCommand(() => { });
            BrowseLabeledImageDestinationCommand = new RelayCommand(() => { });
            BrowseLabelFileDestinationCommand = new RelayCommand(() => { });
            SelectClassCommand = new RelayCommand<ImageClass>(SelectClass!);
            NextImageCommand = new RelayCommand(NavigateNext, () => HasNextImage);
            PreviousImageCommand = new RelayCommand(NavigatePrevious, () => HasPreviousImage);
            SelectAnnotationCommand = new RelayCommand<YoloAnnotation>(SelectAnnotation!);
            DeleteAnnotationCommand = new RelayCommand<YoloAnnotation>(DeleteAnnotation!);
            ApplyModelCommand = new RelayCommand(ApplyModel, () => CanApplyModel);
            MoveCurrentFileCommand = new RelayCommand(ExecuteMoveCurrentFile, CanMoveCurrentFile);

            // Initialize selected class
            var classes = ImageClasses;
            _selectedClass = classes.Length > 0 ? classes[0] : new ImageClass(0, "Unknown", "#808080");

            // Load saved settings
            if (_mainViewModel != null)
            {
                _sourceFolderPath = _mainViewModel.Settings.LabelSourceFolder;
                _labeledImageDestination = _mainViewModel.Settings.LabeledImageDestination;
                _labelFileDestination = _mainViewModel.Settings.LabelFileDestination;
                _autoMoveEnabled = _mainViewModel.Settings.LabelAutoMoveEnabled;

                // If destination folders are empty, default to source folder
                if (string.IsNullOrEmpty(_labeledImageDestination) && !string.IsNullOrEmpty(_sourceFolderPath))
                {
                    _labeledImageDestination = _sourceFolderPath;
                }
                if (string.IsNullOrEmpty(_labelFileDestination) && !string.IsNullOrEmpty(_sourceFolderPath))
                {
                    _labelFileDestination = _sourceFolderPath;
                }

                var savedClassId = _mainViewModel.Settings.LabelSelectedClassId;
                _selectedClass = ImageClasses.FirstOrDefault(c => c.Id == savedClassId) ?? _selectedClass;

                // Load images if source folder exists
                if (!string.IsNullOrEmpty(_sourceFolderPath))
                {
                    LoadImagesFromSource();
                }

                // Subscribe to model changes (if ModelViewModel is initialized)
                if (_mainViewModel.ModelViewModel != null)
                {
                    _mainViewModel.ModelViewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ModelViewModel.HasModel))
                        {
                            OnPropertyChanged(nameof(CanApplyModel));
                            (ApplyModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        }
                    };
                }
            }
        }

        public void SetFolderPickerCallback(Func<string?> browseSourceFolder)
        {
            (BrowseSourceFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseSourceFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    SourceFolderPath = folder;
                }
            });
        }

        public void SetLabeledImageDestinationCallback(Func<string?> browseLabeledImageDestination)
        {
            (BrowseLabeledImageDestinationCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseLabeledImageDestination();
                if (!string.IsNullOrEmpty(folder))
                {
                    LabeledImageDestination = folder;
                }
            });
        }

        public void SetLabelFileDestinationCallback(Func<string?> browseLabelFileDestination)
        {
            (BrowseLabelFileDestinationCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseLabelFileDestination();
                if (!string.IsNullOrEmpty(folder))
                {
                    LabelFileDestination = folder;
                }
            });
        }

        private void SelectClass(ImageClass imageClass)
        {
            SelectedClass = imageClass;
            OnPropertyChanged(nameof(CurrentClassLabel));
        }

        private void LoadImagesFromSource()
        {
            _sourceImageFiles.Clear();
            _currentImageIndex = -1;
            CurrentImage?.Dispose();
            CurrentImage = null;
            CurrentAnnotations = new ObservableCollection<YoloAnnotation>();

            if (string.IsNullOrEmpty(SourceFolderPath) || !Directory.Exists(SourceFolderPath))
            {
                UpdateImageCount();
                return;
            }

            _sourceImageFiles = Directory.GetFiles(SourceFolderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            if (_sourceImageFiles.Count > 0)
            {
                LoadImage(0);
            }

            UpdateImageCount();
        }

        private void LoadImage(int index)
        {
            if (index < 0 || index >= _sourceImageFiles.Count)
            {
                CurrentImage?.Dispose();
                CurrentImage = null;
                _currentImageIndex = -1;
                CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                UpdateNavigationState();
                return;
            }

            try
            {
                CurrentImage?.Dispose();
                _currentImageIndex = index;
                var imagePath = _sourceImageFiles[index];
                CurrentImage = new Bitmap(imagePath);

                // Load or create annotation file
                LoadAnnotations(imagePath);
            }
            catch
            {
                CurrentImage = null;
                CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
            }

            UpdateImageCount();
            UpdateNavigationState();
        }

        private void LoadAnnotations(string imagePath)
        {
            var annotationPath = Path.ChangeExtension(imagePath, ".txt");

            if (File.Exists(annotationPath))
            {
                try
                {
                    var lines = File.ReadAllLines(annotationPath);
                    var annotations = lines
                        .Select(YoloAnnotation.Parse)
                        .Where(a => a != null)
                        .Cast<YoloAnnotation>()
                        .ToList();
                    CurrentAnnotations = new ObservableCollection<YoloAnnotation>(annotations);
                }
                catch
                {
                    CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                }
            }
            else
            {
                // Create empty annotation file
                try
                {
                    File.WriteAllText(annotationPath, string.Empty);
                    CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                }
                catch
                {
                    CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                }
            }
        }

        private void NavigateNext()
        {
            if (HasNextImage)
            {
                LoadImage(_currentImageIndex + 1);
            }
        }

        private void NavigatePrevious()
        {
            if (HasPreviousImage)
            {
                LoadImage(_currentImageIndex - 1);
            }
        }

        private void UpdateImageCount()
        {
            if (_sourceImageFiles.Count == 0)
            {
                ImageCountText = "0 of 0";
            }
            else if (_currentImageIndex >= 0 && _currentImageIndex < _sourceImageFiles.Count)
            {
                ImageCountText = $"{_currentImageIndex + 1} of {_sourceImageFiles.Count}";
            }
            else
            {
                ImageCountText = $"0 of {_sourceImageFiles.Count}";
            }

            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(HasPreviousImage));
            OnPropertyChanged(nameof(HasNextImage));
            OnPropertyChanged(nameof(CanApplyModel));
            (NextImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ApplyModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SaveSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.LabelSourceFolder = _sourceFolderPath;
                _mainViewModel.Settings.LabeledImageDestination = _labeledImageDestination;
                _mainViewModel.Settings.LabelFileDestination = _labelFileDestination;
                _mainViewModel.Settings.LabelSelectedClassId = SelectedClass.Id;
                _mainViewModel.Settings.LabelAutoMoveEnabled = _autoMoveEnabled;
                _mainViewModel.SaveSettings();
            }
        }

        public ImageClass? GetClassById(int classId)
        {
            return ImageClasses.FirstOrDefault(c => c.Id == classId);
        }

        public void RefreshClasses()
        {
            OnPropertyChanged(nameof(ImageClasses));
            OnPropertyChanged(nameof(CurrentClassLabel));

            // Re-validate selected class
            var classes = ImageClasses;
            if (classes.Length > 0 && !classes.Any(c => c.Id == SelectedClass.Id))
            {
                SelectedClass = classes[0];
            }
        }

        public void AddAnnotation(YoloAnnotation annotation)
        {
            CurrentAnnotations.Add(annotation);
            (MoveCurrentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            SaveCurrentAnnotations();
        }

        private void SelectAnnotation(YoloAnnotation annotation)
        {
            SelectedAnnotation = annotation;
        }

        public void DeleteAnnotation(YoloAnnotation annotation)
        {
            CurrentAnnotations.Remove(annotation);
            if (SelectedAnnotation == annotation)
            {
                SelectedAnnotation = null;
            }
            (MoveCurrentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            SaveCurrentAnnotations();
        }

        public void SaveCurrentAnnotations()
        {
            if (_currentImageIndex < 0 || _currentImageIndex >= _sourceImageFiles.Count)
                return;

            try
            {
                var imagePath = _sourceImageFiles[_currentImageIndex];
                var annotationPath = Path.ChangeExtension(imagePath, ".txt");
                var lines = CurrentAnnotations.Select(a => a.ToString());
                File.WriteAllLines(annotationPath, lines);

                // If auto-move is enabled, image has annotations, and destination folders are different from source, move files
                if (_autoMoveEnabled && CurrentAnnotations.Count > 0)
                {
                    var sourceFolder = Path.GetDirectoryName(imagePath);
                    var shouldMoveImage = !string.IsNullOrEmpty(_labeledImageDestination) &&
                                         !string.Equals(sourceFolder, _labeledImageDestination, StringComparison.OrdinalIgnoreCase);
                    var shouldMoveLabel = !string.IsNullOrEmpty(_labelFileDestination) &&
                                         !string.Equals(sourceFolder, _labelFileDestination, StringComparison.OrdinalIgnoreCase);

                    if (shouldMoveImage || shouldMoveLabel)
                    {
                        // Ensure destination directories exist
                        if (shouldMoveImage && !Directory.Exists(_labeledImageDestination))
                        {
                            Directory.CreateDirectory(_labeledImageDestination);
                        }
                        if (shouldMoveLabel && !Directory.Exists(_labelFileDestination))
                        {
                            Directory.CreateDirectory(_labelFileDestination);
                        }

                        // Move image file
                        if (shouldMoveImage)
                        {
                            var imageFileName = Path.GetFileName(imagePath);
                            var destImagePath = Path.Combine(_labeledImageDestination, imageFileName);

                            // If destination file exists, delete it first
                            if (File.Exists(destImagePath))
                            {
                                File.Delete(destImagePath);
                            }
                            File.Move(imagePath, destImagePath);
                        }

                        // Move label file
                        if (shouldMoveLabel)
                        {
                            var labelFileName = Path.GetFileName(annotationPath);
                            var destLabelPath = Path.Combine(_labelFileDestination, labelFileName);

                            // If destination file exists, delete it first
                            if (File.Exists(destLabelPath))
                            {
                                File.Delete(destLabelPath);
                            }
                            File.Move(annotationPath, destLabelPath);
                        }

                        // Remove the image from the source list and load next image
                        _sourceImageFiles.RemoveAt(_currentImageIndex);

                        // Adjust current index and reload
                        if (_sourceImageFiles.Count == 0)
                        {
                            // No more images
                            _currentImageIndex = -1;
                            CurrentImage?.Dispose();
                            CurrentImage = null;
                            CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                        }
                        else if (_currentImageIndex >= _sourceImageFiles.Count)
                        {
                            // Was at end, go to new last image
                            LoadImage(_sourceImageFiles.Count - 1);
                        }
                        else
                        {
                            // Stay at same index (which now points to next image)
                            LoadImage(_currentImageIndex);
                        }

                        UpdateImageCount();
                    }
                }
            }
            catch
            {
                // Silently fail - in production, you'd want to show an error to the user
            }
        }

        private bool CanMoveCurrentFile()
        {
            if (_currentImageIndex < 0 || _currentImageIndex >= _sourceImageFiles.Count)
                return false;

            if (CurrentAnnotations.Count == 0)
                return false;

            var imagePath = _sourceImageFiles[_currentImageIndex];
            var sourceFolder = Path.GetDirectoryName(imagePath);
            var shouldMoveImage = !string.IsNullOrEmpty(_labeledImageDestination) &&
                                 !string.Equals(sourceFolder, _labeledImageDestination, StringComparison.OrdinalIgnoreCase);
            var shouldMoveLabel = !string.IsNullOrEmpty(_labelFileDestination) &&
                                 !string.Equals(sourceFolder, _labelFileDestination, StringComparison.OrdinalIgnoreCase);

            return shouldMoveImage || shouldMoveLabel;
        }

        private void ExecuteMoveCurrentFile()
        {
            if (!CanMoveCurrentFile())
                return;

            try
            {
                // Save annotations first
                SaveCurrentAnnotations();

                var imagePath = _sourceImageFiles[_currentImageIndex];
                var annotationPath = Path.ChangeExtension(imagePath, ".txt");
                var sourceFolder = Path.GetDirectoryName(imagePath);
                var shouldMoveImage = !string.IsNullOrEmpty(_labeledImageDestination) &&
                                     !string.Equals(sourceFolder, _labeledImageDestination, StringComparison.OrdinalIgnoreCase);
                var shouldMoveLabel = !string.IsNullOrEmpty(_labelFileDestination) &&
                                     !string.Equals(sourceFolder, _labelFileDestination, StringComparison.OrdinalIgnoreCase);

                // Ensure destination directories exist
                if (shouldMoveImage && !Directory.Exists(_labeledImageDestination))
                {
                    Directory.CreateDirectory(_labeledImageDestination);
                }
                if (shouldMoveLabel && !Directory.Exists(_labelFileDestination))
                {
                    Directory.CreateDirectory(_labelFileDestination);
                }

                // Move image file
                if (shouldMoveImage)
                {
                    var imageFileName = Path.GetFileName(imagePath);
                    var destImagePath = Path.Combine(_labeledImageDestination, imageFileName);

                    if (File.Exists(destImagePath))
                    {
                        File.Delete(destImagePath);
                    }
                    File.Move(imagePath, destImagePath);
                }

                // Move label file
                if (shouldMoveLabel && File.Exists(annotationPath))
                {
                    var labelFileName = Path.GetFileName(annotationPath);
                    var destLabelPath = Path.Combine(_labelFileDestination, labelFileName);

                    if (File.Exists(destLabelPath))
                    {
                        File.Delete(destLabelPath);
                    }
                    File.Move(annotationPath, destLabelPath);
                }

                // Remove the image from the source list and load next image
                _sourceImageFiles.RemoveAt(_currentImageIndex);

                // Adjust current index and reload
                if (_sourceImageFiles.Count == 0)
                {
                    // No more images
                    _currentImageIndex = -1;
                    CurrentImage?.Dispose();
                    CurrentImage = null;
                    CurrentAnnotations = new ObservableCollection<YoloAnnotation>();
                }
                else if (_currentImageIndex >= _sourceImageFiles.Count)
                {
                    // Was at end, go to new last image
                    LoadImage(_sourceImageFiles.Count - 1);
                }
                else
                {
                    // Load the image that's now at the current index
                    LoadImage(_currentImageIndex);
                }

                UpdateImageCount();
            }
            catch
            {
                // Silently fail
            }
        }

        public void OnViewActivated()
        {
            // Auto-select class if folder name matches a class name
            if (!string.IsNullOrEmpty(SourceFolderPath))
            {
                var folderName = Path.GetFileName(SourceFolderPath);
                var matchingClass = ImageClasses.FirstOrDefault(c =>
                    c.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                if (matchingClass != null)
                {
                    SelectedClass = matchingClass;
                    OnPropertyChanged(nameof(CurrentClassLabel));
                }
            }
        }

        private void ApplyModel()
        {
            if (_mainViewModel?.ModelViewModel == null || _currentImageIndex < 0 || _currentImageIndex >= _sourceImageFiles.Count)
            {
                ModelStatus = "Error: No model or image available";
                return;
            }

            try
            {
                ModelStatus = "Running inference...";
                var imagePath = _sourceImageFiles[_currentImageIndex];
                var detections = _mainViewModel.ModelViewModel.RunInferenceOnImage(imagePath);

                if (detections.Count == 0)
                {
                    ModelStatus = "No detections found";
                    return;
                }

                // Find highest confidence detection
                var bestDetection = detections.OrderByDescending(d => d.Confidence).First();
                ModelStatus = $"{bestDetection.ClassName}: {bestDetection.Confidence:P0}";

                // Add bounding box if confidence is above 50%
                if (bestDetection.Confidence >= 0.5f)
                {
                    var annotation = new YoloAnnotation(
                        bestDetection.ClassId,
                        bestDetection.X,
                        bestDetection.Y,
                        bestDetection.Width,
                        bestDetection.Height
                    );

                    AddAnnotation(annotation);
                    ModelStatus = $"Added: {bestDetection.ClassName} ({bestDetection.Confidence:P0})";
                }
                else
                {
                    ModelStatus = $"Low confidence: {bestDetection.ClassName} ({bestDetection.Confidence:P0})";
                }
            }
            catch (Exception ex)
            {
                ModelStatus = $"Error: {ex.Message}";
            }
        }
    }
}
