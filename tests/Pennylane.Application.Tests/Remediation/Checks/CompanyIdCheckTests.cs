using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Remediation;
using Pennylane.Application.Remediation.Checks;
using Xunit;

namespace Pennylane.Application.Tests.Remediation.Checks;

public class CompanyIdCheckTests
{
    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<ILogger<CompanyIdCheck>> _loggerMock = new();

    private static CheckContext Context(string expectedId, string currentId) => new()
    {
        ExpectedCompanyId = expectedId,
        Siren = "123456789",
        CompanyId = currentId,
        FirmId = "firm-42",
        Collaborators = [],
        Signatories = [],
    };

    [Fact]
    public async Task ExecuteAsync_WhenIdMatches_ReturnsCompliant()
    {
        var check = new CompanyIdCheck(_apiMock.Object, _loggerMock.Object);

        var result = await check.ExecuteAsync(Context("EC123", "EC123"), CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryUpdateCompanyIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdDiffersAndUpdateSucceeds_ReturnsFixedAndMutatesContext()
    {
        _apiMock.Setup(a => a.TryUpdateCompanyIdAsync("EC-OLD", "EC123")).ReturnsAsync(true);
        var context = Context("EC123", "EC-OLD");
        var check = new CompanyIdCheck(_apiMock.Object, _loggerMock.Object);

        var result = await check.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        context.CompanyId.Should().Be("EC123");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateFails_ReturnsFailedAndKeepsContext()
    {
        _apiMock.Setup(a => a.TryUpdateCompanyIdAsync("EC-OLD", "EC123")).ReturnsAsync(false);
        var context = Context("EC123", "EC-OLD");
        var check = new CompanyIdCheck(_apiMock.Object, _loggerMock.Object);

        var result = await check.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        context.CompanyId.Should().Be("EC-OLD");
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiCallThrowsRequestHttpException_ReturnsFailedWithoutThrowing()
    {
        _apiMock.Setup(a => a.TryUpdateCompanyIdAsync("EC-OLD", "EC123"))
            .ThrowsAsync(new RequestHttpException(System.Net.HttpStatusCode.ServiceUnavailable, "temporarily unavailable"));
        var context = Context("EC123", "EC-OLD");
        var check = new CompanyIdCheck(_apiMock.Object, _loggerMock.Object);

        var result = await check.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().NotBeNullOrEmpty();
    }
}
