using MongoDB.Driver;
using DeepSigma.DataAccess.Models;

namespace DeepSigma.DataAccess.Database;

/// <summary>
/// Provides methods to interact with MongoDB, including connecting to the database and performing CRUD operations.
/// </summary>
/// <param name="connection_string"></param>
public class MongoDBAPI(string connection_string)
{
    private string ConnectionString { get; set; } = connection_string;
    private MongoClient Client { get; set; } = new(connection_string);

    /// <summary>
    /// Gets a document by its ID from the specified database and collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<T?> GetByIdAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Finds documents in the specified database and collection based on the provided filter, sort, skip, and take parameters.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="filter"></param>
    /// <param name="sort"></param>
    /// <param name="skip"></param>
    /// <param name="take"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
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
    /// Counts the number of documents in the specified database and collection that match the provided filter.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="filter"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<long> CountAsync<T>(string database_name, string collection_name, FilterDefinition<T>? filter = null, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.CountDocumentsAsync(filter ?? Builders<T>.Filter.Empty, cancellationToken: ct);
    }

    /// <summary>
    /// Inserts a new document into the specified database and collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="document"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task InsertAsync<T>(string database_name, string collection_name, T document, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        await collection.InsertOneAsync(document, cancellationToken: ct);
    }

    /// <summary>
    /// Inserts multiple documents into the specified database and collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="documents"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task InsertAsync<T>(string database_name, string collection_name, IEnumerable<T> documents, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        await collection.InsertManyAsync(documents, cancellationToken: ct);
    }

    /// <summary>
    /// Replaces an existing document in the specified database and collection. If the document does not exist and upsert is true, it will be inserted.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="document"></param>
    /// <param name="upsert"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> ReplaceAsync<T>(string database_name, string collection_name, T document, bool upsert = false, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.ReplaceOneAsync(x => x.Id == document.Id, document,
            new ReplaceOptions { IsUpsert = upsert }, ct);
        return result.IsAcknowledged && result.ModifiedCount + (upsert ? result.UpsertedId == null ? 0 : 1 : 0) > 0;
    }

    /// <summary>
    /// Deletes a document by its ID from the specified database and collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> DeleteAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IMongoDocument
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.DeleteOneAsync(x => x.Id == id, ct);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Deletes multiple documents from the specified database and collection that match the provided filter.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="filter"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> DeleteManyAsync<T>(string database_name, string collection_name, FilterDefinition<T> filter, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        var result = await collection.DeleteManyAsync(filter, ct);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    /// <summary>
    /// Creates an index on the specified database and collection with the given keys and options.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="database_name"></param>
    /// <param name="collection_name"></param>
    /// <param name="keys"></param>
    /// <param name="options"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<string> CreateIndexAsync<T>(string database_name, string collection_name, IndexKeysDefinition<T> keys, CreateIndexOptions? options = null, CancellationToken ct = default)
    {
        IMongoDatabase database = Client.GetDatabase(database_name);
        IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
        return await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, options), cancellationToken: ct);
    }
}
