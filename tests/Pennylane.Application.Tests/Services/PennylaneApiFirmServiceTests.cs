using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.services;
using Pennylane.Application.Exceptions;
using Xunit;
using System.Collections.Generic;
using Pulse.ExceptionMiddleware.Exceptions;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Application.Models.Pagers;

namespace Pennylane.Application.Tests.Services;

public class PennylaneApiFirmServiceTests
{
    private static PennylaneFirmTokenOptions GetOptionsWithToken(string firmId = "firm1", string token = "token1")
    {
        return new PennylaneFirmTokenOptions
        {
            new PennylaneFirmTokenItem { Id = firmId, Tokens = new List<string> { token } }
        };
    }

    [Fact]
    public async Task GetCompanyDmsFilesAsync_ReturnsAllFiles_WhenPaginationIsNeeded()
    {
        // Arrange
        var firmId = "firm1";
        var companyId = 1234; // int attendu
        var token = "token1";
        var options = Options.Create(GetOptionsWithToken(firmId, token));
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("http://localhost/");
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        // Simule deux pages
        handler.QueueResponse(new PennylaneDmsFilesResponse
        {
            Items = new List<PennylaneDmsFile> { new() { Id = 1, Name = "A", Path = "/", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now, Url = "urlA" } },
            HasMore = true,
            NextCursor = "cursor2"
        });
        handler.QueueResponse(new PennylaneDmsFilesResponse
        {
            Items = new List<PennylaneDmsFile> { new() { Id = 2, Name = "B", Path = "/", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now, Url = "urlB" } },
            HasMore = false,
            NextCursor = null
        });

        // Act
        var result = await service.GetCompanyDmsFilesAsync(firmId, companyId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
        handler.RequestCount.Should().Be(2, "la pagination doit faire deux appels HTTP");
    }

    [Fact]
    public async Task GetCompanyDmsFilesAsync_Throws_WhenNoToken()
    {
        var options = Options.Create(new PennylaneFirmTokenOptions());
        var httpClient = new HttpClient(new TestHttpMessageHandler());
        httpClient.BaseAddress = new Uri("http://localhost/");
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetCompanyDmsFilesAsync("firmX", 1234));
    }

    [Fact]
    public async Task GetCompanyDmsFilesAsync_Throws_WhenApiReturnsError()
    {
        var firmId = "firm1";
        var companyId = 1234;
        var token = "token1";
        var options = Options.Create(GetOptionsWithToken(firmId, token));
        var handler = new TestHttpMessageHandler();
        handler.QueueError(HttpStatusCode.BadRequest, "bad request");
        var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("http://localhost/");
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetCompanyDmsFilesAsync(firmId, companyId));
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_ReturnsId_WhenAccountNumberExists()
    {
        var firmId = "firm1";
        var token = "token1";
        var options = Options.Create(new PennylaneFirmTokenOptions { new PennylaneFirmTokenItem { Id = firmId, Tokens = new List<string> { token } } });
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);
        handler.QueueResponse(new PennylaneCompanyFirmPager
        {
            Items = new List<PennylaneCompanyFirm> { new() { Id = 42, ClientCode = "ACC123" } },
            TotalPages = 1
        });
        var result = await service.GetPennylaneInternalIdByAccountNumberAsync(firmId, "ACC123");
        result.Should().Be(42);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_ReturnsNull_WhenAccountNumberNotFound()
    {
        var firmId = "firm1";
        var token = "token1";
        var options = Options.Create(new PennylaneFirmTokenOptions { new PennylaneFirmTokenItem { Id = firmId, Tokens = new List<string> { token } } });
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);
        handler.QueueResponse(new PennylaneCompanyFirmPager
        {
            Items = new List<PennylaneCompanyFirm> { new() { Id = 42, ClientCode = "OTHER" } },
            TotalPages = 1
        });
        var result = await service.GetPennylaneInternalIdByAccountNumberAsync(firmId, "ACC123");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_Throws_WhenApiReturnsError()
    {
        var firmId = "firm1";
        var token = "token1";
        var options = Options.Create(new PennylaneFirmTokenOptions { new PennylaneFirmTokenItem { Id = firmId, Tokens = new List<string> { token } } });
        var handler = new TestHttpMessageHandler();
        handler.QueueError(HttpStatusCode.BadRequest, "bad request");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetPennylaneInternalIdByAccountNumberAsync(firmId, "ACC123"));
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_Throws_WhenNoToken()
    {
        var options = Options.Create(new PennylaneFirmTokenOptions());
        var httpClient = new HttpClient(new TestHttpMessageHandler()) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetPennylaneInternalIdByAccountNumberAsync("firmX", "ACC123"));
    }
    
    [Fact]
    public async Task GetCompanyDmsFilesAsync_ReturnsSingleFile_WhenFileIdIsProvided()
    {
        // Arrange
        var firmId = "firm1";
        var companyId = 1234;
        var fileId = "1";
        var token = "token1";
        var options = Options.Create(GetOptionsWithToken(firmId, token));
        var handler = new TestHttpMessageHandler();

        // Setup the expected response for a single file
        handler.QueueResponse(new PennylaneDmsFilesResponse
        {
            Items = new List<PennylaneDmsFile>
            {
                new PennylaneDmsFile { Id = 1, Name = "SingleFile", Path = "/", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now, Url = "urlSingle" }
            },
            HasMore = false,
            NextCursor = null
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        // Act
        var result = await service.GetCompanyDmsFilesAsync(firmId, companyId, fileId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SingleFile");
        handler.RequestCount.Should().Be(1, "Fetching a single file should make one HTTP call.");
    }


    [Fact]
    public async Task GetCompanyDmsFilesAsync_ThrowsRequestHttpException_WhenApiReturns404()
    {
        // Arrange
        var firmId = "firm1";
        var companyId = 1234;
        var token = "token1";
        var options = Options.Create(GetOptionsWithToken(firmId, token));
        var handler = new TestHttpMessageHandler();
        handler.QueueError(HttpStatusCode.NotFound, "not found");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<RequestHttpException>(() => service.GetCompanyDmsFilesAsync(firmId, companyId));
        ex.HttpCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCompanyDmsFilesAsync_ThrowsBadRequestException_WhenApiReturns500()
    {
        // Arrange
        var firmId = "firm1";
        var companyId = 1234;
        var token = "token1";
        var options = Options.Create(GetOptionsWithToken(firmId, token));
        var handler = new TestHttpMessageHandler();
        handler.QueueError(HttpStatusCode.InternalServerError, "server error");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new PennylaneApiFirmService(httpClient, NullLogger<PennylaneApiFirmService>.Instance, options);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetCompanyDmsFilesAsync(firmId, companyId));
    }

    // Handler de test pour simuler les r�ponses HTTP
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int RequestCount { get; private set; } = 0;

        public void QueueResponse(object response)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            };
            _responses.Enqueue(msg);
        }

        public void QueueError(HttpStatusCode code, string content)
        {
            var msg = new HttpResponseMessage(code)
            {
                Content = new StringContent(content)
            };
            _responses.Enqueue(msg);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
