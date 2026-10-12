using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Application.Remediation.Checks;

/// <summary>
/// Resolves the Pennylane role to grant to a signatory: OverrideClientRole, else the SaaS plan mapping, else DefaultRole.
/// Deliberate copy of PennylaneService.GetClientRoleAsync — keep both in sync.
/// </summary>
internal static class ClientRoleResolver
{
    internal static async Task<string> ResolveAsync(
        IPennylaneApiGroupService pennylaneApiGroupService,
        PennylaneServiceOptions options,
        string companyId)
    {
        if (!string.IsNullOrWhiteSpace(options.OverrideClientRole))
        {
            return options.OverrideClientRole;
        }

        if (string.IsNullOrWhiteSpace(options.DefaultRole))
        {
            throw new BadRequestException(nameof(PennylaneServiceOptions), message: "Default role is not configured in PennylaneServiceOptions.");
        }

        if (options.PlansRoleAssignments == null || options.PlansRoleAssignments.Count == 0)
        {
            return options.DefaultRole;
        }

        try
        {
            var company = await pennylaneApiGroupService.GetCompanyByIdAsync(companyId);
            var rolesMapping = options.PlansRoleAssignments
                .FirstOrDefault(x => x.Plan is not null && x.Plan.Equals(company.SaasPlan, StringComparison.OrdinalIgnoreCase));

            if (rolesMapping is not null && !string.IsNullOrWhiteSpace(rolesMapping.ClientRole))
            {
                return rolesMapping.ClientRole;
            }

            return options.DefaultRole;
        }
        catch (Exception)
        {
            return options.DefaultRole;
        }
    }
}
