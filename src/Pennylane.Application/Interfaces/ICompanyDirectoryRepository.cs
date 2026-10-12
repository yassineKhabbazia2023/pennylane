using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces;

public interface ICompanyDirectoryRepository
{
    Task ReplaceAllAsync(IReadOnlyList<CompanyDirectoryEntity> entries, CancellationToken cancellationToken = default);

    Task<SirenResolution> ResolveBySirenAsync(string siren, CancellationToken cancellationToken = default);
}
