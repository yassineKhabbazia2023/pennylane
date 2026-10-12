using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models;
using Pennylane.Application.Remediation;
using Pennylane.Application.services;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Xunit;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Tests.Services;

public class SubscriptionRemediationServiceTests
{
    private readonly Mock<ICheckProfileRegistry> _registryMock = new();
    private readonly Mock<ISubscriptionResolver> _resolverMock = new();
    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<INotificationManager> _notificationMock = new();
    private readonly Mock<ILogger<SubscriptionRemediationService>> _loggerMock = new();

    private static RemediationRequest Request() => new(
        SubscriptionId: 42,
        ExpectedCompanyId: "EC-EXPECTED",
        Siren: "123456789",
        CurrentStatus: "pennylane-transfer-in-progress",
        Collaborators: [],
        Signatories: []);

    private SubscriptionRemediationService CreateSut() => new(
        _registryMock.Object,
        _apiMock.Object,
        _notificationMock.Object,
        _loggerMock.Object);

    [Fact]
    public async Task RemediateAsync_WhenNoProfileForStatus_LogsErrorAndDoesNotPublish()
    {
        await CreateSut().RemediateAsync(Request());

        _notificationMock.Verify(
            n => n.PublishAsync(It.IsAny<PennylaneSubscriptionCheckCompletedEvent>(), null),
            Times.Never);
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemediateAsync_WhenResolutionNotFound_PublishesFoundFalseWithEmptyResultsAndRunsNoChecks()
    {
        var checkMock = new Mock<ISubscriptionCheck>();
        checkMock.SetupGet(c => c.Key).Returns(SubscriptionChecks.CompanyId);
        var profile = new CheckProfile { Resolver = _resolverMock.Object, NotFound = NotFoundBehavior.LeaveUnchanged, Checks = [checkMock.Object] };
        _registryMock.Setup(r => r.TryGetProfile("pennylane-transfer-in-progress", out profile)).Returns(true);
        _resolverMock.Setup(r => r.ResolveAsync(It.IsAny<RemediationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult(false, null));
        BaseEvent<PennylaneSubscriptionCheckCompletedEventData>? published = null;
        _notificationMock.Setup(n => n.PublishAsync(It.IsAny<PennylaneSubscriptionCheckCompletedEvent>(), null))
            .Callback<BaseEvent<PennylaneSubscriptionCheckCompletedEventData>, string?>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RemediateAsync(Request());

        checkMock.Verify(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()), Times.Never);
        published.Should().NotBeNull();
        published!.Data.Found.Should().BeFalse();
        published.Data.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task RemediateAsync_WhenDirectoryCompanyNoLongerExists_PublishesFoundFalse()
    {
        var checkMock = new Mock<ISubscriptionCheck>();
        checkMock.SetupGet(c => c.Key).Returns(SubscriptionChecks.CompanyId);
        var profile = new CheckProfile { Resolver = _resolverMock.Object, NotFound = NotFoundBehavior.LeaveUnchanged, Checks = [checkMock.Object] };
        _registryMock.Setup(r => r.TryGetProfile("pennylane-transfer-in-progress", out profile)).Returns(true);
        _resolverMock.Setup(r => r.ResolveAsync(It.IsAny<RemediationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult(true, "EC-GONE"));
        _apiMock.Setup(a => a.TryGetCompanyByIdAsync("EC-GONE")).ReturnsAsync((PennylaneCompany?)null);
        BaseEvent<PennylaneSubscriptionCheckCompletedEventData>? published = null;
        _notificationMock.Setup(n => n.PublishAsync(It.IsAny<PennylaneSubscriptionCheckCompletedEvent>(), null))
            .Callback<BaseEvent<PennylaneSubscriptionCheckCompletedEventData>, string?>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RemediateAsync(Request());

        checkMock.Verify(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()), Times.Never);
        published.Should().NotBeNull();
        published!.Data.Found.Should().BeFalse();
        published.Data.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task RemediateAsync_WhenResolved_RunsChecksInProfileOrderAndPublishesResults()
    {
        var executionOrder = new List<string>();
        var checks = new[] { SubscriptionChecks.CompanyId, SubscriptionChecks.Collaborators }.Select(key =>
        {
            var mock = new Mock<ISubscriptionCheck>();
            mock.SetupGet(c => c.Key).Returns(key);
            mock.Setup(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add(key))
                .ReturnsAsync(new CheckResult(key, CheckOutcome.Compliant));
            return mock.Object;
        }).ToList();
        var profile = new CheckProfile { Resolver = _resolverMock.Object, NotFound = NotFoundBehavior.LeaveUnchanged, Checks = checks };
        _registryMock.Setup(r => r.TryGetProfile("pennylane-transfer-in-progress", out profile)).Returns(true);
        _resolverMock.Setup(r => r.ResolveAsync(It.IsAny<RemediationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult(true, "EC-RESOLVED"));
        _apiMock.Setup(a => a.TryGetCompanyByIdAsync("EC-RESOLVED")).ReturnsAsync(new PennylaneCompany { Id = "EC-RESOLVED" });
        BaseEvent<PennylaneSubscriptionCheckCompletedEventData>? published = null;
        _notificationMock.Setup(n => n.PublishAsync(It.IsAny<PennylaneSubscriptionCheckCompletedEvent>(), null))
            .Callback<BaseEvent<PennylaneSubscriptionCheckCompletedEventData>, string?>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RemediateAsync(Request());

        executionOrder.Should().ContainInOrder(SubscriptionChecks.CompanyId, SubscriptionChecks.Collaborators);
        published.Should().NotBeNull();
        published!.Data.Found.Should().BeTrue();
        published.Data.CurrentStatus.Should().Be("pennylane-transfer-in-progress");
        published.Data.Results.Should().AllSatisfy(r => r.Outcome.Should().Be(SubscriptionCheckOutcomes.Compliant));
    }

    [Fact]
    public async Task RemediateAsync_WhenOneCheckFails_StillRunsRemainingChecksAndPublishesAllResults()
    {
        var failedCheckMock = new Mock<ISubscriptionCheck>();
        failedCheckMock.SetupGet(c => c.Key).Returns(SubscriptionChecks.CompanyId);
        failedCheckMock.Setup(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckResult(SubscriptionChecks.CompanyId, CheckOutcome.Failed, "boom"));
        var nextCheckMock = new Mock<ISubscriptionCheck>();
        nextCheckMock.SetupGet(c => c.Key).Returns(SubscriptionChecks.Collaborators);
        nextCheckMock.Setup(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckResult(SubscriptionChecks.Collaborators, CheckOutcome.Compliant));
        var profile = new CheckProfile
        {
            Resolver = _resolverMock.Object,
            NotFound = NotFoundBehavior.LeaveUnchanged,
            Checks = [failedCheckMock.Object, nextCheckMock.Object],
        };
        _registryMock.Setup(r => r.TryGetProfile("pennylane-transfer-in-progress", out profile)).Returns(true);
        _resolverMock.Setup(r => r.ResolveAsync(It.IsAny<RemediationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult(true, "EC-RESOLVED"));
        _apiMock.Setup(a => a.TryGetCompanyByIdAsync("EC-RESOLVED")).ReturnsAsync(new PennylaneCompany { Id = "EC-RESOLVED" });
        BaseEvent<PennylaneSubscriptionCheckCompletedEventData>? published = null;
        _notificationMock.Setup(n => n.PublishAsync(It.IsAny<PennylaneSubscriptionCheckCompletedEvent>(), null))
            .Callback<BaseEvent<PennylaneSubscriptionCheckCompletedEventData>, string?>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RemediateAsync(Request());

        nextCheckMock.Verify(c => c.ExecuteAsync(It.IsAny<CheckContext>(), It.IsAny<CancellationToken>()), Times.Once);
        published.Should().NotBeNull();
        published!.Data.Results.Should().HaveCount(2);
        published.Data.Results.Should().Contain(r => r.CheckKey == SubscriptionChecks.CompanyId && r.Outcome == SubscriptionCheckOutcomes.Failed);
        published.Data.Results.Should().Contain(r => r.CheckKey == SubscriptionChecks.Collaborators && r.Outcome == SubscriptionCheckOutcomes.Compliant);
    }
}
