// <copyright file="RoleUpdatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Mappers;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class RoleDeletedEventHandler : IEventHandler
{
    private readonly ILogger<RoleDeletedEventHandler> _logger;
    private readonly IRoleRepository _roleRepository;
    private readonly IPennylaneService _pennylaneService;

    public RoleDeletedEventHandler(
    ILogger<RoleDeletedEventHandler> logger,
    IRoleRepository roleRepository,
    IPennylaneService pennylaneService
    )
    {
        _logger = logger;
        _roleRepository = roleRepository;
        _pennylaneService = pennylaneService;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("[ERREUR] Impossible de traiter l'événement car le message est null - RoleDeletedEventHandler");
            return;
        }

        var roleEvent = JsonDefaults.TryDeserialize<RoleDeletedEvent>(message);

        if (roleEvent?.Data == null || roleEvent.Data.ContactId <= 0 || roleEvent.Data.AccountId < -1)
        {
            var eventDataMessage = roleEvent.Data is not null
                ? $"[ERREUR] Format de données invalide pour le role du contact: {roleEvent?.Data.ContactId} sur l'account: {roleEvent?.Data.AccountId}"
                : "[ERREUR] Format de données invalide pour le role du contact";

            _logger.LogError(eventDataMessage);
            return;
        }

        var rolePulse = roleEvent!.Data.MapToRoleEntity();
        await _roleRepository.DeleteRoleAsync(rolePulse!);
        await _pennylaneService.DeletePennylaneRoleAsync(rolePulse.AccountId, rolePulse.ContactId);

        _logger.LogInformation("Le role du contact: {ContactId} sur l'account: {AccountId} vient d'être modifié.", rolePulse.ContactId, rolePulse.AccountId);
    }
}