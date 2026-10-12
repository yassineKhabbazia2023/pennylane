using Xunit;
using Pennylane.Application.Helpers;
using System.Collections.Generic;
using Pennylane.Application.Consts;
using Application.Models.Accounts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;
namespace Pennylane.Application.Tests.Helpers;
public class RoleHelperTests
{
    [Theory]
    [InlineData(SaasNames.v1_pro_internalized)]
    [InlineData(SaasNames.v1_electronic_invoicing)]
    public void GetRoles_ReturnsExpectedRoles_ForExistingSaasPlan(SaasNames saasPlan)
    {
        // Arrange
        var saasPlanKey = saasPlan.GetDescription();

        // Act
        var roles = RoleHelper.GetRoles(saasPlanKey);

        // Assert
        Assert.NotNull(roles);
        // Check that roles contain expected role codes, for example, ExecutiveRole
        Assert.Contains(roles, r => r.Code == PennylaneRoles.ExecutiveRole.Code);
    }

    [Fact]
    public void GetRoles_ReturnsDefaultRoles_WhenSaasPlanNotFound()
    {
        // Arrange
        var nonexistentPlan = "nonexistent_plan";

        // Act
        var roles = RoleHelper.GetRoles(nonexistentPlan);

        // Assert
        Assert.NotNull(roles);
        // It should be equal to the roles list for v1_saas_free
        var expectedRoles = PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()];
        Assert.Equal(expectedRoles, roles);
    }

    [Fact]
    public void GetRoles_ReturnsDefaultRoles_WhenSaasPlanIsNull()
    {
        // Act
        var roles = RoleHelper.GetRoles(null);

        // Assert
        Assert.NotNull(roles);
        var expectedRoles = PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()];
        Assert.Equal(expectedRoles, roles);
    }

    [Fact]
    public void GetRoles_ReturnsDefaultRoles_WhenSaasPlanIsEmpty()
    {
        // Act
        var roles = RoleHelper.GetRoles(string.Empty);

        // Assert
        Assert.NotNull(roles);
        var expectedRoles = PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()];
        Assert.Equal(expectedRoles, roles);
    }


}
