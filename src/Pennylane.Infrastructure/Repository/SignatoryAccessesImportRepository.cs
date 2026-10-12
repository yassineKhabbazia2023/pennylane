using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ContactRegistry.Infrastructure.Context;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Repository;

public class SignatoryAccessesImportRepository(PennylaneContext context) : ISignatoryAccessesImportRepository
{
    private readonly PennylaneContext _context = context;

    public async Task InsertSignatoryAccessAsync(SignatoryAccessEntity entity)
    {
        await _context.SignatoryAccessEntity.AddAsync(entity);
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public async Task<List<SignatoryAccessEntity>> GetAllSignatoryAccessAsync()
    {
        var list = await _context.SignatoryAccessEntity
            .AsNoTracking()
            .ToListAsync();

        return list;
    }

    public void UpdateSignatoryAccess(SignatoryAccessEntity entity)
    {
        var existing = _context.SignatoryAccessEntity
            .FirstOrDefault(x => x.AccountNumber == entity.AccountNumber);

        if (existing is null)
        {
            _context.SignatoryAccessEntity.Add(entity);
            return;
        }

        existing.Email = entity.Email;
        existing.LastName = entity.LastName;
        existing.FirstName = entity.FirstName;
        existing.PlanName = entity.PlanName;
        existing.UserCount = entity.UserCount;
        existing.PlanPriceExclTax = entity.PlanPriceExclTax;
        existing.CompliancePriceExclTax = entity.CompliancePriceExclTax;
        existing.CanAccess = entity.CanAccess;
        existing.CreationDate = entity.CreationDate;
        existing.FreeTrialEndDate = entity.FreeTrialEndDate;
        existing.TermsId = entity.TermsId;
        existing.Wave = entity.Wave;
    }

    public async Task<SignatoryAccessEntity> GetSignatoryAccessByAccountNumberAsync(string accountNumber)
    {
        return await _context.SignatoryAccessEntity.Include(s => s.Terms).FirstOrDefaultAsync(sa => sa.AccountNumber == accountNumber)
            ?? throw new NotFoundException(Errors.SignatoryAccessNotFoundCode, string.Format(Errors.SignatoryAccessNotFoundMessage, accountNumber));
    }

    public async Task<IEnumerable<Tuple<SignatoryAccessEntity, string>>> GetSignatoryAccessesByUserEmailAsync(string userEmail)
    {
        var result = await _context.SignatoryAccessEntity.AsNoTracking().Include(sa => sa.Terms)
                .Join(_context.AccountEntity,
                      sa => sa.AccountNumber,
                      a => a.AccountNumber,
                      (sa, a) => new
                      {
                          SignatoryAccess = sa,
                          a.LegalName
                      })
                .Where(x => x.SignatoryAccess.Email == userEmail)
            .ToListAsync();

        // Account.Accounts n'a pas de contrainte d'unicité sur AccountNumber :
        // un dossier dupliqué en base multiplierait les lignes du JOIN (doublons d'affichage).
        return result
            .GroupBy(s => s.SignatoryAccess.AccountNumber)
            .Select(g => Tuple.Create(g.First().SignatoryAccess, g.First().LegalName))
            .ToList();
    }

    public void ValidateTerms(string accountNumber, int termsId, bool status, SignatoryAccessEntity sourceData)
    {
        var history = new ValidationHistoryEntity
        {
            AccountNumber = accountNumber,
            TermsId = termsId,
            Status = status,
            Email = sourceData.Email,
            LastName = sourceData.LastName,
            FirstName = sourceData.FirstName,
            PlanName = sourceData.PlanName,
            UserCount = sourceData.UserCount,
            PlanPriceExclTax = sourceData.PlanPriceExclTax,
            CompliancePriceExclTax = sourceData.CompliancePriceExclTax,
        };

        _context.ValidationHistoryEntity.Add(history);
    }

    public async Task DeleteSignatoryAccessesAsync(IEnumerable<string> accountNumbers)
    {
        var accountNumbersList = accountNumbers.ToList();
        if (accountNumbersList.Count > 0)
        {
            await _context.SignatoryAccessEntity
                .Where(x => accountNumbersList.Contains(x.AccountNumber))
                .ExecuteDeleteAsync();
        }
    }

    public async Task<int> GetTermsIdByLinkFrAsync(string linkFr)
    {
        var term = await _context.TermsEntity.AsNoTracking()
        .Where(t => t.LinkFr == linkFr)
        .Select(t => new { t.Id })
        .FirstOrDefaultAsync();

        return term is null ? throw new NotFoundException(Errors.TermsNotFoundCode, string.Format(Errors.TermsNotFoundMessage, linkFr)) : term.Id;
    }

    public async Task<IEnumerable<Tuple<SignatoryAccessEntity, string>>> GetSignatoryAccessesByAccountNumbersAsync(IEnumerable<string> accountNumbers)
    {
        if (!accountNumbers.Any())
        {
            return [];
        }

        var result = await _context.SignatoryAccessEntity
            .AsNoTracking()
            .Include(sa => sa.Terms)
            .Join(_context.AccountEntity,
                      sa => sa.AccountNumber,
                      a => a.AccountNumber,
                      (sa, a) => new
                      {
                          SignatoryAccess = sa,
                          a.LegalName
                      })
            .Where(sa => accountNumbers.Contains(sa.SignatoryAccess.AccountNumber))
            .ToListAsync();

        // Account.Accounts n'a pas de contrainte d'unicité sur AccountNumber :
        // un dossier dupliqué en base multiplierait les lignes du JOIN (doublons d'affichage).
        return result
            .GroupBy(s => s.SignatoryAccess.AccountNumber)
            .Select(g => Tuple.Create(g.First().SignatoryAccess, g.First().LegalName))
            .ToList();
    }
}
