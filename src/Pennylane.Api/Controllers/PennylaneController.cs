// <copyright file="PennylaneController.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Models.Accounts;
using Microsoft.AspNetCore.Mvc;
using Pennylane.Application.Consts;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pennylane;
using Pennylane.WebApi.Models;
using Pulse.ExceptionMiddleware.Model;
using System.ComponentModel.DataAnnotations;

namespace Pennylane.WebApi.Controllers;

/// <summary>
/// Controller for managing Pennylane-related operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PennylaneController"/> class.
/// </remarks>
/// <param name="pennylaneService">The service for handling Pennylane operations.</param>
[Route("api/pennylane")]
[ApiController]
public class PennylaneController(IPennylaneService pennylaneService) : ControllerBase
{
    /// <summary>
    /// Retrieves the list of roles.
    /// </summary>
    /// <returns>A list of roles.</returns>
    [HttpGet("roles")]
    public async Task<ActionResult<List<Role>>> GetRoles([FromQuery] string? companyId)
    {
        if (companyId == null)
        {
            return BadRequest("accountNumber is required.");
        }

        var company = await pennylaneService.GetCompanyByIdAsync(companyId);

        if (company == null)
        {
            return NotFound($"Could not find company {companyId}");
        }

        return RoleHelper.GetRoles(company.SaasPlan);
    }
   
