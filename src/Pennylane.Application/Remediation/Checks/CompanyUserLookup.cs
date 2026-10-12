using Pennylane.Application.Interfaces;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;

namespace Pennylane.Application.Remediation.Checks;

/// <summary>
/// Reads the full paginated users listing of a Pennylane company.
/// </summary>
internal static class CompanyUserLookup
{
    internal static async Task<IReadOnlyList<PennylaneUsers>> GetAllAsync(
        IPennylaneApiGroupService pennylaneApiGroupService,
        string companyId,
        CancellationToken cancellationToken)
    {
        var users = new List<PennylaneUsers>();
        var currentPage = 1;
        int totalPages;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pager = await pennylaneApiGroupService.GetCompanyUsersAsync(companyId, currentPage);
            totalPages = pager.TotalPages;
            users.AddRange(pager.Users);
            currentPage++;
        }
        while (currentPage <= totalPages);

        return users;
    }
}
