// <copyright file="SubscriptionRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Offers;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class SubscriptionRepository : ISubscriptionRepository
{
    private PennylaneContext _context;

    public SubscriptionRepository(PennylaneContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task DeleteSubcriptionAsync(SubscriptionEntity subscription)
    {
        _context.SubscriptionEntity.Remove(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task<SubscriptionEntity?> GetSubscriptionByOfferAndAccountAsync(string offerName, int accountId)
    {
        return await _context.SubscriptionEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OfferName == offerName && s.AccountId == accountId);
    }

    public async Task SaveSubcriptionAsync(SubscriptionEntity subscription)
    {
        await _context.SubscriptionEntity.AddAsync(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task<List<(int AccountId, string AccountNumber)>> GetAllActiveSubscriptionsWithAccountsAsync(string offerName)
    {
        return await _context.SubscriptionEntity
            .AsNoTracking()
            .Where(s => s.OfferName == offerName)
            .Join(
                _context.AccountEntity,
                s => s.AccountId,
                a => a.AccountId,
                (s, a) => new { a.AccountId, a.AccountNumber })
            .Select(x => ValueTuple.Create(x.AccountId, x.AccountNumber))
            .ToListAsync();
    }

    public async Task<List<(int AccountId, string AccountNumber)>> GetActiveSubscriptionsWithPennylaneCompaniesAsync()
    {
        return await _context.SubscriptionEntity
            .AsNoTracking()
            .Where(s => s.OfferName == OfferCodes.Pennylane && s.AccountId.HasValue)
            .Join(
                _context.AccountEntity,
                s => s.AccountId!.Value,
                a => a.AccountId,
                (s, a) => new { a.AccountId, a.AccountNumber })
            .Join(
                _context.CompanyEntity,
                acc => acc.AccountNumber,
                comp => comp.Id,
                (acc, comp) => new { acc.AccountId, acc.AccountNumber })
            .Select(x => ValueTuple.Create(x.AccountId, x.AccountNumber))
            .ToListAsync();
    }
}
