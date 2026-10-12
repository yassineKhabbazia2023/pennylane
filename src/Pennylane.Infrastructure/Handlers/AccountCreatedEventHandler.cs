// <copyright file="AccountCreatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Application.StaticValues;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class AccountCreatedEventHandler : IEventHandler
{
    private readonly ILogger<AccountCreatedEventHandler> _logger;
    private readonly IAccountService _accountService;

    public AccountCreatedEventHandler(
        ILogger<AccountCreatedEventHandler> logger,
        IAccountService accountService)
    {
        _logger = logger;
        _accountService = accountService;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("[ERREUR] Impossible de traiter l'événement car le message est null - AccountCreatedEventHandler");
            return;
        }

        var accountEvent = JsonDefaults.TryDeserialize<AccountCreatedEvent>(message);

        if (accountEvent?.Data == null || string.IsNullOrEmpty(accountEvent.Data.AccountNumber))
        {
            string eventData = accountEvent.Data is not null ?
                $"[ERREUR] Format de données invalide pour le deployment de l'account: {accountEvent?.Data.AccountNumber}" :
                "[ERREUR] Format de données invalide pour le deployment de l'account";

            _logger.LogError(eventData);
            return;
        }

        _logger.LogInformation("Consommation de l'event type: {EventType}, accountNumber: {AccountNumber}",
            accountEvent.EventType,
            accountEvent.Data.AccountNumber);

        var syncStatus = await _accountService.SyncAcountAsync(accountEvent.Data, OperationAction.Insert);
        if (syncStatus)
        {
            _logger.LogInformation("Completed syncing event {EventType} related to the account {AccountNumber}",
               accountEvent.EventType,
               accountEvent.Data.AccountNumber);
        }
    }
}