// <copyright file="DateTimeExtensions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Globalization;

namespace Pennylane.Application.Extensions;

public static class DateTimeExtensions
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public static DateTime ConvertToLocalTime(this DateTime utcDate)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, TimeZoneInfo.Local);
    }

    public static DateTime? ConvertToLocalTime(this DateTime? utcDate)
    {
        return utcDate.HasValue ? ConvertToLocalTime(utcDate.Value) : null;
    }

    public static string? ToFrenchLongDate(this DateTime? date)
        => date?.ToString("dd MMMM yyyy", FrenchCulture);
}

