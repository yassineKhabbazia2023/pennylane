using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Remediation;

public sealed class BySirenResolver(ICompanyDirectoryRepository companyDirectoryRepository) : ISubscriptionResolver
{
    public async Task<ResolutionResult> ResolveAsync(RemediationRequest request, CancellationToken cancellationToken)
    {
        var resolution = await companyDirectoryRepository.ResolveBySirenAsync(request.Siren, cancellationToken);

        return resolution.Status switch
        {
            SirenResolutionStatus.Found => new ResolutionResult(true, resolution.CompanyId),
            SirenResolutionStatus.Ambiguous => new ResolutionResult(false, null, "Siren resolution is ambiguous: several companies share this Siren."),
            _ => new ResolutionResult(false, null),
        };
    }
}
