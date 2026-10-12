namespace Pennylane.Application.Remediation;

public sealed record ResolutionResult(bool Found, string? CompanyId, string? Detail = null);
