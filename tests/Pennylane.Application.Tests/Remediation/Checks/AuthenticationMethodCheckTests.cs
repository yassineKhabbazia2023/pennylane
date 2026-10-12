using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Remediation;
using Pennylane.Application.Remediation.Checks;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Xunit;

namespace Pennylane.Application.Tests.Remediation.Checks;

public class AuthenticationMethodCheckTests
{
    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<ILogger<AuthenticationMethodCheck>> _loggerMock = new();

    private static IOptions<PennylaneServiceOptions> Options() => Microsoft.Extensions.Options.Options.Create(new PennylaneServiceOptions
    {
        ApiUrl = "http://test",
        ApiFirmUrl = "http://test",
        ApiKey = "test",
        AuthenticationMode = "kpmg-customer",
        CollaboratorAuthenticationMode = "kpmg-collaborator",
        DefaultRole = "employee",
        SupportedSaasPlans = "test",
        Token = "test",
    });

    private static CheckContext Context(params ExpectedContact[] signatories) => new()
    {
        ExpectedCompanyId = "EC123",
        Siren = "123456789",
        CompanyId = "EC123",
        FirmId = "firm-42",
        Collaborators = [],
        Signatories = signatories,
    };

    private AuthenticationMethodCheck CreateSut() => new(_apiMock.Object, Options(), _loggerMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenNoSignatoriesExpected_ReturnsCompliant()
    {
        var result = await CreateSut().ExecuteAsync(Context(), CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryGetUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthenticationMethodAlreadyMatches_ReturnsCompliant()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com"))
            .ReturnsAsync(new PennylaneUsers { Id = "u1", Email = "signer@test.com", FirstName = "S", LastName = "S", Role = "executive", AuthenticationMethod = "kpmg-customer" });
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryUpdateUserAuthenticationMethodAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthenticationMethodDiffersAndUpdateSucceeds_ReturnsFixed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com"))
            .ReturnsAsync(new PennylaneUsers { Id = "u1", Email = "signer@test.com", FirstName = "S", LastName = "S", Role = "executive", AuthenticationMethod = "some-other-mode" });
        _apiMock.Setup(a => a.TryUpdateUserAuthenticationMethodAsync("u1", "kpmg-customer")).ReturnsAsync(true);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        _apiMock.Verify(a => a.TryUpdateUserAuthenticationMethodAsync("u1", "kpmg-customer"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateFails_ReturnsFailed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com"))
            .ReturnsAsync(new PennylaneUsers { Id = "u1", Email = "signer@test.com", FirstName = "S", LastName = "S", Role = "executive", AuthenticationMethod = "some-other-mode" });
        _apiMock.Setup(a => a.TryUpdateUserAuthenticationMethodAsync("u1", "kpmg-customer")).ReturnsAsync(false);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().Contain("authentication method update was rejected");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthenticationMethodFieldIsNull_ReturnsFailed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com"))
            .ReturnsAsync(new PennylaneUsers { Id = "u1", Email = "signer@test.com", FirstName = "S", LastName = "S", Role = "executive", AuthenticationMethod = null });
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().Contain("authentication_method was not returned");
        result.Detail.Should().Contain("kpmg-customer");
        _apiMock.Verify(a => a.TryUpdateUserAuthenticationMethodAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSignatoryUserNotFound_ReturnsFailed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("missing@test.com")).ReturnsAsync((PennylaneUsers?)null);
        var context = Context(new ExpectedContact("missing@test.com", "M", "M", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().Contain("missing@test.com: user not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiCallThrowsRequestHttpException_ReturnsFailedWithoutThrowing()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com"))
            .ThrowsAsync(new RequestHttpException(System.Net.HttpStatusCode.ServiceUnavailable, "temporarily unavailable"));
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().NotBeNullOrEmpty();
    }
}
