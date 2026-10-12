// <copyright file="WebhookPayload.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models.Webhook;

public class WebhookPayload
{
    public User? Before { get; set; }

    public User? After { get; set; }
    
    public required string Event { get; set; }

    public long Timestamp { get; set; }
}
