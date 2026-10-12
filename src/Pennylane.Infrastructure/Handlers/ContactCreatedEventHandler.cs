// <copyright file="ContactEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;

public class ContactCreatedEventHandler : IEventHandler
{
    private readonly ILogger<ContactCreatedEventHandler> logger;
    private readonly IContactService contactService;

    public ContactCreatedEventHandler(
        ILogger<ContactCreatedEventHandler> logger,
        IContactService contactService
        )
    {
        this.logger = logger;
        this.contactService = contactService;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactCreatedEvent)} [ERROR]: Message Body Is Empty");
            return;
        }

        var contactEvent = JsonDefaults.TryDeserialize<ContactCreatedEvent>(message);
        logger.LogInformation($"Consommation de l'event type: {contactEvent?.EventType}",
            contactEvent?.EventType,
            contactEvent?.Data?.ContactId);

        if (contactEvent?.Data == null)
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactCreatedEvent)} [ERROR]: Data is Null Or Empty");
            return;
        }

        try
        {
            await contactService.OnCreatedContactEventExecution(contactEvent.Data);
        }
        catch (Exception e)
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactCreatedEvent)} [ERROR]: {e.Message}");
            return;
        }

        logger.LogInformation($"[EVENT-TYPE]: {nameof(ContactCreatedEvent)} [EMAIL]: {contactEvent.Data.Email} Creation Succeeded");
    }
}