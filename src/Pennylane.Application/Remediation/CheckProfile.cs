using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation;

public enum NotFoundBehavior
{
    LeaveUnchanged,
}

public sealed class CheckProfile
{
    public required ISubscriptionResolver Resolver { get; init; }

    public required NotFoundBehavior NotFound { get; init; }

    public required IReadOnlyList<ISubscriptionCheck> Checks { get; init; }
}
