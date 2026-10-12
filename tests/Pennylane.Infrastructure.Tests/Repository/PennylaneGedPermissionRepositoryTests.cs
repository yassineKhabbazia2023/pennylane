// <copyright file="PennylaneGedPermissionRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Permissions;
using Pennylane.Infrastructure.Repository;
using Pennylane.Infrastructure.Tests.Utils;

namespace Pennylane.Infrastructure.Tests.Repository;

public class PennylaneGedPermissionRepositoryTests
{
    private readonly Mock<ILogger<PennylaneGedPermissionRepository>> _loggerMock;

    public PennylaneGedPermissionRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<PennylaneGedPermissionRepository>>();
    }

    private TestContext GetTestContext()
    {
        var options = DbContextMockExtensions.CreateSqliteInMemoryOptions(Guid.NewGuid().ToString());
        return DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
    }

    #region AddPermissionAsync Tests

    [Fact]
    public async Task AddPermissionAsync_AddsNewPermission_WhenNotExists()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);
        var contactId = 1;
        var accountId = 100;

        // Act
        var result = await repo.AddPermissionAsync(contactId, accountId);

        // Assert
        Assert.True(result);
        var permission = await context.PennylaneGedPermissionEntity
            .FirstOrDefaultAsync(p => p.ContactId == contactId && p.AccountId == accountId);
        Assert.NotNull(permission);
        Assert.Equal(contactId, permission.ContactId);
        Assert.Equal(accountId, permission.AccountId);
        Assert.True(permission.GrantedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AddPermissionAsync_ReturnsTrue_WhenPermissionAlreadyExists()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId = 1;
        var accountId = 100;

        context.PennylaneGedPermissionEntity.Add(new PennylaneGedPermissionEntity
        {
            ContactId = contactId,
            AccountId = accountId,
            GrantedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.AddPermissionAsync(contactId, accountId);

        // Assert
        Assert.True(result);
        var count = await context.PennylaneGedPermissionEntity
            .CountAsync(p => p.ContactId == contactId && p.AccountId == accountId);
        Assert.Equal(1, count); // Should still be only 1 permission
    }

    [Fact]
    public async Task AddPermissionAsync_AddsMultiplePermissions_ForDifferentAccounts()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);
        var contactId = 1;

        // Act
        await repo.AddPermissionAsync(contactId, 100);
        await repo.AddPermissionAsync(contactId, 200);
        await repo.AddPermissionAsync(contactId, 300);

        // Assert
        var permissions = await context.PennylaneGedPermissionEntity
            .Where(p => p.ContactId == contactId)
            .ToListAsync();
        Assert.Equal(3, permissions.Count);
    }

    #endregion

    #region RemovePermissionAsync Tests

    [Fact]
    public async Task RemovePermissionAsync_ReturnsTrue_WhenPermissionExists()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId = 1;
        var accountId = 100;

        context.PennylaneGedPermissionEntity.Add(new PennylaneGedPermissionEntity
        {
            ContactId = contactId,
            AccountId = accountId,
            GrantedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemovePermissionAsync(contactId, accountId);

        // Assert
        Assert.True(result);
        var permission = await context.PennylaneGedPermissionEntity
            .FirstOrDefaultAsync(p => p.ContactId == contactId && p.AccountId == accountId);
        Assert.Null(permission);
    }

    [Fact]
    public async Task RemovePermissionAsync_ReturnsFalse_WhenPermissionNotExists()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemovePermissionAsync(999, 999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemovePermissionAsync_OnlyRemovesSpecificPermission()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId = 1;

        context.PennylaneGedPermissionEntity.AddRange(
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 200, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 300, GrantedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemovePermissionAsync(contactId, 200);

        // Assert
        Assert.True(result);
        var remainingPermissions = await context.PennylaneGedPermissionEntity
            .Where(p => p.ContactId == contactId)
            .ToListAsync();
        Assert.Equal(2, remainingPermissions.Count);
        Assert.DoesNotContain(remainingPermissions, p => p.AccountId == 200);
    }

    #endregion

    #region RemoveAllPermissionsForContactAsync Tests

    [Fact]
    public async Task RemoveAllPermissionsForContactAsync_ReturnsTrue_WhenPermissionsExist()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId = 1;

        context.PennylaneGedPermissionEntity.AddRange(
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 200, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId, AccountId = 300, GrantedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemoveAllPermissionsForContactAsync(contactId);

        // Assert
        Assert.True(result);
        var remainingPermissions = await context.PennylaneGedPermissionEntity
            .Where(p => p.ContactId == contactId)
            .ToListAsync();
        Assert.Empty(remainingPermissions);
    }

    [Fact]
    public async Task RemoveAllPermissionsForContactAsync_ReturnsFalse_WhenNoPermissionsExist()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemoveAllPermissionsForContactAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveAllPermissionsForContactAsync_OnlyRemovesPermissionsForSpecificContact()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId1 = 1;
        var contactId2 = 2;

        context.PennylaneGedPermissionEntity.AddRange(
            new PennylaneGedPermissionEntity { ContactId = contactId1, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId1, AccountId = 200, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId2, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = contactId2, AccountId = 300, GrantedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.RemoveAllPermissionsForContactAsync(contactId1);

        // Assert
        Assert.True(result);
        var allPermissions = await context.PennylaneGedPermissionEntity.ToListAsync();
        Assert.Equal(2, allPermissions.Count);
        Assert.All(allPermissions, p => Assert.Equal(contactId2, p.ContactId));
    }

    #endregion

    #region GetPermissionsWithContactInfoAsync Tests

    [Fact]
    public async Task GetPermissionsWithContactInfoAsync_ReturnsEmptyList_WhenAccountIdsEmpty()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.GetPermissionsWithContactInfoAsync(Array.Empty<int>());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPermissionsWithContactInfoAsync_ReturnsEmptyList_WhenNoPermissionsExist()
    {
        // Arrange
        using var context = GetTestContext();
        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.GetPermissionsWithContactInfoAsync(new[] { 100, 200 });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPermissionsWithContactInfoAsync_ReturnsPermissionsWithContactInfo()
    {
        // Arrange
        using var context = GetTestContext();
        var contactId = 1;
        var accountId = 100;

        context.ContactEntity.Add(new ContactEntity
        {
            ContactId = contactId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Type = "Customer"
        });

        context.PennylaneGedPermissionEntity.Add(new PennylaneGedPermissionEntity
        {
            ContactId = contactId,
            AccountId = accountId,
            GrantedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.GetPermissionsWithContactInfoAsync(new[] { accountId });

        // Assert
        Assert.Single(result);
        var permission = result[0];
        Assert.Equal(contactId, permission.ContactId);
        Assert.Equal(accountId, permission.AccountId);
        Assert.Equal("test@example.com", permission.Email);
        Assert.Equal("John", permission.FirstName);
        Assert.Equal("Doe", permission.LastName);
        Assert.Equal("Customer", permission.ContactType);
    }

    [Fact]
    public async Task GetPermissionsWithContactInfoAsync_ReturnsMultiplePermissions_ForMultipleAccounts()
    {
        // Arrange
        using var context = GetTestContext();

        context.ContactEntity.AddRange(
            new ContactEntity { ContactId = 1, Email = "user1@example.com", FirstName = "User", LastName = "One", Type = "Customer" },
            new ContactEntity { ContactId = 2, Email = "user2@example.com", FirstName = "User", LastName = "Two", Type = "Customer" }
        );

        context.PennylaneGedPermissionEntity.AddRange(
            new PennylaneGedPermissionEntity { ContactId = 1, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = 2, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = 1, AccountId = 200, GrantedAt = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.GetPermissionsWithContactInfoAsync(new[] { 100, 200 });

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetPermissionsWithContactInfoAsync_OnlyReturnsPermissionsForRequestedAccounts()
    {
        // Arrange
        using var context = GetTestContext();

        context.ContactEntity.Add(
            new ContactEntity { ContactId = 1, Email = "user1@example.com", FirstName = "User", LastName = "One", Type = "Customer" }
        );

        context.PennylaneGedPermissionEntity.AddRange(
            new PennylaneGedPermissionEntity { ContactId = 1, AccountId = 100, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = 1, AccountId = 200, GrantedAt = DateTime.UtcNow },
            new PennylaneGedPermissionEntity { ContactId = 1, AccountId = 300, GrantedAt = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();

        var repo = new PennylaneGedPermissionRepository(context, _loggerMock.Object);

        // Act
        var result = await repo.GetPermissionsWithContactInfoAsync(new[] { 100, 300 });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.AccountId == 100);
        Assert.Contains(result, p => p.AccountId == 300);
        Assert.DoesNotContain(result, p => p.AccountId == 200);
    }

    #endregion
}
