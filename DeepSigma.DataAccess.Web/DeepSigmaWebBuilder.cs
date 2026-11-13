using System.Text.Json;

// ReSharper disable once CheckNamespace -- co-located with the DI extensions.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Fluent selector for <see cref="DeepSigmaHttpServiceCollectionExtensions.AddDeepSigmaWeb"/>: add and
/// configure exactly the transports you need. Each method delegates to the corresponding granular
/// <c>AddDeepSigma*</c> registration.
/// </summary>
public sealed class DeepSigmaWebBuilder
{
    internal DeepSigmaWebBuilder(IServiceCollection services) => Services = services;

    /// <summary>The underlying service collection, for further composition.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Registers <c>HttpApi</c>/<c>IHttpApi</c>. See <see cref="DeepSigmaHttpServiceCollectionExtensions.AddDeepSigmaHttp"/>.</summary>
    public DeepSigmaWebBuilder AddHttp(Action<System.Net.Http.HttpClient>? configureClient = null)
    {
        Services.AddDeepSigmaHttp(configureClient);
        return this;
    }

    /// <summary>Registers <c>SseClient</c>/<c>ISseClient</c>. See <see cref="DeepSigmaHttpServiceCollectionExtensions.AddDeepSigmaSse"/>.</summary>
    public DeepSigmaWebBuilder AddSse(Action<System.Net.Http.HttpClient>? configureClient = null)
    {
        Services.AddDeepSigmaSse(configureClient);
        return this;
    }

    /// <summary>Registers the WebSocket factory. See <see cref="DeepSigmaHttpServiceCollectionExtensions.AddDeepSigmaWebSockets"/>.</summary>
    public DeepSigmaWebBuilder AddWebSockets(JsonSerializerOptions? jsonOptions = null)
    {
        Services.AddDeepSigmaWebSockets(jsonOptions);
        return this;
    }
}
