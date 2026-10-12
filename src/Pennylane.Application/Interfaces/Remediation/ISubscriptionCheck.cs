using Pennylane.Application.Remediation;

namespace Pennylane.Application.Interfaces.Remediation;

public interface ISubscriptionCheck
{
    string Key { get; }

    Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken);
}
