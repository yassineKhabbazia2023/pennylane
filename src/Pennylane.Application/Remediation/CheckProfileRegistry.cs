using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation;

public sealed class CheckProfileRegistry : ICheckProfileRegistry
{
    private static readonly string[] TransferInProgressCheckOrder =
    [
        SubscriptionChecks.CompanyId,
        SubscriptionChecks.Collaborators,
        SubscriptionChecks.SignatoryAttachment,
        SubscriptionChecks.SignatoryRole,
        SubscriptionChecks.AuthenticationMethod,
    ];

    private readonly IReadOnlyDictionary<string, CheckProfile> _profiles;

    public CheckProfileRegistry(ISubscriptionResolver sirenResolver, IEnumerable<ISubscriptionCheck> checks)
    {
        var checksByKey = checks.ToDictionary(c => c.Key);
        _profiles = new Dictionary<string, CheckProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [SubscriptionStatuses.PennylaneTransferInProgress] = new CheckProfile
            {
                Resolver = sirenResolver,
                NotFound = NotFoundBehavior.LeaveUnchanged,
                Checks = TransferInProgressCheckOrder.Select(key => checksByKey[key]).ToList(),
            },
        };
    }

    public bool TryGetProfile(string subscriptionStatus, out CheckProfile profile)
    {
        return _profiles.TryGetValue(subscriptionStatus, out profile!);
    }
}
