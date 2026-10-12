// <copyright file="ISubscriptionRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.Entities.Offers;

namespace Pennylane.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<SubscriptionEntity?> GetSubscriptionByOfferAndAccountAsync(string offerName, int accountId);

    Task SaveSubcriptionAsync(SubscriptionEntity subscription);

    Task DeleteSubcriptionAsync(SubscriptionEntity subscription);

    Task<List<(int AccountId, string AccountNumber)>> GetAllActiveSubscriptionsWithAccountsAsync(string offerName);

    Task<List<(int AccountId, string AccountNumber)>> GetActiveSubscriptionsWithPennylaneCompaniesAsync();
}
