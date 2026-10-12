// <copyright file="WebhookProcessingService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models.Webhook;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Application.services;

public class WebhookProcessingService : IWebhookProcessingService
{
    private readonly INotificationManager _notificationManager;
    private readonly ILogger<WebhookProcessingService> _logger;
    private readonly IPennylaneRepository _pennylaneRepository;

    public WebhookProcessingService(INotificationManager notificationManager, ILogger<WebhookProcessingService> logger, IPennylaneRepository pennylaneRepository)
    {
        _notificationManager = notificationManager;
        _logger = logger;
        _pennylaneRepository = pennylaneRepository;
    }

    public async Task ProcessEvent(string eventName, WebhookPayload payload)
    {
        try
        {
            if (eventName == PennylaneEventNames.UserCreatedEvent)
            {
                var user = payload.After;
                await ProcessCreateUserEvent(user, eventName);
            }

            if (eventName == PennylaneEventNames.UserUpdatedEvent)
            {
                await ProcessUserRemovedEvent(payload, eventName);
                await SyncRoleUpdateFromWebhookAsync(payload, eventName);
            }
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, "ServiceBusException occurred while processing event: {EventName}", eventName);
        }
    }

    private async Task ProcessCreateUserEvent(User user, string eventName)
    {
        var @event = new PennylaneUserCreatedEventData()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            PhoneNumber = user.PhoneNumber,
            AssignedToFirm = user.AssignedToFirm,
            CompanyIds = user.CompanyIds ?? new List<string>(),
            SubRole = user.SubRole,
        };

        await _notificationManager.PublishAsync(new PennylaneUserCreatedEvent(@event));
        _logger.LogInformation("Processed event: {EventName} related to the user: {User}", eventName, user.Email);
    }

    private async Task ProcessUserRemovedEvent(WebhookPayload payload, string eventName)
    {
        var before = payload.Before;
        var after = payload.After;

        if (before is null || after is null)
        {
            _logger.LogWarning("Payload before or after is null for event: {EventName}", eventName);
            return;
        }

        var removedCompanies = before.CompanyIds!
            .Except(after.CompanyIds!)
            .ToList();

        if (removedCompanies.Count == 0)
        {
            _logger.LogInformation("No companies removed for user {Email}, skipping event publication.", after.Email);
            return;
        }

        var externalRemovals = await GetExternalRemovalsAsync(removedCompanies, after.Email);
        if (externalRemovals.Count == 0)
        {
            _logger.LogInformation(
                "All removals for user {Email} are self-initiated, skipping PennylaneUserRemovedEvent.",
                after.Email);
            return;
        }

        var removedUserEvent = new PennylaneUserRemovedEventData()
        {
            Id = after.Id,
            Email = after.Email,
            CompanyIds = externalRemovals
        };

        await _notificationManager.PublishAsync(new PennylaneUserRemovedEvent(removedUserEvent));
        _logger.LogInformation("Processed event: {EventName} for user {UserEmail} with removed companies: {RemovedCompanies}",
            eventName, after.Email, externalRemovals);
    }

    private async Task<List<string>> GetExternalRemovalsAsync(List<string> removedCompanies, string email)
    {
        var externalRemovals = new List<string>();

        foreach (var companyId in removedCompanies)
        {
            if (await _pennylaneRepository.PennylaneRoleExistsAsync(companyId, email))
            {
                externalRemovals.Add(companyId);
            }
            else
            {
                _logger.LogInformation(
                    "Skipping self-initiated revocation for user {Email} on company {CompanyId}.",
                    email, companyId);
            }
        }

        return externalRemovals;
    }

    private async Task SyncRoleUpdateFromWebhookAsync(WebhookPayload payload, string eventName)
    {
        var after = payload.After;

        if (after is null || string.IsNullOrWhiteSpace(after.SubRole) || after.CompanyIds is null || after.CompanyIds.Count == 0)
        {
            return;
        }

        // ⚠️ Pennylane may send before == after even when the sub_role has actually changed.
        foreach (var companyId in after.CompanyIds)
        {
            try
            {
                await _pennylaneRepository.UpsertPennylaneRoleByEmailAndAccountNumberAsync(after.Email, companyId, after.SubRole);

                _logger.LogInformation("Upserted sub_role '{SubRole}' for user {Email} on company {CompanyId} from event {EventName}.", after.SubRole, after.Email, companyId, eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update sub_role for user {Email} on company {CompanyId} from event {EventName}.", after.Email, companyId, eventName);
            }
        }
    }
}
