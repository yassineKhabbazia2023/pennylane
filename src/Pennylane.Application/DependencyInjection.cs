// <copyright file="DependencyInjection.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Remediation;
using Pennylane.Application.Remediation.Checks;
using Pennylane.Application.services;
using Pennylane.Application.Services;
using Pennylane.Application.StaticValues;
using System.Diagnostics.CodeAnalysis;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        AddSetupSettingOptions(services, configuration);
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IPennylaneService, PennylaneService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IAuthorizationUpdatedEventManager, AuthorizationUpdatedEventManager>();
        services.AddScoped<IWebhookProcessingService, WebhookProcessingService>();
        services.AddScoped<IEmailRecapService, EmailRecapService>();
        services.AddScoped<IComplianceEmailService, ComplianceEmailService>();
        services.AddScoped<ICompanyDirectoryService, CompanyDirectoryService>();
        services.AddScoped<ISubscriptionCheck, CompanyIdCheck>();
        services.AddScoped<ISubscriptionCheck, CollaboratorsCheck>();
        services.AddScoped<ISubscriptionCheck, SignatoryAttachmentCheck>();
        services.AddScoped<ISubscriptionCheck, SignatoryRoleCheck>();
        services.AddScoped<ISubscriptionCheck, AuthenticationMethodCheck>();
        services.AddScoped<ISubscriptionResolver, BySirenResolver>();
        services.AddScoped<ICheckProfileRegistry, CheckProfileRegistry>();
        services.AddScoped<ISubscriptionRemediationService, SubscriptionRemediationService>();

        return services;
    }

    private static void AddSetupSettingOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PennylaneServiceOptions>(configuration.GetSection(Constant.PennylaneServiceSettings));
        // Configure ServiceBusOptions
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.ServiceBus));
        services.Configure<PennylaneFirmTokenOptions>(configuration.GetSection(Constant.PennylaneFirms));

        // Configure DocumentSyncOptions with validation
        services.AddOptions<DocumentSyncOptions>()
            .Bind(configuration.GetSection(DocumentSyncOptions.DocumentSync))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // Configure EmailRecapOptions with validation
        services.AddOptions<EmailRecapOptions>()
            .Bind(configuration.GetSection(EmailRecapOptions.EmailRecapSettings))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // Configure DocumentSyncSchedulerOptions with validation
        services.AddOptions<DocumentSyncSchedulerOptions>()
            .Bind(configuration.GetSection(DocumentSyncSchedulerOptions.DocumentSyncScheduler))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.AddOptions<ComplianceEmailOptions>()
            .Bind(configuration.GetSection(ComplianceEmailOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CompanyDirectorySchedulerOptions>()
            .Bind(configuration.GetSection(CompanyDirectorySchedulerOptions.CompanyDirectoryScheduler))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<FreeTrialInfoOptions>(configuration.GetSection(FreeTrialInfoOptions.SectionName));

        services.Configure<DocumentFilteringOptions>(configuration.GetSection(DocumentFilteringOptions.DocumentFiltering));
    }
}