using MongoDB.Driver;

namespace DeepSigma.DataAccess.MongoDB;

/// <summary>
/// Provides methods to interact with MongoDB, including connecting to the database and performing CRUD operations.
/// </summary>
public class MongoDBAPI
{
    private MongoClient Client { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDBAPI"/> class.
    /// </summary>
    public MongoDBAPI(string connection_string)
    {
        Client = new MongoClient(connection_string);
    }

    /// <summary>
    /// Gets a document by its ID from the specified database and collection.
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Finds documents matching the provided filter / sort / paging.
    /// </summary>
    public async Task<IReadOnlyList<T>> FindAsync<T>(string database_name, string collection_name, FilterDefinition<T>? filter = null, SortDefinition<T>? sort = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);

        var f = filter ?? Builders<T>.Filter.Empty;
        var find = collection.Find(f).Skip(skip).Limit(take);
        if (sort is not null) find = find.Sort(sort);

        return await find.ToListAsync(ct);
    }

    /// <summary>
    /// Counts documents in the specified collection.
    /// </summary>
    public async Task<long> CountAsync<T>(string database_name, string collection_name, FilterDefinition<T>? filter = null, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.CountDocumentsAsync(filter ?? Builders<T>.Filter.Empty, cancellationToken: ct);
    }

    /// <summary>
    /// Inserts a new document.
    /// </summary>
    public async Task InsertAsync<T>(string database_name, string collection_name, T document, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        await collection.InsertOneAsync(document, cancellationToken: ct);
    }

    /// <summary>
    /// Inserts multiple documents.
    /// </summary>
    public async Task InsertAsync<T>(string database_name, string collection_name, IEnumerable<T> documents, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        await collection.InsertManyAsync(documents, cancellationToken: ct);
    }

    /// <summary>
    /// Replaces an existing document (optionally upserting).
    /// </summary>
    public async Task<bool> ReplaceAsync<T>(string database_name, string collection_name, T document, bool upsert = false, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.ReplaceOneAsync(x => x.Id == document.Id, document,
            new ReplaceOptions { IsUpsert = upsert }, ct);
        return result.IsAcknowledged && result.ModifiedCount + (upsert ? result.UpsertedId == null ? 0 : 1 : 0) > 0;
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    public async Task<bool> DeleteAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.DeleteOneAsync(x => x.Id == id, ct);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Deletes multiple documents matching the provided filter.
    /// </summary>
    public async Task<bool> DeleteManyAsync<T>(string database_name, string collection_name, FilterDefinition<T> filter, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.DeleteManyAsync(filter, ct);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Creates an index on the specified collection.
    /// </summary>
    public async Task<string> CreateIndexAsync<T>(string database_name, string collection_name, IndexKeysDefinition<T> keys, CreateIndexOptions? options = null, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, options), cancellationToken: ct);
    }
}
