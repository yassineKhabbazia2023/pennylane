using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class CompanyDirectoryRepository(PennylaneContext pennylaneContext) : ICompanyDirectoryRepository
{
    public async Task ReplaceAllAsync(IReadOnlyList<CompanyDirectoryEntity> entries, CancellationToken cancellationToken = default)
    {
        var existing = await pennylaneContext.CompanyDirectoryEntity.ToListAsync(cancellationToken);
        pennylaneContext.CompanyDirectoryEntity.RemoveRange(existing);
        await pennylaneContext.CompanyDirectoryEntity.AddRangeAsync(entries, cancellationToken);
        await pennylaneContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SirenResolution> ResolveBySirenAsync(string siren, CancellationToken cancellationToken = default)
    {
        var companyIds = await pennylaneContext.CompanyDirectoryEntity
            .AsNoTracking()
            .Where(e => e.Siren == siren)
            .Select(e => e.CompanyId)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (companyIds.Count == 0)
        {
            return new SirenResolution(SirenResolutionStatus.NotFound, null);
        }

        if (companyIds.Count > 1)
        {
            return new SirenResolution(SirenResolutionStatus.Ambiguous, null);
        }

        return new SirenResolution(SirenResolutionStatus.Found, companyIds[0]);
    }
}
