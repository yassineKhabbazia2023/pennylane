using System.Threading.Tasks;
using System.Collections.Generic;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;

namespace Pennylane.Application.Interfaces;

public interface IPennylaneApiFirmService
{
    Task<List<PennylaneDmsFile>> GetCompanyDmsFilesAsync(string firmId, int pennylaneInternalcompanyId, string? fileId = null);
    Task<int?> GetPennylaneInternalIdByAccountNumberAsync(string firmId, string accountNumber);
}
