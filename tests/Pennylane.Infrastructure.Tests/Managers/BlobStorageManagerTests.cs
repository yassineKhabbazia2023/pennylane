using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Infrastructure.Managers;
using Pennylane.Infrastructure.Options;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Text;
using OptionCreator = Microsoft.Extensions.Options.Options;

namespace Pennylane.Infrastructure.Tests.Managers;

public class BlobStorageManagerTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly Mock<ILogger<BlobStorageManager>> _mockLogger;
    private readonly IOptions<BlobStorageOptions> _options;
    private readonly BlobStorageManager _blobStorageManager;

    public BlobStorageManagerTests()
    {
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockBlobContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();
        _mockLogger = new Mock<ILogger<BlobStorageManager>>();
        _options = OptionCreator.Create(new BlobStorageOptions { ContainerName = "cgu" });

        _mockBlobServiceClient
            .Setup(s => s.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockBlobContainerClient.Object);

        _mockBlobContainerClient
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);

        _blobStorageManager = new BlobStorageManager(
            _mockBlobServiceClient.Object,
            _options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetFileAsync_ShouldReturnFileStreamResult_WhenBlobExists()
    {
        // Arrange
        var fileName = "test.pdf";
        var fakeStream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));
        var blobDownloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: fakeStream);

        _mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, new Mock<Response>().Object));

        _mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(blobDownloadResult, null!));

        // Act
        var result = await _blobStorageManager.GetFileAsync(fileName);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<FileStreamResult>(result);

        result.FileStream.Position = 0;
        using var reader = new StreamReader(result.FileStream);
        var resultContent = await reader.ReadToEndAsync();
        Assert.Equal("fake pdf content", resultContent);
        _mockLogger.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, _) => true), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetFileAsync_ShouldThrowNotFoundException_WhenBlobDoesNotExist()
    {
        // Arrange
        var fileName = "notfound.pdf";

        _mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, ""));

        // Act
        var result = async () => await _blobStorageManager.GetFileAsync(fileName);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(result);
        Assert.Equal("PNL010", exception.Code);
        Assert.Equal($"File named '{fileName}' doesn't exist.", exception.Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, _) => true), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task GetFileAsync_ShouldReturnNull_WhenRequestFailedExceptionIsThrown()
    {
        // Arrange
        var fileName = "test.pdf";

        _mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new RequestFailedException("Access denied"));

        // Act
        var result = await _blobStorageManager.GetFileAsync(fileName);

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, _) => true), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidFilename))]
    public async Task GetFileAsync_ShouldThrowNullArgumentException_WhenFilenameIsNullOrEmpty(string? filename)
    {
        var result = await Assert.ThrowsAsync<NullArgumentException>(async () => await _blobStorageManager.GetFileAsync(filename!));

        Assert.Equal("PNL009", result.Code);
        Assert.Equal("Parameter fileName must be informed.", result.Message);
    }

    public static TheoryData<string> InvalidFilename =>
        new TheoryData<string>
        {
            { null! },
            { string.Empty },
        };
}
