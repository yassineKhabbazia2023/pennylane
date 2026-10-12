// <copyright file="HangfireAuthorizationFilterTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using FluentAssertions;
using Hangfire.Dashboard;
using Pennylane.WebApi.Hangfire;

namespace Pennylane.WebApi.Tests.Hangfire;

public class HangfireAuthorizationFilterTests
{
    [Fact]
    public void Authorize_WithNullContext_ReturnsTrue()
    {
        // Arrange
        var filter = new HangfireAuthorizationFilter();

        // Act
        var result = filter.Authorize(null!);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Filter_ImplementsIDashboardAuthorizationFilter()
    {
        // Arrange & Act
        var filter = new HangfireAuthorizationFilter();

        // Assert
        filter.Should().BeAssignableTo<IDashboardAuthorizationFilter>();
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var filter = new HangfireAuthorizationFilter();

        // Assert
        filter.Should().NotBeNull();
    }
}
