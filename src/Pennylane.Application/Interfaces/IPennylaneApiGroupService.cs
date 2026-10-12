using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;

namespace Pennylane.Application.Interfaces;

public interface IPennylaneApiGroupService
{
    Task<PennylaneUsersPager> GetCompanyUsersAsync(string companyId, int? page = default);

    Task<PennylaneUsersPager> GetUsersAsync(int? page = default);

    Task<string> PostUsersAsync(PennylaneContact pennylaneContact);

    Task<(string? UserId, bool AlreadyExists)> TryPostUsersAsync(PennylaneContact pennylaneContact);

    Task<PennylaneCompanyPager> GetCompaniesAsync(int? page = default, int? perPage = default);

    Task<int> GrantCompanyUserAccessAsync(string companyId, GrantUserAccess userAccess);

    Task RevokeAccessUserCompanyAsync(string companyId, string userId);

    Task<PennylaneCompany> GetCompanyByIdAsync(string companyId);

    Task<PennylaneUsers?> TryGetUserAsync(string contactEmail);
    Task<PennylaneCompany?> TryGetCompanyByIdAsync(string companyId);

    Task<PennylaneUsers?> TryGetCompanyUserAsync(string contactEmail, string accountNumber);

    Task<int?> TryGrantCompanyUserAccessAsync(string companyId, GrantUserAccess userAccess);

    Task<string> UpdateUserEmailAsync(PennylaneContact pennylaneContact, string newEmail);
    Task<bool> TryUpdateCompanyIdAsync(string currentCompanyId, string newCompanyId);

    Task<bool> TryUpdateCompanyAccountingContactsAsync(string companyId, UpdateCompanyAccountingContactsRequest request);
    Task<bool> TryUpdateUserAuthenticationMethodAsync(string userId, string authenticationMethod);
    Task<CreateCompanyApiResult> CreateCompanyAsync(CreatePennylaneCompanyRequest request);
    Task<(bool Success, string? ErrorMessage)> GrantFirmUserAccessAsync(string firmId, string role, string? userId = null, string? userEmail = null);
    Task<string?> TryGetFirmIdByNameAsync(string hubName);
}
