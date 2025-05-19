using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using EasyConverter.Models;
using EasyConverter.Interfaces;

namespace EasyConverter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<VideoController> _logger;
        private readonly IVideoConversionService _conversionService;
        private const int MaxFileSize = 1024 * 1024 * 500; // 500MB

        public VideoController(
            IWebHostEnvironment environment, 
            ILogger<VideoController> logger,
            IVideoConversionService conversionService)
        {
            _environment = environment;
            _logger = logger;
            _conversionService = conversionService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadVideo(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) {
                _logger.LogInformation("Creating uploads directory at {UploadsFolder}", uploadsFolder);
                Directory.CreateDirectory(uploadsFolder);
            }

            var checkResult = CheckVideoFile(file);
            if (checkResult != null)
                return BadRequest(checkResult);

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try {
                _logger.LogInformation("Saving file to {FilePath}", filePath);
                
                using (var stream = new FileStream(filePath, FileMode.Create)) {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("File uploaded successfully. Original: {OriginalName}, Saved as: {UniqueFileName}, Size: {FileSize}",
                    file.FileName, uniqueFileName, file.Length);

                return Ok(new {
                    fileName = uniqueFileName,
                    originalName = file.FileName,
                    fileSize = file.Length,
                    fileType = file.ContentType
                });
                
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error uploading file {FileName} to {FilePath}", file.FileName, filePath);
                return StatusCode(500, "An error occurred while uploading the file.");
            }
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertVideo(object body)
        {
            var data = JsonConvert.DeserializeObject<VideoData>(body.ToString());
            string outputFormat = data.OutputFormat;
            string fileName = data.FileName;
            
            if (string.IsNullOrEmpty(fileName)) {
                _logger.LogWarning("Convert request with empty fileName");
                return BadRequest("File name is required");
            }

            if (string.IsNullOrEmpty(outputFormat)) {
                _logger.LogWarning("Convert request with empty outputFormat");
                return BadRequest("Output format is required");
            }

            var allowedOutputFormats = new[] { "mp4", "webm", "avi", "mov", "mkv" };
            outputFormat = outputFormat.ToLowerInvariant().TrimStart('.');

            if (!allowedOutputFormats.Contains(outputFormat)) {
                _logger.LogWarning("Invalid output format attempted: {OutputFormat}", outputFormat);
                return BadRequest($"Invalid output format. Allowed formats: {string.Join(", ", allowedOutputFormats)}");
            }

            var inputPath = Path.Combine(_environment.WebRootPath, "uploads", fileName);
            if (!System.IO.File.Exists(inputPath)) {
                _logger.LogWarning("Convert request for non-existent file: {InputPath}", inputPath);
                return NotFound("File not found");
            }

            try {
                _logger.LogInformation("Starting video conversion");

                var duration = await _conversionService.GetVideoDurationAsync(inputPath);
                _logger.LogInformation("Video duration: {Duration}", duration);

                var progress = new Progress<double>(p =>
                    _logger.LogInformation("Conversion progress: {Progress:F2}%", p));

                var outputPath = await _conversionService.ConvertVideoAsync(
                    inputPath,
                    outputFormat,
                    progress);

                var outputFileName = Path.GetFileName(outputPath);
                _logger.LogInformation("Video conversion completed. Output file: {OutputFileName}", outputFileName);

                return Ok(new {
                    originalFile = outputPath,
                    convertedFile = outputFileName,
                    duration = duration.TotalSeconds
                });
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error during video conversion");
                return StatusCode(500, "An error occurred during video conversion");
            }
        }


        private string? CheckVideoFile(IFormFile file) {
            if (file == null || file.Length == 0) {
                _logger.LogWarning("Upload attempt with no file");
                return "No file was uploaded.";
            }

            if (file.Length > MaxFileSize) {
                _logger.LogWarning("File size {FileSize} exceeds maximum allowed size of {MaxFileSize}", file.Length, MaxFileSize);
                return $"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)}MB";
            }

            _logger.LogInformation("Starting file upload. File: {FileName}, Size: {FileSize}, ContentType: {ContentType}",
                file.FileName, file.Length, file.ContentType);

            var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension)) {
                _logger.LogWarning("Invalid file type attempted: {FileExtension}", fileExtension);
                return $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}";
            }
            return null;
        }



    }
} 