using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation.Checks;

public sealed class SignatoryAttachmentCheck(IPennylaneApiGroupService pennylaneApiGroupService, IOptions<PennylaneServiceOptions> options, ILogger<SignatoryAttachmentCheck> logger) : ISubscriptionCheck
{
    private readonly PennylaneServiceOptions _options = options.Value;

    public string Key => SubscriptionChecks.SignatoryAttachment;

    public async Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Signatories.Count == 0)
        {
            return new CheckResult(Key, CheckOutcome.Compliant);
        }

        try
        {
            var existingUsers = await CompanyUserLookup.GetAllAsync(pennylaneApiGroupService, context.CompanyId, cancellationToken);
            var existingEmails = new HashSet<string>(existingUsers.Select(u => u.Email), StringComparer.OrdinalIgnoreCase);

            var missing = context.Signatories.Where(s => !existingEmails.Contains(s.Email)).ToList();
            if (missing.Count == 0)
            {
                return new CheckResult(Key, CheckOutcome.Compliant);
            }

            var role = await ClientRoleResolver.ResolveAsync(pennylaneApiGroupService, _options, context.CompanyId);

            var added = new List<string>();
            var failed = new List<string>();

            foreach (var signatory in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var attached = await AttachSignatoryAsync(context.CompanyId, signatory, role);
                if (attached)
                {
                    added.Add(signatory.Email);
                }
                else
                {
                    failed.Add(signatory.Email);
                }
            }

            if (failed.Count > 0)
            {
                return new CheckResult(Key, CheckOutcome.Failed, "Failed to attach signatories: " + string.Join(", ", failed) + ".");
            }

            return new CheckResult(Key, CheckOutcome.Fixed, "Added signatories: " + string.Join(", ", added) + ".");
        }
        catch (Exception ex) when (ex is ServiceAuthenticationException || ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "{CheckKey} check failed calling the Pennylane API.", Key);
            return new CheckResult(Key, CheckOutcome.Failed, Key + " check failed calling the Pennylane API: " + ex.Message);
        }
    }

    private async Task<bool> AttachSignatoryAsync(string companyId, ExpectedContact signatory, string role)
    {
        var userId = await GetOrCreateUserIdAsync(signatory);
        if (userId is null)
        {
            return false;
        }

        var grantAccess = GrantAccessFactory.Build(_options, role, userId, signatory.Email);

        var grantResult = await pennylaneApiGroupService.TryGrantCompanyUserAccessAsync(companyId, grantAccess);
        return grantResult is not null;
    }

    private async Task<string?> GetOrCreateUserIdAsync(ExpectedContact signatory)
    {
        var existingUser = await pennylaneApiGroupService.TryGetUserAsync(signatory.Email);
        if (existingUser is not null)
        {
            return existingUser.Id;
        }

        var contact = new PennylaneContact
        {
            FirstName = signatory.FirstName ?? string.Empty,
            LastName = signatory.LastName ?? string.Empty,
            Email = signatory.Email,
            SkipWelcomeEmail = true,
            AuthenticationMethod = _options.AuthenticationMode,
        };

        var (createdUserId, alreadyExists) = await pennylaneApiGroupService.TryPostUsersAsync(contact);
        if (createdUserId is not null)
        {
            return createdUserId;
        }

        if (alreadyExists)
        {
            var refetchedUser = await pennylaneApiGroupService.TryGetUserAsync(signatory.Email);
            return refetchedUser?.Id;
        }

        return null;
    }
}
