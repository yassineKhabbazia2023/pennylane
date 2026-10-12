// <copyright file="IWebhookProcessingService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.Webhook;

namespace Pennylane.Application.Interfaces
{
    public interface IWebhookProcessingService
    {
        Task ProcessEvent(string eventName, WebhookPayload payload);
    }
}
