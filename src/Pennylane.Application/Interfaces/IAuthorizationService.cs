// <copyright file="IAuthorizationService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.EmailRecap;

namespace Pennylane.Application.Interfaces;

public interface IAuthorizationService
{
    Task<IReadOnlyList<AuthorizedContact>> GetContactsWithGedPermissionAsync(int accountId);
    Task<Dictionary<int, IReadOnlyList<AuthorizedContact>>> GetContactsForAccountsAsync(IEnumerable<int> accountIds);
}
