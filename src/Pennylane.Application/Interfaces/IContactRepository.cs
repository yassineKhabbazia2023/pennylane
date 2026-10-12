// <copyright file="IContactRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Models.Contacts;

namespace Application.Interfaces;

public interface IContactRepository
{

    Task<bool> AddContactAsync(Contact contact);

    Task<bool> UpdateContactAsync(Contact contact);

    Task<bool> DeleteContactByIdAsync(int? contactId);

    Task<Contact?> GetContactByEmailOrIdAsync(string? email = null, int? contactId = null);

    Task<bool> DoesContactExistByEmailOrIdAsync(string? email = null, int? contactId = null);

    Task<Contact?> GetContactByGlobalUniqueId(Guid contactId);
}
