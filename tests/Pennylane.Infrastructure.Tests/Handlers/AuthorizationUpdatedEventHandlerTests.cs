using Application.Interfaces;
using Application.Models.Contacts;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Application.StaticValues.Enum;
using Pennylane.Common.Tests;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Offers;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;
using OptionCreator = Microsoft.Extensions.Options.Options;

namespace Pennylane.Infrastructure.Tests.Handlers;

public class AuthorizationUpdatedEventHandlerTests : BaseTests
{
    private readonly Mock<ILogger<AuthorizationUpdatedEventHandler>> _loggerMock;
    private readonly Mock<ILogger<AuthorizationUpdatedEventManager>> _loggerManagerMock;
    private readonly Mock<IPennylaneApiGroupService> _serviceMock;
    private readonly Mock<IAccountRepository> _accountRepoMock;
    private readonly Mock<IContactRepository> _contactRepoMock;
    private readonly IOptions<PennylaneServiceOptions> _options;
    private readonly AuthorizationUpdatedEventHandler _handler;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepoMock;
    private readonly AuthorizationUpdatedEventManager _authorizationUpdateEventManager;
    private readonly IEnumerable<IPermissionHandler> _permissionHandlers;
    private readonly Fixture _fixture;

    public AuthorizationUpdatedEventHandlerTests()
    {
        _loggerMock = Mock.Initialize<ILogger<AuthorizationUpdatedEventHandler>>();
        _loggerManagerMock = Mock.Initialize<ILogger<AuthorizationUpdatedEventManager>>();
        _serviceMock = Mock.Initialize<IPennylaneApiGroupService>();
        _accountRepoMock = Mock.Initialize<IAccountRepository>();
        _contactRepoMock = Mock.Initialize<IContactRepository>();
        _subscriptionRepoMock = Mock.Initialize<ISubscriptionRepository>();
        _permissionHandlers = Enumerable.Empty<IPermissionHandler>();

        _options = OptionCreator.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "plan",
            EnableEventHandler = true,
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
        });

        _authorizationUpdateEventManager = new AuthorizationUpdatedEventManager(_loggerManagerMock.Object, _serviceMock.Object, _accountRepoMock.Object, _contactRepoMock.Object, _options);

        _handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            _authorizationUpdateEventManager,
            _permissionHandlers
        );


        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task HandleAsync_Account_Without_Pennylane_Subscription_stops_process()
    {
        // Arrange
        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 1,
            ContactId = 2,
            Codes = new List<string> { "SOME_CODE" }
        };
        var evt = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(evt);

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 1))
            .ReturnsAsync((SubscriptionEntity)null);

        // Act
        await _handler.HandleAsync(message);
        // Assert: no calls to service or other repos
        _serviceMock.Verify(s => s.GetCompanyUsersAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _serviceMock.Verify(s => s.GetUsersAsync(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceMessage_ThrowsBadRequestException(string message)
    {
        // JsonDefaults.TryDeserialize treats null, empty and whitespace uniformly (returns null
        // without throwing). Newtonsoft.Json's DeserializeObject used to throw ArgumentNullException
        // for a null string specifically (its internal StringReader rejects null), which is what this
        // test used to assert for that case; all three now fall through to the same "Data is null"
        // check and throw BadRequestException.
        await Assert.ThrowsAsync<BadRequestException>(() => _handler.HandleAsync(message));
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "not a valid json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => _handler.HandleAsync(invalidJson));
    }

    [Fact]
    public async Task HandleAsync_MissingData_ThrowsBadRequestException()
    {
        // Arrange
        var wrapper = new { EventType = "AuthorizationUpdatedEvent", Data = (BaseAuthorizationEventData)null! };
        var message = JsonSerializer.Serialize(wrapper);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _handler.HandleAsync(message));
    }

    [Fact]
    public async Task HandleAsync_Revocation_UserExists_CallsRevoke()
    {
        // Arrange
        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 10,
            ContactId = 20,
            Codes = new List<string> { "SOME_OTHER_CODE" }
        };
        var evt = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(evt);
        var authorizationUpdatedManager = new Mock<IAuthorizationUpdatedEventManager>();
        // accountRepo returns entity (use Fixture to set AccountId and AccountNumber)
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 10)
            .With(a => a.AccountNumber, "ACC123")
            .Create();
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync("10"))
            .ReturnsAsync(accountEntity);

        // contactRepo returns Contact (use Fixture to set ContactId and Email)
        var contactEntity = _fixture.Build<Contact>()
            .With(c => c.ContactId, 20)
            .With(c => c.Email, "user@example.com")
            .Create();
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 20))
            .ReturnsAsync(contactEntity);

        // service.GetCompanyUsersAsync returns pager with matching user
        var pager = new PennylaneUsersPager
        {
            Users = new List<PennylaneUsers>
                {
                    _fixture.Build<PennylaneUsers>()
                        .With(u => u.Email, "user@example.com")
                        .With(u => u.Id, "USER123")
                        .Create()
                },
            TotalPages = 1,
            CurrentPage = 1
        };
        _serviceMock
            .Setup(s => s.GetCompanyUsersAsync("ACC123", 1))
            .ReturnsAsync(pager);

        authorizationUpdatedManager.Setup(s => s.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        authorizationUpdatedManager.Setup(s => s.GetContactByIdAsync(contactEntity.ContactId)).ReturnsAsync(contactEntity);

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 10))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 10
            });

        var customHandler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            authorizationUpdatedManager.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        authorizationUpdatedManager.Setup(s => s.CheckIfUserExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((true, "ACC123"));

        // Act
        await customHandler.HandleAsync(message);

        // Assert: revoke called once
        authorizationUpdatedManager.Verify(s => s.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Authorization_NewGlobalUser_CreatesAndAssigns()
    {
        // Arrange
        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 30,
            ContactId = 31,
            Codes = new List<string> { PennylaneCodes.CLPEN001.ToString() }
        };
        var evt = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(evt);

        // accountRepo returns entity
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 30)
            .With(a => a.AccountNumber, "ACC789")
            .Create();
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync("30"))
            .ReturnsAsync(accountEntity);

        // contactRepo returns Contact
        var contactEntity = _fixture.Build<Contact>()
            .With(c => c.ContactId, 31)
            .With(c => c.Email, "newuser@example.com")
            .Create();
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 31))
            .ReturnsAsync(contactEntity);

        // COMPANY‐SCOPED pager must contain at least one dummy user (non‐matching email)
        var companyPager = new PennylaneUsersPager
        {
            Users = new List<PennylaneUsers>
                {
                    _fixture.Build<PennylaneUsers>()
                           .With(u => u.Email, "someone.else@company.com")
                           .With(u => u.Id, "DUMMY_COMP")
                           .Create()
                },
            TotalPages = 1,
            CurrentPage = 1
        };
        _serviceMock
            .Setup(s => s.GetCompanyUsersAsync("ACC789", 1))
            .ReturnsAsync(companyPager);

        // GLOBAL pager must also contain at least one dummy user (non‐matching email)
        var globalPager = new PennylaneUsersPager
        {
            Users = new List<PennylaneUsers>
                {
                    _fixture.Build<PennylaneUsers>()
                           .With(u => u.Email, "someone.else@global.com")
                           .With(u => u.Id, "DUMMY_GLOBAL")
                           .Create()
                },
            TotalPages = 1,
            CurrentPage = 1
        };
        _serviceMock
            .Setup(s => s.GetUsersAsync(1))
            .ReturnsAsync(globalPager);

        // Now that neither company nor global calls will throw, the handler can create & assign:
        _serviceMock
            .Setup(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync("NEWUSER123");

        _serviceMock
            .Setup(s => s.TryGrantCompanyUserAccessAsync(It.IsAny<string>(), It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(1);

        _subscriptionRepoMock.Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 30))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 30
            });

        // Act
        await _handler.HandleAsync(message);

        // Assert: ensure Create and Assign were called exactly once
        _serviceMock.Verify(
            s => s.PostUsersAsync(It.Is<PennylaneContact>(
                pc => pc.Email == "newuser@example.com")),
            Times.Once);

        _serviceMock.Verify(
            s => s.TryGrantCompanyUserAccessAsync(It.IsAny<string>(), It.IsAny<GrantUserAccess>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ServiceReturnsValidId_ReturnsThatId()
    {
        // Arrange
        // Build a “random” Contact with AutoFixture, then override only the Email/Name fields:
        var contact = _fixture.Build<Contact>()
                             .With(x => x.ContactId, 123)
                             .With(x => x.FirstName, "Jane")
                             .With(x => x.LastName, "Doe")
                             .With(x => x.Email, "jane.doe@example.com")
                             .Create();

        var authMethod = _fixture.Create<string>();
        var expectedUserId = _fixture.Create<string>();

        // Set up the service so that PostUsersAsync(...) returns our expectedUserId
        _serviceMock
            .Setup(s => s.PostUsersAsync(It.Is<PennylaneContact>(
                pc => pc.Email == contact.Email
                   && pc.FirstName == contact.FirstName
                   && pc.LastName == contact.LastName
                   && pc.SkipWelcomeEmail == true
                   && pc.AuthenticationMethod == authMethod)))
            .ReturnsAsync(expectedUserId);

        var helpers = new AuthorizationUpdatedEventManager(
                 NullLogger<AuthorizationUpdatedEventManager>.Instance,
                 _serviceMock.Object,
                 _accountRepoMock.Object,
                 _contactRepoMock.Object,
                 _options
             );
        // Act

        var result = await helpers.CreatePennylaneUserAsync(contact, authMethod);

        // Assert
        Assert.Equal(expectedUserId, result);
        _serviceMock.Verify(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()), Times.Once);
    }

    [Fact]
    public async Task CreatePennylaneUserAsync_ServiceReturnsEmpty_ThrowsBadRequestException()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
                             .With(x => x.ContactId, 456)
                             .With(x => x.FirstName, "John")
                             .With(x => x.LastName, "Smith")
                             .With(x => x.Email, "john.smith@example.com")
                             .Create();

        var authMethod = _fixture.Create<string>();

        // Make the service return an empty string to simulate a failure
        _serviceMock
            .Setup(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync(string.Empty);

        var helpers = new AuthorizationUpdatedEventManager(
                NullLogger<AuthorizationUpdatedEventManager>.Instance,
                 _serviceMock.Object,
                 _accountRepoMock.Object,
                 _contactRepoMock.Object,
                 _options
             );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            helpers.CreatePennylaneUserAsync(contact, authMethod));

        Assert.Contains(contact.Email, ex.Message);
        _serviceMock.Verify(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Authorization_UsesMappedClientRole_WhenPlanMatches()
    {
        // Arrange
        var planName = "Gold";
        var mappedRole = "GoldRole";
        var defaultRole = "DefaultRole";
        var accountNumber = "99";

        var company = _fixture.Build<PennylaneCompany>()
            .With(x => x.Id, accountNumber)
            .With(x => x.SaasPlan, planName).Create();

        // build options with one PlansRoleAssignments entry
        var opts = OptionCreator.Create(new PennylaneServiceOptions
        {
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "plan",
            EnableEventHandler = true,
            DefaultRole = defaultRole,
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            Token = "Token",
            PlansRoleAssignments = new List<PennylanePlansRoleAssignmentOptions> {
                    new() { Plan = planName, ClientRole = mappedRole }
                }
        });

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            opts,
            _subscriptionRepoMock.Object,
            _authorizationUpdateEventManager,
            _permissionHandlers
        );

        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 99,
            ContactId = 100,
            Codes = new[] { PennylaneCodes.CLPEN001.ToString() }.ToList()
        };
        var wrapper = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(wrapper);

        // account lookup
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 99)
            .With(a => a.AccountNumber, accountNumber)
            .Create();
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync(accountNumber))
            .ReturnsAsync(accountEntity);

        // contact lookup
        var contactEntity = _fixture.Build<Contact>()
            .With(c => c.ContactId, 100)
            .With(c => c.Email, "foo@bar.com")
            .Create();
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 100))
            .ReturnsAsync(contactEntity);

        // no company‐scoped user but pager non‐empty to avoid NotFound
        _serviceMock
            .Setup(s => s.GetCompanyUsersAsync(accountNumber, 1))
            .ReturnsAsync(new PennylaneUsersPager
            {
                Users = new[] {
                _fixture.Build<PennylaneUsers>()
                       .With(u => u.Email, "someone@else.com")
                       .With(u => u.Id,    "X").Create()
                }.ToList(),
                TotalPages = 1,
                CurrentPage = 1
            });

        // no global‐scoped user, same trick
        _serviceMock
            .Setup(s => s.GetUsersAsync(1))
            .ReturnsAsync(new PennylaneUsersPager
            {
                Users = new[] {
                _fixture.Build<PennylaneUsers>()
                       .With(u => u.Email, "other@global.com")
                       .With(u => u.Id,    "Y").Create()
                }.ToList(),
                TotalPages = 1,
                CurrentPage = 1
            });


        // now exercise creation + grant
        _serviceMock
            .Setup(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()))
            .ReturnsAsync("NEWID");

        _serviceMock
            .Setup(s => s.GetCompanyByIdAsync(accountNumber))
            .ReturnsAsync(company);

        _serviceMock
            .Setup(s => s.TryGrantCompanyUserAccessAsync("99", It.IsAny<GrantUserAccess>()))
            .ReturnsAsync(1);

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 99))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 99
            });

        // Act
        await handler.HandleAsync(message);

        // Assert: should have used mappedRole, not defaultRole
        _serviceMock.Verify(s =>
            s.TryGrantCompanyUserAccessAsync(It.IsAny<string>(), It.IsAny<GrantUserAccess>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_Authorization_MissingDefaultRole_ThrowsBadRequestException()
    {
        // Arrange
        var opts = OptionCreator.Create(new PennylaneServiceOptions
        {
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "plan",
            EnableEventHandler = true,
            DefaultRole = "   ",               // blank → fail
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
            Token = "",
            PlansRoleAssignments = new List<PennylanePlansRoleAssignmentOptions>()
        });

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            opts,
            _subscriptionRepoMock.Object,
            _authorizationUpdateEventManager,
            _permissionHandlers
        );

        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 55,
            ContactId = 66,
            Codes = new[] { PennylaneCodes.CLPEN001.ToString() }.ToList()
        };
        var wrapper = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(wrapper);

        // account & contact lookups (with non‐empty pagers to avoid NotFound)
        _accountRepoMock.Setup(r => r.GetAccountByNumberOrIdAsync("55"))
            .ReturnsAsync(_fixture.Build<AccountEntity>()
                .With(a => a.AccountId, 55)
                .With(a => a.AccountNumber, "55").Create()
            );
        _contactRepoMock.Setup(r => r.GetContactByEmailOrIdAsync(null, 66))
            .ReturnsAsync(_fixture.Build<Contact>()
                .With(c => c.ContactId, 66)
                .With(c => c.Email, "x@x.com").Create()
            );

        _serviceMock.Setup(s => s.GetCompanyUsersAsync("55", 1))
            .ReturnsAsync(new PennylaneUsersPager
            {
                Users = new[] { new PennylaneUsers { Email = "a@b.com", Id = "1" } }.ToList(),
                TotalPages = 1,
                CurrentPage = 1
            });
        _serviceMock.Setup(s => s.GetUsersAsync(1))
            .ReturnsAsync(new PennylaneUsersPager
            {
                Users = new[] { new PennylaneUsers { Email = "c@d.com", Id = "2" } }.ToList(),
                TotalPages = 1,
                CurrentPage = 1
            });
        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 55))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 55
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.HandleAsync(message));
        Assert.Contains("Default role is not configured", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_Authorization_FeatureFlagDisbaled_Skips_RoleCreation()
    {
        // Arrange
        var options = OptionCreator.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "plan",
            EnableEventHandler = false,
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
        });

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            options,
            _subscriptionRepoMock.Object,
            _authorizationUpdateEventManager,
            _permissionHandlers
        );

        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 100,
            ContactId = 101,
            Codes = new List<string> { PennylaneCodes.CLPEN001.ToString() }
        };

        var evt = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");

        var message = JsonSerializer.Serialize(evt);

        _subscriptionRepoMock.Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 100))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 100
            });

        // Act

        await handler.HandleAsync(message);

        // Assert: no calls to service for creating or assigning user
        _serviceMock.Verify(s => s.PostUsersAsync(It.IsAny<PennylaneContact>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Authorization_FeatureFlagDisbale_DoseNotSkip_RoleDeletion()
    {
        // Arrange

        var options = OptionCreator.Create(new PennylaneServiceOptions
        {
            Token = "token",
            ApiKey = "yolo",
            ApiUrl = "http://yolo.com",
            ApiFirmUrl = "http://firm-url.com",
            SupportedSaasPlans = "plan",
            EnableEventHandler = false,
            DefaultRole = "UserRole",
            AuthenticationMode = "AuthMode",
            CollaboratorAuthenticationMode = "CollabAuthMode",
        });

        var eventData = new BaseAuthorizationEventData
        {
            AccountId = 10,
            ContactId = 20,
            Codes = new List<string> { "SOME_OTHER_CODE" }
        };
        var evt = new AuthorizationUpdatedEvent(eventData, "AuthorizationUpdatedEvent");
        var message = JsonSerializer.Serialize(evt);
        var authorizationUpdatedManager = new Mock<IAuthorizationUpdatedEventManager>();
        // accountRepo returns entity (use Fixture to set AccountId and AccountNumber)
        var accountEntity = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 10)
            .With(a => a.AccountNumber, "ACC123")
            .Create();
        _accountRepoMock
            .Setup(r => r.GetAccountByNumberOrIdAsync("10"))
            .ReturnsAsync(accountEntity);

        // contactRepo returns Contact (use Fixture to set ContactId and Email)
        var contactEntity = _fixture.Build<Contact>()
            .With(c => c.ContactId, 20)
            .With(c => c.Email, "user@example.com")
            .Create();
        _contactRepoMock
            .Setup(r => r.GetContactByEmailOrIdAsync(null, 20))
            .ReturnsAsync(contactEntity);

        // service.GetCompanyUsersAsync returns pager with matching user
        var pager = new PennylaneUsersPager
        {
            Users = new List<PennylaneUsers>
                {
                    _fixture.Build<PennylaneUsers>()
                        .With(u => u.Email, "user@example.com")
                        .With(u => u.Id, "USER123")
                        .Create()
                },
            TotalPages = 1,
            CurrentPage = 1
        };
        _serviceMock
            .Setup(s => s.GetCompanyUsersAsync("ACC123", 1))
            .ReturnsAsync(pager);

        authorizationUpdatedManager.Setup(s => s.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        authorizationUpdatedManager.Setup(s => s.GetContactByIdAsync(contactEntity.ContactId)).ReturnsAsync(contactEntity);

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", 10))
            .ReturnsAsync(new SubscriptionEntity
            {
                OfferName = "Pennylane",
                AccountId = 10
            });

        var customHandler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            options,
            _subscriptionRepoMock.Object,
            authorizationUpdatedManager.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        authorizationUpdatedManager.Setup(s => s.CheckIfUserExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((true, "ACC123"));

        // Act
        await customHandler.HandleAsync(message);

        // Assert: revoke called once
        authorizationUpdatedManager.Verify(s => s.RevokeAccessUserCompanyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceAuthFails_WrapsAndThrowsBadRequestException()
    {
        // Arrange
        var accountId = 50;
        var contactId = 60;
        var evtData = new BaseAuthorizationEventData
        {
            AccountId = accountId,
            ContactId = contactId,
            Codes = new List<string> { PennylaneCodes.CLPEN001.ToString() }
        };
        var message = JsonSerializer.Serialize(new AuthorizationUpdatedEvent(evtData, "AuthorizationUpdatedEvent"));

        // subscription present
        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", accountId))
            .ReturnsAsync(new SubscriptionEntity { OfferName = "Pennylane", AccountId = accountId });

        // stub auth-manager to throw ServiceAuthenticationException
        var authMgr = new Mock<IAuthorizationUpdatedEventManager>();
        authMgr
            .Setup(m => m.GetAccountNumberByAccountIdAsync(accountId))
            .ReturnsAsync("ACC50");
        authMgr
            .Setup(m => m.GetContactByIdAsync(contactId))
            .ReturnsAsync(_fixture.Create<Contact>());
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), "ACC50"))
            .ThrowsAsync(new ServiceAuthenticationException("oops"));

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            authMgr.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.HandleAsync(message));
        Assert.Contains("Service authentication failed: oops", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenHttpRequestFails_WrapsAndThrowsBadRequestException()
    {
        // Arrange
        var accountId = 51;
        var contactId = 61;
        var evtData = new BaseAuthorizationEventData
        {
            AccountId = accountId,
            ContactId = contactId,
            Codes = new List<string> { PennylaneCodes.CLPEN001.ToString() }
        };
        var message = JsonSerializer.Serialize(new AuthorizationUpdatedEvent(evtData, "AuthorizationUpdatedEvent"));

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", accountId))
            .ReturnsAsync(new SubscriptionEntity { OfferName = "Pennylane", AccountId = accountId });

        var authMgr = new Mock<IAuthorizationUpdatedEventManager>();
        authMgr.Setup(m => m.GetAccountNumberByAccountIdAsync(accountId)).ReturnsAsync("ACC51");
        authMgr.Setup(m => m.GetContactByIdAsync(contactId)).ReturnsAsync(_fixture.Create<Contact>());
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), "ACC51"))
            .ThrowsAsync(new RequestHttpException(System.Net.HttpStatusCode.BadRequest, "fail"));

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            authMgr.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.HandleAsync(message));
        Assert.Contains("HTTP request failed: fail", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenNotFoundOccurs_WrapsAndThrowsNotFoundException()
    {
        // Arrange
        var accountId = 52;
        var contactId = 62;
        var evtData = new BaseAuthorizationEventData
        {
            AccountId = accountId,
            ContactId = contactId,
            Codes = new List<string> { PennylaneCodes.CLPEN001.ToString() }
        };
        var message = JsonSerializer.Serialize(new AuthorizationUpdatedEvent(evtData, "AuthorizationUpdatedEvent"));

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", accountId))
            .ReturnsAsync(new SubscriptionEntity { OfferName = "Pennylane", AccountId = accountId });

        var authMgr = new Mock<IAuthorizationUpdatedEventManager>();
        authMgr.Setup(m => m.GetAccountNumberByAccountIdAsync(accountId)).ReturnsAsync("ACC52");
        authMgr.Setup(m => m.GetContactByIdAsync(contactId)).ReturnsAsync(_fixture.Create<Contact>());
        authMgr
            .Setup(m => m.CheckIfUserExistsAsync(It.IsAny<string>(), "ACC52"))
            .ThrowsAsync(new NotFoundException("NF_CODE", "not found"));

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            authMgr.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(message));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_Revocation_WhenUserIdEmpty_ThrowsBadRequestException()
    {
        // Arrange
        var accountId = 70;
        var contactId = 80;
        var evtData = new BaseAuthorizationEventData
        {
            AccountId = accountId,
            ContactId = contactId,
            Codes = new List<string> { "OTHER_CODE" }   // revocation path
        };
        var message = JsonSerializer.Serialize(new AuthorizationUpdatedEvent(evtData, "AuthorizationUpdatedEvent"));

        _subscriptionRepoMock
            .Setup(r => r.GetSubscriptionByOfferAndAccountAsync("Pennylane", accountId))
            .ReturnsAsync(new SubscriptionEntity { OfferName = "Pennylane", AccountId = accountId });

        var authMgr = new Mock<IAuthorizationUpdatedEventManager>();
        authMgr.Setup(m => m.GetAccountNumberByAccountIdAsync(accountId)).ReturnsAsync("ACC70");
        var contact = _fixture.Build<Contact>()
                             .With(c => c.ContactId, contactId)
                             .With(c => c.Email, "noid@example.com")
                             .Create();
        authMgr.Setup(m => m.GetContactByIdAsync(contactId)).ReturnsAsync(contact);
        // simulate hasExisting=true but empty ID
        authMgr.Setup(m => m.CheckIfUserExistsAsync(contact.Email, "ACC70"))
               .ReturnsAsync((true, string.Empty));

        var handler = new AuthorizationUpdatedEventHandler(
            _loggerMock.Object,
            _serviceMock.Object,
            _accountRepoMock.Object,
            _contactRepoMock.Object,
            _options,
            _subscriptionRepoMock.Object,
            authMgr.Object,
            Enumerable.Empty<IPermissionHandler>()
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.HandleAsync(message));
        Assert.Contains("User ID is null or empty for email: noid@example.com", ex.Message);
    }
}
