using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class SortViewModel : ViewModelBase
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };

        // Dynamic property for binding - builds from settings
        public string[] ImageClasses
        {
            get
            {
                var classes = new List<string> { "none" };
                if (_mainViewModel != null)
                {
                    classes.AddRange(_mainViewModel.Settings.ClassNames);
                }
                return classes.ToArray();
            }
        }

        private string _sourceFolderPath = string.Empty;
        private string _destinationFolderPath = string.Empty;
        private Bitmap? _currentImage;
        private int _currentImageIndex = -1;
        private List<string> _sourceImageFiles = new();
        private string _progressText = "No images";
        private string? _lastMovedImagePath;
        private string? _lastMovedFromFolder;

        public string SourceFolderPath
        {
            get => _sourceFolderPath;
            set
            {
                if (SetProperty(ref _sourceFolderPath, value))
                {
                    LoadImagesFromSource();
                    SaveFolderSettings();
                }
            }
        }

        public string DestinationFolderPath
        {
            get => _destinationFolderPath;
            set
            {
                if (SetProperty(ref _destinationFolderPath, value))
                {
                    UpdateNavigationState();
                    SaveFolderSettings();
                }
            }
        }

        public Bitmap? CurrentImage
        {
            get => _currentImage;
            private set => SetProperty(ref _currentImage, value);
        }

        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        public bool HasPreviousImage => _currentImageIndex > 0 || CanUndo;

        public bool HasNextImage => _currentImageIndex >= 0 && _currentImageIndex < _sourceImageFiles.Count - 1;

        public bool CanUndo => _currentImageIndex == 0 && !string.IsNullOrEmpty(_lastMovedImagePath);

        public bool CanClassify => _currentImageIndex >= 0 &&
                                   _currentImageIndex < _sourceImageFiles.Count &&
                                   !string.IsNullOrEmpty(DestinationFolderPath);

        public ICommand BrowseSourceFolderCommand { get; }
        public ICommand BrowseDestinationFolderCommand { get; }
        public ICommand ClassifyImageCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand PreviousImageCommand { get; }

        private MainWindowViewModel? _mainViewModel;

        public SortViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseSourceFolderCommand = new RelayCommand(() => { });
            BrowseDestinationFolderCommand = new RelayCommand(() => { });
            ClassifyImageCommand = new RelayCommand<string>(MoveImageToClass, _ => CanClassify);
            NextImageCommand = new RelayCommand(NavigateNext, () => HasNextImage);
            PreviousImageCommand = new RelayCommand(NavigatePrevious, () => HasPreviousImage);

            // Load saved folder paths
            if (_mainViewModel != null)
            {
                _sourceFolderPath = _mainViewModel.Settings.SortSourceFolder;
                _destinationFolderPath = _mainViewModel.Settings.SortDestinationFolder;

                // Load images if source folder exists
                if (!string.IsNullOrEmpty(_sourceFolderPath))
                {
                    LoadImagesFromSource();
                }
            }
        }

        public void SetFolderPickerCallback(Func<string?> browseSourceFolder, Func<string?> browseDestinationFolder)
        {
            (BrowseSourceFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseSourceFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    SourceFolderPath = folder;
                }
            });

            (BrowseDestinationFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseDestinationFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    DestinationFolderPath = folder;
                }
            });
        }

        private void LoadImagesFromSource()
        {
            _sourceImageFiles.Clear();
            _currentImageIndex = -1;
            CurrentImage?.Dispose();
            CurrentImage = null;
            _lastMovedImagePath = null;
            _lastMovedFromFolder = null;

            if (string.IsNullOrEmpty(SourceFolderPath) || !Directory.Exists(SourceFolderPath))
            {
                UpdateProgress();
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

            UpdateProgress();
        }

        private void LoadImage(int index)
        {
            if (index < 0 || index >= _sourceImageFiles.Count)
            {
                CurrentImage?.Dispose();
                CurrentImage = null;
                _currentImageIndex = -1;
                UpdateNavigationState();
                return;
            }

            try
            {
                CurrentImage?.Dispose();
                _currentImageIndex = index;
                CurrentImage = new Bitmap(_sourceImageFiles[index]);
            }
            catch
            {
                CurrentImage = null;
            }

            UpdateNavigationState();
        }

        private void MoveImageToClass(string? className)
        {
            if (string.IsNullOrEmpty(className) ||
                _currentImageIndex < 0 ||
                _currentImageIndex >= _sourceImageFiles.Count ||
                string.IsNullOrEmpty(DestinationFolderPath))
            {
                return;
            }

            try
            {
                var currentImagePath = _sourceImageFiles[_currentImageIndex];
                var destinationFolder = Path.Combine(DestinationFolderPath, className);

                Directory.CreateDirectory(destinationFolder);

                var fileName = Path.GetFileName(currentImagePath);
                var destinationPath = Path.Combine(destinationFolder, fileName);

                CurrentImage?.Dispose();
                CurrentImage = null;

                // Cache for undo
                _lastMovedImagePath = destinationPath;
                _lastMovedFromFolder = currentImagePath;

                // Move file (overwrite if exists)
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(currentImagePath, destinationPath);

                // Remove from list
                _sourceImageFiles.RemoveAt(_currentImageIndex);

                // Load next image or show empty
                if (_sourceImageFiles.Count > 0)
                {
                    if (_currentImageIndex >= _sourceImageFiles.Count)
                    {
                        _currentImageIndex = _sourceImageFiles.Count - 1;
                    }
                    LoadImage(_currentImageIndex);
                }
                else
                {
                    _currentImageIndex = -1;
                    UpdateNavigationState();
                }

                UpdateProgress();
            }
            catch (Exception ex)
            {
                // In a real app, show error to user
                Console.WriteLine($"Error moving file: {ex.Message}");
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
            if (_currentImageIndex > 0)
            {
                LoadImage(_currentImageIndex - 1);
            }
            else if (CanUndo)
            {
                UndoLastMove();
            }
        }

        private void UndoLastMove()
        {
            if (string.IsNullOrEmpty(_lastMovedImagePath) || string.IsNullOrEmpty(_lastMovedFromFolder))
            {
                return;
            }

            try
            {
                CurrentImage?.Dispose();
                CurrentImage = null;

                // Move file back
                if (File.Exists(_lastMovedFromFolder))
                {
                    File.Delete(_lastMovedFromFolder);
                }
                File.Move(_lastMovedImagePath, _lastMovedFromFolder);

                // Insert back at beginning
                _sourceImageFiles.Insert(0, _lastMovedFromFolder);

                // Clear cache
                _lastMovedImagePath = null;
                _lastMovedFromFolder = null;

                // Load the restored image
                LoadImage(0);
                UpdateProgress();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error undoing move: {ex.Message}");
            }
        }

        private void UpdateProgress()
        {
            ProgressText = _sourceImageFiles.Count == 0
                ? "No images"
                : _sourceImageFiles.Count == 1
                    ? "1 image"
                    : $"{_sourceImageFiles.Count} images";

            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(HasPreviousImage));
            OnPropertyChanged(nameof(HasNextImage));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanClassify));
            (ClassifyImageCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
            (NextImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SaveFolderSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.SortSourceFolder = _sourceFolderPath;
                _mainViewModel.Settings.SortDestinationFolder = _destinationFolderPath;
                _mainViewModel.SaveSettings();
            }
        }

        public void RefreshClasses()
        {
            OnPropertyChanged(nameof(ImageClasses));
        }
    }
}
