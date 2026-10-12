// <copyright file="AccountRemovedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Application.StaticValues;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Infrastructure.Handlers;

public class AccountRemovedEventHandler : IEventHandler
{
    private readonly ILogger<AccountRemovedEventHandler> _logger;
    private readonly IAccountService _accountService;

    public AccountRemovedEventHandler(
        ILogger<AccountRemovedEventHandler> logger,
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

        var accountEvent = JsonDefaults.TryDeserialize<AccountRemovedEvent>(message);

        if (accountEvent?.Data == null || accountEvent.Data.AccountId == 0)
        {
            string eventData = accountEvent.Data is not null ?
                $"[ERREUR] Format de données invalide pour le deployment de l'account: {accountEvent?.Data.AccountId}" :
                "[ERREUR] Format de données invalide pour le deployment de l'account";

            _logger.LogError(eventData);
            return;
        }

        _logger.LogInformation("Consommation de l'event type: {EventType}, AccountId: {AccountId}",
            accountEvent.EventType,
            accountEvent.Data.AccountId);

        var accountStateEventData = new AccountStateEventData() { AccountId = accountEvent.Data.AccountId };
        var accountNumber = await _accountService.GetAccountNumberByIdAsync(accountEvent.Data.AccountId);
        accountStateEventData.AccountNumber = accountNumber;

        var syncStatus = await _accountService.SyncAcountAsync(accountStateEventData, OperationAction.Delete);
        if (syncStatus)
        {
            _logger.LogInformation("Completed syncing event {EventType} related to the account {AccountNumber}",
               accountEvent.EventType,
               accountNumber);
        }
    }
}