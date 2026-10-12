// <copyright file="DependencyInjection.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Repository;
using Azure.Identity;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pennylane.Application;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.services;
using Pennylane.Infrastructure.Configurations.Options;
using Pennylane.Infrastructure.ExternalServices;
using Pennylane.Infrastructure.Handlers;
using Pennylane.Infrastructure.Handlers.Permissions;
using Pennylane.Infrastructure.Managers;
using Pennylane.Infrastructure.Options;
using Pennylane.Infrastructure.Providers;
using Pennylane.Infrastructure.Repository;
using Pennylane.Infrastructure.Services;
using Pulse.Back.Events;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.Configurations;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.ContactRegistry.Infrastructure.Context;
using Registry.Infrastructure.Options;
using System.Diagnostics.CodeAnalysis;

namespace Pennylane.Infrastructure.Configurations;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationServices(configuration);
        services.AddServiceBusConfiguration(configuration);

        ArgumentException.ThrowIfNullOrEmpty(configuration["DatabaseConnectionString"]);
        services.AddDatabaseConfigurations(configuration);
        services.AddRegistrationServiceApi(configuration);
        services.RegisterBlobStorageConfig(configuration);

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPennylaneRepository, PennylaneRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyDirectoryRepository, CompanyDirectoryRepository>();
        services.AddScoped<IEmailRecapRepository, EmailRecapRepository>();
        services.AddScoped<ISignatoryAccessesImportRepository, SignatoryAccessesImportRepository>();
        services.AddScoped<INotificationManager, NotificationsManager>();
        services.AddHttpClient<IDocumentStreamService, DocumentStreamService>();
        services.AddScoped<ISignatoryAccessesImportService, SignatoryAccessesImportService>();

        services.AddScoped<IPennylaneGedPermissionRepository, PennylaneGedPermissionRepository>();
        services.AddScoped<IAuthorizationService, PennylaneGedAuthorizationService>();
        services.AddScoped<IPermissionHandler, PennylaneGedPermissionHandler>();

        services.AddKeyedScoped<IEventHandler, AccountCreatedEventHandler>(nameof(AccountCreatedEvent));
        services.AddKeyedScoped<IEventHandler, AccountUpdatedEventHandler>(nameof(AccountUpdatedEvent));
        services.AddKeyedScoped<IEventHandler, AccountRemovedEventHandler>(nameof(AccountRemovedEvent));
        services.AddKeyedScoped<IEventHandler, RoleCreatedEventHandler>(nameof(RoleCreatedEvent));
        services.AddKeyedScoped<IEventHandler, RoleDeletedEventHandler>(nameof(RoleDeletedEvent));
        services.AddKeyedScoped<IEventHandler, ContactCreatedEventHandler>(nameof(ContactCreatedEvent));
        services.AddKeyedScoped<IEventHandler, ContactUpdatedEventHandler>(nameof(ContactUpdatedEvent));
        services.AddKeyedScoped<IEventHandler, ContactRemovedEventHandler>(nameof(ContactRemovedEvent));
        services.AddKeyedScoped<IEventHandler, AuthorizationUpdatedEventHandler>(nameof(AuthorizationUpdatedEvent));
        services.AddKeyedScoped<IEventHandler, SubscriptionValidatedEventHandler>(nameof(SubscriptionValidatedEvent));
        services.AddKeyedScoped<IEventHandler, SubscriptionDeactivatedEventHandler>(nameof(SubscriptionDeactivatedEvent));
        services.AddKeyedScoped<IEventHandler, DocumentSyncEventHandler>(nameof(PennylaneDocumentSyncEvent));
        services.AddKeyedScoped<IEventHandler, SubscriptionCheckRequestedEventHandler>(nameof(PennylaneSubscriptionCheckRequestedEvent));

        services.AddScoped<IHistoryEventPublisher, HistoryEventPublisher>();
    }

    private static void AddDatabaseConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PennylaneContext>(

                     options =>
                     {
                         options.UseSqlServer(

                             configuration["DatabaseConnectionString"], sqlServerOptionsAction: sqlOptions =>
                             {
                                 sqlOptions.MigrationsAssembly(typeof(PennylaneContext).Assembly.FullName);

                                 sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);

                                 sqlOptions.CommandTimeout(120);
                             });
                     }, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Extension to configure applicationInsight.
    /// </summary>
    /// <param name="services">IServiceCollection./param>.
    /// <param name="configuration">configuration.</param>.
    public static void AddServiceBusConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var brokerSettings = configuration!.GetSection("BrokerSetting").Get<BrokerSettingOptions>();
        ArgumentException.ThrowIfNullOrEmpty(brokerSettings!.FullyQualifiedNamespace);
        ArgumentException.ThrowIfNullOrEmpty(brokerSettings!.ServiceBusPennylaneTopicName);

        var connectToResourcesViaManagedIdentity = configuration["ConnectToResourcesViaManagedIdentity"];
        bool useManagedIdentity = string.Equals(connectToResourcesViaManagedIdentity, "true", StringComparison.InvariantCultureIgnoreCase);

        BrokerOptions options;
        if (useManagedIdentity)
        {
            options = new BrokerOptions
            {
                ServiceBusNamespace = brokerSettings!.FullyQualifiedNamespace!,
                ManagedIdentityClientId = brokerSettings!.ManagedIdentityClientId!,
                PushTopicNames = brokerSettings!.PushTopicName ?? new List<string>(),
            };
            services.LoadPullTopics(options, brokerSettings);
        }
        else if (!string.IsNullOrEmpty(brokerSettings.ConnectionString))
        {
            options = new BrokerOptions
            {
                ServiceBusConnectionString = brokerSettings!.ConnectionString!,
                PushTopicNames = brokerSettings!.PushTopicName ?? new List<string>(),
            };
            services.LoadPullTopics(options, brokerSettings);
        }
    }

    private static void LoadPullTopics(this IServiceCollection services, BrokerOptions options, BrokerSettingOptions brokerSettings)
    {
        if (brokerSettings.PullTopics?.Count != 0)
        {
            foreach (var topic in brokerSettings.PullTopics!)
            {
                if (!string.IsNullOrEmpty(topic?.TopicName) && !options.PullTopics.ContainsKey(topic.TopicName))
                {
                    if (topic.SubscriptionsWithOptions?.Count > 0)
                    {
                        options.AddPullTopicItem(topic.TopicName!, topic.SubscriptionsWithOptions);
                    }
                    else if (topic.Subscriptions?.Count > 0)
                    {
                        options.AddPullTopicItem(topic.TopicName!, topic.Subscriptions!);
                    }
                }
            }
        }

        services.AddEventPullServices(options);
        services.AddEventPushServices(options);
    }

    private static void RegisterBlobStorageConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BlobStorageOptions>().BindConfiguration("BlobStorage");

        var brokerSettings = configuration!.GetSection("BrokerSetting").Get<BrokerSetting>();
        var blobStorage = configuration!.GetSection("BlobStorage").Get<BlobStorageOptions>() ?? throw new ArgumentException("BlobStorage parameters should been provided");
        ArgumentException.ThrowIfNullOrEmpty(blobStorage!.BlobUri);
        ArgumentException.ThrowIfNullOrEmpty(brokerSettings?.ManagedIdentityClientId);

        services.AddAzureClients(delegate (AzureClientFactoryBuilder builder)
        {
            builder.AddBlobServiceClient(new Uri(blobStorage!.BlobUri))
                    .WithCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = brokerSettings.ManagedIdentityClientId,
                    }));
        });

        services.AddScoped<IBlobStorageManager, BlobStorageManager>();
    }
}