using Avalonia.Media.Imaging;
using ImageLabeller.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class ExtractViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel? _mainViewModel;
        private readonly VideoPlayerService _videoPlayerService;
        private CancellationTokenSource? _cancellationTokenSource;

        private string _videoFilePath = string.Empty;
        private string _outputFolderPath = string.Empty;
        private string _classNamePrefix = string.Empty;
        private Bitmap? _currentFrame;
        private int _currentFrameIndex;
        private double _totalFrames;
        private bool _isPlaying;
        private bool _isExtracting;
        private int _extractionProgress;
        private string _statusMessage = string.Empty;
        private int? _inPointFrame;
        private int? _outPointFrame;

        public string VideoFilePath
        {
            get => _videoFilePath;
            set
            {
                if (SetProperty(ref _videoFilePath, value))
                {
                    LoadVideo(value);
                }
            }
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (SetProperty(ref _outputFolderPath, value))
                {
                    SaveSettings();
                }
            }
        }

        public string ClassNamePrefix
        {
            get => _classNamePrefix;
            set
            {
                if (SetProperty(ref _classNamePrefix, value))
                {
                    SaveSettings();
                }
            }
        }

        public Bitmap? CurrentFrame
        {
            get => _currentFrame;
            private set => SetProperty(ref _currentFrame, value);
        }

        public int CurrentFrameIndex
        {
            get => _currentFrameIndex;
            set
            {
                if (SetProperty(ref _currentFrameIndex, value))
                {
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    if (!_isPlaying && _videoPlayerService.IsVideoLoaded)
                    {
                        _videoPlayerService.SeekToFrame(value);
                    }
                }
            }
        }

        public double TotalFrames
        {
            get => _totalFrames;
            private set => SetProperty(ref _totalFrames, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    OnPropertyChanged(nameof(PlayPauseButtonText));
                    UpdateCommandStates();
                }
            }
        }

        public bool IsExtracting
        {
            get => _isExtracting;
            private set
            {
                if (SetProperty(ref _isExtracting, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public int ExtractionProgress
        {
            get => _extractionProgress;
            private set => SetProperty(ref _extractionProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public int? InPointFrame
        {
            get => _inPointFrame;
            private set
            {
                if (SetProperty(ref _inPointFrame, value))
                {
                    OnPropertyChanged(nameof(InPointDisplay));
                    OnPropertyChanged(nameof(FrameCountEstimate));
                    OnPropertyChanged(nameof(HasInOutPoints));
                    UpdateCommandStates();
                    SaveSettings();
                }
            }
        }

        public int? OutPointFrame
        {
            get => _outPointFrame;
            private set
            {
                if (SetProperty(ref _outPointFrame, value))
                {
                    OnPropertyChanged(nameof(OutPointDisplay));
                    OnPropertyChanged(nameof(FrameCountEstimate));
                    OnPropertyChanged(nameof(HasInOutPoints));
                    UpdateCommandStates();
                    SaveSettings();
                }
            }
        }

        public string CurrentTimeDisplay => FormatFrameTime(CurrentFrameIndex);
        public string DurationDisplay => FormatFrameTime((int)TotalFrames);
        public string InPointDisplay => InPointFrame.HasValue ? FormatFrameTime(InPointFrame.Value) : "--:--";
        public string OutPointDisplay => OutPointFrame.HasValue ? FormatFrameTime(OutPointFrame.Value) : "--:--";
        public string PlayPauseButtonText => IsPlaying ? "Pause" : "Play";

        public bool HasInOutPoints => InPointFrame.HasValue && OutPointFrame.HasValue;

        public string FrameCountEstimate
        {
            get
            {
                if (!InPointFrame.HasValue || !OutPointFrame.HasValue)
                    return string.Empty;

                var frameCount = OutPointFrame.Value - InPointFrame.Value + 1;
                if (frameCount <= 0)
                    return "Error: In point must be before Out point";

                return $"Will extract {frameCount:N0} frames ({FormatFrameTime(InPointFrame.Value)} to {FormatFrameTime(OutPointFrame.Value)})";
            }
        }

        public bool IsVideoLoaded => _videoPlayerService.IsVideoLoaded;
        public bool CanSetInOut => IsVideoLoaded && !IsExtracting;
        public bool CanExtract => IsVideoLoaded && InPointFrame.HasValue && OutPointFrame.HasValue &&
                                  InPointFrame < OutPointFrame && !IsExtracting;

        public ICommand BrowseVideoCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand StepForwardCommand { get; }
        public ICommand StepBackwardCommand { get; }
        public ICommand SetInPointCommand { get; }
        public ICommand SetOutPointCommand { get; }
        public ICommand ExtractCommand { get; }

        public ExtractViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;
            _videoPlayerService = new VideoPlayerService();

            // Initialize commands (will be wired up from code-behind)
            BrowseVideoCommand = new RelayCommand(() => { });
            BrowseOutputFolderCommand = new RelayCommand(() => { });
            PlayPauseCommand = new RelayCommand(PlayPause, () => IsVideoLoaded && !IsExtracting);
            StopCommand = new RelayCommand(Stop, () => IsPlaying);
            StepForwardCommand = new RelayCommand(() => _videoPlayerService.StepForward(), () => IsVideoLoaded && !IsPlaying);
            StepBackwardCommand = new RelayCommand(() => _videoPlayerService.StepBackward(), () => IsVideoLoaded && !IsPlaying);
            SetInPointCommand = new RelayCommand(SetInPoint, () => CanSetInOut);
            SetOutPointCommand = new RelayCommand(SetOutPoint, () => CanSetInOut);
            ExtractCommand = new RelayCommand(async () => await ExtractFrames(), () => CanExtract);

            // Subscribe to service events
            _videoPlayerService.FrameReady += OnFrameReady;
            _videoPlayerService.PositionChanged += OnPositionChanged;
            _videoPlayerService.ErrorOccurred += OnError;

            // Load saved settings
            if (_mainViewModel != null)
            {
                _videoFilePath = _mainViewModel.Settings.ExtractLastVideoPath;
                _outputFolderPath = _mainViewModel.Settings.ExtractOutputFolder;
                _classNamePrefix = _mainViewModel.Settings.ExtractClassNamePrefix;

                // Load video if path exists
                if (!string.IsNullOrEmpty(_videoFilePath))
                {
                    LoadVideo(_videoFilePath);

                    // Restore In/Out points if they were saved
                    if (_mainViewModel.Settings.ExtractLastInFrame > 0)
                    {
                        _inPointFrame = _mainViewModel.Settings.ExtractLastInFrame;
                        OnPropertyChanged(nameof(InPointDisplay));
                    }
                    if (_mainViewModel.Settings.ExtractLastOutFrame > 0)
                    {
                        _outPointFrame = _mainViewModel.Settings.ExtractLastOutFrame;
                        OnPropertyChanged(nameof(OutPointDisplay));
                    }
                }
            }
        }

        public void SetFilePickerCallback(Func<string?> browseVideoFile)
        {
            (BrowseVideoCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var file = browseVideoFile();
                if (!string.IsNullOrEmpty(file))
                {
                    VideoFilePath = file;
                }
            });
        }

        public void SetFolderPickerCallback(Func<string?> browseOutputFolder)
        {
            (BrowseOutputFolderCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var folder = browseOutputFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    OutputFolderPath = folder;
                }
            });
        }

        private void LoadVideo(string filePath)
        {
            StatusMessage = "Loading video...";
            bool success = _videoPlayerService.LoadVideo(filePath);

            if (success)
            {
                TotalFrames = _videoPlayerService.TotalFrames;
                CurrentFrameIndex = 0;
                StatusMessage = $"Loaded: {_videoPlayerService.Width}x{_videoPlayerService.Height}, " +
                              $"{_videoPlayerService.TotalFrames} frames @ {_videoPlayerService.Fps:F2} FPS";

                // Clear In/Out points when loading new video
                InPointFrame = null;
                OutPointFrame = null;

                OnPropertyChanged(nameof(IsVideoLoaded));
                OnPropertyChanged(nameof(DurationDisplay));
                UpdateCommandStates();
                SaveSettings();
            }
            else
            {
                TotalFrames = 0;
                CurrentFrameIndex = 0;
                CurrentFrame = null;
                OnPropertyChanged(nameof(IsVideoLoaded));
                UpdateCommandStates();
            }
        }

        private void PlayPause()
        {
            if (IsPlaying)
            {
                _videoPlayerService.Pause();
                IsPlaying = false;
            }
            else
            {
                _videoPlayerService.Play();
                IsPlaying = true;
            }
        }

        private void Stop()
        {
            _videoPlayerService.Stop();
            IsPlaying = false;
        }

        private void SetInPoint()
        {
            InPointFrame = CurrentFrameIndex;
            StatusMessage = $"In point set to frame {CurrentFrameIndex} ({FormatFrameTime(CurrentFrameIndex)})";
        }

        private void SetOutPoint()
        {
            OutPointFrame = CurrentFrameIndex;
            StatusMessage = $"Out point set to frame {CurrentFrameIndex} ({FormatFrameTime(CurrentFrameIndex)})";
        }

        private async Task ExtractFrames()
        {
            if (!ValidateExtraction())
                return;

            if (!CheckLargeExtraction())
                return;

            IsExtracting = true;
            var frameCount = OutPointFrame!.Value - InPointFrame!.Value + 1;
            StatusMessage = $"Extracting {frameCount} frames...";
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    ExtractionProgress = percent;
                    var current = (int)((percent / 100.0) * frameCount);
                    StatusMessage = $"Extracting frame {current} of {frameCount}...";
                });

                await _videoPlayerService.ExtractFrameRange(
                    InPointFrame.Value,
                    OutPointFrame.Value,
                    OutputFolderPath,
                    progress,
                    _cancellationTokenSource.Token,
                    ClassNamePrefix
                );

                StatusMessage = $"Successfully extracted {frameCount} frames to {OutputFolderPath}";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Extraction cancelled by user.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Extraction failed: {ex.Message}";
            }
            finally
            {
                IsExtracting = false;
                ExtractionProgress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private bool ValidateExtraction()
        {
            if (string.IsNullOrEmpty(OutputFolderPath))
            {
                StatusMessage = "Error: Please select an output folder for extracted frames.";
                return false;
            }

            if (!InPointFrame.HasValue || !OutPointFrame.HasValue)
            {
                StatusMessage = "Error: Please set both In (I key) and Out (O key) points.";
                return false;
            }

            if (InPointFrame.Value >= OutPointFrame.Value)
            {
                StatusMessage = "Error: In point must be before Out point.";
                return false;
            }

            return true;
        }

        private bool CheckLargeExtraction()
        {
            var frameCount = OutPointFrame!.Value - InPointFrame!.Value + 1;

            // Warn if extracting more than 10,000 frames
            if (frameCount > 10000)
            {
                StatusMessage = $"Warning: You are about to extract {frameCount:N0} frames. " +
                               "This may take several minutes and use significant disk space.";
                // In a real app, you'd show a confirmation dialog here
                // For now, we'll just allow it
            }

            return true;
        }

        private void OnFrameReady(object? sender, Bitmap frame)
        {
            CurrentFrame?.Dispose();
            CurrentFrame = frame;
        }

        private void OnPositionChanged(object? sender, int frameIndex)
        {
            _currentFrameIndex = frameIndex;
            OnPropertyChanged(nameof(CurrentFrameIndex));
            OnPropertyChanged(nameof(CurrentTimeDisplay));
        }

        private void OnError(object? sender, string message)
        {
            StatusMessage = $"Error: {message}";
            IsPlaying = false;
        }

        private string FormatFrameTime(int frameIndex)
        {
            if (!IsVideoLoaded || _videoPlayerService.Fps == 0)
                return "00:00.000";

            var totalSeconds = frameIndex / _videoPlayerService.Fps;
            var timeSpan = TimeSpan.FromSeconds(totalSeconds);

            // Format: MM:SS.ms (e.g., "01:23.456")
            return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
        }

        private void UpdateCommandStates()
        {
            OnPropertyChanged(nameof(CanSetInOut));
            OnPropertyChanged(nameof(CanExtract));
            (PlayPauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StepForwardCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StepBackwardCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SetInPointCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SetOutPointCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExtractCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SaveSettings()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Settings.ExtractLastVideoPath = _videoFilePath;
                _mainViewModel.Settings.ExtractOutputFolder = _outputFolderPath;
                _mainViewModel.Settings.ExtractClassNamePrefix = _classNamePrefix;
                _mainViewModel.Settings.ExtractLastInFrame = InPointFrame ?? 0;
                _mainViewModel.Settings.ExtractLastOutFrame = OutPointFrame ?? 0;
                _mainViewModel.SaveSettings();
            }
        }
    }
}
