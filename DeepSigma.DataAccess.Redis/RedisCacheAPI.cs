using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DeepSigma.DataAccess.Redis;

/// <summary>
/// API for interacting with a Redis cache.
/// </summary>
/// <remarks>
/// <see cref="StackExchange.Redis"/> does not accept a <see cref="CancellationToken"/> on its async methods;
/// cancellation is honoured cooperatively by calling <see cref="CancellationToken.ThrowIfCancellationRequested"/>
/// before issuing each command. Once the command is in-flight to the server, it cannot be cancelled.
/// </remarks>
public class RedisCacheApi
{
    private readonly string _redisConnectionString;
    private readonly string _redisInstanceName;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheApi> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisCacheApi"/>.
    /// </summary>
    public RedisCacheApi(string redisConnectionString, string redisInstanceName, ILogger<RedisCacheApi>? logger = null)
    {
        _redisConnectionString = redisConnectionString;
        _redisInstanceName = redisInstanceName;
        IConnectionMultiplexer connection = ConnectionMultiplexer.Connect(_redisConnectionString);
        _database = connection.GetDatabase();
        _logger = logger ?? NullLogger<RedisCacheApi>.Instance;
    }

    /// <summary>
    /// Gets the cached value for the given key.
    /// </summary>
    public async Task<T?> GetCacheDataAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }
        // value is non-null/non-empty here because of the IsNullOrEmpty guard above;
        // the implicit RedisValue→string conversion is typed as nullable, so explicitly bang it for the compiler.
        return JsonConvert.DeserializeObject<T>(value!);
    }

    /// <summary>
    /// Removes the cached value for the given key. Returns <c>true</c> if the key existed and was deleted,
    /// <c>false</c> if it did not exist. Single Redis round-trip.
    /// </summary>
    public async Task<bool> RemoveCacheDataAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // KeyDeleteAsync returns true if the key was deleted, false if it didn't exist —
        // no need for a separate KeyExistsAsync probe.
        return await _database.KeyDeleteAsync(key);
    }

    /// <summary>
    /// Sets the cached value for the given key, with an expiration.
    /// </summary>
    public async Task<bool> SetCacheDataAsync<T>(string key, T value, DateTimeOffset expirationTime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
        string valueString = JsonConvert.SerializeObject(value);
        bool isSet = await _database.StringSetAsync(key, valueString, expiryTime);
        return isSet;
    }
}
