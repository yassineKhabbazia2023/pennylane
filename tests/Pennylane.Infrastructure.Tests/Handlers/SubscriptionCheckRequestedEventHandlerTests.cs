// <copyright file="SubscriptionCheckRequestedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Linq;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Remediation;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Infrastructure.Tests.Handlers;

public class SubscriptionCheckRequestedEventHandlerTests
{
    private readonly Mock<ILogger<SubscriptionCheckRequestedEventHandler>> _loggerMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly SubscriptionCheckRequestedEventHandler _handler;

    public SubscriptionCheckRequestedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<SubscriptionCheckRequestedEventHandler>>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>(MockBehavior.Strict);
        _handler = new SubscriptionCheckRequestedEventHandler(_loggerMock.Object, _backgroundJobClientMock.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_DoesNotEnqueue(string message)
    {
        await _handler.HandleAsync(message);

        _backgroundJobClientMock.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_DoesNotEnqueue()
    {
        var wrapper = new { EventType = "PennylaneSubscriptionCheckRequestedEvent", Data = (object)null! };
        var message = JsonSerializer.Serialize(wrapper);

        await _handler.HandleAsync(message);

        _backgroundJobClientMock.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_EnqueuesRemediationWithMappedRequest()
    {
        var eventData = new PennylaneSubscriptionCheckRequestedEventData
        {
            SubscriptionId = 42,
            AccountNumber = "EC12345678",
            Siren = "123456789",
            CurrentStatus = "pennylane-transfer-in-progress",
            RequestedPlanCode = "PREMIUM",
            Collaborators = new[]
            {
                new SubscriptionCheckContactData { Email = "collab@kpmg.fr", FirstName = "Jean", LastName = "Collab", FunctionName = "Manager", FunctionId = 1 },
            },
            Signatories = new[]
            {
                new SubscriptionCheckContactData { Email = "client@acme.fr", FirstName = "Alice", LastName = "Cliente" },
            },
        };
        var wrapper = new { EventType = "PennylaneSubscriptionCheckRequestedEvent", Data = eventData };
        var message = JsonSerializer.Serialize(wrapper);

        Job? capturedJob = null;
        _backgroundJobClientMock
            .Setup(c => c.Create(
                It.Is<Job>(j => j.Method.Name == nameof(ISubscriptionRemediationService.RemediateAsync)),
                It.IsAny<EnqueuedState>()))
            .Callback<Job, IState>((job, _) => capturedJob = job)
            .Returns("job-id");

        await _handler.HandleAsync(message);

        _backgroundJobClientMock.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Method.Name == nameof(ISubscriptionRemediationService.RemediateAsync)),
                It.IsAny<EnqueuedState>()),
            Times.Once);

        Assert.NotNull(capturedJob);
        var request = Assert.IsType<RemediationRequest>(capturedJob!.Args.Single());
        Assert.Equal(42, request.SubscriptionId);
        Assert.Equal("EC12345678", request.ExpectedCompanyId);
        Assert.Equal("123456789", request.Siren);
        Assert.Equal("pennylane-transfer-in-progress", request.CurrentStatus);

        Assert.Single(request.Collaborators);
        Assert.Equal("collab@kpmg.fr", request.Collaborators[0].Email);
        Assert.Equal("Jean", request.Collaborators[0].FirstName);
        Assert.Equal("Collab", request.Collaborators[0].LastName);
        Assert.Equal("Manager", request.Collaborators[0].FunctionName);
        Assert.Equal(1, request.Collaborators[0].FunctionId);

        Assert.Single(request.Signatories);
        Assert.Equal("client@acme.fr", request.Signatories[0].Email);
        Assert.Equal("Alice", request.Signatories[0].FirstName);
        Assert.Equal("Cliente", request.Signatories[0].LastName);
        Assert.Null(request.Signatories[0].FunctionName);
    }
}
