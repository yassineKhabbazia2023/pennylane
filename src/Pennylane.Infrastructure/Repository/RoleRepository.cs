// <copyright file="RoleRepository.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pulse.ContactRegistry.Infrastructure.Context;
using Pennylane.Domain.Entities;
using Pennylane.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Pennylane.Infrastructure.Repository;

/// <summary>
/// ContactsRepository.
/// </summary>  
/// <param name="PennylaneContext">dbContext.</param>
public class RoleRepository(PennylaneContext PennylaneContext) : IRoleRepository
{
    public async Task AddRoleAsync(RoleEntity role)
    {
        var existingRole = PennylaneContext.RoleEntity.FirstOrDefault(
            r => r.ContactId == role.ContactId && r.AccountId == role.AccountId);
        if (existingRole == null)
        {
            PennylaneContext.RoleEntity.Add(role);
            await PennylaneContext.SaveChangesAsync();
        }
        else if (existingRole.IsSignatory != role.IsSignatory)
        {
            existingRole.IsSignatory = role.IsSignatory;
            PennylaneContext.RoleEntity.Update(existingRole);
            await PennylaneContext.SaveChangesAsync();
        }
    }

    public async Task<bool> CheckRoleExistsAsync(string accountNumber, string contactEmail)
    {
        return await PennylaneContext.RoleEntity.AsNoTracking()
            .Include(r => r.Contact)
            .Include(r => r.Account)
            .AnyAsync(r => r.Account.AccountNumber == accountNumber && r.Contact.Email == contactEmail);
    }

    public async Task DeleteRoleAsync(RoleEntity role)
    {
        PennylaneContext.RoleEntity.Remove(role);
        await PennylaneContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<RoleEntity>> GetUserRolesAsync(string contactEmail)
    {
        return await PennylaneContext.RoleEntity.AsNoTracking()
            .Include(r => r.Contact)
            .Include(r => r.Account)
            .Where(r => r.Contact.Email == contactEmail)
            .ToListAsync();
    }
}
