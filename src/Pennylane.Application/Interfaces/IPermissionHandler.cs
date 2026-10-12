// <copyright file="IPermissionHandler.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Interfaces;

/// <summary>
/// Interface for handling permission events.
/// A single handler can support multiple permission codes with the same logic.
/// </summary>
public interface IPermissionHandler
{
    /// <summary>
    /// Gets the collection of permission codes this handler supports.
    /// </summary>
    IReadOnlyCollection<string> PermissionCodes { get; }

    /// <summary>
    /// Handles a permission being granted.
    /// </summary>
    /// <param name="permissionCode">The permission code being granted.</param>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="accountId">The account identifier.</param>
    Task HandlePermissionGrantedAsync(string permissionCode, int contactId, int accountId);

    /// <summary>
    /// Handles a permission being revoked.
    /// </summary>
    /// <param name="permissionCode">The permission code being revoked.</param>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="accountId">The account identifier.</param>
    Task HandlePermissionRevokedAsync(string permissionCode, int contactId, int accountId);
}
