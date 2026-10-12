using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Remediation;
using Pennylane.Application.Remediation.Checks;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Xunit;

namespace Pennylane.Application.Tests.Remediation.Checks;

public class CollaboratorsCheckTests
{
    private const string CompanyId = "EC123";
    private const string FirmId = "firm-42";

    private readonly Mock<IPennylaneApiGroupService> _apiMock = new();
    private readonly Mock<ILogger<CollaboratorsCheck>> _loggerMock = new();

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

    private static ExpectedContact Collaborator(string email, int? functionId) => new(email, "F", "L", null, functionId);

    private static CheckContext Context(string? managerId, string? supervisorId, string? accountantId, params ExpectedContact[] collaborators) => new()
    {
        ExpectedCompanyId = CompanyId,
        Siren = "123456789",
        CompanyId = CompanyId,
        FirmId = FirmId,
        AccountingManagerId = managerId,
        AccountingSupervisorId = supervisorId,
        AccountantId = accountantId,
        Collaborators = collaborators,
        Signatories = [],
    };

    private static PennylaneUsers User(string id, string email) => new() { Id = id, Email = email, FirstName = "F", LastName = "L", Role = "employee" };

    private CollaboratorsCheck CreateSut() => new(_apiMock.Object, Options(), _loggerMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenNoCollaboratorHasAnAccountingFunction_ReturnsCompliant()
    {
        var context = Context(null, null, null,
            Collaborator("nofunction@test.com", null),
            Collaborator("unknown@test.com", 99));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryGetUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThreeDistinctUsersMatchTheirFunctions_ReturnsCompliant()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("m@test.com")).ReturnsAsync(User("uuid-m", "m@test.com"));
        _apiMock.Setup(a => a.TryGetUserAsync("s@test.com")).ReturnsAsync(User("uuid-s", "s@test.com"));
        _apiMock.Setup(a => a.TryGetUserAsync("a@test.com")).ReturnsAsync(User("uuid-a", "a@test.com"));
        var context = Context("uuid-m", "uuid-s", "uuid-a",
            Collaborator("m@test.com", PennylaneFunctions.AccountingManagerFunctionId),
            Collaborator("s@test.com", PennylaneFunctions.AccountingSupervisorFunctionId),
            Collaborator("a@test.com", PennylaneFunctions.AccountantFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryUpdateCompanyAccountingContactsAsync(It.IsAny<string>(), It.IsAny<UpdateCompanyAccountingContactsRequest>()), Times.Never);
        _apiMock.Verify(a => a.GrantFirmUserAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSameUserHoldsAllThreeFunctions_ResolvesOnceAndReturnsCompliant()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("solo@test.com")).ReturnsAsync(User("uuid-solo", "solo@test.com"));
        var context = Context("uuid-solo", "uuid-solo", "uuid-solo",
            Collaborator("solo@test.com", PennylaneFunctions.AccountingManagerFunctionId),
            Collaborator("solo@test.com", PennylaneFunctions.AccountingSupervisorFunctionId),
            Collaborator("solo@test.com", PennylaneFunctions.AccountantFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Compliant);
        _apiMock.Verify(a => a.TryGetUserAsync("solo@test.com"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneFunctionIsUnassigned_GrantsFirmAccessThenPutsOnlyThatFunction()
    {
        var callOrder = new List<string>();
        _apiMock.Setup(a => a.TryGetUserAsync("a@test.com")).ReturnsAsync(User("uuid-a", "a@test.com"));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-a", "a@test.com"))
            .Callback(() => callOrder.Add("firm"))
            .ReturnsAsync((true, (string?)null));
        UpdateCompanyAccountingContactsRequest? sent = null;
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()))
            .Callback<string, UpdateCompanyAccountingContactsRequest>((_, r) => { callOrder.Add("put"); sent = r; })
            .ReturnsAsync(true);
        var context = Context(null, null, null, Collaborator("a@test.com", PennylaneFunctions.AccountantFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        callOrder.Should().ContainInOrder("firm", "put");
        sent!.AccountantId.Should().Be("uuid-a");
        sent.AccountingManagerId.Should().BeNull();
        sent.AccountingSupervisorId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSameUserExpectedOnAllThreeFunctions_MakesOneResolutionOneGrantOnePut()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("solo@test.com")).ReturnsAsync(User("uuid-solo", "solo@test.com"));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-solo", "solo@test.com")).ReturnsAsync((true, (string?)null));
        UpdateCompanyAccountingContactsRequest? sent = null;
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()))
            .Callback<string, UpdateCompanyAccountingContactsRequest>((_, r) => sent = r)
            .ReturnsAsync(true);
        var context = Context(null, null, null,
            Collaborator("solo@test.com", PennylaneFunctions.AccountingManagerFunctionId),
            Collaborator("solo@test.com", PennylaneFunctions.AccountingSupervisorFunctionId),
            Collaborator("solo@test.com", PennylaneFunctions.AccountantFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        _apiMock.Verify(a => a.TryGetUserAsync("solo@test.com"), Times.Once);
        _apiMock.Verify(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-solo", "solo@test.com"), Times.Once);
        _apiMock.Verify(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()), Times.Once);
        sent!.AccountingManagerId.Should().Be("uuid-solo");
        sent.AccountingSupervisorId.Should().Be("uuid-solo");
        sent.AccountantId.Should().Be("uuid-solo");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFunctionHeldByAnotherUser_OverwritesIt()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("m@test.com")).ReturnsAsync(User("uuid-m", "m@test.com"));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-m", "m@test.com")).ReturnsAsync((true, (string?)null));
        UpdateCompanyAccountingContactsRequest? sent = null;
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()))
            .Callback<string, UpdateCompanyAccountingContactsRequest>((_, r) => sent = r)
            .ReturnsAsync(true);
        var context = Context("uuid-someone-else", null, null, Collaborator("m@test.com", PennylaneFunctions.AccountingManagerFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        sent!.AccountingManagerId.Should().Be("uuid-m");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserMissing_CreatesItBeforePut()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("new@test.com")).ReturnsAsync((PennylaneUsers?)null);
        _apiMock.Setup(a => a.TryPostUsersAsync(It.Is<PennylaneContact>(c => c.Email == "new@test.com" && c.AuthenticationMethod == "kpmg-collaborator")))
            .ReturnsAsync(("uuid-new", false));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-new", "new@test.com")).ReturnsAsync((true, (string?)null));
        UpdateCompanyAccountingContactsRequest? sent = null;
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()))
            .Callback<string, UpdateCompanyAccountingContactsRequest>((_, r) => sent = r)
            .ReturnsAsync(true);
        var context = Context(null, null, null, Collaborator("new@test.com", PennylaneFunctions.AccountingSupervisorFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Fixed);
        sent!.AccountingSupervisorId.Should().Be("uuid-new");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneUserUnresolvable_FixesTheOthersAndReturnsFailed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("ok@test.com")).ReturnsAsync(User("uuid-ok", "ok@test.com"));
        _apiMock.Setup(a => a.TryGetUserAsync("ko@test.com")).ReturnsAsync((PennylaneUsers?)null);
        _apiMock.Setup(a => a.TryPostUsersAsync(It.Is<PennylaneContact>(c => c.Email == "ko@test.com"))).ReturnsAsync(((string?)null, false));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-ok", "ok@test.com")).ReturnsAsync((true, (string?)null));
        UpdateCompanyAccountingContactsRequest? sent = null;
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>()))
            .Callback<string, UpdateCompanyAccountingContactsRequest>((_, r) => sent = r)
            .ReturnsAsync(true);
        var context = Context(null, null, null,
            Collaborator("ok@test.com", PennylaneFunctions.AccountingManagerFunctionId),
            Collaborator("ko@test.com", PennylaneFunctions.AccountantFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().Contain("ko@test.com");
        sent!.AccountingManagerId.Should().Be("uuid-ok");
        sent.AccountantId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPutFails_ReturnsFailed()
    {
        _apiMock.Setup(a => a.TryGetUserAsync("m@test.com")).ReturnsAsync(User("uuid-m", "m@test.com"));
        _apiMock.Setup(a => a.GrantFirmUserAccessAsync(FirmId, "accountant", "uuid-m", "m@test.com")).ReturnsAsync((true, (string?)null));
        _apiMock.Setup(a => a.TryUpdateCompanyAccountingContactsAsync(CompanyId, It.IsAny<UpdateCompanyAccountingContactsRequest>())).ReturnsAsync(false);
        var context = Context(null, null, null, Collaborator("m@test.com", PennylaneFunctions.AccountingManagerFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiCallThrowsRequestHttpException_ReturnsFailedWithoutThrowing()
    {
        _apiMock.Setup(a => a.TryGetUserAsync(It.IsAny<string>()))
            .ThrowsAsync(new RequestHttpException(System.Net.HttpStatusCode.ServiceUnavailable, "unavailable"));
        var context = Context(null, null, null, Collaborator("m@test.com", PennylaneFunctions.AccountingManagerFunctionId));

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Outcome.Should().Be(CheckOutcome.Failed);
        result.Detail.Should().NotBeNullOrEmpty();
    }
}
