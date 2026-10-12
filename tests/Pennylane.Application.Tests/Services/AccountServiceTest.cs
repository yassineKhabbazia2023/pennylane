// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.StaticValues;
using Pennylane.Domain.Entities;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Application.Tests.Services;

public class AccountServiceTests
{
    private readonly Fixture _fixture;
    private readonly Mock<ILogger<AccountService>> _loggerMock;

    public AccountServiceTests()
    {
        _fixture = new Fixture();
        // Prevent cycles in our fixture graph
        _fixture.Behaviors
            .OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _loggerMock = new Mock<ILogger<AccountService>>();
    }

    [Fact]
    public async Task GetAccountNumberByIdAsync_WhenAccountExists_ReturnsAccountNumber()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var expectedNumber = _fixture.Create<string>();
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(new AccountEntity { AccountId = accountId, AccountNumber = expectedNumber });

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.GetAccountNumberByIdAsync(accountId);

        // Assert
        result.Should().Be(expectedNumber);
        repo.Verify(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()), Times.Once);
    }

    [Fact]
    public async Task GetAccountNumberByIdAsync_WhenAccountDoesNotExist_ReturnsNull()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync((AccountEntity)null);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.GetAccountNumberByIdAsync(accountId);

        // Assert
        result.Should().BeNull();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()), Times.Once);
    }

    [Fact]
    public async Task GetAccountNumberByIdAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var accountId = _fixture.Create<int>();
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ThrowsAsync(new InvalidOperationException("db fail"));

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act / Assert
        await svc.Invoking(s => s.GetAccountNumberByIdAsync(accountId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("db fail");
    }

    [Theory]
    [InlineData(OperationAction.Insert)]
    public async Task SyncAccountAsync_Insert_WhenNotExists_CreatesAccountAndReturnsTrue(string syncType)
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, _fixture.Create<string>())
            .With(x => x.AccountGlobalUniqueId, Guid.NewGuid())
            .Create();

        AccountEntity? capturedEntity = null;
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync(eventData.AccountNumber))
            .ReturnsAsync((AccountEntity)null);
        repo.Setup(r => r.AddAccountAsync(It.IsAny<AccountEntity>()))
            .Callback<AccountEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, syncType);

        // Assert
        result.Should().BeTrue();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync(eventData.AccountNumber), Times.Once);
        repo.Verify(r => r.AddAccountAsync(It.IsAny<AccountEntity>()), Times.Once);
        repo.Verify(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
        repo.Verify(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()), Times.Never);

        // ensure mapping of fields
        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccountId.Should().Be(eventData.AccountId);
        capturedEntity.AccountNumber.Should().Be(eventData.AccountNumber);
        capturedEntity.LegalName.Should().Be(eventData.LegalName);
        capturedEntity.AccountGlobalUniqueId.Should().Be(eventData.AccountGlobalUniqueId);
        capturedEntity.CreatedBy.Should().Be(eventData.CreatedBy);
        capturedEntity.ModifiedBy.Should().Be(eventData.ModifiedBy);
    }

    [Fact]
    public async Task SyncAccountAsync_Insert_WhenExists_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "EXISTING")
            .Create();

        var existing = new AccountEntity { AccountNumber = "EXISTING", AccountId = eventData.AccountId };
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("EXISTING"))
            .ReturnsAsync(existing);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Insert);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("EXISTING"), Times.Once);
        repo.Verify(r => r.AddAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
    }

    [Fact]
    public async Task SyncAccountAsync_Insert_WhenAddThrows_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, _fixture.Create<string>())
            .Create();

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync(eventData.AccountNumber))
            .ReturnsAsync((AccountEntity)null);
        repo.Setup(r => r.AddAccountAsync(It.IsAny<AccountEntity>()))
            .ThrowsAsync(new DbOperationException("add failed", new Exception("inner")));

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Insert);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync(eventData.AccountNumber), Times.Once);
        repo.Verify(r => r.AddAccountAsync(It.IsAny<AccountEntity>()), Times.Once);
    }

    [Fact]
    public async Task SyncAccountAsync_Update_WhenExists_UpdatesAccountAndReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "ACC")
            .With(x => x.AccountGlobalUniqueId, id)
            .With(x => x.AccountId, 42)
            .With(x => x.LegalName, "Name")
            .With(x => x.CreatedBy, "creator")
            .With(x => x.ModifiedBy, "modifier")
            .Create();

        var existing = new AccountEntity { AccountNumber = "ACC", AccountGlobalUniqueId = id, AccountId = 42 };
        AccountEntity? capturedEntity = null;

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("ACC"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()))
            .Callback<AccountEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Update);

        // Assert
        result.Should().BeTrue();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("ACC"), Times.Once);
        repo.Verify(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()), Times.Once);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccountId.Should().Be(42);
        capturedEntity.AccountNumber.Should().Be("ACC");
        capturedEntity.AccountGlobalUniqueId.Should().Be(id);
        capturedEntity.LegalName.Should().Be("Name");
        capturedEntity.CreatedBy.Should().Be("creator");
        capturedEntity.ModifiedBy.Should().Be("modifier");
    }

    [Fact]
    public async Task SyncAccountAsync_Update_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "ACC")
            .Create();

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("ACC"))
            .ReturnsAsync((AccountEntity)null);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Update);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("ACC"), Times.Once);
        repo.Verify(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
    }

    [Fact]
    public async Task SyncAccountAsync_Update_WhenUpdateThrows_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "ACC")
            .Create();

        var existing = new AccountEntity { AccountNumber = "ACC", AccountGlobalUniqueId = Guid.NewGuid(), AccountId = eventData.AccountId };
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("ACC"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()))
            .ThrowsAsync(new DbOperationException("update failed", null));

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Update);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()), Times.Once);
    }

    [Fact]
    public async Task SyncAccountAsync_Delete_WhenExists_RemovesAccountAndReturnsTrue()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "DEL")
            .Create();

        var existing = new AccountEntity
        {
            AccountNumber = "DEL",
            AccountGlobalUniqueId = Guid.NewGuid(),
            AccountId = eventData.AccountId
        };
        AccountEntity? capturedEntity = null;

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("DEL"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()))
            .Callback<AccountEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Delete);

        // Assert
        result.Should().BeTrue();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("DEL"), Times.Once);
        repo.Verify(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()), Times.Once);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AccountNumber.Should().Be("DEL");
        capturedEntity.AccountGlobalUniqueId.Should().Be(existing.AccountGlobalUniqueId);
        capturedEntity.AccountId.Should().Be(existing.AccountId);
    }

    [Fact]
    public async Task SyncAccountAsync_Delete_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "NONE")
            .Create();

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("NONE"))
            .ReturnsAsync((AccountEntity)null);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Delete);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("NONE"), Times.Once);
        repo.Verify(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
    }

    [Fact]
    public async Task SyncAccountAsync_Delete_WhenRemoveThrows_ReturnsFalse()
    {
        // Arrange
        var eventData = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "DEL")
            .Create();

        var existing = new AccountEntity { AccountNumber = "DEL", AccountGlobalUniqueId = Guid.NewGuid(), AccountId = eventData.AccountId };
        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("DEL"))
            .ReturnsAsync(existing);
        repo.Setup(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()))
            .ThrowsAsync(new DbOperationException("remove failed", null));

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(eventData, OperationAction.Delete);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()), Times.Once);
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData(null)]
    public async Task SyncAccountAsync_InvalidType_ReturnsFalse(string? syncType)
    {
        // Arrange
        var data = _fixture.Build<AccountStateEventData>()
            .With(x => x.AccountNumber, "XYZ")
            .Create();

        var repo = new Mock<IAccountRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetAccountByNumberOrIdAsync("XYZ"))
            .ReturnsAsync((AccountEntity)null);

        var svc = new AccountService(repo.Object, _loggerMock.Object);

        // Act
        var result = await svc.SyncAcountAsync(data, syncType);

        // Assert
        result.Should().BeFalse();
        repo.Verify(r => r.GetAccountByNumberOrIdAsync("XYZ"), Times.Once);
        repo.Verify(r => r.AddAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
        repo.Verify(r => r.UpdateAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
        repo.Verify(r => r.RemoveAccountAsync(It.IsAny<AccountEntity>()), Times.Never);
    }
}