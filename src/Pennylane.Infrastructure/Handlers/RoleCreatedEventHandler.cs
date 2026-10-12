// <copyright file="RoleCreatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Application.StaticValues.Enum;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using PnlRoles = Pennylane.Application.Consts.PennylaneRoles;

namespace Pennylane.Infrastructure.Handlers;

public class RoleCreatedEventHandler : IEventHandler
{
    private readonly ILogger<RoleCreatedEventHandler> _logger;
    private readonly IRoleRepository _roleRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IPennylaneApiGroupService _pennylaneServiceApi;
    private readonly IPennylaneRepository _pennylaneRepository;
    private readonly string _authenticationMode;
    private readonly bool _useUserIdForGrantAccess;

    public RoleCreatedEventHandler(
        ILogger<RoleCreatedEventHandler> logger,
        IRoleRepository roleRepository,
        IAccountRepository accountRepository,
        IContactRepository contactRepository,
        IPennylaneApiGroupService pennylaneServiceApi,
        IPennylaneRepository pennylaneRepository,
        IOptions<PennylaneServiceOptions> options
    )
    {
        _logger = logger;
        _roleRepository = roleRepository;
        _accountRepository = accountRepository;
        _contactRepository = contactRepository;
        _pennylaneServiceApi = pennylaneServiceApi;
        _pennylaneRepository = pennylaneRepository;
        _authenticationMode = options.Value.AuthenticationMode;
        _useUserIdForGrantAccess = options.Value.UseUserIdForGrantAccess;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError(
                "[ERREUR] Impossible de traiter l'événement car le message est null - RoleCreatedEventHandler");
            return;
        }

        var roleEvent = JsonDefaults.TryDeserialize<RoleCreatedEvent>(message);

        if (roleEvent?.Data == null || roleEvent.Data.ContactId <= 0 || roleEvent.Data.AccountId < -1)
        {
            _logger.LogError(
                "[ERREUR] Format de données invalide pour le role du contact: {ContactId} sur l'account: {AccountId}",
                roleEvent?.Data?.ContactId, roleEvent?.Data?.AccountId);
            return;
        }

        var pulseRole = roleEvent.Data.MapToRoleEntity();
        await _roleRepository.AddRoleAsync(pulseRole);

        await HandlePennylaneRole(roleEvent.Data);

        _logger.LogInformation(
            "Consommation de l'event type: {EventType}, contactId: {ContactId}, accountId: {AccountId}",
            roleEvent.EventType,
            roleEvent.Data.ContactId,
            roleEvent.Data.AccountId);
        
        if (roleEvent.Data.IncludePennylaneAccess != true) return;

        var rydgeAccount = await _accountRepository.GetAccountByNumberOrIdAsync(pulseRole.AccountId.ToString());
        if (rydgeAccount is null)
        {
            _logger.LogWarning("Can't add role on account {AccountId}. It doesn't exist on Rydge", pulseRole.AccountId);
            return;
        }

        var pnlAccount = await _pennylaneServiceApi.TryGetCompanyByIdAsync(rydgeAccount.AccountNumber);

        if (pnlAccount is null)
        {
            _logger.LogWarning("Can't add role on account {AccountId}. It doesn't exist on Pennylane",
                rydgeAccount.AccountNumber);
            return;
        }

        var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: pulseRole.ContactId);

        if (contact is null)
        {
            _logger.LogWarning("Can't add role on contact {ContactId}. It doesn't exist on Rydge", pulseRole.ContactId);
            return;
        }

        if (!contact.Type?.Equals("Collaborator", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            _logger.LogWarning("Can't add role on contact {ContactId}. It is not a collaborator ({ContactType}).",
                pulseRole.ContactId, contact.Type);
            return;
        }

        var pnlUser = await _pennylaneServiceApi.TryGetUserAsync(contact.Email);
        var userId = pnlUser?.Id;

        if (pnlUser is null)
        {
            userId = await _pennylaneServiceApi.PostUsersAsync(contact.ToPennylaneContact(_authenticationMode));
        }

        var pnlCompanyUser = await _pennylaneServiceApi.TryGetCompanyUserAsync(contact.Email, pnlAccount.Id);

        if (pnlCompanyUser is null)
        {
            var grantUserAccess = new GrantUserAccess
            {
                Role = PennylaneRoles.Accountant.GetDescription()
            };

            // Feature flag logic: if UseUserIdForGrantAccess is true, use user_id (legacy), otherwise use user_email (new)
            if (_useUserIdForGrantAccess)
            {
                grantUserAccess.UserId = userId;
            }
            else
            {
                grantUserAccess.UserEmail = contact.Email;
            }

            await _pennylaneServiceApi.TryGrantCompanyUserAccessAsync(pnlAccount.Id, grantUserAccess);
            return;
        }

        _logger.LogInformation("Event successfully consumed {Event}", roleEvent);
    }

    private async Task HandlePennylaneRole(RoleCreatedEventData data)
    {
        var pnlRole = data.MapToPnlRoleEntity();

        if (pnlRole.Role != null && PnlRoles.IsValidRoleCode(pnlRole.Role))
        {
            var existingPennylaneRole = await _pennylaneRepository.FetchPennylaneRoleByAccountAndContactIdAsync(pnlRole.AccountId, pnlRole.ContactId);

            if (existingPennylaneRole == null)
            {
                await _pennylaneRepository.InsertPennylaneRoleAsync(pnlRole);
            }
            else if (!existingPennylaneRole.Role.Equals(pnlRole.Role))
            {
                await _pennylaneRepository.DeletePennylaneRoleAsync(pnlRole);
                await _pennylaneRepository.InsertPennylaneRoleAsync(pnlRole);
            }
        }
    }
}