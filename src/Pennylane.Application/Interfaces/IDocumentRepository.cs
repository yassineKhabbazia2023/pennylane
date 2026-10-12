using Pennylane.Application.Models.EmailRecap;
using Pennylane.Domain;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces
{
    public interface IDocumentRepository
    {
        /// <summary>
        /// Gets document counts grouped by account for documents created within the specified date range.
        /// Optimized query using GROUP BY on SQL side.
        /// </summary>
        /// <param name="fromUtc">Start date (UTC, inclusive).</param>
        /// <param name="toUtc">End date (UTC, exclusive).</param>
        /// <returns>Dictionary of account ID to document count information.</returns>
        Task<Dictionary<int, DocumentCountByAccount>> GetDocumentCountsByAccountAsync(DateTime fromUtc, DateTime toUtc);

        Task<AccountDocumentsPage> GetDocumentsAsync(string accountNumber, int pageRank, int pageSize, string? search = null, IReadOnlyCollection<string>? excludedExtensions = null);
        Task<Document> GetDocumentByIdAsync(string accountNumber, long documentId);

        /// <summary>
        /// Retrieves the documents matching the given identifiers for an account in a single query.
        /// </summary>
        /// <param name="accountNumber">The account number.</param>
        /// <param name="documentIds">The identifiers of the documents to retrieve.</param>
        /// <returns>The matching documents.</returns>
        Task<IReadOnlyList<Document>> GetDocumentsByIdsAsync(string accountNumber, IReadOnlyCollection<long> documentIds);
        Task<DocumentsSyncStatusEntity?> GetSyncStatusAsync(string accountNumber);
        Task<int> InsertPennylaneDocumentsAsync(string accountNumber, List<DocumentEntity> documents);
        Task AddOrUpdateSyncDateAsync(int accountId, string accountNumber);
        
        /// <summary>
        /// Deletes documents from the database based on their IDs.
        /// </summary>
        /// <param name="accountNumber"></param>
        /// <param name="documentIds"></param>
        /// <returns></returns>
        Task<int> HardDeleteDocumentsAsync(string accountNumber, IEnumerable<long> documentIds);
        
        /// <summary>
        /// Retrieves all documents for a given account number and path.
        /// </summary>
        /// <param name="accountNumber"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<IReadOnlyList<Document>> GetAllDocumentsByPathAsync(string accountNumber, string path);
    }
}