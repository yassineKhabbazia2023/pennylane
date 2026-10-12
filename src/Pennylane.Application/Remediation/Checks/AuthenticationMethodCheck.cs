using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation.Checks;

/// <summary>
/// Verifies that every signatory's Pennylane user has the configured authentication method, and updates it when it differs.
/// </summary>
public sealed class AuthenticationMethodCheck(IPennylaneApiGroupService pennylaneApiGroupService, IOptions<PennylaneServiceOptions> options, ILogger<AuthenticationMethodCheck> logger) : ISubscriptionCheck
{
    private readonly PennylaneServiceOptions _options = options.Value;

    public string Key => SubscriptionChecks.AuthenticationMethod;

    public async Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Signatories.Count == 0)
        {
            return new CheckResult(Key, CheckOutcome.Compliant);
        }

        try
        {
            var fixedCount = 0;
            var failed = new List<string>();

            foreach (var signatory in context.Signatories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var user = await pennylaneApiGroupService.TryGetUserAsync(signatory.Email);
                if (user is null)
                {
                    failed.Add(signatory.Email + ": user not found.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(user.AuthenticationMethod))
                {
                    failed.Add(signatory.Email + ": authentication_method was not returned by Pennylane; cannot verify it matches the configured mode '" + _options.AuthenticationMode + "'.");
                    continue;
                }

                if (string.Equals(user.AuthenticationMethod, _options.AuthenticationMode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var updated = await pennylaneApiGroupService.TryUpdateUserAuthenticationMethodAsync(user.Id, _options.AuthenticationMode);
                if (!updated)
                {
                    failed.Add(signatory.Email + ": authentication method update was rejected by Pennylane.");
                    continue;
                }

                fixedCount++;
            }

            if (failed.Count > 0)
            {
                return new CheckResult(Key, CheckOutcome.Failed, string.Join(" ", failed));
            }

            if (fixedCount == 0)
            {
                return new CheckResult(Key, CheckOutcome.Compliant);
            }

            return new CheckResult(Key, CheckOutcome.Fixed, fixedCount + " signatory/ies updated to authentication mode '" + _options.AuthenticationMode + "'.");
        }
        catch (Exception ex) when (ex is ServiceAuthenticationException || ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "{CheckKey} check failed calling the Pennylane API.", Key);
            return new CheckResult(Key, CheckOutcome.Failed, Key + " check failed calling the Pennylane API: " + ex.Message);
        }
    }
}
