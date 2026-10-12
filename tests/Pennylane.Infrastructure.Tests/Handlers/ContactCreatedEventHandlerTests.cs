// <copyright file="ContactCreatedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Infrastructure.Tests.Providers
{
    public class ContactCreatedEventHandlerTests
    {
        private readonly Mock<ILogger<ContactCreatedEventHandler>> _loggerMock;
        private readonly Mock<IContactService> _serviceMock;
        private readonly ContactCreatedEventHandler _handler;
        private readonly Fixture _fixture;

        public ContactCreatedEventHandlerTests()
        {
            _loggerMock = new Mock<ILogger<ContactCreatedEventHandler>>();
            _serviceMock = new Mock<IContactService>(MockBehavior.Strict);
            _handler = new ContactCreatedEventHandler(_loggerMock.Object, _serviceMock.Object);
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
            _serviceMock.Verify(
                s => s.OnCreatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallService()
        {
            // Arrange
            var invalidJson = "not a json";

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
            _serviceMock.Verify(
                s => s.OnCreatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_MissingData_DoesNotCallService()
        {
            // Arrange
            var wrapper = new { EventType = nameof(ContactCreatedEvent), Data = (ContactStateEventData)null! };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(
                s => s.OnCreatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_CallsServiceOnce()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            var wrapper = new { EventType = nameof(ContactCreatedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnCreatedContactEventExecution(
                    It.Is<ContactStateEventData>(d =>
                        d.ContactId == eventData.ContactId &&
                        d.Email == eventData.Email)))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(s =>
                s.OnCreatedContactEventExecution(
                    It.Is<ContactStateEventData>(d =>
                        d.ContactId == eventData.ContactId &&
                        d.Email == eventData.Email)),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ServiceThrows_ExceptionIsHandled_AndDoesNotPropagate()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            var wrapper = new { EventType = nameof(ContactCreatedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnCreatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)))
                .ThrowsAsync(new InvalidOperationException("service error"));

            // Act
            var exception = await Record.ExceptionAsync(() => _handler.HandleAsync(message));

            // Assert
            Assert.Null(exception);
            _serviceMock.Verify(s =>
                s.OnCreatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)),
                Times.Once);
        }
    }
}
