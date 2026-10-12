// <copyright file="SubscriptionCheckRequestedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Hangfire;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Remediation;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class SubscriptionCheckRequestedEventHandler(
    ILogger<SubscriptionCheckRequestedEventHandler> logger,
    IBackgroundJobClient backgroundJobClient) : IEventHandler
{
    public Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            logger.LogError("[SubscriptionCheckRequestedEventHandler] Received an empty message.");
            return Task.CompletedTask;
        }

        var @event = JsonDefaults.TryDeserialize<PennylaneSubscriptionCheckRequestedEvent>(message);
        if (@event?.Data is null)
        {
            logger.LogError("[SubscriptionCheckRequestedEventHandler] Message has no data payload.");
            return Task.CompletedTask;
        }

        var data = @event.Data;
        var request = new RemediationRequest(
            data.SubscriptionId,
            data.AccountNumber,
            data.Siren,
            data.CurrentStatus,
            data.Collaborators.Select(p => new ExpectedContact(p.Email, p.FirstName, p.LastName, p.FunctionName, p.FunctionId)).ToList(),
            data.Signatories.Select(p => new ExpectedContact(p.Email, p.FirstName, p.LastName, p.FunctionName, p.FunctionId)).ToList());

        backgroundJobClient.Enqueue<ISubscriptionRemediationService>(service => service.RemediateAsync(request));
        logger.LogInformation("Enqueued remediation job for subscription {SubscriptionId}.", data.SubscriptionId);
        return Task.CompletedTask;
    }
}
