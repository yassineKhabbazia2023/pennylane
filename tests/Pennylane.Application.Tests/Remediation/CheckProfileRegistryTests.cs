using FluentAssertions;
using Moq;
using Pennylane.Application.Remediation;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Xunit;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Tests.Remediation;

public class CheckProfileRegistryTests
{
    [Fact]
    public void TryGetProfile_ForTransferInProgress_ReturnsProfileWithFiveOrderedChecks()
    {
        var registry = BuildRegistry();

        var found = registry.TryGetProfile(SubscriptionStatuses.PennylaneTransferInProgress, out var profile);

        found.Should().BeTrue();
        profile.NotFound.Should().Be(NotFoundBehavior.LeaveUnchanged);
        profile.Checks.Select(c => c.Key).Should().ContainInOrder(
            SubscriptionChecks.CompanyId,
            SubscriptionChecks.Collaborators,
            SubscriptionChecks.SignatoryAttachment,
            SubscriptionChecks.SignatoryRole,
            SubscriptionChecks.AuthenticationMethod);
    }

    [Fact]
    public void TryGetProfile_ForUnknownStatus_ReturnsFalse()
    {
        var registry = BuildRegistry();

        registry.TryGetProfile("pennylane-not-created", out _).Should().BeFalse();
    }

    private static CheckProfileRegistry BuildRegistry()
    {
        static ISubscriptionCheck FakeCheck(string key)
        {
            var mock = new Mock<ISubscriptionCheck>();
            mock.SetupGet(c => c.Key).Returns(key);
            return mock.Object;
        }

        return new CheckProfileRegistry(
            new Mock<ISubscriptionResolver>().Object,
            new[]
            {
                FakeCheck(SubscriptionChecks.CompanyId),
                FakeCheck(SubscriptionChecks.Collaborators),
                FakeCheck(SubscriptionChecks.SignatoryAttachment),
                FakeCheck(SubscriptionChecks.SignatoryRole),
                FakeCheck(SubscriptionChecks.AuthenticationMethod),
            });
    }
}
