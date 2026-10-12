using System;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Application.services;

public class SyncService(
    ILogger<SyncService> logger,
    IDocumentRepository documentRepository,
    IAccountRepository accountRepository,
    INotificationManager notificationManager,
    IOptions<ServiceBusOptions> options,
    ISubscriptionRepository subscriptionRepository,
    IOptions<DocumentSyncSchedulerOptions> schedulerOptions) : ISyncService
{
    private readonly IDocumentRepository _documentRepository = documentRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly INotificationManager _notificationManager = notificationManager;
    private readonly ServiceBusOptions _options = options.Value;
    private readonly ISubscriptionRepository _subscriptionRepository = subscriptionRepository;
    private readonly DocumentSyncSchedulerOptions _schedulerOptions = schedulerOptions.Value;

    public async Task<bool> CheckAndPushSyncMessageAsync(string accountNumber, bool forceSync = false)
    {
        try
        {
            var syncStatus = await _documentRepository.GetSyncStatusAsync(accountNumber);
            var account = await _accountRepository.GetAccountByNumberOrIdAsync(accountNumber);
            if (account != null && (forceSync || syncStatus == null || (DateTime.UtcNow - syncStatus.LastSynchro).TotalMinutes > _options.SyncThresholdMinutes))
            {
                var @event = new PennylaneDocumentSyncEventData()
                {
                    AccountId = account.AccountId,
                    AccountNumber = accountNumber
                };

                await _notificationManager.PublishAsync(new PennylaneDocumentSyncEvent(@event, "1.0"));
                await _documentRepository.AddOrUpdateSyncDateAsync(account.AccountId, accountNumber);
                logger.LogInformation("Sync message pushed for account {AccountNumber} (forceSync: {ForceSync})", accountNumber, forceSync);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            // En cas d'erreur, ne rien faire pour ne pas bloquer l'API
            logger.LogError(ex, "Failed to check and push sync message for account {AccountNumber}", accountNumber);
            return false;
        }
    }

    public async Task<DocumentSyncSchedulerResult> ScheduledSyncAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var errors = new List<string>();
        var published = 0;
        var totalCompanies = 0;

        try
        {
            logger.LogInformation("[ScheduledSync] Starting scheduled document sync");

            // Récupérer les accounts avec souscription Pennylane active ET présents dans la table Companies
            var subscriptions = await _subscriptionRepository
                .GetActiveSubscriptionsWithPennylaneCompaniesAsync();

            totalCompanies = subscriptions.Select(s => s.AccountNumber).Distinct().Count();

            logger.LogInformation(
                "[ScheduledSync] Found {Count} Pennylane companies with active subscriptions",
                totalCompanies);

            // Organiser en batches
            var batches = subscriptions
                .Select((sub, index) => new { sub, index })
                .GroupBy(x => x.index / _schedulerOptions.BatchSize)
                .Select(g => g.Select(x => x.sub).ToList())
                .ToList();

            logger.LogInformation(
                "[ScheduledSync] Processing {BatchCount} batches of {BatchSize} companies",
                batches.Count, _schedulerOptions.BatchSize);

            // Traiter chaque batch
            foreach (var (batch, batchIndex) in batches.Select((b, i) => (b, i)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.LogInformation(
                    "[ScheduledSync] Processing batch {BatchIndex}/{TotalBatches} ({Count} companies)",
                    batchIndex + 1, batches.Count, batch.Count);

                // Traiter chaque compte dans le batch
                foreach (var (accountId, accountNumber) in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool success = await CheckAndPushSyncMessageAsync(
                        accountNumber,
                        forceSync: _schedulerOptions.ForceSyncIgnoreThreshold);

                    if (success)
                    {
                        published++;
                    }
                }

                // Délai entre batches (sauf pour le dernier)
                if (batchIndex < batches.Count - 1 && _schedulerOptions.BatchDelaySeconds > 0)
                {
                    logger.LogDebug(
                        "[ScheduledSync] Waiting {Seconds}s before next batch",
                        _schedulerOptions.BatchDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_schedulerOptions.BatchDelaySeconds), cancellationToken);
                }
            }

            var result = new DocumentSyncSchedulerResult(
                totalCompanies,
                published,
                errors.Count,
                errors,
                startedAt,
                DateTime.UtcNow);

            logger.LogInformation(
                "[ScheduledSync] Completed in {Duration}. Total: {Total}, Published: {Published}, Errors: {Errors}",
                result.Duration, result.TotalCompanies, result.SyncMessagesPublished, result.Errors);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[ScheduledSync] Sync was cancelled. Processed {Published} companies before cancellation", published);
            errors.Add("Sync was cancelled");
            return new DocumentSyncSchedulerResult(
                totalCompanies, published, errors.Count, errors, startedAt, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ScheduledSync] Critical failure during scheduled sync");
            errors.Add($"Critical error: {ex.Message}");
            return new DocumentSyncSchedulerResult(
                0, published, errors.Count, errors, startedAt, DateTime.UtcNow);
        }
    }
}
