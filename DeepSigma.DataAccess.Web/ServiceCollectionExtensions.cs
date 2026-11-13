using System.Text.Json;
using DeepSigma.DataAccess.Web;
using DeepSigma.DataAccess.Web.Throttling;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the HTTP API.
/// </summary>
public static class DeepSigmaHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpApi"/> as a typed <see cref="HttpClient"/> consumer via
    /// <see cref="IHttpClientFactory"/>, and maps <see cref="IHttpApi"/> to it so consumers can
    /// depend on the interface. Use the optional <paramref name="configureClient"/> callback to set
    /// default headers, base address, etc.
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

        // Resolve IHttpApi to the typed-client HttpApi so callers may inject either.
        services.AddTransient<IHttpApi>(sp => sp.GetRequiredService<HttpApi>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="SseClient"/> as a typed <see cref="HttpClient"/> consumer via
    /// <see cref="IHttpClientFactory"/>, and maps <see cref="ISseClient"/> to it. Use the optional
    /// <paramref name="configureClient"/> callback to set the base address, default headers, etc.
    /// The same throttle/retry handlers (<see cref="AddMinIntervalThrottle"/>, <see cref="AddRetryAfterPolicy"/>)
    /// compose onto this client's builder.
    /// </summary>
    public static IServiceCollection AddDeepSigmaSse(this IServiceCollection services, Action<HttpClient>? configureClient = null)
    {
        if (configureClient is null)
        {
            services.AddHttpClient<SseClient>();
        }
        else
        {
            services.AddHttpClient<SseClient>(configureClient);
        }

        services.AddTransient<ISseClient>(sp => sp.GetRequiredService<SseClient>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="IWebSocketConnectionFactory"/> (a <see cref="WebSocketConnectionFactory"/>)
    /// as a singleton. Pass <paramref name="jsonOptions"/> to control the JSON shape used by typed
    /// send/receive; an <see cref="ILoggerFactory"/> and <see cref="TimeProvider"/> are resolved from DI when present.
    /// </summary>
    public static IServiceCollection AddDeepSigmaWebSockets(this IServiceCollection services, JsonSerializerOptions? jsonOptions = null)
    {
        services.AddSingleton<IWebSocketConnectionFactory>(sp => new WebSocketConnectionFactory(
            jsonOptions,
            sp.GetService<ILoggerFactory>(),
            sp.GetService<TimeProvider>()));
        return services;
    }

    /// <summary>
    /// Registers the DeepSigma web transports in one call. With no <paramref name="configure"/>, all three
    /// (<see cref="HttpApi"/>, <see cref="SseClient"/>, and the WebSocket factory) are registered with
    /// defaults. Pass a callback to select and configure a subset, e.g.
    /// <c>AddDeepSigmaWeb(w =&gt; w.AddHttp(c =&gt; c.BaseAddress = baseUri).AddSse())</c>.
    /// </summary>
    public static IServiceCollection AddDeepSigmaWeb(this IServiceCollection services, Action<DeepSigmaWebBuilder>? configure = null)
    {
        var builder = new DeepSigmaWebBuilder(services);
        if (configure is null)
        {
            builder.AddHttp().AddSse().AddWebSockets();
        }
        else
        {
            configure(builder);
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
