// <copyright file="HangfireJobsConfiguration.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Hangfire;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;

namespace Pennylane.WebApi.Hangfire;

/// <summary>
/// Extension methods for configuring Hangfire recurring jobs.
/// </summary>
public static class HangfireJobsConfiguration
{
    /// <summary>
    /// Registers all recurring jobs for the email recap feature and document sync scheduler.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void ConfigureHangfireJobs(this WebApplication app)
    {
        var emailOptions = app.Services.GetRequiredService<IOptions<EmailRecapOptions>>().Value;
        var syncSchedulerOptions = app.Services.GetRequiredService<IOptions<DocumentSyncSchedulerOptions>>().Value;
        var directoryOptions = app.Services.GetRequiredService<IOptions<CompanyDirectorySchedulerOptions>>().Value;
        var parisTimeZone = TimeZoneHelper.GetParisTimeZone();

        // Email recap jobs
        RecurringJob.AddOrUpdate<IEmailRecapService>(
            recurringJobId: "email-recap-generate",
            methodCall: service => service.GenerateDailyRecapsAsync(),
            cronExpression: emailOptions.GenerationCron,
            options: new RecurringJobOptions { TimeZone = parisTimeZone });

        RecurringJob.AddOrUpdate<IEmailRecapService>(
            recurringJobId: "email-recap-send",
            methodCall: service => service.SendPendingRecapsAsync(),
            cronExpression: emailOptions.SendCron,
            options: new RecurringJobOptions { TimeZone = parisTimeZone });

        // Document sync scheduler job
        if (syncSchedulerOptions.Enabled)
        {
            RecurringJob.AddOrUpdate<ISyncService>(
                recurringJobId: "document-sync-scheduled",
                methodCall: service => service.ScheduledSyncAllSubscriptionsAsync(default),
                cronExpression: syncSchedulerOptions.SyncCron,
                options: new RecurringJobOptions { TimeZone = parisTimeZone });
        }
        else
        {
            RecurringJob.RemoveIfExists("document-sync-scheduled");
        }

        // Company directory refresh scheduler job
        if (directoryOptions.Enabled)
        {
            RecurringJob.AddOrUpdate<ICompanyDirectoryService>(
                recurringJobId: "company-directory-refresh",
                methodCall: service => service.RefreshAsync(default),
                cronExpression: directoryOptions.RefreshCron,
                options: new RecurringJobOptions { TimeZone = parisTimeZone });
        }
        else
        {
            RecurringJob.RemoveIfExists("company-directory-refresh");
        }
    }
}
