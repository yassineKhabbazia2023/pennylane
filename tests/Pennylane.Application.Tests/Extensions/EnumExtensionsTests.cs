// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel;
using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;

public class EnumExtensionsTests
{
    private enum TestEnum
    {
        [Description("First Value Description")]
        FirstValue,

        SecondValue
    }

    [Fact]
    public void GetDescription_WithDescriptionAttribute_ReturnsDescription()
    {
        // Arrange
        var enumValue = TestEnum.FirstValue;

        // Act
        var description = enumValue.GetDescription();

        // Assert
        Assert.Equal("First Value Description", description);
    }

    [Fact]
    public void GetDescription_WithoutDescriptionAttribute_ReturnsName()
    {
        // Arrange
        var enumValue = TestEnum.SecondValue;

        // Act
        var description = enumValue.GetDescription();

        // Assert
        Assert.Equal("SecondValue", description);
    }

    [Fact]
    public void GetDescription_NullEnum_ReturnsEmptyString()
    {
        // Arrange
        Enum? enumValue = null;

        // Act
        var description = enumValue.GetDescription();

        // Assert
        Assert.Equal(string.Empty, description);
    }
}
