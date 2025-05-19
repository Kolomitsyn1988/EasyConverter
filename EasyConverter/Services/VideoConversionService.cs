using Serilog.Context;
using FFMpegCore;
using EasyConverter.Interfaces;

namespace EasyConverter.Services
{
    public class VideoConversionService : IVideoConversionService
    {
        private readonly ILogger<VideoConversionService> _logger;
        private bool _disposed;

        private static readonly string[] SupportedFormats = { "mp4", "webm", "avi", "mov", "mkv" };
        private static readonly string[] SupportedVideoCodecs = { "libx264", "libvpx-vp9", "mpeg4" };
        private static readonly string[] SupportedAudioCodecs = { "aac", "libvorbis", "mp3" };

        public VideoConversionService(ILogger<VideoConversionService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            GlobalFFOptions.Configure(options => options.BinaryFolder = "./bin");
        }

        public bool IsFormatSupported(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            format = format.ToLowerInvariant().TrimStart('.');
            return SupportedFormats.Contains(format);
        }

        public async Task<string> ConvertVideoAsync(string inputPath, string outputFormat, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input file not found: {inputPath}");
            }

            if (!IsFormatSupported(outputFormat))
            {
                throw new ArgumentException($"Output format '{outputFormat}' is not supported. Supported formats: {string.Join(", ", SupportedFormats)}");
            }

            var outputPath = Path.ChangeExtension(inputPath, outputFormat);
            _logger.LogInformation("Starting video conversion to {OutputPath}", outputPath);

            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                if (mediaInfo.VideoStreams.FirstOrDefault() == null) 
                    throw new InvalidOperationException("No video stream found in the input file");

                // Настраиваем параметры конвертации
                var (videoCodec, audioCodec) = GetCodecsForFormat(outputFormat);

                var result = await FFMpegArguments.FromFileInput(inputPath)
                    .OutputToFile(outputPath, true, x => 
                        x.WithVideoCodec(videoCodec)
                        .WithAudioCodec(audioCodec)
                        .WithVideoBitrate(2000000)
                        .WithAudioBitrate(128000))
                    .ProcessAsynchronously(true, new FFOptions { BinaryFolder = "./bin", TemporaryFilesFolder = "/tmp" });

                if (result)
                    _logger.LogInformation("Video conversion completed successfully");
                else
                    _logger.LogInformation("Video conversion was not completed successfully");
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during video conversion");
                throw;
            }
        }

        public async Task<TimeSpan> GetVideoDurationAsync(string inputPath)
        {
            ThrowIfDisposed();
            using var _ = LogContext.PushProperty("InputPath", inputPath);

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input file not found: {inputPath}");
            }

            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                var duration = mediaInfo.Duration;
                
                _logger.LogInformation("Retrieved video duration: {Duration}", duration);
                return duration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting video duration");
                throw;
            }
        }

        private (string videoCodec, string audioCodec) GetCodecsForFormat(string format)
        {
            return format.ToLower() switch
            {
                "mp4" => ("libx264", "aac"),
                "webm" => ("libvpx-vp9", "libvorbis"),
                "avi" => ("mpeg4", "mp3"),
                "mov" => ("libx264", "aac"),
                "mkv" => ("libx264", "aac"),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Освобождаем ресурсы
                }
                _disposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(VideoConversionService));
            }
        }
    }
} 