// <copyright file="ContactRemovedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Infrastructure.Tests.Providers
{
    public class ContactRemovedEventHandlerTests
    {
        private readonly Mock<ILogger<ContactRemovedEventHandler>> _loggerMock;
        private readonly Mock<IContactService> _serviceMock;
        private readonly Mock<IPennylaneGedPermissionRepository> _gedPermissionRepoMock;
        private readonly ContactRemovedEventHandler _handler;
        private readonly Fixture _fixture;

        public ContactRemovedEventHandlerTests()
        {
            _loggerMock = new Mock<ILogger<ContactRemovedEventHandler>>();
            _serviceMock = new Mock<IContactService>(MockBehavior.Strict);
            _gedPermissionRepoMock = new Mock<IPennylaneGedPermissionRepository>();
            _gedPermissionRepoMock
                .Setup(r => r.RemoveAllPermissionsForContactAsync(It.IsAny<int>()))
                .ReturnsAsync(true);
            _handler = new ContactRemovedEventHandler(_loggerMock.Object, _serviceMock.Object, _gedPermissionRepoMock.Object);
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
                s => s.OnRemovedContactEventExecution(It.IsAny<ContactRemovedEventData>()),
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
                s => s.OnRemovedContactEventExecution(It.IsAny<ContactRemovedEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_MissingData_DoesNotCallService()
        {
            // Arrange
            var wrapper = new { EventType = nameof(ContactRemovedEvent), Data = (ContactRemovedEventData)null! };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(
                s => s.OnRemovedContactEventExecution(It.IsAny<ContactRemovedEventData>()),
                Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task HandleAsync_NonPositiveContactId_DoesNotCallService(int invalidId)
        {
            // Arrange
            var wrapper = new { EventType = nameof(ContactRemovedEvent), Data = new ContactRemovedEventData { ContactId = invalidId } };
            var message = JsonSerializer.Serialize(wrapper);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(
                s => s.OnRemovedContactEventExecution(It.IsAny<ContactRemovedEventData>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_CallsServiceOnce()
        {
            // Arrange
            var eventData = _fixture.Create<ContactRemovedEventData>();
            var wrapper = new { EventType = nameof(ContactRemovedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnRemovedContactEventExecution(
                    It.Is<ContactRemovedEventData>(d => d.ContactId == eventData.ContactId)))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleAsync(message);

            // Assert
            _serviceMock.Verify(s =>
                s.OnRemovedContactEventExecution(
                    It.Is<ContactRemovedEventData>(d => d.ContactId == eventData.ContactId)),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ServiceThrows_ExceptionIsHandled_AndDoesNotPropagate()
        {
            // Arrange
            var eventData = _fixture.Create<ContactRemovedEventData>();
            var wrapper = new { EventType = nameof(ContactRemovedEvent), Data = eventData };
            var message = JsonSerializer.Serialize(wrapper);

            _serviceMock
                .Setup(s => s.OnRemovedContactEventExecution(
                    It.Is<ContactRemovedEventData>(d => d.ContactId == eventData.ContactId)))
                .ThrowsAsync(new InvalidOperationException("service error"));

            // Act
            var exception = await Record.ExceptionAsync(() => _handler.HandleAsync(message));

            // Assert
            Assert.Null(exception);
            _serviceMock.Verify(s =>
                s.OnRemovedContactEventExecution(
                    It.Is<ContactRemovedEventData>(d => d.ContactId == eventData.ContactId)),
                Times.Once);
        }
    }
}
