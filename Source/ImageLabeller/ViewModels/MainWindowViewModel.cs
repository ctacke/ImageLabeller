using ImageLabeller.Models;
using ImageLabeller.Services;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase? _currentView;
        private readonly ClassesViewModel _classesViewModel;
        private readonly SortViewModel _sortViewModel;
        private readonly LabelViewModel _labelViewModel;
        private readonly ModelViewModel _modelViewModel;
        private readonly ExtractViewModel _extractViewModel;
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
                    OnPropertyChanged(nameof(IsModelViewActive));
                    OnPropertyChanged(nameof(IsExtractViewActive));
                    SaveCurrentView();
                }
            }
        }

        public bool IsClassesViewActive => CurrentView == _classesViewModel;
        public bool IsSortViewActive => CurrentView == _sortViewModel;
        public bool IsLabelViewActive => CurrentView == _labelViewModel;
        public bool IsModelViewActive => CurrentView == _modelViewModel;
        public bool IsExtractViewActive => CurrentView == _extractViewModel;

        public ICommand NavigateToClasses { get; }
        public ICommand NavigateToSort { get; }
        public ICommand NavigateToLabel { get; }
        public ICommand NavigateToModel { get; }
        public ICommand NavigateToExtract { get; }

        public MainWindowViewModel()
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            // Create view models once and reuse them
            _classesViewModel = new ClassesViewModel(this);
            _sortViewModel = new SortViewModel(this);
            _modelViewModel = new ModelViewModel(this);
            _labelViewModel = new LabelViewModel(this);
            _extractViewModel = new ExtractViewModel(this);

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

            // Restore last active view or default to Sort
            RestoreLastView();
        }

        private void RestoreLastView()
        {
            CurrentView = Settings.LastActiveView switch
            {
                "Classes" => _classesViewModel,
                "Label" => _labelViewModel,
                "Model" => _modelViewModel,
                "Extract" => _extractViewModel,
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
            else if (CurrentView == _modelViewModel)
            {
                Settings.LastActiveView = "Model";
            }
            else if (CurrentView == _extractViewModel)
            {
                Settings.LastActiveView = "Extract";
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
        }
    }
}
