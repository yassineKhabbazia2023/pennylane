using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.StaticValues.Enum;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Handlers;

public class AuthorizationUpdatedEventHandler(ILogger<AuthorizationUpdatedEventHandler> logger,
                                              IPennylaneApiGroupService pennylaneServiceApi,
                                              IAccountRepository accountRepository,
                                              IContactRepository contactRepository,
                                              IOptions<PennylaneServiceOptions> options,
                                              ISubscriptionRepository subscriptionRepository,
                                              IAuthorizationUpdatedEventManager authorizationUpdatedEventManager,
                                              IEnumerable<IPermissionHandler> permissionHandlers) : IEventHandler
{
    private readonly PennylaneServiceOptions pennylaneServiceOptions = options.Value;

    public async Task HandleAsync(string message)
    {
        ArgumentNullException.ThrowIfNull(pennylaneServiceOptions);

        if (string.IsNullOrWhiteSpace(message))
        {
            logger.LogError($"[AuthorizationUpdatedEventHandler]: {nameof(AuthorizationUpdatedEvent)} [ERROR]: Message Body Is Empty");
        }

        var authorizationUpdatedEvent = JsonDefaults.TryDeserialize<AuthorizationUpdatedEvent>(message);
        logger.LogInformation("[AuthorizationUpdatedEventHandler] Event type: {EventType}", authorizationUpdatedEvent?.EventType);

        if (authorizationUpdatedEvent?.Data == null)
        {
            logger.LogError($"[AuthorizationUpdatedEventHandler]: {nameof(AuthorizationUpdatedEvent)} [ERROR]: Data is Null Or Empty");
            throw new BadRequestException(nameof(AuthorizationUpdatedEvent), message: $"[AuthorizationUpdatedEventHandler]: {nameof(AuthorizationUpdatedEvent)} [ERROR]: Data is Null Or Empty");
        }

        var subscription = await subscriptionRepository.GetSubscriptionByOfferAndAccountAsync(
            OfferCodes.Pennylane,
            authorizationUpdatedEvent.Data.AccountId);

        if (subscription is null)
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] No pennylane subscription found for account ID: {AccountId}. No action taken.", authorizationUpdatedEvent.Data.AccountId);
            return;
        }

        try
        {
            if (authorizationUpdatedEvent.Data.Codes.Contains(PennylaneCodes.CLPEN001.ToString()))
            {
                if (!pennylaneServiceOptions.EnableEventHandler)
                {
                    logger.LogInformation("[AuthorizationUpdatedEventHandler] Event handling fro creation is disabled in configuration.");
                    return;
                }

                await HandleUserAuthorizationAsync(authorizationUpdatedEvent.Data);
            }
            else
            {
                await HandleUserRevocationAsync(authorizationUpdatedEvent.Data);
            }

            await HandlePermissionsAsync(authorizationUpdatedEvent.Data);
        }
        catch (ServiceAuthenticationException ex)
        {
            logger.LogError(ex, "[AuthorizationUpdatedEventHandler] Service authentication failed: {Message}", ex.Message);
            throw new BadRequestException(nameof(PennylaneUsers), message: $"Service authentication failed: {ex.Message}");
        }
        catch (RequestHttpException ex)
        {
            logger.LogError(ex, "[AuthorizationUpdatedEventHandler] HTTP request failed: {Message}", ex.Message);
            throw new BadRequestException(nameof(PennylaneUsers), message: $"HTTP request failed: {ex.Message}");
        }
        catch (NotFoundException ex)
        {
            logger.LogError(ex, "[AuthorizationUpdatedEventHandler] Not found: {Message}", ex.Message);
            throw new NotFoundException(nameof(PennylaneUsers), message: ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AuthorizationUpdatedEventHandler] An error occurred while processing the event: {Message} with error {Error}", ex.Message, ex.InnerException?.ToString());
            throw;
        }
    }

    private async Task HandleUserRevocationAsync(BaseAuthorizationEventData authorizationEventData)
    {
        var accountNumber = await authorizationUpdatedEventManager.GetAccountNumberByAccountIdAsync(authorizationEventData.AccountId);
        var contact = await authorizationUpdatedEventManager.GetContactByIdAsync(authorizationEventData.ContactId);
        var (hasExistingUser, pennylaneUserId) = await authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email, accountNumber);

        if (hasExistingUser)
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] [HandleUserRevocationAsync] User already exists with email: {Email} in company {AccountNumber}", contact.Email, accountNumber);

            if (string.IsNullOrEmpty(pennylaneUserId))
            {
                logger.LogError("[AuthorizationUpdatedEventHandler] [HandleUserRevocationAsync] User ID is null or empty for email: {Email}", contact.Email);
                throw new BadRequestException(nameof(PennylaneUsers), message: $"User ID is null or empty for email: {contact.Email}");
            }

            await authorizationUpdatedEventManager.RevokeAccessUserCompanyAsync(accountNumber, pennylaneUserId);
        }
        else
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] [HandleUserRevocationAsync] No user found with email: {Email} in company {AccountNumber}", contact.Email, accountNumber);
        }
    }

    private async Task HandleUserAuthorizationAsync(BaseAuthorizationEventData authorizationEventData)
    {
        var accountNumber = await authorizationUpdatedEventManager.GetAccountNumberByAccountIdAsync(authorizationEventData.AccountId);
        var contact = await authorizationUpdatedEventManager.GetContactByIdAsync(authorizationEventData.ContactId);
        var (hasExistingUser, _) = await authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email, accountNumber);

        if (hasExistingUser)
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] [HandleUserAuthorizationAsync] User already exists with email: {Email} in company {AccountNumber}", contact.Email, accountNumber);
            return;
        }

        var (hasExistingGlobalUser, pennylaneUserId) = await authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);
        string clientRole = await GetClientRole(accountNumber, pennylaneServiceOptions);

        if (hasExistingGlobalUser && !string.IsNullOrEmpty(pennylaneUserId))
        {
            logger.LogInformation("[AuthorizationUpdatedEventHandler] [HandleUserAuthorizationAsync] Global user already exists with email: {Email} with ID: {PennylaneUserId}", contact.Email, pennylaneUserId);
            await authorizationUpdatedEventManager.AssignUserAccessAsync(accountNumber, contact, clientRole, pennylaneUserId);
            return;
        }

        ArgumentNullException.ThrowIfNullOrWhiteSpace(pennylaneServiceOptions.AuthenticationMode);
        var userId = await authorizationUpdatedEventManager.CreatePennylaneUserAsync(contact, pennylaneServiceOptions.AuthenticationMode);
        await authorizationUpdatedEventManager.AssignUserAccessAsync(accountNumber, contact, clientRole, userId);
    }

    private async Task<string> GetClientRole(string accountNumber, PennylaneServiceOptions pennylaneServiceOptions)
    {
        if (string.IsNullOrWhiteSpace(pennylaneServiceOptions.DefaultRole))
        {
            logger.LogError("[AuthorizationUpdatedEventHandler] Default role is not configured in PennylaneServiceOptions.");
            throw new BadRequestException(nameof(PennylaneUsers), message: "Default role is not configured in PennylaneServiceOptions.");
        }

        if (pennylaneServiceOptions.PlansRoleAssignments == null)
        {
            logger.LogError("[AuthorizationUpdatedEventHandler] PlansRoleAssignments is not configured in PennylaneServiceOptions.");
            throw new BadRequestException(nameof(PennylaneUsers), message: "PlansRoleAssignments is not configured in PennylaneServiceOptions.");
        }

        if (!pennylaneServiceOptions.PlansRoleAssignments.Any())
        {
            return pennylaneServiceOptions.DefaultRole;
        }

        var company = await pennylaneServiceApi.GetCompanyByIdAsync(accountNumber);

        var rolesMapping = pennylaneServiceOptions.PlansRoleAssignments.FirstOrDefault(x => x.Plan.Equals(company.SaasPlan, StringComparison.OrdinalIgnoreCase));

        if(rolesMapping != null && !string.IsNullOrWhiteSpace(rolesMapping.ClientRole)){ 
            return rolesMapping.ClientRole;
        }

        return pennylaneServiceOptions.DefaultRole;
    }

    private async Task HandlePermissionsAsync(BaseAuthorizationEventData authorizationEventData)
    {
        int contactId = authorizationEventData.ContactId ?? 0;
        int accountId = authorizationEventData.AccountId;

        if (contactId <= 0 || accountId <= 0)
        {
            logger.LogWarning("[AuthorizationUpdatedEventHandler] [HandlePermissionsAsync] ContactId or AccountId is invalid, skipping permissions handling");
            return;
        }

        foreach (var handler in permissionHandlers)
        {
            foreach (var permissionCode in handler.PermissionCodes)
            {
                if (authorizationEventData.Codes.Contains(permissionCode))
                {
                    await handler.HandlePermissionGrantedAsync(permissionCode, contactId, accountId);
                }
                else
                {
                    await handler.HandlePermissionRevokedAsync(permissionCode, contactId, accountId);
                }
            }
        }
    }
}