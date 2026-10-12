namespace Pennylane.Domain;

public record AccountDocumentsPage(
    int Page,
    int PageSize,
    int DocumentsCount,
    IReadOnlyCollection<Document> Documents,
    DateTime? LastSynchroDate);
