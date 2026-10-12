// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;

public class PreventArgumentNullExtensionTests
{
    private enum TestEnum
    {
        DefaultValue,
        NonDefaultValue
    }

    // Tests for ValidateParamWithException<T>(this T param, string? paramName)

    [Fact]
    public void ValidateParamWithException_WithNullParamAndCustomName_ThrowsArgumentNullException()
    {
        // Arrange
        string? param = null;
        string paramName = "myParam";

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => param.ValidateParamWithException(paramName));
        Assert.Contains(paramName, ex.ParamName);
    }

    [Fact]
    public void ValidateParamWithException_WithNullParamAndNullName_ThrowsArgumentNullExceptionWithParamDefaultName()
    {
        // Arrange
        string? param = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => param.ValidateParamWithException(null));
        // When paramName is null, nameof(param) ("param") is used
        Assert.Contains("param", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateParamWithException_WithEmptyOrWhitespaceString_ThrowsArgumentException(string param)
    {
        // Arrange
        string paramName = "inputString";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => param.ValidateParamWithException(paramName));
        Assert.Contains(paramName, ex.ParamName);
    }

    [Fact]
    public void ValidateParamWithException_WithNonEmptyString_DoesNotThrow()
    {
        // Arrange
        string param = "valid";

        // Act & Assert
        Exception? ex = Record.Exception(() => param.ValidateParamWithException("input"));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateParamWithException_WithNonNullObject_DoesNotThrow()
    {
        // Arrange
        object param = new object();

        // Act & Assert
        Exception? ex = Record.Exception(() => param.ValidateParamWithException("obj"));
        Assert.Null(ex);
    }

    // Tests for ValidateParamWithException<T>(this T param)

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateParamWithException_Overload_EmptyOrWhitespaceString_ThrowsArgumentNullException(string param)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => param.ValidateParamWithException());
    }

    [Fact]
    public void ValidateParamWithException_Overload_NonEmptyString_ReturnsFalse()
    {
        // Arrange
        string param = "hello";

        // Act
        bool result = param.ValidateParamWithException();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateParamWithException_Overload_IntZero_ReturnsFalse()
    {
        // Arrange
        int param = 0;

        // Act
        bool result = param.ValidateParamWithException();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateParamWithException_Overload_IntNonZero_ReturnsFalse()
    {
        // Arrange
        int param = 5;

        // Act
        bool result = param.ValidateParamWithException();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateParamWithException_Overload_DefaultEnum_ReturnsFalse()
    {
        // Arrange
        TestEnum param = TestEnum.DefaultValue;

        // Act
        bool result = param.ValidateParamWithException();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateParamWithException_Overload_NonDefaultEnum_ReturnsFalse()
    {
        // Arrange
        TestEnum param = TestEnum.NonDefaultValue;

        // Act
        bool result = param.ValidateParamWithException();

        // Assert
        Assert.False(result);
    }
}
