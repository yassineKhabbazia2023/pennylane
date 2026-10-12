// <copyright file="MapPnlRoles.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.Pennylane;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Mappers
{
    public static class MapPnlRoles
    {
        public static PnlRoleEntity MapPnlRoleModelToEntity(this PnlRole pnlRole) =>
            new()
            {
                AccountId = pnlRole.AccountId,
                ContactId = pnlRole.ContactId,
                Role = pnlRole.Role,
            };

        public static PnlRole MapPnlRoleModelToEntity(this PnlRoleEntity pnlRoleEntity) =>
            new()
            {
                AccountId = pnlRoleEntity.AccountId,
                ContactId = pnlRoleEntity.ContactId,
                Role = pnlRoleEntity.Role,
            };
    }
}
