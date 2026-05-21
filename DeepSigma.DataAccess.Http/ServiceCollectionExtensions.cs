using DeepSigma.DataAccess.Http;
using DeepSigma.DataAccess.Http.Throttling;

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

    /// <summary>
    /// Adds a process-scoped minimum-interval throttle to this client. All clients sharing the same
    /// <paramref name="key"/> cooperate on spacing; if <paramref name="key"/> is null, the client's
    /// registered name is used.
    /// </summary>
    public static IHttpClientBuilder AddMinIntervalThrottle(this IHttpClientBuilder builder, TimeSpan minInterval, string? key = null)
    {
        string throttleKey = key ?? builder.Name;
        return builder.AddHttpMessageHandler(() => new MinIntervalDelegatingHandler(new MinIntervalOptions
        {
            MinInterval = minInterval,
            Key = throttleKey,
        }));
    }

    /// <summary>
    /// Adds a retry policy that honors <c>Retry-After</c> on configured status codes (default: 429, 503)
    /// and falls back to capped exponential backoff with jitter.
    /// </summary>
    public static IHttpClientBuilder AddRetryAfterPolicy(this IHttpClientBuilder builder, Action<RetryAfterOptions>? configure = null)
    {
        RetryAfterOptions options = new();
        configure?.Invoke(options);
        return builder.AddHttpMessageHandler(() => new RetryAfterDelegatingHandler(options));
    }
}
