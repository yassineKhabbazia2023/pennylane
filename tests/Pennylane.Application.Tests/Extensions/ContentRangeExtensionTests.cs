// <copyright file="AccountServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;

public class ContentRangeExtensionTests
{
    [Fact]
    public void GetContentRange_NoHeader_ReturnsZeros()
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            Content = new StringContent(string.Empty)
        };

        // Act
        var contentRange = response.GetContentRange();

        // Assert
        Assert.Equal(0, contentRange.From);
        Assert.Equal(0, contentRange.To);
        Assert.Equal(0, contentRange.Length);
    }

    [Fact]
    public void GetContentRange_InvalidHeaderFormat_ReturnsZeros()
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            Content = new StringContent(string.Empty)
        };
        response.Content.Headers.TryAddWithoutValidation("Content-Range", "invalid-format");

        // Act
        var contentRange = response.GetContentRange();

        // Assert
        Assert.Equal(0, contentRange.From);
        Assert.Equal(0, contentRange.To);
        Assert.Equal(0, contentRange.Length);
    }

    [Fact]
    public void GetContentRange_ValidHeader_ReturnsParsedValues()
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            Content = new StringContent(string.Empty)
        };
        // "someprefix12-34/56" splits into ["someprefix12", "34", "56"]
        response.Content.Headers.TryAddWithoutValidation("Content-Range", "someprefix12-34/56");

        // Act
        var contentRange = response.GetContentRange();

        // Assert
        Assert.Equal(12, contentRange.From);
        Assert.Equal(34, contentRange.To);
        Assert.Equal(56, contentRange.Length);
    }
}
