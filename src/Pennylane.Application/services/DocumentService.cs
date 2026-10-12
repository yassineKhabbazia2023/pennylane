using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Domain;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pulse.ExceptionMiddleware.Exceptions;
using System.IO.Compression;

namespace Pennylane.Application.Services;

public class DocumentService(IDocumentRepository documentRepository,
                             IPennylaneApiFirmService pennylaneApiFirmService,
                             IPennylaneApiGroupService pennylaneApiGroupService,
                             IDocumentStreamService documentStreamService,
                             IPennylaneService pennylaneService,
                             IOptions<DocumentFilteringOptions> documentFilteringOptions) : IDocumentService
{
    private IReadOnlyCollection<string> GetExcludedExtensions()
    {
        return documentFilteringOptions.Value.ExcludedExtensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .ToArray();
    }

    public async Task<IReadOnlyList<Document>> GetAllDocumentsByPathAsync(string accountNumber, string path)
    {
        return await documentRepository.GetAllDocumentsByPathAsync(accountNumber, path);
    }

    public async Task<Stream> GetDocumentContentStreamAsync(string accountNumber, long id)
    {
        // Check if the document exists
        var document = await documentRepository.GetDocumentByIdAsync(accountNumber, id);
        if (document == null)
        {
            return Stream.Null; // Return an empty stream if document not found
        }

        // If the document exists, proceed to get the company and file details
        var company = await pennylaneApiGroupService.TryGetCompanyByIdAsync(accountNumber);
        if (company == null)
        {
            return Stream.Null;
        }

        var firmId = company.FirmId;
        var pennylaneCompanyInternalId = await pennylaneService.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber);
        if (!pennylaneCompanyInternalId.HasValue)
        {
            return Stream.Null;
        }

        var dmsFiles = await pennylaneApiFirmService.GetCompanyDmsFilesAsync(firmId, pennylaneCompanyInternalId.Value, id.ToString());
        if (dmsFiles is not { Count: not 0 })
        {
            return Stream.Null;
        }

        var fileUrl = dmsFiles.First().Url;
        return await documentStreamService.GetStreamFromUrlAsync(fileUrl);
    }

    public async Task<Stream> GetDocumentsArchiveStreamAsync(string accountNumber, IReadOnlyCollection<long> ids)
    {
        // Batch-load the requested documents metadata in a single query (avoids the N+1 pattern).
        var documents = await documentRepository.GetDocumentsByIdsAsync(accountNumber, ids);
        if (documents.Count == 0)
        {
            return Stream.Null;
        }

        // Resolve the company / firm / Pennylane internal id once for the whole account instead of
        // per document. An account without a resolvable firm/token must fail explicitly (rule §9)
        // rather than silently producing an incomplete archive.
        var company = await pennylaneApiGroupService.TryGetCompanyByIdAsync(accountNumber)
            ?? throw new BadRequestException("PennylaneCompany", $"No Pennylane company resolved for account {accountNumber}.");

        var firmId = company.FirmId;
        var pennylaneCompanyInternalId = await pennylaneService.GetPennylaneInternalIdByAccountNumberAsync(firmId, accountNumber)
            ?? throw new BadRequestException("PennylaneCompany", $"No Pennylane internal company id resolved for account {accountNumber}.");

        var archiveStream = new MemoryStream();

        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                // One content fetch per document is unavoidable (each file is streamed from its own URL),
                // but the shared company/firm/token resolution above is done only once.
                var dmsFiles = await pennylaneApiFirmService.GetCompanyDmsFilesAsync(firmId, pennylaneCompanyInternalId, document.Id.ToString());
                if (dmsFiles is not { Count: not 0 })
                {
                    // Document-level absence (no file backing this document) is skipped, unlike a
                    // firm/token resolution failure which throws above.
                    continue;
                }

                await using var contentStream = await documentStreamService.GetStreamFromUrlAsync(dmsFiles.First().Url);
                if (contentStream == null || ReferenceEquals(contentStream, Stream.Null))
                {
                    continue;
                }

                var entryName = BuildUniqueEntryName(document.Name, document.Id, usedEntryNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await contentStream.CopyToAsync(entryStream);
            }
        }

        archiveStream.Position = 0;
        return archiveStream;
    }

    private static string BuildUniqueEntryName(string name, long id, HashSet<string> usedEntryNames)
    {
        var fileName = string.IsNullOrWhiteSpace(name) ? null : Path.GetFileName(name.Replace('\\', '/').Trim());
        var baseName = string.IsNullOrWhiteSpace(fileName) ? id.ToString() : fileName;
        var extension = Path.GetExtension(baseName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(baseName);

        var candidate = baseName;
        var counter = 1;
        while (!usedEntryNames.Add(candidate))
        {
            candidate = $"{nameWithoutExtension} ({counter}){extension}";
            counter++;
        }

        return candidate;
    }

    public async Task<AccountDocumentsPage> GetDocumentsAsync(string accountNumber, int pageRank, int pageSize, string? search = null, bool applyExtensionFilter = true)
    {
        var excludedExtensions = applyExtensionFilter ? GetExcludedExtensions() : null;
        return await documentRepository.GetDocumentsAsync(accountNumber, pageRank, pageSize, search, excludedExtensions);
    }

    public async Task<int> HardDeleteDocumentsAsync(string accountNumber, IEnumerable<long> documentIds)
    {
        return await documentRepository.HardDeleteDocumentsAsync(accountNumber, documentIds);
    }

    public async Task<int> InsertPennylaneDocumentsAsync(string accountNumber, List<PennylaneDmsFile> dmsFiles)
    {
        var excludedExtensions = GetExcludedExtensions();
        var documentEntities = dmsFiles
            .Where(f => !IsExcluded(f.Name, excludedExtensions))
            .Select(f => new DocumentEntity
            {
                Id = f.Id,
                Name = f.Name,
                Path = f.Path,
                Url = f.Url,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }).ToList();

        return await documentRepository.InsertPennylaneDocumentsAsync(accountNumber, documentEntities);
    }

    private static bool IsExcluded(string name, IReadOnlyCollection<string> excludedExtensions)
    {
        return !string.IsNullOrEmpty(name)
            && excludedExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}