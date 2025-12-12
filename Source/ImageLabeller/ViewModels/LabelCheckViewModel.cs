using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ImageLabeller.Models;

namespace ImageLabeller.ViewModels
{
    public class ImageFileItem : ViewModelBase
    {
        private string _fileName = string.Empty;
        private string _fullPath = string.Empty;
        private string _status = string.Empty;
        private bool _hasIssue;
        private bool _isMisClassed;
        private int? _actualClassId;

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool HasIssue
        {
            get => _hasIssue;
            set
            {
                if (SetProperty(ref _hasIssue, value))
                {
                    OnPropertyChanged(nameof(FileNameColor));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public bool IsMisClassed
        {
            get => _isMisClassed;
            set => SetProperty(ref _isMisClassed, value);
        }

        public int? ActualClassId
        {
            get => _actualClassId;
            set => SetProperty(ref _actualClassId, value);
        }

        public string FileNameColor => HasIssue ? "#FF6B6B" : "#CCCCCC";
        public string StatusColor => HasIssue ? "#FF6B6B" : "#808080";
    }

    public class LabelCheckViewModel : ViewModelBase
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };

        private readonly MainWindowViewModel? _mainViewModel;
        private string _sourceImageFolder = string.Empty;
        private string _sourceLabelFolder = string.Empty;
        private string _selectedClassName = string.Empty;
        private ObservableCollection<ImageFileItem> _imageList = new();
        private bool _canReClass = false;
        private string _validationSummary = string.Empty;
        private string _validationSummaryColor = "#CCCCCC";

        public string SourceImageFolder
        {
            get => _sourceImageFolder;
            set
            {
                if (SetProperty(ref _sourceImageFolder, value))
                {
                    // Auto-select class if folder name matches
                    if (!string.IsNullOrEmpty(value))
                    {
                        var folderName = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar));
                        var matchingClass = ClassNames.FirstOrDefault(c =>
                            c.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                        if (matchingClass != null && _selectedClassName != matchingClass)
                        {
                            SelectedClassName = matchingClass;
                        }
                    }

                    // Auto-fill label folder if empty
                    if (string.IsNullOrEmpty(_sourceLabelFolder))
                    {
                        SourceLabelFolder = value;
                    }

                    LoadImages();
                    SaveSettings();
                    (ValidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SourceLabelFolder
        {
            get => _sourceLabelFolder;
            set
            {
                if (SetProperty(ref _sourceLabelFolder, value))
                {
                    SaveSettings();
                    (ValidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedClassName
        {
            get => _selectedClassName;
            set
            {
                if (SetProperty(ref _selectedClassName, value))
                {
                    SaveSettings();
                    (ValidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<ImageFileItem> ImageList
        {
            get => _imageList;
            private set => SetProperty(ref _imageList, value);
        }

        public bool CanReClass
        {
            get => _canReClass;
            private set
            {
                if (SetProperty(ref _canReClass, value))
                {
                    (ReClassCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string ValidationSummary
        {
            get => _validationSummary;
            private set => SetProperty(ref _validationSummary, value);
        }

        public string ValidationSummaryColor
        {
            get => _validationSummaryColor;
            private set => SetProperty(ref _validationSummaryColor, value);
        }

        public string[] ClassNames
        {
            get
            {
                if (_mainViewModel == null)
                    return Array.Empty<string>();

                return _mainViewModel.Settings.ClassNames.ToArray();
            }
        }

        public ICommand BrowseImageFolderCommand { get; }
        public ICommand BrowseLabelFolderCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand ReClassCommand { get; }

        public LabelCheckViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseImageFolderCommand = new RelayCommand(() => { });
            BrowseLabelFolderCommand = new RelayCommand(() => { });
            ValidateCommand = new RelayCommand(ExecuteValidate, CanValidate);
            ReClassCommand = new RelayCommand(ExecuteReClass, () => CanReClass);

            // Load saved settings
            if (_mainViewModel != null)
            {
                _sourceImageFolder = _mainViewModel.Settings.LabelCheckSourceImageFolder;
                _sourceLabelFolder = _mainViewModel.Settings.LabelCheckSourceLabelFolder;
                _selectedClassName = _mainViewModel.Settings.LabelCheckSelectedClassName;

                // Default to first class if no class selected
                if (string.IsNullOrEmpty(_selectedClassName) && ClassNames.Length > 0)
                {
                    _selectedClassName = ClassNames[0];
                }

                // Load images if source folder exists
                if (!string.IsNullOrEmpty(_sourceImageFolder))
                {
                    LoadImages();
                }
            }
        }

        public void SetImageFolderPickerCallback(Func<string?> browseImageFolder)
        {
            (BrowseImageFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseImageFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    SourceImageFolder = folder;
                }
            });
        }

        public void SetLabelFolderPickerCallback(Func<string?> browseLabelFolder)
        {
            (BrowseLabelFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseLabelFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    SourceLabelFolder = folder;
                }
            });
        }

        private void LoadImages()
        {
            ImageList.Clear();
            ValidationSummary = string.Empty;
            ValidationSummaryColor = "#CCCCCC";
            CanReClass = false;

            if (string.IsNullOrEmpty(SourceImageFolder) || !Directory.Exists(SourceImageFolder))
            {
                (ValidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                return;
            }

            var imageFiles = Directory.GetFiles(SourceImageFolder)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var imagePath in imageFiles)
            {
                ImageList.Add(new ImageFileItem
                {
                    FileName = Path.GetFileName(imagePath),
                    FullPath = imagePath,
                    Status = "Not validated",
                    HasIssue = false,
                    IsMisClassed = false
                });
            }

            (ValidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanValidate()
        {
            return !string.IsNullOrEmpty(SourceImageFolder) &&
                   !string.IsNullOrEmpty(SourceLabelFolder) &&
                   !string.IsNullOrEmpty(SelectedClassName) &&
                   Directory.Exists(SourceImageFolder) &&
                   Directory.Exists(SourceLabelFolder) &&
                   ImageList.Count > 0;
        }

        private void ExecuteValidate()
        {
            if (!CanValidate())
                return;

            int missingLabelCount = 0;
            int misClassedCount = 0;
            int okCount = 0;

            // Find the class ID for the selected class name
            var selectedClassId = Array.IndexOf(ClassNames, SelectedClassName);
            if (selectedClassId < 0)
                return;

            foreach (var imageItem in ImageList)
            {
                var labelPath = Path.Combine(SourceLabelFolder, Path.ChangeExtension(imageItem.FileName, ".txt"));

                if (!File.Exists(labelPath))
                {
                    imageItem.Status = "Missing label file";
                    imageItem.HasIssue = true;
                    imageItem.IsMisClassed = false;
                    missingLabelCount++;
                }
                else
                {
                    try
                    {
                        var labelLines = File.ReadAllLines(labelPath);
                        if (labelLines.Length == 0)
                        {
                            imageItem.Status = "Empty label file";
                            imageItem.HasIssue = true;
                            imageItem.IsMisClassed = false;
                            missingLabelCount++;
                        }
                        else
                        {
                            // Check if any annotation has wrong class ID
                            bool hasMisClass = false;
                            bool hasValidAnnotation = false;
                            int? actualClassId = null;

                            foreach (var line in labelLines)
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                var parts = line.Split(' ');
                                if (parts.Length >= 5 && int.TryParse(parts[0], out int classId))
                                {
                                    hasValidAnnotation = true;
                                    if (classId != selectedClassId)
                                    {
                                        hasMisClass = true;
                                        actualClassId = classId;
                                        break;
                                    }
                                }
                            }

                            if (!hasValidAnnotation)
                            {
                                imageItem.Status = "Invalid label format";
                                imageItem.HasIssue = true;
                                imageItem.IsMisClassed = false;
                                missingLabelCount++;
                            }
                            else if (hasMisClass)
                            {
                                var actualClassName = actualClassId.HasValue && actualClassId.Value < ClassNames.Length
                                    ? ClassNames[actualClassId.Value]
                                    : actualClassId?.ToString() ?? "unknown";
                                imageItem.Status = $"Wrong class: {actualClassName} (expected: {SelectedClassName})";
                                imageItem.HasIssue = true;
                                imageItem.IsMisClassed = true;
                                imageItem.ActualClassId = actualClassId;
                                misClassedCount++;
                            }
                            else
                            {
                                imageItem.Status = "OK";
                                imageItem.HasIssue = false;
                                imageItem.IsMisClassed = false;
                                okCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        imageItem.Status = $"Error reading label: {ex.Message}";
                        imageItem.HasIssue = true;
                        imageItem.IsMisClassed = false;
                    }
                }
            }

            // Update summary
            var hasErrors = misClassedCount > 0 || missingLabelCount > 0;
            var prefix = hasErrors ? "⚠ " : "";
            ValidationSummary = $"{prefix}Validation complete: {okCount} OK, {misClassedCount} mis-classed, {missingLabelCount} missing/invalid labels";
            ValidationSummaryColor = hasErrors ? "#FF6B6B" : "#4CAF50";

            // Enable re-class button if there are mis-classed items
            CanReClass = misClassedCount > 0;

            // Notify UI to refresh
            OnPropertyChanged(nameof(ImageList));
        }

        private void ExecuteReClass()
        {
            if (!CanReClass)
                return;

            var selectedClassId = Array.IndexOf(ClassNames, SelectedClassName);
            if (selectedClassId < 0)
                return;

            int reClassedCount = 0;

            foreach (var imageItem in ImageList.Where(i => i.IsMisClassed))
            {
                var labelPath = Path.Combine(SourceLabelFolder, Path.ChangeExtension(imageItem.FileName, ".txt"));

                if (File.Exists(labelPath))
                {
                    try
                    {
                        var labelLines = File.ReadAllLines(labelPath);
                        var updatedLines = labelLines.Select(line =>
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                return line;

                            var parts = line.Split(' ');
                            if (parts.Length >= 5)
                            {
                                // Replace class ID with selected class ID
                                parts[0] = selectedClassId.ToString();
                                return string.Join(" ", parts);
                            }
                            return line;
                        }).ToArray();

                        File.WriteAllLines(labelPath, updatedLines);

                        imageItem.Status = "Re-classed";
                        imageItem.HasIssue = false;
                        imageItem.IsMisClassed = false;
                        reClassedCount++;
                    }
                    catch (Exception ex)
                    {
                        imageItem.Status = $"Error re-classing: {ex.Message}";
                    }
                }
            }

            ValidationSummary = $"Re-classed {reClassedCount} files";
            CanReClass = false;

            // Notify UI to refresh
            OnPropertyChanged(nameof(ImageList));
        }

        private void SaveSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.LabelCheckSourceImageFolder = _sourceImageFolder;
                _mainViewModel.Settings.LabelCheckSourceLabelFolder = _sourceLabelFolder;
                _mainViewModel.Settings.LabelCheckSelectedClassName = _selectedClassName;
                _mainViewModel.SaveSettings();
            }
        }

        public void RefreshClasses()
        {
            OnPropertyChanged(nameof(ClassNames));

            // Re-validate selected class
            var classes = ClassNames;
            if (classes.Length > 0 && !classes.Contains(SelectedClassName))
            {
                SelectedClassName = classes[0];
            }
        }
    }
}
