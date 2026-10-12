using Pennylane.Application.Configurations.Options;
using Pennylane.Domain.ExternalApis.CommandParams;

namespace Pennylane.Application.Remediation.Checks;

/// <summary>
/// Builds the GrantUserAccess payload. UseUserIdForGrantAccess is not applied inside the API client: every caller must branch here.
/// </summary>
internal static class GrantAccessFactory
{
    internal static GrantUserAccess Build(PennylaneServiceOptions options, string role, string? userId, string email)
    {
        var grantAccess = new GrantUserAccess { Role = role };

        if (options.UseUserIdForGrantAccess)
        {
            grantAccess.UserId = userId;
        }
        else
        {
            grantAccess.UserEmail = email;
        }

        return grantAccess;
    }
}
