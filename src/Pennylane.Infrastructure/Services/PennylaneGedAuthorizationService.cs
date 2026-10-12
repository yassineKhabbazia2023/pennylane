// <copyright file="PennylaneGedAuthorizationService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.EmailRecap;

namespace Pennylane.Infrastructure.Services;

public class PennylaneGedAuthorizationService(
    IPennylaneGedPermissionRepository gedPermissionRepository,
    ILogger<PennylaneGedAuthorizationService> logger) : IAuthorizationService
{
    public async Task<IReadOnlyList<AuthorizedContact>> GetContactsWithGedPermissionAsync(int accountId)
    {
        var permissions = await gedPermissionRepository.GetPermissionsWithContactInfoAsync(new[] { accountId });

        return permissions
            .Where(p => string.Equals(p.ContactType, "Customer", StringComparison.OrdinalIgnoreCase))
            .Select(p => new AuthorizedContact(p.ContactId, p.Email, p.FirstName, p.LastName, p.ContactType))
            .ToList();
    }

    public async Task<Dictionary<int, IReadOnlyList<AuthorizedContact>>> GetContactsForAccountsAsync(
        IEnumerable<int> accountIds)
    {
        var accountIdList = accountIds.ToList();

        if (accountIdList.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<AuthorizedContact>>();
        }

        logger.LogInformation(
            "[PennylaneGedAuthorizationService] Getting contacts for {AccountCount} accounts",
            accountIdList.Count);

        var permissions = await gedPermissionRepository.GetPermissionsWithContactInfoAsync(accountIdList);

        var result = permissions
            .Where(p => string.Equals(p.ContactType, "Customer", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.AccountId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AuthorizedContact>)g
                    .Select(p => new AuthorizedContact(p.ContactId, p.Email, p.FirstName, p.LastName, p.ContactType))
                    .ToList());

        logger.LogInformation(
            "[PennylaneGedAuthorizationService] Found contacts for {ResultCount} accounts out of {RequestedCount}",
            result.Count, accountIdList.Count);

        return result;
    }
}
