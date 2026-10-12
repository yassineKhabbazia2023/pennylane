using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces;

public interface IComplianceEmailService
{
    Task SendAcceptanceEmailAsync(ComplianceEmailContext context, TermsEntity terms, CancellationToken cancellationToken = default);

    Task SendRefusalEmailAsync(ComplianceRefusalContext context, CancellationToken cancellationToken = default);

    Task SendCustomerRelationRefusalEmailAsync(CustomerRelationRefusalContext context, CancellationToken cancellationToken = default);
}
