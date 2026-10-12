using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class CompanyRepository(PennylaneContext pennylaneContext) : ICompanyRepository
{
    public async Task<IEnumerable<PennylaneCompany>> GetAllCompaniesAsync()
    {
        var entities = await pennylaneContext.CompanyEntity
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return entities.Select(e => e.ToPennylaneCompany());
    }

    public async Task<DateTime?> GetLastSyncDateAsync(string companyId)
    {
        var companyEntity = await pennylaneContext.CompanyEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        return companyEntity?.LastSyncDate;
    }

    public Task SaveCompaniesAsync(List<PennylaneCompany> companies)
    {
        // update existing companies or add new ones
        foreach (var company in companies)
        {
            var entity = pennylaneContext.CompanyEntity
                .FirstOrDefault(c => c.Id == company.Id);
            if (entity != null)
            {
                // Update existing company
                entity.Name = company.Name;
                entity.LastSyncDate = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.SaasPlan = company.SaasPlan;
                entity.SupplierInvoiceTransmissionEmail = company.SupplierInvoiceTransmissionEmails?.FirstOrDefault();
                entity.CustomerInvoiceTransmissionEmail = company.CustomerInvoiceTransmissionEmails?.FirstOrDefault();
                entity.RegNo = company.RegNo;
                entity.FirmId = company.FirmId;
            }
            else
            {
                // Add new company
                pennylaneContext.CompanyEntity.Add(company.ToCompanyEntity());
            }
        }

        return pennylaneContext.SaveChangesAsync();
    }

    public async Task<string?> GetPennylaneInternalIdByACompanyIdAsync(string companyId)
    {
        var company = await pennylaneContext.CompanyEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        return company?.InternalId;
    }

    public async Task SavePennylaneInternalIdAsync(string companyId, int value)
    {
        var company = await pennylaneContext.CompanyEntity.FirstOrDefaultAsync(c => c.Id == companyId);
        if (company != null)
        {
            company.InternalId = value.ToString();
            company.UpdatedAt = DateTime.UtcNow;
            pennylaneContext.CompanyEntity.Update(company);
        }

        await pennylaneContext.SaveChangesAsync();
    }

    public async Task<PennylaneCompany?> GetCompanyByIdAsync(string companyId)
    {
        var entity = await pennylaneContext.CompanyEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId) ?? throw new KeyNotFoundException($"Company with ID {companyId} not found."); ;

        return entity?.ToPennylaneCompany();
    }

    public async Task<PennylaneCompany?> GetCompanyByIdWithRetentionAsync(string companyId, int LastSyncDateRetentionDays)
    {
        var retentionDate = DateTime.UtcNow.AddDays(-LastSyncDateRetentionDays);
        var entity = await pennylaneContext.CompanyEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.LastSyncDate >= retentionDate);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Company with ID {companyId} not found or last sync date is older than {LastSyncDateRetentionDays} days.");
        }

        return entity?.ToPennylaneCompany();
    }
}