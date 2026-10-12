// <copyright file="AccountRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Exceptions;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using AutoFixture;
using Pulse.ContactRegistry.Infrastructure.Context;
using Pennylane.Domain.Entities;

namespace Pennylane.Infrastructure.Tests.Repository;

public class AccountRepositoryTests
{
    private readonly Fixture _fixture;

    public AccountRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private DbContextOptions<PennylaneContext> CreateInMemoryOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase($"{databaseName}_{Guid.NewGuid()}")
            .Options;
    }

    private AccountRepository CreateRepository(PennylaneContext context)
    {
        return new AccountRepository(context);
    }

    [Fact]
    public async Task AddAccountAsync_Should_Add_Account()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(AddAccountAsync_Should_Add_Account));
        await using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        var account = _fixture.Create<AccountEntity>();

        // Act
        await repository.AddAccountAsync(account);
        var result = await context.AccountEntity.FindAsync(account.AccountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.AccountNumber, result!.AccountNumber);
    }

    [Fact]
    public async Task GetAccountByNumberOrIdAsync_Using_AccountNumber_Should_Return_Account()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAccountByNumberOrIdAsync_Using_AccountNumber_Should_Return_Account));
        await using var context = new PennylaneContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAccountByNumberOrIdAsync(account.AccountNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.AccountNumber, result!.AccountNumber);
    }

    [Fact]
    public async Task GetAccountByNumberOrIdAsync_Using_AccountId_Should_Return_Account()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAccountByNumberOrIdAsync_Using_AccountId_Should_Return_Account));
        await using var context = new PennylaneContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetAccountByNumberOrIdAsync(account.AccountId.ToString());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.AccountNumber, result!.AccountNumber);
    }

    [Fact]
    public async Task GetAccountByNumberOrIdAsync_WhenNoMatch_ReturnsNull()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GetAccountByNumberOrIdAsync_WhenNoMatch_ReturnsNull));
        await using var context = new PennylaneContext(options);
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetAccountByNumberOrIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAccountAsync_Should_Update_Account()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(UpdateAccountAsync_Should_Update_Account));
        await using var context = new PennylaneContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();

        // Act
        var newGuid = _fixture.Create<Guid>();
        account.AccountGlobalUniqueId = newGuid;
        await repository.UpdateAccountAsync(account);

        // Assert
        var updated = await context.AccountEntity.FindAsync(account.AccountId);
        Assert.NotNull(updated);
        Assert.Equal(newGuid, updated!.AccountGlobalUniqueId);
    }

    [Fact]
    public async Task RemoveAccountAsync_Should_Remove_Account()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(RemoveAccountAsync_Should_Remove_Account));
        await using var context = new PennylaneContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Build<AccountEntity>()
            .Without(a => a.PnlRoleEntity)
            .Without(a => a.RoleEntity)
            .Create();
        context.AccountEntity.Add(account);
        await context.SaveChangesAsync();

        // Act
        await repository.RemoveAccountAsync(account);

        // Assert
        var removed = await context.AccountEntity.FindAsync(account.AccountId);
        Assert.Null(removed);
    }

    // Subclass to simulate SaveChangesAsync throwing a DbUpdateException
    private class ThrowingContext : PennylaneContext
    {
        public ThrowingContext(DbContextOptions<PennylaneContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Simulated failure", new Exception("inner"));
    }

    [Fact]
    public async Task AddAccountAsync_WhenSaveFails_ThrowsDbOperationException()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(AddAccountAsync_WhenSaveFails_ThrowsDbOperationException));
        await using var context = new ThrowingContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbOperationException>(() => repository.AddAccountAsync(account));
        Assert.Contains("Something went wrong while saving data", ex.Message);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenSaveFails_ThrowsDbOperationException()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(UpdateAccountAsync_WhenSaveFails_ThrowsDbOperationException));
        await using var context = new ThrowingContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAccountAsync(account));
    }

    [Fact]
    public async Task RemoveAccountAsync_WhenSaveFails_ThrowsDbOperationException()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(RemoveAccountAsync_WhenSaveFails_ThrowsDbOperationException));
        await using var context = new ThrowingContext(options);

        var repository = CreateRepository(context);
        var account = _fixture.Create<AccountEntity>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RemoveAccountAsync(account));
    }
}
