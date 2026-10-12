using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Polly;
using Polly.Retry;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Web;
using InvalidOperationException = System.InvalidOperationException;

namespace Pennylane.Application.services;

public class PennylaneApiGroupService(HttpClient httpClient, ILogger<PennylaneApiGroupService> logger, IOptions<PennylaneServiceOptions> options) : IPennylaneApiGroupService
{
    private readonly PennylaneServiceOptions _options = options.Value;
    private static readonly string code = "PNL0500";
    private const string CompanyNotInScopeMarker = "not found in your scope";
    private readonly ILogger<PennylaneApiGroupService> logger = logger;

    private readonly HttpClient httpClient = httpClient;

    private readonly ResiliencePipeline _companyNotInScopeRetryPipeline = BuildCompanyNotInScopeRetryPipeline(options.Value, logger);

    public async Task<PennylaneCompanyPager> GetCompaniesAsync(int? page = default, int? perPage = default)
    {
        var endpoint = CanAddPager("companies", page, perPage);
        var (pennylaneCompanyPager, _) = await httpClient.GetAndParseAsync<PennylaneCompanyPager>(endpoint);

        if (pennylaneCompanyPager == null)
        {
            throw new BadRequestException(code, "Failed to retrieve companies.");
        }

        return pennylaneCompanyPager;
    }

    const int MAX_PER_PAGE = 500;

    public async Task<PennylaneUsersPager> GetCompanyUsersAsync(string companyId, int? page = default)
    {
        var endpoint = CanAddPager($"companies/{HttpUtility.UrlEncode(companyId)}/users", page, MAX_PER_PAGE);
        var (pennylaneUsersPager, _) = await httpClient.GetAndParseAsync<PennylaneUsersPager>(endpoint);

        if (pennylaneUsersPager == null)
        {
            throw new BadRequestException(code, $"Failed to retrieve company users for the company {companyId}.");
        }

        return pennylaneUsersPager;
    }

    public async Task<PennylaneUsersPager> GetUsersAsync(int? page = default)
    {
        var endpoint = CanAddPager("users", page);
        var (pennylaneUsersPager, _) = await httpClient.GetAndParseAsync<PennylaneUsersPager>(endpoint);

        if (pennylaneUsersPager == null)
        {
            throw new BadRequestException(code, "Failed to retrieve users.");
        }

        return pennylaneUsersPager;
    }

    public async Task<string?> TryGetFirmIdByNameAsync(string hubName)
    {
        if (string.IsNullOrWhiteSpace(hubName))
        {
            return null;
        }

        var normalizedHubName = hubName.Trim();
        int currentPage = 1;
        PennylaneFirmsPager? response;
        do
        {
            var endpoint = BuildFirmsPagerUrl(currentPage, MAX_PER_PAGE);
            var (pennylaneFirmsPager, _) = await httpClient.GetAndParseAsync<PennylaneFirmsPager>(endpoint);

            if (pennylaneFirmsPager == null)
            {
                throw new BadRequestException(code, "Failed to retrieve firms.");
            }

            var firm = pennylaneFirmsPager.Firms
                .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Name)
                    && f.Name.Contains(normalizedHubName, StringComparison.OrdinalIgnoreCase));

            if (firm != null)
            {
                return firm.Id;
            }

