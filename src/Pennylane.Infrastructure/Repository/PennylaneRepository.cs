// <copyright file="PennylaneRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class PennylaneRepository(PennylaneContext PennylaneContext) : IPennylaneRepository
{
    public async Task DeletePennylaneRoleAsync(PnlRoleEntity pennylaneRoleEntity)
    {
        await PennylaneContext.PnlRoleEntity
            .Where(role => role.AccountId == pennylaneRoleEntity.AccountId && role.ContactId == pennylaneRoleEntity.ContactId)
            .ExecuteDeleteAsync();
    }

    public async Task<PnlRoleEntity?> FetchPennylaneRoleByAccountAndContactIdAsync(int accountId, int contactId)
    {
        return await PennylaneContext.PnlRoleEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.AccountId == accountId && role.ContactId == contactId);
    }

    public async Task InsertPennylaneRoleAsync(PnlRoleEntity pennylaneRoleEntity)
    {
        await PennylaneContext.PnlRoleEntity.AddAsync(pennylaneRoleEntity);
        await PennylaneContext.SaveChangesAsync();
    }

    public async Task<bool> PennylaneRoleExistsAsync(string accountNumber, string email)
    {
        return await PennylaneContext.PnlRoleEntity
            .AnyAsync(role => role.Account.AccountNumber == accountNumber
                           && role.Contact.Email == email);
    }


    public async Task UpsertPennylaneRoleByEmailAndAccountNumberAsync(string email, string accountNumber, string role)
    {
        // Resolve IDs via scalar queries to avoid an UPDATE...JOIN
        // which can fail on SQL Server when using ExecuteUpdateAsync with navigation properties.
        var contactId = await PennylaneContext.ContactEntity
            .Where(c => c.Email == email)
            .Select(c => (int?)c.ContactId)
            .FirstOrDefaultAsync();

        var accountId = await PennylaneContext.AccountEntity
            .Where(a => a.AccountNumber == accountNumber)
            .Select(a => (int?)a.AccountId)
            .FirstOrDefaultAsync();

        if (contactId is null || accountId is null) return;

        // UPDATE on primary keys only — no JOIN in the DML
        var updated = await PennylaneContext.PnlRoleEntity
            .Where(r => r.ContactId == contactId.Value && r.AccountId == accountId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Role, role));

        if (updated > 0) return;

        // INSERT if no row was updated
        await PennylaneContext.PnlRoleEntity.AddAsync(new PnlRoleEntity
        {
            ContactId = contactId.Value,
            AccountId = accountId.Value,
            Role = role
        });
        await PennylaneContext.SaveChangesAsync();
    }
}

