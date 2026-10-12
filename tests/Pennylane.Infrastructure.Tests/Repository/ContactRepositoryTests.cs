// <copyright file="ContactRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Models.Contacts;
using Application.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AutoFixture;
using Application.Mappers;
using Pulse.ContactRegistry.Infrastructure.Context;
using Pennylane.Domain.Entities;

namespace Pennylane.Infrastructure.Tests.Repository;

public class ContactRepositoryTests
{
    private readonly Fixture _fixture;
    private DbContextOptions<PennylaneContext> CreateInMemoryOptions(string name) =>
        new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase($"{name}_{Guid.NewGuid()}")
            .Options;

    private ContactRepository CreateRepository(PennylaneContext ctx) =>
        new ContactRepository(ctx, Mock.Of<ILogger<ContactRepository>>());

    public ContactRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors
            .OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    #region AddContactAsync

    [Fact]
    public async Task AddContactAsync_ShouldReturnFalse_IfContactAlreadyExists()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(AddContactAsync_ShouldReturnFalse_IfContactAlreadyExists));
        using var ctx = new PennylaneContext(opts);
        var existing = _fixture.Create<ContactEntity>();
        ctx.ContactEntity.Add(existing);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);
        var model = existing.MapContactEntityToModel();

        // Act
        var result = await repo.AddContactAsync(model);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddContactAsync_ShouldReturnTrue_IfContactDoesNotExist()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(AddContactAsync_ShouldReturnTrue_IfContactDoesNotExist));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);
        var model = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, _fixture.Create<string>())
            .Create();

        // Act
        var result = await repo.AddContactAsync(model);

        // Assert
        Assert.True(result);
        var entity = await ctx.ContactEntity.FindAsync(model.ContactId);
        Assert.NotNull(entity);
        Assert.Equal(model.Email, entity.Email);
    }

    [Fact]
    public async Task AddContactAsync_WhenSaveThrows_ReturnsFalse()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(AddContactAsync_WhenSaveThrows_ReturnsFalse));
        using var ctx = new ThrowingContactContext(opts);
        var repo = CreateRepository(ctx);
        var model = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, _fixture.Create<string>())
            .Create();

        // Act
        var result = await repo.AddContactAsync(model);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region UpdateContactAsync

    [Fact]
    public async Task UpdateContactAsync_ShouldReturnFalse_IfContactDoesNotExist()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(UpdateContactAsync_ShouldReturnFalse_IfContactDoesNotExist));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);
        var model = _fixture.Create<Contact>();

        // Act
        var result = await repo.UpdateContactAsync(model);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateContactAsync_ShouldReturnTrue_IfContactExists()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(UpdateContactAsync_ShouldReturnTrue_IfContactExists));
        using var ctx = new PennylaneContext(opts);
        var existing = _fixture.Create<ContactEntity>();
        ctx.ContactEntity.Add(existing);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);
        var model = existing.MapContactEntityToModel();
        var newGuid = Guid.NewGuid();
        model.ContactGlobalUniqueId = newGuid;

        // Act
        var result = await repo.UpdateContactAsync(model);

        // Assert
        Assert.True(result);
        var updated = await ctx.ContactEntity.FindAsync(existing.ContactId);
        Assert.NotNull(updated);
        Assert.Equal(newGuid, updated.ContactGlobalUniqueId);
    }

    [Fact]
    public async Task UpdateContactAsync_WhenSaveThrows_ReturnsFalse()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(UpdateContactAsync_WhenSaveThrows_ReturnsFalse));
        using var ctx = new ThrowingContactContext(opts);
        var repo = CreateRepository(ctx);
        var model = _fixture.Create<Contact>();

        // Act
        var result = await repo.UpdateContactAsync(model);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region DeleteContactByIdAsync

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task DeleteContactByIdAsync_InvalidId_ReturnsFalse(int? id)
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DeleteContactByIdAsync_InvalidId_ReturnsFalse));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DeleteContactByIdAsync(id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteContactByIdAsync_NotFound_ReturnsFalse()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DeleteContactByIdAsync_NotFound_ReturnsFalse));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DeleteContactByIdAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteContactByIdAsync_Existing_RemovesContactAndRoles_ReturnsTrue()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DeleteContactByIdAsync_Existing_RemovesContactAndRoles_ReturnsTrue));
        using var ctx = new PennylaneContext(opts);
        var contact = _fixture.Build<ContactEntity>()
            .Without(c => c.RoleEntity)
            .Without(c => c.PnlRoleEntity)
            .Create();
        ctx.ContactEntity.Add(contact);
        var role = _fixture.Build<RoleEntity>()
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.AccountId, _fixture.Create<int>())
            .Without(r => r.Account)
            .Without(r => r.Contact)
            .Create();
        ctx.RoleEntity.Add(role);
        await ctx.SaveChangesAsync();

        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DeleteContactByIdAsync(contact.ContactId);

        // Assert
        Assert.True(result);
        Assert.Null(await ctx.ContactEntity.FindAsync(contact.ContactId));
        Assert.Empty(ctx.RoleEntity.Where(r => r.ContactId == contact.ContactId));
    }

    [Fact]
    public async Task DeleteContactByIdAsync_WhenSaveThrows_ReturnsFalse()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DeleteContactByIdAsync_WhenSaveThrows_ReturnsFalse));
        using var ctx = new ThrowingContactContext(opts);
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DeleteContactByIdAsync(_fixture.Create<int>());

        // Assert
        Assert.False(result);
    }

    #endregion

    #region DoesContactExistByEmailOrIdAsync

    [Fact]
    public async Task DoesContactExistByEmailOrIdAsync_ReturnsFalse_WhenNeitherProvided()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DoesContactExistByEmailOrIdAsync_ReturnsFalse_WhenNeitherProvided));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DoesContactExistByEmailOrIdAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DoesContactExistByEmailOrIdAsync_ReturnsTrue_ForEmail()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DoesContactExistByEmailOrIdAsync_ReturnsTrue_ForEmail));
        using var ctx = new PennylaneContext(opts);
        var entity = _fixture.Build<ContactEntity>()
            .With(c => c.Email, _fixture.Create<string>())
            .Create();
        ctx.ContactEntity.Add(entity);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DoesContactExistByEmailOrIdAsync(email: entity.Email);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DoesContactExistByEmailOrIdAsync_ReturnsTrue_ForId()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(DoesContactExistByEmailOrIdAsync_ReturnsTrue_ForId));
        using var ctx = new PennylaneContext(opts);
        var entity = _fixture.Create<ContactEntity>();
        ctx.ContactEntity.Add(entity);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.DoesContactExistByEmailOrIdAsync(contactId: entity.ContactId);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region GetContactByEmailOrIdAsync

    [Fact]
    public async Task GetContactByEmailOrIdAsync_ReturnsNull_WhenNeitherProvided()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(GetContactByEmailOrIdAsync_ReturnsNull_WhenNeitherProvided));
        using var ctx = new PennylaneContext(opts);
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.GetContactByEmailOrIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetContactByEmailOrIdAsync_ReturnsContact_ForEmail()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(GetContactByEmailOrIdAsync_ReturnsContact_ForEmail));
        using var ctx = new PennylaneContext(opts);
        var entity = _fixture.Create<ContactEntity>();
        ctx.ContactEntity.Add(entity);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.GetContactByEmailOrIdAsync(email: entity.Email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.Email, result!.Email);
    }

    [Fact]
    public async Task GetContactByEmailOrIdAsync_ReturnsContact_ForId()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(GetContactByEmailOrIdAsync_ReturnsContact_ForId));
        using var ctx = new PennylaneContext(opts);
        var entity = _fixture.Create<ContactEntity>();
        ctx.ContactEntity.Add(entity);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.GetContactByEmailOrIdAsync(contactId: entity.ContactId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.FirstName, result!.FirstName);
    }

    #endregion

    #region GetContactByGlobalUniqueId
    [Fact]
    public async Task GetContactByGlobalUniqueId_ReturnsContact()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(GetContactByGlobalUniqueId_ReturnsContact));
        var contactGlobalUniqueId = Guid.NewGuid();
        using var ctx = new PennylaneContext(opts);
        var entity = _fixture.Build<ContactEntity>()
            .With(c => c.ContactGlobalUniqueId, contactGlobalUniqueId)
            .Create();

        ctx.ContactEntity.Add(entity);
        await ctx.SaveChangesAsync();
        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.GetContactByGlobalUniqueId(contactGlobalUniqueId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Contact>(result);
    }

    [Fact]
    public async Task GetContactByGlobalUniqueId_Contact_NotFound_ReturnsNull()
    {
        // Arrange
        var opts = CreateInMemoryOptions(nameof(GetContactByGlobalUniqueId_ReturnsContact));
        var contactGlobalUniqueId = Guid.NewGuid();
        using var ctx = new PennylaneContext(opts);

        var repo = CreateRepository(ctx);

        // Act
        var result = await repo.GetContactByGlobalUniqueId(contactGlobalUniqueId);

        // Assert
        Assert.Null(result);
    }
    #endregion

    // Subclass of PennylaneContext that throws on SaveChangesAsync
    private class ThrowingContactContext : PennylaneContext
    {
        public ThrowingContactContext(DbContextOptions<PennylaneContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Simulated failure");
        }
    }
}
