using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation.Checks;

public sealed class SignatoryRoleCheck(IPennylaneApiGroupService pennylaneApiGroupService, IOptions<PennylaneServiceOptions> options, ILogger<SignatoryRoleCheck> logger) : ISubscriptionCheck
{
    private readonly PennylaneServiceOptions _options = options.Value;

    public string Key => SubscriptionChecks.SignatoryRole;

    public async Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Signatories.Count == 0)
        {
            return new CheckResult(Key, CheckOutcome.Compliant);
        }

        try
        {
            var companyUsers = await CompanyUserLookup.GetAllAsync(pennylaneApiGroupService, context.CompanyId, cancellationToken);
            var usersByEmail = companyUsers
                .GroupBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var expectedRole = await ClientRoleResolver.ResolveAsync(pennylaneApiGroupService, _options, context.CompanyId);

            var repaired = new List<string>();
            var failed = new List<string>();

            foreach (var signatory in context.Signatories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!usersByEmail.TryGetValue(signatory.Email, out var user))
                {
                    failed.Add(signatory.Email + ": user not found in company.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(user.Role))
                {
                    failed.Add(signatory.Email + ": Role not exposed by company users listing.");
                    continue;
                }

                if (string.Equals(user.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var grantAccess = GrantAccessFactory.Build(_options, expectedRole, user.Id, signatory.Email);

                var grantResult = await pennylaneApiGroupService.TryGrantCompanyUserAccessAsync(context.CompanyId, grantAccess);
                if (grantResult is null)
                {
                    failed.Add(signatory.Email + ": re-grant with the expected role failed.");
                    continue;
                }

                repaired.Add(signatory.Email);
            }

            if (failed.Count > 0)
            {
                return new CheckResult(Key, CheckOutcome.Failed, string.Join(" ", failed));
            }

            if (repaired.Count == 0)
            {
                return new CheckResult(Key, CheckOutcome.Compliant);
            }

            return new CheckResult(Key, CheckOutcome.Fixed, "Re-granted the '" + expectedRole + "' role for: " + string.Join(", ", repaired) + ".");
        }
        catch (Exception ex) when (ex is ServiceAuthenticationException || ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "{CheckKey} check failed calling the Pennylane API.", Key);
            return new CheckResult(Key, CheckOutcome.Failed, Key + " check failed calling the Pennylane API: " + ex.Message);
        }
    }
}
