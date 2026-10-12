using Application.Interfaces;
using Application.Models.Contacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Domain.Entities;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Application.Managers;

public class AuthorizationUpdatedEventManager(ILogger<AuthorizationUpdatedEventManager> logger,
                                              IPennylaneApiGroupService pennylaneServiceApi,
                                              IAccountRepository accountRepository,
                                              IContactRepository contactRepository,
                                              IOptions<PennylaneServiceOptions> options) : IAuthorizationUpdatedEventManager
{
    private readonly PennylaneServiceOptions _options = options.Value;

    public async Task AssignUserAccessAsync(string accountNumber, Contact contact, string roleWhenCreatedContact, string? pennylaneUserId = null)
    {
        var grantUserAccess = new GrantUserAccess
        {
            Role = roleWhenCreatedContact
        };

        // Feature flag logic: if UseUserIdForGrantAccess is true, use user_id (legacy), otherwise use user_email (new)
        string userIdentifier;
        string identifierType;
        if (_options.UseUserIdForGrantAccess)
        {
            if (string.IsNullOrWhiteSpace(pennylaneUserId))
            {
                throw new ArgumentException("pennylaneUserId is required when UseUserIdForGrantAccess is enabled.", nameof(pennylaneUserId));
            }
            grantUserAccess.UserId = pennylaneUserId;
            userIdentifier = pennylaneUserId;
            identifierType = "UserId";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                throw new ArgumentException("contact.Email is required when UseUserIdForGrantAccess is disabled.", nameof(contact));
            }
            grantUserAccess.UserEmail = contact.Email;
            userIdentifier = contact.Email;
            identifierType = "UserEmail";
        }

        logger.LogInformation("[AuthorizationUpdatedEventManager] Granting company access using {IdentifierType}: {UserIdentifier}", identifierType, userIdentifier);

        var accessId = await pennylaneServiceApi.TryGrantCompanyUserAccessAsync(accountNumber, grantUserAccess);

        if (accessId <= 0)
        {
            logger.LogError("[AuthorizationUpdatedEventHandler] Failed to grant access to user with identifier: {UserIdentifier}", userIdentifier);
            throw new BadRequestException(nameof(PennylaneUsers), message: $"Failed to grant access to user with identifier: {userIdentifier}");
        }

        logger.LogInformation("[AuthorizationUpdatedEventHandler] Access granted to user with identifier: {UserIdentifier}", userIdentifier);
    }

    public async Task<string> CreatePennylaneUserAsync(Contact contact, string authenticationMethod)
    {
        PennylaneContact pennylaneContact = contact.ToPennylaneContact(authenticationMethod);

        var userId = await pennylaneServiceApi.PostUsersAsync(pennylaneContact);
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogError("[AuthorizationUpdatedEventHandler] Failed to create user with email: {Email}", contact.Email);
            throw new BadRequestException(nameof(PennylaneUsers), message: $"Failed to create user with email: {contact.Email}");
        }

        return userId;
    }

    public async Task<(string? UserId, bool AlreadyExists)> TryCreatePennylaneUserAsync(Contact contact, string authenticationMethod)
    {
        PennylaneContact pennylaneContact = contact.ToPennylaneContact(authenticationMethod);

        var (userId, alreadyExists) = await pennylaneServiceApi.TryPostUsersAsync(pennylaneContact);

        if (string.IsNullOrEmpty(userId) && alreadyExists)
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] User creation returned null for email: {Email}. User already exists.", contact.Email);
        }
        else if (string.IsNullOrEmpty(userId) && !alreadyExists)
        {
            logger.LogWarning("[AuthorizationUpdatedEventHandler] User creation failed for email: {Email}.", contact.Email);
        }

        return (userId, alreadyExists);
    }

    public async Task<(bool hasExistingUser, string? pennylaneUserId)> CheckIfUserExistsAsync(string contactEmail, string? accountNumber = default)
    {
        PennylaneUsers? user = string.IsNullOrEmpty(accountNumber) ? await pennylaneServiceApi.TryGetUserAsync(contactEmail): await pennylaneServiceApi.TryGetCompanyUserAsync(contactEmail, accountNumber);
        return (user is not null, user?.Id);
    }

    public async Task<string> GetAccountNumberByAccountIdAsync(int accountId)
    {
        var account = await accountRepository.GetAccountByNumberOrIdAsync(accountId.ToString());
        if (account == null)
        {
            throw new NotFoundException(nameof(AccountEntity), accountId.ToString());
        }

        if (string.IsNullOrWhiteSpace(account.AccountNumber))
        {
            throw new BadRequestException(nameof(AccountEntity), message: $"Account with ID {accountId} does not have a valid account number.");
        }

        return account.AccountNumber;
    }

    public async Task<Contact> GetContactByIdAsync(int? contactId)
    {
        if (contactId == null || contactId <= 0)
        {
            throw new BadRequestException(nameof(Contact), message: "Contact ID is null or invalid.");
        }

        Contact? contact = await contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);
        if (contact == null)
        {
            throw new NotFoundException(nameof(Contact), contactId.ToString());
        }

        return contact;
    }

    public async Task RevokeAccessUserCompanyAsync(string companyId, string userId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            throw new BadRequestException(nameof(PennylaneUsers), message: "Company ID cannot be null or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new BadRequestException(nameof(PennylaneUsers), message: "User ID must be greater than zero.");
        }

        await pennylaneServiceApi.RevokeAccessUserCompanyAsync(companyId, userId);
        logger.LogInformation("[AuthorizationUpdatedEventHelpers] Access revoked for user with ID: {UserId} in company: {CompanyId}", userId, companyId);
    }
}