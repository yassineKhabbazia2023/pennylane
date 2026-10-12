using FluentAssertions;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain;

namespace Pennylane.Application.Tests.Models;

public class DocumentsPaginationTests
{
    [Fact]
    public void Create_ShouldReturnCorrectPagination_WithNormalInput()
    {
        var docs = new List<Document>
        {
            new(232134,"doc1", "/", "url1", DateTime.UnixEpoch, DateTime.UtcNow),
            new(232134,"doc2", "/", "url2", DateTime.UnixEpoch, DateTime.UtcNow)
        };
        var page = new AccountDocumentsPage(
            2,
            10,
            25,
            docs,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        var result = DocumentsPagination.Create(page);
        result.Items.Should().BeEquivalentTo(docs);
        result.CurrentPage.Should().Be(2);
        result.TotalPage.Should().Be(3);
        result.TotalItems.Should().Be(25);
        var expected = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
        result.LastSynchroDate.Should().Be(expected);
    }

    [Fact]
    public void Create_ShouldHandleNullDocuments()
    {
        var page = new AccountDocumentsPage(1, 10, 0, null, null);
        var result = DocumentsPagination.Create(page);
        result.Items.Should().BeEmpty();
        result.CurrentPage.Should().Be(1);
        result.TotalPage.Should().Be(0);
        result.TotalItems.Should().Be(0);
        result.LastSynchroDate.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 10, 20, 1, 2)] // page=0
    [InlineData(-5, 10, 20, 1, 2)] // page<0
    [InlineData(2, 0, 20, 2, 20)] // pageSize=0
    [InlineData(2, -10, 20, 2, 20)] // pageSize<0
    public void Create_ShouldHandleInvalidPageOrPageSize(int page, int pageSize, int totalItems, int expectedPage, int expectedTotalPage)
    {
        var docs = new List<Document> { new(232134,"doc", "/", "url", DateTime.Now, DateTime.Now) };
        var accPage = new AccountDocumentsPage(page, pageSize, totalItems, docs, null);
        var result = DocumentsPagination.Create(accPage);
        result.CurrentPage.Should().Be(expectedPage);
        result.TotalPage.Should().Be(expectedTotalPage);
    }

    [Fact]
    public void Create_ShouldReturnZeroTotalPage_WhenNoItems()
    {
        var accPage = new AccountDocumentsPage(1, 10, 0, new List<Document>(), null);
        var result = DocumentsPagination.Create(accPage);
        result.TotalPage.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldPropagateNullLastSynchroDate()
    {
        var accPage = new AccountDocumentsPage(1, 10, 1, new List<Document>(), null);
        var result = DocumentsPagination.Create(accPage);
        result.LastSynchroDate.Should().BeNull();
    }
}
