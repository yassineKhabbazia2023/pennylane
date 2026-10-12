// pyright file="DecimalZeroConverter.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Pennylane.Application.Helpers;

/// <summary>
/// Convertisseur CsvHelper pour les décimaux traitant les chaînes vides ou "NULL" comme 0.
/// </summary>
public class DecimalZeroConverter : DecimalConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "NULL", StringComparison.OrdinalIgnoreCase))
            return 0m;

        return base.ConvertFromString(text, row, memberMapData);
    }
}
