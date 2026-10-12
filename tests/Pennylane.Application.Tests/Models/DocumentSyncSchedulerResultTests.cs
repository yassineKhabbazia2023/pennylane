// <copyright file="DocumentSyncSchedulerResultTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models;

namespace Pennylane.Application.Tests.Models;

public class DocumentSyncSchedulerResultTests
{
    [Fact]
    public void DocumentSyncSchedulerResult_Should_CalculateDuration_Correctly()
    {
        // Arrange
        var startedAt = new DateTime(2025, 12, 11, 7, 30, 0);
        var completedAt = new DateTime(2025, 12, 11, 7, 47, 30); // 17 minutes 30 secondes plus tard

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 10000,
            SyncMessagesPublished: 9995,
            Errors: 5,
            ErrorMessages: new List<string> { "Error 1", "Error 2" },
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(17.5), result.Duration);
        Assert.Equal(17, result.Duration.Minutes);
        Assert.Equal(30, result.Duration.Seconds);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_Should_HaveCorrectProperties()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddMinutes(10);
        var errorMessages = new List<string> { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 95,
            Errors: 3,
            ErrorMessages: errorMessages,
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(100, result.TotalCompanies);
        Assert.Equal(95, result.SyncMessagesPublished);
        Assert.Equal(3, result.Errors);
        Assert.Equal(errorMessages, result.ErrorMessages);
        Assert.Equal(startedAt, result.StartedAt);
        Assert.Equal(completedAt, result.CompletedAt);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_WithNoErrors_Should_HaveEmptyErrorList()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddSeconds(30);

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 50,
            SyncMessagesPublished: 50,
            Errors: 0,
            ErrorMessages: new List<string>(),
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(0, result.Errors);
        Assert.Empty(result.ErrorMessages);
        Assert.Equal(50, result.SyncMessagesPublished);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_Duration_Should_BeZero_WhenStartAndEndAreEqual()
    {
        // Arrange
        var timestamp = new DateTime(2025, 12, 11, 7, 30, 0);

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 0,
            SyncMessagesPublished: 0,
            Errors: 0,
            ErrorMessages: new List<string>(),
            StartedAt: timestamp,
            CompletedAt: timestamp);

        // Assert
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_Should_HandleLongRunningJob()
    {
        // Arrange
        var startedAt = new DateTime(2025, 12, 11, 7, 30, 0);
        var completedAt = startedAt.AddHours(1).AddMinutes(15).AddSeconds(45); // 1h 15m 45s

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 10000,
            SyncMessagesPublished: 10000,
            Errors: 0,
            ErrorMessages: new List<string>(),
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(1, result.Duration.Hours);
        Assert.Equal(15, result.Duration.Minutes);
        Assert.Equal(45, result.Duration.Seconds);
        Assert.Equal(TimeSpan.FromSeconds(4545), result.Duration);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_With_AllCompaniesSkipped_Should_HaveCorrectCounts()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddSeconds(5);

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 0,
            Errors: 0,
            ErrorMessages: new List<string>(),
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(100, result.TotalCompanies);
        Assert.Equal(0, result.SyncMessagesPublished);
        Assert.Equal(0, result.Errors);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_WithCriticalError_Should_ReturnZeroTotalCompanies()
    {
        // Arrange - simule une erreur critique qui empêche le traitement
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddMilliseconds(100);
        var errorMessages = new List<string> { "Critical error: Database connection failed" };

        // Act
        var result = new DocumentSyncSchedulerResult(
            TotalCompanies: 0,
            SyncMessagesPublished: 0,
            Errors: 1,
            ErrorMessages: errorMessages,
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Assert
        Assert.Equal(0, result.TotalCompanies);
        Assert.Equal(0, result.SyncMessagesPublished);
        Assert.Equal(1, result.Errors);
        Assert.Single(result.ErrorMessages);
        Assert.Contains("Critical error", result.ErrorMessages[0]);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_Should_SupportRecordEquality()
    {
        // Arrange
        var startedAt = new DateTime(2025, 12, 11, 7, 30, 0);
        var completedAt = new DateTime(2025, 12, 11, 7, 45, 0);
        var errorMessages = new List<string> { "Error 1" };

        var result1 = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 95,
            Errors: 1,
            ErrorMessages: errorMessages,
            StartedAt: startedAt,
            CompletedAt: completedAt);

        var result2 = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 95,
            Errors: 1,
            ErrorMessages: errorMessages,
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Act & Assert - Records avec les mêmes valeurs sont égaux
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void DocumentSyncSchedulerResult_WithDifferentValues_Should_NotBeEqual()
    {
        // Arrange
        var startedAt = new DateTime(2025, 12, 11, 7, 30, 0);
        var completedAt = new DateTime(2025, 12, 11, 7, 45, 0);

        var result1 = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 95,
            Errors: 1,
            ErrorMessages: new List<string> { "Error 1" },
            StartedAt: startedAt,
            CompletedAt: completedAt);

        var result2 = new DocumentSyncSchedulerResult(
            TotalCompanies: 100,
            SyncMessagesPublished: 90, // Différent
            Errors: 1,
            ErrorMessages: new List<string> { "Error 1" },
            StartedAt: startedAt,
            CompletedAt: completedAt);

        // Act & Assert
        Assert.NotEqual(result1, result2);
    }
}
