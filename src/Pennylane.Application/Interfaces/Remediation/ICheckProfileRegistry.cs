using Pennylane.Application.Remediation;

namespace Pennylane.Application.Interfaces.Remediation;

public interface ICheckProfileRegistry
{
    bool TryGetProfile(string subscriptionStatus, out CheckProfile profile);
}
