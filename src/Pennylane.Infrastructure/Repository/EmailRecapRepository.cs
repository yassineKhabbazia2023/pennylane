// <copyright file="EmailRecapRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Enums;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

/// <summary>
/// Repository for managing email recap batches and items.
/// </summary>
public class EmailRecapRepository(PennylaneContext context) : IEmailRecapRepository
{
    /// <inheritdoc />
    public async Task<EmailRecapBatchEntity> CreateBatchAsync(DateTime recapDate)
    {
        var batch = new EmailRecapBatchEntity
        {
            RecapDate = recapDate.Date,
            GeneratedAt = DateTime.UtcNow,
            Status = (int)EmailRecapBatchStatus.Generating,
            TotalItems = 0,
            SentCount = 0,
            FailedCount = 0
        };

        context.EmailRecapBatchEntity.Add(batch);
        await context.SaveChangesAsync();

        return batch;
    }

    /// <inheritdoc />
    public async Task<EmailRecapBatchEntity?> GetBatchByIdAsync(int batchId)
    {
        return await context.EmailRecapBatchEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId);
    }

    /// <inheritdoc />
    public async Task<EmailRecapBatchEntity?> GetBatchByDateAsync(DateTime recapDate)
    {
        return await context.EmailRecapBatchEntity
            .AsNoTracking()
            .Where(b => b.RecapDate == recapDate.Date)
            .OrderByDescending(b => b.GeneratedAt)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRecapBatchEntity>> GetBatchesByStatusAsync(EmailRecapBatchStatus status)
    {
        return await context.EmailRecapBatchEntity
            .AsNoTracking()
            .Where(b => b.Status == (int)status)
            .OrderBy(b => b.GeneratedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateBatchAsync(EmailRecapBatchEntity batch)
    {
        context.EmailRecapBatchEntity.Update(batch);
        await context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task AddItemsAsync(int batchId, IEnumerable<EmailRecapItemEntity> items)
    {
        var itemList = items.ToList();
        foreach (var item in itemList)
        {
            item.BatchId = batchId;
        }

        await context.EmailRecapItemEntity.AddRangeAsync(itemList);
        await context.SaveChangesAsync();

        await context.EmailRecapBatchEntity
            .Where(b => b.Id == batchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.TotalItems, b => b.TotalItems + itemList.Count));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRecapItemEntity>> GetPendingItemsAsync(int batchId)
    {
        return await context.EmailRecapItemEntity
            .AsNoTracking()
            .Where(i => i.BatchId == batchId && i.Status == (int)EmailRecapItemStatus.Pending)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRecapItemEntity>> GetItemsByBatchIdAsync(int batchId)
    {
        return await context.EmailRecapItemEntity
            .AsNoTracking()
            .Where(i => i.BatchId == batchId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateItemStatusAsync(int itemId, EmailRecapItemStatus status, string? errorMessage = null)
    {
        var sentAt = status == EmailRecapItemStatus.Sent ? DateTime.UtcNow : (DateTime?)null;

        await context.EmailRecapItemEntity
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, (int)status)
                .SetProperty(i => i.SentAt, sentAt)
                .SetProperty(i => i.ErrorMessage, errorMessage)
                .SetProperty(i => i.RetryCount, i => i.RetryCount + 1));
    }

    /// <inheritdoc />
    public async Task UpdateItemsStatusAsync(IEnumerable<(int ItemId, EmailRecapItemStatus Status, string? Error)> updates)
    {
        var updatesList = updates.ToList();
        if (updatesList.Count == 0)
            return;

        var sentIds = updatesList
            .Where(u => u.Status == EmailRecapItemStatus.Sent)
            .Select(u => u.ItemId)
            .ToList();

        var failedUpdates = updatesList
            .Where(u => u.Status == EmailRecapItemStatus.Failed)
            .ToList();

        // Bulk update for sent items
        if (sentIds.Count > 0)
        {
            await context.EmailRecapItemEntity
                .Where(i => sentIds.Contains(i.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, (int)EmailRecapItemStatus.Sent)
                    .SetProperty(i => i.SentAt, DateTime.UtcNow)
                    .SetProperty(i => i.ErrorMessage, (string?)null)
                    .SetProperty(i => i.RetryCount, i => i.RetryCount + 1));
        }

        // Individual updates for failed items (each has unique error message)
        foreach (var (itemId, status, error) in failedUpdates)
        {
            await context.EmailRecapItemEntity
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, (int)status)
                    .SetProperty(i => i.SentAt, (DateTime?)null)
                    .SetProperty(i => i.ErrorMessage, error)
                    .SetProperty(i => i.RetryCount, i => i.RetryCount + 1));
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasBatchForDateAsync(DateTime recapDate)
    {
        return await context.EmailRecapBatchEntity
            .AsNoTracking()
            .AnyAsync(b => b.RecapDate == recapDate.Date &&
                          (b.Status == (int)EmailRecapBatchStatus.Ready ||
                           b.Status == (int)EmailRecapBatchStatus.Sending ||
                           b.Status == (int)EmailRecapBatchStatus.Completed));
    }
}
