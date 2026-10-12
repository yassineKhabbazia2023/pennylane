using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Remediation;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.services;

public class SubscriptionRemediationService(
    ICheckProfileRegistry checkProfileRegistry,
    IPennylaneApiGroupService pennylaneApiGroupService,
    INotificationManager notificationManager,
    ILogger<SubscriptionRemediationService> logger) : ISubscriptionRemediationService
{
    public async Task RemediateAsync(RemediationRequest request)
    {
        if (!checkProfileRegistry.TryGetProfile(request.CurrentStatus, out var profile))
        {
            logger.LogError("No check profile registered for status {Status} (subscription {SubscriptionId}).", request.CurrentStatus, request.SubscriptionId);
            return;
        }

        var resolution = await profile.Resolver.ResolveAsync(request, CancellationToken.None);
        if (!resolution.Found || string.IsNullOrWhiteSpace(resolution.CompanyId))
        {
            await PublishAsync(request, found: false, Array.Empty<CheckResult>());
            return;
        }

        var company = await pennylaneApiGroupService.TryGetCompanyByIdAsync(resolution.CompanyId);
        if (company is null)
        {
            logger.LogWarning("Company {CompanyId} from directory no longer exists live (subscription {SubscriptionId}).", resolution.CompanyId, request.SubscriptionId);
            await PublishAsync(request, found: false, Array.Empty<CheckResult>());
            return;
        }

        var context = new CheckContext
        {
            ExpectedCompanyId = request.ExpectedCompanyId,
            Siren = request.Siren,
            CompanyId = company.Id,
            FirmId = company.FirmId,
            AccountingManagerId = company.AccountingManagerId,
            AccountingSupervisorId = company.AccountingSupervisorId,
            AccountantId = company.AccountantId,
            Collaborators = request.Collaborators,
            Signatories = request.Signatories,
        };

        var results = new List<CheckResult>();
        foreach (var check in profile.Checks)
        {
            results.Add(await check.ExecuteAsync(context, CancellationToken.None));
        }

        await PublishAsync(request, found: true, results);
    }

    private async Task PublishAsync(RemediationRequest request, bool found, IReadOnlyList<CheckResult> results)
    {
        var eventData = new PennylaneSubscriptionCheckCompletedEventData
        {
            SubscriptionId = request.SubscriptionId,
            CurrentStatus = request.CurrentStatus,
            Found = found,
            Results = results.Select(r => new SubscriptionCheckResultData
            {
                CheckKey = r.CheckKey,
                Outcome = MapOutcome(r.Outcome),
                Detail = r.Detail,
            }).ToList(),
        };

        await notificationManager.PublishAsync(new PennylaneSubscriptionCheckCompletedEvent(eventData));
        logger.LogInformation("Published check result for subscription {SubscriptionId} (found: {Found}).", request.SubscriptionId, found);
    }

    private static string MapOutcome(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Compliant => SubscriptionCheckOutcomes.Compliant,
        CheckOutcome.Fixed => SubscriptionCheckOutcomes.Fixed,
        _ => SubscriptionCheckOutcomes.Failed,
    };
}
