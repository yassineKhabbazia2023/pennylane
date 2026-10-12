// <copyright file="IPennylaneGedPermissionRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.Permissions;

namespace Pennylane.Application.Interfaces;

public interface IPennylaneGedPermissionRepository
{
    Task<bool> AddPermissionAsync(int contactId, int accountId);
    Task<bool> RemovePermissionAsync(int contactId, int accountId);
    Task<bool> RemoveAllPermissionsForContactAsync(int contactId);
    Task<IReadOnlyList<PennylaneGedPermissionWithContact>> GetPermissionsWithContactInfoAsync(IEnumerable<int> accountIds);
}
