// <copyright file="BrokerSetting.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Configurations;

namespace Registry.Infrastructure.Options
{
    /// <summary>
    /// BrokerSetting.
    /// </summary>
    public class BrokerSetting
    {
        /// <summary>
        /// Gets or sets fullyQualifiedNamespace.
        /// </summary>
        public string? FullyQualifiedNamespace { get; set; }

        public string? ManagedIdentityClientId { get; set; }

        public List<string>? PushTopicName { get; set; }

        public List<PullTopic>? PullTopics { get; set; }
    }
}
