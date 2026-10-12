// <copyright file="ContactUpdatedEventHandlerTests.cs" company="Pulse">
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
    public class ContactUpdatedEventHandlerTests
    {
        private readonly Mock<ILogger<ContactUpdatedEventHandler>> _loggerMock;
        private readonly Mock<IContactService> _serviceMock;
        private readonly ContactUpdatedEventHandler _handler;
        private readonly Fixture _fixture;

        public ContactUpdatedEventHandlerTests()
        {
            _loggerMock = new Mock<ILogger<ContactUpdatedEventHandler>>();
            _serviceMock = new Mock<IContactService>(MockBehavior.Strict);
            _handler = new ContactUpdatedEventHandler(_loggerMock.Object, _serviceMock.Object);
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
                s => s.OnUpdatedContactEventExecution(It.IsAny<ContactStateEventData>()),
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
                s => s.OnUpdatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_MissingData_DoesNotCallService()
        {
            // Arrange
            var wrapper = new { EventType = nameof(ContactUpdatedEvent), Data = (ContactStateEventData)null! };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(
                s => s.OnUpdatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task HandleAsync_NonPositiveContactId_DoesNotCallService(int invalidId)
        {
            // Arrange
            var dummy = new ContactStateEventData
            {
                ContactId = invalidId,
                FirstName = "fn",
                LastName = "ln",
                Email = "test@example.com"
            };
            var wrapper = new
            {
                EventType = nameof(ContactUpdatedEvent),
                Data = dummy
            };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(
                s => s.OnUpdatedContactEventExecution(It.IsAny<ContactStateEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_CallsServiceOnce()
        {
            // Arrange
            var eventData = _fixture.Build<ContactStateEventData>()
                .Without(x => x.PersonaName)
                .Without(x => x.Type)
                .Without(x => x.Status)
                .Without(x => x.Office)
                .Without(x => x.LandPhone)
                .Without(x => x.MobilePhone)
                .Without(x => x.CreationDate)
                .Without(x => x.ContactGlobalUniqueId)
                .Without(x => x.AccountNumber)
                .Create();

            var wrapper = new { EventType = nameof(ContactUpdatedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnUpdatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(s =>
                s.OnUpdatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ServiceThrows_PropagatesException()
        {
            // Arrange
            var eventData = _fixture.Build<ContactStateEventData>()
                .Without(x => x.PersonaName)
                .Without(x => x.Type)
                .Without(x => x.Status)
                .Without(x => x.Office)
                .Without(x => x.LandPhone)
                .Without(x => x.MobilePhone)
                .Without(x => x.CreationDate)
                .Without(x => x.ContactGlobalUniqueId)
                .Without(x => x.AccountNumber)
                .Create();

            var wrapper = new { EventType = nameof(ContactUpdatedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnUpdatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)))
                .ThrowsAsync(new InvalidOperationException("error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
            _serviceMock.Verify(s =>
                s.OnUpdatedContactEventExecution(
                    It.Is<ContactStateEventData>(d => d.ContactId == eventData.ContactId)),
                Times.Once);
        }
    }
}
