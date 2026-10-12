using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.StaticValues.Enum;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation.Checks;

/// <summary>
/// Verifies that the company's accounting contacts (manager, supervisor, accountant) match the expected collaborators, and fixes them with a single company update.
/// </summary>
public sealed class CollaboratorsCheck(IPennylaneApiGroupService pennylaneApiGroupService, IOptions<PennylaneServiceOptions> options, ILogger<CollaboratorsCheck> logger) : ISubscriptionCheck
{
    private readonly PennylaneServiceOptions _options = options.Value;

    public string Key => SubscriptionChecks.Collaborators;

    public async Task<CheckResult> ExecuteAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var expectedByFunctionId = BuildExpectedAccountingContacts(context.Collaborators);
        if (expectedByFunctionId.Count == 0)
        {
            return new CheckResult(Key, CheckOutcome.Compliant);
        }

        try
        {
            var currentByFunctionId = new Dictionary<int, string?>
            {
                [PennylaneFunctions.AccountingManagerFunctionId] = context.AccountingManagerId,
                [PennylaneFunctions.AccountingSupervisorFunctionId] = context.AccountingSupervisorId,
                [PennylaneFunctions.AccountantFunctionId] = context.AccountantId,
            };

            var userIdByEmail = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var email in expectedByFunctionId.Values.Select(c => c.Email).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var user = await pennylaneApiGroupService.TryGetUserAsync(email);
                userIdByEmail[email] = user?.Id;
            }

            var mismatched = expectedByFunctionId
                .Where(kv => userIdByEmail[kv.Value.Email] is null
                    || !string.Equals(currentByFunctionId[kv.Key], userIdByEmail[kv.Value.Email], StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (mismatched.Count == 0)
            {
                return new CheckResult(Key, CheckOutcome.Compliant);
            }

            var failedEmails = new List<string>();
            foreach (var contact in mismatched.Select(kv => kv.Value).DistinctBy(c => c.Email, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                userIdByEmail[contact.Email] ??= await CreateUserAsync(contact);
                var userId = userIdByEmail[contact.Email];
                if (userId is null)
                {
                    failedEmails.Add(contact.Email);
                    continue;
                }

                var (firmGrantSuccess, firmGrantError) = await pennylaneApiGroupService.GrantFirmUserAccessAsync(
                    context.FirmId, StaticValues.Enum.PennylaneRoles.Accountant.GetDescription(), userId, contact.Email);
                if (!firmGrantSuccess)
                {
                    logger.LogWarning(
                        "Failed to grant firm access to collaborator {Email} on firm {FirmId}. Error: {Error}",
                        contact.Email, context.FirmId, firmGrantError);
                }
            }

            var accountingContactsToSet = mismatched.Where(kv => userIdByEmail[kv.Value.Email] is not null).ToList();
            if (accountingContactsToSet.Count > 0)
            {
                var request = new UpdateCompanyAccountingContactsRequest();
                foreach (var kv in accountingContactsToSet)
                {
                    SetAccountingContact(request, kv.Key, userIdByEmail[kv.Value.Email]!);
                }

                var updated = await pennylaneApiGroupService.TryUpdateCompanyAccountingContactsAsync(context.CompanyId, request);
                if (!updated)
                {
                    return new CheckResult(Key, CheckOutcome.Failed, "Failed to update accounting contacts for company " + context.CompanyId + ".");
                }
            }

            if (failedEmails.Count > 0)
            {
                return new CheckResult(Key, CheckOutcome.Failed, "Could not resolve collaborators: " + string.Join(", ", failedEmails) + ".");
            }

            return new CheckResult(Key, CheckOutcome.Fixed, "Updated accounting contacts: " + string.Join(", ", accountingContactsToSet.Select(kv => kv.Key + " -> " + kv.Value.Email)) + ".");
        }
        catch (Exception ex) when (ex is ServiceAuthenticationException || ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "{CheckKey} check failed calling the Pennylane API.", Key);
            return new CheckResult(Key, CheckOutcome.Failed, Key + " check failed calling the Pennylane API: " + ex.Message);
        }
    }

    private static Dictionary<int, ExpectedContact> BuildExpectedAccountingContacts(IReadOnlyList<ExpectedContact> collaborators)
    {
        var accountingContacts = new Dictionary<int, ExpectedContact>();
        foreach (var collaborator in collaborators)
        {
            if (collaborator.FunctionId is not (PennylaneFunctions.AccountingManagerFunctionId
                or PennylaneFunctions.AccountingSupervisorFunctionId
                or PennylaneFunctions.AccountantFunctionId))
            {
                continue;
            }

            accountingContacts.TryAdd(collaborator.FunctionId.Value, collaborator);
        }

        return accountingContacts;
    }

    private static void SetAccountingContact(UpdateCompanyAccountingContactsRequest request, int functionId, string userId)
    {
        switch (functionId)
        {
            case PennylaneFunctions.AccountingManagerFunctionId:
                request.AccountingManagerId = userId;
                break;
            case PennylaneFunctions.AccountingSupervisorFunctionId:
                request.AccountingSupervisorId = userId;
                break;
            case PennylaneFunctions.AccountantFunctionId:
                request.AccountantId = userId;
                break;
        }
    }

    private async Task<string?> CreateUserAsync(ExpectedContact collaborator)
    {
        var contact = new PennylaneContact
        {
            FirstName = collaborator.FirstName ?? string.Empty,
            LastName = collaborator.LastName ?? string.Empty,
            Email = collaborator.Email,
            SkipWelcomeEmail = true,
            AuthenticationMethod = _options.CollaboratorAuthenticationMode,
        };

        var (createdUserId, alreadyExists) = await pennylaneApiGroupService.TryPostUsersAsync(contact);
        if (createdUserId is not null)
        {
            return createdUserId;
        }

        if (alreadyExists)
        {
            var refetchedUser = await pennylaneApiGroupService.TryGetUserAsync(collaborator.Email);
            return refetchedUser?.Id;
        }

        return null;
    }
}
