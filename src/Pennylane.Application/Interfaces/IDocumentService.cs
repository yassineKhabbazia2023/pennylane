using Pennylane.Domain;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;

namespace Pennylane.Application.Interfaces;

public interface IDocumentService
{
    Task<Stream> GetDocumentContentStreamAsync(string accountNumber, long id);

    /// <summary>
    /// Builds a zip archive stream containing the content of the given documents.
    /// </summary>
    /// <param name="accountNumber">The account number.</param>
    /// <param name="ids">The identifiers of the documents to include in the archive.</param>
    /// <returns>A readable stream positioned at the beginning of the generated zip archive.</returns>
    Task<Stream> GetDocumentsArchiveStreamAsync(string accountNumber, IReadOnlyCollection<long> ids);

    /// <summary>
    /// Gets a paginated page of documents for an account.
    /// </summary>
    /// <param name="applyExtensionFilter">
    /// When true (default), documents whose extension is configured as excluded are hidden.
    /// Set to false for maintenance flows (e.g. purge) that must see every document.
    /// </param>
    Task<AccountDocumentsPage> GetDocumentsAsync(string accountNumber, int pageRank, int pageSize, string? search = null, bool applyExtensionFilter = true);
    Task<int> InsertPennylaneDocumentsAsync(string accountNumber, List<PennylaneDmsFile> dmsFiles);
    
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