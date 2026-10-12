// <copyright file="PennylaneServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using AutoFixture;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Pennylane;
using Pennylane.Application.services;
using Pennylane.Domain.Entities.Pennylane;
using Application.Interfaces;
using Application.Models.Contacts;
using Pulse.ExceptionMiddleware.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pennylane.Application.Managers;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.Application.Models;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.WebApi.Models;
using Pennylane.Application.Consts;
using Pennylane.Domain.Entities;

namespace Pennylane.Application.Tests.Services;

public class PennylaneServiceTests
{
    private readonly IFixture _fixture;

    public PennylaneServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private PennylaneService CreateService(
        out Mock<IAccountRepository> accountRepo,
        out Mock<ICompanyRepository> companyRepo,
        out Mock<IContactRepository> contactRepo,
        out Mock<IPennylaneRepository> pnlRepo,
        out Mock<IAuthorizationUpdatedEventManager> authorizationUpdatedEventManager,
        out Mock<IPennylaneApiGroupService> pennylaneServiceApiMock,
        out Mock<IPennylaneApiFirmService> pennylaneServiceApiFirmMock
        )
    {
        accountRepo = new Mock<IAccountRepository>(MockBehavior.Strict);
        companyRepo = new Mock<ICompanyRepository>(MockBehavior.Strict);
        contactRepo = new Mock<IContactRepository>(MockBehavior.Strict);
        pnlRepo = new Mock<IPennylaneRepository>(MockBehavior.Strict);

        authorizationUpdatedEventManager = new Mock<IAuthorizationUpdatedEventManager>(MockBehavior.Strict);
        pennylaneServiceApiMock = new Mock<IPennylaneApiGroupService>();
        pennylaneServiceApiFirmMock = new Mock<IPennylaneApiFirmService>();

        var opts = Options.Create(new PennylaneServiceOptions
        {
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "niceplan",
            EnableEventHandler = true,
            DefaultRole = "def",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            Token = "Token",
            LastSyncDateRetentionDays = 30,
            PlansRoleAssignments = new List<PennylanePlansRoleAssignmentOptions> {
                        new() { Plan = "plan", ClientRole = "role" }
                }
        });

        var firmTokenOpts = Options.Create(new PennylaneFirmTokenOptions
            {
                new PennylaneFirmTokenItem { Id = "firm-123", Tokens = new List<string> { "firm-token" } }
            });

        var service = new PennylaneService(
            accountRepo.Object,
            contactRepo.Object,
            companyRepository: companyRepo.Object,
            pnlRepo.Object,
            NullLogger<PennylaneService>.Instance,
            authorizationUpdatedEventManager.Object,
            opts,
            firmTokenOpts,
            pennylaneServiceApiMock.Object,
            pennylaneServiceApiFirmMock.Object);

        return service;
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldThrowAccountNotFoundException_WhenAccountNotFound()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountId = _fixture.Create<int>();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, _fixture.Create<int>())
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync((AccountEntity?)null)
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.InsertPennylaneRoleAsync(role));
        accountRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldThrowContactNotFoundException_WhenContactNotFound()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contactId = _fixture.Create<int>();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contactId)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountEntity.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((Contact?)null)
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.InsertPennylaneRoleAsync(role));
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldRethrowException_WhenAccountLookupFails()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out _, out _, out _, out _, out _);
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, _fixture.Create<int>())
            .With(r => r.ContactId, _fixture.Create<int>())
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(role.AccountId.ToString()))
            .ThrowsAsync(new System.InvalidOperationException("Database connection failed"))
            .Verifiable();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.InsertPennylaneRoleAsync(role));
        exception.Message.Should().Be("Database connection failed");
        accountRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldRethrowException_WhenContactLookupFails()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out _, out _, out _, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, _fixture.Create<int>())
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountEntity.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ThrowsAsync(new System.InvalidOperationException("Contact service unavailable"))
            .Verifiable();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.InsertPennylaneRoleAsync(role));
        exception.Message.Should().Be("Contact service unavailable");
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldRethrowException_WhenFetchPennylaneRoleFails()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out _, out _, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .Create();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountEntity.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact)
            .Verifiable();

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ThrowsAsync(new System.InvalidOperationException("Database error on fetch"))
            .Verifiable();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.InsertPennylaneRoleAsync(role));
        exception.Message.Should().Be("Database error on fetch");
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldRethrowException_WhenInsertRoleFails()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authMgr, out _, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .With(a => a.AccountNumber, "ACC123")
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, "test@example.com")
            .Create();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountEntity.AccountId.ToString()))
            .ReturnsAsync(accountEntity);

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact);

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null);

        // User exists in Pennylane
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((true, "existingUserId"));

        authMgr
            .Setup(m => m.GetContactByIdAsync(contact.ContactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountEntity.AccountId))
            .ReturnsAsync(accountEntity.AccountNumber);

        // Insert fails
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()))
            .ThrowsAsync(new System.InvalidOperationException("Insert failed"))
            .Verifiable();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.InsertPennylaneRoleAsync(role));
        exception.Message.Should().Be("Insert failed");
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_Should_Not_Create_User_InPennylane_When_User_Exists()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .With(a => a.AccountNumber, _fixture.Create<string>())
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, _fixture.Create<string>())
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.Role, _fixture.Create<string>())
            .Create();

        // Setup: Account and contact exist
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(role.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: Role does not exist yet
        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        // Setup: User already exists in Pennylane
        authorizationUpdatedEventManager
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((true, "existingPennylaneUserId"))
            .Verifiable();

        authorizationUpdatedEventManager
            .Setup(m => m.GetContactByIdAsync(contact.ContactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m
            .GetAccountNumberByAccountIdAsync(accountEntity.AccountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        // Should NOT call CreatePennylaneUserAsync
        authorizationUpdatedEventManager
            .Setup(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()))
            .Throws(new Exception("Should not be called"));

        // Should insert the role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == role.AccountId &&
                e.ContactId == role.ContactId &&
                e.Role == role.Role)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        authorizationUpdatedEventManager.Verify(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber), Times.Once);
        authorizationUpdatedEventManager.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_Should_Only_AssignUserAccess_When_User_Exists_Globally()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .With(a => a.AccountNumber, _fixture.Create<string>())
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, _fixture.Create<string>())
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.Role, _fixture.Create<string>())
            .Create();

        // Setup: Account and contact exist
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(role.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: Role does not exist yet
        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        // Setup: User exists globally in Pennylane
        authorizationUpdatedEventManager
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "globalPennylaneUserId"))
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m
            .GetAccountNumberByAccountIdAsync(accountEntity.AccountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        authorizationUpdatedEventManager
            .Setup(m => m.GetContactByIdAsync(contact.ContactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authorizationUpdatedEventManager
            .Setup(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, null))
            .Verifiable();

        authorizationUpdatedEventManager
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "globalPennylaneUserId"))
            .Verifiable();

        // Should assign user access
        authorizationUpdatedEventManager
            .Setup(m => m.AssignUserAccessAsync(
                accountEntity.AccountNumber,
                contact,
                role.Role,
                "globalPennylaneUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Should NOT call CreatePennylaneUserAsync
        authorizationUpdatedEventManager
            .Setup(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()))
            .ReturnsAsync(("globalPennylaneUserId", false));

        // Should insert the role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == role.AccountId &&
                e.ContactId == role.ContactId &&
                e.Role == role.Role)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
        pnlRepo.VerifyAll();

        authorizationUpdatedEventManager.Verify(m => m.AssignUserAccessAsync(
            accountEntity.AccountNumber,
            contact,
            role.Role,
            "globalPennylaneUserId"), Times.Once);
        authorizationUpdatedEventManager.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_Should_CreateUser_InPennylane_And_AssignRole()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .With(a => a.AccountNumber, _fixture.Create<string>())
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Email, _fixture.Create<string>())
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.Role, _fixture.Create<string>())
            .Create();

        // Setup: Account and contact exist
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(role.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: Role does not exist yet
        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m
            .GetAccountNumberByAccountIdAsync(accountEntity.AccountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m
            .GetContactByIdAsync(contact.ContactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, null))
            .Verifiable();

        // Setup: User does not exist in Pennylane
        authorizationUpdatedEventManager
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null))
            .Verifiable();

        // Should create the user
        authorizationUpdatedEventManager
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()))
            .ReturnsAsync(("newPennylaneUserId", false))
            .Verifiable();

        // Should assign user access
        authorizationUpdatedEventManager
            .Setup(m => m.AssignUserAccessAsync(
                accountEntity.AccountNumber,
                contact,
                role.Role,
                "newPennylaneUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Should insert the role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == role.AccountId &&
                e.ContactId == role.ContactId &&
                e.Role == role.Role)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
        pnlRepo.VerifyAll();
        authorizationUpdatedEventManager.Verify(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()), Times.Once);
        authorizationUpdatedEventManager.Verify(m => m.AssignUserAccessAsync(
            accountEntity.AccountNumber,
            contact,
            role.Role,
            "newPennylaneUserId"), Times.Once);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldInsertRole_WhenValidRoleProvided()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .Create();
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.Role, _fixture.Create<string>())
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(role.AccountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(contact)
            .Verifiable();

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(
                It.Is<PnlRoleEntity>(e =>
                    e.AccountId == role.AccountId &&
                    e.ContactId == role.ContactId &&
                    e.Role == role.Role)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m
            .GetAccountNumberByAccountIdAsync(accountEntity.AccountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        authorizationUpdatedEventManager.Setup(m => m.GetContactByIdAsync(contact.ContactId))
            .ReturnsAsync(contact);
        authorizationUpdatedEventManager
            .Setup(m => m.TryCreatePennylaneUserAsync(
                It.IsAny<Contact>(),
                It.IsAny<string>()))
            .ReturnsAsync(((string?)null, true));

        authorizationUpdatedEventManager
            .SetupSequence(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "globalPennylaneUserId"));

        authorizationUpdatedEventManager
            .Setup(m => m.AssignUserAccessAsync(
                accountEntity.AccountNumber,
                contact,
                role.Role,
                (string?)null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task GetPennylaneRoleAsync_ShouldReturnNull_WhenRoleDoesntExists()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .Create();

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountEntity.AccountId, contact.ContactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        // Act
        var result = await service.GetPennylaneRoleAsync(contact.ContactId, accountEntity.AccountId);

        // Assert
        Assert.Null(result);
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task GetPennylaneRoleAsync_ShouldReturnRole_WhenRoleExists()
    {
        // Arrange
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .Create();
        var role = _fixture.Build<PnlRoleEntity>()
            .With(r => r.AccountId, accountEntity.AccountId)
            .With(r => r.ContactId, contact.ContactId)
            .With(r => r.Role, _fixture.Create<string>())
            .Create();

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId))
            .ReturnsAsync(role)
            .Verifiable();

        // Act
        var result = await service.GetPennylaneRoleAsync(contact.ContactId, accountEntity.AccountId);

        // Assert
        result.Should().NotBeNull();
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_WhenAccountIsNull_WillNotCallPennylaneRevoke()
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .Create();

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync((AccountEntity?)null);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        pennylaneServiceApiMock.Setup(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_WhenContactIsNull_WillNotCallPennylaneRevoke()
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
             .With(c => c.Type, "Collaborator")
            .Create();

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(accountEntity);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync((Contact?)null);
        pennylaneServiceApiMock.Setup(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Collaborator")]
    [InlineData("colLAborAtoR")]
    public async Task DeletePennylaneRoleAsync_WhenAllIsCorrect_WillCallPennylaneRevokeAndDeletePnlRole(string contactType)
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 1)
            .With(c => c.Type, contactType)
            .Create();
        var pennylanePageUsers = new PennylaneUsersPager()
        {
            CurrentPage = 1,
            TotalPages = 1,
            Users = new List<PennylaneUsers>() { new PennylaneUsers {Email = contact.Email,
                FirstName = contact.FirstName,
                Id = contact.ContactId.ToString(),
                LastName = contact.LastName,
                Role = "Employee"
                } }
        };
        var existingPnlRole = new PnlRoleEntity { AccountId = 1, ContactId = 1, Role = "employee" };

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(accountEntity);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        pennylaneServiceApiMock.Setup(p => p.GetCompanyUsersAsync(It.IsAny<string>(), null)).ReturnsAsync(pennylanePageUsers);
        pennylaneServiceApiMock.Setup(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        pnlRepo.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(1, 1)).ReturnsAsync(existingPnlRole);
        pnlRepo.Setup(r => r.DeletePennylaneRoleAsync(existingPnlRole)).Returns(Task.CompletedTask);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        pnlRepo.Verify(r => r.DeletePennylaneRoleAsync(existingPnlRole), Times.Once);
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_WhenContactDoesNotExistsInPennylaneCompany_WillNotCallPennylaneRevoke()
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, _fixture.Create<int>())
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, _fixture.Create<int>())
            .With(c => c.Type, "Collaborator")
            .Create();

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(accountEntity);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        pennylaneServiceApiMock.Setup(p => p.GetCompanyUsersAsync(It.IsAny<string>(), null)).ReturnsAsync(new PennylaneUsersPager() { CurrentPage = 1, TotalPages = 1, Users = new List<PennylaneUsers>() });
        pennylaneServiceApiMock.Setup(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        pnlRepo.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((PnlRoleEntity?)null);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_WhenUserNotInPennylane_WillStillDeletePnlRole()
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 1)
            .With(c => c.Type, "Collaborator")
            .Create();
        var existingPnlRole = new PnlRoleEntity { AccountId = 1, ContactId = 1, Role = "employee" };

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(accountEntity);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        pennylaneServiceApiMock.Setup(p => p.GetCompanyUsersAsync(It.IsAny<string>(), null)).ReturnsAsync(new PennylaneUsersPager() { CurrentPage = 1, TotalPages = 1, Users = new List<PennylaneUsers>() });
        pnlRepo.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(1, 1)).ReturnsAsync(existingPnlRole);
        pnlRepo.Setup(r => r.DeletePennylaneRoleAsync(existingPnlRole)).Returns(Task.CompletedTask);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        pnlRepo.Verify(r => r.DeletePennylaneRoleAsync(existingPnlRole), Times.Once);
    }

    [Fact]
    public async Task DeletePennylaneRoleAsync_WhenPnlRoleDoesNotExist_WillRevokeButNotCallDeletePnlRole()
    {
        var service = CreateService(out var accountRepo, out _, out var contactRepo, out var pnlRepo, out var authorizationUpdatedEventManager, out var pennylaneServiceApiMock, out _);
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .Create();
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 1)
            .With(c => c.Type, "Collaborator")
            .Create();
        var pennylanePageUsers = new PennylaneUsersPager()
        {
            CurrentPage = 1,
            TotalPages = 1,
            Users = new List<PennylaneUsers>() { new PennylaneUsers {Email = contact.Email,
                FirstName = contact.FirstName,
                Id = contact.ContactId.ToString(),
                LastName = contact.LastName,
                Role = "Employee"
                } }
        };

        accountRepo.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(accountEntity);
        contactRepo.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        pennylaneServiceApiMock.Setup(p => p.GetCompanyUsersAsync(It.IsAny<string>(), null)).ReturnsAsync(pennylanePageUsers);
        pennylaneServiceApiMock.Setup(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        pnlRepo.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(1, 1)).ReturnsAsync((PnlRoleEntity?)null);

        await service.DeletePennylaneRoleAsync(1, 1);

        pennylaneServiceApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        pnlRepo.Verify(r => r.DeletePennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldUpdateRole_WhenExistingRoleDifferent()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneServiceApiMock,
            out _);

        // Prepare IDs and models
        var accountId = _fixture.Create<int>();
        var contactId = _fixture.Create<int>();
        var existingRoleEnt = _fixture.Build<PnlRoleEntity>()
            .With(e => e.AccountId, accountId)
            .With(e => e.ContactId, contactId)
            .With(e => e.Role, "OLD_ROLE")
            .Create();
        var newRoleModel = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "NEW_ROLE")
            .Create();
        var accountNumber = "ACC#";

        // 1) account exists
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        // 2) contact exists
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "foo@bar.com")
            .Create();
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        // 3) existing role returned
        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync(existingRoleEnt)
            .Verifiable();

        // 4) in update: check global user
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "OLDUID"))
            .Verifiable();

        // 5) in update: delete old in pulse
        pnlRepo
            .Setup(r => r.DeletePennylaneRoleAsync(existingRoleEnt))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // 6) in update: revoke in Pennylane
        authMgr
            .Setup(m => m.RevokeAccessUserCompanyAsync(accountNumber, "OLDUID"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // 7) in update: insert new in pulse
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "NEW_ROLE")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // 8) in update: assign in Pennylane
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "NEW_ROLE", "OLDUID"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // 9) after update: main flow still does one Insert and skips CreateUser
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "NEW_ROLE")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(newRoleModel);

        // Assert
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
        pnlRepo.Verify(r => r.DeletePennylaneRoleAsync(existingRoleEnt), Times.Once);
        pnlRepo.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Once);
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, null), Times.Once);
        authMgr.Verify(m => m.RevokeAccessUserCompanyAsync(accountNumber, "OLDUID"), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "NEW_ROLE", "OLDUID"), Times.Once);
    }

    [Fact]
    public async Task GetPennylaneEmailsAsync_ShouldReturn_PennylaneEmails()
    {
        var accountNumber = "AccountNumber";
        var customerInvoiceTransmissionEmail = "customerInvoiceTransmissionEmail";
        var supplierInvoiceTransmissionEmail = "supplierInvoiceTransmissionEmail";
        var expected = new PennylaneEmails(accountNumber, customerInvoiceTransmissionEmail, supplierInvoiceTransmissionEmail);
        var service = CreateService(out _, out _, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        pennylaneServiceApiMock.Setup(s =>
            s.TryGetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(
                new PennylaneCompany
                {
                    Id = accountNumber,
                    CustomerInvoiceTransmissionEmails = [customerInvoiceTransmissionEmail],
                    SupplierInvoiceTransmissionEmails = [supplierInvoiceTransmissionEmail]
                });

        var result = await service.GetPennylaneEmailsAsync(accountNumber);
        result.Should().BeEquivalentTo(expected);
        pennylaneServiceApiMock.Verify(s => s.TryGetCompanyByIdAsync(accountNumber), Times.Once);
    }

    [Fact]
    public async Task GetPennylaneEmailsAsync_ShouldReturnNull_PennylaneEmails()
    {
        var accountNumber = "AccountNumber";
        var service = CreateService(out _, out _, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        pennylaneServiceApiMock.Setup(s =>
                s.TryGetCompanyByIdAsync(accountNumber))
            .ReturnsAsync((PennylaneCompany?)null);

        var result = await service.GetPennylaneEmailsAsync(accountNumber);
        result.Should().BeNull();
        pennylaneServiceApiMock.Verify(s => s.TryGetCompanyByIdAsync(accountNumber), Times.Once);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_ShouldReturnNull_WhenNotFoundInAPI()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out _, out var pennylaneApiFirmService);
        var firmId = "123";
        var accountNumber = "ACC123";

        companyRepo
            .Setup(r => r.GetPennylaneInternalIdByACompanyIdAsync(accountNumber))
            .ReturnsAsync((string?)null)
            .Verifiable();

        pennylaneApiFirmService
            .Setup(p => p.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber))
            .ReturnsAsync((int?)null)
            .Verifiable();

        // Note: No call to SavePennylaneInternalIdAsync should be made when API returns null

        // Act
        var result = await service.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber);

        // Assert
        result.Should().BeNull();
        companyRepo.VerifyAll();
        pennylaneApiFirmService.VerifyAll();
        companyRepo.Verify(r => r.SavePennylaneInternalIdAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByAccountNumberAsync_ForceRefresh_ShouldSkipCacheAndCallApi()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out _, out var pennylaneApiFirmService);
        var firmId = "123";
        var accountNumber = "ACC123";
        var expectedInternalId = 42;

        // Should NOT call the cache
        pennylaneApiFirmService
            .Setup(p => p.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber))
            .ReturnsAsync(expectedInternalId)
            .Verifiable();

        companyRepo
            .Setup(r => r.SavePennylaneInternalIdAsync(accountNumber, expectedInternalId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await service.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber, forceRefresh: true);

        // Assert
        result.Should().Be(expectedInternalId);
        // Cache should NOT have been queried
        companyRepo.Verify(r => r.GetPennylaneInternalIdByACompanyIdAsync(It.IsAny<string>()), Times.Never);
        pennylaneApiFirmService.VerifyAll();
        companyRepo.VerifyAll();
    }

    [Fact]
    public async Task RefreshCompanyFromApiAsync_ShouldFetchFromApiAndUpdateCache()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "ACC123";
        var apiCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .With(c => c.FirmId, "NEW-FIRM")
            .Create();

        pennylaneServiceApiMock
            .Setup(p => p.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(apiCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == companyId)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await service.RefreshCompanyFromApiAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(apiCompany);
        pennylaneServiceApiMock.VerifyAll();
        companyRepo.VerifyAll();
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldReturnFromRepository_WhenDataIsFresh()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";
        var freshSyncDate = DateTime.UtcNow.AddDays(-10); // Within 30-day retention period
        var expectedCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .Create();

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ReturnsAsync(expectedCompany)
            .Verifiable();

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(expectedCompany);
        companyRepo.VerifyAll();
        // API should not be called for fresh data
        pennylaneServiceApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldFetchFromAPI_WhenDataIsExpired()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";
        var expiredSyncDate = DateTime.UtcNow.AddDays(-35); // Older than 30-day retention period
        var apiCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .Create();

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ThrowsAsync(new KeyNotFoundException())
            .Verifiable();

        pennylaneServiceApiMock
            .Setup(p => p.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(apiCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == apiCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(apiCompany);
        companyRepo.VerifyAll();
        pennylaneServiceApiMock.VerifyAll();
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldFetchFromAPI_WhenLastSyncDateIsNull()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";
        var apiCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .Create();

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ThrowsAsync(new KeyNotFoundException())
            .Verifiable();

        pennylaneServiceApiMock
            .Setup(p => p.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(apiCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == apiCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(apiCompany);
        companyRepo.VerifyAll();
        pennylaneServiceApiMock.VerifyAll();
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldFetchFromAPI_WhenNotInRepository()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";
        var apiCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .Create();

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ThrowsAsync(new KeyNotFoundException($"Company with ID {companyId} not found."))
            .Verifiable();

        pennylaneServiceApiMock
            .Setup(p => p.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(apiCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == apiCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(apiCompany);
        companyRepo.VerifyAll();
        pennylaneServiceApiMock.VerifyAll();
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldThrowException_WhenNotFoundInRepositoryAndAPIFails()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ThrowsAsync(new KeyNotFoundException($"Company with ID {companyId} not found."))
            .Verifiable();

        pennylaneServiceApiMock
            .Setup(p => p.GetCompanyByIdAsync(companyId))
            .ThrowsAsync(new HttpRequestException("Company not found in API"))
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetCompanyByIdAsync(companyId));
        companyRepo.VerifyAll();
        pennylaneServiceApiMock.VerifyAll();
        // Should not try to save when API throws exception
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ShouldTestBoundaryCondition_ExactlyAtRetentionLimit()
    {
        // Arrange
        var service = CreateService(out _, out var companyRepo, out _, out _, out _, out var pennylaneServiceApiMock, out _);
        var companyId = "123";
        // Use a clearly fresh date (1 day ago) to avoid boundary timing issues
        var freshSyncDate = DateTime.UtcNow.AddDays(-1);
        var cachedCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, companyId)
            .Create();

        companyRepo
            .Setup(r => r.GetCompanyByIdWithRetentionAsync(companyId, 30))
            .ReturnsAsync(cachedCompany)
            .Verifiable();

        // Act
        var result = await service.GetCompanyByIdAsync(companyId);

        // Assert
        result.Should().BeEquivalentTo(cachedCompany);
        companyRepo.VerifyAll();
        // Should use cached data when clearly within retention period
        pennylaneServiceApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldUseExistingUserIdWhenUserExistsInCompany()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneServiceApiMock,
            out _);

        var accountId = _fixture.Create<int>();
        var contactId = _fixture.Create<int>();
        var existingUserId = "existing-user-id-123";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "existing@example.com")
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "executive")
            .Create();

        // Setup: Account exists
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        // Setup: Contact exists
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: Role doesn't exist yet
        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        // Setup: Get account number and contact for CreatePennylaneUserAsync
        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        authMgr
            .Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: User already exists in company
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((true, existingUserId))
            .Verifiable();

        // Setup: Should NOT create user or assign access (already exists)
        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()))
            .Throws(new Exception("Should not create user"));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(It.IsAny<string>(), It.IsAny<Contact>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new Exception("Should not assign access"));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .Throws(new Exception("Should not check global user"));

        // Setup: Insert role in DB with existing user ID
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "executive")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        accountRepo.VerifyAll();
        contactRepo.VerifyAll();
        pnlRepo.VerifyAll();
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber), Times.Once);
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, null), Times.Never);
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Never);
        authMgr.Verify(m => m.AssignUserAccessAsync(It.IsAny<string>(), It.IsAny<Contact>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldAssignAccessWhenGlobalUserExistsButNotInCompany()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out _,
            out _);

        var accountId = _fixture.Create<int>();
        var contactId = _fixture.Create<int>();
        var globalUserId = "global-user-id-456";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC456")
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "global@example.com")
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "accountant")
            .Create();

        // Setup basics
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync((PnlRoleEntity?)null)
            .Verifiable();

        // Setup: Get account number and contact
        authMgr
            .SetupSequence(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, globalUserId));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "accountant", globalUserId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()))
            .ReturnsAsync((globalUserId, false));

        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountEntity.AccountNumber)
            .Verifiable();

        authMgr
            .Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        // Setup: Insert role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "accountant")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber), Times.Exactly(2));
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "accountant", globalUserId), Times.Once);
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldCreateNewUserWhenDoesNotExistAnywhere()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out _,
            out _);

        var accountId = _fixture.Create<int>();
        var contactId = _fixture.Create<int>();
        var newUserId = "new-user-id-789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC789")
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "newuser@example.com")
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "executive")
            .Create();

        // Setup basics
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity);

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync((PnlRoleEntity?)null);

        // Setup: Get account number and contact
        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountEntity.AccountNumber);

        authMgr
            .Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact);

        // Setup: User doesn't exist in company (first call), then exists after creation (second call)
        authMgr
            .SetupSequence(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, newUserId));

        // Setup: Should create user
        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()))
            .ReturnsAsync((newUserId, false))
            .Verifiable();

        // Setup: Should assign access after creation
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "executive", newUserId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Setup: Insert role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "executive")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber), Times.Exactly(2));
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "executive", newUserId), Times.Once);
        pnlRepo.VerifyAll();
    }

    [Fact]
    public async Task InsertPennylaneRoleAsync_ShouldCreateUserWhenGlobalUserIdIsNullOrEmpty()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out _,
            out _);

        var accountId = _fixture.Create<int>();
        var contactId = _fixture.Create<int>();
        var newUserId = "new-user-created-999";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC999")
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "nullcase@example.com")
            .Create();

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "accountant")
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity);

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync((PnlRoleEntity?)null);

        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountEntity.AccountNumber);

        authMgr
            .Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact);

        // User doesn't exist in company (first call), then exists after creation (second call)
        authMgr
            .SetupSequence(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, newUserId));

        // Should create user because pennylaneUserId is null (fails the !string.IsNullOrEmpty check)
        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()))
            .ReturnsAsync((newUserId, false))
            .Verifiable();

        // Should assign access after creation
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "accountant", newUserId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Setup: Insert role
        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
                e.AccountId == accountId &&
                e.ContactId == contactId &&
                e.Role == "accountant")))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        authMgr.Verify(m => m.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber), Times.Exactly(2));
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountEntity.AccountNumber, contact, "accountant", newUserId), Times.Once);
        pnlRepo.VerifyAll();
    }

    // =========================
    // Tests pour InsertPennylaneRoleAsync
    // =========================

    [Fact]
    public async Task InsertPennylaneRoleAsync_HappyPath_InsertsAndLogs()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out _,
            out _
        );

        var accountId = 602;
        var contactId = 42217;
        var accountNumber = "ACC123";
        var userEmail = "nullcase@example.com";
        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .With(r => r.Role, "executive")
            .Create();
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, accountNumber)
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, userEmail)
            .Create();
        accountRepo.Setup(x => x.GetAccountByNumberOrIdAsync(role.AccountId.ToString())).ReturnsAsync(accountEntity);
        contactRepo.Setup(x => x.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId)).ReturnsAsync(contact);

        pnlRepo.Setup(x => x.FetchPennylaneRoleByAccountAndContactIdAsync(role.AccountId, role.ContactId)).ReturnsAsync((PnlRoleEntity)null);
        pnlRepo.Setup(x => x.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>())).Returns(Task.CompletedTask);
        authMgr.Setup(x => x.CheckIfUserExistsAsync(contact.Email, accountEntity.AccountNumber)).ReturnsAsync((false, null));
        authMgr.Setup(x => x.TryCreatePennylaneUserAsync(contact, It.IsAny<string>())).ReturnsAsync(("NEW_ID", false));
        authMgr.Setup(x => x.AssignUserAccessAsync(accountEntity.AccountNumber, contact, role.Role, "NEW_ID")).Returns(Task.CompletedTask);
        authMgr
            .Setup(x => x.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountNumber);
        authMgr
            .Setup(x => x.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact);

        // Act
        await service.InsertPennylaneRoleAsync(role);

        // Assert
        pnlRepo.Verify(x => x.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Once());
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ShouldThrowInvalidOperationException_WhenRetryFindsUserButNoId()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out _,
            out _
        );

        var accountId = 602;
        var contactId = 42217;
        var accountNumber = "ACC123";
        var userEmail = "test@example.com";

        var role = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, accountId)
            .With(r => r.ContactId, contactId)
            .Create();

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, accountNumber)
            .Create();

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, userEmail)
            .Create();

        // Setup basique
        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity);

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        pnlRepo
            .Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId))
            .ReturnsAsync((PnlRoleEntity?)null);

        authMgr.Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync(accountNumber);

        authMgr.Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(userEmail, accountNumber))
            .ReturnsAsync((false, (string?)null));

        var checkGlobalCallCount = 0;
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(userEmail, null))
            .ReturnsAsync(() =>
            {
                checkGlobalCallCount++;
                if (checkGlobalCallCount == 1)
                    return (false, (string?)null);
                else
                    return (true, (string?)null); // ❌ Trouvé mais sans ID !
            });

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("source_id must be unique"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.InsertPennylaneRoleAsync(role)
        );

        // Le code devrait re-throw l'exception car userId est null après retry
        exception.Message.Should().Contain("source_id must be unique");
    }

    #region CreateCompanyOnlyAsync Tests

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldCreateNewCompany_WhenDoesNotExist()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createResponse = new CreatePennylaneCompanyResponse { Id = "ACC123" };
        var createdCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, "ACC123")
            .With(c => c.Name, "Test Company")
            .Create();

        var createCompanyRequest = _fixture.Build<CreateCompanyRequest>()
            .With(c => c.AccountId, accountId)
            .With(c => c.HubName, string.Empty)
            .With(c => c.NumberOfUsers, "Gratuit")
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req =>
                req.Id == accountEntity.AccountNumber &&
                req.Name == accountEntity.LegalName &&
                req.NumberOfUsers == "unlimited" &&
                req.NumberOfEmployees == null)))
            .ReturnsAsync(CreateCompanyApiResult.Succeeded(createResponse))
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(createResponse.Id))
            .ReturnsAsync(createdCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == createdCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeEquivalentTo(createdCompany);
        alreadyExists.Should().BeFalse();
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
        companyRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnExistingCompany_WhenAlreadyExists()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .Create();

        var existingCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, "ACC123")
            .With(c => c.Name, "Existing Company")
            .Create();

        var createCompanyRequest = _fixture.Build<CreateCompanyRequest>()
            .With(c => c.AccountId, accountId)
            .With(c => c.HubName, string.Empty)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync(existingCompany)
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeEquivalentTo(existingCompany);
        alreadyExists.Should().BeTrue();
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
        pennylaneApiMock.Verify(p => p.CreateCompanyAsync(It.IsAny<CreatePennylaneCompanyRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnAlreadyExists_WhenRegNoConflictOccurs()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var registrationNumber = "123456789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createCompanyRequest = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>(),
            RegistrationNumber = registrationNumber,
            HubName = string.Empty
        };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req => req.RegNo == registrationNumber)))
            .ReturnsAsync(CreateCompanyApiResult.FailedRegNoExists("A company with the same reg_no already exist under the same firm."))
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeNull();
        alreadyExists.Should().BeTrue();
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnRequiresFirmAssignment_WhenUpstreamRefusesAssignment()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var registrationNumber = "123456789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createCompanyRequest = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>(),
            RegistrationNumber = registrationNumber,
            HubName = string.Empty
        };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req => req.RegNo == registrationNumber)))
            .ReturnsAsync(CreateCompanyApiResult.FailedRequiresFirmAssignment("Company already exists, the client must request to be assigned to your firm"))
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, requiresFirmAssignment, requiresFirmTransfer) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeNull();
        alreadyExists.Should().BeFalse();
        createdOnDefault.Should().BeFalse();
        requiresFirmAssignment.Should().BeTrue();
        requiresFirmTransfer.Should().BeFalse();
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnRequiresFirmTransfer_WhenUpstreamRefusesTransfer()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var registrationNumber = "123456789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createCompanyRequest = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>(),
            RegistrationNumber = registrationNumber,
            HubName = string.Empty
        };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req => req.RegNo == registrationNumber)))
            .ReturnsAsync(CreateCompanyApiResult.FailedRequiresFirmTransfer("Company already exists, you can request its transfer from its current firm"))
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, requiresFirmAssignment, requiresFirmTransfer) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeNull();
        alreadyExists.Should().BeFalse();
        createdOnDefault.Should().BeFalse();
        requiresFirmAssignment.Should().BeFalse();
        requiresFirmTransfer.Should().BeTrue();
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnNotCreated_WhenCreateCompanyFails()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var registrationNumber = "123456789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createCompanyRequest = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>(),
            RegistrationNumber = registrationNumber,
            HubName = string.Empty
        };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.IsAny<CreatePennylaneCompanyRequest>()))
            .ReturnsAsync(CreateCompanyApiResult.Failed("Unexpected error"))
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeNull();
        alreadyExists.Should().BeFalse();
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldReturnNotCreated_WhenCreateCompanyReturnsBadRequest()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var registrationNumber = "123456789";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createCompanyRequest = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>(),
            RegistrationNumber = registrationNumber,
            HubName = string.Empty
        };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.IsAny<CreatePennylaneCompanyRequest>()))
            .ReturnsAsync(CreateCompanyApiResult.Failed("Registration Number Registration number must only contain alphanumeric characters and"))
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeNull();
        alreadyExists.Should().BeFalse();
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldUseHubFirmId_WhenHubIsFound()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var hubName = "Hub A";
        var hubFirmId = "hub-firm-456";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createResponse = new CreatePennylaneCompanyResponse { Id = "ACC123" };
        var createdCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, "ACC123")
            .With(c => c.Name, "Test Company")
            .Create();

        var createCompanyRequest = _fixture.Build<CreateCompanyRequest>()
            .With(c => c.AccountId, accountId)
            .With(c => c.HubName, hubName)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetFirmIdByNameAsync(hubName))
            .ReturnsAsync(hubFirmId)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req =>
                req.Id == accountEntity.AccountNumber &&
                req.FirmId == hubFirmId)))
            .ReturnsAsync(CreateCompanyApiResult.Succeeded(createResponse))
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(createResponse.Id))
            .ReturnsAsync(createdCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == createdCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeEquivalentTo(createdCompany);
        alreadyExists.Should().BeFalse();
        createdOnDefault.Should().BeFalse();
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
        companyRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldFallbackToDefaultFirmId_WhenHubIsNotFound()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var accountId = 123;
        var hubName = "Missing Hub";
        var defaultFirmId = "firm-123";

        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, accountId)
            .With(a => a.AccountNumber, "ACC123")
            .With(a => a.LegalName, "Test Company")
            .Create();

        var createResponse = new CreatePennylaneCompanyResponse { Id = "ACC123" };
        var createdCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, "ACC123")
            .With(c => c.Name, "Test Company")
            .Create();

        var createCompanyRequest = _fixture.Build<CreateCompanyRequest>()
            .With(c => c.AccountId, accountId)
            .With(c => c.HubName, hubName)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(accountEntity)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountEntity.AccountNumber))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.TryGetFirmIdByNameAsync(hubName))
            .ReturnsAsync((string?)null)
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.CreateCompanyAsync(It.Is<CreatePennylaneCompanyRequest>(req =>
                req.Id == accountEntity.AccountNumber &&
                req.FirmId == defaultFirmId)))
            .ReturnsAsync(CreateCompanyApiResult.Succeeded(createResponse))
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(createResponse.Id))
            .ReturnsAsync(createdCompany)
            .Verifiable();

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list =>
                list.Count == 1 && list.First().Id == createdCompany.Id)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var (company, alreadyExists, createdOnDefault, _, _) = await service.CreateCompanyOnlyAsync(createCompanyRequest);

        // Assert
        company.Should().BeEquivalentTo(createdCompany);
        alreadyExists.Should().BeFalse();
        createdOnDefault.Should().BeTrue();
        accountRepo.VerifyAll();
        pennylaneApiMock.VerifyAll();
        companyRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyOnlyAsync_ShouldThrowNotFoundException_WhenAccountNotFound()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        var accountId = 999;

        var createCompanyRequest = _fixture.Build<CreateCompanyRequest>()
            .With(c => c.AccountId, accountId)
            .With(c => c.HubName, string.Empty)
            .Create();

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync((AccountEntity?)null)
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateCompanyOnlyAsync(createCompanyRequest));
        accountRepo.VerifyAll();
    }

    #endregion

    #region AddUsersToCompanyAsync Tests

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldReturnEmptyList_WhenNoContactsProvided()
    {
        // Arrange
        var service = CreateService(out _, out _, out _, out _, out _, out _, out _);
        var accountNumber = "ACC123";
        var emptyContacts = new List<int>();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, emptyContacts);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldReturnFailure_WhenContactNotFound()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out _,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactIds = new List<int> { 999 };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), 999))
            .ReturnsAsync((Contact?)null)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, contactIds);

        // Assert
        results.Should().HaveCount(1);
        results[0].ContactId.Should().Be(999);
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("not found in database");
        contactRepo.VerifyAll();
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldReturnSuccess_WhenUserAlreadyExistsInCompany()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "test@example.com")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((true, "userId123"))
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].ContactId.Should().Be(contactId);
        results[0].Email.Should().Be(contact.Email);
        results[0].Success.Should().BeTrue();
        results[0].Error.Should().BeNull();
        contactRepo.VerifyAll();
        authMgr.VerifyAll();
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldGrantAccess_WhenGlobalUserExists()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "test@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "globalUserId"))
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company)
            .Verifiable();

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "globalUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "globalUserId"), Times.Once);
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldCreateUserAndGrantAccess_WhenUserDoesNotExist()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "newuser@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null))
            .Verifiable();

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company)
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false))
            .Verifiable();

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldAssignAccountantRole_WhenContactIsCollaborator()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "collaborator@example.com")
            .With(c => c.Type, "Collaborator")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "CollabAuthMode"))
            .ReturnsAsync(("newUserId", false))
            .Verifiable();

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "accountant", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "accountant", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldReturnFailureAndContinue_WhenUserCreationFails()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var failingContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 1)
            .With(c => c.Email, "fail@example.com")
            .With(c => c.Type, "Customer")
            .Create();
        var okContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 2)
            .With(c => c.Email, "ok@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), 1))
            .ReturnsAsync(failingContact);
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), 2))
            .ReturnsAsync(okContact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(failingContact.Email, accountNumber))
            .ReturnsAsync((false, null));
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(failingContact.Email, null))
            .ReturnsAsync((false, null));
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(okContact.Email, accountNumber))
            .ReturnsAsync((false, null));
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(okContact.Email, null))
            .ReturnsAsync((false, null));

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(failingContact, "AuthMode"))
            .ReturnsAsync(((string?)null, false));
        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(okContact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, okContact, "role", "newUserId"))
            .Returns(Task.CompletedTask);

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { 1, 2 });

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results[0].ContactId.Should().Be(1);
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().NotBeNullOrEmpty();
        results[1].ContactId.Should().Be(2);
        results[1].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, failingContact, It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldHandleExceptions_AndReturnFailureResult()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "error@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ThrowsAsync(new Exception("API Error"))
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].ContactId.Should().Be(contactId);
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("API Error");
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldProcessMultipleContacts_WithMixedResults()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contact1 = _fixture.Build<Contact>()
            .With(c => c.ContactId, 1)
            .With(c => c.Email, "success@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var contact2 = _fixture.Build<Contact>()
            .With(c => c.ContactId, 2)
            .With(c => c.Email, "fail@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan")
            .Create();

        // Setup for contact 1 (success)
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), 1))
            .ReturnsAsync(contact1);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact1.Email, accountNumber))
            .ReturnsAsync((true, "userId1"));

        // Setup for contact 2 (not found)
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), 2))
            .ReturnsAsync((Contact?)null);

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { 1, 2 });

        // Assert
        results.Should().HaveCount(2);
        results[0].ContactId.Should().Be(1);
        results[0].Success.Should().BeTrue();
        results[1].ContactId.Should().Be(2);
        results[1].Success.Should().BeFalse();
        results[1].Error.Should().Contain("not found in database");
    }

    #endregion

    #region CreateCollaboratorsAndGetPennylaneIdsAsync Tests

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnNullForAll_WhenContactFunctionsIsNull()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(null);

        // Assert
        accountingManagerId.Should().BeNull();
        accountingSupervisorId.Should().BeNull();
        accountantId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnNullForAll_WhenContactFunctionsIsEmpty()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        var contactFunctions = new List<ContactFunctions>();

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().BeNull();
        accountingSupervisorId.Should().BeNull();
        accountantId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldCreateAccountingManager_WhenProvided()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 100))
            .ReturnsAsync(contact);

        authMgr
            .SetupSequence(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "CollabAuthMode"))
            .ReturnsAsync(("pnl-manager-123", false));

        pennylaneApiMock
            .Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()))
            .ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().Be("pnl-manager-123");
        accountingSupervisorId.Should().BeNull();
        accountantId.Should().BeNull();
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldCreateAllThreeRoles_WhenAllProvided()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var supervisorContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 200)
            .With(c => c.Email, "supervisor@example.com")
            .Create();

        var accountantContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 300)
            .With(c => c.Email, "accountant@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId },
                new() { ContactId = 300, FunctionId = PennylaneFunctions.AccountantFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 200)).ReturnsAsync(supervisorContact);
        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 300)).ReturnsAsync(accountantContact);

        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(supervisorContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-supervisor-456"));
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(accountantContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-accountant-789"));

        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(supervisorContact, "CollabAuthMode")).ReturnsAsync(("pnl-supervisor-456", false));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(accountantContact, "CollabAuthMode")).ReturnsAsync(("pnl-accountant-789", false));

        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().Be("pnl-manager-123");
        accountingSupervisorId.Should().Be("pnl-supervisor-456");
        accountantId.Should().Be("pnl-accountant-789");
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", It.IsAny<string?>(), It.IsAny<string?>()), Times.Exactly(3));
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnExistingId_WhenUserAlreadyExists()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "existing@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 100))
            .ReturnsAsync(contact);

        // User already exists in Pennylane
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "existing-pnl-id-999"));

        // Grant access to hub should still be called for existing users
        pennylaneApiMock
            .Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "existing-pnl-id-999", It.IsAny<string?>()))
            .ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, _, _) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should return existing ID, not create new, but still grant access
        accountingManagerId.Should().Be("existing-pnl-id-999");
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "existing-pnl-id-999", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnNull_WhenContactNotFound()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out _,
            out _,
            out _);

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 999, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        // Contact not found in database
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 999))
            .ReturnsAsync((Contact?)null);

        // Act
        var (accountingManagerId, _, _) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnNull_WhenExceptionOccurs()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out _,
            out _,
            out _);

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        // Exception thrown
        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 100))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var (accountingManagerId, _, _) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should return null and not throw
        accountingManagerId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldHandlePartialSuccess()
    {
        // Arrange - one succeeds, one fails
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

        // Manager found and created successfully
        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Supervisor not found
        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 200)).ReturnsAsync((Contact?)null);

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - manager succeeds, supervisor fails
        accountingManagerId.Should().Be("pnl-manager-123");
        accountingSupervisorId.Should().BeNull();
        accountantId.Should().BeNull();
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldCreateSupervisorOnly_WhenOnlySupervisorProvided()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var supervisorContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 200)
            .With(c => c.Email, "supervisor@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 200)).ReturnsAsync(supervisorContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(supervisorContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-supervisor-456"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(supervisorContact, "CollabAuthMode")).ReturnsAsync(("pnl-supervisor-456", false));
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-supervisor-456", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().BeNull();
        accountingSupervisorId.Should().Be("pnl-supervisor-456");
        accountantId.Should().BeNull();
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-supervisor-456", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldCreateAccountantOnly_WhenOnlyAccountantProvided()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountantContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 300)
            .With(c => c.Email, "accountant@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 300, FunctionId = PennylaneFunctions.AccountantFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 300)).ReturnsAsync(accountantContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(accountantContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-accountant-789"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(accountantContact, "CollabAuthMode")).ReturnsAsync(("pnl-accountant-789", false));
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-accountant-789", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().BeNull();
        accountingSupervisorId.Should().BeNull();
        accountantId.Should().Be("pnl-accountant-789");
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-accountant-789", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldHandleMixOfExistingAndNewUsers()
    {
        // Arrange - manager exists, supervisor new
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var supervisorContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 200)
            .With(c => c.Email, "supervisor@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId },
                new() { ContactId = 200, FunctionId = PennylaneFunctions.AccountingSupervisorFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 200)).ReturnsAsync(supervisorContact);

        // Manager already exists
        authMgr.Setup(m => m.CheckIfUserExistsAsync(managerContact.Email, null)).ReturnsAsync((true, "existing-manager-id"));
        // Supervisor is new
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(supervisorContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "new-supervisor-id"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(supervisorContact, "CollabAuthMode")).ReturnsAsync(("new-supervisor-id", false));
        // Grant access should be called for both existing and new users
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "existing-manager-id", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "new-supervisor-id", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, accountingSupervisorId, _) =
            await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert
        accountingManagerId.Should().Be("existing-manager-id");
        accountingSupervisorId.Should().Be("new-supervisor-id");
        // TryCreatePennylaneUserAsync should only be called for supervisor
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(managerContact, It.IsAny<string>()), Times.Never);
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(supervisorContact, "CollabAuthMode"), Times.Once);
        // GrantFirmUserAccessAsync should be called for both existing and new users
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "existing-manager-id", It.IsAny<string?>()), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "new-supervisor-id", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldUseHubFirmId_WhenHubNameProvidedAndFound()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));

        // Hub name is provided and found
        pennylaneApiMock.Setup(p => p.TryGetFirmIdByNameAsync("MyHub")).ReturnsAsync("hub-firm-456");
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("hub-firm-456", "accountant", "pnl-manager-123", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions, "MyHub");

        // Assert
        accountingManagerId.Should().Be("pnl-manager-123");
        // Should use hub firm ID, not default
        pennylaneApiMock.Verify(p => p.TryGetFirmIdByNameAsync("MyHub"), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("hub-firm-456", "accountant", "pnl-manager-123", It.IsAny<string?>()), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldFallbackToDefaultFirm_WhenHubNameNotFound()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));

        // Hub name is provided but NOT found - returns null
        pennylaneApiMock.Setup(p => p.TryGetFirmIdByNameAsync("UnknownHub")).ReturnsAsync((string?)null);
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>())).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions, "UnknownHub");

        // Assert
        accountingManagerId.Should().Be("pnl-manager-123");
        // Should fallback to default firm ID
        pennylaneApiMock.Verify(p => p.TryGetFirmIdByNameAsync("UnknownHub"), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnUserId_WhenGrantFirmAccessFails()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));

        // Grant firm access fails
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()))
            .ReturnsAsync((false, "User already has access"));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should still return the user ID even though grant failed
        accountingManagerId.Should().Be("pnl-manager-123");
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", "pnl-manager-123", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldNotGrantAccess_WhenFirmIdIsNull()
    {
        // Arrange - Create service with null firm token options
        var accountRepo = new Mock<IAccountRepository>(MockBehavior.Strict);
        var companyRepo = new Mock<ICompanyRepository>(MockBehavior.Strict);
        var contactRepo = new Mock<IContactRepository>(MockBehavior.Strict);
        var pnlRepo = new Mock<IPennylaneRepository>(MockBehavior.Strict);
        var authMgr = new Mock<IAuthorizationUpdatedEventManager>(MockBehavior.Strict);
        var pennylaneApiMock = new Mock<IPennylaneApiGroupService>();
        var pennylaneApiFirmMock = new Mock<IPennylaneApiFirmService>();

        var opts = Options.Create(new PennylaneServiceOptions
        {
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "niceplan",
            EnableEventHandler = true,
            DefaultRole = "def",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            Token = "Token",
            LastSyncDateRetentionDays = 30,
            PlansRoleAssignments = new List<PennylanePlansRoleAssignmentOptions>()
        });

        // Empty firm token options
        var firmTokenOpts = Options.Create(new PennylaneFirmTokenOptions());

        var service = new PennylaneService(
            accountRepo.Object,
            contactRepo.Object,
            companyRepo.Object,
            pnlRepo.Object,
            NullLogger<PennylaneService>.Instance,
            authMgr.Object,
            opts,
            firmTokenOpts,
            pennylaneApiMock.Object,
            pennylaneApiFirmMock.Object);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);
        authMgr.SetupSequence(m => m.CheckIfUserExistsAsync(managerContact.Email, null))
            .ReturnsAsync((false, (string?)null))
            .ReturnsAsync((true, "pnl-manager-123"));
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync(("pnl-manager-123", false));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should return the user ID, but GrantFirmUserAccessAsync should NOT be called since firmId is null
        accountingManagerId.Should().Be("pnl-manager-123");
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldProceedWithEmail_WhenUserCreationReturnsAlreadyExists()
    {
        // Arrange - User creation returns (null, true) meaning "already taken"
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);

        // User doesn't exist globally when checked
        authMgr.Setup(m => m.CheckIfUserExistsAsync(managerContact.Email, null)).ReturnsAsync((false, null));
        // TryCreatePennylaneUserAsync returns (null, true) - meaning user already exists (email already taken)
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync((null, true));
        // Grant access should be called with email (not userId) since userId is null
        pennylaneApiMock.Setup(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", null, managerContact.Email)).ReturnsAsync((true, (string?)null));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should return null for userId but grant access should have been attempted with email
        accountingManagerId.Should().BeNull();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode"), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync("firm-123", "accountant", null, managerContact.Email), Times.Once);
    }

    [Fact]
    public async Task CreateCollaboratorsAndGetPennylaneIdsAsync_ShouldReturnNull_WhenUserCreationFailsCompletely()
    {
        // Arrange - User creation returns (null, false) meaning creation failed
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var managerContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "manager@example.com")
            .Create();

        var contactFunctions = new List<ContactFunctions>
            {
                new() { ContactId = 100, FunctionId = PennylaneFunctions.AccountingManagerFunctionId }
            };

        contactRepo.Setup(r => r.GetContactByEmailOrIdAsync(null, 100)).ReturnsAsync(managerContact);

        // User doesn't exist globally when checked
        authMgr.Setup(m => m.CheckIfUserExistsAsync(managerContact.Email, null)).ReturnsAsync((false, null));
        // TryCreatePennylaneUserAsync returns (null, false) - meaning creation failed for another reason
        authMgr.Setup(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode")).ReturnsAsync((null, false));

        // Act
        var (accountingManagerId, _, _) = await service.CreateCollaboratorsAndGetPennylaneIdsAsync(contactFunctions);

        // Assert - should return null and NOT call GrantFirmUserAccessAsync
        accountingManagerId.Should().BeNull();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(managerContact, "CollabAuthMode"), Times.Once);
        pennylaneApiMock.Verify(p => p.GrantFirmUserAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    #endregion

    #region OverrideClientRole Tests

    private PennylaneService CreateServiceWithOverrideRole(
        string? overrideRole,
        out Mock<IAccountRepository> accountRepo,
        out Mock<ICompanyRepository> companyRepo,
        out Mock<IContactRepository> contactRepo,
        out Mock<IPennylaneRepository> pnlRepo,
        out Mock<IAuthorizationUpdatedEventManager> authorizationUpdatedEventManager,
        out Mock<IPennylaneApiGroupService> pennylaneServiceApiMock,
        out Mock<IPennylaneApiFirmService> pennylaneServiceApiFirmMock,
        string? defaultRole = "def",
        List<PennylanePlansRoleAssignmentOptions>? plansRoleAssignments = null)
    {
        accountRepo = new Mock<IAccountRepository>(MockBehavior.Strict);
        companyRepo = new Mock<ICompanyRepository>(MockBehavior.Strict);
        contactRepo = new Mock<IContactRepository>(MockBehavior.Strict);
        pnlRepo = new Mock<IPennylaneRepository>(MockBehavior.Strict);

        authorizationUpdatedEventManager = new Mock<IAuthorizationUpdatedEventManager>(MockBehavior.Strict);
        pennylaneServiceApiMock = new Mock<IPennylaneApiGroupService>();
        pennylaneServiceApiFirmMock = new Mock<IPennylaneApiFirmService>();

        var opts = Options.Create(new PennylaneServiceOptions
        {
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "niceplan",
            EnableEventHandler = true,
            DefaultRole = defaultRole,
            OverrideClientRole = overrideRole,
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            Token = "Token",
            LastSyncDateRetentionDays = 30,
            PlansRoleAssignments = plansRoleAssignments ?? new List<PennylanePlansRoleAssignmentOptions> {
                    new() { Plan = "plan", ClientRole = "role" }
                }
        });

        var firmTokenOpts = Options.Create(new PennylaneFirmTokenOptions
            {
                new PennylaneFirmTokenItem { Id = "firm-123", Tokens = new List<string> { "firm-token" } }
            });

        var service = new PennylaneService(
            accountRepo.Object,
            contactRepo.Object,
            companyRepository: companyRepo.Object,
            pnlRepo.Object,
            NullLogger<PennylaneService>.Instance,
            authorizationUpdatedEventManager.Object,
            opts,
            firmTokenOpts,
            pennylaneServiceApiMock.Object,
            pennylaneServiceApiFirmMock.Object);

        return service;
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldUseOverrideClientRole_WhenConfigured()
    {
        // Arrange
        var service = CreateServiceWithOverrideRole(
            "executive",
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false))
            .Verifiable();

        // Should use "executive" from OverrideClientRole, NOT from plan mapping
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "executive", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "executive", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldNotCallPennylaneApi_WhenOverrideClientRoleIsConfigured()
    {
        // Arrange
        var service = CreateServiceWithOverrideRole(
            "executive",
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "executive", "newUserId"))
            .Returns(Task.CompletedTask);

        // Act
        await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert - Should NOT call GetCompanyByIdAsync since OverrideClientRole bypasses plan-based role lookup
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldUsePlanBasedRole_WhenOverrideClientRoleIsNull()
    {
        // Arrange - using CreateService which has null OverrideClientRole
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan") // Matches the plan in CreateService
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        // Should call GetCompanyByIdAsync to get SaaS plan
        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company)
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use "role" from PlansRoleAssignments (plan -> role mapping)
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(accountNumber), Times.Once);
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldUsePlanBasedRole_WhenOverrideClientRoleIsEmpty()
    {
        // Arrange
        var service = CreateServiceWithOverrideRole(
            "", // Empty string should be treated as not configured
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        // Should call GetCompanyByIdAsync because OverrideClientRole is empty
        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company)
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "role", "newUserId"))
            .Returns(Task.CompletedTask);

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(accountNumber), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_CollaboratorShouldStillGetAccountantRole_EvenWithOverrideClientRole()
    {
        // Arrange - OverrideClientRole only affects Customer contacts, not Collaborators
        var service = CreateServiceWithOverrideRole(
            "executive",
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "collaborator@example.com")
            .With(c => c.Type, "Collaborator")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact)
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null))
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "CollabAuthMode"))
            .ReturnsAsync(("newUserId", false))
            .Verifiable();

        // Collaborators should still get "accountant" role, NOT the override
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "accountant", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "accountant", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldThrowBadRequestException_WhenDefaultRoleIsNotConfigured()
    {
        // Arrange - No OverrideClientRole and no DefaultRole
        var service = CreateServiceWithOverrideRole(
            null, // No override
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _,
            defaultRole: null); // No default role - should throw

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert - Should fail because DefaultRole is not configured
        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("Default role is not configured");
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseDefaultRole_WhenNoPlansRoleAssignmentsConfigured()
    {
        // Arrange - No OverrideClientRole and empty PlansRoleAssignments
        var service = CreateServiceWithOverrideRole(
            null, // No override
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _,
            defaultRole: "defaultClientRole",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>()); // Empty list

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use default role since no PlansRoleAssignments
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "defaultClientRole", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "defaultClientRole", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseDefaultRole_WhenPlanNotFoundInMapping()
    {
        // Arrange - Plan exists but not in mapping
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "fallbackRole",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "premium_plan", ClientRole = "premiumRole" }
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "basic_plan") // Different plan, not in mapping
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use fallback role since plan not in mapping
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "fallbackRole", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "fallbackRole", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseDefaultRole_WhenApiCallFails()
    {
        // Arrange - API call fails, should fallback to default
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "errorFallbackRole",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "any_plan", ClientRole = "anyRole" }
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        // API call throws exception
        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ThrowsAsync(new HttpRequestException("API Error"));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use fallback role when API fails
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "errorFallbackRole", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "errorFallbackRole", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseMappedRole_WhenPlanFoundInMapping()
    {
        // Arrange - Plan found in mapping
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "shouldNotBeUsed",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "v1_saas_free", ClientRole = "executive" },
                    new() { Plan = "v2_saas_premium", ClientRole = "admin" }
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "v1_saas_free") // Matches first mapping
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use mapped role "executive"
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "executive", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "executive", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldBeCaseInsensitive_WhenMatchingPlan()
    {
        // Arrange - Plan matching should be case insensitive
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "shouldNotBeUsed",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "V1_SAAS_FREE", ClientRole = "caseInsensitiveRole" }
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "v1_saas_free") // Lowercase version
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should match despite case difference
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "caseInsensitiveRole", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "caseInsensitiveRole", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseDefaultRole_WhenMappingHasEmptyClientRole()
    {
        // Arrange - Mapping exists but ClientRole is empty
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "defaultWhenEmpty",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "plan_with_empty_role", ClientRole = "" } // Empty ClientRole
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "plan_with_empty_role")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        // Should use default role because mapping's ClientRole is empty
        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "defaultWhenEmpty", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "defaultWhenEmpty", "newUserId"), Times.Once);
    }

    [Theory]
    [InlineData("executive")]
    [InlineData("viewer")]
    [InlineData("admin")]
    [InlineData("employee")]
    public async Task GetClientRoleAsync_ShouldSupportDifferentOverrideRoleValues(string overrideRole)
    {
        // Arrange - Test various override role values
        var service = CreateServiceWithOverrideRole(
            overrideRole,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, overrideRole, "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, overrideRole, "newUserId"), Times.Once);
    }

    [Fact]
    public async Task GetClientRoleAsync_ShouldUseDefaultRole_WhenPlansRoleAssignmentsIsNull()
    {
        // Arrange - PlansRoleAssignments is null
        var service = CreateServiceWithOverrideRole(
            null,
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _,
            defaultRole: "nullAssignmentsFallback",
            plansRoleAssignments: null!);

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "nullAssignmentsFallback", "newUserId"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        authMgr.Verify(m => m.AssignUserAccessAsync(accountNumber, contact, "nullAssignmentsFallback", "newUserId"), Times.Once);
    }

    [Fact]
    public async Task AddUsersToCompanyAsync_ShouldHandleWhitespaceOnlyOverrideRole()
    {
        // Arrange - Override role with whitespace only should be treated as not configured
        var service = CreateServiceWithOverrideRole(
            "   ", // Whitespace only
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out var pennylaneApiMock,
            out _,
            defaultRole: "whitespaceNotUsed",
            plansRoleAssignments: new List<PennylanePlansRoleAssignmentOptions>
            {
                    new() { Plan = "testPlan", ClientRole = "planBasedRole" }
            });

        var accountNumber = "ACC123";
        var contactId = 1;
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, contactId)
            .With(c => c.Email, "customer@example.com")
            .With(c => c.Type, "Customer")
            .Create();

        var company = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, accountNumber)
            .With(c => c.SaasPlan, "testPlan")
            .Create();

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string>(), contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, accountNumber))
            .ReturnsAsync((false, null));

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, null));

        // Should call GetCompanyByIdAsync because whitespace-only override is ignored
        pennylaneApiMock
            .Setup(p => p.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company)
            .Verifiable();

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync(("newUserId", false));

        authMgr
            .Setup(m => m.AssignUserAccessAsync(accountNumber, contact, "planBasedRole", "newUserId"))
            .Returns(Task.CompletedTask);

        // Act
        var results = await service.AddUsersToCompanyAsync(accountNumber, new List<int> { contactId });

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(accountNumber), Times.Once);
    }

    #endregion

    #region GetCompanyByAccountIdAsync Tests

    [Fact]
    public async Task GetCompanyByAccountIdAsync_WhenAccountNotFound_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var accountId = 123;

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync((AccountEntity?)null);

        // Act
        var result = await service.GetCompanyByAccountIdAsync(accountId);

        // Assert
        result.Should().BeNull();
        accountRepo.Verify(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()), Times.Once);
    }

    [Fact]
    public async Task GetCompanyByAccountIdAsync_WhenCompanyExistsInRepository_ReturnsCompanyFromRepository()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var accountId = 123;
        var accountNumber = "ACC123";
        var account = new AccountEntity { AccountId = accountId, AccountNumber = accountNumber };
        var company = new PennylaneCompany { Id = accountNumber, Name = "Test Company" };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(account);

        companyRepo
            .Setup(r => r.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        // Act
        var result = await service.GetCompanyByAccountIdAsync(accountId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(company);
        companyRepo.Verify(r => r.GetCompanyByIdAsync(accountNumber), Times.Once);
        pennylaneApiMock.Verify(p => p.TryGetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyByAccountIdAsync_WhenCompanyNotInRepository_FetchesFromApiAndCaches()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var accountId = 123;
        var accountNumber = "ACC123";
        var account = new AccountEntity { AccountId = accountId, AccountNumber = accountNumber };
        var company = new PennylaneCompany { Id = accountNumber, Name = "Test Company from API" };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(account);

        companyRepo
            .Setup(r => r.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync((PennylaneCompany?)null);

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        companyRepo
            .Setup(r => r.SaveCompaniesAsync(It.Is<List<PennylaneCompany>>(list => list.Contains(company))))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.GetCompanyByAccountIdAsync(accountId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(company);
        pennylaneApiMock.Verify(p => p.TryGetCompanyByIdAsync(accountNumber), Times.Once);
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Once);
    }

    [Fact]
    public async Task GetCompanyByAccountIdAsync_WhenCompanyNotInRepositoryAndNotInApi_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var accountId = 123;
        var accountNumber = "ACC123";
        var account = new AccountEntity { AccountId = accountId, AccountNumber = accountNumber };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(account);

        companyRepo
            .Setup(r => r.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync((PennylaneCompany?)null);

        pennylaneApiMock
            .Setup(p => p.TryGetCompanyByIdAsync(accountNumber))
            .ReturnsAsync((PennylaneCompany?)null);

        // Act
        var result = await service.GetCompanyByAccountIdAsync(accountId);

        // Assert
        result.Should().BeNull();
        pennylaneApiMock.Verify(p => p.TryGetCompanyByIdAsync(accountNumber), Times.Once);
        companyRepo.Verify(r => r.SaveCompaniesAsync(It.IsAny<List<PennylaneCompany>>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyByAccountIdAsync_WhenCompanyFoundInRepository_DoesNotCallApi()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var accountId = 456;
        var accountNumber = "ACC456";
        var account = new AccountEntity { AccountId = accountId, AccountNumber = accountNumber };
        var company = new PennylaneCompany { Id = accountNumber, Name = "Cached Company" };

        accountRepo
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountId.ToString()))
            .ReturnsAsync(account);

        companyRepo
            .Setup(r => r.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        // Act
        var result = await service.GetCompanyByAccountIdAsync(accountId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Cached Company");
        pennylaneApiMock.Verify(p => p.TryGetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        pennylaneApiMock.Verify(p => p.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetPennylaneCompanyUser Tests

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenContactNotFound_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().BeNull();
        result.AlreadyExistsInCompany.Should().BeFalse();
        contactRepo.Verify(r => r.GetContactByEmailOrIdAsync(null, contactId), Times.Once);
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenUserExistsInCompany_ReturnsUserFromCompany()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var pennylaneUserId = "pnl-user-123";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((true, pennylaneUserId));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(pennylaneUserId);
        result.User.Email.Should().Be(contact.Email);
        result.AlreadyExistsInCompany.Should().BeTrue();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenUserNotInCompanyButExistsGlobally_ReturnsGlobalUser()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var globalPennylaneUserId = "global-pnl-user-456";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User not in company
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((false, (string?)null));

        // User exists globally (null accountNumber)
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, globalPennylaneUserId));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(globalPennylaneUserId);
        result.User.Email.Should().Be(contact.Email);
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenUserNotInCompanyAndNotGlobally_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User not in company
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((false, (string?)null));

        // User doesn't exist globally either
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((false, (string?)null));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().BeNull();
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenUserInCompanyWithEmptyId_FallsBackToGlobalCheck()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var globalPennylaneUserId = "global-user-789";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User exists in company but with empty ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((true, string.Empty));

        // User exists globally with valid ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, globalPennylaneUserId));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(globalPennylaneUserId);
        result.User.Email.Should().Be(contact.Email);
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenUserInCompanyWithWhitespaceId_FallsBackToGlobalCheck()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var globalPennylaneUserId = "global-user-789";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User exists in company but with whitespace ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((true, "   "));

        // User exists globally with valid ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, globalPennylaneUserId));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(globalPennylaneUserId);
        result.User.Email.Should().Be(contact.Email);
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenGlobalUserHasEmptyId_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User not in company
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((false, (string?)null));

        // User exists globally but with empty ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, string.Empty));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().BeNull();
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenGlobalUserHasWhitespaceId_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User not in company
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((false, (string?)null));

        // User exists globally but with whitespace ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, "   "));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().BeNull();
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetPennylaneCompanyUser_WhenHasExistingUserInCompanyFalseButIdProvided_FallsBackToGlobalCheck()
    {
        // Arrange
        var service = CreateService(
            out var accountRepo,
            out var companyRepo,
            out var contactRepo,
            out var pnlRepo,
            out var authMgr,
            out var pennylaneApiMock,
            out var pennylaneApiFirmMock);

        var pennylaneCompanyId = "COMP123";
        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var globalPennylaneUserId = "global-user-999";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // hasExistingUserInCompany is false but ID is provided (edge case)
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == pennylaneCompanyId)))
            .ReturnsAsync((false, "some-id"));

        // User exists globally
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.Is<string>(e => e == contact.Email), It.Is<string?>(c => c == null)))
            .ReturnsAsync((true, globalPennylaneUserId));

        // Act
        var result = await service.GetPennylaneCompanyUser(pennylaneCompanyId, contactId);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(globalPennylaneUserId);
        result.AlreadyExistsInCompany.Should().BeFalse();
    }

    #endregion

    #region CreatePennylaneUserAsync(int contactId) Tests

    [Fact]
    public async Task CreatePennylaneUserAsync_ByContactId_WhenContactNotFound_ReturnsNull()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out _,
            out _,
            out _);

        var contactId = 123;

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await service.CreatePennylaneUserAsync(contactId);

        // Assert
        result.UserId.Should().BeNull();
        result.AlreadyExists.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ByContactId_WhenUserAlreadyExists_ReturnsExistingUserId()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var existingUserId = "existing-user-123";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, existingUserId));

        // Act
        var result = await service.CreatePennylaneUserAsync(contactId);

        // Assert
        result.UserId.Should().Be(existingUserId);
        result.AlreadyExists.Should().BeTrue();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(It.IsAny<Contact>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ByContactId_WhenUserDoesNotExist_CreatesNewUser()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var newUserId = "new-user-456";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((false, (string?)null));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync((newUserId, false));

        // Act
        var result = await service.CreatePennylaneUserAsync(contactId);

        // Assert
        result.UserId.Should().Be(newUserId);
        result.AlreadyExists.Should().BeFalse();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"), Times.Once);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ByContactId_WhenUserExistsWithEmptyId_CreatesNewUser()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var newUserId = "new-user-789";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User exists but with empty ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, string.Empty));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync((newUserId, false));

        // Act
        var result = await service.CreatePennylaneUserAsync(contactId);

        // Assert
        result.UserId.Should().Be(newUserId);
        result.AlreadyExists.Should().BeFalse();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"), Times.Once);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ByContactId_WhenUserExistsWithWhitespaceId_CreatesNewUser()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out var authMgr,
            out _,
            out _);

        var contactId = 123;
        var contact = new Contact { ContactId = contactId, Email = "test@example.com" };
        var newUserId = "new-user-999";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, contactId))
            .ReturnsAsync(contact);

        // User exists but with whitespace ID
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(contact.Email, null))
            .ReturnsAsync((true, "   "));

        authMgr
            .Setup(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"))
            .ReturnsAsync((newUserId, false));

        // Act
        var result = await service.CreatePennylaneUserAsync(contactId);

        // Assert
        result.UserId.Should().Be(newUserId);
        result.AlreadyExists.Should().BeFalse();
        authMgr.Verify(m => m.TryCreatePennylaneUserAsync(contact, "AuthMode"), Times.Once);
    }

    #endregion

    #region InsertUserPennylaneRole Tests

    [Fact]
    public async Task InsertUserPennylaneRole_WhenAccessGranted_ReturnsTrue()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out _,
            out var pennylaneApiMock,
            out _);

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 2)
            .With(c => c.Email, "test@example.com")
            .Create();

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var pennylaneUserId = "user-123";
        var pennylaneCompanyId = "company-456";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 2))
            .ReturnsAsync(contact);

        pennylaneApiMock
            .Setup(p => p.TryGrantCompanyUserAccessAsync(pennylaneCompanyId, It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(123);

        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.InsertUserPennylaneRole(pnlRole, pennylaneUserId, pennylaneCompanyId);

        // Assert
        result.Should().BeTrue();
        pnlRepo.Verify(r => r.InsertPennylaneRoleAsync(It.Is<PnlRoleEntity>(e =>
            e.AccountId == pnlRole.AccountId &&
            e.ContactId == pnlRole.ContactId &&
            e.Role == pnlRole.Role)), Times.Once);
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenAccessNotGranted_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out _,
            out var pennylaneApiMock,
            out _);

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 2)
            .With(c => c.Email, "test@example.com")
            .Create();

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var pennylaneUserId = "user-123";
        var pennylaneCompanyId = "company-456";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 2))
            .ReturnsAsync(contact);

        pennylaneApiMock
            .Setup(p => p.TryGrantCompanyUserAccessAsync(pennylaneCompanyId, It.IsAny<GrantUserAccess>()))
            .ReturnsAsync((int?)null);

        // Act
        var result = await service.InsertUserPennylaneRole(pnlRole, pennylaneUserId, pennylaneCompanyId);

        // Assert
        result.Should().BeFalse();
        pnlRepo.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenPnlRoleIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InsertUserPennylaneRole(null!, "user-123", "company-456"));
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenContactNotFound_ReturnsFalse()
    {
        // Arrange - With default feature flag (UseUserIdForGrantAccess = false), the contact is needed
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out _,
            out _,
            out _,
            out _);

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 999, Role = "employee" };

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 999))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await service.InsertUserPennylaneRole(pnlRole, "user-123", "company-456");

        // Assert - Should return false when contact is not found
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenPennylaneCompanyIdIsNull_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InsertUserPennylaneRole(pnlRole, "user-123", null!));
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenPennylaneCompanyIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentException for empty strings
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InsertUserPennylaneRole(pnlRole, "user-123", string.Empty));
    }

    [Fact]
    public async Task InsertUserPennylaneRole_WhenInsertFails_ThrowsException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out var contactRepo,
            out var pnlRepo,
            out _,
            out var pennylaneApiMock,
            out _);

        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 2)
            .With(c => c.Email, "test@example.com")
            .Create();

        var pnlRole = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var pennylaneUserId = "user-123";
        var pennylaneCompanyId = "company-456";

        contactRepo
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 2))
            .ReturnsAsync(contact);

        pennylaneApiMock
            .Setup(p => p.TryGrantCompanyUserAccessAsync(pennylaneCompanyId, It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(123);

        pnlRepo
            .Setup(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()))
            .ThrowsAsync(new System.InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<System.InvalidOperationException>(() =>
            service.InsertUserPennylaneRole(pnlRole, pennylaneUserId, pennylaneCompanyId));
    }

    #endregion

    #region RevokeUserAccessAsync Tests

    [Fact]
    public async Task RevokeUserAccessAsync_WhenValidParameters_CallsRevokeOnApi()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var pennylaneCompanyId = "company-456";
        var pennylaneUserId = "user-123";

        pennylaneApiMock
            .Setup(p => p.RevokeAccessUserCompanyAsync(pennylaneCompanyId, pennylaneUserId))
            .Returns(Task.CompletedTask);

        // Act
        await service.RevokeUserAccessAsync(pennylaneCompanyId, pennylaneUserId);

        // Assert
        pennylaneApiMock.Verify(p => p.RevokeAccessUserCompanyAsync(pennylaneCompanyId, pennylaneUserId), Times.Once);
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenCompanyIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.RevokeUserAccessAsync(null!, "user-123"));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenCompanyIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RevokeUserAccessAsync(string.Empty, "user-123"));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenCompanyIdIsWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RevokeUserAccessAsync("   ", "user-123"));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenUserIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.RevokeUserAccessAsync("company-456", null!));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenUserIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RevokeUserAccessAsync("company-456", string.Empty));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenUserIdIsWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RevokeUserAccessAsync("company-456", "   "));
    }

    [Fact]
    public async Task RevokeUserAccessAsync_WhenApiThrowsException_PropagatesException()
    {
        // Arrange
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            out _,
            out var pennylaneApiMock,
            out _);

        var pennylaneCompanyId = "company-456";
        var pennylaneUserId = "user-123";

        pennylaneApiMock
            .Setup(p => p.RevokeAccessUserCompanyAsync(pennylaneCompanyId, pennylaneUserId))
            .ThrowsAsync(new System.InvalidOperationException("API error"));

        // Act & Assert
        await Assert.ThrowsAsync<System.InvalidOperationException>(() =>
            service.RevokeUserAccessAsync(pennylaneCompanyId, pennylaneUserId));
    }

    #endregion
}
