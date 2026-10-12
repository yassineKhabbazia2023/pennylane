// <copyright file="Program.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Hangfire;
using Microsoft.OpenApi;
using FluentValidation;
using FluentValidation.AspNetCore;
using Pennylane.Application.Configurations.Options;
using Pennylane.Infrastructure.Configurations;
using Pennylane.WebApi.Configurations;
using Pennylane.WebApi.Hangfire;
using Pennylane.WebApi.Validators;
using Pulse.Back.ExceptionMiddleware;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure services
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// Configure middleware
ConfigureMiddleware(app);

await app.RunAsync();

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Add controllers and JSON options
    services.AddControllers()
        .AddControllersAsServices()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // Add Swagger/OpenAPI configuration
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.AddServer(new OpenApiServer { Url = "/" });
        c.AddServer(new OpenApiServer { Url = "/pennylane" });
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        c.UseInlineDefinitionsForEnums();
    });

    // Add FluentValidation
    services.AddFluentValidationAutoValidation();
    services.AddValidatorsFromAssemblyContaining<PnlRoleValidator>();

    // Add application-specific services
    services.AddInfrastructureServices(configuration);
    services.AddHealthChecks();
    services.AddProblemDetails();
    services.RegisterApplicationInsights(configuration);

    // Configure ServiceBusOptions
    services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.ServiceBus));

    // Configure Hangfire
    var connectionString = configuration["DatabaseConnectionString"];
    services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new Hangfire.SqlServer.SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true,
        }));

    services.AddHangfireServer();
}

void ConfigureMiddleware(WebApplication app)
{
    // Configure Swagger
    app.UseSwagger(option =>
    {
        option.RouteTemplate = "/pennylane/api/{documentName}/api.json";
    });

    var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    app.UseSwaggerUI(c =>
    {
        c.EnableTryItOutByDefault();
        c.SwaggerEndpoint("/pennylane/api/v1/api.json", $"{assemblyName} v1");
        c.RoutePrefix = "api";
    });

    // Configure middleware pipeline
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseExceptionMiddleware();
    app.UseRouting();
    app.UseAuthorization();

    // Configure Hangfire Dashboard
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        DashboardTitle = "Pennylane - Email Recap Jobs",
    });

    // Register recurring jobs
    app.ConfigureHangfireJobs();

    // Map endpoints
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapFallbackToFile("index.html");
}

/// <summary>
/// Program partial class.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class Program
{ }