            response = pennylaneFirmsPager;
            currentPage++;
        }
        while (currentPage <= response.TotalPages);

        return null;
    }

    public async Task<int> GrantCompanyUserAccessAsync(string companyId, GrantUserAccess userAccess)
    {
        userAccess.UserEmail = userAccess.UserEmail?.ToLowerInvariant();
        var response = await httpClient.PostJsonAndParseAsync<GrantUserAccess, PennylaneResult<int>>($"companies/{companyId}/grant_access", userAccess);
        if (response == null)
        {
            throw new BadRequestException(code, $"Failed to grant access. Status code: {response}");
        }

        return response.Id;
    }

    public async Task<int?> TryGrantCompanyUserAccessAsync(string companyId, GrantUserAccess userAccess)
    {
        userAccess.UserEmail = userAccess.UserEmail?.ToLowerInvariant();
        var response = await httpClient.PostAsync($"companies/{companyId}/grant_access", JsonContent.Create(userAccess));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            var userIdentifier = userAccess.UserEmail ?? userAccess.UserId;
            logger.LogWarning("Fail to add user {UserIdentifier} to company {CompanyId}. Error: {Error}", userIdentifier, companyId, error);
            return null;
        }

        var plop = await response.Content.ReadFromJsonAsync<PennylaneResult<int>>();
        return plop.Id;
    }

    private static string CanAddPager(string baseUrl, int? page, int? perPage = null)
    {
        List<KeyValuePair<string, StringValues>> parameters = new();
        if (page.HasValue)
        {
            parameters.Add(new("page", page.ToString()));
        }

        if (perPage.HasValue)
        {
            parameters.Add(new("per_page", perPage.ToString()));
        }

        var queryString = QueryString.Create(parameters);

        return $"{baseUrl}{queryString}";
    }

    private static string BuildFirmsPagerUrl(int page, int perPage)
    {
        List<KeyValuePair<string, StringValues>> parameters =
        [
            new("page", page.ToString()),
            new("per_page", perPage.ToString())
        ];

        var queryString = QueryString.Create(parameters);
        return $"firms{queryString}";
    }

    public async Task<string> PostUsersAsync(PennylaneContact pennylaneContact)
    {
        var response = await httpClient.PostJsonAndParseAsync<PennylaneContact, PennylaneResult<string>>("users", pennylaneContact);
        if (response == null)
        {
            throw new BadRequestException(code, "Failed to create user.");
        }

        return response.Id;
    }

    public async Task<(string? UserId, bool AlreadyExists)> TryPostUsersAsync(PennylaneContact pennylaneContact)
    {
        var response = await httpClient.PostAsync("users", JsonContent.Create(pennylaneContact));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            // If user already exists (email already taken), return AlreadyExists = true so caller can fetch user ID
            if (errorContent.Contains("already taken", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("User with email {Email} already exists in Pennylane (already taken). Continuing flow.", pennylaneContact.Email);
                return (null, true);
            }

            // Other error - log and return failure (caller should NOT continue to grant)
            logger.LogError("Failed to create user in Pennylane. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            return (null, false);
        }

        var result = await response.Content.ReadFromJsonAsync<PennylaneResult<string>>();
        return (result?.Id, false);
    }

    public async Task<string> UpdateUserEmailAsync(PennylaneContact pennylaneContact, string newEmail)
    {
        var response = await httpClient.PutJsonAndParseAsync<object, PennylaneResult<string>>($"users/{pennylaneContact.Id}", new { email = newEmail });
        if (response == null)
        {
            throw new BadRequestException(code, "Failed to update user.");
        }
        return response.Id;
    }

    public async Task<bool> TryUpdateCompanyIdAsync(string currentCompanyId, string newCompanyId)
    {
        try
        {
            var response = await httpClient.PutJsonAndParseAsync<object, PennylaneResult<string>>(
                $"companies/{HttpUtility.UrlEncode(currentCompanyId)}", new { id = newCompanyId });
            return response is not null;
        }
        catch (Exception ex) when (ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "Failed to update Pennylane company id from {CurrentId} to {NewId}", currentCompanyId, newCompanyId);
            return false;
        }
    }

    public async Task<bool> TryUpdateCompanyAccountingContactsAsync(string companyId, UpdateCompanyAccountingContactsRequest request)
    {
        try
        {
            var response = await httpClient.PutJsonAndParseAsync<UpdateCompanyAccountingContactsRequest, PennylaneResult<string>>(
                $"companies/{HttpUtility.UrlEncode(companyId)}", request);
            return response is not null;
        }
        catch (Exception ex) when (ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "Failed to update accounting contacts for Pennylane company {CompanyId}", companyId);
            return false;
        }
    }

    public async Task<bool> TryUpdateUserAuthenticationMethodAsync(string userId, string authenticationMethod)
    {
        try
        {
            var response = await httpClient.PutJsonAndParseAsync<object, PennylaneResult<string>>(
                $"users/{HttpUtility.UrlEncode(userId)}", new { authentication_method = authenticationMethod });
            return response is not null;
        }
        catch (Exception ex) when (ex is BadRequestException || ex is RequestHttpException)
        {
            logger.LogError(ex, "Failed to update authentication method for Pennylane user {UserId}", userId);
            return false;
        }
    }

    public async Task RevokeAccessUserCompanyAsync(string companyId, string userId)
    {
        await httpClient.DeleteWithValidationAsync($"companies/{companyId}/revoke_access?user_id={userId}");
    }

    public async Task<PennylaneCompany> GetCompanyByIdAsync(string companyId)
    {
        var endpoint = $"companies/{HttpUtility.UrlEncode(companyId)}";
        var response = await httpClient.GetAsync(endpoint);
        var result = await response.Content.ReadFromJsonAsync<PennylaneCompany>();
        return result!;
    }

    public async Task<PennylaneCompany?> TryGetCompanyByIdAsync(string companyId)
    {
        var endpoint = $"companies/{HttpUtility.UrlEncode(companyId)}";
        var response = await httpClient.GetAsync(endpoint);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await response.Content.ReadFromJsonAsync<PennylaneCompany?>();
    }

    protected static string GetUserFilterUrl(string contactEmail)
    {
        var urlEncode = HttpUtility.UrlEncode(contactEmail);
        return
            $$"""users?filter=[{"field":"email","operator":"eq","value":"{{urlEncode}}"}]""";
    }

    public async Task<PennylaneUsers?> TryGetUserAsync(string contactEmail)
    {
        var response = await httpClient.GetAsync(GetUserFilterUrl(contactEmail));
        var result = await response.Content.ReadFromJsonAsync<PennylaneUsersPager>();
        return result!.Users.FirstOrDefault();
    }

    public async Task<PennylaneUsers?> TryGetCompanyUserAsync(string contactEmail, string accountNumber)
    {
        try
        {
            return await _companyNotInScopeRetryPipeline.ExecuteAsync(
                async _ => await FindCompanyUserByEmailAsync(contactEmail, accountNumber));
        }
        catch (RequestHttpException ex) when (IsCompanyNotInScopeError(ex))
        {
            logger.LogWarning(ex, "Company {CompanyId} users still not reachable after retries. No user returned for {Email}.", accountNumber, contactEmail);
            return null;
        }
    }

    private static ResiliencePipeline BuildCompanyNotInScopeRetryPipeline(PennylaneServiceOptions options, ILogger<PennylaneApiGroupService> logger)
    {
        if (options.CompanyUsersRetryCount < 1)
        {
            return ResiliencePipeline.Empty;
        }

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // Pennylane propagates the firm scope asynchronously after a company creation:
                // /users can transiently return 404 "Company not found in your scope".
                ShouldHandle = new PredicateBuilder().Handle<RequestHttpException>(IsCompanyNotInScopeError),
                MaxRetryAttempts = options.CompanyUsersRetryCount,
                Delay = TimeSpan.FromMilliseconds(options.CompanyUsersRetryDelayInMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogInformation("Company not yet visible in scope (attempt {Attempt}/{MaxAttempts}). Retrying in {DelayMs} ms.", args.AttemptNumber + 1, options.CompanyUsersRetryCount, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .Build();
    }

    private static bool IsCompanyNotInScopeError(RequestHttpException ex)
        => ex.HttpCode == HttpStatusCode.NotFound
            && ex.Message.Contains(CompanyNotInScopeMarker, StringComparison.OrdinalIgnoreCase);

    private async Task<PennylaneUsers?> FindCompanyUserByEmailAsync(string contactEmail, string accountNumber)
    {
        int currentPage = 1;
        PennylaneUsersPager? response;

        do
        {
            response = await GetCompanyUsersAsync(accountNumber, currentPage);

            var user = response.Users.FirstOrDefault(u => u.Email.Equals(contactEmail, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                return user;
            }

            currentPage++;
        }
        while (currentPage <= response.TotalPages);

        return null;
    }

    public async Task<CreateCompanyApiResult> CreateCompanyAsync(CreatePennylaneCompanyRequest request)
    {
        var url = "companies";
        var response = await httpClient.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            if (RequiresFirmAssignment(errorContent))
            {
                logger.LogWarning("Pennylane company creation refused: client must request firm assignment. Error: {Error}", errorContent);
                return CreateCompanyApiResult.FailedRequiresFirmAssignment(errorContent);
            }

            if (RequiresFirmTransfer(errorContent))
            {
                logger.LogWarning("Pennylane company creation refused: company requires firm transfer. Error: {Error}", errorContent);
                return CreateCompanyApiResult.FailedRequiresFirmTransfer(errorContent);
            }

            if (IsRegNoConflict(errorContent) || DoesAlreadyExists(errorContent))
            {
                return CreateCompanyApiResult.FailedRegNoExists(errorContent);
            }

            logger.LogError("Failed to create company in Pennylane. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            return CreateCompanyApiResult.Failed(errorContent);
        }

        var result = await response.Content.ReadFromJsonAsync<CreatePennylaneCompanyResponse>();
        if (result == null)
        {
            return CreateCompanyApiResult.Failed("Failed to deserialize the company creation response.");
        }

        logger.LogInformation("Successfully created company {CompanyName} with ID {CompanyId} in Pennylane.", request.Name, result.Id);
        return CreateCompanyApiResult.Succeeded(result);
    }

    private static bool IsRegNoConflict(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("reg_no", StringComparison.OrdinalIgnoreCase)
            && message.Contains("already", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoesAlreadyExists(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Company already exists", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool RequiresFirmAssignment(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("must request to be assigned to your firm", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool RequiresFirmTransfer(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("you can request its transfer from its current firm", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task<(bool Success, string? ErrorMessage)> GrantFirmUserAccessAsync(string firmId, string role, string? userId = null, string? userEmail = null)
    {
        // Validate that the appropriate identifier is provided based on the feature flag
        if (_options.UseUserIdForGrantAccess)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required when UseUserIdForGrantAccess is enabled.", nameof(userId));
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new ArgumentException("userEmail is required when UseUserIdForGrantAccess is disabled.", nameof(userEmail));
            }
        }

        var grantAccess = new GrantUserAccess
        {
            Role = role
        };

        // Feature flag logic: if UseUserIdForGrantAccess is true, use user_id (legacy), otherwise use user_email (new)
        string userIdentifier;
        string identifierType;
        if (_options.UseUserIdForGrantAccess)
        {
            grantAccess.UserId = userId;
            userIdentifier = userId!;
            identifierType = "UserId";
        }
        else
        {
            grantAccess.UserEmail = userEmail;
            userIdentifier = userEmail!;
            identifierType = "UserEmail";
        }

        logger.LogInformation("[PennylaneApiGroupService] Granting firm access using {IdentifierType}: {UserIdentifier}", identifierType, userIdentifier);

        var url = $"firms/{firmId}/grant_access";
        var response = await httpClient.PostAsJsonAsync(url, grantAccess);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            // 422 with "already has access" message is considered OK
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity &&
                (errorContent.Contains("a déjà accès à ce cabinet", StringComparison.OrdinalIgnoreCase)
                 || errorContent.Contains("already has access to the firm", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogInformation("User {UserIdentifier} already has access to firm {FirmId}. Skipping grant.", userIdentifier, firmId);
                return (true, null);
            }

            logger.LogError("Failed to grant firm access. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            return (false, errorContent);
        }

        logger.LogInformation("Successfully granted {Role} access to user {UserIdentifier} on firm {FirmId}.", role, userIdentifier, firmId);
        return (true, null);
    }
}
