using Avalonia.Media.Imaging;
using ImageLabeller.Models;
using System;
using System.Collections.Generic;
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
        private Bitmap? _currentImage;
        private int _currentImageIndex = -1;
        private List<string> _sourceImageFiles = new();
        private string _imageCountText = "0 of 0";
        private ImageClass _selectedClass;
        private List<YoloAnnotation> _currentAnnotations = new();
        private MainWindowViewModel? _mainViewModel;

        public string SourceFolderPath
        {
            get => _sourceFolderPath;
            set
            {
                if (SetProperty(ref _sourceFolderPath, value))
                {
                    LoadImagesFromSource();
                    SaveSettings();
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

        public List<YoloAnnotation> CurrentAnnotations
        {
            get => _currentAnnotations;
            private set => SetProperty(ref _currentAnnotations, value);
        }

        public bool HasPreviousImage => _currentImageIndex > 0;
        public bool HasNextImage => _currentImageIndex >= 0 && _currentImageIndex < _sourceImageFiles.Count - 1;

        public ICommand BrowseSourceFolderCommand { get; }
        public ICommand SelectClassCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand PreviousImageCommand { get; }

        public LabelViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseSourceFolderCommand = new RelayCommand(() => { });
            SelectClassCommand = new RelayCommand<ImageClass>(SelectClass!);
            NextImageCommand = new RelayCommand(NavigateNext, () => HasNextImage);
            PreviousImageCommand = new RelayCommand(NavigatePrevious, () => HasPreviousImage);

            // Initialize selected class
            var classes = ImageClasses;
            _selectedClass = classes.Length > 0 ? classes[0] : new ImageClass(0, "Unknown", "#808080");

            // Load saved settings
            if (_mainViewModel != null)
            {
                _sourceFolderPath = _mainViewModel.Settings.LabelSourceFolder;
                var savedClassId = _mainViewModel.Settings.LabelSelectedClassId;
                _selectedClass = ImageClasses.FirstOrDefault(c => c.Id == savedClassId) ?? _selectedClass;

                // Load images if source folder exists
                if (!string.IsNullOrEmpty(_sourceFolderPath))
                {
                    LoadImagesFromSource();
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
            CurrentAnnotations = new List<YoloAnnotation>();

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
                CurrentAnnotations = new List<YoloAnnotation>();
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
                CurrentAnnotations = new List<YoloAnnotation>();
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
                    CurrentAnnotations = lines
                        .Select(YoloAnnotation.Parse)
                        .Where(a => a != null)
                        .Cast<YoloAnnotation>()
                        .ToList();
                }
                catch
                {
                    CurrentAnnotations = new List<YoloAnnotation>();
                }
            }
            else
            {
                // Create empty annotation file
                try
                {
                    File.WriteAllText(annotationPath, string.Empty);
                    CurrentAnnotations = new List<YoloAnnotation>();
                }
                catch
                {
                    CurrentAnnotations = new List<YoloAnnotation>();
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
            (NextImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SaveSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.LabelSourceFolder = _sourceFolderPath;
                _mainViewModel.Settings.LabelSelectedClassId = SelectedClass.Id;
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
    }
}
