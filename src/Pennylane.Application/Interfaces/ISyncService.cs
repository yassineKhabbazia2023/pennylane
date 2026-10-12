using System;
using Pennylane.Application.Models;

namespace Pennylane.Application.Interfaces;

public interface ISyncService
{
    /// <summary>
    /// Checks sync eligibility and publishes sync message if eligible.
    /// </summary>
    /// <param name="accountNumber">The account number to sync.</param>
    /// <param name="forceSync">If true, bypasses the sync threshold check.</param>
    /// <returns>True if the sync message was successfully published, false otherwise.</returns>
    Task<bool> CheckAndPushSyncMessageAsync(string accountNumber, bool forceSync = false);

    Task<DocumentSyncSchedulerResult> ScheduledSyncAllSubscriptionsAsync(CancellationToken cancellationToken = default);
}
