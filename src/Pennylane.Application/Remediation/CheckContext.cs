namespace Pennylane.Application.Remediation;

public sealed record ExpectedContact(string Email, string? FirstName, string? LastName, string? FunctionName, int? FunctionId = null);

public sealed class CheckContext
{
    public required string ExpectedCompanyId { get; init; }

    public required string Siren { get; init; }

    public required string CompanyId { get; set; }

    public required string FirmId { get; init; }

    public string? AccountingManagerId { get; init; }

    public string? AccountingSupervisorId { get; init; }

    public string? AccountantId { get; init; }

    public required IReadOnlyList<ExpectedContact> Collaborators { get; init; }

    public required IReadOnlyList<ExpectedContact> Signatories { get; init; }
}
