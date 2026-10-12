// <copyright file="ContactUpdatedEventHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers;
public class ContactUpdatedEventHandler : IEventHandler
{
    private readonly ILogger<ContactUpdatedEventHandler> logger;
    private readonly IContactService contactService;

    public ContactUpdatedEventHandler(ILogger<ContactUpdatedEventHandler> logger, IContactService contactService)
    {
        this.logger = logger;
        this.contactService = contactService;
    }

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactUpdatedEvent)} [ERROR]: Message Body Is Empty");
            return;
        }

        var contactEvent = JsonDefaults.TryDeserialize<ContactUpdatedEvent>(message);

        logger.LogInformation($"Consommation de l'event type: {contactEvent?.EventType}",
            contactEvent?.EventType,
            contactEvent?.Data?.ContactId);

        if (contactEvent == null || contactEvent?.Data == null || contactEvent.Data?.ContactId <= 0)
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactUpdatedEvent)} [ERROR]: Data is Null Or Empty [Body]: {JsonSerializer.Serialize(contactEvent, JsonDefaults.Options)}");
            return;
        }

        try
        {
            await contactService.OnUpdatedContactEventExecution(contactEvent.Data);
        }

        catch (Exception e)
        {
            logger.LogError($"[EVENT-TYPE]: {nameof(ContactUpdatedEvent)} [ERROR]: {e.Message}");
            throw;
        }

        logger.LogInformation($"[EVENT-TYPE]: {nameof(ContactUpdatedEvent)} [EMAIL]: {contactEvent.Data.Email} Update Succeeded");
    }
}

