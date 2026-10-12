// <copyright file="ContactService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Consts;
using Application.Interfaces;
using Application.Mappers;
using Application.Models.Contacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Application.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository contactRepository;
    private readonly ILogger<ContactService> logger;
    private readonly IPennylaneApiGroupService pennylaneServiceApi;
    private readonly PennylaneServiceOptions pennylaneServiceOptions;

    public ContactService(ILogger<ContactService> logger,
        IContactRepository contactRepository,
        IPennylaneApiGroupService pennylaneServiceApi,
        IOptions<PennylaneServiceOptions> options)
    {
        this.contactRepository = contactRepository;
        this.logger = logger;
        this.pennylaneServiceApi = pennylaneServiceApi;
        this.pennylaneServiceOptions = options.Value;
    }

    public async Task OnCreatedContactEventExecution(ContactStateEventData contactStateEventData)
    {
        var contactModel = contactStateEventData.ContactStateEventDataToContactModel();

        var isInserted = await this.contactRepository.AddContactAsync(contactModel);
        if (!isInserted)
        {
            logger.LogError($"[Method]: {nameof(OnCreatedContactEventExecution)} ; [Error]: Failed to insert instance contact.Contact in Database.");
        }
    }

    public async Task OnUpdatedContactEventExecution(ContactStateEventData contactStateEventData)
    {

        await HandleUpdatePennylaneUserEmailAsync(contactStateEventData);

        var contactModel = contactStateEventData.ContactStateEventDataToContactModel();

        var isUpdated = await this.contactRepository.UpdateContactAsync(contactModel);
        if (!isUpdated)
        {
            logger.LogError($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Error]: Failed to update instance contact.Contact in Database.");
        }
    }

    public async Task OnRemovedContactEventExecution(ContactRemovedEventData contactRemovedData)
    {
        bool isRegisteredInDb = false;


        int contactId = contactRemovedData.ContactId;
        Contact? contact = await this.contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);

        if (contact == null)
        {
            logger.LogError($"[Method]: {nameof(OnRemovedContactEventExecution)} ; [Error]: Failed to Deleted instance contact.Contact with contactId {contactId} because contact does not exist!");
        }
        else
        {
            isRegisteredInDb = await this.contactRepository.DeleteContactByIdAsync(contactRemovedData.ContactId);
            if (!isRegisteredInDb)
            {
                logger.LogError($"[Method]: {nameof(OnRemovedContactEventExecution)} ; [Error]: Something went wrong while Deleting Contact {contact.ContactId}");
            }
        }
    }

    private static bool ShouldUpdatePennylaneUserEmail(ContactStateEventData contactStateEventData, Contact? contact)
    {
        return contact is not null &&
               !contact.Email.Equals(contactStateEventData.Email, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdatePennylaneUserEmailAsync(PennylaneUsers user, ContactStateEventData contactStateEventData)
    {
        try
        {
            await pennylaneServiceApi.UpdateUserEmailAsync(user.PennylaneUserToPennylaneContact(), contactStateEventData.Email);
            logger.LogInformation($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Info]: Successfully updated user email in Pennylane API for user with ID: {user.Id} and new email: {contactStateEventData.Email}");
        }
        catch (Exception ex)
        {
            logger.LogError($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Error]: Failed to update user email in Pennylane API: {ex.Message}");
        }
    }

    private async Task HandleUpdatePennylaneUserEmailAsync(ContactStateEventData contactStateEventData)
    {
        if (!pennylaneServiceOptions.EnableUpdatePennylaneUsersEmails)
        {
            logger.LogInformation($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Info]: Pennylane user email update is disabled. Skipping the process of updating user email for contact with Email: {contactStateEventData.Email}.");
            return;
        }

        if (contactStateEventData.ContactGlobalUniqueId is null)
        {
            logger.LogWarning($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Warning]: ContactGlobalUniqueId is null for contact with Email: {contactStateEventData.Email}. Skipping the process of updating user email.");
            return;
        }

        Contact? existingContact = null;
        try
        {
            existingContact = await this.contactRepository.GetContactByGlobalUniqueId(contactStateEventData.ContactGlobalUniqueId.Value!);
        }
        catch (Exception ex)
        {
            logger.LogError($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Error]: Failed to retrieve contact by email: {contactStateEventData.Email}. Exception: {ex.Message}");
            return;
        }

        if (!ShouldUpdatePennylaneUserEmail(contactStateEventData, existingContact) )
        {
            logger.LogInformation($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Info]: No need to update Pennylane user email for contact with Email: {contactStateEventData.Email}. Existing contact email is the same.");
            return;
        }

        PennylaneUsers? user = await pennylaneServiceApi.TryGetUserAsync(existingContact!.Email);
        if (user == null)
        {
            logger.LogWarning($"[Method]: {nameof(OnUpdatedContactEventExecution)} ; [Warning]: No user found in Pennylane for email: {existingContact.Email}. Skipping the process of updating user email.");
            return;
        }

        await UpdatePennylaneUserEmailAsync(user, contactStateEventData);
    }
}
