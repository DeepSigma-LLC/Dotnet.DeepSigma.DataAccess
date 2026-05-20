using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DeepSigma.Core.Utilities;
using DeepSigma.DataAccess.Cosmos.Exceptions;

namespace DeepSigma.DataAccess.Cosmos;

/// <summary>
/// Provides methods to interact with Azure Cosmos DB, including creating databases and containers,
/// inserting, querying, updating, and deleting items.
/// </summary>
/// <remarks>
/// Holds a single long-lived <see cref="CosmosClient"/> for the lifetime of the instance, per Microsoft's
/// guidance. Dispose this instance when you are done with it (or register it as a singleton in your DI container).
/// </remarks>
public class CosmosDbApi : IDisposable
{
    private readonly CosmosClient _client;
    private readonly ILogger<CosmosDbApi> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosDbApi"/> with a long-lived <see cref="CosmosClient"/>.
    /// </summary>
    public CosmosDbApi(string endpointUri, string apiKey, string appName, ILogger<CosmosDbApi>? logger = null)
    {
        _client = new CosmosClient(endpointUri, apiKey, new CosmosClientOptions { ApplicationName = appName });
        _logger = logger ?? NullLogger<CosmosDbApi>.Instance;
    }

    /// <summary>
    /// Creates a database at the configured Cosmos instance.
    /// </summary>
    public async Task CreateDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        await _client.CreateDatabaseIfNotExistsAsync(databaseId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates the container if it does not exist.
    /// </summary>
    public async Task CreateContainerAsync(string databaseId, string containerId, string partitionKeyPath, int? throughput = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Database db = _client.GetDatabase(databaseId);
        await db.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKeyPath, throughput, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Scales the throughput (RU/s) of an existing manual-throughput container.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the container is configured for autoscale (manual throughput cannot be read/replaced).
    /// </exception>
    public async Task ScaleContainerAsync(string databaseId, string containerId, int throughputIncrease, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(databaseId, containerId);
        int? currentThroughput = await container.ReadThroughputAsync(cancellationToken);
        if (!currentThroughput.HasValue)
        {
            throw new InvalidOperationException(
                $"Container '{containerId}' in database '{databaseId}' does not expose manual throughput " +
                "(likely configured for autoscale). Use the autoscale-specific APIs instead.");
        }
        int newThroughput = currentThroughput.Value + throughputIncrease;
        await container.ReplaceThroughputAsync(newThroughput, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Inserts an item into the container. Single round-trip; race-free.
    /// </summary>
    /// <exception cref="CosmosDuplicateItemException">Thrown if an item with the same id and partition key already exists.</exception>
    public async Task<T> InsertAsync<T>(string databaseId, string containerId, T item, Expression<Func<T, dynamic>> idProperty, Expression<Func<T, dynamic>> partitionKeyProperty, CancellationToken cancellationToken = default)
    {
        object? partitionKeyValue = ObjectUtilities.GetPropertyValue<T, object>(item, partitionKeyProperty);
        PartitionKey partitionKey = ToPartitionKey(partitionKeyValue);
        string? idValue = ObjectUtilities.GetPropertyValue<T, string>(item, idProperty);
        Container container = _client.GetContainer(databaseId, containerId);
        try
        {
            ItemResponse<T> createResponse = await container.CreateItemAsync(item, partitionKey, cancellationToken: cancellationToken);
            return createResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CosmosDuplicateItemException(containerId, idValue, ex);
        }
    }

    /// <summary>
    /// Runs a query (Azure Cosmos DB SQL syntax) against the container.
    /// </summary>
    public async Task<List<T>> QueryItemsAsync<T>(string databaseId, string containerId, string sqlQueryText, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(databaseId, containerId);
        QueryDefinition queryDefinition = new(sqlQueryText);
        FeedIterator<T> queryResultSetIterator = container.GetItemQueryIterator<T>(queryDefinition);
        List<T> results = [];

        while (queryResultSetIterator.HasMoreResults)
        {
            FeedResponse<T> currentResultSet = await queryResultSetIterator.ReadNextAsync(cancellationToken);
            foreach (T result in currentResultSet)
            {
                results.Add(result);
            }
        }
        return results;
    }

    /// <summary>
    /// Replaces an item in the container. Single round-trip; race-free.
    /// </summary>
    /// <exception cref="CosmosItemNotFoundException">Thrown if no item with the given id and partition key exists.</exception>
    public async Task<T> UpdateItemAsync<T>(string databaseId, string containerId, T newItem, Expression<Func<T, dynamic>> idProperty, Expression<Func<T, dynamic>> partitionKeyProperty, CancellationToken cancellationToken = default)
    {
        string? idValue = ObjectUtilities.GetPropertyValue<T, string>(newItem, idProperty);
        object? partitionKeyValue = ObjectUtilities.GetPropertyValue<T, object>(newItem, partitionKeyProperty);
        PartitionKey partitionKey = ToPartitionKey(partitionKeyValue);
        Container container = _client.GetContainer(databaseId, containerId);
        try
        {
            ItemResponse<T> updateResponse = await container.ReplaceItemAsync(newItem, idValue, partitionKey, cancellationToken: cancellationToken);
            return updateResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new CosmosItemNotFoundException(containerId, idValue, ex);
        }
    }

    /// <summary>
    /// Deletes an item from the container by id and partition key value.
    /// </summary>
    /// <param name="databaseId">The database identifier.</param>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="id">The document id.</param>
    /// <param name="partitionKeyValue">The partition key value. Supports <c>string</c>, <c>double</c>, <c>bool</c>, or <c>null</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="CosmosItemNotFoundException">Thrown if no item with the given id and partition key exists.</exception>
    public async Task DeleteItemAsync<T>(string databaseId, string containerId, string id, object? partitionKeyValue, CancellationToken cancellationToken = default)
    {
        PartitionKey partitionKey = ToPartitionKey(partitionKeyValue);
        Container container = _client.GetContainer(databaseId, containerId);
        try
        {
            await container.DeleteItemAsync<T>(id, partitionKey, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new CosmosItemNotFoundException(containerId, id, ex);
        }
    }

    /// <summary>
    /// Deletes the entire database.
    /// </summary>
    public async Task DeleteDatabaseAndCleanupAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Database db = _client.GetDatabase(databaseId);
        await db.DeleteAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="PartitionKey"/> from a value, supporting all native Cosmos partition-key types.
    /// </summary>
    private static PartitionKey ToPartitionKey(object? value) => value switch
    {
        null => PartitionKey.None,
        string s => new PartitionKey(s),
        bool b => new PartitionKey(b),
        double d => new PartitionKey(d),
        float f => new PartitionKey(f),
        int i => new PartitionKey(i),
        long l => new PartitionKey(l),
        decimal dec => new PartitionKey((double)dec),
        _ => throw new ArgumentException(
            $"Partition key type '{value.GetType().Name}' is not supported. Cosmos supports string, bool, and numeric partition keys.",
            nameof(value)),
    };

    /// <summary>
    /// Disposes the underlying <see cref="CosmosClient"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
