using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pulse.ContactRegistry.Infrastructure.Context;
using FluentAssertions;
using Xunit;

namespace Pennylane.Infrastructure.Tests.Repository;

public class CompanyDirectoryRepositoryTests
{
    private static PennylaneContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PennylaneContext(options);
    }

    private static CompanyDirectoryEntity Entry(string siren, string companyId) => new()
    {
        Siren = siren,
        CompanyId = companyId,
        SnapshotDate = DateTime.UtcNow,
    };

    [Fact]
    public async Task ResolveBySirenAsync_WhenSirenAbsent_ReturnsNotFound()
    {
        var repository = new CompanyDirectoryRepository(GetInMemoryContext());

        var result = await repository.ResolveBySirenAsync("999999999");

        result.Status.Should().Be(SirenResolutionStatus.NotFound);
        result.CompanyId.Should().BeNull();
    }

    [Fact]
    public async Task ResolveBySirenAsync_WhenSingleMatch_ReturnsFoundWithCompanyId()
    {
        var context = GetInMemoryContext();
        context.CompanyDirectoryEntity.Add(Entry("123456789", "EC11111111"));
        await context.SaveChangesAsync();
        var repository = new CompanyDirectoryRepository(context);

        var result = await repository.ResolveBySirenAsync("123456789");

        result.Status.Should().Be(SirenResolutionStatus.Found);
        result.CompanyId.Should().Be("EC11111111");
    }

    [Fact]
    public async Task ResolveBySirenAsync_WhenDuplicateSiren_ReturnsAmbiguous()
    {
        var context = GetInMemoryContext();
        context.CompanyDirectoryEntity.Add(Entry("123456789", "EC11111111"));
        context.CompanyDirectoryEntity.Add(Entry("123456789", "EC22222222"));
        await context.SaveChangesAsync();
        var repository = new CompanyDirectoryRepository(context);

        var result = await repository.ResolveBySirenAsync("123456789");

        result.Status.Should().Be(SirenResolutionStatus.Ambiguous);
        result.CompanyId.Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAllAsync_ReplacesPreviousSnapshot()
    {
        var context = GetInMemoryContext();
        context.CompanyDirectoryEntity.Add(Entry("111111111", "EC-OLD"));
        await context.SaveChangesAsync();
        var repository = new CompanyDirectoryRepository(context);

        await repository.ReplaceAllAsync(new[] { Entry("222222222", "EC-NEW") });

        (await repository.ResolveBySirenAsync("111111111")).Status.Should().Be(SirenResolutionStatus.NotFound);
        (await repository.ResolveBySirenAsync("222222222")).Status.Should().Be(SirenResolutionStatus.Found);
    }
}
