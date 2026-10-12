using AutoFixture;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models.Pennylane;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Tests.Mappers;

public class MapPnlRolesTests
{
    private readonly Fixture _fixture;

    public MapPnlRolesTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    #region MapPnlRoleModelToEntity Tests (PnlRole to PnlRoleEntity)

    [Fact]
    public void MapPnlRoleModelToEntity_WhenPnlRoleIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var pnlRole = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, 123)
            .With(r => r.ContactId, 456)
            .With(r => r.Role, "Administrator")
            .Create();

        // Act
        var result = pnlRole.MapPnlRoleModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pnlRole.AccountId, result.AccountId);
        Assert.Equal(pnlRole.ContactId, result.ContactId);
        Assert.Equal(pnlRole.Role, result.Role);
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(1, 1, "Employee")]
    [InlineData(999999, 888888, "Manager")]
    [InlineData(-1, -1, "Guest")]
    public void MapPnlRoleModelToEntity_WithDifferentValues_ShouldMapCorrectly(int accountId, int contactId, string role)
    {
        // Arrange
        var pnlRole = new PnlRole
        {
            AccountId = accountId,
            ContactId = contactId,
            Role = role
        };

        // Act
        var result = pnlRole.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(contactId, result.ContactId);
        Assert.Equal(role, result.Role);
    }

    [Fact]
    public void MapPnlRoleModelToEntity_WhenRoleIsEmpty_ShouldMapEmptyString()
    {
        // Arrange
        var pnlRole = _fixture.Build<PnlRole>()
            .With(r => r.Role, string.Empty)
            .Create();

        // Act
        var result = pnlRole.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(string.Empty, result.Role);
    }

    [Fact]
    public void MapPnlRoleModelToEntity_ShouldCreateNewInstance()
    {
        // Arrange
        var pnlRole = _fixture.Create<PnlRole>();

        // Act
        var result1 = pnlRole.MapPnlRoleModelToEntity();
        var result2 = pnlRole.MapPnlRoleModelToEntity();

        // Assert
        Assert.NotSame(result1, result2); // Different instances
        Assert.Equal(result1.AccountId, result2.AccountId); // Same data
        Assert.Equal(result1.ContactId, result2.ContactId);
        Assert.Equal(result1.Role, result2.Role);
    }

    #endregion

    #region MapPnlRoleModelToEntity Tests (PnlRoleEntity to PnlRole)

    [Fact]
    public void MapPnlRoleModelToEntity_WhenPnlRoleEntityIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var pnlRoleEntity = _fixture.Build<PnlRoleEntity>()
            .With(r => r.AccountId, 789)
            .With(r => r.ContactId, 012)
            .With(r => r.Role, "Supervisor")
            .Create();

        // Act
        var result = pnlRoleEntity.MapPnlRoleModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pnlRoleEntity.AccountId, result.AccountId);
        Assert.Equal(pnlRoleEntity.ContactId, result.ContactId);
        Assert.Equal(pnlRoleEntity.Role, result.Role);
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(1, 1, "Executive")]
    [InlineData(555555, 444444, "Accountant")]
    [InlineData(-100, -200, "External")]
    public void MapPnlRoleModelToEntity_FromEntity_WithDifferentValues_ShouldMapCorrectly(int accountId, int contactId, string role)
    {
        // Arrange
        var pnlRoleEntity = new PnlRoleEntity
        {
            AccountId = accountId,
            ContactId = contactId,
            Role = role
        };

        // Act
        var result = pnlRoleEntity.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(contactId, result.ContactId);
        Assert.Equal(role, result.Role);
    }

    [Fact]
    public void MapPnlRoleModelToEntity_FromEntity_WhenRoleIsEmpty_ShouldMapEmptyString()
    {
        // Arrange
        var pnlRoleEntity = _fixture.Build<PnlRoleEntity>()
            .With(r => r.Role, string.Empty)
            .Create();

        // Act
        var result = pnlRoleEntity.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(string.Empty, result.Role);
    }

    [Fact]
    public void MapPnlRoleModelToEntity_FromEntity_ShouldCreateNewInstance()
    {
        // Arrange
        var pnlRoleEntity = _fixture.Create<PnlRoleEntity>();

        // Act
        var result1 = pnlRoleEntity.MapPnlRoleModelToEntity();
        var result2 = pnlRoleEntity.MapPnlRoleModelToEntity();

        // Assert
        Assert.NotSame(result1, result2); // Different instances
        Assert.Equal(result1.AccountId, result2.AccountId); // Same data
        Assert.Equal(result1.ContactId, result2.ContactId);
        Assert.Equal(result1.Role, result2.Role);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MapPnlRoles_RoundTripMapping_ShouldPreserveAllData()
    {
        // Arrange
        var originalPnlRole = _fixture.Build<PnlRole>()
            .With(r => r.AccountId, 12345)
            .With(r => r.ContactId, 67890)
            .With(r => r.Role, "TestRole")
            .Create();

        // Act - Convert PnlRole -> PnlRoleEntity -> PnlRole
        var entity = originalPnlRole.MapPnlRoleModelToEntity();
        var roundTripRole = entity.MapPnlRoleModelToEntity();

        // Assert - All data should be preserved exactly
        Assert.Equal(originalPnlRole.AccountId, roundTripRole.AccountId);
        Assert.Equal(originalPnlRole.ContactId, roundTripRole.ContactId);
        Assert.Equal(originalPnlRole.Role, roundTripRole.Role);
    }

    [Fact]
    public void MapPnlRoles_ReverseRoundTripMapping_ShouldPreserveAllData()
    {
        // Arrange
        var originalEntity = _fixture.Build<PnlRoleEntity>()
            .With(r => r.AccountId, 54321)
            .With(r => r.ContactId, 09876)
            .With(r => r.Role, "ReverseTestRole")
            .Create();

        // Act - Convert PnlRoleEntity -> PnlRole -> PnlRoleEntity
        var model = originalEntity.MapPnlRoleModelToEntity();
        var roundTripEntity = model.MapPnlRoleModelToEntity();

        // Assert - All data should be preserved exactly
        Assert.Equal(originalEntity.AccountId, roundTripEntity.AccountId);
        Assert.Equal(originalEntity.ContactId, roundTripEntity.ContactId);
        Assert.Equal(originalEntity.Role, roundTripEntity.Role);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Employee")]
    [InlineData("Manager")]
    [InlineData("Guest")]
    [InlineData("")]
    [InlineData("VeryLongRoleNameThatCouldCauseIssues")]
    public void MapPnlRoles_WithDifferentRoleNames_ShouldPreserveRoleExactly(string roleName)
    {
        // Arrange
        var pnlRole = _fixture.Build<PnlRole>()
            .With(r => r.Role, roleName)
            .Create();

        // Act
        var entity = pnlRole.MapPnlRoleModelToEntity();
        var backToModel = entity.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(roleName, entity.Role);
        Assert.Equal(roleName, backToModel.Role);
    }

    [Fact]
    public void MapPnlRoles_MultipleConversions_ShouldBeIdempotent()
    {
        // Arrange
        var originalPnlRole = _fixture.Create<PnlRole>();

        // Act - Multiple conversions
        var entity1 = originalPnlRole.MapPnlRoleModelToEntity();
        var model1 = entity1.MapPnlRoleModelToEntity();
        var entity2 = model1.MapPnlRoleModelToEntity();
        var model2 = entity2.MapPnlRoleModelToEntity();

        // Assert - Results should be identical across all conversions
        Assert.Equal(originalPnlRole.AccountId, model1.AccountId);
        Assert.Equal(originalPnlRole.ContactId, model1.ContactId);
        Assert.Equal(originalPnlRole.Role, model1.Role);
        
        Assert.Equal(model1.AccountId, model2.AccountId);
        Assert.Equal(model1.ContactId, model2.ContactId);
        Assert.Equal(model1.Role, model2.Role);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void MapPnlRoles_WithMinMaxIntValues_ShouldHandleCorrectly()
    {
        // Arrange
        var pnlRole = new PnlRole
        {
            AccountId = int.MaxValue,
            ContactId = int.MinValue,
            Role = "EdgeCase"
        };

        // Act
        var entity = pnlRole.MapPnlRoleModelToEntity();
        var backToModel = entity.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(int.MaxValue, entity.AccountId);
        Assert.Equal(int.MinValue, entity.ContactId);
        Assert.Equal(int.MaxValue, backToModel.AccountId);
        Assert.Equal(int.MinValue, backToModel.ContactId);
    }

    [Fact]
    public void MapPnlRoles_WithZeroValues_ShouldMapCorrectly()
    {
        // Arrange
        var pnlRole = new PnlRole
        {
            AccountId = 0,
            ContactId = 0,
            Role = "ZeroTest"
        };

        // Act
        var entity = pnlRole.MapPnlRoleModelToEntity();

        // Assert
        Assert.Equal(0, entity.AccountId);
        Assert.Equal(0, entity.ContactId);
        Assert.Equal("ZeroTest", entity.Role);
    }

    #endregion
}
