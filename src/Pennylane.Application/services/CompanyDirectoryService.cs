using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.services;

public class CompanyDirectoryService(
    IPennylaneApiGroupService pennylaneApiGroupService,
    ICompanyDirectoryRepository companyDirectoryRepository,
    ILogger<CompanyDirectoryService> logger) : ICompanyDirectoryService
{
    private const int PageSize = 500;
    private const int SirenLength = 9;
    private const int SiretLength = 14;

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshotDate = DateTime.UtcNow;
        var entries = new List<CompanyDirectoryEntity>();
        var currentPage = 1;
        int totalPages;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pager = await pennylaneApiGroupService.GetCompaniesAsync(currentPage, PageSize);
            totalPages = pager.TotalPages;

            entries.AddRange(pager.Companies
                .Select(c => new { c.Id, Siren = NormalizeToSiren(c.RegNo) })
                .Where(c => c.Siren is not null)
                .Select(c => new CompanyDirectoryEntity
                {
                    CompanyId = c.Id,
                    Siren = c.Siren!,
                    SnapshotDate = snapshotDate,
                }));

            currentPage++;
        }
        while (currentPage <= totalPages);

        if (entries.Count == 0)
        {
            logger.LogWarning("Company directory refresh produced no entries across {Pages} pages; keeping the previous snapshot.", totalPages);
            return 0;
        }

        await companyDirectoryRepository.ReplaceAllAsync(entries, cancellationToken);
        logger.LogInformation("Company directory refreshed with {Count} entries across {Pages} pages.", entries.Count, totalPages);
        return entries.Count;
    }

    private static string? NormalizeToSiren(string? regNo)
    {
        var trimmed = regNo?.Trim();
        if (trimmed is null || (trimmed.Length != SirenLength && trimmed.Length != SiretLength))
        {
            return null;
        }

        if (!trimmed.All(char.IsAsciiDigit))
        {
            return null;
        }

        return trimmed[..SirenLength];
    }
}
