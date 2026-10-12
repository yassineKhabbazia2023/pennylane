// <copyright file="PennylaneRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pennylane.Infrastructure.Tests.Utils;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Tests.Repository;

public class PennylaneRepositoryTests
{
    private readonly IFixture _fixture;

    public PennylaneRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private PennylaneContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new PennylaneContext(options);
    }

    [Fact]
    public async Task FetchPennylaneRoleByAccountAndContactIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var entity = _fixture.Build<PnlRoleEntity>()
                             .With(e => e.AccountId, 123)
                             .With(e => e.ContactId, 456)
                             .Without(e => e.Account)
                             .Without(e => e.Contact)
                             .Create();
        await context.PnlRoleEntity.AddAsync(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new PennylaneRepository(context);

        // Act
        var result = await repository.FetchPennylaneRoleByAccountAndContactIdAsync(123, 456);

        // Assert
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(123);
        result.ContactId.Should().Be(456);
        result.Role.Should().Be(entity.Role);
    }

    [Fact]
    public async Task FetchPennylaneRoleByAccountAndContactIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var repository = new PennylaneRepository(context);

        // Act
        var result = await repository.FetchPennylaneRoleByAccountAndContactIdAsync(999, 888);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldAddEntityToContext()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var repository = new PennylaneRepository(context);
        var newEntity = _fixture.Build<PnlRoleEntity>()
                                .With(e => e.AccountId, 321)
                                .With(e => e.ContactId, 654)
                                .Without(e => e.Account)
                                .Without(e => e.Contact)
                                .Create();

        // Act
        await repository.InsertPennylaneRoleAsync(newEntity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Assert
        var saved = await context.PnlRoleEntity
            .FirstOrDefaultAsync(e => e.AccountId == 321 && e.ContactId == 654);
        saved.Should().NotBeNull();
        saved!.Role.Should().Be(newEntity.Role);
    }


    [Fact]
    public async Task PennylaneRoleExistsAsync_ShouldReturnTrue_WhenRoleExists()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(PennylaneRoleExistsAsync_ShouldReturnTrue_WhenRoleExists));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .With(a => a.AccountNumber, "ACC-001")
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Create();

        var contact = _fixture.Build<ContactEntity>()
            .With(c => c.ContactId, 1)
            .With(c => c.Email, "user@test.com")
            .Without(c => c.RoleEntity)
            .Without(c => c.PnlRoleEntity)
            .Create();

        var role = _fixture.Build<PnlRoleEntity>()
            .With(r => r.AccountId, 1)
            .With(r => r.ContactId, 1)
            .Without(r => r.Account)
            .Without(r => r.Contact)
            .Create();

        context.AddRange(account, contact);
        await context.PnlRoleEntity.AddAsync(role);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act
        var result = await repository.PennylaneRoleExistsAsync("ACC-001", "user@test.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PennylaneRoleExistsAsync_ShouldReturnFalse_WhenRoleDoesNotExist()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(PennylaneRoleExistsAsync_ShouldReturnFalse_WhenRoleDoesNotExist));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        // Act
        var result = await repository.PennylaneRoleExistsAsync("ACC-999", "nobody@test.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_ShouldRemoveEntity_WhenExists()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(DeletePennylaneRoleAsync_ShouldRemoveEntity_WhenExists));

        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);

        var repository = new PennylaneRepository(context);

        var entity = _fixture.Build<PnlRoleEntity>()
                             .With(e => e.AccountId, 10)
                             .With(e => e.ContactId, 20)
                             .Without(e => e.Account)
                             .Without(e => e.Contact)
                             .Create();


        // Seed the entities
        context.AddRange(
            _fixture.Build<AccountEntity>().With(a => a.AccountId, 10).Without(a => a.RoleEntity).Without(a => a.PnlRoleEntity).Create(),
            _fixture.Build<ContactEntity>().With(c => c.ContactId, 20).Without(a => a.RoleEntity).Without(a => a.PnlRoleEntity).Create()
        );
        await context.PnlRoleEntity.AddAsync(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act
        await repository.DeletePennylaneRoleAsync(entity);

        // Assert
        var still = await context.PnlRoleEntity
            .AnyAsync(e => e.AccountId == 10 && e.ContactId == 20);
        still.Should().BeFalse();
    }

    // ── UpsertPennylaneRoleByEmailAndAccountNumberAsync ───────────────────────

    [Fact]
    public async Task UpsertPennylaneRoleByEmailAndAccountNumberAsync_ContactNotFound_DoesNothing()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(UpsertPennylaneRoleByEmailAndAccountNumberAsync_ContactNotFound_DoesNothing));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        // Only seed an account — no contact
        context.Add(_fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .With(a => a.AccountNumber, "ACC-001")
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Create());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — contact email does not exist
        await repository.UpsertPennylaneRoleByEmailAndAccountNumberAsync("unknown@test.com", "ACC-001", "executive");

        // Assert — no role inserted
        var any = await context.PnlRoleEntity.AnyAsync();
        any.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertPennylaneRoleByEmailAndAccountNumberAsync_AccountNotFound_DoesNothing()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(UpsertPennylaneRoleByEmailAndAccountNumberAsync_AccountNotFound_DoesNothing));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        // Only seed a contact — no account
        context.Add(_fixture.Build<ContactEntity>()
            .With(c => c.ContactId, 1)
            .With(c => c.Email, "user@test.com")
            .Without(c => c.RoleEntity)
            .Without(c => c.PnlRoleEntity)
            .Create());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — account number does not exist
        await repository.UpsertPennylaneRoleByEmailAndAccountNumberAsync("user@test.com", "UNKNOWN-ACC", "executive");

        // Assert — no role inserted
        var any = await context.PnlRoleEntity.AnyAsync();
        any.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertPennylaneRoleByEmailAndAccountNumberAsync_RoleExists_UpdatesRole()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(UpsertPennylaneRoleByEmailAndAccountNumberAsync_RoleExists_UpdatesRole));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        context.AddRange(
            _fixture.Build<AccountEntity>()
                .With(a => a.AccountId, 1).With(a => a.AccountNumber, "ACC-001")
                .Without(a => a.RoleEntity).Without(a => a.PnlRoleEntity).Create(),
            _fixture.Build<ContactEntity>()
                .With(c => c.ContactId, 1).With(c => c.Email, "user@test.com")
                .Without(c => c.RoleEntity).Without(c => c.PnlRoleEntity).Create()
        );
        await context.PnlRoleEntity.AddAsync(_fixture.Build<PnlRoleEntity>()
            .With(r => r.AccountId, 1).With(r => r.ContactId, 1).With(r => r.Role, "employee")
            .Without(r => r.Account).Without(r => r.Contact).Create());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act
        await repository.UpsertPennylaneRoleByEmailAndAccountNumberAsync("user@test.com", "ACC-001", "executive");

        // Assert — role updated, still only one row
        var role = await context.PnlRoleEntity.FirstOrDefaultAsync(r => r.ContactId == 1 && r.AccountId == 1);
        role.Should().NotBeNull();
        role!.Role.Should().Be("executive");
        context.PnlRoleEntity.Count().Should().Be(1);
    }

    [Fact]
    public async Task UpsertPennylaneRoleByEmailAndAccountNumberAsync_RoleDoesNotExist_InsertsRole()
    {
        // Arrange
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(nameof(UpsertPennylaneRoleByEmailAndAccountNumberAsync_RoleDoesNotExist_InsertsRole));
        using var context = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repository = new PennylaneRepository(context);

        context.AddRange(
            _fixture.Build<AccountEntity>()
                .With(a => a.AccountId, 1).With(a => a.AccountNumber, "ACC-001")
                .Without(a => a.RoleEntity).Without(a => a.PnlRoleEntity).Create(),
            _fixture.Build<ContactEntity>()
                .With(c => c.ContactId, 1).With(c => c.Email, "user@test.com")
                .Without(c => c.RoleEntity).Without(c => c.PnlRoleEntity).Create()
        );
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act — no existing role → insert
        await repository.UpsertPennylaneRoleByEmailAndAccountNumberAsync("user@test.com", "ACC-001", "executive");

        // Assert — new row created
        var role = await context.PnlRoleEntity.FirstOrDefaultAsync(r => r.ContactId == 1 && r.AccountId == 1);
        role.Should().NotBeNull();
        role!.Role.Should().Be("executive");
    }
}
