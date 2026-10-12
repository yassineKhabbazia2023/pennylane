// <copyright file="EmailRecapRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Enums;
using Pennylane.Infrastructure.Repository;
using Pennylane.Infrastructure.Tests.Utils;

namespace Pennylane.Infrastructure.Tests.Repository;

public class EmailRecapRepositoryTests : IDisposable
{
    private readonly TestContext _context;
    private readonly EmailRecapRepository _sut;

    public EmailRecapRepositoryTests()
    {
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(Guid.NewGuid().ToString());
        _context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        _sut = new EmailRecapRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateBatchAsync Tests

    [Fact]
    public async Task CreateBatchAsync_Success_ReturnsBatchWithCorrectStatus()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;

        // Act
        var result = await _sut.CreateBatchAsync(recapDate);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.RecapDate.Should().Be(recapDate);
        result.Status.Should().Be((int)EmailRecapBatchStatus.Generating);
        result.TotalItems.Should().Be(0);
        result.SentCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateBatchAsync_MultipleBatches_CreatesUniqueIds()
    {
        // Arrange & Act
        var batch1 = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        var batch2 = await _sut.CreateBatchAsync(DateTime.UtcNow.Date.AddDays(1));

        // Assert
        batch1.Id.Should().NotBe(batch2.Id);
    }

    #endregion

    #region GetBatchByIdAsync Tests

    [Fact]
    public async Task GetBatchByIdAsync_Exists_ReturnsBatch()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);

        // Act
        var result = await _sut.GetBatchByIdAsync(batch.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(batch.Id);
    }

    [Fact]
    public async Task GetBatchByIdAsync_NotExists_ReturnsNull()
    {
        // Act
        var result = await _sut.GetBatchByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetBatchByDateAsync Tests

    [Fact]
    public async Task GetBatchByDateAsync_Exists_ReturnsBatch()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        await _sut.CreateBatchAsync(recapDate);

        // Act
        var result = await _sut.GetBatchByDateAsync(recapDate);

        // Assert
        result.Should().NotBeNull();
        result!.RecapDate.Should().Be(recapDate);
    }

