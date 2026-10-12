// <copyright file="WebhookProcessingServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using Azure.Messaging.ServiceBus;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models.Webhook;
using Pennylane.Application.services;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Tests.Services;

public class WebhookProcessingServiceTests
{
    private readonly IFixture _fixture;

    public WebhookProcessingServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private WebhookProcessingService CreateService(
        out Mock<INotificationManager> notificationManager,
        out Mock<ILogger<WebhookProcessingService>> logger,
        out Mock<IPennylaneRepository> pennylaneRepository)
    {
        notificationManager = new Mock<INotificationManager>(MockBehavior.Strict);
        logger = new Mock<ILogger<WebhookProcessingService>>(MockBehavior.Loose);
        pennylaneRepository = new Mock<IPennylaneRepository>(MockBehavior.Strict);
        return new WebhookProcessingService(notificationManager.Object, logger.Object, pennylaneRepository.Object);
    }

    [Fact]
    public async Task ProcessEvent_UserCreated_PublishesPennylaneUserCreatedEvent()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var user = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "C1", "C2" })
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.After, user)
            .Create();
        notificationManager
            .Setup(m => m.PublishAsync(It.Is<PennylaneUserCreatedEvent>(e =>
                e.Data.Id == user.Id &&
                e.Data.Email == user.Email &&
                e.Data.FirstName == user.FirstName &&
                e.Data.LastName == user.LastName &&
                e.Data.Role == user.Role &&
                e.Data.PhoneNumber == user.PhoneNumber &&
                e.Data.AssignedToFirm == user.AssignedToFirm &&
                e.Data.CompanyIds.Count == user.CompanyIds.Count &&
                e.Data.CompanyIds[0] == user.CompanyIds[0] &&
                e.Data.CompanyIds[1] == user.CompanyIds[1]
                && e.Data.SubRole == user.SubRole
            ), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserCreatedEvent, payload);

        // Assert
        notificationManager.VerifyAll();
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_BeforeOrAfterNull_DoesNotPublishAndLogsWarning()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, (User)null)
            .With(p => p.After, (User)null)
            .Create();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.Verify(m => m.PublishAsync(It.IsAny<PennylaneUserRemovedEvent>(), null), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_PublishesPennylaneUserRemovedEventWithRemovedCompanies()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "A", "B", "C" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "B", "C" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .With(u => u.SubRole, (string?)null)   // pas de sync sub_role dans ce test
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        // PnlRole exists → external removal
        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync("A", after.Email))
            .ReturnsAsync(true);
        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        notificationManager
            .Setup(m => m.PublishAsync(It.Is<PennylaneUserRemovedEvent>(e =>
                e.Data.Id == after.Id &&
                e.Data.Email == after.Email &&
                e.Data.CompanyIds.Count == 1 &&
                e.Data.CompanyIds[0] == "A"
            ), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.VerifyAll();
    }

    [Fact]
    public async Task ProcessEvent_ServiceBusException_IsHandledSilently()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var user = _fixture.Create<User>();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.After, user)
            .Create();
        var exception = new ServiceBusException("error", ServiceBusFailureReason.ServiceBusy);
        notificationManager
            .Setup(m => m.PublishAsync(It.IsAny<PennylaneUserCreatedEvent>(), null))
            .ThrowsAsync(exception);

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserCreatedEvent, payload);

        // Assert
        notificationManager.Verify(m => m.PublishAsync(It.IsAny<PennylaneUserCreatedEvent>(), null), Times.Once);
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_SelfInitiated_PnlRoleAbsent_DoesNotPublish()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "A", "B" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "B" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .With(u => u.SubRole, (string?)null)   // pas de sync sub_role dans ce test
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        // PnlRole absent → self-initiated removal
        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync("A", after.Email))
            .ReturnsAsync(false);
        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.Verify(m => m.PublishAsync(It.IsAny<PennylaneUserRemovedEvent>(), null), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_ExternalRemoval_PnlRolePresent_PublishesEvent()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "X", "Y" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "Y" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .With(u => u.SubRole, (string?)null)   // pas de sync sub_role dans ce test
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        // PnlRole exists → external removal
        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync("X", after.Email))
            .ReturnsAsync(true);
        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        notificationManager
            .Setup(m => m.PublishAsync(It.Is<PennylaneUserRemovedEvent>(e =>
                e.Data.Id == after.Id &&
                e.Data.Email == after.Email &&
                e.Data.CompanyIds.Count == 1 &&
                e.Data.CompanyIds[0] == "X"
            ), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.VerifyAll();
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_MixedScenario_OnlyExternalCompaniesPublished()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "A", "B", "C" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "C" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .With(u => u.SubRole, (string?)null)   // pas de sync sub_role dans ce test
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        // "A" → self-initiated (PnlRole absent), "B" → external (PnlRole present)
        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync("A", after.Email))
            .ReturnsAsync(false);
        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync("B", after.Email))
            .ReturnsAsync(true);
        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        notificationManager
            .Setup(m => m.PublishAsync(It.Is<PennylaneUserRemovedEvent>(e =>
                e.Data.Id == after.Id &&
                e.Data.Email == after.Email &&
                e.Data.CompanyIds.Count == 1 &&
                e.Data.CompanyIds[0] == "B"
            ), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.VerifyAll();
    }

    [Fact]
    public async Task ProcessEvent_UserRemoved_NoCompaniesRemoved_DoesNotPublish()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "A", "B" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "A", "B" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .With(u => u.SubRole, (string?)null)   // pas de sync sub_role dans ce test
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        notificationManager.Verify(m => m.PublishAsync(It.IsAny<PennylaneUserRemovedEvent>(), null), Times.Never);
    }

    // ── New tests for scenarios 4 & 5 ─────────────────────────────────────────


    [Fact]
    public async Task ProcessEvent_UserUpdated_SubRoleChanged_UpdatesRoleInRepository()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.SubRole, "employee")
            .With(u => u.CompanyIds, new List<string> { "EC12345678" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, new List<string> { "EC12345678" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                after.Email, "EC12345678", "executive"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            after.Email, "EC12345678", "executive"), Times.Once);
    }

    [Fact]
    public async Task ProcessEvent_UserUpdated_WithSubRole_AlwaysUpsertsForIdempotency()
    {
        // Arrange — before == after (même sub_role) : l'upsert est quand même appelé (idempotent)
        // car Pennylane peut envoyer before == after même quand le rôle a changé.
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var sameSubRole = "executive";
        var before = _fixture.Build<User>()
            .With(u => u.SubRole, sameSubRole)
            .With(u => u.CompanyIds, new List<string> { "EC12345678" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, sameSubRole)
            .With(u => u.CompanyIds, new List<string> { "EC12345678" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                after.Email, "EC12345678", sameSubRole))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — upsert appelé (idempotent, pas de pré-check DB)
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            after.Email, "EC12345678", sameSubRole), Times.Once);
    }


    // ── SyncRoleUpdateFromWebhookAsync guard conditions ───────────────────────

    [Fact]
    public async Task ProcessEvent_UserUpdated_AfterIsNull_SkipsSync()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out _, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, (User?)null)
            .Create();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — no upsert
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserUpdated_AfterSubRoleEmpty_SkipsSync()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out _, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "  ")
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — no upsert because SubRole is empty
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserUpdated_AfterCompanyIdsNull_SkipsSync()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out _, out var pennylaneRepository);
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, (List<string>?)null)
            .Create();
        // before = null → ProcessUserRemovedEvent returns early (logs warning), then SyncRoleUpdateFromWebhookAsync runs with after.CompanyIds = null
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, (User?)null)
            .With(p => p.After, after)
            .Create();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — no upsert because after.CompanyIds is null
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserUpdated_AfterCompanyIdsEmpty_SkipsSync()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out _, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, new List<string>())
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — no upsert
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_UserUpdated_UpsertThrows_LogsErrorAndContinues()
    {
        // Arrange
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, new List<string> { "EC001" })
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.PennylaneRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("db error"));

        // Act — must not throw
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — error was logged
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to update sub_role")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Bug Pennylane : before == after même quand le sub_role a réellement changé.
    /// Le code appelle l'upsert directement (idempotent) sans pré-check DB.
    /// </summary>
    [Fact]
    public async Task ProcessEvent_UserUpdated_PennylaneBeforeEqualsAfterButDbSubRoleDiffers_UpdatesRole()
    {
        // Arrange — Pennylane envoie before == after == "executive" (bug : before devrait être "employee")
        var service = CreateService(out var notificationManager, out var logger, out var pennylaneRepository);
        var before = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, new List<string> { "2025546345" })
            .Create();
        var after = _fixture.Build<User>()
            .With(u => u.SubRole, "executive")
            .With(u => u.CompanyIds, new List<string> { "2025546345" })
            .With(u => u.Id, before.Id)
            .With(u => u.Email, before.Email)
            .Create();
        var payload = _fixture.Build<WebhookPayload>()
            .With(p => p.Before, before)
            .With(p => p.After, after)
            .Create();

        pennylaneRepository
            .Setup(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
                after.Email, "2025546345", "executive"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.ProcessEvent(PennylaneEventNames.UserUpdatedEvent, payload);

        // Assert — upsert appelé malgré before == after
        pennylaneRepository.Verify(r => r.UpsertPennylaneRoleByEmailAndAccountNumberAsync(
            after.Email, "2025546345", "executive"), Times.Once);
    }
}
