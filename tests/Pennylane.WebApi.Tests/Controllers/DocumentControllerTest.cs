using System.Net;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain;
using Pennylane.WebApi.Controllers;
using Pennylane.WebApi.Models;

namespace Pennylane.WebApi.Tests.Controllers;

public class DocumentControllerTest
{
    private readonly DocumentController _controller;
    private readonly Mock<IDocumentService> _service;
    private readonly Mock<ISyncService> _syncService;
    private readonly Fixture _fixture;

    public DocumentControllerTest()
    {
        _fixture = new Fixture();
        _service = new Mock<IDocumentService>(MockBehavior.Strict);
        _syncService = new Mock<ISyncService>(MockBehavior.Strict);
        _controller = new DocumentController(_service.Object, _syncService.Object);
    }

    [Fact]
    public async Task GetDocumentsAsync_ShouldReturnEmptyPage_WhenThereAreNoDocuments()
    {
        var synchroDate = _fixture.Create<DateTime>();
        var accountNumber = _fixture.Create<string>();
        var page = 1;
        var pageSize = 1;

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, null, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, 0, [], synchroDate))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await _controller.GetDocumentsAsync(accountNumber, null, page, pageSize);

        result.Should().BeEquivalentTo(new OkObjectResult(DocumentsPagination.Create(new AccountDocumentsPage(page, pageSize, 0, [], synchroDate))));
        _service.VerifyAll();
        _syncService.VerifyAll();
    }

    [Theory]
    [InlineData(2, 100, 300)]
    [InlineData(1, 100, 300)]
    [InlineData(10, 1000, 300)]
    public async Task GetDocumentsAsync_ShouldReturnCorrectPage_WhenThereAreDocuments(int page, int pageSize, int documentsCount)
    {
        var synchroDate = _fixture.Create<DateTime>();
        var accountNumber = _fixture.Create<string>();
        var documents = _fixture.CreateMany<Document>(documentsCount).ToList();
        var pageDocuments = documents.OrderByDescending(d => d.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, null, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, pageSize, pageDocuments, synchroDate))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await _controller.GetDocumentsAsync(accountNumber, null, page, pageSize);

        result.Should().BeEquivalentTo(new OkObjectResult(DocumentsPagination.Create(new AccountDocumentsPage(page, pageSize, pageSize, pageDocuments, synchroDate))));
        _service.VerifyAll();
        _syncService.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentsAsync_ShouldReturnNoSynchroDate_WhenThereIsNoSynchroDataInDatabase()
    {
        var page = 3;
        var pageSize = 10;
        var documentsCount = 100;
        var accountNumber = _fixture.Create<string>();
        var documents = _fixture.CreateMany<Document>(documentsCount).ToList();
        var pageDocuments = documents.OrderByDescending(d => d.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, null, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, pageSize, pageDocuments, null))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await _controller.GetDocumentsAsync(accountNumber, null, page, pageSize);

        result.Should().BeEquivalentTo(new OkObjectResult(DocumentsPagination.Create(new AccountDocumentsPage(page, pageSize, pageSize, pageDocuments, null))));
        _service.VerifyAll();
        _syncService.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentsAsync_ShouldCallCheckAndPushSyncMessageAsync()
    {
        var accountNumber = _fixture.Create<string>();
        var page = 1;
        var pageSize = 10;
        var synchroDate = _fixture.Create<DateTime>();
        var documents = _fixture.CreateMany<Document>(pageSize).ToList();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, null, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, pageSize, documents, synchroDate))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await _controller.GetDocumentsAsync(accountNumber, null, page, pageSize);

        result.Should().BeOfType<OkObjectResult>();
        _syncService.Verify(s => s.CheckAndPushSyncMessageAsync(accountNumber, false), Times.Once);
        _service.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentsAsync_WithSearchParameter_ShouldPassSearchToService()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var search = "facture";
        var page = 1;
        var pageSize = 10;
        var synchroDate = _fixture.Create<DateTime>();
        var documents = _fixture.CreateMany<Document>(3).ToList();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, search, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, 3, documents, synchroDate))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.GetDocumentsAsync(accountNumber, search, page, pageSize);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _service.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentsAsync_WithNullSearch_ShouldPassNullToService()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        string? search = null;
        var page = 1;
        var pageSize = 10;
        var synchroDate = _fixture.Create<DateTime>();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, page, pageSize, null, true))
            .ReturnsAsync(new AccountDocumentsPage(page, pageSize, 0, [], synchroDate))
            .Verifiable();

        _syncService
            .Setup(s => s.CheckAndPushSyncMessageAsync(accountNumber, false))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.GetDocumentsAsync(accountNumber, search, page, pageSize);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _service.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentContentAsync_ReturnsFileStreamResult_WhenDocumentExists()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = 1;
        var fileStream = new MemoryStream(new byte[] { 0x20, 0x20, 0x20 }); // Exemple de contenu

        _service.Setup(s => s.GetDocumentContentStreamAsync(accountNumber, documentId))
            .ReturnsAsync(fileStream);

        // Act
        var result = await _controller.GetDocumentContentAsync(accountNumber, documentId);

        // Assert
        var fileStreamResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileStreamResult.ContentType);
    }

    [Fact]
    public async Task GetDocumentContentAsync_ReturnsNotFound_WhenDocumentNotFound()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = 1;

        _service.Setup(s => s.GetDocumentContentStreamAsync(accountNumber, documentId))
            .ReturnsAsync((Stream?)null); // Simulates document not found

        // Act
        var result = await _controller.GetDocumentContentAsync(accountNumber, documentId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetDocumentContentAsync_ReturnsBadRequest_WhenExceptionOccurs()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = 1;
        var exceptionMessage = "An error occurred";

        _service.Setup(s => s.GetDocumentContentStreamAsync(accountNumber, documentId))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _controller.GetDocumentContentAsync(accountNumber, documentId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    [Fact]
    public async Task PurgeDocumentsAsync_ShouldReturnZero_WhenThereAreNoDocuments()
    {
        var accountNumber = _fixture.Create<string>();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, 1, 100, null, false))
            .ReturnsAsync(new AccountDocumentsPage(1, 100, 0, Array.Empty<Document>(), null))
            .Verifiable();

        var result = await _controller.PurgeDocumentsAsync(accountNumber);

        result.Should().BeEquivalentTo(new OkObjectResult(new { deleted = 0 }));
        _service.VerifyAll();
    }

    [Fact]
    public async Task PurgeDocumentsAsync_ShouldDeleteAllDocuments_WhenDocumentsExist()
    {
        var accountNumber = _fixture.Create<string>();
        var documents = _fixture.CreateMany<Document>(2).ToList();

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, 1, 100, null, false))
            .ReturnsAsync(new AccountDocumentsPage(1, 100, 2, documents, null))
            .Verifiable();

        _service
            .Setup(s => s.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()))
            .ReturnsAsync(2)
            .Verifiable();

        var result = await _controller.PurgeDocumentsAsync(accountNumber);

        result.Should().BeEquivalentTo(new OkObjectResult(new { deleted = 2 }));
        _service.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentContentAsync_Returns404_WhenRequestHttpExceptionWithNotFound()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = 1;

        _service.Setup(s => s.GetDocumentContentStreamAsync(accountNumber, documentId))
            .ThrowsAsync(new RequestHttpException(HttpStatusCode.NotFound, "not found"));

        // Act
        var result = await _controller.GetDocumentContentAsync(accountNumber, documentId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PurgeDocumentsAsync_Returns500_WhenExceptionOccurs()
    {
        var accountNumber = _fixture.Create<string>();
        var exceptionMessage = "Something went wrong";

        _service
            .Setup(s => s.GetDocumentsAsync(accountNumber, 1, 100, null, false))
            .ThrowsAsync(new Exception(exceptionMessage))
            .Verifiable();

        var result = await _controller.PurgeDocumentsAsync(accountNumber);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        Assert.Equal(exceptionMessage, obj.Value);
        _service.VerifyAll();
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_ReturnsBadRequest_WhenRequestIsNull()
    {
        var accountNumber = _fixture.Create<string>();

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_ReturnsBadRequest_WhenIdsAreEmpty()
    {
        var accountNumber = _fixture.Create<string>();
        var request = new DownloadDocumentsRequest { Ids = new List<long>() };

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_ReturnsBadRequest_WhenMoreThanFifteenDocuments()
    {
        var accountNumber = _fixture.Create<string>();
        var request = new DownloadDocumentsRequest { Ids = Enumerable.Range(1, 16).Select(i => (long)i).ToList() };

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_ReturnsNotFound_WhenNoDocumentMatchesTheRequestedIds()
    {
        var accountNumber = _fixture.Create<string>();
        var request = new DownloadDocumentsRequest { Ids = new List<long> { 7601833 } };

        _service
            .Setup(s => s.GetDocumentsArchiveStreamAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(Stream.Null)
            .Verifiable();

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        _service.VerifyAll();
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_DeduplicatesIds_BeforeCallingService()
    {
        var accountNumber = _fixture.Create<string>();
        var request = new DownloadDocumentsRequest { Ids = new List<long> { 1, 1, 2, 2, 3 } };
        var archiveStream = new MemoryStream(new byte[] { 0x50, 0x4B });

        _service
            .Setup(s => s.GetDocumentsArchiveStreamAsync(
                accountNumber,
                It.Is<IReadOnlyCollection<long>>(collection => collection.Count == 3 && collection.SequenceEqual(new long[] { 1, 2, 3 }))))
            .ReturnsAsync(archiveStream)
            .Verifiable();

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, request);

        var fileStreamResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/x-zip-compressed", fileStreamResult.ContentType);
        Assert.StartsWith("documents-rydge-conseil_", fileStreamResult.FileDownloadName);
        Assert.EndsWith(".zip", fileStreamResult.FileDownloadName);
        _service.VerifyAll();
    }

    [Fact]
    public async Task DownloadDocumentsArchiveAsync_ReturnsFileStreamResult_WhenIdsAreValid()
    {
        var accountNumber = _fixture.Create<string>();
        var request = new DownloadDocumentsRequest { Ids = new List<long> { 7601833, 7601834 } };
        var archiveStream = new MemoryStream(new byte[] { 0x50, 0x4B });

        _service
            .Setup(s => s.GetDocumentsArchiveStreamAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(archiveStream)
            .Verifiable();

        var result = await _controller.DownloadDocumentsArchiveAsync(accountNumber, request);

        var fileStreamResult = Assert.IsType<FileStreamResult>(result);
        Assert.Same(archiveStream, fileStreamResult.FileStream);
        _service.VerifyAll();
    }
}
