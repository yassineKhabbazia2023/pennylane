// <copyright file="AccountRemovedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.StaticValues;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Infrastructure.Tests.Providers;

public class AccountRemovedEventHandlerTests
{
    private readonly Mock<ILogger<AccountRemovedEventHandler>> _loggerMock;
    private readonly Mock<IAccountService> _accountServiceMock;
    private readonly AccountRemovedEventHandler _handler;
    private readonly Fixture _fixture;

    public AccountRemovedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AccountRemovedEventHandler>>();
        _accountServiceMock = new Mock<IAccountService>(MockBehavior.Strict);
        _handler = new AccountRemovedEventHandler(_loggerMock.Object, _accountServiceMock.Object);
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_DoesNotCallService(string message)
    {
        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(It.IsAny<int>()), Times.Never);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallService()
    {
        // Arrange
        var invalidJson = "not a json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(It.IsAny<int>()), Times.Never);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_DoesNotCallService()
    {
        // Arrange
        var wrapper = new { EventType = "AccountRemovedEvent", Data = (AccountRemovedEventData)null! };
        var message = JsonSerializer.Serialize(wrapper);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(It.IsAny<int>()), Times.Never);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ZeroAccountId_DoesNotCallService()
    {
        // Arrange
        var wrapper = new { EventType = "AccountRemovedEvent", Data = new AccountRemovedEventData { AccountId = 0 } };
        var message = JsonSerializer.Serialize(wrapper);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(It.IsAny<int>()), Times.Never);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_ServiceReturnsFalse_CallsServiceOnce()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var accountNumber = _fixture.Create<string>();
        var innerEvent = new AccountRemovedEventData { AccountId = accountId };
        var wrapper = new { EventType = "AccountRemovedEvent", Data = innerEvent };
        var message = JsonSerializer.Serialize(wrapper);

        _accountServiceMock
            .Setup(s => s.GetAccountNumberByIdAsync(accountId))
            .ReturnsAsync(accountNumber);

        _accountServiceMock
            .Setup(s => s.SyncAcountAsync(
                It.Is<AccountStateEventData>(d =>
                    d.AccountId == accountId &&
                    d.AccountNumber == accountNumber),
                OperationAction.Delete))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(accountId), Times.Once);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(
            It.Is<AccountStateEventData>(d => d.AccountId == accountId && d.AccountNumber == accountNumber),
            OperationAction.Delete),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_ServiceReturnsTrue_CallsServiceOnce()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var accountNumber = _fixture.Create<string>();
        var innerEvent = new AccountRemovedEventData { AccountId = accountId };
        var wrapper = new { EventType = "AccountRemovedEvent", Data = innerEvent };
        var message = JsonSerializer.Serialize(wrapper);

        _accountServiceMock
            .Setup(s => s.GetAccountNumberByIdAsync(accountId))
            .ReturnsAsync(accountNumber);

        _accountServiceMock
            .Setup(s => s.SyncAcountAsync(
                It.Is<AccountStateEventData>(d =>
                    d.AccountId == accountId &&
                    d.AccountNumber == accountNumber),
                OperationAction.Delete))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(accountId), Times.Once);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(
            It.Is<AccountStateEventData>(d => d.AccountId == accountId && d.AccountNumber == accountNumber),
            OperationAction.Delete),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GetAccountNumberThrows_PropagatesException()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var innerEvent = new AccountRemovedEventData { AccountId = accountId };
        var wrapper = new { EventType = "AccountRemovedEvent", Data = innerEvent };
        var message = JsonSerializer.Serialize(wrapper);

        _accountServiceMock
            .Setup(s => s.GetAccountNumberByIdAsync(accountId))
            .ThrowsAsync(new InvalidOperationException("fail"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(accountId), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SyncThrows_PropagatesException()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var accountNumber = _fixture.Create<string>();
        var innerEvent = new AccountRemovedEventData { AccountId = accountId };
        var wrapper = new { EventType = "AccountRemovedEvent", Data = innerEvent };
        var message = JsonSerializer.Serialize(wrapper);

        _accountServiceMock
            .Setup(s => s.GetAccountNumberByIdAsync(accountId))
            .ReturnsAsync(accountNumber);

        _accountServiceMock
            .Setup(s => s.SyncAcountAsync(
                It.Is<AccountStateEventData>(d =>
                    d.AccountId == accountId &&
                    d.AccountNumber == accountNumber),
                OperationAction.Delete))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
        _accountServiceMock.Verify(s => s.GetAccountNumberByIdAsync(accountId), Times.Once);
        _accountServiceMock.Verify(s => s.SyncAcountAsync(
            It.Is<AccountStateEventData>(d => d.AccountId == accountId && d.AccountNumber == accountNumber),
            OperationAction.Delete),
            Times.Once);
    }
}