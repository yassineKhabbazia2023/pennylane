using Pennylane.Application.Extensions;
using Pennylane.Domain;

namespace Pennylane.Application.Models.Pagers;

public record DocumentsPagination
{
    private DocumentsPagination(IReadOnlyCollection<Document> items, int currentPage, int totalPage, int totalItems,
        DateTime? lastSynchroDate)
    {
        Items = items ?? Array.Empty<Document>();
        CurrentPage = currentPage > 0 ? currentPage : 1;
        TotalPage = totalPage >= 0 ? totalPage : 0;
        TotalItems = totalItems;
        LastSynchroDate = lastSynchroDate;
    }

    public IReadOnlyCollection<Document> Items { get; init; }
    public int CurrentPage { get; init; }
    public int TotalPage { get; init; }
    public int TotalItems { get; init; }
    public DateTime? LastSynchroDate { get; init; }

    public static DocumentsPagination Create(AccountDocumentsPage result)
    {
        var items = result.Documents ?? Array.Empty<Document>();
        var currentPage = result.Page > 0 ? result.Page : 1;
        var pageSize = result.PageSize > 0 ? result.PageSize : 1;
        var totalItems = result.DocumentsCount;
        var totalPage = totalItems > 0
            ? (int)Math.Ceiling((double)totalItems / pageSize)
            : 0;
        var lastSynchroLocal = result.LastSynchroDate.ConvertToLocalTime();

        return new DocumentsPagination(items, currentPage, totalPage, totalItems, lastSynchroLocal);
    }
}