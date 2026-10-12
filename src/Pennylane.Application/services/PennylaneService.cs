// <copyright file="PennylaneService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Models.Contacts;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pennylane;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.WebApi.Models;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Application.services;

public class PennylaneService(IAccountRepository accountRepository,
    IContactRepository contactRepository,
    ICompanyRepository companyRepository,
    IPennylaneRepository pennylaneRepository,
    ILogger<PennylaneService> logger,
    IAuthorizationUpdatedEventManager authorizationUpdatedEventManager,
    IOptions<PennylaneServiceOptions> options,
    IOptions<PennylaneFirmTokenOptions> pennylaneFirmTokenOptions,
    IPennylaneApiGroupService pennylaneServiceApi,
    IPennylaneApiFirmService pennylaneApiFirmService
        ) : IPennylaneService
{
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IContactRepository _contactRepository = contactRepository;
    private readonly IPennylaneRepository _pennylaneRepository = pennylaneRepository;
    private readonly ILogger<PennylaneService> _logger = logger;
    private readonly IAuthorizationUpdatedEventManager _authorizationUpdatedEventManager = authorizationUpdatedEventManager;
    private readonly PennylaneServiceOptions _options = options.Value;
    private readonly IPennylaneApiGroupService _pennylaneServiceApi = pennylaneServiceApi;
    private readonly PennylaneFirmTokenOptions pennylaneFirmTokenItems = pennylaneFirmTokenOptions.Value;

    public async Task<PnlRole?> GetPennylaneRoleAsync(int contactId, int accountId)
    {
        var role = await _pennylaneRepository.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId);

        return role?.MapPnlRoleModelToEntity();
    }

    public async Task InsertPennylaneRoleAsync(PnlRole roleToCreate)
    {
        // 1. Validation des prérequis avec bloc try/catch pour chaque accès critique
        AccountEntity? account;
        try
        {
            account = await _accountRepository.GetAccountByNumberOrIdAsync(roleToCreate.AccountId.ToString());
            if (account is null)
                throw new NotFoundException(Errors.AccountNotFoundCode,
                    string.Format(Errors.AccountNotFoundMessage, roleToCreate.AccountId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PennylaneService] Account lookup failed for {AccountId}", roleToCreate.AccountId);
            throw;
        }

        Contact? contact;
        try
        {
            contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: roleToCreate.ContactId);
            if (contact is null)
                throw new NotFoundException(Errors.ContactNotFoundCode,
                    string.Format(Errors.ContactNotFoundMessage, roleToCreate.ContactId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PennylaneService] Contact lookup failed for {ContactId}", roleToCreate.ContactId);
            throw;
        }

        // 2. Vérifier si le rôle existe déjà en base locale
        PnlRoleEntity? pnlRole;
        try
        {
            pnlRole = await _pennylaneRepository.FetchPennylaneRoleByAccountAndContactIdAsync(
                roleToCreate.AccountId, roleToCreate.ContactId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PennylaneService] DB error on FetchPennylaneRole {AccountId} {ContactId}", roleToCreate.AccountId, roleToCreate.ContactId);
            throw;
        }

        if (pnlRole is not null)
        {
            await UpdatePennylaneRoleAsync(pnlRole, roleToCreate, contact, account.AccountNumber);
            return;
        }

        // 3. Créer/récupérer l'utilisateur et lui donner accès à l'entreprise
        var pennylaneUserId = await CreatePennylaneUserAsync(roleToCreate);

        // 4. Créer l'entrée en base locale
        try
        {
            var pnlRoleEntity = roleToCreate.MapPnlRoleModelToEntity();
            await _pennylaneRepository.InsertPennylaneRoleAsync(pnlRoleEntity);

            _logger.LogInformation(
                "Successfully inserted PnlRole for account {AccountId}, contact {ContactId}, and Pennylane user {PennylaneUserId}",
                roleToCreate.AccountId, roleToCreate.ContactId, pennylaneUserId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PennylaneService] InsertPennylaneRoleAsync failed for {AccountId} - {ContactId}", roleToCreate.AccountId, roleToCreate.ContactId);
            throw;
        }
    }

    public async Task<PennylaneEmails?> GetPennylaneEmailsAsync(string accountNumber)
    {
        var pennylaneCompany = await _pennylaneServiceApi.TryGetCompanyByIdAsync(accountNumber);
        if (pennylaneCompany is null) return null;
        return new PennylaneEmails(
            accountNumber,
            pennylaneCompany.CustomerInvoiceTransmissionEmails?.FirstOrDefault() ?? string.Empty,
            pennylaneCompany.SupplierInvoiceTransmissionEmails?.FirstOrDefault() ?? string.Empty);
    }

    public async Task<PennylaneCompany> GetCompanyByIdAsync(string companyId)
    {
        try
        {
            var company = await companyRepository.GetCompanyByIdWithRetentionAsync(companyId, _options.LastSyncDateRetentionDays);
            return company;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Company with ID {CompanyId} not found or last sync date is too old.", companyId);

            var company = await _pennylaneServiceApi.GetCompanyByIdAsync(companyId);
            await companyRepository.SaveCompaniesAsync([company]);
            return company;
        }
    }

    public async Task<int?> GetPennylaneInternalIdByAccountNumberAsync(string firmId, string accountNumber, bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var internalId = await companyRepository.GetPennylaneInternalIdByACompanyIdAsync(accountNumber);
            if (internalId != null)
            {
                return int.Parse(internalId);
            }
        }

        // If internal ID is not found in the repository, fetch it from the API
        var internalIdFromApi = await pennylaneApiFirmService.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber);

        // If the internal ID is found, save it to the repository for future use
        if (internalIdFromApi.HasValue)
        {
            await companyRepository.SavePennylaneInternalIdAsync(accountNumber, internalIdFromApi.Value);
        }

        return internalIdFromApi;
    }

    public async Task<PennylaneCompany> RefreshCompanyFromApiAsync(string companyId)
    {
        var company = await _pennylaneServiceApi.GetCompanyByIdAsync(companyId);
        await companyRepository.SaveCompaniesAsync([company]);
        return company;
    }

    private async Task<string?> CreatePennylaneUserAsync(PnlRole roleToCreate)
    {
        var accountNumber = await _authorizationUpdatedEventManager
            .GetAccountNumberByAccountIdAsync(roleToCreate.AccountId);
        var contact = await _authorizationUpdatedEventManager
            .GetContactByIdAsync(roleToCreate.ContactId);
        string clientRole = roleToCreate.Role;

        // Scénario 1️⃣ : User existe dans cette company
        var (hasAccessInCompany, existingUserId) = await _authorizationUpdatedEventManager
            .CheckIfUserExistsAsync(contact.Email, accountNumber);

        if (hasAccessInCompany && !string.IsNullOrEmpty(existingUserId))
        {
            _logger.LogInformation(
                "[PennylaneService] [CreatePennylaneUserAsync] User already exists with email: {Email} in company {AccountNumber}",
                contact.Email, accountNumber
            );
            return existingUserId;
        }

        // Tentative de création directe (plus de check global préalable)
        ArgumentNullException.ThrowIfNullOrWhiteSpace(_options.AuthenticationMode);
        try
        {
            var (createdUserId, alreadyExists) = await _authorizationUpdatedEventManager
                .TryCreatePennylaneUserAsync(contact, _options.AuthenticationMode);

            if (!string.IsNullOrEmpty(createdUserId))
            {
                // User was successfully created
                _logger.LogInformation("Successfully created user {Email} ({RoleName}) with Pennylane ID {PennylaneUserId}.", contact.Email, clientRole, createdUserId);
            }
            else if (alreadyExists)
            {
                // User already exists (already taken), proceed to grant access using email
                _logger.LogInformation("User already exists for {Email} ({RoleName}). Proceeding to grant access with email.", contact.Email, clientRole);
            }
            else
            {
                // User creation failed for another reason - do NOT continue to grant
                _logger.LogWarning("User creation failed for {Email} ({RoleName}). Skipping grant access.", contact.Email, clientRole);
                return null;
            }

            await _authorizationUpdatedEventManager.AssignUserAccessAsync(
                accountNumber, contact, clientRole, createdUserId
            );

            _logger.LogInformation(
                "[PennylaneService] [CreatePennylaneUserAsync] Successfully created user {Email} with ID: {UserId}",
                contact.Email, createdUserId
            );

             (hasAccessInCompany, existingUserId) = await _authorizationUpdatedEventManager
                 .CheckIfUserExistsAsync(contact.Email, accountNumber);

            return existingUserId;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("source_id must be unique"))
        {
            // En cas de duplicata ("source_id must be unique"), check global et assignation
            _logger.LogWarning(
                "[PennylaneService] [CreatePennylaneUserAsync] User creation failed with 'source_id must be unique' for {Email}. Retrying search...",
                contact.Email
            );

            try
            {
                await Task.Delay(500);
                var (existsAfterRetry, userIdAfterRetry) = await _authorizationUpdatedEventManager
                    .CheckIfUserExistsAsync(contact.Email, null);

                if (existsAfterRetry && !string.IsNullOrEmpty(userIdAfterRetry))
                {
                    _logger.LogInformation(
                        "[PennylaneService] [CreatePennylaneUserAsync] User {Email} found after retry with ID: {UserId}",
                        contact.Email, userIdAfterRetry
                    );

                    await _authorizationUpdatedEventManager.AssignUserAccessAsync(
                        accountNumber, contact, clientRole, userIdAfterRetry
                    );
                    return userIdAfterRetry;
                }

                _logger.LogError(ex,
                    "[PennylaneService] [CreatePennylaneUserAsync] User {Email} still not found after retry",
                    contact.Email
                );
                throw;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx,
                    "[PennylaneService] [CreatePennylaneUserAsync] Retry and user search failed for {Email}",
                    contact.Email
                );
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PennylaneService] [CreatePennylaneUserAsync] General failure for {Email}", contact.Email);
            throw;
        }
    }

    private async Task UpdatePennylaneRoleAsync(PnlRoleEntity existingRole, PnlRole roleToCreate, Contact contact, string accountNumber)
    {
        // Check if there are differences
        if (existingRole.Role != roleToCreate.Role)
        {
            // Get pennylane user ID and check if the user exists in Pennylane
            var (_, pennylaneUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);

            // Delete the existing role from pulse
            await _pennylaneRepository.DeletePennylaneRoleAsync(existingRole);

            // Delete the existing role from Pennylane
            if (pennylaneUserId != null)
            {
                await _authorizationUpdatedEventManager.RevokeAccessUserCompanyAsync(accountNumber, pennylaneUserId!);
            }

            // Create the new role in pulse
            var pnlNewRoleEntity = MapPnlRoles.MapPnlRoleModelToEntity(roleToCreate);
            if (pnlNewRoleEntity is null)
            {
                _logger.LogError("Failed to map PnlRole model to entity for account {AccountId} and contact {ContactId}.", roleToCreate.AccountId, roleToCreate.ContactId);
                return;
            }

            await _pennylaneRepository.InsertPennylaneRoleAsync(pnlNewRoleEntity!);

            // Assign the new role to the user in Pennylane
            await _authorizationUpdatedEventManager.AssignUserAccessAsync(accountNumber, contact, roleToCreate.Role, pennylaneUserId!);

            _logger.LogInformation("Successfully updated PnlRole for account {AccountId} and contact {ContactId}.", roleToCreate.AccountId, roleToCreate.ContactId);
        }
    }

    public async Task DeletePennylaneRoleAsync(int accountId, int contactId)
    {
        var accountEntity = await _accountRepository.GetAccountByNumberOrIdAsync(accountId.ToString());
        var contactEntity = await _contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);

        if (accountEntity == null)
        {
            _logger.LogError("Account with accountId {AccountId} was not found!", accountId);
            return;
        }

        if (contactEntity == null)
        {
            _logger.LogError("Contact with contactId {ContactId} was not found!", contactId);
            return;
        }

        var companyUsers = await _pennylaneServiceApi.GetCompanyUsersAsync(accountEntity.AccountNumber);
        string? userId = companyUsers.Users.FirstOrDefault(user => string.Equals(user.Email, contactEntity.Email, StringComparison.OrdinalIgnoreCase))?.Id;

        if (!string.IsNullOrEmpty(userId))
        {
            await _pennylaneServiceApi.RevokeAccessUserCompanyAsync(accountEntity.AccountNumber, userId);
        }
        else
        {
            _logger.LogWarning("The User {ContactEmail} does not exist in the company {AccountNumber}, skipping Pennylane API revocation", contactEntity.Email, accountEntity.AccountNumber);
        }

        var pnlRole = await _pennylaneRepository.FetchPennylaneRoleByAccountAndContactIdAsync(accountId, contactId);
        if (pnlRole != null)
        {
            await _pennylaneRepository.DeletePennylaneRoleAsync(pnlRole);
            _logger.LogInformation("PnlRole deleted for contact {ContactId} on account {AccountId}", contactId, accountId);
        }
    }

    /// <summary>
    /// Creates or retrieves a company in Pennylane (company management only, no user additions).
    /// </summary>
    public async Task<(PennylaneCompany? Company, bool AlreadyExists, bool CreatedOnDefault, bool RequiresFirmAssignment, bool RequiresFirmTransfer)> CreateCompanyOnlyAsync(CreateCompanyRequest createCompanyRequest)
    {
        var account = await _accountRepository.GetAccountByNumberOrIdAsync(createCompanyRequest.AccountId.ToString())
            ?? throw new NotFoundException(Errors.AccountNotFoundCode, string.Format(Errors.AccountNotFoundMessage, createCompanyRequest.AccountId.ToString()));

        // Check if company already exists in Pennylane
        var existingCompany = await _pennylaneServiceApi.TryGetCompanyByIdAsync(account.AccountNumber);
        if (existingCompany is not null)
        {
            _logger.LogInformation("Company {CompanyId} already exists in Pennylane.", existingCompany.Id);
            return (existingCompany, AlreadyExists: true, CreatedOnDefault: false, RequiresFirmAssignment: false, RequiresFirmTransfer: false);
        }

        var firmId = pennylaneFirmTokenItems?.FirstOrDefault()?.Id ?? string.Empty;
        var createdOnDefault = false;

        if (!string.IsNullOrWhiteSpace(createCompanyRequest.HubName))
        {
            var hubFirmId = await _pennylaneServiceApi.TryGetFirmIdByNameAsync(createCompanyRequest.HubName);
            if (!string.IsNullOrWhiteSpace(hubFirmId))
            {
                firmId = hubFirmId;
                _logger.LogInformation("Hub {HubName} found with firm ID {FirmId}.", createCompanyRequest.HubName, firmId);
            }
            else
            {
                createdOnDefault = true;
                _logger.LogInformation("Hub {HubName} not found. Using default firm ID {FirmId}.", createCompanyRequest.HubName, firmId);
            }
        }

        var userNumber = PennylaneInformationHelper.GetExpectedUserNumber(createCompanyRequest.NumberOfUsers, createCompanyRequest.RequestedPlanCode);
        int? numberOfEmployees = PennylaneInformationHelper.GetExpectedEmployeesNumber(userNumber, createCompanyRequest.RequestedPlanCode);

        // Create company in Pennylane via API with the provided Pennylane user IDs
        var createPennylaneCompanyRequest = new CreatePennylaneCompanyRequest
        {
            Id = account.AccountNumber,
            FirmId = firmId,
            Name = account.LegalName ?? string.Empty,
            CountryAlpha2 = createCompanyRequest.CountryCode ?? "FR",
            NotYetRegistered = createCompanyRequest.NotYetRegistered,
            AccountingType = createCompanyRequest.AccountingType,
            AccountingManagerId = createCompanyRequest.PennylaneAccountingManagerId,
            AccountingSupervisorId = createCompanyRequest.PennylaneAccountingSupervisorId,
            AccountantId = createCompanyRequest.PennylaneAccountantId,
            SaasPlan = PennylaneInformationHelper.GetExpectedSaasPlan(createCompanyRequest.RequestedPlanCode),
            NumberOfUsers = userNumber,
            NumberOfEmployees = numberOfEmployees,
        };

        if (!string.IsNullOrEmpty(createCompanyRequest.RegistrationNumber))
        {
            createPennylaneCompanyRequest.RegNo = createCompanyRequest.RegistrationNumber;
        }

        var result = await _pennylaneServiceApi.CreateCompanyAsync(createPennylaneCompanyRequest);

        if (result.RequiresFirmAssignment)
        {
            return (null, AlreadyExists: false, CreatedOnDefault: false, RequiresFirmAssignment: true, RequiresFirmTransfer: false);
        }

        if (result.RequiresFirmTransfer)
        {
            return (null, AlreadyExists: false, CreatedOnDefault: false, RequiresFirmAssignment: false, RequiresFirmTransfer: true);
        }

        if (result.RegNoAlreadyExists)
        {
            _logger.LogInformation(
                "Company creation failed because reg_no already exists. Returning already-exists without resolving a company id. RegNo: {RegNo}. Error: {Error}",
                createCompanyRequest.RegistrationNumber, result.ErrorContent);

            return (null, AlreadyExists: true, CreatedOnDefault: false, RequiresFirmAssignment: false, RequiresFirmTransfer: false);
        }

        if (!result.Success)
        {
            _logger.LogError(
                "Company creation failed for account {AccountNumber}. Error: {Error}",
                account.AccountNumber,
                result.ErrorContent);

            return (null, AlreadyExists: false, CreatedOnDefault: false, RequiresFirmAssignment: false, RequiresFirmTransfer: false);
        }

        _logger.LogInformation("Company created in Pennylane with ID {CompanyId}. Fetching full company data...", result.Response!.Id);

        // Fetch the complete company data from Pennylane after creation
        var company = await _pennylaneServiceApi.GetCompanyByIdAsync(result.Response!.Id);

        // Save the company to the local repository/cache for future lookups
        await companyRepository.SaveCompaniesAsync([company]);
        _logger.LogInformation("Successfully created and cached company {CompanyName} with ID {CompanyId}.", company.Name, company.Id);

        return (company, AlreadyExists: false, createdOnDefault, RequiresFirmAssignment: false, RequiresFirmTransfer: false);
    }

    /// <summary>
    /// Adds users to a Pennylane company based on the provided contact IDs.
    /// For each contact ID:
    /// - Retrieves the contact from the database to get its type (Collaborator or Customer)
    /// - Checks if the user already exists in the company
    /// - Creates the user in Pennylane if they don't exist globally
    /// - Grants access to the company with the appropriate role based on contact type
    /// </summary>
    /// <param name="accountNumber">The account number (company ID) in Pennylane.</param>
    /// <param name="contactIds">List of contact IDs to add to the company.</param>
    /// <returns>List of user addition results indicating success or failure for each contact.</returns>
    public async Task<List<UserAdditionResult>> AddUsersToCompanyAsync(string accountNumber, IReadOnlyList<int> contactIds)
    {
        var results = new List<UserAdditionResult>();

        if (contactIds == null || !contactIds.Any())
        {
            _logger.LogInformation("No contacts to add to company {AccountNumber}.", accountNumber);
            return results;
        }

        foreach (var contactId in contactIds)
        {
            try
            {
                // Get contact information from repository
                var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);
                if (contact is null)
                {
                    _logger.LogWarning("Contact with ID {ContactId} not found. Skipping user addition.", contactId);
                    results.Add(new UserAdditionResult
                    {
                        ContactId = contactId,
                        Email = "unknown",
                        Success = false,
                        Error = $"Contact with ID {contactId} not found in database"
                    });
                    continue;
                }

                // Check if user already exists in this company
                var (hasExistingUserInCompany, _) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email, accountNumber);
                if (hasExistingUserInCompany)
                {
                    _logger.LogInformation("User {Email} already exists in company {AccountNumber}. Skipping.", contact.Email, accountNumber);
                    results.Add(new UserAdditionResult
                    {
                        ContactId = contactId,
                        Email = contact.Email,
                        Success = true,
                        Error = null
                    });
                    continue;
                }

                // Check if user exists globally in Pennylane
                var (hasExistingGlobalUser, pennylaneUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);

                // Determine the role based on contact type from database
                string role = await DetermineUserRoleAsync(contact, accountNumber);

                if (hasExistingGlobalUser && !string.IsNullOrEmpty(pennylaneUserId))
                {
                    // User exists globally, just grant access to this company
                    _logger.LogInformation("Global user {Email} exists with ID {PennylaneUserId}. Granting access to company {AccountNumber}.", contact.Email, pennylaneUserId, accountNumber);
                    await _authorizationUpdatedEventManager.AssignUserAccessAsync(accountNumber, contact, role, pennylaneUserId);
                }
                else
                {
                    // User doesn't exist globally, create them first then grant access
                    _logger.LogInformation("Creating new user {Email} and granting access to company {AccountNumber}.", contact.Email, accountNumber);

                    // Use different authentication mode based on contact type
                    var isCollaborator = string.Equals(contact.Type, ContactType.collaborator.GetDescription(), StringComparison.OrdinalIgnoreCase);
                    var authenticationMode = isCollaborator ? _options.CollaboratorAuthenticationMode : _options.AuthenticationMode;
                    ArgumentNullException.ThrowIfNullOrWhiteSpace(authenticationMode);
                    var (createdUserId, alreadyExists) = await _authorizationUpdatedEventManager.TryCreatePennylaneUserAsync(contact, authenticationMode);

                    if (!string.IsNullOrEmpty(createdUserId))
                    {
                        // User was successfully created
                        _logger.LogInformation("Successfully created user {Email} ({RoleName}) with Pennylane ID {PennylaneUserId}.", contact.Email, role, createdUserId);
                    }
                    else if (alreadyExists)
                    {
                        // User already exists (already taken), proceed to grant access using email
                        _logger.LogInformation("User already exists for {Email} ({RoleName}). Proceeding to grant access with email.", contact.Email, role);
                    }
                    else
                    {
                        // User creation failed for another reason - do NOT continue to grant
                        _logger.LogWarning("User creation failed for {Email} ({RoleName}). Skipping grant access.", contact.Email, role);
                        results.Add(new UserAdditionResult
                        {
                            ContactId = contactId,
                            Email = contact.Email,
                            Success = false,
                            Error = "User creation failed in Pennylane"
                        });
                        continue;
                    }

                    await _authorizationUpdatedEventManager.AssignUserAccessAsync(accountNumber, contact, role, createdUserId);
                }

                _logger.LogInformation("Successfully added user {Email} to company {AccountNumber} with role {Role}.", contact.Email, accountNumber, role);
                results.Add(new UserAdditionResult
                {
                    ContactId = contactId,
                    Email = contact.Email,
                    Success = true,
                    Error = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add user with ContactId {ContactId} to company {AccountNumber}.", contactId, accountNumber);
                results.Add(new UserAdditionResult
                {
                    ContactId = contactId,
                    Email = "unknown",
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Determines the appropriate Pennylane role for a contact based on:
    /// 1. Contact type (Collaborator vs Customer) from database
    /// 2. Company's SaaS plan configuration (for customers)
    /// </summary>
    private async Task<string> DetermineUserRoleAsync(Contact contact, string accountNumber)
    {
        // Determine role based on contact type from database
        if (string.Equals(contact.Type, ContactType.collaborator.GetDescription(), StringComparison.OrdinalIgnoreCase))
        {
            // Collaborators get "accountant" role
            _logger.LogInformation("Assigning 'accountant' role to collaborator {ContactId} with type '{ContactType}'.", contact.ContactId, contact.Type);
            return StaticValues.Enum.PennylaneRoles.Accountant.GetDescription();
        }

        // Customers get role based on SaaS plan
        _logger.LogInformation("Determining role for customer {ContactId} with type '{ContactType}' based on company SaaS plan.", contact.ContactId, contact.Type);
        return await GetClientRoleAsync(accountNumber);
    }

    /// <summary>
    /// Creates or retrieves collaborators from ContactFunctions and returns a mapping of their Pennylane user IDs.
    /// This method processes each collaborator contact ID from ContactFunctions, creates them in Pennylane if needed,
    /// and returns their Pennylane user IDs mapped by their function (accounting manager, supervisor, accountant).
    /// </summary>
    /// <param name="contactFunctions">List of contact functions containing collaborator IDs and their roles.</param>
    /// <param name="hubName">Optional hub name to grant access to. If not provided, uses the default firm.</param>
    /// <returns>A tuple with three Pennylane user IDs: (AccountingManagerId, AccountingSupervisorId, AccountantId).</returns>
    public async Task<(string? AccountingManagerId, string? AccountingSupervisorId, string? AccountantId)> CreateCollaboratorsAndGetPennylaneIdsAsync(IReadOnlyList<ContactFunctions>? contactFunctions, string? hubName = null)
    {
        if (contactFunctions == null || !contactFunctions.Any())
        {
            _logger.LogInformation("No contact functions provided. Returning null values for all three FK fields.");
            return (null, null, null);
        }

        // Extract the accounting contacts structure from contact functions
        var accountingContacts = ContactFunctionsHelper.GetAccountingContacts(contactFunctions);

        string? pennylaneAccountingManagerId = null;
        string? pennylaneAccountingSupervisorId = null;
        string? pennylaneAccountantId = null;

        // Process Accounting Manager
        if (accountingContacts.AccountingManagerId > 0)
        {
            pennylaneAccountingManagerId = await CreateOrGetCollaboratorPennylaneIdAsync(accountingContacts.AccountingManagerId, "Accounting Manager", hubName);
        }

        // Process Accounting Supervisor
        if (accountingContacts.AccountingSupervisorId > 0)
        {
            pennylaneAccountingSupervisorId = await CreateOrGetCollaboratorPennylaneIdAsync(accountingContacts.AccountingSupervisorId, "Accounting Supervisor", hubName);
        }

        // Process Accountant
        if (accountingContacts.AccountantId > 0)
        {
            pennylaneAccountantId = await CreateOrGetCollaboratorPennylaneIdAsync(accountingContacts.AccountantId, "Accountant", hubName);
        }

        return (pennylaneAccountingManagerId, pennylaneAccountingSupervisorId, pennylaneAccountantId);
    }

    /// <summary>
    /// Creates or retrieves a single collaborator and returns their Pennylane user ID.
    /// Also grants access to the specified hub (or default firm if no hub specified).
    /// </summary>
    private async Task<string?> CreateOrGetCollaboratorPennylaneIdAsync(int contactId, string roleName, string? hubName)
    {
        try
        {
            var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);
            if (contact is null)
            {
                _logger.LogWarning("Contact with ID {ContactId} ({RoleName}) not found in database. Skipping.", contactId, roleName);
                return null;
            }

            string? userId = null;

            // Check if user already exists globally in Pennylane
            var (hasExistingGlobalUser, pennylaneUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);

            if (hasExistingGlobalUser && !string.IsNullOrEmpty(pennylaneUserId))
            {
                _logger.LogInformation("Collaborator {Email} ({RoleName}) already exists in Pennylane with ID {PennylaneUserId}.", contact.Email, roleName, pennylaneUserId);
                userId = pennylaneUserId;
            }
            else
            {
                // User doesn't exist globally, try to create them
                _logger.LogInformation("Creating new collaborator {Email} ({RoleName}) in Pennylane.", contact.Email, roleName);
                ArgumentNullException.ThrowIfNullOrWhiteSpace(_options.CollaboratorAuthenticationMode);
                var (createdUserId, alreadyExists) = await _authorizationUpdatedEventManager.TryCreatePennylaneUserAsync(contact, _options.CollaboratorAuthenticationMode);

                if (!string.IsNullOrEmpty(createdUserId))
                {
                    // User was successfully created
                    userId = createdUserId;
                    _logger.LogInformation("Successfully created collaborator {Email} ({RoleName}) with Pennylane ID {PennylaneUserId}.", contact.Email, roleName, userId);
                }
                else if (alreadyExists)
                {
                    // User already exists (already taken), proceed to grant access using email
                    _logger.LogInformation("User already exists for {Email} ({RoleName}). Proceeding to grant access with email.", contact.Email, roleName);
                }
                else
                {
                    // User creation failed for another reason - do NOT continue to grant
                    _logger.LogWarning("User creation failed for {Email} ({RoleName}). Skipping grant access.", contact.Email, roleName);
                    return null;
                }
            }

            // Resolve the firm ID: use hubName if provided, otherwise use default firm
            var firmId = pennylaneFirmTokenItems?.FirstOrDefault()?.Id;
            if (!string.IsNullOrWhiteSpace(hubName))
            {
                var hubFirmId = await _pennylaneServiceApi.TryGetFirmIdByNameAsync(hubName);
                if (!string.IsNullOrWhiteSpace(hubFirmId))
                {
                    firmId = hubFirmId;
                    _logger.LogInformation("Hub {HubName} found with firm ID {FirmId} for collaborator grant access.", hubName, firmId);
                }
                else
                {
                    _logger.LogWarning("Hub {HubName} not found. Using default firm for collaborator {Email} ({RoleName}).", hubName, contact.Email, roleName);
                }
            }

            // Grant access to the firm/hub
            if (!string.IsNullOrEmpty(firmId))
            {
                var (grantSuccess, grantError) = await _pennylaneServiceApi.GrantFirmUserAccessAsync(
                    firmId,
                    StaticValues.Enum.PennylaneRoles.Accountant.GetDescription(),
                    userId: userId,
                    userEmail: contact.Email);
                if (grantSuccess)
                {
                    _logger.LogInformation("Granted firm access to collaborator {Email} ({RoleName}) with Pennylane ID {PennylaneUserId} on firm {FirmId}.", contact.Email, roleName, userId, firmId);
                }
                else
                {
                    _logger.LogWarning("Failed to grant firm access to collaborator {Email} ({RoleName}) on firm {FirmId}. Error: {Error}", contact.Email, roleName, firmId, grantError);
                }
            }

            // Recheck the user to retrieve the UserId after access is granted by email
            // (required when the email POST fails because the user already exists)
            var (_, pennylaneUserIdToreturn) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);

            return pennylaneUserIdToreturn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or retrieve collaborator with ContactId {ContactId} ({RoleName}).", contactId, roleName);
            return null;
        }
    }

    /// <summary>
    /// Gets the client role based on the company's SaaS plan configuration.
    /// If OverrideClientRole is configured, it will be used for all clients regardless of the plan.
    /// </summary>
    private async Task<string> GetClientRoleAsync(string accountNumber)
    {
        // If an override role is configured, use it for all clients
        if (!string.IsNullOrWhiteSpace(_options.OverrideClientRole))
        {
            _logger.LogInformation("Using override client role {OverrideClientRole} for account {AccountNumber}.", _options.OverrideClientRole, accountNumber);
            return _options.OverrideClientRole;
        }

        if (string.IsNullOrWhiteSpace(_options.DefaultRole))
        {
            throw new BadRequestException(nameof(PennylaneUsers), message: "Default role is not configured in PennylaneServiceOptions.");
        }

        if (_options.PlansRoleAssignments == null || !_options.PlansRoleAssignments.Any())
        {
            _logger.LogInformation("No PlansRoleAssignments configured. Using default role {DefaultRole}.", _options.DefaultRole);
            return _options.DefaultRole;
        }

        try
        {
            var company = await _pennylaneServiceApi.GetCompanyByIdAsync(accountNumber);

            var rolesMapping = _options.PlansRoleAssignments.FirstOrDefault(x => x.Plan.Equals(company.SaasPlan, StringComparison.OrdinalIgnoreCase));

            if (rolesMapping != null && !string.IsNullOrWhiteSpace(rolesMapping.ClientRole))
            {
                _logger.LogInformation("Found role mapping for plan {Plan}: {Role}.", company.SaasPlan, rolesMapping.ClientRole);
                return rolesMapping.ClientRole;
            }

            _logger.LogInformation("No role mapping found for plan {Plan}. Using default role {DefaultRole}.", company.SaasPlan, _options.DefaultRole);
            return _options.DefaultRole;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch company SaaS plan for {AccountNumber}. Using default role {DefaultRole}.", accountNumber, _options.DefaultRole);
            return _options.DefaultRole;
        }
    }

    public async Task<PennylaneCompany?> GetCompanyByAccountIdAsync(int accountId)
    {
        var account = await _accountRepository.GetAccountByNumberOrIdAsync(accountId.ToString());
        if (account is null)
        {
            _logger.LogWarning("Account with ID {AccountId} not found.", accountId);
            return null;
        }

        // Check company in the internal companies table
        var company = await companyRepository.GetCompanyByIdAsync(account.AccountNumber);
        if (company is not null)
        {
            return company;
        }

        // Fetch company from Pennylane via API
        company = await _pennylaneServiceApi.TryGetCompanyByIdAsync(account.AccountNumber);
        if (company is not null)
        {
            // Cache the company for future lookups
            await companyRepository.SaveCompaniesAsync([company]);
            _logger.LogInformation("Company {CompanyId} fetched from Pennylane API and cached.", account.AccountNumber);
        }

        return company;
    }

    public async Task<(PennylaneUsers? User, bool AlreadyExistsInCompany)> GetPennylaneCompanyUser(string pennylaneCompanyId, int contactId)
    {
        var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);
        if (contact is null)
        {
            _logger.LogWarning("Contact with ID {ContactId} not found.", contactId);
            return (null, false);
        }

        // Check if user exists in this company first
        var (hasExistingUserInCompany, pennylaneCompanyUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email, pennylaneCompanyId);
        if (hasExistingUserInCompany && !string.IsNullOrWhiteSpace(pennylaneCompanyUserId))
        {
            return (new PennylaneUsers
            {
                Id = pennylaneCompanyUserId,
                Email = contact.Email,
            }, true);
        }

        // Fallback: check if user exists globally in Pennylane
        var (hasExistingGlobalUser, pennylaneUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);
        if (hasExistingGlobalUser && !string.IsNullOrWhiteSpace(pennylaneUserId))
        {
            return (new PennylaneUsers
            {
                Id = pennylaneUserId,
                Email = contact.Email,
            }, false);
        }

        return (null, false);
    }

    public async Task<(string? UserId, bool AlreadyExists)> CreatePennylaneUserAsync(int contactId)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(_options.AuthenticationMode);

        var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: contactId);
        if (contact is null)
        {
            _logger.LogWarning("Contact with ID {ContactId} not found.", contactId);
            return (null, false);
        }

        // Check if user already exists globally in Pennylane
        var (hasExistingUser, existingUserId) = await _authorizationUpdatedEventManager.CheckIfUserExistsAsync(contact.Email);
        if (hasExistingUser && !string.IsNullOrWhiteSpace(existingUserId))
        {
            _logger.LogInformation("User {Email} already exists in Pennylane with ID {UserId}.", contact.Email, existingUserId);
            return (existingUserId, true);
        }

        var (createdUserId, alreadyExistError) = await _authorizationUpdatedEventManager.TryCreatePennylaneUserAsync(contact, _options.AuthenticationMode);
        _logger.LogInformation("Successfully created user {Email} in Pennylane with ID {UserId}.", contact.Email, createdUserId);

        return (createdUserId, alreadyExistError);
    }

    public async Task<bool> InsertUserPennylaneRole(PnlRole pnlRole, string pennylaneUserId, string pennylaneCompanyId)
    {
        ArgumentNullException.ThrowIfNull(pnlRole);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(pennylaneCompanyId);

        var grantUserAccess = new GrantUserAccess
        {
            Role = pnlRole.Role
        };

        string userIdentifier;

        // Feature flag logic: if UseUserIdForGrantAccess is true, use user_id (legacy), otherwise use user_email (new)
        if (_options.UseUserIdForGrantAccess)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(pennylaneUserId);
            grantUserAccess.UserId = pennylaneUserId;
            userIdentifier = pennylaneUserId;
        }
        else
        {
            var contact = await _contactRepository.GetContactByEmailOrIdAsync(contactId: pnlRole.ContactId);
            if (contact is null)
            {
                _logger.LogWarning("Contact with ID {ContactId} not found.", pnlRole.ContactId);
                return false;
            }
            grantUserAccess.UserEmail = contact.Email;
            userIdentifier = contact.Email;
        }

        var accessGranted = await _pennylaneServiceApi.TryGrantCompanyUserAccessAsync(pennylaneCompanyId, grantUserAccess);

        if (accessGranted is null)
        {
            _logger.LogWarning("Failed to grant access for user {UserIdentifier} to company {CompanyId}.", userIdentifier, pennylaneCompanyId);
            return false;
        }

        var pnlNewRoleEntity = MapPnlRoles.MapPnlRoleModelToEntity(pnlRole);
        if (pnlNewRoleEntity is null)
        {
            _logger.LogError("Failed to map PnlRole to entity for account {AccountId} and contact {ContactId}.", pnlRole.AccountId, pnlRole.ContactId);
            return false;
        }

        await _pennylaneRepository.InsertPennylaneRoleAsync(pnlNewRoleEntity);
        _logger.LogInformation("Successfully inserted Pennylane role for user {UserIdentifier} in company {CompanyId}.", userIdentifier, pennylaneCompanyId);

        return true;
    }

    public async Task RevokeUserAccessAsync(string pennylaneCompanyId, string pennylaneUserId)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(pennylaneCompanyId);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(pennylaneUserId);

        await _pennylaneServiceApi.RevokeAccessUserCompanyAsync(pennylaneCompanyId, pennylaneUserId);
        _logger.LogInformation("Successfully revoked access for user {UserId} from company {CompanyId}.", pennylaneUserId, pennylaneCompanyId);
    }
}
