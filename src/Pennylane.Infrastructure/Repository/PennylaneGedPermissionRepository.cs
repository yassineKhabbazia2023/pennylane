// <copyright file="PennylaneGedPermissionRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Permissions;
using Pennylane.Domain.Entities.Permissions;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class PennylaneGedPermissionRepository(
    PennylaneContext context,
    ILogger<PennylaneGedPermissionRepository> logger) : IPennylaneGedPermissionRepository
{
    public async Task<bool> AddPermissionAsync(int contactId, int accountId)
    {
        var exists = await context.PennylaneGedPermissionEntity
            .AnyAsync(p => p.ContactId == contactId && p.AccountId == accountId);

        if (exists)
        {
            logger.LogDebug(
                "[PennylaneGedPermissionRepository] Permission already exists for Contact {ContactId} on Account {AccountId}",
                contactId, accountId);
            return true;
        }

        var permission = new PennylaneGedPermissionEntity
        {
            ContactId = contactId,
            AccountId = accountId,
            GrantedAt = DateTime.UtcNow
        };

        context.PennylaneGedPermissionEntity.Add(permission);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "[PennylaneGedPermissionRepository] Added permission for Contact {ContactId} on Account {AccountId}",
            contactId, accountId);

        return true;
    }

    public async Task<bool> RemovePermissionAsync(int contactId, int accountId)
    {
        var deleted = await context.PennylaneGedPermissionEntity
            .Where(p => p.ContactId == contactId && p.AccountId == accountId)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            logger.LogInformation(
                "[PennylaneGedPermissionRepository] Removed permission for Contact {ContactId} on Account {AccountId}",
                contactId, accountId);
        }

        return deleted > 0;
    }

    public async Task<bool> RemoveAllPermissionsForContactAsync(int contactId)
    {
        var deleted = await context.PennylaneGedPermissionEntity
            .Where(p => p.ContactId == contactId)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            logger.LogInformation(
                "[PennylaneGedPermissionRepository] Removed {Count} permissions for Contact {ContactId}",
                deleted, contactId);
        }

        return deleted > 0;
    }

    public async Task<IReadOnlyList<PennylaneGedPermissionWithContact>> GetPermissionsWithContactInfoAsync(
        IEnumerable<int> accountIds)
    {
        var accountIdList = accountIds.ToList();

        if (accountIdList.Count == 0)
        {
            return Array.Empty<PennylaneGedPermissionWithContact>();
        }

        var result = await (
            from permission in context.PennylaneGedPermissionEntity
            join contact in context.ContactEntity on permission.ContactId equals contact.ContactId
            where accountIdList.Contains(permission.AccountId)
            select new PennylaneGedPermissionWithContact(
                permission.ContactId,
                permission.AccountId,
                contact.Email,
                contact.FirstName,
                contact.LastName,
                contact.Type))
            .ToListAsync();

        return result;
    }
}
