// <copyright file="PullTopic.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pulse.Back.Events.Configurations;

namespace Application.Configurations;

public class PullTopic
{
    public string? TopicName { get; set; }

    public List<string>? Subscriptions { get; set; }

    public List<SubscriptionOptions>? SubscriptionsWithOptions { get; set; }
}
