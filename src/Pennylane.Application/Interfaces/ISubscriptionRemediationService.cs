using Pennylane.Application.Remediation;

namespace Pennylane.Application.Interfaces;

public interface ISubscriptionRemediationService
{
    Task RemediateAsync(RemediationRequest request);
}
