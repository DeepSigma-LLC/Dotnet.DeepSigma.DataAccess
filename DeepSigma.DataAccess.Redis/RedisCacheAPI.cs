using Newtonsoft.Json;
using StackExchange.Redis;

namespace DeepSigma.DataAccess.Redis;

/// <summary>
/// API for interacting with a Redis cache.
/// </summary>
public class RedisCacheAPI
{
    private readonly string _redisConnectionString;
    private readonly string _redisInstanceName;
    private IDatabase _database;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisCacheAPI"/>.
    /// </summary>
    public RedisCacheAPI(string redisConnectionString, string redisInstanceName)
    {
        _redisConnectionString = redisConnectionString;
        _redisInstanceName = redisInstanceName;
        IConnectionMultiplexer connection = ConnectionMultiplexer.Connect(_redisConnectionString);
        _database = connection.GetDatabase();
    }

    /// <summary>
    /// Gets the cached value for the given key.
    /// </summary>
    public async Task<T?> GetCacheData<T>(string key)
    {
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
    public async Task<object> RemoveCacheData(string key)
    {
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
    public async Task<bool> SetCacheData<T>(string key, T value, DateTimeOffset expirationTime)
    {
        TimeSpan expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
        string valueString = JsonConvert.SerializeObject(value);
        bool isSet = await _database.StringSetAsync(key, valueString, expiryTime);
        return isSet;
    }
}
