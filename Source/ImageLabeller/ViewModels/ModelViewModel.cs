using Avalonia.Media.Imaging;
using ImageLabeller.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace ImageLabeller.ViewModels
{
    public class ModelViewModel : ViewModelBase
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };
        private const int ModelInputSize = 640;  // Default YOLO input size
        private const float ConfidenceThreshold = 0.5f;  // 50% confidence
        private const float IouThreshold = 0.45f;

        private string _modelPath = string.Empty;
        private string _testImagePath = string.Empty;
        private Avalonia.Media.Imaging.Bitmap? _currentImage;
        private int _currentImageIndex = -1;
        private List<string> _testImageFiles = new();
        private string _imageCountText = "0 of 0";
        private ObservableCollection<Detection> _detections = new();
        private readonly MainWindowViewModel? _mainViewModel;
        private InferenceSession? _session;

        public string ModelPath
        {
            get => _modelPath;
            set
            {
                if (SetProperty(ref _modelPath, value))
                {
                    SaveSettings();
                    RunInference();
                }
            }
        }

        public string TestImagePath
        {
            get => _testImagePath;
            set
            {
                if (SetProperty(ref _testImagePath, value))
                {
                    LoadTestImage(value);
                    SaveSettings();
                }
            }
        }

        public Avalonia.Media.Imaging.Bitmap? CurrentImage
        {
            get => _currentImage;
            private set => SetProperty(ref _currentImage, value);
        }

        public string ImageCountText
        {
            get => _imageCountText;
            private set => SetProperty(ref _imageCountText, value);
        }

        public ObservableCollection<Detection> Detections
        {
            get => _detections;
            private set => SetProperty(ref _detections, value);
        }

        public bool HasPreviousImage => _currentImageIndex > 0;
        public bool HasNextImage => _currentImageIndex >= 0 && _currentImageIndex < _testImageFiles.Count - 1;

        public ICommand BrowseModelCommand { get; }
        public ICommand BrowseTestImageCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand PreviousImageCommand { get; }

        public ModelViewModel(MainWindowViewModel? mainViewModel = null)
        {
            _mainViewModel = mainViewModel;

            BrowseModelCommand = new RelayCommand(() => { });
            BrowseTestImageCommand = new RelayCommand(() => { });
            NextImageCommand = new RelayCommand(NavigateNext, () => HasNextImage);
            PreviousImageCommand = new RelayCommand(NavigatePrevious, () => HasPreviousImage);

            // Load saved settings
            if (_mainViewModel != null)
            {
                _modelPath = _mainViewModel.Settings.ModelPath;
                _testImagePath = _mainViewModel.Settings.ModelTestImagePath;

                // Load test image if path exists
                if (!string.IsNullOrEmpty(_testImagePath) && File.Exists(_testImagePath))
                {
                    LoadTestImage(_testImagePath);
                }
            }
        }

        public void SetFilePickerCallbacks(Func<string?> browseModel, Func<string?> browseTestImage)
        {
            (BrowseModelCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var file = browseModel();
                if (!string.IsNullOrEmpty(file))
                {
                    ModelPath = file;
                }
            });

            (BrowseTestImageCommand as RelayCommand)!.UpdateExecute(() =>
            {
                var file = browseTestImage();
                if (!string.IsNullOrEmpty(file))
                {
                    TestImagePath = file;
                }
            });
        }

        private void LoadTestImage(string imagePath)
        {
            if (!File.Exists(imagePath))
                return;

            // Get all images in the same folder
            var folder = Path.GetDirectoryName(imagePath);
            if (string.IsNullOrEmpty(folder))
                return;

            _testImageFiles = Directory.GetFiles(folder)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            _currentImageIndex = _testImageFiles.IndexOf(imagePath);

            LoadImage(_currentImageIndex);
        }

        private void LoadImage(int index)
        {
            if (index < 0 || index >= _testImageFiles.Count)
            {
                CurrentImage?.Dispose();
                CurrentImage = null;
                _currentImageIndex = -1;
                UpdateImageCount();
                UpdateNavigationState();
                return;
            }

            try
            {
                CurrentImage?.Dispose();
                _currentImageIndex = index;
                var imagePath = _testImageFiles[index];
                CurrentImage = new Bitmap(imagePath);

                // Run inference on the new image
                RunInference();
            }
            catch
            {
                CurrentImage = null;
            }

            UpdateImageCount();
            UpdateNavigationState();
        }

        private void RunInference()
        {
            Detections.Clear();

            if (string.IsNullOrEmpty(ModelPath) || !File.Exists(ModelPath) || _currentImageIndex < 0 || _currentImageIndex >= _testImageFiles.Count)
            {
                return;
            }

            try
            {
                // Load model if not loaded
                if (_session == null)
                {
                    _session = new InferenceSession(ModelPath);
                }

                var imagePath = _testImageFiles[_currentImageIndex];

                // Load and preprocess image using SixLabors.ImageSharp
                using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(imagePath);
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                // Resize to model input size
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(ModelInputSize, ModelInputSize),
                    Mode = ResizeMode.Stretch
                }));

                // Convert to tensor (CHW format, normalized to 0-1)
                var tensor = new DenseTensor<float>(new[] { 1, 3, ModelInputSize, ModelInputSize });
                for (int y = 0; y < ModelInputSize; y++)
                {
                    for (int x = 0; x < ModelInputSize; x++)
                    {
                        var pixel = image[x, y];
                        tensor[0, 0, y, x] = pixel.R / 255f;
                        tensor[0, 1, y, x] = pixel.G / 255f;
                        tensor[0, 2, y, x] = pixel.B / 255f;
                    }
                }

                // Run inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", tensor)
                };

                using var results = _session.Run(inputs);
                var outputTensor = results.First();

                // Get output dimensions
                var shape = outputTensor.AsTensor<float>().Dimensions.ToArray();
                Console.WriteLine($"Output shape: [{string.Join(", ", shape)}]");

                var output = outputTensor.AsEnumerable<float>().ToArray();
                Console.WriteLine($"Output array length: {output.Length}");

                // Process YOLO output
                var detections = ProcessYoloOutput(output, shape, originalWidth, originalHeight);

                // Apply NMS
                detections = ApplyNMS(detections, IouThreshold);

                foreach (var detection in detections)
                {
                    Detections.Add(detection);
                }
            }
            catch (Exception ex)
            {
                // Log error - in production you'd show this to the user
                Console.WriteLine($"Inference error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private List<Detection> ProcessYoloOutput(float[] output, int[] shape, int originalWidth, int originalHeight)
        {
            var detections = new List<Detection>();

            try
            {
                var classNames = _mainViewModel?.Settings.ClassNames ?? new List<string>();

                // Handle different YOLO output formats
                // YOLOv8 format: [batch, 84, 8400] or [batch, 8400, 84]
                // YOLOv5 format: [batch, 25200, 85] where first 5 are [x, y, w, h, obj_conf]

                int numDetections, numClasses, boxDim;
                bool transposeNeeded = false;

                if (shape.Length == 3)
                {
                    if (shape[1] < shape[2])
                    {
                        // Format: [batch, features, detections] - need transpose
                        numDetections = shape[2];
                        int features = shape[1];
                        numClasses = features - 4;  // Subtract bbox dimensions
                        boxDim = 4;
                        transposeNeeded = true;
                    }
                    else
                    {
                        // Format: [batch, detections, features]
                        numDetections = shape[1];
                        int features = shape[2];
                        numClasses = features - 4;
                        boxDim = 4;
                    }
                }
                else
                {
                    Console.WriteLine($"Unexpected output shape: [{string.Join(", ", shape)}]");
                    return detections;
                }

                Console.WriteLine($"Processing {numDetections} detections, {numClasses} classes, boxDim={boxDim}, transpose={transposeNeeded}");

                for (int i = 0; i < numDetections && i < 8400; i++)  // Limit to avoid excessive processing
                {
                    float cx, cy, w, h;

                    if (transposeNeeded)
                    {
                        // Data is in [feature, detection] format
                        int idx = i;
                        cx = output[0 * numDetections + idx];
                        cy = output[1 * numDetections + idx];
                        w = output[2 * numDetections + idx];
                        h = output[3 * numDetections + idx];
                    }
                    else
                    {
                        // Data is in [detection, feature] format
                        int offset = i * (boxDim + numClasses);
                        if (offset + boxDim + numClasses > output.Length)
                            break;

                        cx = output[offset + 0];
                        cy = output[offset + 1];
                        w = output[offset + 2];
                        h = output[offset + 3];
                    }

                    // Normalize to 0-1 range
                    cx = cx / ModelInputSize;
                    cy = cy / ModelInputSize;
                    w = w / ModelInputSize;
                    h = h / ModelInputSize;

                    // Find max class confidence
                    float maxConf = 0;
                    int maxClassId = 0;

                    for (int c = 0; c < numClasses; c++)
                    {
                        float conf;
                        if (transposeNeeded)
                        {
                            int classIdx = (boxDim + c) * numDetections + i;
                            if (classIdx >= output.Length)
                                break;
                            conf = output[classIdx];
                        }
                        else
                        {
                            int classIdx = i * (boxDim + numClasses) + boxDim + c;
                            if (classIdx >= output.Length)
                                break;
                            conf = output[classIdx];
                        }

                        if (conf > maxConf)
                        {
                            maxConf = conf;
                            maxClassId = c;
                        }
                    }

                    if (maxConf >= ConfidenceThreshold)
                    {
                        var className = maxClassId < classNames.Count ? classNames[maxClassId] : maxClassId.ToString();

                        detections.Add(new Detection(
                            maxClassId,
                            className,
                            maxConf,
                            cx, cy, w, h
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessYoloOutput: {ex.Message}");
            }

            return detections;
        }

        private List<Detection> ApplyNMS(List<Detection> detections, float iouThreshold)
        {
            var result = new List<Detection>();
            var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                result.Add(best);
                sorted.RemoveAt(0);

                sorted.RemoveAll(d => CalculateIoU(best, d) > iouThreshold);
            }

            return result;
        }

        private float CalculateIoU(Detection a, Detection b)
        {
            float x1 = Math.Max(a.X - a.Width / 2, b.X - b.Width / 2);
            float y1 = Math.Max(a.Y - a.Height / 2, b.Y - b.Height / 2);
            float x2 = Math.Min(a.X + a.Width / 2, b.X + b.Width / 2);
            float y2 = Math.Min(a.Y + a.Height / 2, b.Y + b.Height / 2);

            float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = a.Width * a.Height;
            float areaB = b.Width * b.Height;
            float union = areaA + areaB - intersection;

            return union > 0 ? intersection / union : 0;
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
            if (_testImageFiles.Count == 0)
            {
                ImageCountText = "0 of 0";
            }
            else if (_currentImageIndex >= 0 && _currentImageIndex < _testImageFiles.Count)
            {
                ImageCountText = $"{_currentImageIndex + 1} of {_testImageFiles.Count}";
            }
            else
            {
                ImageCountText = $"0 of {_testImageFiles.Count}";
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
                _mainViewModel.Settings.ModelPath = _modelPath;
                _mainViewModel.Settings.ModelTestImagePath = _testImagePath;
                _mainViewModel.SaveSettings();
            }
        }
    }
}
