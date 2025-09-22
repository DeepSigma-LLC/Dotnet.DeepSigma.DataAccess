using MongoDB.Driver;
using DeepSigma.DataAccess.Models;

namespace DeepSigma.DataAccess.Database
{
    /// <summary>
    /// Provides methods to interact with MongoDB, including connecting to the database and performing CRUD operations.
    /// </summary>
    /// <param name="connection_string"></param>
    public class MongoDBAPI(string connection_string)
    {
        private string ConnectionString { get; set; } = connection_string;
        private MongoClient Client { get; set; } = new(connection_string);

        public async Task<T?> GetByIdAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IDocument
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            return await collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        }

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

        public async Task<long> CountAsync<T>(string database_name, string collection_name, FilterDefinition<T>? filter = null, CancellationToken ct = default)
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            return await collection.CountDocumentsAsync(filter ?? Builders<T>.Filter.Empty, cancellationToken: ct);
        }

        public async Task InsertAsync<T>(string database_name, string collection_name, T document, CancellationToken ct = default)
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            await collection.InsertOneAsync(document, cancellationToken: ct);
        }

        public async Task InsertAsync<T>(string database_name, string collection_name, IEnumerable<T> documents, CancellationToken ct = default)
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            await collection.InsertManyAsync(documents, cancellationToken: ct);
        }

        public async Task<bool> ReplaceAsync<T>(string database_name, string collection_name, T document, bool upsert = false, CancellationToken ct = default) where T : IDocument
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            var result = await collection.ReplaceOneAsync(x => x.Id == document.Id, document,
                new ReplaceOptions { IsUpsert = upsert }, ct);
            return result.IsAcknowledged && result.ModifiedCount + (upsert ? result.UpsertedId == null ? 0 : 1 : 0) > 0;
        }

        public async Task<bool> DeleteAsync<T>(string database_name, string collection_name, string id, CancellationToken ct = default) where T : IDocument
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            var result = await collection.DeleteOneAsync(x => x.Id == id, ct);
            return result.IsAcknowledged && result.DeletedCount > 0;
        }

        public async Task<bool> DeleteManyAsync<T>(string database_name, string collection_name, FilterDefinition<T> filter, CancellationToken ct = default)
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            var result = await collection.DeleteManyAsync(filter, ct);
            return result.IsAcknowledged && result.DeletedCount > 0;
        }

        public async Task<string> CreateIndexAsync<T>(string database_name, string collection_name, IndexKeysDefinition<T> keys, CreateIndexOptions? options = null, CancellationToken ct = default)
        {
            IMongoDatabase database = Client.GetDatabase(database_name);
            IMongoCollection<T> collection = database.GetCollection<T>(collection_name);
            return await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, options), cancellationToken: ct);
        }
    }
}
