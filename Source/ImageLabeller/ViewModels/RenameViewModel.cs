using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class RenameViewModel : ViewModelBase
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };

        private readonly MainWindowViewModel? _mainViewModel;
        private string _sourceFolderPath = string.Empty;
        private int _nextFileIndex = 1;
        private ObservableCollection<string> _fileList = new();
        private string _selectedClassName = string.Empty;
        private bool _renameAll = false;
        private bool _isLoadingFiles = false;

        public string SourceFolderPath
        {
            get => _sourceFolderPath;
            set
            {
                if (SetProperty(ref _sourceFolderPath, value))
                {
                    LoadFilesFromSource();
                    SaveSettings();
                }
            }
        }

        public int NextFileIndex
        {
            get => _nextFileIndex;
            set
            {
                if (SetProperty(ref _nextFileIndex, value))
                {
                    SaveSettings();
                }
            }
        }

        public ObservableCollection<string> FileList
        {
            get => _fileList;
            private set => SetProperty(ref _fileList, value);
        }

        public string SelectedClassName
        {
            get => _selectedClassName;
            set
            {
                if (SetProperty(ref _selectedClassName, value))
                {
                    // Only reload files if not already in LoadFilesFromSource (prevents recursion)
                    if (!_isLoadingFiles)
                    {
                        LoadFilesFromSource();
                    }
                    SaveSettings();
                    (RenameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool RenameAll
        {
            get => _renameAll;
            set
            {
                if (SetProperty(ref _renameAll, value))
                {
                    SaveSettings();
                }
            }
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

        public ICommand BrowseSourceFolderCommand { get; }
        public ICommand RenameCommand { get; }

        public RenameViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseSourceFolderCommand = new RelayCommand(() => { });
            RenameCommand = new RelayCommand(ExecuteRename, CanRename);

            // Load saved settings
            if (_mainViewModel != null)
            {
                _sourceFolderPath = _mainViewModel.Settings.RenameSourceFolder;
                _nextFileIndex = _mainViewModel.Settings.RenameNextFileIndex;
                _selectedClassName = _mainViewModel.Settings.RenameSelectedClassName;
                _renameAll = _mainViewModel.Settings.RenameAll;

                // Default to first class if no class selected
                if (string.IsNullOrEmpty(_selectedClassName) && ClassNames.Length > 0)
                {
                    _selectedClassName = ClassNames[0];
                }

                // Load files if source folder exists
                if (!string.IsNullOrEmpty(_sourceFolderPath))
                {
                    LoadFilesFromSource();
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

        private void LoadFilesFromSource()
        {
            _isLoadingFiles = true;
            try
            {
                FileList.Clear();

                if (string.IsNullOrEmpty(SourceFolderPath) || !Directory.Exists(SourceFolderPath))
                {
                    NextFileIndex = 1;
                    (RenameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    return;
                }

                // Auto-select class if folder name matches a class name
                var folderName = Path.GetFileName(SourceFolderPath.TrimEnd(Path.DirectorySeparatorChar));
                var matchingClass = ClassNames.FirstOrDefault(c =>
                    c.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                if (matchingClass != null && _selectedClassName != matchingClass)
                {
                    SelectedClassName = matchingClass; // Use property to trigger bindings
                }

                // Get all image files
                var imageFiles = Directory.GetFiles(SourceFolderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

                foreach (var file in imageFiles)
                {
                    FileList.Add(Path.GetFileName(file));
                }

            // Notify command to re-evaluate CanExecute
            (RenameCommand as RelayCommand)?.RaiseCanExecuteChanged();

                // Parse existing files to find the next index
                if (!string.IsNullOrEmpty(_selectedClassName))
                {
                    var pattern = $@"^{Regex.Escape(_selectedClassName)}_(\d+)\.(jpg|jpeg|png)$";
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);

                    int maxIndex = 0;
                    foreach (var file in imageFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var match = regex.Match(fileName);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                        {
                            if (index > maxIndex)
                            {
                                maxIndex = index;
                            }
                        }
                    }

                    NextFileIndex = maxIndex + 1;
                }
            }
            finally
            {
                _isLoadingFiles = false;
            }
        }

        private bool CanRename()
        {
            return !string.IsNullOrEmpty(SourceFolderPath) &&
                   !string.IsNullOrEmpty(SelectedClassName) &&
                   Directory.Exists(SourceFolderPath) &&
                   FileList.Count > 0;
        }

        private void ExecuteRename()
        {
            if (!CanRename())
                return;

            try
            {
                var imageFiles = Directory.GetFiles(SourceFolderPath)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f)
                    .ToList();

                var pattern = $@"^{Regex.Escape(SelectedClassName)}_";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);

                int currentIndex = NextFileIndex;

                foreach (var imagePath in imageFiles)
                {
                    var fileName = Path.GetFileName(imagePath);

                    // Skip files that already match the pattern (unless RenameAll is true)
                    if (!RenameAll && regex.IsMatch(fileName))
                        continue;

                    // Get the extension
                    var extension = Path.GetExtension(imagePath);

                    // Create new filename
                    var newFileName = $"{SelectedClassName}_{currentIndex:D4}{extension}";
                    var newImagePath = Path.Combine(SourceFolderPath, newFileName);

                    // Check if destination file already exists
                    if (File.Exists(newImagePath))
                    {
                        // Skip this file to avoid overwriting
                        continue;
                    }

                    // Rename the image file
                    File.Move(imagePath, newImagePath);

                    // Check for corresponding label file
                    var labelPath = Path.ChangeExtension(imagePath, ".txt");
                    if (File.Exists(labelPath))
                    {
                        var newLabelPath = Path.ChangeExtension(newImagePath, ".txt");
                        if (!File.Exists(newLabelPath))
                        {
                            File.Move(labelPath, newLabelPath);
                        }
                    }

                    currentIndex++;
                }

                // Update the next file index
                NextFileIndex = currentIndex;

                // Refresh the file list
                LoadFilesFromSource();
            }
            catch (Exception ex)
            {
                // In production, show error to user
                System.Diagnostics.Debug.WriteLine($"Error renaming files: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.RenameSourceFolder = _sourceFolderPath;
                _mainViewModel.Settings.RenameNextFileIndex = _nextFileIndex;
                _mainViewModel.Settings.RenameSelectedClassName = _selectedClassName;
                _mainViewModel.Settings.RenameAll = _renameAll;
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
