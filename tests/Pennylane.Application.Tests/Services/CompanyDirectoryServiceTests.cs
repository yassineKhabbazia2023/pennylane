using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Application.services;
using Pennylane.Domain.Entities.Pennylane;
using Xunit;

namespace Pennylane.Application.Tests.Services;

public class CompanyDirectoryServiceTests
{
    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<ICompanyDirectoryRepository> _repositoryMock = new();
    private readonly Mock<ILogger<CompanyDirectoryService>> _loggerMock = new();

    private CompanyDirectoryService CreateSut() => new(_apiMock.Object, _repositoryMock.Object, _loggerMock.Object);

    private static PennylaneCompanyPager Page(int current, int total, params PennylaneCompany[] companies) =>
        new() { CurrentPage = current, TotalPages = total, Companies = companies.ToList() };

    private static PennylaneCompany Company(string id, string? regNo) => new() { Id = id, RegNo = regNo!, FirmId = "firm-1", Name = id };

    [Fact]
    public async Task RefreshAsync_PaginatesUntilTotalPagesAndReplacesDirectory()
    {
        _apiMock.Setup(a => a.GetCompaniesAsync(1, 500)).ReturnsAsync(Page(1, 2, Company("EC1", "111111111")));
        _apiMock.Setup(a => a.GetCompaniesAsync(2, 500)).ReturnsAsync(Page(2, 2, Company("EC2", "222222222")));
        IReadOnlyList<CompanyDirectoryEntity>? saved = null;
        _repositoryMock
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<CompanyDirectoryEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CompanyDirectoryEntity>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        var count = await CreateSut().RefreshAsync();

        count.Should().Be(2);
        saved.Should().NotBeNull();
        saved!.Select(e => e.CompanyId).Should().BeEquivalentTo("EC1", "EC2");
        _apiMock.Verify(a => a.GetCompaniesAsync(It.IsAny<int?>(), 500), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshAsync_SkipsCompaniesWithoutRegNo()
    {
        _apiMock.Setup(a => a.GetCompaniesAsync(1, 500)).ReturnsAsync(Page(1, 1, Company("EC1", "111111111"), Company("EC2", null)));
        IReadOnlyList<CompanyDirectoryEntity>? saved = null;
        _repositoryMock
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<CompanyDirectoryEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CompanyDirectoryEntity>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RefreshAsync();

        saved!.Should().ContainSingle(e => e.CompanyId == "EC1");
    }

    [Fact]
    public async Task RefreshAsync_NormalizesSiretRegNoToNineDigitSiren()
    {
        _apiMock.Setup(a => a.GetCompaniesAsync(1, 500)).ReturnsAsync(Page(1, 1,
            Company("EC1", "78420478600072"),
            Company("EC2", " 123456789 ")));
        IReadOnlyList<CompanyDirectoryEntity>? saved = null;
        _repositoryMock
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<CompanyDirectoryEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CompanyDirectoryEntity>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RefreshAsync();

        saved!.Single(e => e.CompanyId == "EC1").Siren.Should().Be("784204786");
        saved!.Single(e => e.CompanyId == "EC2").Siren.Should().Be("123456789");
    }

    [Fact]
    public async Task RefreshAsync_SkipsRegNoThatIsNeitherSirenNorSiret()
    {
        _apiMock.Setup(a => a.GetCompaniesAsync(1, 500)).ReturnsAsync(Page(1, 1,
            Company("EC1", "111111111"),
            Company("EC2", "1234"),
            Company("EC3", "123456789012"),
            Company("EC4", "HRB1234567890"),
            Company("EC5", "78420478600072X")));
        IReadOnlyList<CompanyDirectoryEntity>? saved = null;
        _repositoryMock
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<CompanyDirectoryEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CompanyDirectoryEntity>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await CreateSut().RefreshAsync();

        saved!.Should().HaveCount(1);
        saved![0].CompanyId.Should().Be("EC1");
    }

    [Fact]
    public async Task RefreshAsync_KeepsPreviousSnapshotWhenAllCompaniesHaveBlankRegNo()
    {
        _apiMock.Setup(a => a.GetCompaniesAsync(1, 500)).ReturnsAsync(Page(1, 1, Company("EC1", null), Company("EC2", " ")));

        var count = await CreateSut().RefreshAsync();

        count.Should().Be(0);
        _repositoryMock.Verify(
            r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<CompanyDirectoryEntity>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
