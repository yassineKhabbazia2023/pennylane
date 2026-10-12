// <copyright file="EmailRecapService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Commons.WebApi.QueryParams;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.EmailRecap;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Enums;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Services;

public class EmailRecapService : IEmailRecapService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IEmailRecapRepository _emailRecapRepository;
    private readonly EmailRecapOptions _options;
    private readonly ILogger<EmailRecapService> _logger;

    public EmailRecapService(
        IDocumentRepository documentRepository,
        IAuthorizationService authorizationService,
        IEventPublisher eventPublisher,
        IEmailRecapRepository emailRecapRepository,
        IOptions<EmailRecapOptions> options,
        ILogger<EmailRecapService> logger)
    {
        _documentRepository = documentRepository;
        _authorizationService = authorizationService;
        _eventPublisher = eventPublisher;
        _emailRecapRepository = emailRecapRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailRecapGenerationResult> GenerateDailyRecapsAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email recap feature is disabled");
            return EmailRecapGenerationResult.Empty(DateTime.UtcNow.Date);
        }

        var recapDate = DateTime.UtcNow.Date;

        try
        {
            if (await _emailRecapRepository.HasBatchForDateAsync(recapDate))
            {
                _logger.LogWarning("Batch already exists for date {RecapDate}", recapDate);
                return EmailRecapGenerationResult.Failed($"Batch already exists for {recapDate:yyyy-MM-dd}");
            }

            var (fromUtc, toUtc) = CalculateLookbackPeriod();

            _logger.LogInformation(
                "Generating email recaps for documents between {FromUtc} and {ToUtc}",
                fromUtc, toUtc);

            var documentCounts = await _documentRepository.GetDocumentCountsByAccountAsync(fromUtc, toUtc);

            if (documentCounts.Count == 0)
            {
                _logger.LogInformation("No documents found in the lookback period");
                return EmailRecapGenerationResult.Empty(recapDate);
            }

            _logger.LogInformation("Found {AccountCount} accounts with new documents", documentCounts.Count);

            var batch = await _emailRecapRepository.CreateBatchAsync(recapDate);

            try
            {
                var accountIds = documentCounts.Keys.ToList();
                var contactsByAccount = await _authorizationService.GetContactsForAccountsAsync(accountIds);

                if (contactsByAccount.Count == 0)
                {
                    _logger.LogInformation("No authorized contacts found");
                    batch.Status = (int)EmailRecapBatchStatus.Ready;
                    batch.TotalItems = 0;
                    await _emailRecapRepository.UpdateBatchAsync(batch);
                    return new EmailRecapGenerationResult(batch.Id, recapDate, 0, 0, documentCounts.Count, true, null, DateTime.UtcNow);
                }

                var items = CreateEmailRecapItems(documentCounts, contactsByAccount);

                if (items.Count > 0)
                {
                    await _emailRecapRepository.AddItemsAsync(batch.Id, items);
                }

                batch.Status = (int)EmailRecapBatchStatus.Ready;
                batch.TotalItems = items.Count;
                await _emailRecapRepository.UpdateBatchAsync(batch);

                var totalDocuments = documentCounts.Values.Sum(d => d.DocumentCount);

                _logger.LogInformation(
                    "Generated batch {BatchId} with {ItemCount} items for {AccountCount} accounts",
                    batch.Id, items.Count, documentCounts.Count);

                return new EmailRecapGenerationResult(
                    batch.Id,
                    recapDate,
                    items.Count,
                    totalDocuments,
                    documentCounts.Count,
                    true,
                    null,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                batch.Status = (int)EmailRecapBatchStatus.Failed;
                batch.ErrorMessage = ex.Message;
                await _emailRecapRepository.UpdateBatchAsync(batch);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating email recaps");
            return EmailRecapGenerationResult.Failed(ex.Message);
        }
    }

    public async Task<EmailRecapSendResult> SendPendingRecapsAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email recap feature is disabled");
            return EmailRecapSendResult.Empty();
        }

        var startedAt = DateTime.UtcNow;
        var errors = new List<string>();

        try
        {
            var readyBatches = await _emailRecapRepository.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready);

            if (readyBatches.Count == 0)
            {
                _logger.LogInformation("No ready batches to process");
                return EmailRecapSendResult.Empty();
            }

            var totalSent = 0;
            var totalFailed = 0;
            var lastBatchId = 0;
            var totalItems = 0;

            foreach (var batch in readyBatches)
            {
                lastBatchId = batch.Id;
                var (sent, failed, batchErrors) = await ProcessBatchAsync(batch);
                totalSent += sent;
                totalFailed += failed;
                totalItems += batch.TotalItems;
                errors.AddRange(batchErrors);
            }

            _logger.LogInformation(
                "Send completed. Total sent: {Sent}, Failed: {Failed}",
                totalSent, totalFailed);

            return new EmailRecapSendResult(
                lastBatchId,
                totalItems,
                totalSent,
                totalFailed,
                errors,
                startedAt,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email recaps");
            errors.Add($"Critical error: {ex.Message}");
            return new EmailRecapSendResult(0, 0, 0, 0, errors, startedAt, DateTime.UtcNow);
        }
    }

    private async Task<(int sent, int failed, List<string> errors)> ProcessBatchAsync(EmailRecapBatchEntity batch)
    {
        var errors = new List<string>();

        batch.Status = (int)EmailRecapBatchStatus.Sending;
        batch.SendingStartedAt = DateTime.UtcNow;
        await _emailRecapRepository.UpdateBatchAsync(batch);

        var items = await _emailRecapRepository.GetPendingItemsAsync(batch.Id);

        if (items.Count == 0)
        {
            batch.Status = (int)EmailRecapBatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
            await _emailRecapRepository.UpdateBatchAsync(batch);
            return (0, 0, errors);
        }

        _logger.LogInformation("Processing batch {BatchId} with {ItemCount} items", batch.Id, items.Count);

        var semaphore = new SemaphoreSlim(_options.MaxParallelEmailSends);
        var sentCount = new ConcurrentBag<int>();
        var failedCount = new ConcurrentBag<int>();
        var updates = new ConcurrentBag<(int ItemId, EmailRecapItemStatus Status, string? Error)>();

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var payload = JsonSerializer.Deserialize<EmailRecapPayload>(item.PayloadJson);
                if (payload == null)
                {
                    failedCount.Add(1);
                    updates.Add((item.Id, EmailRecapItemStatus.Failed, "Invalid payload"));
                    return;
                }

                var emailRequest = BuildEmailRequest(item.ContactEmail, payload);
                var emailEvent = new EmailOnlySenderEvent(emailRequest);
                await _eventPublisher.PublishAsync(emailEvent, correlationId: null, topicName: _options.ServiceBusTopic);

                sentCount.Add(1);
                updates.Add((item.Id, EmailRecapItemStatus.Sent, null));
            }
            catch (Exception ex)
            {
                failedCount.Add(1);
                updates.Add((item.Id, EmailRecapItemStatus.Failed, ex.Message));
                errors.Add($"Error sending to {item.ContactEmail}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        await _emailRecapRepository.UpdateItemsStatusAsync(updates);

        batch.SentCount = sentCount.Count;
        batch.FailedCount = failedCount.Count;
        batch.Status = (int)EmailRecapBatchStatus.Completed;
        batch.CompletedAt = DateTime.UtcNow;
        await _emailRecapRepository.UpdateBatchAsync(batch);

        return (sentCount.Count, failedCount.Count, errors);
    }

    private List<EmailRecapItemEntity> CreateEmailRecapItems(
        Dictionary<int, DocumentCountByAccount> documentCounts,
        Dictionary<int, IReadOnlyList<AuthorizedContact>> contactsByAccount)
    {
        var aggregations = new Dictionary<string, (AuthorizedContact Contact, List<DocumentCountByAccount> Accounts)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (accountId, contacts) in contactsByAccount)
        {
            if (!documentCounts.TryGetValue(accountId, out var docCount))
                continue;

            foreach (var contact in contacts)
            {
                if (string.IsNullOrWhiteSpace(contact.Email))
                    continue;

                if (!aggregations.TryGetValue(contact.Email, out var agg))
                {
                    agg = (contact, new List<DocumentCountByAccount>());
                    aggregations[contact.Email] = agg;
                }

                agg.Accounts.Add(docCount);
            }
        }

        return aggregations.Select(kvp =>
        {
            var (contact, accounts) = kvp.Value;
            var payload = BuildPayload(contact, accounts);

            return new EmailRecapItemEntity
            {
                ContactId = contact.ContactId,
                ContactEmail = contact.Email,
                ContactName = BuildUserName(contact.FirstName, contact.LastName),
                TotalDocuments = accounts.Sum(a => a.DocumentCount),
                AccountCount = accounts.Count,
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = (int)EmailRecapItemStatus.Pending,
                RetryCount = 0
            };
        }).ToList();
    }

    private EmailRecapPayload BuildPayload(AuthorizedContact contact, List<DocumentCountByAccount> accounts)
    {
        var summaryLines = accounts
            .Select(d => new SummaryLine(d.AccountName ?? d.AccountNumber, d.DocumentCount))
            .OrderBy(s => s.EntityName)
            .ToList();

        return new EmailRecapPayload
        {
            IsRecipientCustomer = true,
            UserName = BuildUserName(contact.FirstName, contact.LastName),
            LoginUrl = _options.LoginUrl,
            TitleDocumentsTypeName = "Pennylane",
            SummaryType = "Comptabilité",
            DepositedDocumentsCount = accounts.Sum(a => a.DocumentCount),
            DocumentsDepositDate = FormatDepositDate(DateTime.UtcNow),
            SummaryLines = summaryLines
        };
    }

    private EmailRequest BuildEmailRequest(string email, EmailRecapPayload payload)
    {
        return new EmailRequest
        {
            From = _options.SenderEmail,
            To = new List<string> { email },
            TemplateName = _options.TemplateName,
            Variables = new Dictionary<string, object>
            {
                { "isRecipientCustomer", payload.IsRecipientCustomer },
                { "userName", payload.UserName },
                { "loginUrl", payload.LoginUrl },
                { "titleDocumentsTypeName", payload.TitleDocumentsTypeName },
                { "summaryType", payload.SummaryType },
                { "depositedDocumentsCount", payload.DepositedDocumentsCount },
                { "documentsDepositDate", payload.DocumentsDepositDate },
                { "summaryLines", payload.SummaryLines }
            }
        };
    }

    private static string BuildUserName(string? firstName, string? lastName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(firstName)) parts.Add(firstName.Trim());
        if (!string.IsNullOrWhiteSpace(lastName)) parts.Add(lastName.Trim());
        return parts.Count > 0 ? string.Join(" ", parts) : "Client";
    }

    private static string FormatDepositDate(DateTime dateUtc)
    {
        var culture = new CultureInfo("fr-FR");
        return dateUtc.ToString("dd MMMM yyyy", culture);
    }

    private (DateTime fromUtc, DateTime toUtc) CalculateLookbackPeriod()
    {
        var parisTimeZone = TimeZoneHelper.GetParisTimeZone();
        var nowParis = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, parisTimeZone);

        var toTimeParis = nowParis.Date.AddHours(_options.ReferenceHour);
        var fromTimeParis = toTimeParis.AddHours(-_options.LookbackHours);

        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toTimeParis, parisTimeZone);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromTimeParis, parisTimeZone);

        return (fromUtc, toUtc);
    }
}
