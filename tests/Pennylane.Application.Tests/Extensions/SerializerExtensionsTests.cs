// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json;
using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;

public class SerializerExtensionsTests
{
    private class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NullableProp { get; set; }
    }

    [Fact]
    public void Serialize_ObjectWithNullProperty_IgnoresNullAndWritesIndentedJson()
    {
        // Arrange
        var model = new TestModel
        {
            Id = 42,
            Name = "TestName",
            NullableProp = null
        };

        // Act
        string json = model.Serialize();

        // Assert
        // It should contain line breaks and spaces due to WriteIndented = true
        Assert.Contains(Environment.NewLine, json);
        Assert.Contains("  \"Id\": 42", json);
        Assert.Contains("  \"Name\": \"TestName\"", json);
        // NullableProp is null, so it should not appear in the JSON
        Assert.DoesNotContain("NullableProp", json);
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsPopulatedObject()
    {
        // Arrange
        string json = @"{
            ""Id"": 7,
            ""Name"": ""DeserializeTest"",
            ""NullableProp"": ""Value""
            }";

        // Act
        var model = json.Deserialize<TestModel>();

        // Assert
        Assert.Equal(7, model.Id);
        Assert.Equal("DeserializeTest", model.Name);
        Assert.Equal("Value", model.NullableProp);
    }

    [Fact]
    public void Deserialize_JsonWithMissingFields_ReturnsObjectWithDefaults()
    {
        // Arrange
        string json = @"{
            ""Id"": 13
            }";

        // Act
        var model = json.Deserialize<TestModel>();

        // Assert
        Assert.Equal(13, model.Id);
        // Name was not present, so it should remain the default (empty string)
        Assert.Equal(string.Empty, model.Name);
        // NullableProp was not present, so it should be null
        Assert.Null(model.NullableProp);
    }

    [Fact]
    public void Deserialize_JsonWithTrailingComma_ReturnsPopulatedObject()
    {
        // Arrange
        // s_readOptions.AllowTrailingCommas = true should allow this JSON
        string jsonWithTrailingComma = @"{
                ""Id"": 21,
                ""Name"": ""TrailingCommaTest"",
                ""NullableProp"": ""TestValue"",
            }";

        // Act
        var model = jsonWithTrailingComma.Deserialize<TestModel>();

        // Assert
        Assert.Equal(21, model.Id);
        Assert.Equal("TrailingCommaTest", model.Name);
        Assert.Equal("TestValue", model.NullableProp);
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsNewObjectWithDefaults()
    {
        // Arrange
        string emptyJson = "{}";

        // Act
        var model = emptyJson.Deserialize<TestModel>();

        // Assert
        Assert.Equal(0, model.Id);
        Assert.Equal(string.Empty, model.Name);
        Assert.Null(model.NullableProp);
    }
}
