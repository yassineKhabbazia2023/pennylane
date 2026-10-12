using Application.Interfaces;
using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;
using Pennylane.Application.Helpers;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Providers;

public class HistoryEventPublisher(IAccountRepository accountRepository, IEventPublisher eventPublisher) : IHistoryEventPublisher
{
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;

    public async Task PublishHistoryCreatedEventAsync(SignatoryAccessImportRow row)
    {
        var details = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        await PublishAsync(row, ActionCodes.AVEACTIV.ToString(), details);
    }

    public async Task PublishHistoryDeletedEventAsync(SignatoryAccessEntity signatoryAccess)
    {
        var row = signatoryAccess.ToImportRow();

        await PublishAsync(row, ActionCodes.AVEDESAC.ToString(), null);
    }

    public async Task PublishHistoryUpdatedEventAsync(SignatoryAccessEntity oldEntity, SignatoryAccessImportRow newRow)
    {
        var details = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        await PublishAsync(newRow, ActionCodes.AVEMODIF.ToString(), details);
    }

    public async Task PublishHistoryValidatedEventAsync(SignatoryAccessEntity signatoryAccess, bool status)
    {
        var actionCode = status ? ActionCodes.AVEACCEP.ToString() : ActionCodes.AVEREFUS.ToString();
        var row = signatoryAccess.ToImportRow();
        var details = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        await PublishAsync(row, actionCode, details);
    }

    private async Task PublishAsync(SignatoryAccessImportRow row, string actionCode, string? details)
    {
        var account = await _accountRepository.GetAccountByNumberOrIdAsync(row.AccountNumber)
            ?? throw new NotFoundException(Errors.AccountNotFoundCode, string.Format(Errors.AccountNumberNotFoundMessage, row.AccountNumber));

        var data = new HistoryCreatedEventData
        {
            CreationDate = DateTime.UtcNow,
            Account = new AccountHistoryEventData
            {
                AccountId = account.AccountId,
                AccountNumber = account.AccountNumber,
                LegalName = account.LegalName
            },
            Action = new ActionHistoryEventData
            {
                Code = actionCode,
            },
            TargetUser = new UserHistoryEventData
            {
                Name = $"{row.FirstName} {row.LastName}".Trim(),
                Email = row.Email,
                UserType = ContactType.customer.GetDescription(),
            },
            Details = details
        };

        await _eventPublisher.PublishAsync(new HistoryCreatedEvent(data));
    }
}
