// <copyright file="ContactRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Mappers;
using Application.Models.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pennylane.Domain.Entities;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Application.Repository;

/// <summary>
/// ContactsRepository.
/// </summary>
/// <param name="dbContext">dbContext.</param>
public class ContactRepository(PennylaneContext PennylaneContext, ILogger<ContactRepository> logger) : IContactRepository
{

    public async Task<bool> AddContactAsync(Contact contact)
    {
        return await TryReposAction<Contact>(TryAddContactAsync, contact);
    }

    public async Task<bool> UpdateContactAsync(Contact contact)
    {
        return await TryReposAction<Contact>(TryUpdateContactAsync, contact);
    }

    public async Task<bool> DeleteContactByIdAsync(int? contactId)
    {
        return await TryReposAction<int?>(TryDeleteContactAsync, contactId);
    }

    public async Task<bool> DoesContactExistByEmailOrIdAsync(string? email = null, int? contactId = null)
    {
        if (string.IsNullOrEmpty(email) && (contactId == null || contactId == default(int)))
        {
            return false;
        }

        var query = PennylaneContext.ContactEntity.AsNoTracking();
        if (!string.IsNullOrEmpty(email))
        {
            return await query.AnyAsync(x => x.Email == email);
        }

        if (contactId != null && contactId != default(int))
        {
            return await query.AnyAsync(c => c.ContactId == contactId);
        }
        return false;
    }

    public async Task<Contact?> GetContactByEmailOrIdAsync(string? email = null, int? contactId = null)
    {
        if (string.IsNullOrEmpty(email) && (contactId == null || contactId == default(int)))
        {
            return null;
        }
        var query = PennylaneContext.ContactEntity.AsNoTracking();

        if (!string.IsNullOrEmpty(email))
        {
            var contactEntity = await query.FirstOrDefaultAsync(c => c.Email == email);
            return contactEntity.MapContactEntityToModel();
        }

        if (contactId != null && contactId != default(int))
        {
            var contactEntity = await query.FirstOrDefaultAsync(c => c.ContactId == contactId);
            return contactEntity.MapContactEntityToModel();
        }
        return null;
    }



    private async Task<bool> TryAddContactAsync(Contact contact)
    {
        bool contactExisted = await DoesContactExistByEmailOrIdAsync(contactId: contact.ContactId);
        if (contactExisted)
        {
            logger.LogError($"[Method]: ${nameof(AddContactAsync)}; [Error]: Contact {contact.Email} already existed!");
            return false;
        }
        ContactEntity contactEntity = contact.MapContactModelToEntity();
        await PennylaneContext.ContactEntity.AddAsync(contactEntity);
        await PennylaneContext.SaveChangesAsync();
        return true;
    }

    private async Task<bool> TryUpdateContactAsync(Contact contact)
    {
        bool contactExisted = await DoesContactExistByEmailOrIdAsync(contactId: contact.ContactId);
        if (contactExisted)
        {
            var trackedEntity = await PennylaneContext.ContactEntity.FirstOrDefaultAsync(c => c.ContactId == contact.ContactId);
            if (trackedEntity != null)
            {
                PennylaneContext.Entry(trackedEntity).State = EntityState.Detached;
            }

            ContactEntity contactEntity = contact.MapContactModelToEntity();
            PennylaneContext.ContactEntity.Update(contactEntity);
            await PennylaneContext.SaveChangesAsync();
            return true;
        }
        logger.LogError($"[Method]: ${nameof(TryUpdateContactAsync)}; [Error]: Contact with Email {contact.Email} Does not exists!");
        return false;
    }

    private async Task<bool> TryDeleteContactAsync(int? contactId)
    {
        if (contactId == null || contactId == default(int))
        {
            logger.LogError($"[Method]: ${nameof(DeleteContactByIdAsync)}; [Error]: ContactId {contactId} Input is not valid");
            return false;
        }

        bool contactExisted = await DoesContactExistByEmailOrIdAsync(contactId: contactId);
        if (contactExisted)
        {
            var roles = await PennylaneContext.RoleEntity.Where(r => r.ContactId == contactId).ToListAsync();
            if (roles.Any())
            {
                PennylaneContext.RoleEntity.RemoveRange(roles);
            }

            ContactEntity contactEntity = await PennylaneContext.ContactEntity
           .FirstAsync(c => c.ContactId == contactId);

            PennylaneContext.ContactEntity.Remove(contactEntity);
            await PennylaneContext.SaveChangesAsync();
            return true;

        }
        logger.LogError($"[Method]: ${nameof(DeleteContactByIdAsync)}; [Error]: ContactId {contactId} Does not exists in contact.Contact");
        return false;
    }

    private async Task<bool> TryReposAction<T>(Func<T, Task<bool>> functionExecution, T t)
    {
        try
        {
            return await functionExecution(t);
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError($"[Method]: {nameof(functionExecution)}; [Error]: {dbEx.Message}");
            return false;
        }
        catch (InvalidOperationException ioEx)
        {
            logger.LogError($"[Method]: {nameof(functionExecution)}; [Error]: {ioEx.Message}");
            return false;
        }
    }

    public async Task<Contact?> GetContactByGlobalUniqueId(Guid contactId)
    {
        var query = PennylaneContext.ContactEntity.AsNoTracking();
        var contactEntity = await query.FirstOrDefaultAsync(c => c.ContactGlobalUniqueId == contactId);
        
        if (contactEntity == null)
        {
            return null;
        }

        return contactEntity.MapContactEntityToModel();
    }
    public async Task<IReadOnlyCollection<Contact>> GetContacts(IList<int> contactIds)
    {
        if (contactIds == null || !contactIds.Any())
        {
            return Array.Empty<Contact>();
        }

        var contactEntities = await PennylaneContext.ContactEntity
            .AsNoTracking()
            .Where(c => contactIds.Contains(c.ContactId))
            .ToListAsync();

        return contactEntities
            .Select(ce => ce.MapContactEntityToModel())
            .ToList();
    }
}