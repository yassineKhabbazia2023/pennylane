// <copyright file="IPennylaneService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models;
using Pennylane.Application.Models.Pennylane;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.WebApi.Models;

namespace Pennylane.Application.Interfaces;

public interface IPennylaneService
{
    Task<PnlRole?> GetPennylaneRoleAsync(int contactId, int accountId);

    Task DeletePennylaneRoleAsync(int accountId, int contactId);

    Task InsertPennylaneRoleAsync(PnlRole pnlRole);

    Task<PennylaneEmails?> GetPennylaneEmailsAsync(string accountNumber);

    Task<PennylaneCompany> GetCompanyByIdAsync(string companyId);

    Task<int?> GetPennylaneInternalIdByAccountNumberAsync(string firmId, string accountNumber, bool forceRefresh = false);

    Task<PennylaneCompany> RefreshCompanyFromApiAsync(string companyId);

    Task<(PennylaneCompany? Company, bool AlreadyExists, bool CreatedOnDefault, bool RequiresFirmAssignment, bool RequiresFirmTransfer)> CreateCompanyOnlyAsync(CreateCompanyRequest request);

    Task<List<UserAdditionResult>> AddUsersToCompanyAsync(string accountNumber, IReadOnlyList<int> contactIds);

    Task<(string? AccountingManagerId, string? AccountingSupervisorId, string? AccountantId)> CreateCollaboratorsAndGetPennylaneIdsAsync(IReadOnlyList<ContactFunctions>? contactFunctions, string? hubName = null);

    Task<PennylaneCompany?> GetCompanyByAccountIdAsync(int accountId);

    Task<(PennylaneUsers? User, bool AlreadyExistsInCompany)> GetPennylaneCompanyUser(string pennylaneCompanyId, int contactId);

    Task<(string? UserId, bool AlreadyExists)> CreatePennylaneUserAsync(int contactId);

    Task<bool> InsertUserPennylaneRole(PnlRole pnlRole, string pennylaneUserId, string pennylaneCompanyId);

    Task RevokeUserAccessAsync(string pennylaneCompanyId, string pennylaneUserId);
}
