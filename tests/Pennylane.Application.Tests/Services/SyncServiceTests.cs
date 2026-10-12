using Application.Interfaces;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models;
using Pennylane.Application.services;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Tests.Services;

public class SyncServiceTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IAccountRepository> _accountRepositoryMock;
    private readonly Mock<INotificationManager> _notificationManagerMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IOptions<ServiceBusOptions>> _optionsMock;
    private readonly Mock<IOptions<DocumentSyncSchedulerOptions>> _schedulerOptionsMock;
    private readonly Mock<ILogger<SyncService>> logger;
    private readonly SyncService _syncService;
    private readonly Fixture _fixture;

    public SyncServiceTests()
    {
        _fixture = new Fixture();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _accountRepositoryMock = new Mock<IAccountRepository>();
        _notificationManagerMock = new Mock<INotificationManager>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _optionsMock = new Mock<IOptions<ServiceBusOptions>>();
        _schedulerOptionsMock = new Mock<IOptions<DocumentSyncSchedulerOptions>>();
        logger = new Mock<ILogger<SyncService>>();

        _optionsMock.Setup(o => o.Value).Returns(new ServiceBusOptions
        {
            SyncThresholdMinutes = 30
        });

        _schedulerOptionsMock.Setup(o => o.Value).Returns(new DocumentSyncSchedulerOptions
        {
            Enabled = true,
            SyncCron = "30 7 * * *",
            BatchSize = 100,
            BatchDelaySeconds = 10,
            ForceSyncIgnoreThreshold = true
        });

        _syncService = new SyncService(
            logger.Object,
            _documentRepositoryMock.Object,
            _accountRepositoryMock.Object,
            _notificationManagerMock.Object,
            _optionsMock.Object,
            _subscriptionRepositoryMock.Object,
            _schedulerOptionsMock.Object);
    }

    [Fact]
    public async Task CheckAndPushSyncMessageAsync_Should_NotPublishToQueue_When_SyncIsNotNeeded()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var accountId = _fixture.Create<int>();

        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
        .ReturnsAsync(new Domain.Entities.Pennylane.DocumentsSyncStatusEntity
        {
            AccountNumber = accountNumber, // Assurez-vous de définir AccountNumber
            LastSynchro = DateTime.UtcNow
        });


        _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = accountNumber });

        // Act
        var result = await _syncService.CheckAndPushSyncMessageAsync(accountNumber, false);

        // Assert
        Assert.False(result); // Pas de message publié
    _notificationManagerMock.Verify(
        manager => manager.PublishAsync(
            It.IsAny<PennylaneDocumentSyncEvent>(),
            It.IsAny<string>()),
        Times.Never);
    }

    [Fact]
    public async Task CheckAndPushSyncMessageAsync_Should_NotPublishToQueue_When_AccountIsNull()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();

        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
            .ReturnsAsync((DocumentsSyncStatusEntity)null);

            _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
            .ReturnsAsync( (AccountEntity) null);

        // Act
        var result = await _syncService.CheckAndPushSyncMessageAsync(accountNumber, false);

        // Assert
        Assert.False(result); // Compte null, pas de message publié
    _notificationManagerMock.Verify(
        manager => manager.PublishAsync(
            It.IsAny<PennylaneDocumentSyncEvent>(),
            It.IsAny<string>()),
        Times.Never);
    }

    [Fact]
    public async Task CheckAndPushSyncMessageAsync_Should_Handle_Exceptions_Gracefully()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();

        var exceptionMessage = "Database error";

        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _syncService.CheckAndPushSyncMessageAsync(accountNumber, false);

        // Assert
        Assert.False(result); // Exception capturée, retourne false
        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Never);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to check and push sync message")),
                It.Is<Exception>(ex => ex.Message == exceptionMessage),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndPushSyncMessageAsync_WithForceSyncTrue_Should_PublishToQueue_IgnoringThreshold()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var accountId = _fixture.Create<int>();

        // Sync récent (moins de 30 minutes)
        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
            .ReturnsAsync(new DocumentsSyncStatusEntity
            {
                AccountNumber = accountNumber,
                LastSynchro = DateTime.UtcNow.AddMinutes(-5) // Seulement 5 minutes
            });

        _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
            .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = accountNumber });

        _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(accountId, accountNumber))
            .Returns(Task.CompletedTask);

        // Act - avec forceSync = true
        var result = await _syncService.CheckAndPushSyncMessageAsync(accountNumber, forceSync: true);

        // Assert - doit publier malgré le seuil
        Assert.True(result); // Message publié avec succès
        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Once);

        _documentRepositoryMock.Verify(
            repo => repo.AddOrUpdateSyncDateAsync(accountId, accountNumber),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithMultipleSubscriptions_Should_PublishAllMessages()
    {
        // Arrange
        var subscriptions = new List<(int AccountId, string AccountNumber)>
        {
            (1, "ACC001"),
            (2, "ACC002"),
            (3, "ACC003")
        };

        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(subscriptions);

        foreach (var (accountId, accountNumber) in subscriptions)
        {
            _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
                .ReturnsAsync((DocumentsSyncStatusEntity)null);

            _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
                .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = accountNumber });

            _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(accountId, accountNumber))
                .Returns(Task.CompletedTask);
        }

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(3, result.TotalCompanies);
        Assert.Equal(3, result.SyncMessagesPublished);
        Assert.Equal(0, result.Errors);

        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithNoSubscriptions_Should_ReturnEmptyResult()
    {
        // Arrange
        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(new List<(int AccountId, string AccountNumber)>());

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(0, result.TotalCompanies);
        Assert.Equal(0, result.SyncMessagesPublished);
        Assert.Equal(0, result.Errors);

        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithIndividualFailures_Should_ContinueProcessing()
    {
        // Arrange
        var subscriptions = new List<(int AccountId, string AccountNumber)>
        {
            (1, "ACC001"),
            (2, "ACC002"), // Ce compte va échouer
            (3, "ACC003")
        };

        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(subscriptions);

        // Setup pour ACC001 (succès)
        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync("ACC001"))
            .ReturnsAsync((DocumentsSyncStatusEntity)null);
        _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync("ACC001"))
            .ReturnsAsync(new AccountEntity { AccountId = 1, AccountNumber = "ACC001" });
        _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(1, "ACC001"))
            .Returns(Task.CompletedTask);

        // Setup pour ACC002 (échec - l'exception est gérée en interne par CheckAndPushSyncMessageAsync)
        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync("ACC002"))
            .ThrowsAsync(new Exception("Database error for ACC002"));

        // Setup pour ACC003 (succès)
        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync("ACC003"))
            .ReturnsAsync((DocumentsSyncStatusEntity)null);
        _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync("ACC003"))
            .ReturnsAsync(new AccountEntity { AccountId = 3, AccountNumber = "ACC003" });
        _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(3, "ACC003"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(3, result.TotalCompanies);
        // Maintenant SyncMessagesPublished reflète le nombre réel de messages publiés avec succès
        // ACC001 et ACC003 réussissent, ACC002 échoue et retourne false
        Assert.Equal(2, result.SyncMessagesPublished); // Seulement 2 messages réellement publiés
        Assert.Equal(0, result.Errors); // Aucune erreur au niveau du scheduler (erreurs gérées en interne)

        // Vérifie que seulement 2 messages ont été réellement publiés (ACC001 et ACC003)
        // ACC002 a échoué et CheckAndPushSyncMessageAsync a retourné false
        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithLargeVolume_Should_ProcessInBatches()
    {
        // Arrange - créer 250 subscriptions (pour tester le batching avec BatchSize=100)
        var subscriptions = Enumerable.Range(1, 250)
            .Select(i => (AccountId: i, AccountNumber: $"ACC{i:D3}"))
            .ToList();

        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(subscriptions);

        foreach (var (accountId, accountNumber) in subscriptions)
        {
            _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
                .ReturnsAsync((DocumentsSyncStatusEntity)null);

            _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
                .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = accountNumber });

            _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(accountId, accountNumber))
                .Returns(Task.CompletedTask);
        }

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(250, result.TotalCompanies);
        Assert.Equal(250, result.SyncMessagesPublished);
        Assert.Equal(0, result.Errors);

        // Vérifie que tous les messages ont été publiés
        _notificationManagerMock.Verify(
            manager => manager.PublishAsync(
                It.IsAny<PennylaneDocumentSyncEvent>(),
                It.IsAny<string>()),
            Times.Exactly(250));
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithCriticalFailure_Should_ReturnErrorResult()
    {
        // Arrange - simule une erreur critique au niveau du repository
        var exceptionMessage = "Critical database failure";
        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(0, result.TotalCompanies);
        Assert.Equal(0, result.SyncMessagesPublished);
        Assert.Equal(1, result.Errors);
        Assert.Contains($"Critical error: {exceptionMessage}", result.ErrorMessages);

        // Vérifie que le logger a été appelé avec l'erreur critique
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("[ScheduledSync] Critical failure during scheduled sync")),
                It.Is<Exception>(ex => ex.Message == exceptionMessage),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_WithCancellation_Should_ReturnPartialResult()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var subscriptions = Enumerable.Range(1, 150)
            .Select(i => (AccountId: i, AccountNumber: $"ACC{i:D3}"))
            .ToList();

        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(subscriptions);

        // Setup pour les 50 premiers comptes (premier batch)
        for (int i = 1; i <= 50; i++)
        {
            var accountNumber = $"ACC{i:D3}";
            _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
                .ReturnsAsync((DocumentsSyncStatusEntity)null);
            _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
                .ReturnsAsync(new AccountEntity { AccountId = i, AccountNumber = accountNumber });
            _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(i, accountNumber))
                .Returns(Task.CompletedTask);
        }

        // Setup pour déclencher la cancellation après le premier batch
        _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync("ACC051"))
            .Callback(() => cts.Cancel())
            .ReturnsAsync((DocumentsSyncStatusEntity)null);

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync(cts.Token);

        // Assert
        Assert.Equal(150, result.TotalCompanies); // Total connu avant cancellation
        Assert.True(result.SyncMessagesPublished >= 50); // Au moins le premier batch traité
        Assert.True(result.SyncMessagesPublished < 150); // Pas tous traités
        Assert.Equal(1, result.Errors);
        Assert.Contains("Sync was cancelled", result.ErrorMessages);

        // Vérifie que le logger a été appelé avec le warning de cancellation
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("[ScheduledSync] Sync was cancelled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduledSyncAllSubscriptionsAsync_Should_CountDistinctCompanies()
    {
        // Arrange - Simule le cas où on aurait des doublons d'AccountNumber (ne devrait pas arriver avec notre requête SQL, mais teste le comportement)
        var subscriptions = new List<(int AccountId, string AccountNumber)>
        {
            (1, "ACC001"),
            (1, "ACC001"), // Doublon du même account
            (2, "ACC002"),
            (2, "ACC002"), // Doublon du même account
            (3, "ACC003")
        };

        _subscriptionRepositoryMock
            .Setup(r => r.GetActiveSubscriptionsWithPennylaneCompaniesAsync())
            .ReturnsAsync(subscriptions);

        // Setup pour les 3 accounts distincts
        foreach (var accountNumber in new[] { "ACC001", "ACC002", "ACC003" })
        {
            var accountId = int.Parse(accountNumber.Replace("ACC", "").TrimStart('0'));

            _documentRepositoryMock.Setup(repo => repo.GetSyncStatusAsync(accountNumber))
                .ReturnsAsync((DocumentsSyncStatusEntity)null);

            _accountRepositoryMock.Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
                .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = accountNumber });

            _notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncEvent>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock.Setup(r => r.AddOrUpdateSyncDateAsync(accountId, accountNumber))
                .Returns(Task.CompletedTask);
        }

        // Act
        var result = await _syncService.ScheduledSyncAllSubscriptionsAsync();

        // Assert
        Assert.Equal(3, result.TotalCompanies); // Doit compter 3 companies distinctes, pas 5
        Assert.Equal(5, result.SyncMessagesPublished); // Mais 5 messages publiés (tous les tuples sont traités)
        Assert.Equal(0, result.Errors);
    }

}