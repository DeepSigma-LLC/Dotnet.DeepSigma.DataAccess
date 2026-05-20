using DeepSigma.DataAccess.Http;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the HTTP API.
/// </summary>
public static class DeepSigmaHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpApi"/> as a typed <see cref="HttpClient"/> consumer via
    /// <see cref="IHttpClientFactory"/>. Use the optional <paramref name="configureClient"/>
    /// callback to set default headers, base address, etc.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Optional callback to customize the <see cref="HttpClient"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaHttp(this IServiceCollection services, Action<HttpClient>? configureClient = null)
    {
        if (configureClient is null)
        {
            services.AddHttpClient<HttpApi>();
        }
        else
        {
            services.AddHttpClient<HttpApi>(configureClient);
        }
        return services;
    }
}
