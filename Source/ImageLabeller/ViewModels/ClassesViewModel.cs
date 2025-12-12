using ImageLabeller.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class ClassesViewModel : ViewModelBase
    {
        private static readonly string[] ColorPalette = new[]
        {
            "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
            "#FFEAA7", "#DFE6E9", "#74B9FF", "#A29BFE",
            "#FD79A8", "#FDCB6E", "#6C5CE7", "#00B894",
            "#E17055", "#D63031", "#F39C12", "#8E44AD"
        };

        private readonly MainWindowViewModel? _mainViewModel;
        private ObservableCollection<ClassItem> _classes = new();
        private ClassItem? _selectedClass;
        private string _newClassName = string.Empty;
        private bool _isEditing;
        private ClassItem? _editingClass;

        public ObservableCollection<ClassItem> Classes
        {
            get => _classes;
            set => SetProperty(ref _classes, value);
        }

        public ClassItem? SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }

        public string NewClassName
        {
            get => _newClassName;
            set
            {
                if (SetProperty(ref _newClassName, value))
                {
                    // Notify commands to re-evaluate their CanExecute
                    (AddClassCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public ICommand AddClassCommand { get; }
        public ICommand EditClassCommand { get; }
        public ICommand DeleteClassCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public ClassesViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            AddClassCommand = new RelayCommand(AddClass, CanAddClass);
            EditClassCommand = new RelayCommand<ClassItem>(EditClass!);
            DeleteClassCommand = new RelayCommand<ClassItem>(DeleteClass!);
            SaveEditCommand = new RelayCommand(SaveEdit, CanSaveEdit);
            CancelEditCommand = new RelayCommand(CancelEdit);
            MoveUpCommand = new RelayCommand<ClassItem>(MoveUp!);
            MoveDownCommand = new RelayCommand<ClassItem>(MoveDown!);

            RebuildFromSettings();
        }

        private bool CanAddClass()
        {
            return !string.IsNullOrWhiteSpace(NewClassName) &&
                   !Classes.Any(c => c.Name.Equals(NewClassName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void AddClass()
        {
            if (!CanAddClass())
                return;

            var className = NewClassName.Trim();
            _mainViewModel?.Settings.ClassNames.Add(className);
            SaveAndRebuild();

            NewClassName = string.Empty;
            (AddClassCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void EditClass(ClassItem item)
        {
            if (item == null)
                return;

            IsEditing = true;
            _editingClass = item;
            NewClassName = item.Name;
        }

        private bool CanSaveEdit()
        {
            return !string.IsNullOrWhiteSpace(NewClassName) &&
                   (_editingClass == null || !Classes.Any(c => c != _editingClass && c.Name.Equals(NewClassName.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        private void SaveEdit()
        {
            if (!CanSaveEdit() || _editingClass == null || _mainViewModel == null)
                return;

            var newName = NewClassName.Trim();
            var index = _mainViewModel.Settings.ClassNames.IndexOf(_editingClass.Name);
            if (index >= 0)
            {
                _mainViewModel.Settings.ClassNames[index] = newName;
                SaveAndRebuild();
            }

            CancelEdit();
        }

        private void CancelEdit()
        {
            IsEditing = false;
            _editingClass = null;
            NewClassName = string.Empty;
        }

        private async void DeleteClass(ClassItem item)
        {
            if (item == null || _mainViewModel == null)
                return;

            // Show warning
            var confirmed = await ShowConfirmationAsync(
                "Deleting this class may invalidate existing annotations and sorted images. Continue?");

            if (!confirmed)
                return;

            _mainViewModel.Settings.ClassNames.Remove(item.Name);
            SaveAndRebuild();
        }

        private async void MoveUp(ClassItem item)
        {
            if (item == null || item.Index == 0 || _mainViewModel == null)
                return;

            var confirmed = await ShowConfirmationAsync(
                "Reordering classes will change their IDs and may invalidate existing annotations. Continue?");

            if (!confirmed)
                return;

            var index = item.Index;
            var temp = _mainViewModel.Settings.ClassNames[index];
            _mainViewModel.Settings.ClassNames[index] = _mainViewModel.Settings.ClassNames[index - 1];
            _mainViewModel.Settings.ClassNames[index - 1] = temp;

            SaveAndRebuild();
        }

        private async void MoveDown(ClassItem item)
        {
            if (item == null || _mainViewModel == null || item.Index >= _mainViewModel.Settings.ClassNames.Count - 1)
                return;

            var confirmed = await ShowConfirmationAsync(
                "Reordering classes will change their IDs and may invalidate existing annotations. Continue?");

            if (!confirmed)
                return;

            var index = item.Index;
            var temp = _mainViewModel.Settings.ClassNames[index];
            _mainViewModel.Settings.ClassNames[index] = _mainViewModel.Settings.ClassNames[index + 1];
            _mainViewModel.Settings.ClassNames[index + 1] = temp;

            SaveAndRebuild();
        }

        private void RebuildFromSettings()
        {
            Classes.Clear();

            if (_mainViewModel == null)
                return;

            for (int i = 0; i < _mainViewModel.Settings.ClassNames.Count; i++)
            {
                Classes.Add(new ClassItem
                {
                    Index = i,
                    Name = _mainViewModel.Settings.ClassNames[i],
                    Color = ColorPalette[i % ColorPalette.Length]
                });
            }
        }

        private void SaveAndRebuild()
        {
            _mainViewModel?.SaveSettings();
            RebuildFromSettings();
            _mainViewModel?.NotifyClassesChanged();
        }

        private async System.Threading.Tasks.Task<bool> ShowConfirmationAsync(string message)
        {
            // Simple confirmation for now - can be enhanced with proper dialog
            // For now, always return true (allow operation)
            // TODO: Implement proper confirmation dialog
            return await System.Threading.Tasks.Task.FromResult(true);
        }
    }
}
