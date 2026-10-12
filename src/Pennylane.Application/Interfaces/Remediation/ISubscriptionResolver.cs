using Pennylane.Application.Remediation;

namespace Pennylane.Application.Interfaces.Remediation;

public interface ISubscriptionResolver
{
    Task<ResolutionResult> ResolveAsync(RemediationRequest request, CancellationToken cancellationToken);
}
