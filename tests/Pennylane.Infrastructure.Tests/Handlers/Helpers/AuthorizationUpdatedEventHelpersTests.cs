// <copyright file="AuthorizationUpdatedEventHelpersTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Models.Contacts;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models.Pagers;
using Pennylane.Common.Tests;
using Pennylane.Domain.Entities;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.Infrastructure.Handlers;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Tests.Handlers.Helpers;

public class AuthorizationUpdatedEventHelpersTests : BaseTests
{
    private readonly Mock<ILogger<AuthorizationUpdatedEventHandler>> _loggerMock;
    private readonly Mock<IPennylaneApiGroupService> _serviceMock;
    private readonly Mock<IAccountRepository> _accountRepoMock;
    private readonly Mock<IContactRepository> _contactRepoMock;
    private readonly IAuthorizationUpdatedEventManager _managerMock;
    private readonly Mock<ILogger<AuthorizationUpdatedEventManager>> _managerloggerMock;
    private readonly IOptions<PennylaneServiceOptions> _options;

    public AuthorizationUpdatedEventHelpersTests()
    {
        _loggerMock = Mock.Initialize<ILogger<AuthorizationUpdatedEventHandler>>();
        _managerloggerMock = Mock.Initialize<ILogger<AuthorizationUpdatedEventManager>>();
        _serviceMock = Mock.Initialize<IPennylaneApiGroupService>();
        _accountRepoMock = Mock.Initialize<IAccountRepository>();
        _contactRepoMock = Mock.Initialize<IContactRepository>();

        _options = OptionsFactory.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "test",
            ApiUrl = "http://test.com",
            ApiFirmUrl = "http://firm-test.com",
            SupportedSaasPlans = "plan",
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
        });

        _managerMock = new AuthorizationUpdatedEventManager(
            _managerloggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options
        );
    }

    [Fact]
    public async Task AssignUserAccessAsync_WithPositiveAccessId_DoesNotThrow()
    {
        // Arrange
        var accountNumber = Fixture.Create<string>();
        var contact = Fixture.Create<Contact>();
        var pennylaneUserId = Fixture.Create<string>();
        var role = Fixture.Create<string>();

        _serviceMock
            .Setup(s => s.GrantCompanyUserAccessAsync(accountNumber, It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(5);

        // Act & Assert
        await _managerMock.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId);
    }

    [Fact]
    public async Task AssignUserAccessAsync_WithNonPositiveAccessId_ThrowsBadRequestException()
    {
        // Arrange
        var accountNumber = Fixture.Create<string>();
        var contact = Fixture.Create<Contact>();
        var pennylaneUserId = Fixture.Create<string>();
        var role = Fixture.Create<string>();

        _serviceMock
            .Setup(s => s.TryGrantCompanyUserAccessAsync(accountNumber, It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(0);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId)
        );
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_WithValidResponse_ReturnsUserId()
    {
        // Arrange
        var contact = Fixture.Create<Contact>();
        var authMethod = Fixture.Create<string>();
        var expectedUserId = Fixture.Create<string>();

        _serviceMock
            .Setup(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync(expectedUserId);

        // Act
        var result = await _managerMock.CreatePennylaneUserAsync(contact, authMethod);

        // Assert
        Assert.Equal(expectedUserId, result);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_WithEmptyResponse_ThrowsBadRequestException()
    {
        // Arrange
        var contact = Fixture.Create<Contact>();
        var authMethod = Fixture.Create<string>();

        _serviceMock
            .Setup(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.CreatePennylaneUserAsync(contact, authMethod)
        );
    }

    [Fact]
    public async Task CheckIfUserExistsAsync_UserOnFirstPage_ReturnsTrueAndUserId()
    {
        // Arrange
        var email = Fixture.Create<string>();
        var pennylaneUser = new PennylaneUsers { Email = email, Id = "found-id" };

        _serviceMock
            .Setup(s => s.TryGetUserAsync(It.IsAny<string>()))
            .ReturnsAsync(pennylaneUser);

        // Act
        var (hasExisting, userId) = await _managerMock.CheckIfUserExistsAsync(email);

        // Assert
        Assert.True(hasExisting);
        Assert.Equal("found-id", userId);
    }

    [Fact]
    public async Task CheckIfUserExistsAsync_NoUsers_ThenReturnEmptyValues()
    {
        // Arrange
        var email = Fixture.Create<string>();
        var emptyPager = new PennylaneUsersPager
        {
            Users = null!,
            TotalPages = 1,
            CurrentPage = 1
        };

        // Act
        var (hasExisting, userId) = await _managerMock.CheckIfUserExistsAsync(email);

        // Assert
        Assert.False(hasExisting);
        Assert.Null(userId);
    }

    [Fact]
    public async Task CheckIfUserExistsAsync_NoMatchOnAllPages_ReturnsFalseAndEmptyId()
    {
        // Arrange
        var email = Fixture.Create<string>();

        var pennylaneUser = new PennylaneUsers { Email = email, Id = Fixture.Create<string>() };

        _serviceMock.Setup(s => s.TryGetUserAsync(It.IsAny<string>())).ReturnsAsync(default(PennylaneUsers));

        // Act
        var (hasExisting, userId) = await _managerMock.CheckIfUserExistsAsync(email);

        // Assert
        Assert.False(hasExisting);
        Assert.Null(userId);
    }

    [Fact]
    public async Task CheckIfUserExistsAsync_WithAccountNumber_CallsGetCompanyUsersAsync()
    {
        // Arrange
        var email = Fixture.Create<string>();
        var accountNumber = Fixture.Create<string>();
        var pennylaneUser = new PennylaneUsers { Email = email, Id = "acct-id" };

        _serviceMock.Setup(s => s.TryGetCompanyUserAsync(email, accountNumber)).ReturnsAsync(pennylaneUser);

        // Act
        var (hasExisting, userId) = await _managerMock.CheckIfUserExistsAsync(email, accountNumber);

        // Assert
        Assert.True(hasExisting);
        Assert.Equal("acct-id", userId);
    }

    [Fact]
    public async Task GetAccountNumberByAccountIdAsync_WithValidAccount_ReturnsAccountNumber()
    {
        // Arrange
        var accountEntity = new AccountEntity
        {
            AccountId = Fixture.Create<int>(),
            AccountNumber = Fixture.Create<string>()
        };
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(accountEntity);

        // Act
        var result = await _managerMock.GetAccountNumberByAccountIdAsync(Fixture.Create<int>());

        // Assert
        Assert.Equal(accountEntity.AccountNumber, result);
    }

    [Fact]
    public async Task GetAccountNumberByAccountIdAsync_AccountNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync((AccountEntity)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _managerMock.GetAccountNumberByAccountIdAsync(Fixture.Create<int>())
        );
    }

    [Fact]
    public async Task GetAccountNumberByAccountIdAsync_AccountNumberEmpty_ThrowsBadRequestException()
    {
        // Arrange
        var accountEntity = new AccountEntity { AccountId = Fixture.Create<int>(), AccountNumber = "   " };
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(accountEntity);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.GetAccountNumberByAccountIdAsync(Fixture.Create<int>())
        );
    }

    [Fact]
    public async Task GetContactIdByEmailAsync_WithValidContact_ReturnsContact()
    {
        // Arrange
        var contactEntity = Fixture.Create<Contact>();
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, It.IsAny<int?>()))
            .ReturnsAsync(contactEntity);

        // Act
        var result = await _managerMock.GetContactByIdAsync(Fixture.Create<int>());

        // Assert
        Assert.Equal(contactEntity, result);
    }

    [Fact]
    public async Task GetContactIdByEmailAsync_NullOrInvalidId_ThrowsBadRequestException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.GetContactByIdAsync(null)
        );
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.GetContactByIdAsync(0)
        );
    }

    [Fact]
    public async Task GetContactIdByEmailAsync_ContactNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, It.IsAny<int?>()))
            .ReturnsAsync((Contact)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _managerMock.GetContactByIdAsync(Fixture.Create<int>())
        );
    }

    [Fact]
    public async Task RevokeAccessUserCompanyAsync_WithValidIds_CompletesWithoutException()
    {
        // Arrange
        var companyId = Fixture.Create<string>();
        var userId = Fixture.Create<string>();

        // Act & Assert
        await _managerMock.RevokeAccessUserCompanyAsync(companyId, userId);

        // Verify service was called once
        _serviceMock.Verify(s => s.RevokeAccessUserCompanyAsync(companyId, userId), Times.Once);
    }

    [Theory]
    [InlineData(null, "userId")]
    [InlineData("", "userId")]
    [InlineData("companyId", null)]
    [InlineData("companyId", "")]
    public async Task RevokeAccessUserCompanyAsync_InvalidParameters_ThrowsBadRequestException(string companyId, string userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _managerMock.RevokeAccessUserCompanyAsync(companyId, userId)
        );
    }

    [Fact]
    public async Task AssignUserAccessAsync_UseUserIdForGrantAccess_True_WithNullUserId_ThrowsArgumentException()
    {
        // Arrange
        var optionsWithFlag = OptionsFactory.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "test",
            ApiUrl = "http://test.com",
            ApiFirmUrl = "http://firm-test.com",
            SupportedSaasPlans = "plan",
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            UseUserIdForGrantAccess = true
        });

        var manager = new AuthorizationUpdatedEventManager(
            _managerloggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            optionsWithFlag
        );

        var accountNumber = Fixture.Create<string>();
        var contact = Fixture.Create<Contact>();
        var role = Fixture.Create<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId: null)
        );
        Assert.Contains("pennylaneUserId", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignUserAccessAsync_UseUserIdForGrantAccess_True_WithEmptyUserId_ThrowsArgumentException(string emptyUserId)
    {
        // Arrange
        var optionsWithFlag = OptionsFactory.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "test",
            ApiUrl = "http://test.com",
            ApiFirmUrl = "http://firm-test.com",
            SupportedSaasPlans = "plan",
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            UseUserIdForGrantAccess = true
        });

        var manager = new AuthorizationUpdatedEventManager(
            _managerloggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            optionsWithFlag
        );

        var accountNumber = Fixture.Create<string>();
        var contact = Fixture.Create<Contact>();
        var role = Fixture.Create<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId: emptyUserId)
        );
        Assert.Contains("pennylaneUserId", exception.Message);
    }

    [Fact]
    public async Task AssignUserAccessAsync_UseUserIdForGrantAccess_False_WithNullEmail_ThrowsArgumentException()
    {
        // Arrange - default options have UseUserIdForGrantAccess = false
        var accountNumber = Fixture.Create<string>();
        var contact = new Contact
        {
            ContactId = Fixture.Create<int>(),
            FirstName = Fixture.Create<string>(),
            LastName = Fixture.Create<string>(),
            Email = null!
        };
        var role = Fixture.Create<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _managerMock.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId: "some-id")
        );
        Assert.Contains("contact", exception.Message.ToLower());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignUserAccessAsync_UseUserIdForGrantAccess_False_WithEmptyEmail_ThrowsArgumentException(string emptyEmail)
    {
        // Arrange - default options have UseUserIdForGrantAccess = false
        var accountNumber = Fixture.Create<string>();
        var contact = new Contact
        {
            ContactId = Fixture.Create<int>(),
            FirstName = Fixture.Create<string>(),
            LastName = Fixture.Create<string>(),
            Email = emptyEmail
        };
        var role = Fixture.Create<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _managerMock.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId: "some-id")
        );
        Assert.Contains("contact", exception.Message.ToLower());
    }
}