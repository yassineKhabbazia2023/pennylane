// <copyright file="MapContacts.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Models.Contacts;
using Pennylane.Domain.Entities;

namespace Application.Mappers;

public static class MapContacts
{
    public static ContactEntity? MapContactModelToEntity(this Contact? contact)
    {
        if (contact == default(Contact))
        {
            return null;
        }
        return new ContactEntity()
        {
            ContactGlobalUniqueId = contact.ContactGlobalUniqueId,
            ContactId = contact.ContactId,
            Email = contact.Email,
            Type = contact.Type,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
        };
    }

    public static Contact? MapContactEntityToModel(this ContactEntity? contact)
    {
        if (contact == default(ContactEntity))
        {
            return null;
        }
        return new Contact()
        {
            ContactGlobalUniqueId = contact.ContactGlobalUniqueId,
            ContactId = contact.ContactId,
            Email = contact.Email,
            Type = contact.Type,
            FirstName = contact.FirstName,
            LastName = contact.LastName
        };
    }
}
