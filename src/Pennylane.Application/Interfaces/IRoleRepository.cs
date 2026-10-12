// <copyright file="IRoleRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.Entities;

namespace Pennylane.Application.Interfaces;

public interface IRoleRepository
{ 
    Task AddRoleAsync(RoleEntity role);

    Task DeleteRoleAsync(RoleEntity role);

    Task<bool> CheckRoleExistsAsync(string accountNumber, string contactEmail);

    Task<IEnumerable<RoleEntity>> GetUserRolesAsync(string contactEmail);
}
