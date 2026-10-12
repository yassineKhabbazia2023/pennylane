// <copyright file="EmailRecapServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Notifications.Commons.WebApi.QueryParams;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.EmailRecap;
using Pennylane.Application.Helpers;
using Pennylane.Application.Services;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Enums;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Tests.Services;

public class EmailRecapServiceTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IEmailRecapRepository> _emailRecapRepositoryMock;
    private readonly Mock<IOptions<EmailRecapOptions>> _optionsMock;
    private readonly Mock<ILogger<EmailRecapService>> _loggerMock;
    private readonly Fixture _fixture;
    private readonly EmailRecapService _sut;

    public EmailRecapServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors
            .OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _authorizationServiceMock = new Mock<IAuthorizationService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _emailRecapRepositoryMock = new Mock<IEmailRecapRepository>();
        _optionsMock = new Mock<IOptions<EmailRecapOptions>>();
        _loggerMock = new Mock<ILogger<EmailRecapService>>();

        var options = new EmailRecapOptions
        {
            LookbackHours = 24,
            LoginUrl = "https://app.rydge.com/login",
            SenderEmail = "noreply@rydge.com",
            TemplateName = "documents-summary-recap",
            MaxParallelEmailSends = 5
        };
        _optionsMock.Setup(x => x.Value).Returns(options);

        _sut = new EmailRecapService(
            _documentRepositoryMock.Object,
            _authorizationServiceMock.Object,
            _eventPublisherMock.Object,
            _emailRecapRepositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);
    }

    #region GenerateDailyRecapsAsync Tests

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenDisabled_ReturnsEmptyWithoutProcessing()
    {
        // Arrange
        var disabledOptions = new EmailRecapOptions
        {
            Enabled = false,
            LookbackHours = 24,
            LoginUrl = "https://app.rydge.com/login",
            SenderEmail = "noreply@rydge.com",
            TemplateName = "documents-summary-recap"
        };

        var optionsMock = new Mock<IOptions<EmailRecapOptions>>();
        optionsMock.Setup(x => x.Value).Returns(disabledOptions);

        var service = new EmailRecapService(
            _documentRepositoryMock.Object,
            _authorizationServiceMock.Object,
            _eventPublisherMock.Object,
            _emailRecapRepositoryMock.Object,
            optionsMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TotalItems.Should().Be(0);

        _emailRecapRepositoryMock.Verify(
            x => x.HasBatchForDateAsync(It.IsAny<DateTime>()),
            Times.Never);

        _documentRepositoryMock.Verify(
            x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenNoDocuments_ReturnsEmptyResult()
    {
        // Arrange
        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, DocumentCountByAccount>());

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TotalItems.Should().Be(0);
        result.TotalDocuments.Should().Be(0);
        result.AccountsWithDocuments.Should().Be(0);

        _authorizationServiceMock.Verify(
            x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenBatchAlreadyExists_ReturnsFailedResult()
    {
        // Arrange
        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Batch already exists");

        _documentRepositoryMock.Verify(
            x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenDocumentsExist_CallsAuthorizationService()
    {
        // Arrange
        var documentCounts = new Dictionary<int, DocumentCountByAccount>
        {
            { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) },
            { 2, new DocumentCountByAccount(2, "ACC002", "Company B", 3) }
        };

        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentCounts);

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyList<AuthorizedContact>>());

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        _authorizationServiceMock.Verify(
            x => x.GetContactsForAccountsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2))),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenContactsFound_CreatesEmailItems()
    {
        // Arrange
        var documentCounts = new Dictionary<int, DocumentCountByAccount>
        {
            { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) }
        };

        var contacts = new Dictionary<int, IReadOnlyList<AuthorizedContact>>
        {
            {
                1, new List<AuthorizedContact>
                {
                    new AuthorizedContact(100, "client@test.com", "Jean", "Dupont", "Customer")
                }
            }
        };

        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentCounts);

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(contacts);

        _emailRecapRepositoryMock
            .Setup(x => x.AddItemsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<EmailRecapItemEntity>>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TotalItems.Should().Be(1);
        result.TotalDocuments.Should().Be(5);
        result.AccountsWithDocuments.Should().Be(1);

        _emailRecapRepositoryMock.Verify(
            x => x.AddItemsAsync(1, It.Is<IEnumerable<EmailRecapItemEntity>>(items =>
                items.Count() == 1 &&
                items.First().ContactEmail == "client@test.com" &&
                items.First().TotalDocuments == 5)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenContactHasMultipleAccounts_AggregatesByEmail()
    {
        // Arrange
        var documentCounts = new Dictionary<int, DocumentCountByAccount>
        {
            { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) },
            { 2, new DocumentCountByAccount(2, "ACC002", "Company B", 3) }
        };

        var sameContact = new AuthorizedContact(100, "client@test.com", "Jean", "Dupont", "Customer");
        var contacts = new Dictionary<int, IReadOnlyList<AuthorizedContact>>
        {
            { 1, new List<AuthorizedContact> { sameContact } },
            { 2, new List<AuthorizedContact> { sameContact } }
        };

        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentCounts);

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(contacts);

        _emailRecapRepositoryMock
            .Setup(x => x.AddItemsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<EmailRecapItemEntity>>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TotalItems.Should().Be(1); // Un seul email pour le même contact

        _emailRecapRepositoryMock.Verify(
            x => x.AddItemsAsync(1, It.Is<IEnumerable<EmailRecapItemEntity>>(items =>
                items.Count() == 1 &&
                items.First().TotalDocuments == 8 && // 5 + 3
                items.First().AccountCount == 2)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenNoAuthorizedContacts_ReturnsZeroItems()
    {
        // Arrange
        var documentCounts = new Dictionary<int, DocumentCountByAccount>
        {
            { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) }
        };

        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentCounts);

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyList<AuthorizedContact>>());

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TotalItems.Should().Be(0);
        result.AccountsWithDocuments.Should().Be(1);

        _emailRecapRepositoryMock.Verify(
            x => x.AddItemsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<EmailRecapItemEntity>>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenExceptionThrown_MarksBatchAsFailed()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, DocumentCountByAccount>
            {
                { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) }
            });

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ThrowsAsync(new Exception("API Error"));

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API Error");

        _emailRecapRepositoryMock.Verify(
            x => x.UpdateBatchAsync(It.Is<EmailRecapBatchEntity>(b =>
                b.Status == (int)EmailRecapBatchStatus.Failed &&
                b.ErrorMessage == "API Error")),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_WhenContactHasEmptyEmail_SkipsContact()
    {
        // Arrange
        var documentCounts = new Dictionary<int, DocumentCountByAccount>
        {
            { 1, new DocumentCountByAccount(1, "ACC001", "Company A", 5) }
        };

        var contacts = new Dictionary<int, IReadOnlyList<AuthorizedContact>>
        {
            {
                1, new List<AuthorizedContact>
                {
                    new AuthorizedContact(100, "", "Jean", "Dupont", "Customer"),
                    new AuthorizedContact(101, "   ", "Marie", "Martin", "Customer"),
                    new AuthorizedContact(102, "valid@test.com", "Pierre", "Bernard", "Customer")
                }
            }
        };

        var batch = new EmailRecapBatchEntity { Id = 1, Status = (int)EmailRecapBatchStatus.Generating };

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentCounts);

        _emailRecapRepositoryMock
            .Setup(x => x.CreateBatchAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(batch);

        _authorizationServiceMock
            .Setup(x => x.GetContactsForAccountsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(contacts);

        _emailRecapRepositoryMock
            .Setup(x => x.AddItemsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<EmailRecapItemEntity>>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateDailyRecapsAsync();

        // Assert
        result.TotalItems.Should().Be(1); // Seulement le contact avec email valide

        _emailRecapRepositoryMock.Verify(
            x => x.AddItemsAsync(1, It.Is<IEnumerable<EmailRecapItemEntity>>(items =>
                items.Count() == 1 &&
                items.First().ContactEmail == "valid@test.com")),
            Times.Once);
    }

    #endregion

    #region SendPendingRecapsAsync Tests

    [Fact]
    public async Task SendPendingRecapsAsync_WhenNoReadyBatches_ReturnsEmptyResult()
    {
        // Arrange
        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity>());

        // Act
        var result = await _sut.SendPendingRecapsAsync();

        // Assert
        result.TotalItems.Should().Be(0);
        result.SentCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task SendPendingRecapsAsync_WhenBatchHasItems_SendsEmails()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 2
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client1@test.com",
                ContactName = "Client 1",
                PayloadJson = "{\"UserName\":\"Client 1\",\"LoginUrl\":\"https://test.com\",\"DocumentsDepositDate\":\"01 janvier 2024\",\"SummaryLines\":[]}",
                Status = (int)EmailRecapItemStatus.Pending
            },
            new EmailRecapItemEntity
            {
                Id = 2,
                BatchId = 1,
                ContactEmail = "client2@test.com",
                ContactName = "Client 2",
                PayloadJson = "{\"UserName\":\"Client 2\",\"LoginUrl\":\"https://test.com\",\"DocumentsDepositDate\":\"01 janvier 2024\",\"SummaryLines\":[]}",
                Status = (int)EmailRecapItemStatus.Pending
            }
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        _eventPublisherMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SendPendingRecapsAsync();

        // Assert
        result.SentCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), "feedcenter"),
            Times.Exactly(2));
    }


    [Fact]
    public async Task SendPendingRecapsAsync_WhenEmailThrowsException_RecordsError()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 1
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client@test.com",
                ContactName = "Client",
                PayloadJson = "{\"UserName\":\"Client\",\"LoginUrl\":\"https://test.com\",\"DocumentsDepositDate\":\"01 janvier 2024\",\"SummaryLines\":[]}",
                Status = (int)EmailRecapItemStatus.Pending
            }
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        _eventPublisherMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Service Bus Error"));

        // Act
        var result = await _sut.SendPendingRecapsAsync();

        // Assert
        result.SentCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("Service Bus Error"));
    }

    [Fact]
    public async Task SendPendingRecapsAsync_WhenBatchHasNoPendingItems_CompletesWithoutSending()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 0
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(new List<EmailRecapItemEntity>());

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SendPendingRecapsAsync();

        // Assert
        result.SentCount.Should().Be(0);
        result.FailedCount.Should().Be(0);

        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);

        // Le batch passe par Sending puis Completed, donc UpdateBatchAsync est appelé 2 fois
        _emailRecapRepositoryMock.Verify(
            x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendPendingRecapsAsync_UpdatesBatchStatusToSendingThenCompleted()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 1
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client@test.com",
                ContactName = "Client",
                PayloadJson = "{\"UserName\":\"Client\",\"LoginUrl\":\"https://test.com\",\"DocumentsDepositDate\":\"01 janvier 2024\",\"SummaryLines\":[]}",
                Status = (int)EmailRecapItemStatus.Pending
            }
        };

        var statusUpdates = new List<EmailRecapBatchStatus>();

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Callback<EmailRecapBatchEntity>(b => statusUpdates.Add((EmailRecapBatchStatus)b.Status))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        _eventPublisherMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendPendingRecapsAsync();

        // Assert
        statusUpdates.Should().HaveCount(2);
        statusUpdates[0].Should().Be(EmailRecapBatchStatus.Sending);
        statusUpdates[1].Should().Be(EmailRecapBatchStatus.Completed);
    }

    [Fact]
    public async Task SendPendingRecapsAsync_WhenInvalidPayload_FailsItem()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 1
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client@test.com",
                ContactName = "Client",
                PayloadJson = "invalid json",
                Status = (int) EmailRecapItemStatus.Pending
            }
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SendPendingRecapsAsync();

        // Assert
        result.FailedCount.Should().Be(1);

        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task SendPendingRecapsAsync_WhenDisabled_ReturnsEmptyWithoutProcessing()
    {
        // Arrange
        var disabledOptions = new EmailRecapOptions
        {
            Enabled = false,
            LookbackHours = 24,
            LoginUrl = "https://app.rydge.com/login",
            SenderEmail = "noreply@rydge.com",
            TemplateName = "documents-summary-recap"
        };

        var optionsMock = new Mock<IOptions<EmailRecapOptions>>();
        optionsMock.Setup(x => x.Value).Returns(disabledOptions);

        var service = new EmailRecapService(
            _documentRepositoryMock.Object,
            _authorizationServiceMock.Object,
            _eventPublisherMock.Object,
            _emailRecapRepositoryMock.Object,
            optionsMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.SendPendingRecapsAsync();

        // Assert
        result.TotalItems.Should().Be(0);
        result.SentCount.Should().Be(0);

        _emailRecapRepositoryMock.Verify(
            x => x.GetBatchesByStatusAsync(It.IsAny<EmailRecapBatchStatus>()),
            Times.Never);
    }

    #endregion

    #region Email Payload Tests

    [Fact]
    public async Task SendPendingRecapsAsync_BuildsCorrectEmailRequest()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 1
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client@test.com",
                ContactName = "Jean Dupont",
                PayloadJson = "{\"IsRecipientCustomer\":true,\"UserName\":\"Jean Dupont\",\"LoginUrl\":\"https://app.rydge.com/login\",\"TitleDocumentsTypeName\":\"Pennylane\",\"SummaryType\":\"GED Pennylane\",\"DepositedDocumentsCount\":5,\"DocumentsDepositDate\":\"01 janvier 2024\",\"SummaryLines\":[{\"EntityName\":\"Company A\",\"DepositedDocumentsCount\":5}]}",
                Status = (int)EmailRecapItemStatus.Pending
            }
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        _eventPublisherMock
            .Setup(x => x.PublishAsync(
                It.Is<EmailOnlySenderEvent>(e =>
                    e.Data.To.Contains("client@test.com") &&
                    e.Data.From == "noreply@rydge.com" &&
                    e.Data.TemplateName == "documents-summary-recap" &&
                    e.Data.Variables.ContainsKey("userName") &&
                    (string)e.Data.Variables["userName"] == "Jean Dupont" &&
                    e.Data.Variables.ContainsKey("summaryLines")),
                It.IsAny<string?>(),
                "feedcenter"))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendPendingRecapsAsync();

        // Assert
        _eventPublisherMock.Verify(
            x => x.PublishAsync(
                It.Is<EmailOnlySenderEvent>(e =>
                    e.Data.To.Contains("client@test.com") &&
                    e.Data.From == "noreply@rydge.com" &&
                    e.Data.TemplateName == "documents-summary-recap"),
                It.IsAny<string?>(),
                "feedcenter"),
            Times.Once);
    }

    [Fact]
    public async Task SendPendingRecapsAsync_SummaryLines_SerializedCorrectlyWithData()
    {
        // Arrange
        var batch = new EmailRecapBatchEntity
        {
            Id = 1,
            Status = (int)EmailRecapBatchStatus.Ready,
            TotalItems = 1
        };

        var items = new List<EmailRecapItemEntity>
        {
            new EmailRecapItemEntity
            {
                Id = 1,
                BatchId = 1,
                ContactEmail = "client@test.com",
                ContactName = "Test Client",
                PayloadJson = "{\"IsRecipientCustomer\":true,\"UserName\":\"Test Client\",\"LoginUrl\":\"https://app.rydge.com/login\",\"TitleDocumentsTypeName\":\"Pennylane\",\"SummaryType\":\"Comptabilité\",\"DepositedDocumentsCount\":10,\"DocumentsDepositDate\":\"10 décembre 2025\",\"SummaryLines\":[{\"entityName\":\"Company A\",\"depositedDocumentsCount\":5},{\"entityName\":\"Company B\",\"depositedDocumentsCount\":5}]}",
                Status = (int)EmailRecapItemStatus.Pending
            }
        };

        _emailRecapRepositoryMock
            .Setup(x => x.GetBatchesByStatusAsync(EmailRecapBatchStatus.Ready))
            .ReturnsAsync(new List<EmailRecapBatchEntity> { batch });

        _emailRecapRepositoryMock
            .Setup(x => x.GetPendingItemsAsync(1))
            .ReturnsAsync(items);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateBatchAsync(It.IsAny<EmailRecapBatchEntity>()))
            .Returns(Task.CompletedTask);

        _emailRecapRepositoryMock
            .Setup(x => x.UpdateItemsStatusAsync(It.IsAny<IEnumerable<(int, EmailRecapItemStatus, string?)>>()))
            .Returns(Task.CompletedTask);

        EmailOnlySenderEvent? capturedEvent = null;
        _eventPublisherMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?, string?>((evt, _, _) => capturedEvent = evt as EmailOnlySenderEvent)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendPendingRecapsAsync();

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Data.Variables.Should().ContainKey("summaryLines");

        var json = System.Text.Json.JsonSerializer.Serialize(capturedEvent);

        json.Should().NotContain("\"summaryLines\":[]");
        json.Should().NotContain("[[[[");
        json.Should().Contain("\"Company A\"");
        json.Should().Contain("\"Company B\"");
        json.Should().Contain(":5");
        json.Should().Contain("\"entityName\":");
        json.Should().Contain("\"depositedDocumentsCount\":");
    }

    #endregion

    #region LookbackPeriod Tests

    [Fact]
    public async Task GenerateDailyRecapsAsync_LookbackPeriod_QueriesCorrectDateRange()
    {
        // Arrange
        var options = new EmailRecapOptions
        {
            Enabled = true,
            ReferenceHour = 9,
            LookbackHours = 24,
            LoginUrl = "https://app.rydge.com/login",
            SenderEmail = "noreply@rydge.com",
            TemplateName = "documents-summary-recap"
        };

        var optionsMock = new Mock<IOptions<EmailRecapOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);

        var service = new EmailRecapService(
            _documentRepositoryMock.Object,
            _authorizationServiceMock.Object,
            _eventPublisherMock.Object,
            _emailRecapRepositoryMock.Object,
            optionsMock.Object,
            _loggerMock.Object);

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        DateTime capturedFromUtc = default;
        DateTime capturedToUtc = default;
        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((from, to) =>
            {
                capturedFromUtc = from;
                capturedToUtc = to;
            })
            .ReturnsAsync(new Dictionary<int, DocumentCountByAccount>());

        // Act
        await service.GenerateDailyRecapsAsync();

        // Assert
        var expectedDuration = TimeSpan.FromHours(options.LookbackHours);
        var actualDuration = capturedToUtc - capturedFromUtc;
        actualDuration.Should().Be(expectedDuration);

        var parisTimeZone = TimeZoneHelper.GetParisTimeZone();
        var todayParis = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var expectedToUtc = TimeZoneInfo.ConvertTimeToUtc(todayParis.AddHours(options.ReferenceHour), parisTimeZone);
        capturedToUtc.TimeOfDay.Hours.Should().Be(expectedToUtc.TimeOfDay.Hours);
    }

    [Fact]
    public async Task GenerateDailyRecapsAsync_LookbackPeriod_CoversYesterdayToToday()
    {
        // Arrange
        var options = new EmailRecapOptions
        {
            Enabled = true,
            ReferenceHour = 9,
            LookbackHours = 24,
            LoginUrl = "https://app.rydge.com/login",
            SenderEmail = "noreply@rydge.com",
            TemplateName = "documents-summary-recap"
        };

        var optionsMock = new Mock<IOptions<EmailRecapOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);

        var service = new EmailRecapService(
            _documentRepositoryMock.Object,
            _authorizationServiceMock.Object,
            _eventPublisherMock.Object,
            _emailRecapRepositoryMock.Object,
            optionsMock.Object,
            _loggerMock.Object);

        _emailRecapRepositoryMock
            .Setup(x => x.HasBatchForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        DateTime capturedFromUtc = default;
        DateTime capturedToUtc = default;
        _documentRepositoryMock
            .Setup(x => x.GetDocumentCountsByAccountAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((from, to) =>
            {
                capturedFromUtc = from;
                capturedToUtc = to;
            })
            .ReturnsAsync(new Dictionary<int, DocumentCountByAccount>());

        // Act
        await service.GenerateDailyRecapsAsync();

        // Assert — le service calcule la fenêtre en heure de Paris, pas en UTC
        var parisTimeZone = Pennylane.Application.Helpers.TimeZoneHelper.GetParisTimeZone();
        var todayParis = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, parisTimeZone).Date;
        capturedToUtc.Date.Should().Be(todayParis);
        capturedFromUtc.Date.Should().Be(todayParis.AddDays(-1));
    }

    #endregion
}
