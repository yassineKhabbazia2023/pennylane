// <copyright file="ServicesConfiguration.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Pennylane.WebApi.Configurations;

/// <summary>
/// ServiceConfiguration extension.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ServicesConfiguration
{
    /// <summary>
    /// Extension to configure applicationInsight.
    /// </summary>
    /// <param name="services">IServiceCollection.</param>.
    /// <param name="configuration">IConfiguration.</param>.
    public static void RegisterApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"], "APPLICATIONINSIGHTS_CONNECTION_STRING");
        var applicationInsightsConexionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = applicationInsightsConexionString;
            options.EnableAdaptiveSampling = false;
        });
    }
}