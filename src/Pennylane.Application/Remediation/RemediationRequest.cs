namespace Pennylane.Application.Remediation;

public sealed record RemediationRequest(
    int SubscriptionId,
    string ExpectedCompanyId,
    string Siren,
    string CurrentStatus,
    IReadOnlyList<ExpectedContact> Collaborators,
    IReadOnlyList<ExpectedContact> Signatories);
