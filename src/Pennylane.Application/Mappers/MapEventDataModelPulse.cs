using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Application.Mappers;

public static class MapEventDataModelPulse
{
    public static RoleEntity MapToRoleEntity(this RoleCreatedEventData source)
    {
        return source == null ? null! : new RoleEntity
        {
            AccountId = source.AccountId,
            ContactId = source.ContactId,
            IsSignatory = source.IsSignatory,
        };
    }

    public static RoleEntity MapToRoleEntity(this RoleDeletedEventData source)
    {
        return source == null ? null! : new RoleEntity
        {
            AccountId = source.AccountId,
            ContactId = source.ContactId
        };
    }

    public static PnlRoleEntity MapToPnlRoleEntity(this RoleCreatedEventData? source)
    {
        return source == null ? null! : new PnlRoleEntity
        {
            AccountId = source.AccountId,
            ContactId = source.ContactId,
            Role = source.SubRole,
        };
    }
}
