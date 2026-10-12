// <copyright file="SubscriptionDeactivatedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Infrastructure.Tests.Handlers;

public class SubscriptionDeactivatedEventHandlerTests
{
    private readonly Mock<ILogger<SubscriptionDeactivatedEventHandler>> _loggerMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly SubscriptionDeactivatedEventHandler _handler;
    private readonly Fixture _fixture;

    public SubscriptionDeactivatedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<SubscriptionDeactivatedEventHandler>>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>(MockBehavior.Strict);
        _handler = new SubscriptionDeactivatedEventHandler(_loggerMock.Object, _subscriptionRepositoryMock.Object);
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_LogsErrorAndDoesNotCallRepository(string message)
    {
        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallRepository()
    {
        var invalidJson = "not a json";

        await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_LogsErrorAndDoesNotCallRepository()
    {
        var wrapper = new { EventType = "SubscriptionDeactivatedEvent", Data = (object)null! };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmptyOfferName_LogsErrorAndDoesNotCallRepository()
    {
        var eventData = new SubscriptionDeactivatedEventData
        {
            OfferName = "",
            AccountId = 123
        };
        var wrapper = new { EventType = "SubscriptionDeactivatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SubscriptionNotFound_LogsInfoAndDoesNotDelete()
    {
        var eventData = _fixture.Build<SubscriptionDeactivatedEventData>()
            .With(d => d.OfferName, "TestOffer")
            .With(d => d.AccountId, 42)
            .Create();
        var wrapper = new { EventType = "SubscriptionDeactivatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        _subscriptionRepositoryMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId))
            .ReturnsAsync((SubscriptionEntity)null!);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId),
            Times.Once);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_DeletesSubscription()
    {
        var eventData = _fixture.Build<SubscriptionDeactivatedEventData>()
            .With(d => d.OfferName, "TestOffer")
            .With(d => d.AccountId, 42)
            .Create();
        var wrapper = new { EventType = "SubscriptionDeactivatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        var subscriptionEntity = new SubscriptionEntity
        {
            OfferName = eventData.OfferName,
            AccountId = eventData.AccountId
        };

        _subscriptionRepositoryMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId))
            .ReturnsAsync(subscriptionEntity);
        _subscriptionRepositoryMock
            .Setup(r => r.DeleteSubcriptionAsync(subscriptionEntity))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId),
            Times.Once);
        _subscriptionRepositoryMock.Verify(
            r => r.DeleteSubcriptionAsync(subscriptionEntity),
            Times.Once);
    }
}