    /// <summary>
    /// Creates a subscription asynchronously.
    /// </summary>
    /// <param name="roleToCreate">The role containing subscription details.</param>
    /// <returns>An ActionResult indicating the result of the operation.</returns>
    [HttpPost("role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> CreatePennylaneRoleAsync(
       [FromBody][Required] PnlRole roleToCreate)
    {
        await pennylaneService.InsertPennylaneRoleAsync(roleToCreate);
        return StatusCode(StatusCodes.Status201Created, $"Role assigned to contact {roleToCreate.ContactId} on account {roleToCreate.AccountId}.");
    }

    /// <summary>
    /// Grants Pennylane access to a user by creating or retrieving them and assigning a role.
    /// </summary>
    /// <param name="request">The role containing subscription details.</param>
    /// <returns>A detailed result of the operation.</returns>
    [HttpPost("role/create")]
    [ProducesResponseType(typeof(GrantPennylaneAccessResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GrantPennylaneAccessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GrantPennylaneAccessResult), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GrantPennylaneAccessResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GrantPennylaneAccessResult>> GrantPennylaneAccessAsync(
       [FromBody][Required] PnlRole request)
    {
        var result = new GrantPennylaneAccessResult
        {
            ContactId = request.ContactId,
            AccountId = request.AccountId,
            Role = request.Role,
            Status = PennylaneStatuses.Failed,
            Message = string.Empty
        };
        var steps = new List<string>();

        // Step 1: Get company
        var (company, companyError) = await TryGetCompanyAsync(request.AccountId);
        if (company is null)
        {
            steps.Add($"Company not found for account {request.AccountId}");
            result.Steps = steps;
            return NotFound(result.WithError("Company not found", $"No Pennylane company found for account {request.AccountId}. Access not granted."));
        }
        result.PennylaneCompanyId = company.Id;
        steps.Add($"Company found: {company.Id}");

        // Step 2: Check existing user in company
        var (existingUser, alreadyExistsInCompany) = await pennylaneService.GetPennylaneCompanyUser(company.Id, request.ContactId);
        string? userId = existingUser?.Id;

        if (alreadyExistsInCompany)
        {
            steps.Add($"User already exists in company with ID: {userId}");
        }
        else if (existingUser is not null)
        {
            steps.Add($"User exists in Pennylane (ID: {userId}) but not in this company");
        }

        // Step 3: Create user if needed
        bool userWasCreated = false;
        if (userId is null)
        {
            var (createdUserId, alreadyExistError) = await pennylaneService.CreatePennylaneUserAsync(request.ContactId);
            if (string.IsNullOrEmpty(createdUserId) && !alreadyExistError)
            {
                steps.Add($"Failed to create user for contact {request.ContactId}");
                result.Steps = steps;
                return BadRequest(result.WithError("User creation failed", $"Company found but failed to create Pennylane user for contact {request.ContactId}. Access not granted."));
            }
            userId = createdUserId;
            userWasCreated = true;
            steps.Add($"User created or post user returned already exists with ID: {userId??string.Empty}");
        }

        (existingUser, alreadyExistsInCompany) = await pennylaneService.GetPennylaneCompanyUser(company.Id, request.ContactId);
        userId = existingUser?.Id;

        // Step 4: Assign role
        await pennylaneService.InsertUserPennylaneRole(request, userId, company.Id);
        steps.Add($"Role '{request.Role}' assigned successfully");

        result.PennylaneUserId = userId;
        result.Steps = steps;

        // Success response
        return BuildSuccessResponse(result, request, alreadyExistsInCompany, userWasCreated);
    }

    private ActionResult<GrantPennylaneAccessResult> BuildSuccessResponse(
        GrantPennylaneAccessResult result, PnlRole request, bool alreadyExistsInCompany, bool userWasCreated)
    {
        if (alreadyExistsInCompany)
        {
            result.Status = PennylaneStatuses.AlreadyHasAccess;
            result.Message = $"Company found. User already had access to this company. Role '{request.Role}' has been updated. Access granted.";
            return Ok(result);
        }

        if (userWasCreated)
        {
            result.Status = PennylaneStatuses.Created;
            result.Message = $"Company found. User created successfully. Role '{request.Role}' assigned. Access granted.";
            return StatusCode(StatusCodes.Status201Created, result);
        }

        // User exists in Pennylane but not in this company
        result.Status = PennylaneStatuses.ExistingUserAccessGranted;
        result.Message = $"Company found. Existing Pennylane user granted access to this company. Role '{request.Role}' assigned. Access granted.";
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Gets a role.
    /// </summary>
    /// <param name="contactId">Contact identifier.</param>
    /// <param name="accountId">Account identifier.</param>
    /// <returns>PnlRole</returns>
    [HttpGet("role/accounts/{accountId}/contacts/{contactId}")]
    [ProducesResponseType(typeof(PnlRole), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PnlRole>> GetPennylaneRoleAsync(int accountId, int contactId)
    {
        var role = await pennylaneService.GetPennylaneRoleAsync(contactId, accountId);
        return Ok(role);
    }

    /// <summary>
    /// Revokes Pennylane access from a user by removing their access from the company.
    /// </summary>
    /// <param name="request">The request containing account and contact identifiers.</param>
    /// <returns>A detailed result of the revocation operation.</returns>
    [HttpPost("role/revoke")]
    [ProducesResponseType(typeof(RevokePennylaneAccessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RevokePennylaneAccessResult), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RevokePennylaneAccessResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RevokePennylaneAccessResult>> RevokePennylaneAccessAsync(
       [FromBody][Required] RevokeAccessRequest request)
    {
        var result = new RevokePennylaneAccessResult
        {
            ContactId = request.ContactId,
            AccountId = request.AccountId,
            Status = PennylaneStatuses.Failed,
            Message = string.Empty
        };
        var steps = new List<string>();

        // Step 1: Get company
        var (company, _) = await TryGetCompanyAsync(request.AccountId);
        if (company is null)
        {
            steps.Add($"Company not found for account {request.AccountId}");
            result.Steps = steps;
            return NotFound(result.WithError("Company not found", $"No Pennylane company found for account {request.AccountId}. Access not revoked."));
        }
        result.PennylaneCompanyId = company.Id;
        steps.Add($"Company found: {company.Id}");

        // Step 2: Check if user exists in company
        var (existingUser, existsInCompany) = await pennylaneService.GetPennylaneCompanyUser(company.Id, request.ContactId);

        if (!existsInCompany || existingUser is null)
        {
            steps.Add($"User not found in company for contact {request.ContactId}");
            result.Steps = steps;
            result.Status = PennylaneStatuses.UserNotFound;
            result.Message = $"User for contact {request.ContactId} was not found in company {company.Id}. No action taken.";
            return Ok(result);
        }

        result.PennylaneUserId = existingUser.Id;
        steps.Add($"User found in company with ID: {existingUser.Id}");

        // Step 3: Revoke access
        await pennylaneService.RevokeUserAccessAsync(company.Id, existingUser.Id);
        steps.Add($"Access revoked for user {existingUser.Id}");

        // Step 4: Delete local role record
        await pennylaneService.DeletePennylaneRoleAsync(request.AccountId, request.ContactId);
        steps.Add($"Local role record deleted");

        result.Steps = steps;
        result.Status = PennylaneStatuses.Revoked;
        result.Message = $"Access successfully revoked for contact {request.ContactId} from company {company.Id}.";

        return Ok(result);
    }

    private async Task<(PennylaneCompany? Company, bool NotFound)> TryGetCompanyAsync(int accountId)
    {
        try
        {
            var company = await pennylaneService.GetCompanyByAccountIdAsync(accountId);
            return (company, company is null);
        }
        catch (KeyNotFoundException)
        {
            return (null, true);
        }
    }

    /// <summary>
    /// Get company emails.
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <returns>Pennylane emails.</returns>
    [HttpGet("companies/{accountNumber}/emails")]
    [ProducesResponseType(typeof(PennylaneEmails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PennylaneEmails>> GetCompanyEmailsByAccountNumberAsync(string accountNumber)
    {
        var email = await pennylaneService.GetPennylaneEmailsAsync(accountNumber);
        return email is null ? NotFound("Account Not Found") : Ok(email);
    }


    /// <summary>
    /// Create a company in Pennylane and automatically add users to it.
    /// </summary>
    /// <param name="request">The company creation request containing account ID and contact IDs to add.</param>
    /// <returns>The result of the company creation and user additions.</returns>
    /// <remarks>
    /// IMPORTANT: The 'id' field in the request MUST be the client code (accountNumber) to maintain
    /// the direct link with the company for Pennylane DMS synchronization and role assignment.
    /// By default, Pennylane replaces this with a random ID if not specified, which breaks the integration.
    ///
    /// For each contact ID in the request:
    /// - The contact is retrieved from the database to get its type (Collaborator or Customer)
    /// - If the user doesn't exist globally in Pennylane, it will be created
    /// - If the user exists but doesn't have access to this company, access will be granted
    /// - Role assignment is automatic based on the contact type from database:
    ///   * Collaborators (Type='Collaborator'): Assigned the 'accountant' role
    ///   * Customers (Type='Customer'): Assigned a role based on the company's SaaS plan configuration
    ///
    /// HTTP Status Codes:
    /// - 201 Created: Company was newly created and all users added successfully
    /// - 200 OK: Company already existed and all users added successfully
    /// - 200 OK: Company creation failed and should be created later (status "À créer")
    /// - 207 Multi-Status: Company operation succeeded but some users failed to be added
    /// - 409 Conflict: Company already exists under another firm; the client must request firm assignment
    /// - 400/404/500: Error during company creation
    /// </remarks>
    [HttpPost("companies/onboarding")]
    [ProducesResponseType(typeof(CreateCompanyResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateCompanyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateCompanyResult), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(typeof(CreateCompanyResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCompanyResult>> CreateCompanyAsync([FromBody][Required] CreateCompanyRequest request)
    {
        // Step 1: Create collaborators first and get their Pennylane user IDs
        // Also grants access to the hub (or default firm) for each collaborator
        var (accountingManagerId, accountingSupervisorId, accountantId) =
            await pennylaneService.CreateCollaboratorsAndGetPennylaneIdsAsync(request.ContactFunctions, request.HubName);

        // Add the Pennylane IDs to the request for company creation
        request.PennylaneAccountingManagerId = accountingManagerId;
        request.PennylaneAccountingSupervisorId = accountingSupervisorId;
        request.PennylaneAccountantId = accountantId;

        // Step 2: Create or retrieve the company with the Pennylane IDs
        var (company, companyAlreadyExists, createdOnDefault, requiresFirmAssignment, requiresFirmTransfer) = await pennylaneService.CreateCompanyOnlyAsync(request);

        if (requiresFirmAssignment)
        {
            return Conflict(new CreateCompanyResult
            {
                Company = null,
                Status = PennylaneStatuses.RequiresFirmAssignment,
                UserResults = null
            });
        }

        if (requiresFirmTransfer)
        {
            return Conflict(new CreateCompanyResult
            {
                Company = null,
                Status = PennylaneStatuses.RequiresFirmTransfer,
                UserResults = null
            });
        }

        if (companyAlreadyExists && string.IsNullOrWhiteSpace(company?.Id))
        {
            var alreadyExistsResult = new CreateCompanyResult
            {
                Company = company,
                Status = PennylaneStatuses.AlreadyExists,
                UserResults = null
            };

            return Ok(alreadyExistsResult);
        }

        if (!companyAlreadyExists && string.IsNullOrWhiteSpace(company?.Id))
        {
            var toCreateResult = new CreateCompanyResult
            {
                Company = company,
                Status = PennylaneStatuses.ToCreate,
                UserResults = null
            };

            return Ok(toCreateResult);
        }

        // Step 3: Calculate client IDs (Contacts - ContactFunctions IDs)
        var collaboratorIds = request.ContactFunctions?
            .Select(cf => cf.ContactId)
            .ToHashSet() ?? new HashSet<int>();

        var clientIds = request.Contacts
            .Where(contactId => !collaboratorIds.Contains(contactId))
            .ToList();

        var companyId = company!.Id;

        // Step 4: Add clients to the company
        var userResults = await pennylaneService.AddUsersToCompanyAsync(companyId, clientIds);

        // Determine final status
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, companyAlreadyExists, createdOnDefault, company);

        var result = new CreateCompanyResult
        {
            Company = company,
            Status = status,
            UserResults = userResults.Any() ? userResults : null
        };

        // Determine HTTP status code based on operation result
        return result.Status switch
        {
            PennylaneStatuses.Created => StatusCode(StatusCodes.Status201Created, result),
            PennylaneStatuses.CreatedOnDefault => StatusCode(StatusCodes.Status201Created, result),
            PennylaneStatuses.Validated => StatusCode(StatusCodes.Status201Created, result),
            PennylaneStatuses.AlreadyExists => Ok(result),
            _ => StatusCode(StatusCodes.Status207MultiStatus, result)
        };
    }
}
