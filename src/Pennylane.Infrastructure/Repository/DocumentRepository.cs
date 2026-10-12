using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.EmailRecap;
using Pennylane.Domain;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Repository;

public class DocumentRepository(PennylaneContext pennylaneContext) : IDocumentRepository
{
    public async Task<AccountDocumentsPage> GetDocumentsAsync(string accountNumber, int pageRank, int pageSize, string? search = null, IReadOnlyCollection<string>? excludedExtensions = null)
    {
        var accountDocuments =
            pennylaneContext.DocumentEntity
                .Where(d => d.Account!.AccountNumber == accountNumber);

        if (!string.IsNullOrWhiteSpace(search))
        {
            accountDocuments = accountDocuments.Where(d => d.Name.Contains(search));
        }

        var extensions = excludedExtensions?
            .Where(e => !string.IsNullOrEmpty(e))
            .ToList();

        if (extensions is { Count: > 0 })
        {
            foreach (var extension in extensions)
            {
                accountDocuments = accountDocuments.Where(d => !d.Name.EndsWith(extension));
            }
        }

        var syncStatus = await pennylaneContext.DocumentsSyncStatusEntity.AsNoTracking()
           .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber);

        DateTime? accountLastSynchroDate = null;
        if (syncStatus != null && syncStatus.LastSynchro != default)
        {
            accountLastSynchroDate = syncStatus.LastSynchro;
        }

        var documents =
            await accountDocuments
                .AsNoTracking()
                .OrderByDescending(document => document.UpdatedAt)
                .Skip((pageRank - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

        var totalCount = await accountDocuments.CountAsync();

        return new AccountDocumentsPage(pageRank, pageSize, totalCount,
            documents.Select(r => ToDocument(r)).ToList(), accountLastSynchroDate);
    }

    public async Task<Document> GetDocumentByIdAsync(string accountNumber, long documentId)
    {
        var document = await pennylaneContext.DocumentEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Account.AccountNumber == accountNumber && d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found for account {accountNumber}.");
        }

        return ToDocument(document);
    }

    public async Task<IReadOnlyList<Document>> GetDocumentsByIdsAsync(string accountNumber, IReadOnlyCollection<long> documentIds)
    {
        if (documentIds is not { Count: not 0 })
        {
            return Array.Empty<Document>();
        }

        var entities = await pennylaneContext.DocumentEntity
            .AsNoTracking()
            .Where(d => d.Account.AccountNumber == accountNumber && documentIds.Contains(d.Id))
            .ToListAsync();

        return entities.Select(d => ToDocument(d)).ToList();
    }

    public async Task<DocumentsSyncStatusEntity?> GetSyncStatusAsync(string accountNumber)
    {
        return await pennylaneContext.DocumentsSyncStatusEntity.AsNoTracking()
            .Include(d => d.Account)
            .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber);
    }

    public async Task AddOrUpdateSyncDateAsync(int accountId, string accountNumber)
    {
        var syncStatus = await pennylaneContext.DocumentsSyncStatusEntity
            .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber);

        if (syncStatus == null)
        {
            syncStatus = new DocumentsSyncStatusEntity
            {
                AccountId = accountId,
                AccountNumber = accountNumber,
                LastSynchro = DateTime.UtcNow
            };
            await pennylaneContext.DocumentsSyncStatusEntity.AddAsync(syncStatus);
        }
        else
        {
            syncStatus.LastSynchro = DateTime.UtcNow;
            pennylaneContext.DocumentsSyncStatusEntity.Update(syncStatus);
        }

        await pennylaneContext.SaveChangesAsync();
    }

    public async Task<int> InsertPennylaneDocumentsAsync(string accountNumber, List<DocumentEntity> documents)
    {
        // Récupérer l'AccountEntity
        var account = await pennylaneContext.AccountEntity.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        if (account == null)
            throw new InvalidOperationException($"Account {accountNumber} not found");

        // Récupérer les fileIds déjà présents
        var existingFileIds = await pennylaneContext.DocumentEntity
            .Where(d => d.AccountId == account.AccountId)
            .Select(d => d.Id)
            .ToListAsync();

        // Filtrer les nouveaux documents à insérer
        var newDocs = documents.Where(d => !existingFileIds.Contains(d.Id)).ToList();
        newDocs.ForEach(d => d.AccountId = account.AccountId);
        if (newDocs.Count > 0)
        {
            await pennylaneContext.DocumentEntity.AddRangeAsync(newDocs);
            await pennylaneContext.SaveChangesAsync();
        }

        return newDocs.Count; // Retourne le nombre de documents ajoutés
    }

    public async Task<int> HardDeleteDocumentsAsync(string accountNumber, IEnumerable<long> documentIds)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return 0;

        var ids = documentIds?.Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0)
            return 0;

        var accountId = await pennylaneContext.AccountEntity
            .Where(a => a.AccountNumber == accountNumber)
            .Select(a => a.AccountId)
            .SingleOrDefaultAsync();

        if (accountId == 0)
            return 0;

        var deleted = await pennylaneContext.DocumentEntity
            .Where(d => d.AccountId == accountId && ids.Contains(d.Id))
            .ExecuteDeleteAsync();

        return deleted;
    }


    /// <summary>
    /// Retrieves all documents for a given account number where the path starts with the specified path (recursive).
    /// If path is empty or whitespace, returns all documents for the account.
    /// </summary>
    public async Task<IReadOnlyList<Document>> GetAllDocumentsByPathAsync(string accountNumber, string path)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Array.Empty<Document>();

        var query = pennylaneContext.DocumentEntity
            .AsNoTracking()
            .Include(d => d.Account)
            .Where(d => d.Account != null && d.Account.AccountNumber == accountNumber);

        // If path is provided and not empty, filter by path prefix
        if (!string.IsNullOrWhiteSpace(path))
        {
            query = query.Where(d => d.Path.StartsWith(path));
        }

        var entities = await query
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return entities.Select(d => ToDocument(d)).ToList();
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, DocumentCountByAccount>> GetDocumentCountsByAccountAsync(DateTime fromUtc, DateTime toUtc)
    {
        var results = await pennylaneContext.DocumentEntity
            .AsNoTracking()
            .Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc)
            .GroupBy(d => new { d.AccountId, d.Account!.AccountNumber, d.Account.LegalName })
            .Select(g => new DocumentCountByAccount(
                g.Key.AccountId,
                g.Key.AccountNumber,
                g.Key.LegalName,
                g.Count()))
            .ToListAsync();

        return results.ToDictionary(r => r.AccountId);
    }

    private static Document ToDocument(DocumentEntity documentEntity)
    {
        return new Document(documentEntity.Id, documentEntity.Name, documentEntity.Path, documentEntity.Url, documentEntity.CreatedAt, documentEntity.UpdatedAt);
    }
}