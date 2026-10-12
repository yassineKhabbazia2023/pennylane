// <copyright file="DocumentSyncSchedulerOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

/// <summary>
/// Configuration options for the scheduled document synchronization job.
/// </summary>
public class DocumentSyncSchedulerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string DocumentSyncScheduler = "DocumentSyncSchedulerSettings";

    /// <summary>
    /// Gets or sets a value indicating whether the scheduled sync is enabled.
    /// </summary>
    [Required]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the cron expression for the sync schedule.
    /// Default: "30 7 * * *" (7:30 AM daily).
    /// </summary>
    [Required]
    public string SyncCron { get; set; } = "30 7 * * *";

    /// <summary>
    /// Gets or sets the number of companies to process per batch.
    /// </summary>
    [Range(10, 500)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the delay in seconds between batches.
    /// </summary>
    [Range(0, 60)]
    public int BatchDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to force sync ignoring the threshold.
    /// When true, bypasses the 60-minute sync threshold for scheduled syncs.
    /// </summary>
    public bool ForceSyncIgnoreThreshold { get; set; } = true;
}
