// <copyright file="IPennylaneRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces
{
    public interface IPennylaneRepository
    {
        Task InsertPennylaneRoleAsync(PnlRoleEntity pennylaneRoleEntity);

        Task<PnlRoleEntity?> FetchPennylaneRoleByAccountAndContactIdAsync(int accountId, int contactId);

        Task DeletePennylaneRoleAsync(PnlRoleEntity pennylaneRoleEntity);

        Task<bool> PennylaneRoleExistsAsync(string accountNumber, string email);


        Task UpsertPennylaneRoleByEmailAndAccountNumberAsync(string email, string accountNumber, string role);
    }
}
