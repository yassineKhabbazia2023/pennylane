// <copyright file="PennylaneGedPermissionHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;

namespace Pennylane.Infrastructure.Handlers.Permissions;

/// <summary>
/// Handles GED permission events for Pennylane.
/// Supports multiple permission codes that share the same storage logic.
/// </summary>
public class PennylaneGedPermissionHandler(
    IPennylaneGedPermissionRepository repository,
    ILogger<PennylaneGedPermissionHandler> logger) : IPermissionHandler
{
    /// <summary>
    /// Permission codes supported by this handler.
    /// All codes share the same PennylaneGedPermissions table.
    /// </summary>
    public IReadOnlyCollection<string> PermissionCodes => new[] { "CLGEDPEN01" };

    public async Task HandlePermissionGrantedAsync(string permissionCode, int contactId, int accountId)
    {
        logger.LogInformation(
            "[PennylaneGedPermissionHandler] Granting permission {PermissionCode} for Contact {ContactId} on Account {AccountId}",
            permissionCode, contactId, accountId);

        await repository.AddPermissionAsync(contactId, accountId);
    }

    public async Task HandlePermissionRevokedAsync(string permissionCode, int contactId, int accountId)
    {
        logger.LogInformation(
            "[PennylaneGedPermissionHandler] Revoking permission {PermissionCode} for Contact {ContactId} on Account {AccountId}",
            permissionCode, contactId, accountId);

        await repository.RemovePermissionAsync(contactId, accountId);
    }
}
