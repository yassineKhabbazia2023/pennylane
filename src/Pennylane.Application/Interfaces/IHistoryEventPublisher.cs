using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces;

public interface IHistoryEventPublisher
{
    Task PublishHistoryCreatedEventAsync(SignatoryAccessImportRow row);

    Task PublishHistoryDeletedEventAsync(SignatoryAccessEntity signatoryAccess);

    Task PublishHistoryUpdatedEventAsync(SignatoryAccessEntity oldEntity, SignatoryAccessImportRow newRow);

    Task PublishHistoryValidatedEventAsync(SignatoryAccessEntity signatoryAccess, bool status);
}
