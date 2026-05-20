using DeepSigma.DataAccess.Cosmos;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the Azure Cosmos DB provider.
/// </summary>
public static class DeepSigmaCosmosServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CosmosDbApi"/> as a singleton. Since <see cref="CosmosDbApi"/> implements
    /// <see cref="IDisposable"/>, the DI container will dispose the underlying <c>CosmosClient</c>
    /// on application shutdown.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpointUri">Cosmos DB endpoint URI.</param>
    /// <param name="apiKey">Cosmos DB primary key.</param>
    /// <param name="appName">Application name reported in telemetry.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaCosmos(this IServiceCollection services, string endpointUri, string apiKey, string appName)
    {
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<CosmosDbApi>(sp, endpointUri, apiKey, appName));
        return services;
    }
}
