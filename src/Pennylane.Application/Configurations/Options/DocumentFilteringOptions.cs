// <copyright file="DocumentFilteringOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Configurations.Options;

/// <summary>
/// Configuration options controlling which documents are filtered out of the
/// "Documents RYDGE Conseil" API responses and the Pennylane synchronization.
/// </summary>
public class DocumentFilteringOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string DocumentFiltering = "DocumentFilteringSettings";

    /// <summary>
    /// Gets or sets the file extensions (including the leading dot, e.g. ".EDI")
    /// whose documents must never be exposed to clients nor persisted during sync.
    /// Matching is case-insensitive and based on the document name suffix.
    /// </summary>
    public List<string> ExcludedExtensions { get; set; } = new();
}
