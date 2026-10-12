// <copyright file="TimeZoneHelper.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Helpers;

/// <summary>
/// Helper class for timezone operations.
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// Gets the Paris timezone in a cross-platform way.
    /// Windows uses "Romance Standard Time", Linux uses "Europe/Paris".
    /// </summary>
    /// <returns>The Paris TimeZoneInfo.</returns>
    public static TimeZoneInfo GetParisTimeZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Romance Standard Time", out var windowsTz))
        {
            return windowsTz;
        }

        return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    }
}
