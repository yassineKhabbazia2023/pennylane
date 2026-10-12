// <copyright file="IEmailRecapRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.Entities;
using Pennylane.Domain.Enums;

namespace Pennylane.Application.Interfaces;

public interface IEmailRecapRepository
{
    Task<EmailRecapBatchEntity> CreateBatchAsync(DateTime recapDate);
    Task<EmailRecapBatchEntity?> GetBatchByIdAsync(int batchId);
    Task<EmailRecapBatchEntity?> GetBatchByDateAsync(DateTime recapDate);
    Task<IReadOnlyList<EmailRecapBatchEntity>> GetBatchesByStatusAsync(EmailRecapBatchStatus status);
    Task UpdateBatchAsync(EmailRecapBatchEntity batch);
    Task AddItemsAsync(int batchId, IEnumerable<EmailRecapItemEntity> items);
    Task<IReadOnlyList<EmailRecapItemEntity>> GetPendingItemsAsync(int batchId);
    Task<IReadOnlyList<EmailRecapItemEntity>> GetItemsByBatchIdAsync(int batchId);
    Task UpdateItemStatusAsync(int itemId, EmailRecapItemStatus status, string? errorMessage = null);
    Task UpdateItemsStatusAsync(IEnumerable<(int ItemId, EmailRecapItemStatus Status, string? Error)> updates);
    Task<bool> HasBatchForDateAsync(DateTime recapDate);
}
