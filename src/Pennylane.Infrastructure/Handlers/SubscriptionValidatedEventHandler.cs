// <copyright file="SubscriptionValidatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class SubscriptionValidatedEventHandler : IEventHandler
{
    private readonly ILogger<SubscriptionValidatedEventHandler> _logger;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionValidatedEventHandler(
        ILogger<SubscriptionValidatedEventHandler> logger,
        ISubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("[ERREUR] Impossible de traiter l'événement car le message est null - SubscriptionValidatedEventHandler");
            return;
        }

        var subscriptionValidatedEvent = JsonDefaults.TryDeserialize<SubscriptionValidatedEvent>(message);

        if (subscriptionValidatedEvent?.Data is not null && !string.Equals(subscriptionValidatedEvent.Data.OfferName, OfferCodes.Pennylane, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (subscriptionValidatedEvent?.Data == null || string.IsNullOrEmpty(subscriptionValidatedEvent.Data.OfferName))
        {
            string eventData = subscriptionValidatedEvent!.Data is not null ?
                $"[ERREUR] Format de données invalide pour le sauvgarde de la souscription de l'offre {subscriptionValidatedEvent?.Data.OfferName} pour l'account {subscriptionValidatedEvent!.Data.AccountId}" :
                "[ERREUR] Format de données invalide pour le sauvgarde de la souscription";

            _logger.LogError(eventData);
            return;
        }


        _logger.LogInformation("Consommation de l'event type: {EventType}, OfferName: {OfferName}, AccountId {AccountId}",
            subscriptionValidatedEvent.EventType,
            subscriptionValidatedEvent.Data.OfferName,
             subscriptionValidatedEvent.Data.AccountId);

        var subscription = await _subscriptionRepository.GetSubscriptionByOfferAndAccountAsync(
            subscriptionValidatedEvent.Data.OfferName!,
            subscriptionValidatedEvent.Data.AccountId);

        if (subscription != null) {
            _logger.LogInformation("Subscription already exists for OfferName: {OfferName} and AccountId: {AccountId}. No action taken.",
                subscriptionValidatedEvent.Data.OfferName,
                subscriptionValidatedEvent.Data.AccountId);

            return;
        }

        var subscriptionEntity = new SubscriptionEntity()
        {
            AccountId = subscriptionValidatedEvent.Data.AccountId,
            OfferName = subscriptionValidatedEvent.Data.OfferName
        };

        await _subscriptionRepository.SaveSubcriptionAsync(subscriptionEntity);

        _logger.LogInformation("Completed syncing event {EventType} related to the offer {OfferName} and the account {AccountID}",
           subscriptionValidatedEvent.EventType,
           subscriptionValidatedEvent.Data.OfferName,
           subscriptionValidatedEvent.Data.AccountId);
    }
}