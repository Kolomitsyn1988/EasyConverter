using EasyConverter.Services;
using FFmpeg.NET;
using FFmpeg.NET.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace EasyConverter.Tests.Services
{
    public class VideoConversionServiceTests
    {
        private readonly Mock<ILogger<VideoConversionService>> _loggerMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<Engine> _ffmpegEngineMock;
        private readonly string _testFilePath;
        private readonly string _testOutputPath;

        public VideoConversionServiceTests()
        {
            _loggerMock = new Mock<ILogger<VideoConversionService>>();
            _environmentMock = new Mock<IWebHostEnvironment>();
            _ffmpegEngineMock = new Mock<Engine>(MockBehavior.Loose, "ffmpeg.exe");

            // Настраиваем тестовые пути
            _testFilePath = Path.Combine(Path.GetTempPath(), "test-input.mp4");
            _testOutputPath = Path.ChangeExtension(_testFilePath, "webm");

            // Создаем тестовый файл
            File.WriteAllText(_testFilePath, "test content");

            // Настраиваем мок окружения
            _environmentMock.Setup(x => x.WebRootPath)
                .Returns(Path.GetTempPath());
        }

        [Fact]
        public async Task ConvertVideoAsync_ValidInput_ShouldConvertSuccessfully()
        {
            // Arrange
            var service = new VideoConversionService(_loggerMock.Object, _environmentMock.Object);
            var progress = new Progress<double>();
            var cancellationToken = CancellationToken.None;

            // Настраиваем мок FFmpeg для успешной конвертации
            _ffmpegEngineMock.Setup(x => x.ConvertAsync(
                It.IsAny<InputFile>(),
                It.IsAny<OutputFile>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgressEventArgs>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await service.ConvertVideoAsync(_testFilePath, "webm", progress, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(_testOutputPath);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting video conversion")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ConvertVideoAsync_FileNotFound_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var service = new VideoConversionService(_loggerMock.Object, _environmentMock.Object);
            var nonExistentFile = "non-existent-file.mp4";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.ConvertVideoAsync(nonExistentFile, "webm"));
        }

        [Fact]
        public async Task GetVideoDurationAsync_ValidInput_ShouldReturnDuration()
        {
            // Arrange
            var service = new VideoConversionService(_loggerMock.Object, _environmentMock.Object);
            var expectedDuration = TimeSpan.FromMinutes(2);

            // Настраиваем мок FFmpeg для возврата метаданных
            var metaData = new MetaData { Duration = expectedDuration };
            _ffmpegEngineMock.Setup(x => x.GetMetaDataAsync(It.IsAny<InputFile>()))
                .ReturnsAsync(metaData);

            // Act
            var result = await service.GetVideoDurationAsync(_testFilePath);

            // Assert
            result.Should().Be(expectedDuration);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved video duration")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetVideoDurationAsync_FileNotFound_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var service = new VideoConversionService(_loggerMock.Object, _environmentMock.Object);
            var nonExistentFile = "non-existent-file.mp4";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.GetVideoDurationAsync(nonExistentFile));
        }

        public void Dispose()
        {
            // Очистка тестовых файлов
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            if (File.Exists(_testOutputPath))
            {
                File.Delete(_testOutputPath);
            }
        }
    }
} 