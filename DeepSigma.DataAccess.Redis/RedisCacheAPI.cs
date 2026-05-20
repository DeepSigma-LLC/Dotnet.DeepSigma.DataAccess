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
    public async Task<T?> GetCacheData<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }
        return JsonConvert.DeserializeObject<T>(value);
    }

    /// <summary>
    /// Removes the cached value for the given key.
    /// </summary>
    public async Task<object> RemoveCacheData(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool valueExists = await _database.KeyExistsAsync(key);
        if (valueExists)
        {
            return await _database.KeyDeleteAsync(key);
        }
        return false;
    }

    /// <summary>
    /// Sets the cached value for the given key, with an expiration.
    /// </summary>
    public async Task<bool> SetCacheData<T>(string key, T value, DateTimeOffset expirationTime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
        string valueString = JsonConvert.SerializeObject(value);
        bool isSet = await _database.StringSetAsync(key, valueString, expiryTime);
        return isSet;
    }
}
