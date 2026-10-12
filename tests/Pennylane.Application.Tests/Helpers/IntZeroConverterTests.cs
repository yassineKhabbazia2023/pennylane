// pyright file="IntZeroConverterTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;
using Pennylane.Application.Helpers;
using System.Globalization;
using System.IO;
using Xunit;

namespace Pennylane.Application.Tests.Helpers;

public class IntZeroConverterTests
{
    private readonly IntZeroConverter _converter;
    private readonly Mock<IReaderRow> _rowMock;
    private readonly MemberMapData _memberMapData;

    public IntZeroConverterTests()
    {
        _converter = new IntZeroConverter();
        _rowMock = new Mock<IReaderRow>();
        _memberMapData = new MemberMapData(null);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(" ", 0)]
    [InlineData(null, 0)]
    [InlineData("NULL", 0)]
    [InlineData("null", 0)]
    public void ConvertFromString_WhenEmptyOrNull_ReturnsZero(string? input, int expected)
    {
        // Act
        var result = _converter.ConvertFromString(input, _rowMock.Object, _memberMapData);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertFromString_WhenValidInt_ReturnsValue()
    {
        // Arrange
        var input = "42";
        var culture = new System.Globalization.CultureInfo("fr-FR");
        var config = new CsvConfiguration(culture) { Delimiter = ";" };
        using var reader = new StringReader("");
        using var csv = new CsvReader(reader, config);
        
        // S'assurer que les options de conversion sont liées au membre via MemberMapData
        var options = new TypeConverterOptions { CultureInfo = culture };
        var memberMapData = new MemberMapData(null);
        memberMapData.TypeConverterOptions.CultureInfo = culture;

        // Act
        var result = _converter.ConvertFromString(input, csv, memberMapData);

        // Assert
        Assert.Equal(42, result);
    }
}
