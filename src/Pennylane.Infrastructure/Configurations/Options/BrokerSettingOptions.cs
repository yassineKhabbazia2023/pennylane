// <copyright file="BrokerSettingOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Configurations;

namespace Pennylane.Infrastructure.Configurations.Options;

/// <summary>
/// BrokerSetting.
/// </summary>
public class BrokerSettingOptions
{
    /// <summary>
    /// Gets or sets fullyQualifiedNamespace.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    public string? ManagedIdentityClientId { get; set; }

    public string? ConnectionString { get; set; }

    public List<string>? PushTopicName { get; set; }

    public List<PullTopic>? PullTopics { get; set; }

    public string? ServiceBusPennylaneTopicName { get; set; }
}