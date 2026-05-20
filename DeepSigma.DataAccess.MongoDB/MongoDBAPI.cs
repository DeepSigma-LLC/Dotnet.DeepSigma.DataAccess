using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace DeepSigma.DataAccess.MongoDB;

/// <summary>
/// Provides methods to interact with MongoDB, including connecting to the database and performing CRUD operations.
/// </summary>
public class MongoDbApi
{
    private MongoClient Client { get; }
    private readonly ILogger<MongoDbApi> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbApi"/> class.
    /// </summary>
    public MongoDbApi(string connectionString, ILogger<MongoDbApi>? logger = null)
    {
        Client = new MongoClient(connectionString);
        _logger = logger ?? NullLogger<MongoDbApi>.Instance;
    }

    /// <summary>
    /// Gets a document by its ID from the specified database and collection.
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(string databaseName, string collectionName, string id, CancellationToken cancellationToken = default) where T : IMongoDocument
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        return await collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds documents matching the provided filter / sort / paging.
    /// </summary>
    public async Task<IReadOnlyList<T>> FindAsync<T>(string databaseName, string collectionName, FilterDefinition<T>? filter = null, SortDefinition<T>? sort = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);

        var f = filter ?? Builders<T>.Filter.Empty;
        var find = collection.Find(f).Skip(skip).Limit(take);
        if (sort is not null) find = find.Sort(sort);

        return await find.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts documents in the specified collection.
    /// </summary>
    public async Task<long> CountAsync<T>(string databaseName, string collectionName, FilterDefinition<T>? filter = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        return await collection.CountDocumentsAsync(filter ?? Builders<T>.Filter.Empty, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Inserts a new document.
    /// </summary>
    public async Task InsertAsync<T>(string databaseName, string collectionName, T document, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Inserts multiple documents.
    /// </summary>
    public async Task InsertAsync<T>(string databaseName, string collectionName, IEnumerable<T> documents, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Replaces an existing document (optionally upserting).
    /// </summary>
    public async Task<bool> ReplaceAsync<T>(string databaseName, string collectionName, T document, bool upsert = false, CancellationToken cancellationToken = default) where T : IMongoDocument
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        var result = await collection.ReplaceOneAsync(x => x.Id == document.Id, document,
            new ReplaceOptions { IsUpsert = upsert }, cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount + (upsert ? result.UpsertedId == null ? 0 : 1 : 0) > 0;
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    public async Task<bool> DeleteAsync<T>(string databaseName, string collectionName, string id, CancellationToken cancellationToken = default) where T : IMongoDocument
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        var result = await collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Deletes multiple documents matching the provided filter.
    /// </summary>
    public async Task<bool> DeleteManyAsync<T>(string databaseName, string collectionName, FilterDefinition<T> filter, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        var result = await collection.DeleteManyAsync(filter, cancellationToken);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Creates an index on the specified collection.
    /// </summary>
    public async Task<string> CreateIndexAsync<T>(string databaseName, string collectionName, IndexKeysDefinition<T> keys, CreateIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MongoDB operation on {Database}/{Collection}", databaseName, collectionName);
        IMongoDatabase database = Client.GetDatabase(databaseName);
        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
        return await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, options), cancellationToken: cancellationToken);
    }
}
