using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace DeepSigma.DataAccess.Database
{
    internal class RedisCacheAPI
    {
        private readonly string _redisConnectionString;
        private readonly string _redisInstanceName;
        private IDatabase _database;
        public RedisCacheAPI(string redisConnectionString, string redisInstanceName)
        {
            _redisConnectionString = redisConnectionString;
            _redisInstanceName = redisInstanceName;
            IConnectionMultiplexer connection = ConnectionMultiplexer.Connect(_redisConnectionString);
            _database = connection.GetDatabase();
        }

        /// <summary>
        /// Gets the cache data from Redis.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
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
        /// Removes the cache data from Redis.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
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
        /// Sets the cache data in Redis.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expirationTime"></param>
        /// <returns></returns>
        public async Task<bool> SetCacheData<T>(string key, T value, DateTimeOffset expirationTime)
        {
            TimeSpan expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
            string valueString = JsonConvert.SerializeObject(value);
            bool isSet = await _database.StringSetAsync(key, valueString, expiryTime);
            return isSet;
        }
    }
}
