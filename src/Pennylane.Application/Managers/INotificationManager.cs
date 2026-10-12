// <copyright file="INotificationManager.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Azure.Messaging.ServiceBus;
using Pulse.Back.Events.Abstractions;

namespace Pennylane.Application.Managers
{
    /// <summary>
    /// INotificationManager.
    /// </summary>
    public interface INotificationManager
    {
        /// <summary>
        /// Publishes a message to Azure Service Bus.
        /// </summary>
        /// <typeparam name="TEventData">The type of the event to be published.</typeparam>
        /// <param name="baseEvent">The event to be published.</param>
        /// <param name="topicName">The name of the topic to publish the message to (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="baseEvent"/> is null.</exception>
        Task PublishAsync<TEventData>(BaseEvent<TEventData> baseEvent, string? topicName = default!);

        /// <summary>
        /// Publishes a message to Azure Service Queue.
        /// </summary>
        /// <typeparam name="T">The type of the message to be published.</typeparam>
        /// <param name="message">The message to be published.</param>
        /// <param name="correlationId">The correlation ID for the message (optional).</param>
        /// <param name="queueName">The name of the queue to publish the message to (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="baseEvent"/> is null.</exception>
        Task PublishToQueueAsync<T>(T message, string? correlationId = null, string? queueName = null);

        /// <summary>
        /// Publishes messages in batch to Azure Service Topic.
        /// </summary>
        /// <param name="messages">messages.</param>
        /// <param name="topicName">topicName.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BulkPublishAsync(List<ServiceBusMessage> messages, string? topicName = default!);
    }
}
