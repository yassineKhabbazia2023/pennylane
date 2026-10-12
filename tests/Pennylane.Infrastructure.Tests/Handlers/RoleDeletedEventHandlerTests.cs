// <copyright file="RoleDeletedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Infrastructure.Tests.Providers;

public class RoleDeletedEventHandlerTests
{
    private readonly Mock<ILogger<RoleDeletedEventHandler>> _loggerMock;
    private readonly Mock<IRoleRepository> _repoMock;
    private readonly RoleDeletedEventHandler _handler;
    private readonly Fixture _fixture;
    private readonly Mock<IPennylaneService> _pennylaneServiceMock;

    public RoleDeletedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<RoleDeletedEventHandler>>();
        _repoMock = new Mock<IRoleRepository>(MockBehavior.Strict);
        _pennylaneServiceMock = new Mock<IPennylaneService>();
        _handler = new RoleDeletedEventHandler(_loggerMock.Object, _repoMock.Object, _pennylaneServiceMock.Object);
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_DoesNotCallDeleteRole(string message)
    {
        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallDeleteRole()
    {
        // Arrange
        var invalidJson = "not a json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_DoesNotCallDeleteRole()
    {
        // Arrange
        var wrapper = new { EventType = nameof(RoleDeletedEvent), Data = (RoleDeletedEventData)null! };
        var message = JsonSerializer.Serialize(wrapper);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task HandleAsync_NonPositiveContactId_DoesNotCallDeleteRole(int invalidContactId)
    {
        // Arrange
        var data = _fixture.Build<RoleDeletedEventData>()
            .With(d => d.ContactId, invalidContactId)
            .With(d => d.AccountId, _fixture.Create<int>())
            .Create();
        var message = JsonSerializer.Serialize(new RoleDeletedEvent(data));

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-10)]
    public async Task HandleAsync_AccountIdLessThanMinusOne_DoesNotCallDeleteRole(int invalidAccountId)
    {
        // Arrange
        var data = _fixture.Build<RoleDeletedEventData>()
            .With(d => d.ContactId, _fixture.Create<int>() + 1)
            .With(d => d.AccountId, invalidAccountId)
            .Create();
        var message = JsonSerializer.Serialize(new RoleDeletedEvent(data));

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_CallsDeleteRoleOnce_WithMappedEntity()
    {
        // Arrange
        var data = _fixture.Build<RoleDeletedEventData>()
            .With(d => d.AccountId, _fixture.Create<int>())
            .With(d => d.AccountGlobalUniqueId, Guid.NewGuid())
            .With(d => d.ContactId, _fixture.Create<int>())
           .With(d => d.ContactGlobalUniqueId, Guid.NewGuid())
            .With(d => d.AccountNumber, _fixture.Create<string>())
            .With(d => d.ContactEmail, _fixture.Create<string>())
            .Create();
        var message = JsonSerializer.Serialize(new RoleDeletedEvent(data));

        _repoMock
            .Setup(r => r.DeleteRoleAsync(
                It.Is<RoleEntity>(e =>
                    e.AccountId == data.AccountId &&
                    e.ContactId == data.ContactId
                )))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeleteRoleThrows_PropagatesException()
    {
        // Arrange
        var data = _fixture.Build<RoleDeletedEventData>()
            .With(d => d.AccountId, _fixture.Create<int>())
            .With(d => d.ContactId, _fixture.Create<int>())
            .Create();
        var message = JsonSerializer.Serialize(new RoleDeletedEvent(data));

        _repoMock
            .Setup(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()))
            .ThrowsAsync(new InvalidOperationException("error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
        _repoMock.Verify(r => r.DeleteRoleAsync(It.IsAny<RoleEntity>()), Times.Once);
    }
}
