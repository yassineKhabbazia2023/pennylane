// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;

public class StringExtensionsTests
{
    private class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string>? Items { get; set; }
        public string? NullProp { get; set; }
    }

    [Fact]
    public void ToKeyValuePairList_DefaultValue_ReturnsEmpty()
    {
        // Arrange
        TestModel? model = null;

        // Act
        var result = model.ToKeyValuePairList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToKeyValuePairList_ObjectWithProperties_ReturnsAllPairs()
    {
        // Arrange
        var model = new TestModel
        {
            Id = 5,
            Name = "Test",
            Items = new List<string> { "A", "B" },
            NullProp = null
        };

        // Act
        var result = model.ToKeyValuePairList().ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, kvp => kvp.Key == "Id" && kvp.Value == "5");
        Assert.Contains(result, kvp => kvp.Key == "Name" && kvp.Value == "Test");
        Assert.Contains(result, kvp => kvp.Key == "Items" && kvp.Value.Contains("System.Collections.Generic.List"));
        Assert.Contains(result, kvp => kvp.Key == "NullProp" && kvp.Value == string.Empty);
    }

    [Fact]
    public void ToQueryParam_DefaultValue_ReturnsEmptyString()
    {
        // Arrange
        TestModel? model = null;

        // Act
        var query = model.ToQueryParam();

        // Assert
        Assert.Equal(string.Empty, query);
    }

    [Fact]
    public void ToQueryParam_ObjectWithSimpleProperties_ReturnsEncodedQueryString()
    {
        // Arrange
        var model = new TestModel
        {
            Id = 7,
            Name = "Alice",
            Items = new List<string> { "One", "Two" },
            NullProp = null
        };

        // Act
        var query = model.ToQueryParam();

        // Assert
        // Split by '&' for individual parameters
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries).ToList();
        Assert.Contains("id=7", parts);
        Assert.Contains("name=Alice", parts);
        Assert.Contains("items=One%2cTwo", parts); // comma is URL-encoded as %2c (lowercase)
        // NullProp is null, so it should be omitted
        Assert.DoesNotContain(parts, p => p.StartsWith("nullprop="));
    }

    [Fact]
    public void ToQueryParam_PropertyWithEmptyString_IncludesEmptyValue()
    {
        // Arrange
        var model = new TestModel
        {
            Id = 1,
            Name = string.Empty,
            Items = null
        };

        // Act
        var query = model.ToQueryParam();

        // Assert
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries).ToList();
        Assert.Contains("id=1", parts);
        Assert.Contains("name=", parts);
        // Items is null, so omitted
        Assert.DoesNotContain(parts, p => p.StartsWith("items="));
    }

    [Fact]
    public void ToQueryParam_WithEnumerableOfNumbers_ReturnsJoinedValues()
    {
        // Arrange
        var numericModel = new
        {
            Values = new[] { 3, 4, 5 }
        };

        // Act
        var query = numericModel.ToQueryParam();

        // Assert
        // "values=3%2c4%2c5"
        Assert.Equal("values=3%2c4%2c5", query);
    }

    [Theory]
    [InlineData("JEAN", "Jean")]
    [InlineData("jean", "Jean")]
    [InlineData("jEaN", "Jean")]
    [InlineData("MARIE-CLAIRE", "Marie-Claire")]
    [InlineData("DE LA FONTAINE", "De La Fontaine")]
    public void ToCapitalize_ShouldCapitalizeCorrectly(string input, string expected)
    {
        // Act
        var result = input.ToCapitalize();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToCapitalize_WhenNullOrWhitespace_ReturnsEmptyString(string? input)
    {
        // Act
        var result = input.ToCapitalize();

        // Assert
        Assert.Equal(string.Empty, result);
    }
}
