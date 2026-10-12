using Pennylane.Domain.Enums;

namespace Pennylane.Application.Models.Signatory;

public class ComplianceEmailContext
{
    public required string RecipientEmail { get; init; }
    public required string UserFirstName { get; init; }
    public required string UserLastName { get; init; }
    public required IReadOnlyList<ComplianceCompanyInfo> Companies { get; init; }
    public string? FreeTrialEndDate { get; init; }
    public required bool IsNewComplianceFlow { get; init; }
    public bool? IsFreeTrialExpired { get; init; }
    public string? Wave { get; init; }
}

public class ComplianceCompanyInfo
{
    public required string AccountNumber { get; init; }
    public required string Name { get; init; }
    public required decimal? CompliancePriceExclTax { get; init; }
    public required decimal? SubscriptionPriceExclTax { get; init; }
}

public class ComplianceRefusalContext
{
    public required string RecipientEmail { get; init; }
    public string? UserFirstName { get; init; }
    public string? UserLastName { get; init; }
    public string? FreeTrialEndDate { get; init; }
    public required bool IsNewComplianceFlow { get; init; }
    public bool? IsFreeTrialExpired { get; init; }
    public string? Wave { get; init; }
}

public class CustomerRelationRefusalContext
{
    public required string UserEmail { get; init; }
    public required string UserFirstName { get; init; }
    public required string UserLastName { get; init; }
    public required IEnumerable<string> AccountNumbers { get; init; }
}
