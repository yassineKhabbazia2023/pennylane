// <copyright file="DocumentSyncOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

/// <summary>
/// Configuration options for document synchronization from Pennylane.
/// </summary>
public class DocumentSyncOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string DocumentSync = "DocumentSyncSettings";

    /// <summary>
    /// Gets or sets the list of Pennylane folder paths to synchronize.
    /// Supports * wildcard to match any characters in path segments.
    /// Example: "/Dossier annuel/Exercice */FEC" matches all fiscal year FEC folders.
    /// </summary>
    [Required(ErrorMessage = "SyncPaths is required and must contain at least one path.")]
    [MinLength(1, ErrorMessage = "SyncPaths must contain at least one path.")]
    public List<string> SyncPaths { get; set; } = new();

    /// <summary>
    /// Returns the configured sync paths.
    /// Paths are used for pattern matching with * wildcard support.
    /// </summary>
    /// <returns>List of configured paths for filtering.</returns>
    public List<string> GetExpandedPaths()
    {
        return SyncPaths;
    }
}
