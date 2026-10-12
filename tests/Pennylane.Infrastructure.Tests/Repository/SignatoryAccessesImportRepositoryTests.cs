using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pulse.ContactRegistry.Infrastructure.Context;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Tests.Repository;

public class SignatoryAccessesImportRepositoryTests
{
    private readonly Fixture _fixture;

    public SignatoryAccessesImportRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private static PennylaneContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PennylaneContext(options);
    }

    private static SignatoryAccessEntity CreateDomainEntity(
        string accountNumber,
        string email = "test@example.com",
        string lastName = "Last",
        string firstName = "First",
        string planName = "Plan A",
        int? userCount = 1)
    {
        return new SignatoryAccessEntity
        {
            AccountNumber = accountNumber,
            Email = email,
            LastName = lastName,
            FirstName = firstName,
            PlanName = planName,
            UserCount = userCount
        };
    }

    [Fact]
    public async Task InsertAsync_ShouldAddEntityToContext()
    {
        var entity = CreateDomainEntity("ACC001");

        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.InsertSignatoryAccessAsync(entity);
        await repository.SaveChangesAsync();

        var saved = await context.SignatoryAccessEntity.FindAsync("ACC001");
        Assert.NotNull(saved);
        Assert.Equal("ACC001", saved.AccountNumber);
        Assert.Equal(entity.Email, saved.Email);
        Assert.Equal(entity.LastName, saved.LastName);
        Assert.Equal(entity.FirstName, saved.FirstName);
    }

    [Fact]
    public async Task InsertAsync_ShouldNotThrow_WhenEntityIsValid()
    {
        var entity = CreateDomainEntity("ACC002", "user@test.com", "Smith", "Jane", "Plan B", 2);

        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.InsertSignatoryAccessAsync(entity);
        await repository.SaveChangesAsync();

        var count = await context.SignatoryAccessEntity.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoEntities()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetAllSignatoryAccessAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        var entities = new[]
        {
                CreateDomainEntity("ACC001", "user1@test.com", "User1", "Test1"),
                CreateDomainEntity("ACC002", "user2@test.com", "User2", "Test2"),
                CreateDomainEntity("ACC003", "user3@test.com", "User3", "Test3")
            };

        using var context = CreateContext();
        await context.SignatoryAccessEntity.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetAllSignatoryAccessAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.AccountNumber == "ACC001");
        Assert.Contains(result, e => e.AccountNumber == "ACC002");
        Assert.Contains(result, e => e.AccountNumber == "ACC003");
    }

    [Fact]
    public async Task Update_ShouldModifyExistingEntity()
    {
        var originalDomain = CreateDomainEntity(
            "ACC001", "old@test.com", "OldName", "OldFirst", "Old Plan", 1);

        using var context = CreateContext();
        await context.SignatoryAccessEntity.AddAsync(originalDomain);
        await context.SaveChangesAsync();

        var updatedDomain = CreateDomainEntity(
            "ACC001", "new@test.com", "NewName", "NewFirst", "New Plan", 5);

        var repository = new SignatoryAccessesImportRepository(context);
        repository.UpdateSignatoryAccess(updatedDomain);
        await repository.SaveChangesAsync();

        var updated = await context.SignatoryAccessEntity.FindAsync("ACC001");
        Assert.NotNull(updated);
        Assert.Equal("new@test.com", updated.Email);
        Assert.Equal("NewName", updated.LastName);
        Assert.Equal("NewFirst", updated.FirstName);
        Assert.Equal("New Plan", updated.PlanName);
        Assert.Equal(5, updated.UserCount);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        var entity = CreateDomainEntity("ACC001", "test@test.com", "Test", "User");

        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.InsertSignatoryAccessAsync(entity);
        await repository.SaveChangesAsync();

        var saved = await context.SignatoryAccessEntity.FindAsync("ACC001");
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task InsertAsync_MultipleEntities_ShouldAddAll()
    {
        var entity1 = CreateDomainEntity("ACC001", "user1@test.com", "User1", "Test1");
        var entity2 = CreateDomainEntity("ACC002", "user2@test.com", "User2", "Test2");

        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.InsertSignatoryAccessAsync(entity1);
        await repository.InsertSignatoryAccessAsync(entity2);
        await repository.SaveChangesAsync();

        var all = await repository.GetAllSignatoryAccessAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetAllAsync_WithLargeDataset_ShouldReturnAll()
    {
        var list = new List<SignatoryAccessEntity>();

        for (int i = 1; i <= 100; i++)
        {
            var domain = CreateDomainEntity(
                $"ACC{i:D6}",
                $"user{i}@test.com",
                $"LastName{i}",
                $"FirstName{i}");
            list.Add(domain);
        }

        using var context = CreateContext();
        await context.SignatoryAccessEntity.AddRangeAsync(list);
        await context.SaveChangesAsync();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetAllSignatoryAccessAsync();

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public async Task InsertAsync_ThenUpdate_ShouldWork()
    {
        var entity = CreateDomainEntity(
            "ACC001", "original@test.com", "Original", "User", "Plan", 1);

        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.InsertSignatoryAccessAsync(entity);
        await repository.SaveChangesAsync();

        entity.Email = "updated@test.com";
        entity.UserCount = 5;

        repository.UpdateSignatoryAccess(entity);
        await repository.SaveChangesAsync();

        var result = await context.SignatoryAccessEntity.FindAsync("ACC001");
        Assert.Equal("updated@test.com", result.Email);
        Assert.Equal(5, result.UserCount);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ShouldNotThrow()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        await repository.SaveChangesAsync();
        Assert.True(true);
    }

    [Fact]
    public async Task GetAllAsync_AfterMultipleInserts_ShouldReturnCorrectCount()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        for (int i = 1; i <= 5; i++)
        {
            await repository.InsertSignatoryAccessAsync(CreateDomainEntity(
                $"ACC{i:D3}",
                $"user{i}@test.com",
                $"Name{i}",
                $"First{i}"));
        }

        await repository.SaveChangesAsync();

        var result = await repository.GetAllSignatoryAccessAsync();
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetSignatoryAccessByAccountNumberAsync_Nominal()
    {
        using var context = CreateContext();

        var accountNumber = "accNumber";
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        context.SignatoryAccessEntity.Add(signatoryAccess);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessByAccountNumberAsync(accountNumber);

        Assert.NotNull(result);
        Assert.Equal(signatoryAccess.AccountNumber, result.AccountNumber);
        Assert.Equal(signatoryAccess.Email, result.Email);
        Assert.Equal(signatoryAccess.FirstName, result.FirstName);
        Assert.Equal(signatoryAccess.LastName, result.LastName);
        Assert.Equal(signatoryAccess.UserCount, result.UserCount);
        Assert.Equal(signatoryAccess.PlanName, result.PlanName);
        Assert.Equal(signatoryAccess.CompliancePriceExclTax, result.CompliancePriceExclTax);
        Assert.Equal(signatoryAccess.PlanPriceExclTax, result.PlanPriceExclTax);
        Assert.Equal(signatoryAccess.CanAccess, result.CanAccess);
        Assert.Equal(signatoryAccess.CreationDate, result.CreationDate);
        Assert.Equal(signatoryAccess.UpdatedDate, result.UpdatedDate);
        Assert.Equal(signatoryAccess.FreeTrialEndDate, result.FreeTrialEndDate);
        Assert.Equal(signatoryAccess.TermsId, result.TermsId);
    }

    [Fact]
    public async Task GetSignatoryAccessByAccountNumberAsync_When_AccountNumberNotExist_Should_ThrowNotFoundException()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        var result = await Assert.ThrowsAsync<NotFoundException>(async () => await repository.GetSignatoryAccessByAccountNumberAsync("accountNumber"));

        Assert.NotNull(result);
        Assert.Equal("PNL005", result.Code);
        Assert.Equal("SignatoryAccess with accountNumber accountNumber was not found.", result.Message);
    }

    [Fact]
    public async Task GetSignatoryAccessesByUserEmailAsync_Nominal()
    {
        using var context = CreateContext();

        var userEmail = "email";
        var accountNumber = "accNumber";
        var signatoryAccessList = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.Email, userEmail)
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .CreateMany(1);
        context.SignatoryAccessEntity.AddRange(signatoryAccessList);
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByUserEmailAsync(userEmail);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Single(result);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.AccountNumber, result.FirstOrDefault()!.Item1.AccountNumber);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.Email, result.FirstOrDefault()!.Item1.Email);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.FirstName, result.FirstOrDefault()!.Item1.FirstName);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.LastName, result.FirstOrDefault()!.Item1.LastName);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.UserCount, result.FirstOrDefault()!.Item1.UserCount);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.PlanName, result.FirstOrDefault()!.Item1.PlanName);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.CompliancePriceExclTax, result.FirstOrDefault()!.Item1.CompliancePriceExclTax);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.PlanPriceExclTax, result.FirstOrDefault()!.Item1.PlanPriceExclTax);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.CanAccess, result.FirstOrDefault()!.Item1.CanAccess);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.CreationDate, result.FirstOrDefault()!.Item1.CreationDate);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.UpdatedDate, result.FirstOrDefault()!.Item1.UpdatedDate);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.FreeTrialEndDate, result.FirstOrDefault()!.Item1.FreeTrialEndDate);
        Assert.Equal(signatoryAccessList.FirstOrDefault()!.TermsId, result.FirstOrDefault()!.Item1.TermsId);
        Assert.Equal(account.LegalName, result.FirstOrDefault()!.Item2);
    }

    [Fact]
    public async Task GetSignatoryAccessesByUserEmailAsync_When_EmailNotFound_Should_ReturnEmptyList()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByUserEmailAsync("userEmail");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSignatoryAccessesByUserEmailAsync_When_DuplicatedAccounts_Should_ReturnSingleEntryPerAccountNumber()
    {
        using var context = CreateContext();

        var userEmail = "email";
        var accountNumber = "accNumber";
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.Email, userEmail)
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        context.SignatoryAccessEntity.Add(signatoryAccess);

        // Deux lignes Account avec le même AccountNumber (pas de contrainte d'unicité en base)
        var firstAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        var duplicateAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 2)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        context.AccountEntity.AddRange(firstAccount, duplicateAccount);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByUserEmailAsync(userEmail);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(accountNumber, result.First().Item1.AccountNumber);
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_When_DuplicatedAccounts_Should_ReturnSingleEntryPerAccountNumber()
    {
        using var context = CreateContext();

        var accountNumber = "ACC001";
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        context.SignatoryAccessEntity.Add(signatoryAccess);

        // Deux lignes Account avec le même AccountNumber (pas de contrainte d'unicité en base)
        var firstAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        var duplicateAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 2)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        context.AccountEntity.AddRange(firstAccount, duplicateAccount);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync([accountNumber]);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(accountNumber, result.First().Item1.AccountNumber);
    }

    [Fact]
    public async Task ValidateTermsAsync_AddsValidationHistory()
    {
        var accountNumber = "ACC001";
        var termsId = 10;
        var status = true;

        using var context = CreateContext();

        var terms = _fixture.Build<TermsEntity>()
            .With(t => t.Id, termsId)
            .Without(t => t.ValidationHistoryEntity)
            .Without(t => t.SignatoryAccessEntity)
            .Create();
        context.TermsEntity.Add(terms);

        var sourceData = CreateDomainEntity(accountNumber, "sig@test.com", "Dupont", "Jean", "Plan Pro", 3);
        sourceData.PlanPriceExclTax = 49.90m;
        sourceData.CompliancePriceExclTax = 150m;

        var repository = new SignatoryAccessesImportRepository(context);

        // Act
        repository.ValidateTerms(accountNumber, termsId, status, sourceData);
        await repository.SaveChangesAsync();

        // Assert
        var history = await context.ValidationHistoryEntity.SingleAsync();

        Assert.Equal(1, await context.ValidationHistoryEntity.CountAsync());
        Assert.Equal(accountNumber, history.AccountNumber);
        Assert.Equal(termsId, history.TermsId);
        Assert.Equal(status, history.Status);
        Assert.Equal("sig@test.com", history.Email);
        Assert.Equal("Dupont", history.LastName);
        Assert.Equal("Jean", history.FirstName);
        Assert.Equal("Plan Pro", history.PlanName);
        Assert.Equal(3, history.UserCount);
        Assert.Equal(49.90m, history.PlanPriceExclTax);
        Assert.Equal(150m, history.CompliancePriceExclTax);
    }

    [Fact]
    public async Task GetTermsIdByLinkFrAsync_Nominal()
    {
        const string termsName = "Conditions générales en vigueur - mise à jour fevrier 2026 - RYDGE Conseil (1).pdf";

        using var context = CreateContext();

        var terms = _fixture.Build<TermsEntity>()
            .With(t => t.LinkFr, termsName)
            .Without(t => t.ValidationHistoryEntity)
            .Without(t => t.SignatoryAccessEntity)
            .Create();
        context.TermsEntity.Add(terms);
        await context.SaveChangesAsync();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetTermsIdByLinkFrAsync(termsName);

        Assert.Equal(terms.Id, result);
    }

    [Fact]
    public async Task GetTermsIdByLinkFrAsync_WhenTermsNotExist_Should_ThrowNotFoundException()
    {
        const string termsName = "Conditions générales en vigueur - mise à jour fevrier 2026 - RYDGE Conseil (2).pdf";

        using var context = CreateContext();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await Assert.ThrowsAsync<NotFoundException>(async () => await repository.GetTermsIdByLinkFrAsync(termsName));

        Assert.Equal("PNL006", result.Code);
        Assert.Equal($"No Terms with name '{termsName}' is configured.", result.Message);
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_ShouldReturnEmpty_WhenAccountNumbersIsEmpty()
    {
        using var context = CreateContext();
        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync([]);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_ShouldReturnMatchingEntries_WithLegalName()
    {
        using var context = CreateContext();

        var accountNumber = "ACC001";
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Create();

        context.SignatoryAccessEntity.Add(signatoryAccess);
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync([accountNumber]);

        Assert.NotNull(result);
        Assert.Single(result);
        var tuple = result.First();
        Assert.Equal(accountNumber, tuple.Item1.AccountNumber);
        Assert.Equal(account.LegalName, tuple.Item2);
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_ShouldReturnEmpty_WhenNoMatchingAccountNumbers()
    {
        using var context = CreateContext();

        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.AccountNumber, "ACC001")
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC001")
            .Create();

        context.SignatoryAccessEntity.Add(signatoryAccess);
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync(["ACC999"]);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_ShouldReturnOnlyMatchingAccountNumbers()
    {
        using var context = CreateContext();

        var accountNumbers = new[] { "ACC001", "ACC002", "ACC003" };
        foreach (var accountNumber in accountNumbers)
        {
            var sa = _fixture.Build<SignatoryAccessEntity>()
                .With(s => s.AccountNumber, accountNumber)
                .Without(s => s.ValidationHistoryEntity)
                .Create();
            var account = _fixture.Build<AccountEntity>()
                .With(a => a.AccountNumber, accountNumber)
                .Create();
            context.SignatoryAccessEntity.Add(sa);
            context.AccountEntity.Add(account);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync(["ACC001", "ACC003"]);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Item1.AccountNumber == "ACC001");
        Assert.Contains(result, t => t.Item1.AccountNumber == "ACC003");
        Assert.DoesNotContain(result, t => t.Item1.AccountNumber == "ACC002");
    }

    [Fact]
    public async Task GetSignatoryAccessesByAccountNumbersAsync_ShouldMapAllFieldsCorrectly()
    {
        using var context = CreateContext();

        var accountNumber = "ACC001";
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.AccountNumber, accountNumber)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Create();

        context.SignatoryAccessEntity.Add(signatoryAccess);
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SignatoryAccessesImportRepository(context);

        var result = await repository.GetSignatoryAccessesByAccountNumbersAsync([accountNumber]);

        var tuple = result.First();
        Assert.Equal(signatoryAccess.Email, tuple.Item1.Email);
        Assert.Equal(signatoryAccess.FirstName, tuple.Item1.FirstName);
        Assert.Equal(signatoryAccess.LastName, tuple.Item1.LastName);
        Assert.Equal(signatoryAccess.PlanName, tuple.Item1.PlanName);
        Assert.Equal(signatoryAccess.UserCount, tuple.Item1.UserCount);
        Assert.Equal(signatoryAccess.CanAccess, tuple.Item1.CanAccess);
        Assert.Equal(signatoryAccess.TermsId, tuple.Item1.TermsId);
        Assert.Equal(account.LegalName, tuple.Item2);
    }

    // Note : DeleteSignatoryAccessesAsync utilise ExecuteDeleteAsync (non supporté par InMemory).
    // La logique est couverte par les tests service via Moq.

    // ===================================================================
    // Test ValidateTerms avec données source (Scénario 6)
    // ===================================================================

    [Fact]
    public async Task ValidateTermsAsync_StoresSourceDataFromSignatoryAccess()
    {
        var accountNumber = "ACC001";
        var termsId = 10;

        using var context = CreateContext();

        var terms = _fixture.Build<TermsEntity>()
            .With(t => t.Id, termsId)
            .Without(t => t.ValidationHistoryEntity)
            .Without(t => t.SignatoryAccessEntity)
            .Create();
        context.TermsEntity.Add(terms);

        var sourceData = new SignatoryAccessEntity
        {
            AccountNumber = accountNumber,
            Email = "sig@company.com",
            LastName = "Martin",
            FirstName = "Sophie",
            PlanName = "Essentiel",
            UserCount = 10,
            PlanPriceExclTax = 99.99m,
            CompliancePriceExclTax = 200m,
        };

        var repository = new SignatoryAccessesImportRepository(context);

        // Act - test refus
        repository.ValidateTerms(accountNumber, termsId, false, sourceData);
        await repository.SaveChangesAsync();

        // Assert
        var history = await context.ValidationHistoryEntity.SingleAsync();

        Assert.False(history.Status);
        Assert.Equal("sig@company.com", history.Email);
        Assert.Equal("Martin", history.LastName);
        Assert.Equal("Sophie", history.FirstName);
        Assert.Equal("Essentiel", history.PlanName);
        Assert.Equal(10, history.UserCount);
        Assert.Equal(99.99m, history.PlanPriceExclTax);
        Assert.Equal(200m, history.CompliancePriceExclTax);
    }
}
