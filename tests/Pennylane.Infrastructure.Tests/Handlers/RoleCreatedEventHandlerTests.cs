// <copyright file="RoleCreatedEventHandlerTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Models.Contacts;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Infrastructure.Tests.Providers;

public class RoleCreatedEventHandlerTests
{
    private readonly Mock<ILogger<RoleCreatedEventHandler>> _loggerMock;
    private readonly Mock<IAccountRepository> _accountRepoMock;
    private readonly Mock<IRoleRepository> _repoMock;
    private readonly Mock<IContactRepository> _contactRepoMock;
    private readonly Mock<IPennylaneApiGroupService> _pennylaneServiceMock;
    private readonly Mock<IPennylaneRepository> _pennylaneRepoMock;
    private readonly RoleCreatedEventHandler _handler;
    private readonly Fixture _fixture;

    public RoleCreatedEventHandlerTests()
    {
        _loggerMock = new Mock<ILogger<RoleCreatedEventHandler>>();
        _accountRepoMock = new Mock<IAccountRepository>(MockBehavior.Strict);
        _repoMock = new Mock<IRoleRepository>(MockBehavior.Strict);
        _contactRepoMock = new Mock<IContactRepository>(MockBehavior.Strict);
        _pennylaneServiceMock = new Mock<IPennylaneApiGroupService>(MockBehavior.Strict);
        _pennylaneRepoMock = new Mock<IPennylaneRepository>();
        var options = Options.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            AuthenticationMode = "auth",
            CollaboratorAuthenticationMode = "collabAuth",
            DefaultRole = "default",
            SupportedSaasPlans = "plan"
        });
        _handler = new RoleCreatedEventHandler(_loggerMock.Object, _repoMock.Object, _accountRepoMock.Object, _contactRepoMock.Object, _pennylaneServiceMock.Object, _pennylaneRepoMock.Object, options);
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_DoesNotCallAddRole(string? message)
    {
        // Act
        await _handler.HandleAsync(message!);

        // Assert
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ThrowsJsonException_AndDoesNotCallAddRole()
    {
        // Arrange
        var invalidJson = "not a json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MissingData_DoesNotCallAddRole()
    {
        // Arrange
        var wrapper = new { EventType = nameof(RoleCreatedEvent), Data = (RoleCreatedEventData)null! };
        var message = JsonSerializer.Serialize(wrapper);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task HandleAsync_InvalidContactOrAccount_DoesNotCallAddRole(int invalidContactId)
    {
        // Arrange: contactId invalid (<=0)
        var data1 = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.ContactId, invalidContactId)
            .Create();
        var wrapper1 = new RoleCreatedEvent(data1);
        var msg1 = JsonSerializer.Serialize(wrapper1);

        // Arrange: accountId invalid (< -1)
        var data2 = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.ContactId, _fixture.Create<int>() + 1)
            .With(d => d.AccountId, -2) // invalid (less than -1)
            .Create();
        var wrapper2 = new RoleCreatedEvent(data2);
        var msg2 = JsonSerializer.Serialize(wrapper2);

        // Act
        await _handler.HandleAsync(msg1);
        await _handler.HandleAsync(msg2);

        // Assert
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_CallsAddRoleOnce_WithMappedEntity()
    {
        // Arrange
        var eventData = _fixture.Create<RoleCreatedEventData>();
        var account = _fixture.Build<AccountEntity>().Create();
        var pennylanceCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().Create();
        var pennylaneUser = _fixture.Create<PennylaneUsers>();

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(account);
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(It.IsAny<string>())).ReturnsAsync(pennylanceCompany);
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, It.IsAny<int>())).ReturnsAsync(contact);
        _pennylaneServiceMock.Setup(p => p.TryGetUserAsync(It.IsAny<string>())).ReturnsAsync(pennylaneUser);
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(pennylaneUser);

        _repoMock
            .Setup(r => r.AddRoleAsync(
                It.Is<RoleEntity>(e =>
                    e.AccountId == eventData.AccountId &&
                    e.ContactId == eventData.ContactId &&
                    e.IsSignatory == eventData.IsSignatory
                    )))
            .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AddRoleThrows_PropagatesException()
    {
        var account = _fixture.Build<AccountEntity>().Create();
        var pennylanceCompany = _fixture.Build<PennylaneCompany>().Create();
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IsSignatory, _fixture.Create<bool>())
            .Create();

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(It.IsAny<string>())).ReturnsAsync(account);
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(It.IsAny<string>())).ReturnsAsync(pennylanceCompany);

        _repoMock
            .Setup(r => r.AddRoleAsync(
                It.Is<RoleEntity>(e => e.AccountId == eventData.AccountId)))
            .ThrowsAsync(new InvalidOperationException("db error"));


        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(message));
        _repoMock.Verify(r => r.AddRoleAsync(It.IsAny<RoleEntity>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_ContactDoesntExistsOnPennlyane_Succeeds()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();
        var account = _fixture.Build<AccountEntity>().With(a => a.AccountId, eventData.AccountId).Create();
        var pennylaneCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().With(a => a.ContactId, eventData.ContactId)
            .With(a => a.Type, "Collaborator").Create();

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(account.AccountId.ToString()))
            .ReturnsAsync(account).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(account.AccountNumber))
            .ReturnsAsync(pennylaneCompany).Verifiable();
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, eventData.ContactId)).ReturnsAsync(contact)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetUserAsync(contact.Email)).ReturnsAsync((PennylaneUsers?)null)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyUserAsync(contact.Email, pennylaneCompany.Id))
            .ReturnsAsync((PennylaneUsers?)null).Verifiable();
        _pennylaneServiceMock.Setup(p => p.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync(eventData.ContactId.ToString()).Verifiable();

        _pennylaneServiceMock.Setup(p => p.TryGrantCompanyUserAccessAsync(pennylaneCompany.Id,
            It.IsAny<GrantUserAccess>())).ReturnsAsync(It.IsAny<int?>()).Verifiable();

        _repoMock
            .Setup(r => r.AddRoleAsync(
                It.Is<RoleEntity>(e =>
                    e.AccountId == eventData.AccountId &&
                    e.ContactId == eventData.ContactId &&
                    e.IsSignatory == eventData.IsSignatory
                )))
            .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_ContactExistsOnPennlyaneOnCompany_Succeeds()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();
        var account = _fixture.Build<AccountEntity>().With(a => a.AccountId, eventData.AccountId).Create();
        var pennylaneCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().With(a => a.ContactId, eventData.ContactId)
            .With(a => a.Type, "Collaborator").Create();
        var pennylaneUser = _fixture.Build<PennylaneUsers>().With(a => a.Email, contact.Email).Create();

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(account.AccountId.ToString()))
            .ReturnsAsync(account).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(account.AccountNumber))
            .ReturnsAsync(pennylaneCompany).Verifiable();
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, eventData.ContactId)).ReturnsAsync(contact)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetUserAsync(pennylaneUser.Email)).ReturnsAsync(pennylaneUser)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyUserAsync(contact.Email, pennylaneCompany.Id))
            .ReturnsAsync(pennylaneUser).Verifiable();

        _repoMock
            .Setup(r => r.AddRoleAsync(
                It.Is<RoleEntity>(e =>
                    e.AccountId == eventData.AccountId &&
                    e.ContactId == eventData.ContactId &&
                    e.IsSignatory == eventData.IsSignatory
                )))
            .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_ContactExistsOnPennlyaneButNotOnCompany_Succeeds()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();
        var account = _fixture.Build<AccountEntity>().With(a => a.AccountId, eventData.AccountId).Create();
        var pennylaneCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().With(a => a.ContactId, eventData.ContactId)
            .With(a => a.Type, "Collaborator").Create();

        var pennylaneUser = _fixture.Build<PennylaneUsers>().With(a => a.Email, contact.Email).Create();

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(account.AccountId.ToString()))
            .ReturnsAsync(account).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(account.AccountNumber))
            .ReturnsAsync(pennylaneCompany).Verifiable();
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, eventData.ContactId)).ReturnsAsync(contact)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetUserAsync(pennylaneUser.Email)).ReturnsAsync(pennylaneUser)
            .Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyUserAsync(contact.Email, pennylaneCompany.Id))
            .ReturnsAsync((PennylaneUsers?)null).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGrantCompanyUserAccessAsync(pennylaneCompany.Id,
            It.IsAny<GrantUserAccess>())).ReturnsAsync(It.IsAny<int?>()).Verifiable();

        _repoMock
            .Setup(r => r.AddRoleAsync(
                It.Is<RoleEntity>(e =>
                    e.AccountId == eventData.AccountId &&
                    e.ContactId == eventData.ContactId &&
                    e.IsSignatory == eventData.IsSignatory
                )))
            .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_ContactIsNotCollaborator_DoesNothingOnPennylane()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();
        var account = _fixture.Build<AccountEntity>().With(a => a.AccountId, eventData.AccountId).Create();
        var pennylaneCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().With(a => a.ContactId, eventData.ContactId)
            .With(a => a.Type, "NotCollaborator").Create();


        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(account.AccountId.ToString()))
            .ReturnsAsync(account).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(account.AccountNumber))
            .ReturnsAsync(pennylaneCompany).Verifiable();
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, eventData.ContactId)).ReturnsAsync(contact)
            .Verifiable();

        _repoMock
           .Setup(r => r.AddRoleAsync(
               It.Is<RoleEntity>(e =>
                   e.AccountId == eventData.AccountId &&
                   e.ContactId == eventData.ContactId &&
                   e.IsSignatory == eventData.IsSignatory
               )))
           .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_ContactDoesntExistOnRydge_DoesNothingOnPennylane()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();
        var account = _fixture.Build<AccountEntity>().With(a => a.AccountId, eventData.AccountId).Create();
        var pennylaneCompany = _fixture.Build<PennylaneCompany>().Create();
        var contact = _fixture.Build<Contact>().With(a => a.ContactId, eventData.ContactId)
            .With(a => a.Type, "Collaborator").Create();


        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(account.AccountId.ToString()))
            .ReturnsAsync(account).Verifiable();
        _pennylaneServiceMock.Setup(p => p.TryGetCompanyByIdAsync(account.AccountNumber))
            .ReturnsAsync(pennylaneCompany).Verifiable();
        _contactRepoMock.Setup(c => c.GetContactByEmailOrIdAsync(null, eventData.ContactId)).ReturnsAsync((Contact?)null)
            .Verifiable();

        _repoMock
           .Setup(r => r.AddRoleAsync(
               It.Is<RoleEntity>(e =>
                   e.AccountId == eventData.AccountId &&
                   e.ContactId == eventData.ContactId &&
                   e.IsSignatory == eventData.IsSignatory
               )))
           .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [Fact]
    public async Task HandleAsync_When_IncludePennylaneAccess_And_AccountDoesntExistOnRydge_DoesNothingOnPennylane()
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, true)
            .Create();

        _accountRepoMock.Setup(a => a.GetAccountByNumberOrIdAsync(eventData.AccountId.ToString()))
            .ReturnsAsync((AccountEntity?)null).Verifiable();

        _repoMock
           .Setup(r => r.AddRoleAsync(
               It.Is<RoleEntity>(e =>
                   e.AccountId == eventData.AccountId &&
                   e.ContactId == eventData.ContactId &&
                   e.IsSignatory == eventData.IsSignatory
               )))
           .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);
        // Act
        await _handler.HandleAsync(message);

        // Assert
        Mock.VerifyAll(_accountRepoMock, _pennylaneServiceMock, _contactRepoMock);
    }

    [InlineData(null)]
    [InlineData(false)]
    [Theory]
    public async Task HandleAsync_When_IncludePennylaneAccessIsNull_Should_DoNothingOnPennylane(bool? includePennylaneAccess)
    {
        // Arrange
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.IncludePennylaneAccess, includePennylaneAccess)
            .Create();

        _repoMock
           .Setup(r => r.AddRoleAsync(
               It.Is<RoleEntity>(e =>
                   e.AccountId == eventData.AccountId &&
                   e.ContactId == eventData.ContactId &&
                   e.IsSignatory == eventData.IsSignatory
               )))
           .Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        // Act
        await _handler.HandleAsync(message);

        // Assert
        _accountRepoMock.Verify(r => r.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid role")]
    public async Task HandleAsynce_When_DataHasNoSubRole_Should_NotCreatePennylaneRole(string? subRole)
    {
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.SubRole, subRole)
            .Create();

        _repoMock.Setup(_repoMock => _repoMock.AddRoleAsync(It.IsAny<RoleEntity>())).Returns(Task.CompletedTask);

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        await _handler.HandleAsync(message);

        _pennylaneRepoMock.Verify(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _pennylaneRepoMock.Verify(r => r.DeletePennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
        _pennylaneRepoMock.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsynce_When_PennylaneRoleAlreadyExists_Should_NotCreatePennylaneRole()
    {
        var pnlRole = "executive";
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(d => d.SubRole, pnlRole)
            .Create();

        _repoMock.Setup(_repoMock => _repoMock.AddRoleAsync(It.IsAny<RoleEntity>())).Returns(Task.CompletedTask);

        _pennylaneRepoMock.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(eventData.AccountId, eventData.ContactId))
            .ReturnsAsync(new PnlRoleEntity
            {
                AccountId = eventData.AccountId,
                ContactId = eventData.ContactId,
                Role = pnlRole
            }).Verifiable();

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        await _handler.HandleAsync(message);

        _pennylaneRepoMock.Verify(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _pennylaneRepoMock.Verify(r => r.DeletePennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
        _pennylaneRepoMock.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsynce_When_PennylaneRoleIsDifferent_Should_DeleteExistingPennylaneRole_Then_CreateNewOne()
    {
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(r => r.SubRole, "employee")
            .Create();

        _repoMock.Setup(_repoMock => _repoMock.AddRoleAsync(It.IsAny<RoleEntity>())).Returns(Task.CompletedTask);

        _pennylaneRepoMock.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(eventData.AccountId, eventData.ContactId))
            .ReturnsAsync(_fixture.Create<PnlRoleEntity>()).Verifiable();

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        await _handler.HandleAsync(message);

        _pennylaneRepoMock.Verify(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _pennylaneRepoMock.Verify(r => r.DeletePennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Once);
        _pennylaneRepoMock.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsynce_When_PennylaneRoleNotExist_Should_CreatePennylaneRole()
    {
        var eventData = _fixture.Build<RoleCreatedEventData>()
            .With(r => r.SubRole, "sales_manager")
            .Create();

        _repoMock.Setup(_repoMock => _repoMock.AddRoleAsync(It.IsAny<RoleEntity>())).Returns(Task.CompletedTask);

        _pennylaneRepoMock.Setup(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(eventData.AccountId, eventData.ContactId))
            .ReturnsAsync((PnlRoleEntity) null!).Verifiable();

        var roleEvent = new RoleCreatedEvent(eventData);
        var message = JsonSerializer.Serialize(roleEvent);

        await _handler.HandleAsync(message);

        _pennylaneRepoMock.Verify(r => r.FetchPennylaneRoleByAccountAndContactIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _pennylaneRepoMock.Verify(r => r.DeletePennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Never);
        _pennylaneRepoMock.Verify(r => r.InsertPennylaneRoleAsync(It.IsAny<PnlRoleEntity>()), Times.Once);
    }
}
