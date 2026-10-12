// pyright file="MapEventDataModelPulseTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Mappers;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Application.Tests.Mappers;

public class MapEventDataModelPulseTests
{
    [Fact]
    public void MapToRoleEntity_ReturnsNull_WhenRoleCreatedEventDataIsNull()
    {
        // Act
        RoleEntity result = MapEventDataModelPulse.MapToRoleEntity((RoleCreatedEventData)null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapToRoleEntity_MapsAllFields_FromRoleCreatedEventData()
    {
        // Arrange
        var source = new RoleCreatedEventData
        {
            AccountId = 123,
            ContactId = 456,
            IsSignatory = true
        };

        // Act
        var result = source.MapToRoleEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(source.AccountId, result.AccountId);
        Assert.Equal(source.ContactId, result.ContactId);
        Assert.Equal(source.IsSignatory, result.IsSignatory);
    }

    [Fact]
    public void MapToRoleEntity_ReturnsNull_WhenRoleDeletedEventDataIsNull()
    {
        // Act
        RoleEntity result = MapEventDataModelPulse.MapToRoleEntity((RoleDeletedEventData)null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapToRoleEntity_MapsAllFields_FromRoleDeletedEventData()
    {
        // Arrange
        var source = new RoleDeletedEventData
        {
            AccountId = 789,
            ContactId = 321
        };

        // Act
        var result = source.MapToRoleEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(source.AccountId, result.AccountId);
        Assert.Equal(source.ContactId, result.ContactId);
    }

    [Fact]
    public void MapToPnlRoleEntity_ReturnsNull_WhenRoleCreatedEventDataIsNull()
    {
        // Act
        PnlRoleEntity result = MapEventDataModelPulse.MapToPnlRoleEntity(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapToPnlRoleEntity_MapsAllFields_FromRoleCreatedEventData()
    {
        // Arrange
        var source = new RoleCreatedEventData
        {
            AccountId = 111,
            ContactId = 222,
            SubRole = "ADMIN"
        };

        // Act
        var result = source.MapToPnlRoleEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(source.AccountId, result.AccountId);
        Assert.Equal(source.ContactId, result.ContactId);
        Assert.Equal(source.SubRole, result.Role);
    }
}
