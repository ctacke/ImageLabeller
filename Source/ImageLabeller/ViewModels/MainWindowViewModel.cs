using ImageLabeller.Models;
using ImageLabeller.Services;
using System;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase? _currentView;
        private readonly ClassesViewModel _classesViewModel;
        private readonly SortViewModel _sortViewModel;
        private readonly LabelViewModel _labelViewModel;
        private readonly LabelCheckViewModel _labelCheckViewModel;
        private readonly ModelViewModel _modelViewModel;
        private readonly ExtractViewModel _extractViewModel;
        private readonly RenameViewModel _renameViewModel;
        private readonly SettingsService _settingsService;

        public UserSettings Settings { get; private set; }
        public ModelViewModel ModelViewModel => _modelViewModel;

        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsClassesViewActive));
                    OnPropertyChanged(nameof(IsSortViewActive));
                    OnPropertyChanged(nameof(IsLabelViewActive));
                    OnPropertyChanged(nameof(IsLabelCheckViewActive));
                    OnPropertyChanged(nameof(IsModelViewActive));
                    OnPropertyChanged(nameof(IsExtractViewActive));
                    OnPropertyChanged(nameof(IsRenameViewActive));
                    SaveCurrentView();
                }
            }
        }

        public bool IsClassesViewActive => CurrentView == _classesViewModel;
        public bool IsSortViewActive => CurrentView == _sortViewModel;
        public bool IsLabelViewActive => CurrentView == _labelViewModel;
        public bool IsLabelCheckViewActive => CurrentView == _labelCheckViewModel;
        public bool IsModelViewActive => CurrentView == _modelViewModel;
        public bool IsExtractViewActive => CurrentView == _extractViewModel;
        public bool IsRenameViewActive => CurrentView == _renameViewModel;

        public ICommand NavigateToClasses { get; }
        public ICommand NavigateToSort { get; }
        public ICommand NavigateToLabel { get; }
        public ICommand NavigateToLabelCheck { get; }
        public ICommand NavigateToModel { get; }
        public ICommand NavigateToExtract { get; }
        public ICommand NavigateToRename { get; }
        public ICommand RevealConfigCommand { get; }

        public MainWindowViewModel()
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            // Create view models once and reuse them
            _classesViewModel = new ClassesViewModel(this);
            _sortViewModel = new SortViewModel(this);
            _modelViewModel = new ModelViewModel(this);
            _labelViewModel = new LabelViewModel(this);
            _labelCheckViewModel = new LabelCheckViewModel(this);
            _extractViewModel = new ExtractViewModel(this);
            _renameViewModel = new RenameViewModel(this);

            NavigateToClasses = new RelayCommand(() =>
            {
                if (CurrentView != _classesViewModel)
                {
                    CurrentView = _classesViewModel;
                }
            });

            NavigateToSort = new RelayCommand(() =>
            {
                if (CurrentView != _sortViewModel)
                {
                    CurrentView = _sortViewModel;
                }
            });

            NavigateToLabel = new RelayCommand(() =>
            {
                if (CurrentView != _labelViewModel)
                {
                    CurrentView = _labelViewModel;
                }
            });

            NavigateToLabelCheck = new RelayCommand(() =>
            {
                if (CurrentView != _labelCheckViewModel)
                {
                    CurrentView = _labelCheckViewModel;
                }
            });

            NavigateToModel = new RelayCommand(() =>
            {
                if (CurrentView != _modelViewModel)
                {
                    CurrentView = _modelViewModel;
                }
            });

            NavigateToExtract = new RelayCommand(() =>
            {
                if (CurrentView != _extractViewModel)
                {
                    CurrentView = _extractViewModel;
                }
            });

            NavigateToRename = new RelayCommand(() =>
            {
                if (CurrentView != _renameViewModel)
                {
                    CurrentView = _renameViewModel;
                }
            });

            RevealConfigCommand = new RelayCommand(RevealConfig);

            // Restore last active view or default to Sort
            RestoreLastView();
        }

        private void RestoreLastView()
        {
            CurrentView = Settings.LastActiveView switch
            {
                "Classes" => _classesViewModel,
                "Label" => _labelViewModel,
                "LabelCheck" => _labelCheckViewModel,
                "Model" => _modelViewModel,
                "Extract" => _extractViewModel,
                "Rename" => _renameViewModel,
                _ => _sortViewModel
            };
        }

        private void SaveCurrentView()
        {
            if (CurrentView == _classesViewModel)
            {
                Settings.LastActiveView = "Classes";
            }
            else if (CurrentView == _sortViewModel)
            {
                Settings.LastActiveView = "Sort";
            }
            else if (CurrentView == _labelViewModel)
            {
                Settings.LastActiveView = "Label";
            }
            else if (CurrentView == _labelCheckViewModel)
            {
                Settings.LastActiveView = "LabelCheck";
            }
            else if (CurrentView == _modelViewModel)
            {
                Settings.LastActiveView = "Model";
            }
            else if (CurrentView == _extractViewModel)
            {
                Settings.LastActiveView = "Extract";
            }
            else if (CurrentView == _renameViewModel)
            {
                Settings.LastActiveView = "Rename";
            }

            _settingsService.Save(Settings);
        }

        public void SaveSettings()
        {
            _settingsService.Save(Settings);
        }

        public void NotifyClassesChanged()
        {
            _sortViewModel.RefreshClasses();
            _labelViewModel.RefreshClasses();
            _labelCheckViewModel.RefreshClasses();
            _renameViewModel.RefreshClasses();
        }

        private void RevealConfig()
        {
            try
            {
                var configPath = System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "user.settings");

                if (!System.IO.File.Exists(configPath))
                    return;

                // Open file explorer and select the file
                if (OperatingSystem.IsWindows())
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{configPath}\"");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    System.Diagnostics.Process.Start("open", $"-R \"{configPath}\"");
                }
                else if (OperatingSystem.IsLinux())
                {
                    // On Linux, open the containing folder
                    var folderPath = System.IO.Path.GetDirectoryName(configPath);
                    System.Diagnostics.Process.Start("xdg-open", $"\"{folderPath}\"");
                }
            }
            catch
            {
                // Silently fail if we can't open the file explorer
            }
        }
    }
}
