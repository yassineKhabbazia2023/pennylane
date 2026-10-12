namespace Pennylane.Application.Models;

public enum SirenResolutionStatus
{
    NotFound,
    Found,
    Ambiguous,
}

public sealed record SirenResolution(SirenResolutionStatus Status, string? CompanyId);
