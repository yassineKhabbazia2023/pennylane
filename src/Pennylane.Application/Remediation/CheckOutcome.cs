namespace Pennylane.Application.Remediation;

public enum CheckOutcome
{
    Compliant,
    Fixed,
    Failed,
}

public sealed record CheckResult(string CheckKey, CheckOutcome Outcome, string? Detail = null);
