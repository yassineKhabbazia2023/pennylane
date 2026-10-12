using FluentAssertions;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Remediation;
using Xunit;
using Pennylane.Application.Interfaces.Remediation;

namespace Pennylane.Application.Tests.Remediation;

public class BySirenResolverTests
{
    private readonly Mock<ICompanyDirectoryRepository> _directoryMock = new();

    private static RemediationRequest Request() =>
        new(42, "EC12345678", "123456789", SubscriptionStatuses.PennylaneTransferInProgress, [], []);

    [Fact]
    public async Task ResolveAsync_WhenDirectoryReturnsFound_ReturnsFoundWithCompanyId()
    {
        _directoryMock.Setup(d => d.ResolveBySirenAsync("123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SirenResolution(SirenResolutionStatus.Found, "EC-RESOLVED"));

        var result = await new BySirenResolver(_directoryMock.Object).ResolveAsync(Request(), CancellationToken.None);

        result.Found.Should().BeTrue();
        result.CompanyId.Should().Be("EC-RESOLVED");
    }

    [Fact]
    public async Task ResolveAsync_WhenDirectoryReturnsNotFound_ReturnsNotFound()
    {
        _directoryMock.Setup(d => d.ResolveBySirenAsync("123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SirenResolution(SirenResolutionStatus.NotFound, null));

        var result = await new BySirenResolver(_directoryMock.Object).ResolveAsync(Request(), CancellationToken.None);

        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_WhenDirectoryReturnsAmbiguous_ReturnsNotFoundWithDetail()
    {
        _directoryMock.Setup(d => d.ResolveBySirenAsync("123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SirenResolution(SirenResolutionStatus.Ambiguous, null));

        var result = await new BySirenResolver(_directoryMock.Object).ResolveAsync(Request(), CancellationToken.None);

        result.Found.Should().BeFalse();
        result.Detail.Should().Contain("ambiguous");
    }
}
