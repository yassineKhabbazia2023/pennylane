// <copyright file="HangfireAuthorizationFilter.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Hangfire.Dashboard;

namespace Pennylane.WebApi.Hangfire;

/// <summary>
/// Authorization filter for Hangfire dashboard access.
/// Access is controlled at network level (hub and spoke infrastructure).
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <inheritdoc/>
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}
