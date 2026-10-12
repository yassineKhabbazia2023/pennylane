// <copyright file="SubscriptionRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Tests.Repository;

public class SubscriptionRepositoryTests
{
    private Fixture _fixture;

    public SubscriptionRepositoryTests()
    {
        _fixture = new Fixture();
    }

    private DbContextOptions<PennylaneContext> CreateInMemoryOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid()}")
            .Options;
    }

    private SubscriptionRepository CreateRepository(PennylaneContext context)
    {
        return new SubscriptionRepository(context);
    }

    [Fact]
    public async Task SaveSubcriptionAsync_Should_Add_Subscription()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(SaveSubcriptionAsync_Should_Add_Subscription));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);
        var subscription = _fixture.Create<SubscriptionEntity>();
        // Act
        await repository.SaveSubcriptionAsync(subscription);
        var result = await context.SubscriptionEntity.FindAsync(subscription.Id);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(subscription.OfferName, result!.OfferName);
    }

    [Fact]
    public async Task GetSubscriptionByOfferAndAccountAsync_Should_Return_Subscription()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetSubscriptionByOfferAndAccountAsync_Should_Return_Subscription));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);
        var subscription = _fixture.Create<SubscriptionEntity>();
        await repository.SaveSubcriptionAsync(subscription);
        // Act
        var result = await repository.GetSubscriptionByOfferAndAccountAsync(subscription.OfferName, subscription.AccountId!.Value);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(subscription.OfferName, result!.OfferName);
        Assert.Equal(subscription.AccountId, result.AccountId);
    }

    [Fact]
    public async Task DeleteSubcriptionAsync_Should_Remove_Subscription()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(DeleteSubcriptionAsync_Should_Remove_Subscription));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);
        var subscription = _fixture.Create<SubscriptionEntity>();
        await repository.SaveSubcriptionAsync(subscription);
        // Act
        await repository.DeleteSubcriptionAsync(subscription);
        var result = await context.SubscriptionEntity.FindAsync(subscription.Id);
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllActiveSubscriptionsWithAccountsAsync_WithPennylaneOffer_Should_ReturnAccountsWithNumbers()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAllActiveSubscriptionsWithAccountsAsync_WithPennylaneOffer_Should_ReturnAccountsWithNumbers));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer des comptes
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        var account2 = new AccountEntity { AccountId = 2, AccountNumber = "ACC002", LegalName = "Company 2" };
        var account3 = new AccountEntity { AccountId = 3, AccountNumber = "ACC003", LegalName = "Company 3" };

        await context.AccountEntity.AddRangeAsync(account1, account2, account3);
        await context.SaveChangesAsync();

        // Créer des souscriptions Pennylane
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        var sub2 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 2 };
        var sub3 = new SubscriptionEntity { OfferName = "OtherOffer", AccountId = 3 }; // Différent offer

        await context.SubscriptionEntity.AddRangeAsync(sub1, sub2, sub3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllActiveSubscriptionsWithAccountsAsync("Pennylane");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.AccountId == 1 && x.AccountNumber == "ACC001");
        Assert.Contains(result, x => x.AccountId == 2 && x.AccountNumber == "ACC002");
        Assert.DoesNotContain(result, x => x.AccountId == 3); // Ne doit pas inclure l'autre offer
    }

    [Fact]
    public async Task GetAllActiveSubscriptionsWithAccountsAsync_WithNoSubscriptions_Should_ReturnEmptyList()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAllActiveSubscriptionsWithAccountsAsync_WithNoSubscriptions_Should_ReturnEmptyList));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetAllActiveSubscriptionsWithAccountsAsync("Pennylane");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllActiveSubscriptionsWithAccountsAsync_WithNullAccountId_Should_ExcludeSubscription()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAllActiveSubscriptionsWithAccountsAsync_WithNullAccountId_Should_ExcludeSubscription));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer un compte
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        await context.AccountEntity.AddAsync(account1);
        await context.SaveChangesAsync();

        // Créer des souscriptions
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        var sub2 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = null }; // AccountId null

        await context.SubscriptionEntity.AddRangeAsync(sub1, sub2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllActiveSubscriptionsWithAccountsAsync("Pennylane");

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].AccountId);
        Assert.Equal("ACC001", result[0].AccountNumber);
    }

    [Fact]
    public async Task GetAllActiveSubscriptionsWithAccountsAsync_WithLargeVolume_Should_PerformEfficiently()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAllActiveSubscriptionsWithAccountsAsync_WithLargeVolume_Should_PerformEfficiently));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer 100 comptes et souscriptions
        var accounts = Enumerable.Range(1, 100)
            .Select(i => new AccountEntity { AccountId = i, AccountNumber = $"ACC{i:D3}", LegalName = $"Company {i}" })
            .ToList();

        await context.AccountEntity.AddRangeAsync(accounts);
        await context.SaveChangesAsync();

        var subscriptions = Enumerable.Range(1, 100)
            .Select(i => new SubscriptionEntity { OfferName = "Pennylane", AccountId = i })
            .ToList();

        await context.SubscriptionEntity.AddRangeAsync(subscriptions);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllActiveSubscriptionsWithAccountsAsync("Pennylane");

        // Assert
        Assert.Equal(100, result.Count);
        Assert.All(result, item =>
        {
            Assert.True(item.AccountId >= 1 && item.AccountId <= 100);
            Assert.StartsWith("ACC", item.AccountNumber);
        });
    }

    [Fact]
    public async Task GetAllActiveSubscriptionsWithAccountsAsync_WithDifferentOffers_Should_FilterCorrectly()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAllActiveSubscriptionsWithAccountsAsync_WithDifferentOffers_Should_FilterCorrectly));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer des comptes
        var accounts = Enumerable.Range(1, 5)
            .Select(i => new AccountEntity { AccountId = i, AccountNumber = $"ACC{i:D3}", LegalName = $"Company {i}" })
            .ToList();

        await context.AccountEntity.AddRangeAsync(accounts);
        await context.SaveChangesAsync();

        // Créer des souscriptions pour différents offers
        var subscriptions = new List<SubscriptionEntity>
            {
                new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 },
                new SubscriptionEntity { OfferName = "Pennylane", AccountId = 2 },
                new SubscriptionEntity { OfferName = "OtherOffer", AccountId = 3 },
                new SubscriptionEntity { OfferName = "AnotherOffer", AccountId = 4 },
                new SubscriptionEntity { OfferName = "Pennylane", AccountId = 5 }
            };

        await context.SubscriptionEntity.AddRangeAsync(subscriptions);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAllActiveSubscriptionsWithAccountsAsync("Pennylane");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.AccountId == 1);
        Assert.Contains(result, x => x.AccountId == 2);
        Assert.Contains(result, x => x.AccountId == 5);
        Assert.DoesNotContain(result, x => x.AccountId == 3 || x.AccountId == 4);
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_ReturnOnlySubscriptionsWithCompanies()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_ReturnOnlySubscriptionsWithCompanies));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer des comptes
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        var account2 = new AccountEntity { AccountId = 2, AccountNumber = "ACC002", LegalName = "Company 2" };
        var account3 = new AccountEntity { AccountId = 3, AccountNumber = "ACC003", LegalName = "Company 3" };

        await context.AccountEntity.AddRangeAsync(account1, account2, account3);
        await context.SaveChangesAsync();

        // Créer des companies (seulement pour ACC001 et ACC002)
        var company1 = new CompanyEntity { Id = "ACC001", FirmId = "F1", Name = "Company 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var company2 = new CompanyEntity { Id = "ACC002", FirmId = "F1", Name = "Company 2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await context.CompanyEntity.AddRangeAsync(company1, company2);
        await context.SaveChangesAsync();

        // Créer des souscriptions Pennylane pour les 3 comptes
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        var sub2 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 2 };
        var sub3 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 3 }; // Pas de company

        await context.SubscriptionEntity.AddRangeAsync(sub1, sub2, sub3);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.AccountId == 1 && x.AccountNumber == "ACC001");
        Assert.Contains(result, x => x.AccountId == 2 && x.AccountNumber == "ACC002");
        Assert.DoesNotContain(result, x => x.AccountId == 3); // Exclut car pas de company
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithNoCompanies_Should_ReturnEmptyList()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithNoCompanies_Should_ReturnEmptyList));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer des comptes
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        await context.AccountEntity.AddAsync(account1);
        await context.SaveChangesAsync();

        // Créer une souscription Pennylane SANS company correspondante
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        await context.SubscriptionEntity.AddAsync(sub1);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_FilterByPennylaneOfferOnly()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_FilterByPennylaneOfferOnly));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer des comptes
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        var account2 = new AccountEntity { AccountId = 2, AccountNumber = "ACC002", LegalName = "Company 2" };

        await context.AccountEntity.AddRangeAsync(account1, account2);
        await context.SaveChangesAsync();

        // Créer des companies
        var company1 = new CompanyEntity { Id = "ACC001", FirmId = "F1", Name = "Company 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var company2 = new CompanyEntity { Id = "ACC002", FirmId = "F1", Name = "Company 2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await context.CompanyEntity.AddRangeAsync(company1, company2);
        await context.SaveChangesAsync();

        // Créer des souscriptions (Pennylane et autres)
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        var sub2 = new SubscriptionEntity { OfferName = "OtherOffer", AccountId = 2 }; // Autre offre

        await context.SubscriptionEntity.AddRangeAsync(sub1, sub2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].AccountId);
        Assert.Equal("ACC001", result[0].AccountNumber);
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_ExcludeNullAccountId()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_Should_ExcludeNullAccountId));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer un compte et une company
        var account1 = new AccountEntity { AccountId = 1, AccountNumber = "ACC001", LegalName = "Company 1" };
        await context.AccountEntity.AddAsync(account1);
        await context.SaveChangesAsync();

        var company1 = new CompanyEntity { Id = "ACC001", FirmId = "F1", Name = "Company 1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await context.CompanyEntity.AddAsync(company1);
        await context.SaveChangesAsync();

        // Créer des souscriptions
        var sub1 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = 1 };
        var sub2 = new SubscriptionEntity { OfferName = "Pennylane", AccountId = null }; // AccountId null

        await context.SubscriptionEntity.AddRangeAsync(sub1, sub2);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].AccountId);
        Assert.Equal("ACC001", result[0].AccountNumber);
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithMultipleValidEntries_Should_ReturnAll()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithMultipleValidEntries_Should_ReturnAll));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer 3 comptes
        var accounts = Enumerable.Range(1, 3)
            .Select(i => new AccountEntity { AccountId = i, AccountNumber = $"ACC{i:D3}", LegalName = $"Company {i}" })
            .ToList();

        await context.AccountEntity.AddRangeAsync(accounts);
        await context.SaveChangesAsync();

        // Créer 3 companies correspondantes
        var companies = Enumerable.Range(1, 3)
            .Select(i => new CompanyEntity
            {
                Id = $"ACC{i:D3}",
                FirmId = "F1",
                Name = $"Company {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        await context.CompanyEntity.AddRangeAsync(companies);
        await context.SaveChangesAsync();

        // Créer 3 souscriptions Pennylane
        var subscriptions = Enumerable.Range(1, 3)
            .Select(i => new SubscriptionEntity { OfferName = "Pennylane", AccountId = i })
            .ToList();

        await context.SubscriptionEntity.AddRangeAsync(subscriptions);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.AccountId == 1 && x.AccountNumber == "ACC001");
        Assert.Contains(result, x => x.AccountId == 2 && x.AccountNumber == "ACC002");
        Assert.Contains(result, x => x.AccountId == 3 && x.AccountNumber == "ACC003");
    }

    [Fact]
    public async Task GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithLargeVolume_Should_PerformEfficiently()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetActiveSubscriptionsWithPennylaneCompaniesAsync_WithLargeVolume_Should_PerformEfficiently));
        using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Créer 100 comptes
        var accounts = Enumerable.Range(1, 100)
            .Select(i => new AccountEntity { AccountId = i, AccountNumber = $"ACC{i:D3}", LegalName = $"Company {i}" })
            .ToList();

        await context.AccountEntity.AddRangeAsync(accounts);
        await context.SaveChangesAsync();

        // Créer 100 companies
        var companies = Enumerable.Range(1, 100)
            .Select(i => new CompanyEntity
            {
                Id = $"ACC{i:D3}",
                FirmId = "F1",
                Name = $"Company {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        await context.CompanyEntity.AddRangeAsync(companies);
        await context.SaveChangesAsync();

        // Créer 100 souscriptions Pennylane
        var subscriptions = Enumerable.Range(1, 100)
            .Select(i => new SubscriptionEntity { OfferName = "Pennylane", AccountId = i })
            .ToList();

        await context.SubscriptionEntity.AddRangeAsync(subscriptions);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetActiveSubscriptionsWithPennylaneCompaniesAsync();

        // Assert
        Assert.Equal(100, result.Count);
        Assert.All(result, item =>
        {
            Assert.True(item.AccountId >= 1 && item.AccountId <= 100);
            Assert.StartsWith("ACC", item.AccountNumber);
        });
    }
}
