namespace DeepSigma.DataAccess.Web.Throttling;

/// <summary>What <see cref="HttpPipeline"/> puts in front of the handler that sends.</summary>
public sealed record HttpPipelineOptions
{
    /// <summary>Retry on the configured statuses, honoring <c>Retry-After</c> (<see cref="RetryAfterDelegatingHandler"/>);
    /// none when null.</summary>
    public RetryAfterOptions? Retry { get; init; }

    /// <summary>Space requests sharing a key (<see cref="MinIntervalDelegatingHandler"/>); none when null. It sits inside
    /// the retry, so every attempt is spaced, retries included.</summary>
    public MinIntervalOptions? Throttle { get; init; }

    /// <summary>The clock both handlers wait on; <see cref="TimeProvider.System"/> when null. A test passes a fake one.</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>The client's base address; none when null.</summary>
    public Uri? BaseAddress { get; init; }

    /// <summary>The client's own timeout for one send, retries and waits included; <see cref="HttpClient"/>'s default
    /// (100 seconds) when null. A caller that times each call itself passes <see cref="Timeout.InfiniteTimeSpan"/>.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// The handlers composed without dependency injection, for a library that makes its own <see cref="HttpClient"/>: retry
/// (outermost), then spacing, then the handler that sends. The same composition as
/// <c>AddRetryAfterPolicy().AddMinIntervalThrottle()</c> on an <c>IHttpClientBuilder</c>.
/// </summary>
public static class HttpPipeline
{
    /// <summary>A client sending through <paramref name="handler"/> with the options' retry and spacing in front.</summary>
    /// <param name="handler">The handler that sends: a <see cref="SocketsHttpHandler"/>, or a fake one in a test.</param>
    /// <param name="options">What goes in front; nothing when null.</param>
    /// <param name="disposeHandler">Whether disposing the client disposes <paramref name="handler"/>. False when the handler
    /// is shared by several clients, or belongs to the caller.</param>
    public static HttpClient CreateClient(HttpMessageHandler handler, HttpPipelineOptions? options = null, bool disposeHandler = true)
    {
        options ??= new HttpPipelineOptions();
        var client = new HttpClient(CreateHandler(handler, options, disposeHandler));
        if (options.BaseAddress is { } address) client.BaseAddress = address;
        if (options.Timeout is { } timeout) client.Timeout = timeout;
        return client;
    }

    /// <summary>The handler chain alone, for a caller that wraps it further or makes the client itself.</summary>
    /// <inheritdoc cref="CreateClient" path="/param"/>
    public static HttpMessageHandler CreateHandler(HttpMessageHandler handler, HttpPipelineOptions? options = null, bool disposeHandler = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new HttpPipelineOptions();
        HttpMessageHandler pipeline = disposeHandler ? handler : new NonDisposingHandler(handler);
        if (options.Throttle is { } throttle)
            pipeline = new MinIntervalDelegatingHandler(throttle, options.Clock) { InnerHandler = pipeline };
        if (options.Retry is { } retry)
            pipeline = new RetryAfterDelegatingHandler(retry, options.Clock) { InnerHandler = pipeline };
        return pipeline;
    }
}

/// <summary>Passes requests to a handler it does not own: disposing the chain in front leaves that handler open, for
/// another client or its owner.</summary>
public sealed class NonDisposingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <inheritdoc/>
    protected override void Dispose(bool disposing) { }
}
