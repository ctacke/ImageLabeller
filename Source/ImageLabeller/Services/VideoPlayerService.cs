using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OpenCvSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageLabeller.Services
{
    public class VideoPlayerService : IDisposable
    {
        private VideoCapture? _videoCapture;
        private DispatcherTimer? _playbackTimer;
        private bool _disposed = false;

        // Video metadata
        public double TotalFrames { get; private set; }
        public double Fps { get; private set; }
        public TimeSpan Duration { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        // Current state
        public int CurrentFrameIndex { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsVideoLoaded => _videoCapture != null && _videoCapture.IsOpened();

        // Events
        public event EventHandler<Bitmap>? FrameReady;
        public event EventHandler<int>? PositionChanged;
        public event EventHandler<string>? ErrorOccurred;

        public bool LoadVideo(string filePath)
        {
            try
            {
                // Clean up existing video if any
                CleanupVideo();

                if (!File.Exists(filePath))
                {
                    OnError("Video file does not exist.");
                    return false;
                }

                _videoCapture = new VideoCapture(filePath);

                if (!_videoCapture.IsOpened())
                {
                    OnError("Unable to open video file. Format may not be supported.");
                    CleanupVideo();
                    return false;
                }

                // Extract video metadata
                TotalFrames = _videoCapture.FrameCount;
                Fps = _videoCapture.Fps;
                Width = (int)_videoCapture.FrameWidth;
                Height = (int)_videoCapture.FrameHeight;

                if (TotalFrames <= 0 || Fps <= 0)
                {
                    OnError("Invalid video file: Unable to determine frame count or FPS.");
                    CleanupVideo();
                    return false;
                }

                Duration = TimeSpan.FromSeconds(TotalFrames / Fps);
                CurrentFrameIndex = 0;

                // Load and display the first frame
                SeekToFrame(0);

                return true;
            }
            catch (Exception ex)
            {
                OnError($"Error loading video: {ex.Message}");
                CleanupVideo();
                return false;
            }
        }

        public void Play()
        {
            if (!IsVideoLoaded || IsPlaying)
                return;

            IsPlaying = true;

            // Create timer with interval based on video FPS
            var intervalMs = (int)(1000.0 / Fps);
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();
        }

        public void Pause()
        {
            if (!IsPlaying)
                return;

            IsPlaying = false;
            _playbackTimer?.Stop();
        }

        public void Stop()
        {
            Pause();
            SeekToFrame(0);
        }

        public void SeekToFrame(int frameIndex)
        {
            if (!IsVideoLoaded)
                return;

            try
            {
                // Clamp frame index to valid range
                frameIndex = Math.Max(0, Math.Min(frameIndex, (int)TotalFrames - 1));

                _videoCapture!.Set(VideoCaptureProperties.PosFrames, frameIndex);
                CurrentFrameIndex = frameIndex;

                // Read and display the frame
                using var mat = new Mat();
                _videoCapture.Read(mat);

                if (!mat.Empty())
                {
                    var bitmap = ConvertMatToBitmap(mat);
                    FrameReady?.Invoke(this, bitmap);
                    PositionChanged?.Invoke(this, CurrentFrameIndex);
                }
            }
            catch (Exception ex)
            {
                OnError($"Error seeking to frame: {ex.Message}");
            }
        }

        public void StepForward()
        {
            if (!IsVideoLoaded)
                return;

            SeekToFrame(CurrentFrameIndex + 1);
        }

        public void StepBackward()
        {
            if (!IsVideoLoaded)
                return;

            SeekToFrame(CurrentFrameIndex - 1);
        }

        public async Task ExtractFrameRange(int startFrame, int endFrame, string outputFolder,
            IProgress<int>? progress, CancellationToken cancellationToken, string? classNamePrefix = null)
        {
            if (!IsVideoLoaded)
                throw new InvalidOperationException("No video loaded");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            bool wasPlaying = IsPlaying;
            if (wasPlaying)
            {
                Pause();
            }

            try
            {
                await Task.Run(() =>
                {
                    var frameCount = endFrame - startFrame + 1;
                    int processedFrames = 0;

                    // Determine filename prefix
                    string prefix = string.IsNullOrWhiteSpace(classNamePrefix) ? "frame" : classNamePrefix;

                    for (int i = startFrame; i <= endFrame; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        // Seek to frame
                        _videoCapture!.Set(VideoCaptureProperties.PosFrames, i);

                        using var mat = new Mat();
                        _videoCapture.Read(mat);

                        if (!mat.Empty())
                        {
                            // Generate filename: {prefix}_{frameNumber}.jpg
                            // e.g., "mph55_00001.jpg" or "frame_00001.jpg"
                            string filename = $"{prefix}_{i:D5}.jpg";
                            string outputPath = Path.Combine(outputFolder, filename);

                            Cv2.ImWrite(outputPath, mat);
                        }

                        processedFrames++;
                        var progressPercentage = (int)((double)processedFrames / frameCount * 100);
                        progress?.Report(progressPercentage);
                    }
                }, cancellationToken);
            }
            finally
            {
                // Restore position to current frame after extraction
                SeekToFrame(CurrentFrameIndex);

                if (wasPlaying)
                {
                    Play();
                }
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsPlaying || _videoCapture == null)
                return;

            try
            {
                using var mat = new Mat();
                _videoCapture.Read(mat);

                if (mat.Empty())
                {
                    // End of video - loop back to start or stop
                    Stop();
                    return;
                }

                CurrentFrameIndex = (int)_videoCapture.Get(VideoCaptureProperties.PosFrames);

                var bitmap = ConvertMatToBitmap(mat);
                FrameReady?.Invoke(this, bitmap);
                PositionChanged?.Invoke(this, CurrentFrameIndex);
            }
            catch (Exception ex)
            {
                OnError($"Error during playback: {ex.Message}");
                Pause();
            }
        }

        private Bitmap ConvertMatToBitmap(Mat mat)
        {
            try
            {
                // Encode Mat to JPEG bytes
                Cv2.ImEncode(".jpg", mat, out byte[] imageData);

                // Create Avalonia Bitmap from bytes
                using var stream = new MemoryStream(imageData);
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                OnError($"Error converting frame to bitmap: {ex.Message}");
                // Return a dummy bitmap to avoid crashes
                throw;
            }
        }

        private void CleanupVideo()
        {
            Pause();
            _videoCapture?.Dispose();
            _videoCapture = null;
            TotalFrames = 0;
            Fps = 0;
            Duration = TimeSpan.Zero;
            CurrentFrameIndex = 0;
        }

        private void OnError(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            CleanupVideo();
            _disposed = true;
        }
    }
}
