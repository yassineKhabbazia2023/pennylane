// <copyright file="CompanyDirectorySchedulerOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

/// <summary>
/// Configuration options for the scheduled company directory refresh job.
/// </summary>
public class CompanyDirectorySchedulerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string CompanyDirectoryScheduler = "CompanyDirectorySchedulerSettings";

    /// <summary>
    /// Gets or sets a value indicating whether the scheduled refresh is enabled.
    /// </summary>
    [Required]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the cron expression for the refresh schedule.
    /// Default: "30 6 * * *" (6:30 AM daily).
    /// </summary>
    [Required]
    public string RefreshCron { get; set; } = "30 6 * * *";
}
