using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;
using Pennylane.Application.Models;
using Pennylane.WebApi.Models;

namespace Pennylane.Application.Helpers;

/// <summary>
/// Maps Offer PlanCodes to Pennylane SaasPlan names.
/// </summary>
public static class PennylaneInformationHelper
{
    private const string COLLABORATIVE_PLAN_CODE = "COLLABORATIVE";
    private const string STARTER_PLAN_CODE = "STARTER";
    private const string BASIC_PLAN_CODE = "BASIC";
    private const string ESSENTIAL_PLAN_CODE = "ESSENTIAL";
    private const string PREMIUM_PLAN_CODE = "PREMIUM";
    private const string PREMIUM_INTERNAL_PLAN_CODE = "PREMIUM_INTERNAL";
    public const string APPROVED_PLATFORM_PLAN_CODE = "APPROVED_PLATFORM";

    private const string UNLIMITED_USER_NUMBER = "unlimited";

    private static readonly Dictionary<string, string> Mapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { COLLABORATIVE_PLAN_CODE, SaasNames.v1_saas_free.GetDescription() },
        { STARTER_PLAN_CODE, SaasNames.v1_starter.GetDescription()  },
        { BASIC_PLAN_CODE, SaasNames.v1_basic.GetDescription() },
        { ESSENTIAL_PLAN_CODE, SaasNames.v1_essential.GetDescription() },
        { PREMIUM_PLAN_CODE, SaasNames.v1_pro.GetDescription() },
        { PREMIUM_INTERNAL_PLAN_CODE, SaasNames.v1_pro_internalized.GetDescription() },
        { APPROVED_PLATFORM_PLAN_CODE, SaasNames.v1_electronic_invoicing.GetDescription() },
    };

    /// <summary>
    /// Returns the expected Pennylane SaasPlan for a given Offer PlanCode.
    /// Returns null if the PlanCode has no known mapping.
    /// </summary>
    public static string? GetExpectedSaasPlan(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return null;
        }

        return Mapping.TryGetValue(planCode, out var saasPlan) ? saasPlan : null;
    }

    /// <summary>
    /// Returns the expected number of users for a given Offer PlanCode.
    /// Returns null if the PlanCode has no known mapping.
    /// </summary>
    public static string? GetExpectedUserNumber(string? userNumber, string? planCode)
    {
        if (APPROVED_PLATFORM_PLAN_CODE.Equals(planCode, StringComparison.OrdinalIgnoreCase))
        {
            return UNLIMITED_USER_NUMBER;
        }

        string? expectedNumber = null;

        if (userNumber == null)
        {
            return expectedNumber;
        }

        if (userNumber.Equals("Gratuit", StringComparison.InvariantCultureIgnoreCase))
        {
            expectedNumber = UNLIMITED_USER_NUMBER;
        }
        else
        {
            expectedNumber = userNumber.Split(' ')[0];
        }

        return expectedNumber;
    }

    public static int? GetExpectedEmployeesNumber(string? numberOfUsers, string? planCode)
    {
        if (planCode == null)
        {
            return null;
        }

        if (APPROVED_PLATFORM_PLAN_CODE.Equals(planCode, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return planCode == COLLABORATIVE_PLAN_CODE || planCode == STARTER_PLAN_CODE ?
            null : ParseNumberOfUser(numberOfUsers);
    }

    private static int? ParseNumberOfUser(string? numberOfUsers)
    {
        return int.TryParse(numberOfUsers, out var parsedEmployees) ? parsedEmployees : null;
    }

    public static string SetupStatus(CreateCompanyRequest request, IEnumerable<UserAdditionResult> userResults, bool companyAlreadyExists, bool createdOnDefault, PennylaneCompany company)
    {
        string status;
        bool hasFailedUsers = userResults.Any(r => !r.Success);

        if (hasFailedUsers)
        {
            status = PennylaneStatuses.PartialSuccess;
        }
        else if (companyAlreadyExists)
        {
            status = PennylaneStatuses.AlreadyExists;
        }
        else if (createdOnDefault)
        {
            status = PennylaneStatuses.CreatedOnDefault;
        }
        else
        {
            status = PennylaneStatuses.Created;
        }

        if (status is PennylaneStatuses.Created or PennylaneStatuses.CreatedOnDefault or PennylaneStatuses.AlreadyExists
            && !string.IsNullOrWhiteSpace(request.RequestedPlanCode)
            && !RequiresMandateSending(request.RequestedPlanCode))
        {
            var expectedSaasPlan = GetExpectedSaasPlan(request.RequestedPlanCode);
            if (expectedSaasPlan != null
                && string.Equals(expectedSaasPlan, company!.SaasPlan, StringComparison.OrdinalIgnoreCase))
            {
                status = PennylaneStatuses.Validated;
            }
        }

        return status;
    }

    private static bool RequiresMandateSending(string? planCode)
        => APPROVED_PLATFORM_PLAN_CODE.Equals(planCode, StringComparison.OrdinalIgnoreCase);
}