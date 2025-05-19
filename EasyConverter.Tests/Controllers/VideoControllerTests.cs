using EasyConverter.Controllers;
using EasyConverter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using System.Text;

namespace EasyConverter.Tests.Controllers
{
    public class VideoControllerTests
    {
        private readonly Mock<ILogger<VideoController>> _loggerMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<IVideoConversionService> _conversionServiceMock;
        private readonly VideoController _controller;
        private readonly string _testUploadsPath;

        public VideoControllerTests()
        {
            _loggerMock = new Mock<ILogger<VideoController>>();
            _environmentMock = new Mock<IWebHostEnvironment>();
            _conversionServiceMock = new Mock<IVideoConversionService>();

            _testUploadsPath = Path.Combine(Path.GetTempPath(), "uploads");
            Directory.CreateDirectory(_testUploadsPath);

            _environmentMock.Setup(x => x.WebRootPath)
                .Returns(Path.GetTempPath());

            _controller = new VideoController(
                _environmentMock.Object,
                _loggerMock.Object,
                _conversionServiceMock.Object);
        }

        [Fact]
        public async Task UploadVideo_ValidFile_ShouldReturnSuccess()
        {
            // Arrange
            var fileName = "test.mp4";
            var fileContent = "test video content";
            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "video/mp4"
            };

            // Act
            var result = await _controller.UploadVideo(formFile);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<dynamic>(okResult.Value);
            Assert.NotNull(returnValue.fileName);
            Assert.Equal(fileName, returnValue.originalName);
            Assert.Equal(fileStream.Length, returnValue.fileSize);
            Assert.Equal("video/mp4", returnValue.fileType);

            // Verify file was saved
            var savedFiles = Directory.GetFiles(_testUploadsPath);
            Assert.Single(savedFiles);
        }

        [Fact]
        public async Task UploadVideo_NoFile_ShouldReturnBadRequest()
        {
            // Arrange
            IFormFile? formFile = null;

            // Act
            var result = await _controller.UploadVideo(formFile);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Upload attempt with no file")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task UploadVideo_InvalidFileType_ShouldReturnBadRequest()
        {
            // Arrange
            var fileName = "test.txt";
            var fileContent = "test content";
            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act
            var result = await _controller.UploadVideo(formFile);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid file type")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ConvertVideo_ValidInput_ShouldReturnSuccess()
        {
            // Arrange
            var fileName = "test.mp4";
            var outputFormat = "webm";
            var duration = TimeSpan.FromMinutes(2);
            var outputPath = Path.Combine(_testUploadsPath, "test.webm");

            // Создаем тестовый файл
            File.WriteAllText(Path.Combine(_testUploadsPath, fileName), "test content");

            _conversionServiceMock.Setup(x => x.GetVideoDurationAsync(It.IsAny<string>()))
                .ReturnsAsync(duration);

            _conversionServiceMock.Setup(x => x.ConvertVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(outputPath);

            // Act
            var result = await _controller.ConvertVideo(fileName, outputFormat);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<dynamic>(okResult.Value);
            Assert.Equal(fileName, returnValue.originalFile);
            Assert.Equal(Path.GetFileName(outputPath), returnValue.convertedFile);
            Assert.Equal(duration.TotalSeconds, returnValue.duration);
        }

        [Fact]
        public async Task ConvertVideo_FileNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var fileName = "non-existent.mp4";
            var outputFormat = "webm";

            // Act
            var result = await _controller.ConvertVideo(fileName, outputFormat);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Convert request for non-existent file")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ConvertVideo_EmptyFileName_ShouldReturnBadRequest()
        {
            // Arrange
            string fileName = "";
            var outputFormat = "webm";

            // Act
            var result = await _controller.ConvertVideo(fileName, outputFormat);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Convert request with empty fileName")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        public void Dispose()
        {
            // Очистка тестовых файлов
            if (Directory.Exists(_testUploadsPath))
            {
                Directory.Delete(_testUploadsPath, true);
            }
        }
    }
} 