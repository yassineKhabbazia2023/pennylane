// <copyright file="NotificationsManagerTest.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Moq;
using Notifications.Commons.WebApi.QueryParams;
using Pennylane.Infrastructure.Configurations.Options;
using Pennylane.Infrastructure.Managers;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Tests.Managers
{
    public class NotificationsManagerTest
    {
        [Fact]
        public async Task PublishAsync_CompletedTask()
        {
            // Arrange
            var eventPublisherMock = new Mock<IEventPublisher>();
            var serviceBusOptionsMock = new Mock<IOptions<BrokerSettingOptions>>();
            var senderFactoryMock = new Mock<IAzureClientFactory<ServiceBusSender>>();
            var clientFactoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>();

            var expectedRequest = new EmailRequest
            {
                Cc = new List<string>(),
                From = "from@email.test",
                TemplateName = "template",
                To = new List<string> { "to@test.fr" }
            };
            var expectedEvent = new EmailOnlySenderEvent(expectedRequest);

            eventPublisherMock
                .Setup(x => x.PublishAsync(expectedEvent, It.IsAny<string>(), It.IsAny<string>()))
                .Callback<BaseEvent<EmailRequest>, string, string>((e, cid, topic) =>
                {
                    var cast = (EmailOnlySenderEvent)e;
                    cast.Data.Should().BeEquivalentTo(expectedEvent.Data);
                })
                .Returns(Task.CompletedTask)
                .Verifiable();

            var mgr = new NotificationsManager(
                eventPublisherMock.Object,
                serviceBusOptionsMock.Object,
                senderFactoryMock.Object,
                clientFactoryMock.Object);

            // Act
            await mgr.PublishAsync(expectedEvent, topicName: "test-topic");

            // Assert
            eventPublisherMock.Verify(x =>
                x.PublishAsync(expectedEvent, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PublishToQueueAsync_CompletedTask()
        {
            // Arrange
            var eventPublisherMock = new Mock<IEventPublisher>();
            var serviceBusOptionsMock = new Mock<IOptions<BrokerSettingOptions>>();
            var senderFactoryMock = new Mock<IAzureClientFactory<ServiceBusSender>>();
            var clientFactoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>();

            var payload = new EmailRequest
            {
                Cc = new List<string>(),
                From = "from@email.test",
                TemplateName = "template",
                To = new List<string> { "to@test.fr" }
            };

            var mockSender = new Mock<ServiceBusSender>();
            mockSender
                .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            senderFactoryMock
                .Setup(f => f.CreateClient("my-queue"))
                .Returns(mockSender.Object)
                .Verifiable();

            var mgr = new NotificationsManager(
                eventPublisherMock.Object,
                serviceBusOptionsMock.Object,
                senderFactoryMock.Object,
                clientFactoryMock.Object);

            // Act
            await mgr.PublishToQueueAsync(payload, correlationId: "cid", queueName: "my-queue");

            // Assert
            mockSender.Verify(s =>
                s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BulkPublishAsync_CompletedTask()
        {
            // Arrange
            var eventPublisherMock = new Mock<IEventPublisher>();
            var serviceBusOptionsMock = new Mock<IOptions<BrokerSettingOptions>>();
            var senderFactoryMock = new Mock<IAzureClientFactory<ServiceBusSender>>();
            var clientFactoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>();

            // Prépare un batch qui acceptera au moins 1 message
            var backingList = new List<ServiceBusMessage>();
            var mockBatch = ServiceBusModelFactory.ServiceBusMessageBatch(
                batchSizeBytes: 1024,
                batchMessageStore: backingList,
                batchOptions: new CreateMessageBatchOptions(),
                tryAddCallback: _ => backingList.Count < 10);
            var mockSender = new Mock<ServiceBusSender>();
            mockSender
                .Setup(s => s.CreateMessageBatchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockBatch);
            mockSender
                .Setup(s => s.SendMessagesAsync(It.IsAny<ServiceBusMessageBatch>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            senderFactoryMock
                .Setup(f => f.CreateClient("my-topic"))
                .Returns(mockSender.Object);

            var messages = new List<ServiceBusMessage> { new ServiceBusMessage("foo") };

            var mgr = new NotificationsManager(
                eventPublisherMock.Object,
                serviceBusOptionsMock.Object,
                senderFactoryMock.Object,
                clientFactoryMock.Object);

            // Act
            await mgr.BulkPublishAsync(messages, topicName: "my-topic");

            // Assert
            senderFactoryMock.Verify(f => f.CreateClient("my-topic"), Times.Once);
            mockSender.Verify(s => s.CreateMessageBatchAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockSender.Verify(s => s.SendMessagesAsync(It.IsAny<ServiceBusMessageBatch>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
