using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.StaticValues;
using Pennylane.Domain;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Infrastructure.Handlers;

public class DocumentSyncEventHandler(
    ILogger<DocumentSyncEventHandler> logger,
    IOptions<DocumentSyncOptions> syncOptions,
    IPennylaneApiGroupService pennylaneApiGroupService,
    IPennylaneApiFirmService pennylaneApiFirmService,
    IDocumentService documentService,
    IPennylaneService pennylaneService,
    INotificationManager notificationManager) : IEventHandler
{

    private const int DeleteChunkSize = 500;

    public async Task HandleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            logger.LogError("[DocSync] [ERREUR] Message vide pour la synchronisation des documents");
            return;
        }
        var payload = JsonDefaults.TryDeserialize<PennylaneDocumentSyncEvent>(message);
        if (payload?.Data == null || string.IsNullOrEmpty(payload.Data.AccountNumber) || payload.Data.AccountId == default)
        {
            logger.LogError("[DocSync] [ERREUR] Format de message invalide pour DocumentSyncEventHandler: {Message}", message);
            return;
        }
        var company = await pennylaneService.GetCompanyByIdAsync(payload.Data.AccountNumber);
        if (company == null)
        {
            logger.LogWarning("[DocSync] Aucune company trouvée pour AccountNumber {AccountNumber}", payload.Data.AccountNumber);
            return;
        }
        var firmId = company.FirmId;
        var pennylaneCompanyInternalId = await pennylaneService.GetPennylaneInternalIdByAccountNumberAsync(firmId, payload.Data.AccountNumber);
        if (!pennylaneCompanyInternalId.HasValue)
        {
            logger.LogWarning("[DocSync] Aucun ID interne Pennylane n'a été trouvé pour AccountNumber {AccountNumber}", payload.Data.AccountNumber);
            return;
        }
        List<PennylaneDmsFile>? dmsFiles;
        try
        {
            dmsFiles = await pennylaneApiFirmService.GetCompanyDmsFilesAsync(firmId, pennylaneCompanyInternalId.Value);
        }
        catch (RequestHttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("[DocSync] 404 pour InternalId {InternalId}, FirmId {FirmId}, AccountNumber {AccountNumber}. Refresh company + internalId.",
                pennylaneCompanyInternalId.Value, firmId, payload.Data.AccountNumber);

            var refreshedCompany = await pennylaneService.RefreshCompanyFromApiAsync(payload.Data.AccountNumber);
            var newFirmId = refreshedCompany.FirmId;

            var newInternalId = await pennylaneService.GetPennylaneInternalIdByAccountNumberAsync(
                newFirmId, payload.Data.AccountNumber, forceRefresh: true);

            if (!newInternalId.HasValue)
            {
                logger.LogWarning("[DocSync] Apres refresh, aucun ID interne pour AccountNumber {AccountNumber}", payload.Data.AccountNumber);
                return;
            }

            dmsFiles = await pennylaneApiFirmService.GetCompanyDmsFilesAsync(newFirmId, newInternalId.Value);
        }

        dmsFiles ??= [];

        // Get expanded paths from configuration
        var allowedPaths = syncOptions.Value.GetExpandedPaths();

        // Filter files based on configured paths (recursive matching)
        var filteredDmsFiles = FilterPublicationFiles(dmsFiles, allowedPaths);
        var documentsAdded = await documentService.InsertPennylaneDocumentsAsync(payload.Data.AccountNumber, filteredDmsFiles);
        logger.LogInformation("[DocSync] La synchronisation des documents est terminée pour AccountNumber {AccountNumber}. Chemins configurés: {PathCount}", payload.Data.AccountNumber, allowedPaths.Count);

        var documentsRemoved = await StartDeletionWorkflowAsync(accountNumber: payload.Data.AccountNumber, filteredDmsFiles: filteredDmsFiles, allowedPaths: allowedPaths);
        logger.LogInformation("[DocSync] La suppression du delta des documents est terminée pour AccountNumber {AccountNumber}", payload.Data.AccountNumber);

        // Publier l'événement après synchronisation réussie
        if (documentsAdded > 0) // Vérifier que l'insertion s'est bien passée
        {
            await PublishSyncCompletedEventAsync(
                payload.Data.AccountNumber,
                payload.Data.AccountId,
                documentsAdded,
                documentsRemoved);
        }
    }

    /// <summary>
    /// Orchestrates the deletion steps right after the insert workflow is completed.
    /// Skips deletion if the remote filtered list is empty (AC safety).
    /// </summary>
    private async Task<int> StartDeletionWorkflowAsync(string accountNumber, List<PennylaneDmsFile> filteredDmsFiles, List<string> allowedPaths)
    {
        if (ShouldSkipDeletionWhenRemoteEmpty(filteredDmsFiles, accountNumber))
            return 0;

        var localDocs = await LoadLocalDocumentsAsync(accountNumber, allowedPaths);

        var idsToDelete = ComputeDeltaIdsToDelete(filteredDmsFiles, localDocs);
        if (idsToDelete.Length == 0)
        {
            logger.LogInformation("[DocSync] Aucun document à supprimer. Local={Local}, Remote={Remote}, AccountNumber={AccountNumber}",
                localDocs.Count, filteredDmsFiles.Count, accountNumber);
            return 0;
        }

        LogDeletionPlanIds(idsToDelete, accountNumber);

        var deleted = await ExecuteHardDeletesAsync(accountNumber, idsToDelete);
        logger.LogInformation("[DocSync] Suppression terminée. Supprimés={Deleted} / Locaux={Local} vs Distants={Remote}. AccountNumber={AccountNumber}",
            deleted, localDocs.Count, filteredDmsFiles.Count, accountNumber);
        return deleted;
    }

    /// <summary>
    /// Do not delete if remote filtered list is empty — avoid mass accidental deletion.
    /// </summary>
    private bool ShouldSkipDeletionWhenRemoteEmpty(List<PennylaneDmsFile> filteredDmsFiles, string accountNumber)
    {
        if (filteredDmsFiles.Count > 0) return false;

        logger.LogWarning("[DocSync] Liste remote (Publications) vide. Suppression ignorée pour éviter une suppression totale. AccountNumber {AccountNumber}", accountNumber);
        return true;
    }

    /// <summary>
    /// Loads local documents scoped to the configured paths.
    /// Uses a hybrid approach: extracts common prefixes from patterns (before wildcards),
    /// loads documents by prefix at DB level, then filters wildcards in-memory.
    /// This optimizes performance for accounts with many documents.
    /// </summary>
    private async Task<IReadOnlyList<Document>> LoadLocalDocumentsAsync(string accountNumber, List<string> allowedPaths)
    {
        if (allowedPaths == null || allowedPaths.Count == 0)
        {
            return Array.Empty<Document>();
        }

        // Extract prefixes from patterns (part before first wildcard)
        var prefixes = ExtractPrefixesFromPatterns(allowedPaths);

        // Load documents from DB filtered by prefixes (reduces memory footprint)
        var allLocalDocs = new List<Document>();
        foreach (var prefix in prefixes)
        {
            var docs = await documentService.GetAllDocumentsByPathAsync(accountNumber, prefix);
            if (docs != null && docs.Count > 0)
            {
                allLocalDocs.AddRange(docs);
            }
        }

        if (allLocalDocs.Count == 0)
        {
            return Array.Empty<Document>();
        }

        // Remove duplicates (in case prefixes overlap)
        var uniqueDocs = allLocalDocs.DistinctBy(d => d.Id).ToList();

        // Filter documents in-memory using PathMatchesPattern for wildcard support
        var filteredDocs = uniqueDocs
            .Where(doc => allowedPaths.Any(allowedPath => PathMatchesPattern(doc.Path, allowedPath)))
            .ToList();

        return filteredDocs;
    }

    /// <summary>
    /// Extracts prefixes from patterns to optimize DB queries.
    /// For patterns without wildcards, returns the full path.
    /// For patterns with wildcards, returns the part before the first wildcard.
    /// Groups by common prefixes to minimize DB calls.
    /// </summary>
    /// <example>
    /// Input: ["/Dossier permanent/00 - Publications", "/Dossier annuel/Exercice */FEC", "/Dossier annuel/Exercice */Fiscal"]
    /// Output: ["/Dossier permanent/00 - Publications", "/Dossier annuel/Exercice"]
    /// </example>
    private static List<string> ExtractPrefixesFromPatterns(List<string> patterns)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            // Find the position of the first wildcard
            var wildcardIndex = pattern.IndexOf('*');

            if (wildcardIndex == -1)
            {
                // No wildcard: use the full path as prefix
                prefixes.Add(pattern);
            }
            else
            {
                // Has wildcard: extract everything before the wildcard (trimmed)
                // Example: "/Dossier annuel/Exercice */FEC" -> "/Dossier annuel/Exercice"
                var prefix = pattern.Substring(0, wildcardIndex).TrimEnd();
                if (!string.IsNullOrEmpty(prefix))
                {
                    prefixes.Add(prefix);
                }
            }
        }

        // If no valid prefixes found, return empty string to load all documents (fallback)
        return prefixes.Count > 0 ? prefixes.ToList() : new List<string> { string.Empty };
    }

    /// <summary>
    /// Computes the delta (local - remote) by Id (Assumption A: local Id == Pennylane Id).
    /// </summary>
    private static long[] ComputeDeltaIdsToDelete(List<PennylaneDmsFile> filteredDmsFiles, IReadOnlyList<Document> localDocs)
    {
        var remoteIds = new HashSet<long>(filteredDmsFiles.Select(x => x.Id));
        return localDocs.Select(d => d.Id).Except(remoteIds).ToArray();
    }

    /// <summary>
    /// Filters files based on configured paths using recursive matching (StartsWith).
    /// A file is included if its path starts with any of the allowed paths.
    /// </summary>
    private static List<PennylaneDmsFile> FilterPublicationFiles(IEnumerable<PennylaneDmsFile> files, List<string> allowedPaths)
    {
        // Ensure files is not null before filtering
        if (files == null || !files.Any())
        {
            return new List<PennylaneDmsFile>();
        }

        // If no paths configured, return empty list (fail-safe)
        if (allowedPaths == null || allowedPaths.Count == 0)
        {
            return new List<PennylaneDmsFile>();
        }

        // Filter files: keep if path matches any allowed path pattern (supports * wildcard)
        return files.Where(file =>
            allowedPaths.Any(allowedPath =>
                PathMatchesPattern(file.Path, allowedPath)))
            .ToList();
    }

    /// <summary>
    /// Checks if a file path matches a pattern (supports * wildcard and recursive matching).
    /// Examples:
    /// - Pattern "/Dossier permanent/00 - Publications" matches "/Dossier permanent/00 - Publications/file.pdf"
    /// - Pattern "/Dossier annuel/Exercice */FEC" matches "/Dossier annuel/Exercice 2023/FEC/file.pdf"
    /// </summary>
    private static bool PathMatchesPattern(string filePath, string pattern)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // If pattern has no wildcard, use simple StartsWith (recursive matching)
        if (!pattern.Contains('*'))
        {
            return filePath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Convert pattern to regex: escape special chars, then replace \* with [^/]+
        // [^/]+ means "one or more characters that are not /"
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "[^/]+");

        try
        {
            // Check if filePath starts with a string matching the pattern
            // We progressively build the path and test if it matches the full pattern
            // Use static Regex.IsMatch() which uses an internal cache (up to 15 entries)
            var parts = filePath.Split('/');

            for (int i = 0; i <= parts.Length; i++)
            {
                var currentPath = string.Join('/', parts.Take(i));
                if (Regex.IsMatch(currentPath, regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    // The pattern is matched at this point, so filePath starts with a matching prefix
                    return true;
                }
            }

            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // If regex matching times out, return false for safety
            return false;
        }
    }

    /// <summary>
    /// Logs a single, compact deletion plan: joined IDs and total count.
    /// </summary>
    private void LogDeletionPlanIds(IEnumerable<long> idsToDelete, string accountNumber)
    {
        var idsArray = idsToDelete as long[] ?? idsToDelete.ToArray();
        var count = idsArray.Length;

        string deletedDocsIds = string.Join(';', idsArray.OrderBy(i => i));

        logger.LogInformation("[DocSync] Suppression planifiée — Ids={IdsPreview} (Count={Count}), AccountNumber={AccountNumber}",
            deletedDocsIds, count, accountNumber);
    }

    /// <summary>
    /// Executes hard deletes in batches and returns the total deleted count.
    /// </summary>
    private async Task<int> ExecuteHardDeletesAsync(string accountNumber, long[] idsToDelete)
    {
        var totalDeleted = 0;
        foreach (var chunk in Chunk(idsToDelete, DeleteChunkSize))
        {
            var deleted = await documentService.HardDeleteDocumentsAsync(
                accountNumber,
                chunk);

            totalDeleted += deleted;
        }
        return totalDeleted;
    }

    /// <summary>
    /// Simple chunking helper for batch deletes.
    /// </summary>
    private static IEnumerable<IEnumerable<long>> Chunk(long[] ids, int size)
    {
        if (size <= 0) size = ids.Length;
        for (int i = 0; i < ids.Length; i += size)
            yield return ids.Skip(i).Take(size);
    }

    /// <summary>
    /// Publie l'événement de synchronisation terminée.
    /// </summary>
    private async Task PublishSyncCompletedEventAsync(
        string accountNumber,
        int accountId,
        int documentsAdded,
        int documentsRemoved)
    {
        try
        {
            var eventData = new PennylaneDocumentSyncCompletedEventData
            {
                AccountNumber = accountNumber,
                AccountId = accountId,
                DocumentsAdded = documentsAdded,
                DocumentsRemoved = documentsRemoved,
                SyncTimestamp = DateTime.UtcNow
            };

            var @event = new PennylaneDocumentSyncCompletedEvent(
                eventData,
                version: "1.0",
                eventType: nameof(PennylaneDocumentSyncCompletedEvent));

            await notificationManager.PublishAsync(@event);
            logger.LogDebug("[DocSync] Événement PennylaneDocumentSyncCompletedEvent publié pour AccountNumber {AccountNumber}", accountNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DocSync] Échec de la publication de l'événement pour AccountNumber {AccountNumber}", accountNumber);
        }
    }
}