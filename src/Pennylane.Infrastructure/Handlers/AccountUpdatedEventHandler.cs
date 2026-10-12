// <copyright file="AccountUpdatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Application.StaticValues;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class AccountUpdatedEventHandler : IEventHandler
{
    private readonly ILogger<AccountUpdatedEventHandler> _logger;
    private readonly IAccountService _accountService;

    public AccountUpdatedEventHandler(
    ILogger<AccountUpdatedEventHandler> logger,
    IAccountService accountService)
    {
        _logger = logger;
        _accountService = accountService;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("[ERREUR] Impossible de traiter l'événement car le message est null - AccountUpdatedEventHandler");
            return;
        }

        var accountEvent = JsonDefaults.TryDeserialize<AccountUpdatedEvent>(message);

        if (accountEvent?.Data == null || string.IsNullOrEmpty(accountEvent.Data.AccountNumber))
        {
            string eventData = accountEvent.Data is not null
                ? $"[ERREUR] Format de données invalide pour le déploiement de l'account: {accountEvent.Data.AccountNumber}"
                : "[ERREUR] Format de données invalide pour le déploiement de l'account";

            _logger.LogError(eventData);
            return;
        }

        var syncStatus = await _accountService.SyncAcountAsync(accountEvent.Data, OperationAction.Update);
        if (syncStatus)
        {
            _logger.LogInformation("Completed syncing event {EventType} related to the account {AccountNumber}",
               accountEvent.EventType,
               accountEvent.Data.AccountNumber);
        }

        _logger.LogInformation("Consommation de l'event type: {EventType}, accountNumber: {AccountNumber}",
            accountEvent.EventType,
            accountEvent.Data.AccountNumber);
    }
}