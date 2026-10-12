// <copyright file="SubscriptionValidatedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Infrastructure.Tests.Handlers;

public class SubscriptionValidatedEventHandlerTests
{
    private readonly Mock<ILogger<SubscriptionValidatedEventHandler>> _loggerMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly SubscriptionValidatedEventHandler _handler;
    private readonly Fixture _fixture;

    public SubscriptionValidatedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<SubscriptionValidatedEventHandler>>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>(MockBehavior.Strict);
        _handler = new SubscriptionValidatedEventHandler(_loggerMock.Object, _subscriptionRepositoryMock.Object);
        _fixture = new Fixture();
    }


    [Fact]
    public async Task HandleAsync_Offer_DiffrentThan_Pennylane_Stops_Processing()
    {
        var eventData = new SubscriptionValidatedEventData
        {
            OfferName = "testOff",
            AccountId = 123
        };
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
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
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
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
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_LogsErrorAndDoesNotCallRepository()
    {
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = (object)null! };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmptyOfferName_LogsErrorAndDoesNotCallRepository()
    {
        var eventData = new SubscriptionValidatedEventData
        {
            OfferName = "",
            AccountId = 123
        };
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NullOfferName_DoesNotThrow_AndDoesNotCallRepository()
    {
        // OfferName is a nullable, non-required property on SubscriptionValidatedEventData: a
        // well-formed payload can legitimately omit it. The handler used to call
        // Data.OfferName!.Equals(...) with a null-forgiving operator, which threw a
        // NullReferenceException instead of just skipping the event.
        var eventData = new SubscriptionValidatedEventData
        {
            OfferName = null,
            AccountId = 123
        };
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ExistingSubscription_DoesNotSaveAgain()
    {
        var eventData = _fixture.Build<SubscriptionValidatedEventData>()
            .With(d => d.OfferName, OfferCodes.Pennylane)
            .With(d => d.AccountId, 42)
            .Create();
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        _subscriptionRepositoryMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId))
            .ReturnsAsync(new SubscriptionEntity());

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId),
            Times.Once);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.IsAny<SubscriptionEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_SavesSubscription()
    {
        var eventData = _fixture.Build<SubscriptionValidatedEventData>()
            .With(d => d.OfferName, OfferCodes.Pennylane)
            .With(d => d.AccountId, 42)
            .Create();
        var wrapper = new { EventType = "SubscriptionValidatedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        _subscriptionRepositoryMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId))
            .ReturnsAsync((SubscriptionEntity)null!);
        _subscriptionRepositoryMock
            .Setup(r => r.SaveSubcriptionAsync(It.Is<SubscriptionEntity>(s =>
                s.OfferName == eventData.OfferName && s.AccountId == eventData.AccountId)))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(message);

        _subscriptionRepositoryMock.Verify(
            r => r.GetSubscriptionByOfferAndAccountAsync(eventData.OfferName!, eventData.AccountId),
            Times.Once);
        _subscriptionRepositoryMock.Verify(
            r => r.SaveSubcriptionAsync(It.Is<SubscriptionEntity>(s =>
                s.OfferName == eventData.OfferName && s.AccountId == eventData.AccountId)),
            Times.Once);
    }
}
