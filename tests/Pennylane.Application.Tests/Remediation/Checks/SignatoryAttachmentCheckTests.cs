using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Pagers;
using Pennylane.Application.Remediation;
using Pennylane.Application.Remediation.Checks;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Xunit;

namespace Pennylane.Application.Tests.Remediation.Checks;

public class SignatoryAttachmentCheckTests
{
    private const string CompanyId = "EC123";

    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<ILogger<SignatoryAttachmentCheck>> _loggerMock = new();

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
        ExpectedCompanyId = CompanyId,
        Siren = "123456789",
        CompanyId = CompanyId,
        FirmId = "firm-42",
        Collaborators = [],
        Signatories = signatories,
    };

    private static PennylaneUsersPager Page(int current, int total, params PennylaneUsers[] users) => new()
    {
        CurrentPage = current,
        TotalPages = total,
        Users = users.ToList(),
    };

    private static PennylaneUsers User(string id, string email) => new() { Id = id, Email = email, FirstName = "F", LastName = "L", Role = "executive" };

    private SignatoryAttachmentCheck CreateSut() => new(_apiMock.Object, Options(), _loggerMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenSignatoryAlreadyPresent_ReturnsCompliant()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1, User("u1", "signer@test.com")));
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryGrantCompanyUserAccessAsync(It.IsAny<string>(), It.IsAny<GrantUserAccess>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMissingAndUserAlreadyExists_AttachesAndReturnsFixed()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1));
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com")).ReturnsAsync(User("existing-id", "signer@test.com"));
        _apiMock.Setup(a => a.TryGrantCompanyUserAccessAsync(CompanyId, It.Is<GrantUserAccess>(g => g.UserEmail == "signer@test.com" && g.Role == "employee")))
            .ReturnsAsync(1);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        _apiMock.Verify(a => a.TryPostUsersAsync(It.IsAny<PennylaneContact>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanRoleMappingConfigured_UsesMappedRole()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1));
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com")).ReturnsAsync(User("existing-id", "signer@test.com"));
        _apiMock.Setup(a => a.GetCompanyByIdAsync(CompanyId)).ReturnsAsync(new Pennylane.Application.Models.PennylaneCompany { Id = CompanyId, SaasPlan = "premium" });
        _apiMock.Setup(a => a.TryGrantCompanyUserAccessAsync(CompanyId, It.Is<GrantUserAccess>(g => g.UserEmail == "signer@test.com" && g.Role == "executive")))
            .ReturnsAsync(1);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));
        var options = Microsoft.Extensions.Options.Options.Create(new PennylaneServiceOptions
        {
            ApiUrl = "http://test",
            ApiFirmUrl = "http://test",
            ApiKey = "test",
            AuthenticationMode = "kpmg-customer",
            CollaboratorAuthenticationMode = "kpmg-collaborator",
            DefaultRole = "employee",
            SupportedSaasPlans = "test",
            Token = "test",
            PlansRoleAssignments = [new PennylanePlansRoleAssignmentOptions { Plan = "premium", ClientRole = "executive" }],
        });

        var result = await new SignatoryAttachmentCheck(_apiMock.Object, options, _loggerMock.Object).ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUseUserIdForGrantAccessIsEnabled_GrantsByUserId()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1));
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com")).ReturnsAsync(User("existing-id", "signer@test.com"));
        _apiMock.Setup(a => a.TryGrantCompanyUserAccessAsync(CompanyId, It.Is<GrantUserAccess>(g => g.UserId == "existing-id" && g.UserEmail == null && g.Role == "employee")))
            .ReturnsAsync(1);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));
        var options = Microsoft.Extensions.Options.Options.Create(new PennylaneServiceOptions
        {
            ApiUrl = "http://test",
            ApiFirmUrl = "http://test",
            ApiKey = "test",
            AuthenticationMode = "kpmg-customer",
            CollaboratorAuthenticationMode = "kpmg-collaborator",
            DefaultRole = "employee",
            SupportedSaasPlans = "test",
            Token = "test",
            UseUserIdForGrantAccess = true,
        });

        var result = await new SignatoryAttachmentCheck(_apiMock.Object, options, _loggerMock.Object).ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMissingAndUserDoesNotExist_CreatesAttachesAndReturnsFixed()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1));
        _apiMock.Setup(a => a.TryGetUserAsync("newsigner@test.com")).ReturnsAsync((PennylaneUsers?)null);
        _apiMock.Setup(a => a.TryPostUsersAsync(It.Is<PennylaneContact>(c => c.Email == "newsigner@test.com" && c.AuthenticationMethod == "kpmg-customer")))
            .ReturnsAsync(("new-id", false));
        _apiMock.Setup(a => a.TryGrantCompanyUserAccessAsync(CompanyId, It.Is<GrantUserAccess>(g => g.UserEmail == "newsigner@test.com" && g.Role == "employee")))
            .ReturnsAsync(1);
        var context = Context(new ExpectedContact("newsigner@test.com", "New", "Signer", "Gerant"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGrantFails_ReturnsFailed()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1)).ReturnsAsync(Page(1, 1));
        _apiMock.Setup(a => a.TryGetUserAsync("signer@test.com")).ReturnsAsync(User("existing-id", "signer@test.com"));
        _apiMock.Setup(a => a.TryGrantCompanyUserAccessAsync(CompanyId, It.IsAny<GrantUserAccess>())).ReturnsAsync((int?)null);
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().Contain("signer@test.com");
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiCallThrowsRequestHttpException_ReturnsFailedWithoutThrowing()
    {
        _apiMock.Setup(a => a.GetCompanyUsersAsync(CompanyId, 1))
            .ThrowsAsync(new RequestHttpException(System.Net.HttpStatusCode.ServiceUnavailable, "temporarily unavailable"));
        var context = Context(new ExpectedContact("signer@test.com", "S", "S", "President"));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().NotBeNullOrEmpty();
    }
}
