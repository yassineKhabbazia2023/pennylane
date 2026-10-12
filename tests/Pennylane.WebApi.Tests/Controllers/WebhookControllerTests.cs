namespace Pennylane.WebApi.Tests.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Webhook;
using Pennylane.WebApi.Controllers;
using Pulse.ExceptionMiddleware.Exceptions;
using Xunit;

public class WebhookControllerTests
{
    private readonly Mock<ILogger<WebhookController>> _loggerMock;
    private readonly Mock<IOptions<PennylaneServiceOptions>> _optionsMock;
    private readonly Mock<IWebhookProcessingService> _webhookProcessingServiceMock;
    private readonly WebhookController _controller;

    public WebhookControllerTests()
    {
        _loggerMock = new Mock<ILogger<WebhookController>>();
        _optionsMock = new Mock<IOptions<PennylaneServiceOptions>>();
        _webhookProcessingServiceMock = new Mock<IWebhookProcessingService>();

        _optionsMock.Setup(o => o.Value).Returns(new PennylaneServiceOptions
        {
            Token = "valid-token",
            SupportedSaasPlans = string.Empty,
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            AuthenticationMode = "auth",
            CollaboratorAuthenticationMode = "collabAuth",
            DefaultRole = "default",
        });

        _controller = new WebhookController(_loggerMock.Object, _optionsMock.Object, _webhookProcessingServiceMock.Object);
    }

    [Fact]
    public async Task HandleWebhook_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "user.created" };

        // Act
        var result = await _controller.HandleWebhook("invalid-token", payload);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ValidToken_InvalidEvent_ReturnsBadRequest()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "invalid.event" };

        // Act
        var result = await _controller.HandleWebhook("valid-token", payload);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ValidToken_ValidEvent_ReturnsOk()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "user_created" };

        _webhookProcessingServiceMock
            .Setup(w => w.ProcessEvent(It.IsAny<string>(), It.IsAny<WebhookPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.HandleWebhook("valid-token", payload);

        // Assert
        _webhookProcessingServiceMock.Verify(w => w.ProcessEvent("user.created", payload), Times.Once);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_NullPayload_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.HandleWebhook("valid-token", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ProcessEventThrowsBadRequestException_ReturnsBadRequest()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "user.created" };
        _webhookProcessingServiceMock
            .Setup(w => w.ProcessEvent(It.IsAny<string>(), It.IsAny<WebhookPayload>()))
            .Throws(new BadRequestException("Bad request exception", "details de l'erreur"));

        // Act
        var result = await _controller.HandleWebhook("valid-token", payload);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        Assert.Contains("details de l'erreur", badRequest.Value!.ToString());
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BadRequestException processing webhook event:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ProcessEventThrowsHttpRequestException_ReturnsServiceUnavailable()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "user.created" };
        _webhookProcessingServiceMock
            .Setup(w => w.ProcessEvent(It.IsAny<string>(), It.IsAny<WebhookPayload>()))
            .Throws(new HttpRequestException("Downstream service error"));

        // Act
        var result = await _controller.HandleWebhook("valid-token", payload);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        Assert.Contains("Service unavailable", objectResult.Value?.ToString());
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HttpRequestException occurred while processing webhook event:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ProcessEventThrowsGenericException_ReturnsInternalServerError()
    {
        // Arrange
        var payload = new WebhookPayload { Event = "user.created" };
        _webhookProcessingServiceMock
            .Setup(w => w.ProcessEvent(It.IsAny<string>(), It.IsAny<WebhookPayload>()))
            .Throws(new Exception("Unhandled server error"));

        // Act
        var result = await _controller.HandleWebhook("valid-token", payload);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.Contains("Internal server error", objectResult.Value?.ToString());
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error processing webhook event:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
