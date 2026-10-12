// <copyright file="MapEventDataToModel.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Models.Contacts;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Application.Mappers;

public static class MapEventDataToModel
{
    public static Contact ContactStateEventDataToContactModel(this ContactStateEventData contactStateEventData)
    {
        return new Contact()
        {
            ContactGlobalUniqueId = contactStateEventData.ContactGlobalUniqueId,
            ContactId = contactStateEventData.ContactId,
            Email = contactStateEventData.Email,
            Type = contactStateEventData.Type,
            FirstName = contactStateEventData.FirstName,
            LastName = contactStateEventData.LastName
        };
    }
}