    [Fact]
    public async Task GetBatchByDateAsync_NotExists_ReturnsNull()
    {
        // Act
        var result = await _sut.GetBatchByDateAsync(DateTime.UtcNow.Date.AddYears(-1));

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetBatchesByStatusAsync Tests

    [Fact]
    public async Task GetBatchesByStatusAsync_ReturnsFilteredBatches()
    {
        // Arrange
        var batch1 = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        batch1.Status = (int)EmailRecapBatchStatus.Ready;
        await _sut.UpdateBatchAsync(batch1);

        var batch2 = await _sut.CreateBatchAsync(DateTime.UtcNow.Date.AddDays(1));
        batch2.Status = (int)EmailRecapBatchStatus.Completed;
        await _sut.UpdateBatchAsync(batch2);

        var batch3 = await _sut.CreateBatchAsync(DateTime.UtcNow.Date.AddDays(2));
        batch3.Status = (int)EmailRecapBatchStatus.Ready;
        await _sut.UpdateBatchAsync(batch3);

        // Act
        var result = await _sut.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(b => b.Status.Should().Be((int)EmailRecapBatchStatus.Ready));
    }

    [Fact]
    public async Task GetBatchesByStatusAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        batch.Status = (int)EmailRecapBatchStatus.Ready;
        await _sut.UpdateBatchAsync(batch);

        // Act
        var result = await _sut.GetBatchesByStatusAsync(EmailRecapBatchStatus.Failed);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateBatchAsync Tests

    [Fact]
    public async Task UpdateBatchAsync_UpdatesStatus()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        batch.Status = (int)EmailRecapBatchStatus.Completed;
        batch.SentCount = 10;
        batch.FailedCount = 2;
        batch.CompletedAt = DateTime.UtcNow;

        // Act
        await _sut.UpdateBatchAsync(batch);

        // Assert
        _context.ChangeTracker.Clear();
        var updated = await _sut.GetBatchByIdAsync(batch.Id);
        updated!.Status.Should().Be((int)EmailRecapBatchStatus.Completed);
        updated.SentCount.Should().Be(10);
        updated.FailedCount.Should().Be(2);
        updated.CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region AddItemsAsync Tests

    [Fact]
    public async Task AddItemsAsync_AddsItemsToBatch()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        var items = new List<EmailRecapItemEntity>
        {
            new() { ContactId = 1, ContactEmail = "user1@test.com", ContactName = "User 1", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Pending },
            new() { ContactId = 2, ContactEmail = "user2@test.com", ContactName = "User 2", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Pending }
        };

        // Act
        await _sut.AddItemsAsync(batch.Id, items);

        // Assert
        var result = await _sut.GetItemsByBatchIdAsync(batch.Id);
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.BatchId.Should().Be(batch.Id));
    }

    [Fact]
    public async Task AddItemsAsync_EmptyList_DoesNotThrow()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);

        // Act
        var act = async () => await _sut.AddItemsAsync(batch.Id, new List<EmailRecapItemEntity>());

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region GetPendingItemsAsync Tests

    [Fact]
    public async Task GetPendingItemsAsync_ReturnsOnlyPendingItems()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        var items = new List<EmailRecapItemEntity>
        {
            new() { ContactId = 1, ContactEmail = "pending@test.com", ContactName = "Pending", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Pending },
            new() { ContactId = 2, ContactEmail = "sent@test.com", ContactName = "Sent", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Sent },
            new() { ContactId = 3, ContactEmail = "failed@test.com", ContactName = "Failed", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Failed }
        };
        await _sut.AddItemsAsync(batch.Id, items);

        // Update statuses
        var allItems = await _context.EmailRecapItemEntity.ToListAsync();
        allItems[1].Status = (int)EmailRecapItemStatus.Sent;
        allItems[2].Status = (int)EmailRecapItemStatus.Failed;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetPendingItemsAsync(batch.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().ContactEmail.Should().Be("pending@test.com");
    }

    #endregion

    #region GetItemsByBatchIdAsync Tests

    [Fact]
    public async Task GetItemsByBatchIdAsync_ReturnsAllItems()
    {
        // Arrange
        var batch = await _sut.CreateBatchAsync(DateTime.UtcNow.Date);
        var items = new List<EmailRecapItemEntity>
        {
            new() { ContactId = 1, ContactEmail = "user1@test.com", ContactName = "User 1", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Pending },
            new() { ContactId = 2, ContactEmail = "user2@test.com", ContactName = "User 2", PayloadJson = "{}", Status = (int)EmailRecapItemStatus.Sent }
        };
        await _sut.AddItemsAsync(batch.Id, items);

        // Act
        var result = await _sut.GetItemsByBatchIdAsync(batch.Id);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetItemsByBatchIdAsync_WrongBatchId_ReturnsEmpty()
    {
        // Act
        var result = await _sut.GetItemsByBatchIdAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region HasBatchForDateAsync Tests

    [Fact]
    public async Task HasBatchForDateAsync_WithReadyBatch_ReturnsTrue()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        var batch = await _sut.CreateBatchAsync(recapDate);
        batch.Status = (int)EmailRecapBatchStatus.Ready;
        await _sut.UpdateBatchAsync(batch);

        // Act
        var result = await _sut.HasBatchForDateAsync(recapDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBatchForDateAsync_WithSendingBatch_ReturnsTrue()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        var batch = await _sut.CreateBatchAsync(recapDate);
        batch.Status = (int)EmailRecapBatchStatus.Sending;
        await _sut.UpdateBatchAsync(batch);

        // Act
        var result = await _sut.HasBatchForDateAsync(recapDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBatchForDateAsync_WithCompletedBatch_ReturnsTrue()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        var batch = await _sut.CreateBatchAsync(recapDate);
        batch.Status = (int)EmailRecapBatchStatus.Completed;
        await _sut.UpdateBatchAsync(batch);

        // Act
        var result = await _sut.HasBatchForDateAsync(recapDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBatchForDateAsync_WithOnlyGeneratingBatch_ReturnsFalse()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        await _sut.CreateBatchAsync(recapDate);

        // Act
        var result = await _sut.HasBatchForDateAsync(recapDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBatchForDateAsync_WithOnlyFailedBatch_ReturnsFalse()
    {
        // Arrange
        var recapDate = DateTime.UtcNow.Date;
        var batch = await _sut.CreateBatchAsync(recapDate);
        batch.Status = (int)EmailRecapBatchStatus.Failed;
        await _sut.UpdateBatchAsync(batch);

        // Act
        var result = await _sut.HasBatchForDateAsync(recapDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBatchForDateAsync_NoBatch_ReturnsFalse()
    {
        // Act
        var result = await _sut.HasBatchForDateAsync(DateTime.UtcNow.Date.AddYears(-1));

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
