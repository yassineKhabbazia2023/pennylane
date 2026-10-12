using FluentAssertions;
using Pennylane.Application.Consts;
using Pennylane.Application.Helpers;
using Pennylane.Application.Models;
using Pennylane.WebApi.Models;

namespace Pennylane.Application.Tests.Helpers;

public class PennylaneInformationHelperTests
{
    [Theory]
    [InlineData("COLLABORATIVE", "v1_saas_free")]
    [InlineData("STARTER", "v1_starter")]
    [InlineData("BASIC", "v1_basic")]
    [InlineData("ESSENTIAL", "v1_essential")]
    [InlineData("PREMIUM", "v1_pro")]
    [InlineData("PREMIUM_INTERNAL", "v1_pro_internalized")]
    [InlineData("APPROVED_PLATFORM", "v1_electronic_invoicing")]
    public void GetExpectedSaasPlan_WithValidPlanCode_ShouldReturnExpectedSaasPlan(string planCode, string expectedSaasPlan)
    {
        var result = PennylaneInformationHelper.GetExpectedSaasPlan(planCode);

        result.Should().Be(expectedSaasPlan);
    }

    [Theory]
    [InlineData("collaborative", "v1_saas_free")]
    [InlineData("Collaborative", "v1_saas_free")]
    [InlineData("Starter", "v1_starter")]
    [InlineData("basic", "v1_basic")]
    [InlineData("Premium_Internal", "v1_pro_internalized")]
    [InlineData("approved_platform", "v1_electronic_invoicing")]
    public void GetExpectedSaasPlan_ShouldBeCaseInsensitive(string planCode, string expectedSaasPlan)
    {
        var result = PennylaneInformationHelper.GetExpectedSaasPlan(planCode);

        result.Should().Be(expectedSaasPlan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetExpectedSaasPlan_WithNullOrEmpty_ShouldReturnNull(string? planCode)
    {
        var result = PennylaneInformationHelper.GetExpectedSaasPlan(planCode);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("invalid_plan")]
    public void GetExpectedSaasPlan_WithUnknownPlanCode_ShouldReturnNull(string planCode)
    {
        var result = PennylaneInformationHelper.GetExpectedSaasPlan(planCode);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Gratuit", null, "unlimited")]
    [InlineData("10", null, "10")]
    [InlineData("10 utilisateurs", null, "10")]
    [InlineData("5", null, "5")]
    [InlineData("10", "PREMIUM", "10")]
    [InlineData("10 utilisateurs", "PREMIUM", "10")]
    [InlineData(null, "APPROVED_PLATFORM", "unlimited")]
    [InlineData("10", "APPROVED_PLATFORM", "unlimited")]
    public void GetExpectedUserNumber_ShouldReturnExpected(string? userNumber, string? planCode, string? expected)
    {
        var result = PennylaneInformationHelper.GetExpectedUserNumber(userNumber, planCode);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "PREMIUM", null)]
    [InlineData("10", null, null)]
    [InlineData("10", "COLLABORATIVE", null)]
    [InlineData("10", "STARTER", null)]
    [InlineData("10", "PREMIUM_INTERNAL", 10)]
    [InlineData("10", "PREMIUM", 10)]
    [InlineData("5", "ESSENTIAL", 5)]
    [InlineData("abc", "PREMIUM", null)]
    [InlineData(null, "APPROVED_PLATFORM", 1)]
    [InlineData("10", "APPROVED_PLATFORM", 1)]
    public void GetExpectedEmployeesNumber_ShouldReturnExpected(string? userNumber, string? planCode, int? expected)
    {
        var result = PennylaneInformationHelper.GetExpectedEmployeesNumber(userNumber, planCode);
        result.Should().Be(expected);
    }

    [Fact]
    public void SetupStatus_ShouldReturnPartialSuccess_WhenAnyUserFailed()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM" };
        var userResults = new List<UserAdditionResult> {
            new() { Email = "email", Success = true },
            new() { Email = "email", Success = false }
        };
        var company = new PennylaneCompany { SaasPlan = "v1_pro" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, false, company);
        status.Should().Be(PennylaneStatuses.PartialSuccess);
    }

    [Fact]
    public void SetupStatus_ShouldReturnAlreadyExists_WhenCompanyAlreadyExists()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_saas_free" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, true, false, company);
        status.Should().Be(PennylaneStatuses.AlreadyExists);
    }

    [Fact]
    public void SetupStatus_ShouldReturnCreatedOnDefault_WhenCreatedOnDefault()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_saas_free" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, true, company);
        status.Should().Be(PennylaneStatuses.CreatedOnDefault);
    }

    [Fact]
    public void SetupStatus_ShouldReturnCreated_WhenNoSpecialCase()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_pro" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, false, company);
        status.Should().Be(PennylaneStatuses.Validated); // Validated car plan attendu = plan société
    }

    [Fact]
    public void SetupStatus_ShouldReturnValidated_WhenPlanMatchesAndNotPremiumInternal()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_pro" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, false, company);
        status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public void SetupStatus_ShouldReturnValidated_WhenPlanIsPremiumInternal()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "PREMIUM_INTERNAL" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_pro_internalized" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, false, company);
        status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public void SetupStatus_WhenApprovedPlatform_DoesNotPromoteToValidated_BecauseMandateMustBeSent()
    {
        var request = new CreateCompanyRequest { RequestedPlanCode = "APPROVED_PLATFORM" };
        var userResults = new List<UserAdditionResult> { new() { Email = "email", Success = true } };
        var company = new PennylaneCompany { SaasPlan = "v1_electronic_invoicing" };
        var status = PennylaneInformationHelper.SetupStatus(request, userResults, false, false, company);
        status.Should().Be(PennylaneStatuses.Created);
    }
}
