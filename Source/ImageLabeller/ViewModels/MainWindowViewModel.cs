using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase? _currentView;
        private readonly SortViewModel _sortViewModel;
        private readonly LabelViewModel _labelViewModel;

        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsSortViewActive));
                    OnPropertyChanged(nameof(IsLabelViewActive));
                }
            }
        }

        public bool IsSortViewActive => CurrentView == _sortViewModel;
        public bool IsLabelViewActive => CurrentView == _labelViewModel;

        public ICommand NavigateToSort { get; }
        public ICommand NavigateToLabel { get; }

        public MainWindowViewModel()
        {
            // Create view models once and reuse them
            _sortViewModel = new SortViewModel();
            _labelViewModel = new LabelViewModel();

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

            // Start with Sort view by default
            CurrentView = _sortViewModel;
        }
    }
}
