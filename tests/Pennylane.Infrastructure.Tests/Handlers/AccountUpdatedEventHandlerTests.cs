// <copyright file="AccountUpdatedEventHandlerTests.cs" company="Pulse">
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

namespace Infrastructure.Tests.Providers
{
    public class AccountUpdatedEventHandlerTests
    {
        private readonly Mock<ILogger<AccountUpdatedEventHandler>> _loggerMock;
        private readonly Mock<IAccountService> _accountServiceMock;
        private readonly AccountUpdatedEventHandler _handler;
        private readonly Fixture _fixture;

        public AccountUpdatedEventHandlerTests()
        {
            _loggerMock = new Mock<ILogger<AccountUpdatedEventHandler>>();
            _accountServiceMock = new Mock<IAccountService>(MockBehavior.Strict);
            _handler = new AccountUpdatedEventHandler(_loggerMock.Object, _accountServiceMock.Object);
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
            _accountServiceMock.Verify(
                s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallService()
        {
            // Arrange
            var invalidJson = "not a json";

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
            _accountServiceMock.Verify(
                s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_MissingData_DoesNotCallService()
        {
            // Arrange
            var wrapper = new { EventType = "AccountUpdatedEvent", Data = (AccountStateEventData)null! };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _accountServiceMock.Verify(
                s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_EmptyAccountNumber_DoesNotCallService()
        {
            // Arrange
            var eventData = _fixture
                .Build<AccountStateEventData>()
                .With(d => d.AccountNumber, string.Empty)
                .Create();
            var wrapper = new { EventType = "AccountUpdatedEvent", Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _accountServiceMock.Verify(
                s => s.SyncAcountAsync(It.IsAny<AccountStateEventData>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ServiceReturnsFalse_CallsServiceOnce()
        {
            // Arrange
            var eventData = _fixture.Create<AccountStateEventData>();
            var wrapper = new { EventType = "AccountUpdatedEvent", Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _accountServiceMock
                .Setup(s => s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d =>
                        d.AccountNumber == eventData.AccountNumber &&
                        d.AccountId == eventData.AccountId),
                    OperationAction.Update))
                .ReturnsAsync(false);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _accountServiceMock.Verify(s =>
                s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d => d.AccountNumber == eventData.AccountNumber),
                    OperationAction.Update),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ServiceReturnsTrue_CallsServiceOnce()
        {
            // Arrange
            var eventData = _fixture.Create<AccountStateEventData>();
            var wrapper = new { EventType = "AccountUpdatedEvent", Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _accountServiceMock
                .Setup(s => s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d =>
                        d.AccountNumber == eventData.AccountNumber &&
                        d.AccountId == eventData.AccountId),
                    OperationAction.Update))
                .ReturnsAsync(true);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _accountServiceMock.Verify(s =>
                s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d => d.AccountNumber == eventData.AccountNumber),
                    OperationAction.Update),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SyncThrows_PropagatesException()
        {
            // Arrange
            var eventData = _fixture.Create<AccountStateEventData>();
            var wrapper = new { EventType = "AccountUpdatedEvent", Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _accountServiceMock
                .Setup(s => s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d =>
                        d.AccountNumber == eventData.AccountNumber &&
                        d.AccountId == eventData.AccountId),
                    OperationAction.Update))
                .ThrowsAsync(new InvalidOperationException("boom"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
            _accountServiceMock.Verify(s =>
                s.SyncAcountAsync(
                    It.Is<AccountStateEventData>(d => d.AccountNumber == eventData.AccountNumber),
                    OperationAction.Update),
                Times.Once);
        }
    }
}