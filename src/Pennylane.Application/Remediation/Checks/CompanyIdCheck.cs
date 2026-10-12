using Microsoft.Extensions.Logging;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation.Checks;

public sealed class CompanyIdCheck(IPennylaneApiGroupService pennylaneApiGroupService, ILogger<CompanyIdCheck> logger) : ISubscriptionCheck
{
    public string Key => SubscriptionChecks.CompanyId;

    public async Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (string.Equals(context.CompanyId, context.ExpectedCompanyId, StringComparison.OrdinalIgnoreCase))
        {
            return new CheckResult(Key, CheckOutcome.Compliant);
        }

        try
        {
            var updated = await pennylaneApiGroupService.TryUpdateCompanyIdAsync(context.CompanyId, context.ExpectedCompanyId);
            if (!updated)
            {
                return new CheckResult(Key, CheckOutcome.Failed, "Company id update was rejected by Pennylane.");
            }

            var previousId = context.CompanyId;
            context.CompanyId = context.ExpectedCompanyId;
            return new CheckResult(Key, CheckOutcome.Fixed, "Company id updated from " + previousId + " to " + context.ExpectedCompanyId + ".");
        }
        catch (Exception ex) when (ex is ServiceAuthenticationException || ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "{CheckKey} check failed calling the Pennylane API.", Key);
            return new CheckResult(Key, CheckOutcome.Failed, Key + " check failed calling the Pennylane API: " + ex.Message);
        }
    }
}
