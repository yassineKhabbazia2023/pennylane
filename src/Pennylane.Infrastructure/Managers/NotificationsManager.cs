// <copyright file="NotificationsManager.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Pennylane.Application.Managers;
using Pennylane.Infrastructure.Configurations.Options;
using Pulse.Back.Events.Abstractions;
using System.Text.Json;

namespace Pennylane.Infrastructure.Managers
{
    /// <summary>
    /// NotificationsManager.
    /// </summary>
    public class NotificationsManager : INotificationManager
    {
        private readonly IEventPublisher eventPublisher;
        private readonly IOptions<BrokerSettingOptions> serviceBusOptions;
        private readonly IAzureClientFactory<ServiceBusSender> azureClientFactory;
        private readonly IAzureClientFactory<ServiceBusClient> clientFactory;
        private readonly ServiceBusClient serviceBusClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationsManager"/> class.
        /// </summary>
        /// <param name="eventPublisher">Init notifications manager instance.</param>
        /// <param name="serviceBusOptions">serviceBusOptions.</param>
        /// <param name="azureClientFactory">azureClientFactory.</param>
        public NotificationsManager(IEventPublisher eventPublisher, IOptions<BrokerSettingOptions> serviceBusOptions, IAzureClientFactory<ServiceBusSender> azureClientFactory, IAzureClientFactory<ServiceBusClient> clientFactory)
        {
            this.eventPublisher = eventPublisher;
            this.serviceBusOptions = serviceBusOptions;
            this.azureClientFactory = azureClientFactory;
            this.clientFactory = clientFactory;
            serviceBusClient = this.clientFactory.CreateClient("Default");
        }

        /// <inheritdoc/>
        public async Task PublishAsync<TEventData>(BaseEvent<TEventData> @event, string? topicName = default!)
        {
            ArgumentNullException.ThrowIfNull(@event);
            await eventPublisher.PublishAsync(@event, correlationId: null, topicName ?? serviceBusOptions.Value.ServiceBusPennylaneTopicName);
        }

        /// <inheritdoc/>
        public async Task PublishToQueueAsync<T>(T message, string? correlationId = null, string? queueName = null)
        {
            var selectedQueue = queueName ?? serviceBusOptions.Value.ServiceBusPennylaneTopicName;
            var sender = azureClientFactory.CreateClient(selectedQueue);
            var messageBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                CorrelationId = correlationId,
            };
            await sender.SendMessageAsync(serviceBusMessage);
        }

        /// <inheritdoc/>
        public async Task BulkPublishAsync(List<ServiceBusMessage> messages, string? topicName = default!)
        {
            ServiceBusMessageBatch? messageBatch = null;

            try
            {
                var selectedTopic = topicName ?? serviceBusOptions.Value.ServiceBusPennylaneTopicName;
                var sender = azureClientFactory.CreateClient(selectedTopic);
                messageBatch = await sender.CreateMessageBatchAsync();
                foreach (var message in messages)
                {
                    var isAdded = messageBatch.TryAddMessage(message);
                    if (!isAdded)
                    {
                        await sender.SendMessagesAsync(messageBatch);
                        messageBatch.Dispose();
                        messageBatch = await sender.CreateMessageBatchAsync();
                        messageBatch.TryAddMessage(message);
                    }
                }

                if (messageBatch.Count > 0)
                {
                    await sender.SendMessagesAsync(messageBatch);
                }
            }
            finally
            {
                messageBatch?.Dispose();
            }
        }
    }
}
