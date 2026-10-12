// <copyright file="DocumentSyncSchedulerResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models;

/// <summary>
/// Represents the result of a scheduled document synchronization job.
/// </summary>
/// <param name="TotalCompanies">Total number of companies (with active subscriptions) processed.</param>
/// <param name="SyncMessagesPublished">Number of sync messages successfully published.</param>
/// <param name="Errors">Number of errors encountered.</param>
/// <param name="ErrorMessages">List of error messages.</param>
/// <param name="StartedAt">Job start time.</param>
/// <param name="CompletedAt">Job completion time.</param>
public record DocumentSyncSchedulerResult(
    int TotalCompanies,
    int SyncMessagesPublished,
    int Errors,
    List<string> ErrorMessages,
    DateTime StartedAt,
    DateTime CompletedAt)
{
    /// <summary>
    /// Gets the total duration of the sync job.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}
