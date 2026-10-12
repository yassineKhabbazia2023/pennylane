using Pennylane.Application.Models;

namespace Pennylane.Application.Interfaces;

public interface ICompanyRepository
{
    Task<IEnumerable<PennylaneCompany>> GetAllCompaniesAsync();

    Task<PennylaneCompany?> GetCompanyByIdWithRetentionAsync(string companyId, int LastSyncDateRetentionDays);

    Task<PennylaneCompany?> GetCompanyByIdAsync(string companyId);

    Task<DateTime?> GetLastSyncDateAsync(string companyId);

    Task SaveCompaniesAsync(List<PennylaneCompany> companies);

    Task<string?> GetPennylaneInternalIdByACompanyIdAsync(string companyId);

    Task SavePennylaneInternalIdAsync(string companyId, int value);
}