// <copyright file="IAccountRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.Entities;

namespace Application.Interfaces;

public interface IAccountRepository
{

    Task AddAccountAsync(AccountEntity account);

    Task<AccountEntity?> GetAccountByNumberOrIdAsync(string accountNumber);

    Task<IReadOnlyList<AccountEntity>> GetAccountsByNumbersAsync(IEnumerable<string> accountNumbers);

    Task RemoveAccountAsync(AccountEntity account);

    Task UpdateAccountAsync(AccountEntity account);
}
