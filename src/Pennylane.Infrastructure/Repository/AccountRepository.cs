// <copyright file="AccountRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Exceptions;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Infrastructure.Repository;
/// <summary>
/// ContactsRepository.
/// </summary>
/// <param name="dbContext">dbContext.</param>
public class AccountRepository(PennylaneContext PennylaneContext) : IAccountRepository
{
    public async Task AddAccountAsync(AccountEntity account)
    {
        PennylaneContext.Add(account);

        await SaveChangesAsync();
    }

    public async Task<AccountEntity?> GetAccountByNumberOrIdAsync(string accountNumberOrId)
    {
        int accountId = 0;
        int.TryParse(accountNumberOrId, out accountId);

        return await PennylaneContext.AccountEntity.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId.Equals(accountId) || a.AccountNumber.Equals(accountNumberOrId));
    }

    public async Task<IReadOnlyList<AccountEntity>> GetAccountsByNumbersAsync(IEnumerable<string> accountNumbers)
    {
        var numbersList = accountNumbers.ToList();
        return await PennylaneContext.AccountEntity
            .AsNoTracking()
            .Where(a => numbersList.Contains(a.AccountNumber))
            .ToListAsync();
    }

    public async Task RemoveAccountAsync(AccountEntity account)
    {
        PennylaneContext.AccountEntity.Remove(account);

        await SaveChangesAsync();
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            await PennylaneContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DbOperationException("Something went wrong while saving data", ex.InnerException);
        }
    }

    public async Task UpdateAccountAsync(AccountEntity account)
    {
        PennylaneContext.AccountEntity.Update(account);

        await SaveChangesAsync();
    }
}

