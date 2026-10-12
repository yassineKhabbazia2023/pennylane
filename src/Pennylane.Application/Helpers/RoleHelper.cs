using Application.Models.Accounts;
using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;

namespace Pennylane.Application.Helpers
{
    public static class RoleHelper
    {
        public static List<Role> GetRoles(string saasPlan)
        {
            return PennylaneRoles.RoleSaasDictionary.TryGetValue(saasPlan?.ToLower() ?? string.Empty, out var roles)
                ? roles
                : PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()] ;
        }
    }
}
