// <copyright file="PennylaneGedAuthorizationServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Permissions;
using Pennylane.Infrastructure.Services;

namespace Pennylane.Infrastructure.Tests.Services;

public class PennylaneGedAuthorizationServiceTests
{
    private readonly Mock<IPennylaneGedPermissionRepository> _repositoryMock;
    private readonly Mock<ILogger<PennylaneGedAuthorizationService>> _loggerMock;
    private readonly PennylaneGedAuthorizationService _service;

    public PennylaneGedAuthorizationServiceTests()
    {
        _repositoryMock = new Mock<IPennylaneGedPermissionRepository>();
        _loggerMock = new Mock<ILogger<PennylaneGedAuthorizationService>>();
        _service = new PennylaneGedAuthorizationService(_repositoryMock.Object, _loggerMock.Object);
    }

    #region GetContactsWithGedPermissionAsync Tests

    [Fact]
    public async Task GetContactsWithGedPermissionAsync_ReturnsEmptyList_WhenNoPermissions()
    {
        // Arrange
        var accountId = 100;
        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(accountId))))
            .ReturnsAsync(new List<PennylaneGedPermissionWithContact>());

        // Act
        var result = await _service.GetContactsWithGedPermissionAsync(accountId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContactsWithGedPermissionAsync_ReturnsOnlyCustomerContacts()
    {
        // Arrange
        var accountId = 100;
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(1, accountId, "customer@example.com", "John", "Doe", "Customer"),
            new(2, accountId, "collaborator@example.com", "Jane", "Smith", "Collaborator"),
            new(3, accountId, "another@example.com", "Bob", "Wilson", "Customer")
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsWithGedPermissionAsync(accountId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal("Customer", c.Type));
    }

    [Fact]
    public async Task GetContactsWithGedPermissionAsync_MapsContactFieldsCorrectly()
    {
        // Arrange
        var accountId = 100;
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(42, accountId, "test@example.com", "FirstName", "LastName", "Customer")
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsWithGedPermissionAsync(accountId);

        // Assert
        Assert.Single(result);
        var contact = result[0];
        Assert.Equal(42, contact.ContactId);
        Assert.Equal("test@example.com", contact.Email);
        Assert.Equal("FirstName", contact.FirstName);
        Assert.Equal("LastName", contact.LastName);
        Assert.Equal("Customer", contact.Type);
    }

    [Fact]
    public async Task GetContactsWithGedPermissionAsync_IsCaseInsensitiveForContactType()
    {
        // Arrange
        var accountId = 100;
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(1, accountId, "lower@example.com", "A", "B", "customer"),
            new(2, accountId, "upper@example.com", "C", "D", "CUSTOMER"),
            new(3, accountId, "mixed@example.com", "E", "F", "CuStOmEr")
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsWithGedPermissionAsync(accountId);

        // Assert
        Assert.Equal(3, result.Count);
    }

    #endregion

    #region GetContactsForAccountsAsync Tests

    [Fact]
    public async Task GetContactsForAccountsAsync_ReturnsEmptyDictionary_WhenNoAccountIds()
    {
        // Arrange
        var accountIds = Enumerable.Empty<int>();

        // Act
        var result = await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        Assert.Empty(result);
        _repositoryMock.Verify(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
    }

    [Fact]
    public async Task GetContactsForAccountsAsync_ReturnsEmptyDictionary_WhenNoPermissionsFound()
    {
        // Arrange
        var accountIds = new[] { 100, 200 };
        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<PennylaneGedPermissionWithContact>());

        // Act
        var result = await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContactsForAccountsAsync_GroupsContactsByAccountId()
    {
        // Arrange
        var accountIds = new[] { 100, 200 };
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(1, 100, "user1@example.com", "User", "One", "Customer"),
            new(2, 100, "user2@example.com", "User", "Two", "Customer"),
            new(3, 200, "user3@example.com", "User", "Three", "Customer")
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(100));
        Assert.True(result.ContainsKey(200));
        Assert.Equal(2, result[100].Count);
        Assert.Single(result[200]);
    }

    [Fact]
    public async Task GetContactsForAccountsAsync_FiltersOutNonCustomerContacts()
    {
        // Arrange
        var accountIds = new[] { 100 };
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(1, 100, "customer@example.com", "A", "B", "Customer"),
            new(2, 100, "collab@example.com", "C", "D", "Collaborator"),
            new(3, 100, "admin@example.com", "E", "F", "Admin")
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        Assert.Single(result);
        Assert.Single(result[100]);
        Assert.Equal("customer@example.com", result[100][0].Email);
    }

    [Fact]
    public async Task GetContactsForAccountsAsync_DoesNotIncludeAccountsWithNoCustomerContacts()
    {
        // Arrange
        var accountIds = new[] { 100, 200 };
        var permissions = new List<PennylaneGedPermissionWithContact>
        {
            new(1, 100, "customer@example.com", "A", "B", "Customer"),
            new(2, 200, "collab@example.com", "C", "D", "Collaborator") // Not a Customer
        };

        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(100));
        Assert.False(result.ContainsKey(200));
    }

    [Fact]
    public async Task GetContactsForAccountsAsync_CallsRepositoryWithCorrectAccountIds()
    {
        // Arrange
        var accountIds = new[] { 100, 200, 300 };
        _repositoryMock
            .Setup(r => r.GetPermissionsWithContactInfoAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<PennylaneGedPermissionWithContact>());

        // Act
        await _service.GetContactsForAccountsAsync(accountIds);

        // Assert
        _repositoryMock.Verify(r => r.GetPermissionsWithContactInfoAsync(
            It.Is<IEnumerable<int>>(ids => ids.Count() == 3 && ids.Contains(100) && ids.Contains(200) && ids.Contains(300))),
            Times.Once);
    }

    #endregion
}
