namespace Pennylane.Application.Models.Signatory;

public class SignatoryAccount
{
    public required string AccountNumber { get; set; }

    public required string LegalName { get; set; }

    public string? PlanName { get; set; }

    public decimal? PlanPriceExclTax { get; set; }

    public decimal? CompliancePriceExclTax { get; set; }
}
