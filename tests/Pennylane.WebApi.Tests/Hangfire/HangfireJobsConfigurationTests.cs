// <copyright file="HangfireJobsConfigurationTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;

namespace Pennylane.WebApi.Tests.Hangfire;

public class HangfireJobsConfigurationTests
{
    private readonly ServiceCollection _services;

    public HangfireJobsConfigurationTests()
    {
        _services = new ServiceCollection();

        // Configure les options nécessaires
        _services.Configure<EmailRecapOptions>(options =>
        {
            options.SenderEmail = "test@example.com";
            options.LoginUrl = "https://example.com/login";
            options.GenerationCron = "0 8 * * *";
            options.SendCron = "0 9 * * *";
        });

        _services.Configure<DocumentSyncSchedulerOptions>(options =>
        {
            options.Enabled = true;
            options.SyncCron = "30 7 * * *";
            options.BatchSize = 100;
            options.BatchDelaySeconds = 10;
            options.ForceSyncIgnoreThreshold = true;
        });
    }

    [Fact]
    public void EmailRecapOptions_Should_BeConfiguredCorrectly()
    {
        // Arrange & Act
        var serviceProvider = _services.BuildServiceProvider();
        var emailOptions = serviceProvider.GetRequiredService<IOptions<EmailRecapOptions>>().Value;

        // Assert - Vérifie que les options EmailRecap sont bien configurées
        Assert.Equal("0 8 * * *", emailOptions.GenerationCron);
        Assert.Equal("0 9 * * *", emailOptions.SendCron);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_BeConfiguredCorrectly_WhenEnabled()
    {
        // Arrange & Act
        var serviceProvider = _services.BuildServiceProvider();
        var syncOptions = serviceProvider.GetRequiredService<IOptions<DocumentSyncSchedulerOptions>>().Value;

        // Assert - Vérifie que les options de sync sont bien configurées
        Assert.True(syncOptions.Enabled);
        Assert.Equal("30 7 * * *", syncOptions.SyncCron);
        Assert.Equal(100, syncOptions.BatchSize);
        Assert.Equal(10, syncOptions.BatchDelaySeconds);
        Assert.True(syncOptions.ForceSyncIgnoreThreshold);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_BeConfiguredCorrectly_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<DocumentSyncSchedulerOptions>(options =>
        {
            options.Enabled = false; // Désactivé
            options.SyncCron = "30 7 * * *";
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var syncOptions = serviceProvider.GetRequiredService<IOptions<DocumentSyncSchedulerOptions>>().Value;

        // Assert - Vérifie que le job peut être désactivé via configuration
        Assert.False(syncOptions.Enabled);
        Assert.Equal("30 7 * * *", syncOptions.SyncCron);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_HaveCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new DocumentSyncSchedulerOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal("30 7 * * *", options.SyncCron);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(10, options.BatchDelaySeconds);
        Assert.True(options.ForceSyncIgnoreThreshold);
    }

    [Fact]
    public void EmailRecapOptions_Should_HaveCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new EmailRecapOptions
        {
            SenderEmail = "test@example.com",
            LoginUrl = "https://example.com/login"
        };

        // Assert
        Assert.Equal("0 8 * * *", options.GenerationCron);
        Assert.Equal("0 9 * * *", options.SendCron);
        Assert.True(options.Enabled);
        Assert.Equal(9, options.ReferenceHour);
        Assert.Equal("Document deposit pennylane", options.TemplateName);
        Assert.Equal(24, options.LookbackHours);
        Assert.Equal(20, options.MaxParallelEmailSends);
        Assert.Equal(100, options.BatchSize);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_ValidateBatchSize()
    {
        // Arrange
        var options = new DocumentSyncSchedulerOptions
        {
            BatchSize = 5 // Invalide: minimum est 10
        };

        // Act & Assert
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(DocumentSyncSchedulerOptions.BatchSize)));
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_ValidateBatchDelaySeconds()
    {
        // Arrange
        var options = new DocumentSyncSchedulerOptions
        {
            BatchDelaySeconds = 100 // Invalide: maximum est 60
        };

        // Act & Assert
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(DocumentSyncSchedulerOptions.BatchDelaySeconds)));
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_AcceptValidValues()
    {
        // Arrange
        var options = new DocumentSyncSchedulerOptions
        {
            Enabled = true,
            SyncCron = "0 6 * * *",
            BatchSize = 50,
            BatchDelaySeconds = 5,
            ForceSyncIgnoreThreshold = false
        };

        // Act & Assert
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, validationResults, true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_Should_RequireSyncCron()
    {
        // Arrange
        var options = new DocumentSyncSchedulerOptions
        {
            SyncCron = null! // Required field
        };

        // Act & Assert
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(DocumentSyncSchedulerOptions.SyncCron)));
    }

    [Theory]
    [InlineData("30 7 * * *")] // 7:30 AM
    [InlineData("0 0 * * *")]  // Midnight
    [InlineData("*/15 * * * *")] // Every 15 minutes
    [InlineData("0 */2 * * *")] // Every 2 hours
    public void DocumentSyncSchedulerOptions_Should_AcceptValidCronExpressions(string cronExpression)
    {
        // Arrange
        var options = new DocumentSyncSchedulerOptions
        {
            SyncCron = cronExpression
        };

        // Act
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Equal(cronExpression, options.SyncCron);
    }

    [Fact]
    public void DocumentSyncSchedulerOptions_ConfigurationSection_Should_BeCorrect()
    {
        // Assert
        Assert.Equal("DocumentSyncSchedulerSettings", DocumentSyncSchedulerOptions.DocumentSyncScheduler);
    }

    [Fact]
    public void EmailRecapOptions_ConfigurationSection_Should_BeCorrect()
    {
        // Assert
        Assert.Equal("EmailRecapSettings", EmailRecapOptions.EmailRecapSettings);
    }
}
