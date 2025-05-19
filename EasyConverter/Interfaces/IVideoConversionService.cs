namespace EasyConverter.Interfaces {
    public interface IVideoConversionService : IDisposable {
        Task<string> ConvertVideoAsync(string inputPath, string outputFormat, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        Task<TimeSpan> GetVideoDurationAsync(string inputPath);
        bool IsFormatSupported(string format);
    }
}
