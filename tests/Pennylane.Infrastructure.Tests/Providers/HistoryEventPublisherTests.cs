using Moq;
using Pennylane.Infrastructure.Providers;
using Application.Interfaces;
using Pennylane.Application.Models.Signatory;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pennylane.Domain.Entities;
using Pulse.ExceptionMiddleware.Exceptions;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Infrastructure.Tests.Providers;

public class HistoryEventPublisherTests
{
    [Fact]
    public async Task PublishHistoryCreatedEventAsync_ShouldPublishEvent_WhenAccountExists()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC123",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@email.com",
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = "2024-12-31"
        };
        var account = new AccountEntity
        {
            AccountId = 1,
            AccountNumber = "ACC123",
            LegalName = "John Doe SARL"
        };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(row.AccountNumber)).ReturnsAsync(account);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act
        await sut.PublishHistoryCreatedEventAsync(row);

        // Assert
        eventPublisherMock.Verify(e => e.PublishAsync(It.Is<HistoryCreatedEvent>(e =>
            e.Data.Account!.AccountNumber == row.AccountNumber &&
            e.Data.Account.LegalName == account.LegalName &&
            e.Data.CreationDate != default &&
            e.Data.Action!.Code == "AVEACTIV" &&
            e.Data.User == null &&
            e.Data.TargetUser!.Name == $"{row.FirstName} {row.LastName}" &&
            e.Data.TargetUser.Email == row.Email &&
            e.Data.TargetUser.UserType == "Customer" &&
            e.Data.Details != null),
            null!, null), Times.Once);
    }

    [Fact]
    public async Task PublishHistoryCreatedEventAsync_ShouldThrowNotFoundException_WhenAccountDoesNotExist()
    {
        // Arrange
        var row = new SignatoryAccessImportRow { AccountNumber = "NOTFOUND" };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(row.AccountNumber)).ReturnsAsync((AccountEntity?)null);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act & Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(() => sut.PublishHistoryCreatedEventAsync(row));

        Assert.Equal("PNL001", result.Code);
        Assert.Equal($"Account with number {row.AccountNumber} was not found.", result.Message);

        eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<HistoryCreatedEvent>(), null!, null), Times.Never);
    }

    [Fact]
    public async Task PublishHistoryDeletedEventAsync_ShouldPublishEvent_WhenAccountExists()
    {
        // Arrange
        var row = new SignatoryAccessEntity
        {
            AccountNumber = "ACC123",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@email.com",
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = DateTime.UtcNow,
        };
        var account = new AccountEntity
        {
            AccountId = 1,
            AccountNumber = "ACC123",
            LegalName = "John Doe SARL"
        };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(row.AccountNumber)).ReturnsAsync(account);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act
        await sut.PublishHistoryDeletedEventAsync(row);

        // Assert
        eventPublisherMock.Verify(e => e.PublishAsync(It.Is<HistoryCreatedEvent>(e =>
            e.Data.Account!.AccountNumber == row.AccountNumber &&
            e.Data.Account.LegalName == account.LegalName &&
            e.Data.CreationDate != default &&
            e.Data.Action!.Code == "AVEDESAC" &&
            e.Data.User == null &&
            e.Data.TargetUser!.Name == $"{row.FirstName} {row.LastName}" &&
            e.Data.TargetUser.Email == row.Email &&
            e.Data.TargetUser.UserType == "Customer" &&
            e.Data.Details == null),
            null!, null), Times.Once);
    }

    [Fact]
    public async Task PublishHistoryDeletedEventAsync_ShouldThrowNotFoundException_WhenAccountDoesNotExist()
    {
        // Arrange
        var row = new SignatoryAccessEntity { AccountNumber = "NOTFOUND" };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(row.AccountNumber)).ReturnsAsync((AccountEntity?)null);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act & Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(() => sut.PublishHistoryDeletedEventAsync(row));

        Assert.Equal("PNL001", result.Code);
        Assert.Equal($"Account with number {row.AccountNumber} was not found.", result.Message);

        eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<HistoryCreatedEvent>(), null!, null), Times.Never);
    }

    [Fact]
    public async Task PublishHistoryUpdatedEventAsync_ShouldPublishEvent_WhenAccountExists()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            AccountNumber = "ACC123",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@email.com",
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = new DateTime(2026, 6, 30),
        };
        var newRow = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC123",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@email.com",
            PlanName = "PlanB",
            UserCount = 8,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 209m,
            FreeTrialEndDate = "31/10/2026",
        };
        var account = new AccountEntity
        {
            AccountId = 1,
            AccountNumber = "ACC123",
            LegalName = "John Doe SARL"
        };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(newRow.AccountNumber)).ReturnsAsync(account);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act
        await sut.PublishHistoryUpdatedEventAsync(oldEntity, newRow);

        // Assert
        eventPublisherMock.Verify(e => e.PublishAsync(It.Is<HistoryCreatedEvent>(e =>
            e.Data.Account!.AccountNumber == newRow.AccountNumber &&
            e.Data.Account.LegalName == account.LegalName &&
            e.Data.CreationDate != default &&
            e.Data.Action!.Code == "AVEMODIF" &&
            e.Data.User == null &&
            e.Data.TargetUser!.Name == $"{newRow.FirstName} {newRow.LastName}" &&
            e.Data.TargetUser.Email == newRow.Email &&
            e.Data.TargetUser.UserType == "Customer" &&
            e.Data.Details != null),
            null!, null), Times.Once);
    }

    [Fact]
    public async Task PublishHistoryUpdatedEventAsync_ShouldThrowNotFoundException_WhenAccountDoesNotExist()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity { AccountNumber = "NOTFOUND", FreeTrialEndDate = new DateTime(2026, 6, 30) };
        var newRow = new SignatoryAccessImportRow { AccountNumber = "NOTFOUND", FreeTrialEndDate = "31/10/2026" };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(newRow.AccountNumber)).ReturnsAsync((AccountEntity?)null);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act & Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(() => sut.PublishHistoryUpdatedEventAsync(oldEntity, newRow));

        Assert.Equal("PNL001", result.Code);
        Assert.Equal($"Account with number {newRow.AccountNumber} was not found.", result.Message);

        eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<HistoryCreatedEvent>(), null!, null), Times.Never);
    }

    [Theory]
    [InlineData(true, "AVEACCEP")]
    [InlineData(false, "AVEREFUS")]
    public async Task PublishHistoryValidatedEventAsync_ShouldPublishEvent_WhenAccountExists(bool status, string expectedCode)
    {
        // Arrange
        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC123",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@email.com",
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = new DateTime(2026, 6, 30),
        };
        var account = new AccountEntity
        {
            AccountId = 1,
            AccountNumber = "ACC123",
            LegalName = "John Doe SARL"
        };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(signatoryAccess.AccountNumber)).ReturnsAsync(account);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act
        await sut.PublishHistoryValidatedEventAsync(signatoryAccess, status);

        // Assert
        eventPublisherMock.Verify(e => e.PublishAsync(It.Is<HistoryCreatedEvent>(e =>
            e.Data.Account!.AccountNumber == signatoryAccess.AccountNumber &&
            e.Data.Account.LegalName == account.LegalName &&
            e.Data.CreationDate != default &&
            e.Data.Action!.Code == expectedCode &&
            e.Data.User == null &&
            e.Data.TargetUser!.Name == $"{signatoryAccess.FirstName} {signatoryAccess.LastName}" &&
            e.Data.TargetUser.Email == signatoryAccess.Email &&
            e.Data.TargetUser.UserType == "Customer" &&
            e.Data.Details != null),
            null!, null), Times.Once);
    }

    [Fact]
    public async Task PublishHistoryValidatedEventAsync_ShouldThrowNotFoundException_WhenAccountDoesNotExist()
    {
        // Arrange
        var signatoryAccess = new SignatoryAccessEntity { AccountNumber = "NOTFOUND", FreeTrialEndDate = new DateTime(2026, 6, 30) };
        var newRow = new SignatoryAccessImportRow { AccountNumber = "NOTFOUND", FreeTrialEndDate = "31/10/2026" };
        var accountRepoMock = new Mock<IAccountRepository>();
        accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync(newRow.AccountNumber)).ReturnsAsync((AccountEntity?)null);
        var eventPublisherMock = new Mock<IEventPublisher>();
        var sut = new HistoryEventPublisher(accountRepoMock.Object, eventPublisherMock.Object);

        // Act & Assert
        var result = await Assert.ThrowsAsync<NotFoundException>(() => sut.PublishHistoryValidatedEventAsync(signatoryAccess, true));

        Assert.Equal("PNL001", result.Code);
        Assert.Equal($"Account with number {newRow.AccountNumber} was not found.", result.Message);

        eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<HistoryCreatedEvent>(), null!, null), Times.Never);
    }
}
