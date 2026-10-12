// <copyright file="RoleRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Pulse.ContactRegistry.Infrastructure.Context;
using Pennylane.Domain.Entities;
using Pennylane.Infrastructure.Repository;

namespace Pennylane.Infrastructure.Tests.Repository;

public class RoleRepositoryTests
{
    private readonly Fixture _fixture;

    public RoleRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private DbContextOptions<PennylaneContext> GetDbOptions()
    {
        return new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task AddRoleAsync_ShouldNotAddRole_WhenRoleAlreadyExists()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.Add(roleEntity);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        var initialCount = context.RoleEntity.Count();

        // Act
        await repository.AddRoleAsync(roleEntity);

        // Assert
        context.RoleEntity.Count().Should().Be(initialCount);
    }

    [Fact]
    public async Task AddRoleAsync_ShouldAddRole_WhenRoleDoesNotExist()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();

        using var context = new PennylaneContext(GetDbOptions());
        var repository = new RoleRepository(context);
        var initialCount = context.RoleEntity.Count();

        // Act
        await repository.AddRoleAsync(roleEntity);

        // Assert
        context.RoleEntity.Count().Should().Be(initialCount + 1);
        var savedRole = await context.RoleEntity.FirstOrDefaultAsync(r =>
            r.ContactId == roleEntity.ContactId && r.AccountId == roleEntity.AccountId);
        savedRole.Should().NotBeNull();
    }

    [Fact]
    public async Task AddRoleAsync_RoleNotExists_Should_InsertWithIsSignatory()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();
        roleEntity.IsSignatory = true;

        using var context = new PennylaneContext(GetDbOptions());
        var repository = new RoleRepository(context);

        // Act
        await repository.AddRoleAsync(roleEntity);

        // Assert
        var savedRole = await context.RoleEntity.FirstOrDefaultAsync(r =>
            r.ContactId == roleEntity.ContactId && r.AccountId == roleEntity.AccountId);
        savedRole.Should().NotBeNull();
        savedRole!.IsSignatory.Should().BeTrue();
    }

    [Fact]
    public async Task AddRoleAsync_RoleExists_IsSignatoryDiffers_Should_Update()
    {
        // Arrange
        var dbOptions = GetDbOptions();
        var roleEntity = _fixture.Create<RoleEntity>();
        roleEntity.IsSignatory = null;

        using (var context = new PennylaneContext(dbOptions))
        {
            context.RoleEntity.Add(roleEntity);
            await context.SaveChangesAsync();
        }

        using (var context = new PennylaneContext(dbOptions))
        {
            var repository = new RoleRepository(context);
            var updatedRole = new RoleEntity
            {
                ContactId = roleEntity.ContactId,
                AccountId = roleEntity.AccountId,
                IsSignatory = true,
            };

            // Act
            await repository.AddRoleAsync(updatedRole);

            // Assert
            var savedRole = await context.RoleEntity.FirstOrDefaultAsync(r =>
                r.ContactId == roleEntity.ContactId && r.AccountId == roleEntity.AccountId);
            savedRole.Should().NotBeNull();
            savedRole!.IsSignatory.Should().BeTrue();
        }
    }

    [Fact]
    public async Task AddRoleAsync_RoleExists_IsSignatorySame_Should_NotModify()
    {
        // Arrange
        var dbOptions = GetDbOptions();
        var roleEntity = _fixture.Create<RoleEntity>();
        roleEntity.IsSignatory = true;

        using (var context = new PennylaneContext(dbOptions))
        {
            context.RoleEntity.Add(roleEntity);
            await context.SaveChangesAsync();
        }

        using (var context = new PennylaneContext(dbOptions))
        {
            var repository = new RoleRepository(context);
            var sameRole = new RoleEntity
            {
                ContactId = roleEntity.ContactId,
                AccountId = roleEntity.AccountId,
                IsSignatory = true,
            };

            // Act
            await repository.AddRoleAsync(sameRole);

            // Assert
            var savedRole = await context.RoleEntity.FirstOrDefaultAsync(r =>
                r.ContactId == roleEntity.ContactId && r.AccountId == roleEntity.AccountId);
            savedRole.Should().NotBeNull();
            savedRole!.IsSignatory.Should().BeTrue();
            context.RoleEntity.Count().Should().Be(1);
        }
    }

    [Fact]
    public async Task DeleteRoleAsync_ShouldRemoveRole_WhenRoleExists()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.Add(roleEntity);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        var initialCount = context.RoleEntity.Count();

        // Act
        await repository.DeleteRoleAsync(roleEntity);

        // Assert
        context.RoleEntity.Count().Should().Be(initialCount - 1);
        var deletedRole = await context.RoleEntity.FirstOrDefaultAsync(r =>
            r.ContactId == roleEntity.ContactId && r.AccountId == roleEntity.AccountId);
        deletedRole.Should().BeNull();
    }

    [Fact]
    public async Task CheckRoleExistsAsync_ShouldReturnTrue_WhenRoleExists()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();
        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.Add(roleEntity);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Act
        var exists = await repository.CheckRoleExistsAsync(roleEntity.Account.AccountNumber, roleEntity.Contact.Email);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckRoleExistsAsync_ShouldReturnFalse_WhenRoleDoesNotExist()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();
        using var context = new PennylaneContext(GetDbOptions());
        // Pas d'ajout dans la base
        var repository = new RoleRepository(context);

        // Act
        var exists = await repository.CheckRoleExistsAsync(roleEntity.Account.AccountNumber, roleEntity.Contact.Email);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserRolesAsync_ShouldReturnRoles_WhenContactEmailMatches()
    {
        // Arrange
        var contactEmail = "test@example.com";
        var roleEntities = _fixture.CreateMany<RoleEntity>(3).ToList();
        roleEntities.ForEach(r => r.Contact.Email = contactEmail);

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.AddRange(roleEntities);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetUserRolesAsync(contactEmail);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(r => r.Contact.Email == contactEmail);
    }

    [Fact]
    public async Task GetUserRolesAsync_ShouldReturnEmpty_WhenNoRoleMatchesContactEmail()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.Add(roleEntity);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetUserRolesAsync("unknown@example.com");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_ShouldReturnOnlyMatchingRoles_WhenMultipleContactsExist()
    {
        // Arrange
        var targetEmail = "target@example.com";
        var targetRoles = _fixture.CreateMany<RoleEntity>(2).ToList();
        targetRoles.ForEach(r => r.Contact.Email = targetEmail);

        var otherRoles = _fixture.CreateMany<RoleEntity>(2).ToList();

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.AddRange(targetRoles);
        context.RoleEntity.AddRange(otherRoles);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetUserRolesAsync(targetEmail);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Contact.Email == targetEmail);
    }

    [Fact]
    public async Task GetUserRolesAsync_ShouldIncludeContactAndAccount()
    {
        // Arrange
        var roleEntity = _fixture.Create<RoleEntity>();

        using var context = new PennylaneContext(GetDbOptions());
        context.RoleEntity.Add(roleEntity);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);

        // Act
        var result = await repository.GetUserRolesAsync(roleEntity.Contact.Email);

        // Assert
        var role = result.Should().ContainSingle().Subject;
        role.Contact.Should().NotBeNull();
        role.Account.Should().NotBeNull();
    }
}