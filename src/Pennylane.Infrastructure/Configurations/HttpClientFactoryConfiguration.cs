using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.services;
using Pennylane.Application.StaticValues;

namespace Pennylane.Infrastructure.Configurations;

[ExcludeFromCodeCoverage]
public static class HttpClientFactoryConfiguration
{
    public static void AddRegistrationServiceApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PennylaneServiceOptions>()
            .Bind(configuration.GetSection(Constant.PennylaneServiceSettings))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // HttpClient for Group Service
        services.AddHttpClient<IPennylaneApiGroupService, PennylaneApiGroupService>(httpClient =>
        {
            var pennylaneServiceOptions = configuration.GetSection(Constant.PennylaneServiceSettings)
                .Get<PennylaneServiceOptions>();
            httpClient.BaseAddress = new Uri(pennylaneServiceOptions!.ApiUrl);
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pennylaneServiceOptions.ApiKey);
        }).AddStandardResilienceHandler();

        // HttpClient for Firm Service
        services.AddHttpClient<IPennylaneApiFirmService, PennylaneApiFirmService>((serviceProvider, httpClient) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var pennylaneServiceOptions = config.GetSection(Constant.PennylaneServiceSettings)
                .Get<PennylaneServiceOptions>();
            httpClient.BaseAddress = new Uri(pennylaneServiceOptions!.ApiFirmUrl);
        }).AddStandardResilienceHandler();
    }
}