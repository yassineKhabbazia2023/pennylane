// <copyright file="SubscriptionDeactivatedEventHandler.cs.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class SubscriptionDeactivatedEventHandler : IEventHandler
{
    private readonly ILogger<SubscriptionDeactivatedEventHandler> _logger;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionDeactivatedEventHandler(
        ILogger<SubscriptionDeactivatedEventHandler> logger,
        ISubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("[ERREUR] Impossible de traiter l'événement car le message est null - SubscriptionDeactivatedEventHandler");
            return;
        }

        var SubscriptionDeactivatedEvent = JsonDefaults.TryDeserialize<SubscriptionDeactivatedEvent>(message);

        if (SubscriptionDeactivatedEvent?.Data == null || string.IsNullOrEmpty(SubscriptionDeactivatedEvent.Data.OfferName))
        {
            string eventData = SubscriptionDeactivatedEvent!.Data is not null ?
                $"[ERREUR] Format de données invalide pour la supprission de la souscription de l'offre {SubscriptionDeactivatedEvent?.Data.OfferName} pour l'account {SubscriptionDeactivatedEvent!.Data.AccountId}" :
                "[ERREUR] Format de données invalide pour la supprission de la souscription";

            _logger.LogError(eventData);
            return;
        }

        _logger.LogInformation("Consommation de l'event type: {EventType}, OfferName: {OfferName}, AccountId {AccountId}",
            SubscriptionDeactivatedEvent.EventType,
            SubscriptionDeactivatedEvent.Data.OfferName,
             SubscriptionDeactivatedEvent.Data.AccountId);

        var subscription = await _subscriptionRepository.GetSubscriptionByOfferAndAccountAsync(
            SubscriptionDeactivatedEvent.Data.OfferName!,
            SubscriptionDeactivatedEvent.Data.AccountId);

        if (subscription == null) {
            _logger.LogInformation("Subscription Dosen't exists for OfferName: {OfferName} and AccountId: {AccountId}. No action taken.",
                SubscriptionDeactivatedEvent.Data.OfferName,
                SubscriptionDeactivatedEvent.Data.AccountId);

            return;
        }

        await _subscriptionRepository.DeleteSubcriptionAsync(subscription);

        _logger.LogInformation("Completed syncing event {EventType} related to the offer {OfferName} and the account {AccountID}",
           SubscriptionDeactivatedEvent.EventType,
           SubscriptionDeactivatedEvent.Data.OfferName,
           SubscriptionDeactivatedEvent.Data.AccountId);
    }
}