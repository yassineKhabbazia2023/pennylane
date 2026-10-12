using System.Net;
using Moq;
using Moq.Protected;
using Pennylane.Infrastructure.ExternalServices;

namespace Pennylane.Infrastructure.Tests.ExternalServices;

public class DocumentStreamServiceTests
{
    [Fact]
        public async Task GetStreamFromUrlAsync_WhenUrlIsValid_ReturnsStream()
        {
            // Arrange
            var expectedStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StreamContent(expectedStream)
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object);
            var streamHelper = new DocumentStreamService(httpClient);
            var url = "http://example.com/stream";

            // Act
            var result = await streamHelper.GetStreamFromUrlAsync(url);

            // Assert
            Assert.NotNull(result);
            var memoryStream = new MemoryStream();
            await result.CopyToAsync(memoryStream);
            Assert.Equal(expectedStream.ToArray(), memoryStream.ToArray());
        }

        [Fact]
        public async Task GetStreamFromUrlAsync_WhenUrlIsInvalid_ThrowsArgumentException()
        {
            // Arrange
            var httpClient = new HttpClient();
            var streamHelper = new DocumentStreamService(httpClient);
            var url = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => streamHelper.GetStreamFromUrlAsync(url));
        }

        [Fact]
        public async Task GetStreamFromUrlAsync_WhenRequestFails_ThrowsHttpRequestException()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object);
            var streamHelper = new DocumentStreamService(httpClient);
            var url = "http://example.com/error";

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => streamHelper.GetStreamFromUrlAsync(url));
        }
}