using Application.Models.Contacts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;

namespace Pennylane.Application.Tests.Managers;

public class AuthorizationUpdatedEventManagerTests
{
    private readonly Mock<ILogger<AuthorizationUpdatedEventManager>> _loggerMock;
    private readonly Mock<IPennylaneApiGroupService> _pennylaneServiceApiMock;

    public AuthorizationUpdatedEventManagerTests()
    {
        _loggerMock = new Mock<ILogger<AuthorizationUpdatedEventManager>>();
        _pennylaneServiceApiMock = new Mock<IPennylaneApiGroupService>();
    }

    private static IOptions<PennylaneServiceOptions> CreateOptions()
    {
        return Options.Create(new PennylaneServiceOptions
        {
            ApiUrl = "http://test",
            ApiFirmUrl = "http://test",
            ApiKey = "test-key",
            AuthenticationMode = "test-auth",
            CollaboratorAuthenticationMode = "test-collab-auth",
            DefaultRole = "test-role",
            SupportedSaasPlans = "test-plans",
            Token = "test-token"
        });
    }

    private AuthorizationUpdatedEventManager CreateManager()
    {
        return new AuthorizationUpdatedEventManager(
            _loggerMock.Object,
            _pennylaneServiceApiMock.Object,
            Mock.Of<global::Application.Interfaces.IAccountRepository>(),
            Mock.Of<global::Application.Interfaces.IContactRepository>(),
            CreateOptions());
    }

    private static Contact CreateTestContact(string email = "test@example.com")
    {
        return new Contact
        {
            ContactId = 1,
            Email = email,
            FirstName = "John",
            LastName = "Doe"
        };
    }

    #region TryCreatePennylaneUserAsync Tests

    [Fact]
    public async Task TryCreatePennylaneUserAsync_WhenUserCreated_ShouldReturnUserIdAndNotAlreadyExists()
    {
        // Arrange
        var manager = CreateManager();
        var contact = CreateTestContact();
        var expectedUserId = "user-789";

        _pennylaneServiceApiMock
            .Setup(x => x.TryPostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync((expectedUserId, false));

        // Act
        var result = await manager.TryCreatePennylaneUserAsync(contact, "sso");

        // Assert
        result.UserId.Should().Be(expectedUserId);
        result.AlreadyExists.Should().BeFalse();
    }

    [Fact]
    public async Task TryCreatePennylaneUserAsync_WhenUserAlreadyExists_ShouldReturnNullUserIdAndAlreadyExistsTrue()
    {
        // Arrange
        var manager = CreateManager();
        var contact = CreateTestContact();

        _pennylaneServiceApiMock
            .Setup(x => x.TryPostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync((null, true));

        // Act
        var result = await manager.TryCreatePennylaneUserAsync(contact, "sso");

        // Assert
        result.UserId.Should().BeNull();
        result.AlreadyExists.Should().BeTrue();

        // Verify info log was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User already exists")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryCreatePennylaneUserAsync_WhenCreationFails_ShouldReturnNullUserIdAndAlreadyExistsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var contact = CreateTestContact();

        _pennylaneServiceApiMock
            .Setup(x => x.TryPostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync((null, false));

        // Act
        var result = await manager.TryCreatePennylaneUserAsync(contact, "sso");

        // Assert
        result.UserId.Should().BeNull();
        result.AlreadyExists.Should().BeFalse();

        // Verify warning log was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User creation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
