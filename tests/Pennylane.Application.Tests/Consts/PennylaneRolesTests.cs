using Xunit;
using Pennylane.Application.Consts;
using System.Linq;
namespace Pennylane.Application.Tests.Consts;
public class PennylaneRolesTests
{

    [Theory]
    [InlineData("v1_pro_internalized", 6)]
    [InlineData("v1_basic", 5)]
    [InlineData("v1_saas_free", 4)]
    [InlineData("v1_essential", 5)]
    [InlineData("v1_pro", 5)]
    [InlineData("v1_electronic_invoicing", 5)]
    public void RoleSaasDictionary_ShouldContainExpectedRoles(string saasKey, int expectedCount)
    {
        // Arrange
        var dictionary = PennylaneRoles.RoleSaasDictionary;

        // Act
        var roles = dictionary[saasKey];

        // Assert
        Assert.Equal(expectedCount, roles.Count);
    }

}
