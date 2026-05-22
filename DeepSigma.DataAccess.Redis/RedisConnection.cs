using StackExchange.Redis;

namespace DeepSigma.DataAccess.Redis;

/// <summary>
/// A shared, long-lived handle to a Redis instance. Owns a single
/// <see cref="IConnectionMultiplexer"/> and exposes the underlying
/// <see cref="IDatabase"/> so multiple consumers (cache APIs, pub/sub, module
/// commands such as RediSearch <c>FT.*</c> or RedisJSON <c>JSON.*</c>) can share
/// one connection.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConnectionMultiplexer"/> is expensive to construct and intended
/// to be created once per process; this type wraps that lifecycle.
/// </para>
/// <para>
/// Use <see cref="RedisCacheApi"/>'s overload that accepts a
/// <see cref="RedisConnection"/> when you need both the cache surface and
/// direct multiplexer access (e.g. for module commands) without paying for two
/// connections.
/// </para>
/// </remarks>
public sealed class RedisConnection : IDisposable
{
    private bool _disposed;

    /// <summary>The underlying connection multiplexer.</summary>
    public IConnectionMultiplexer Multiplexer { get; }

    /// <summary>The default database on the multiplexer.</summary>
    public IDatabase Database => Multiplexer.GetDatabase();

    /// <summary>Opens a new multiplexer to the supplied connection string.</summary>
    public RedisConnection(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        Multiplexer = ConnectionMultiplexer.Connect(connectionString);
    }

    /// <summary>Wraps a caller-supplied multiplexer. The wrapped instance is disposed when this object is disposed.</summary>
    public RedisConnection(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        Multiplexer = multiplexer;
    }

    /// <summary>Disposes the underlying multiplexer.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Multiplexer.Dispose();
        _disposed = true;
    }
}
