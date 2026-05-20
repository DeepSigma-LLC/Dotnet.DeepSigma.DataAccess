using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using DeepSigma.Core.Utilities;

namespace DeepSigma.DataAccess.Cosmos;

/// <summary>
/// Provides methods to interact with Azure Cosmos DB, including creating databases and containers,
/// inserting, querying, updating, and deleting items.
/// </summary>
public class CosmosDBAPI
{
    private readonly string EndpointUri;
    private readonly string PrimaryKey;
    private readonly string AppName;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosDBAPI"/>.
    /// </summary>
    public CosmosDBAPI(string end_point_uri, string api_key, string app_name)
    {
        EndpointUri = end_point_uri;
        PrimaryKey = api_key;
        AppName = app_name;
    }

    private CosmosClient InstatiateCosmosClient()
    {
        return new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = AppName });
    }

    /// <summary>
    /// Creates a database at the configured Cosmos instance.
    /// </summary>
    public async Task CreateDatabaseAsync(string databaseIdName)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseIdName);
        }
    }

    /// <summary>
    /// Creates the container if it does not exist.
    /// </summary>
    public async Task CreateContainerAsync(string databaseId, string containerIdName, string PartitionKeyPath, int? throughput = null)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Microsoft.Azure.Cosmos.Database db = cosmosClient.GetDatabase(databaseId);
            await db.CreateContainerIfNotExistsAsync(containerIdName, "/" + PartitionKeyPath, throughput);
        }
    }

    /// <summary>
    /// Scales the throughput (RU/s) of an existing container.
    /// </summary>
    public async Task ScaleContainerAsync(string databaseId, string containerId, int throughputIncrease)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            int? currentThroughput = await container.ReadThroughputAsync();
            if (currentThroughput.HasValue)
            {
                int newThroughput = currentThroughput.Value + throughputIncrease;
                await container.ReplaceThroughputAsync(newThroughput);
            }
        }
    }

    /// <summary>
    /// Adds an item to the container.
    /// </summary>
    public async Task<T> InsertAsync<T>(string databaseId, string containerId, T item, Expression<Func<T, dynamic>> idProperty, Expression<Func<T, dynamic>> partitionKeyProperty)
    {
        string? partitionKeyValue = ObjectUtilities.GetPropertyValue<T, string>(item, partitionKeyProperty);
        PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
        string? idPropertyValue = ObjectUtilities.GetPropertyValue<T, string>(item, idProperty);
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            try
            {
                ItemResponse<T> readResponse = await container.ReadItemAsync<T>(idPropertyValue, partitionKey);
                throw new Exception("A duplicate record attempted to be inserted.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                ItemResponse<T> createResponse = await container.CreateItemAsync(item, partitionKey);
                return createResponse.Resource;
            }
        }
    }

    /// <summary>
    /// Runs a query (Azure Cosmos DB SQL syntax) against the container.
    /// </summary>
    public async Task<List<T>> QueryItemsAsync<T>(string databaseId, string containerId, string sqlQueryText)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            QueryDefinition queryDefinition = new(sqlQueryText);
            FeedIterator<T> queryResultSetIterator = container.GetItemQueryIterator<T>(queryDefinition);
            List<T> results = [];

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<T> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (T result in currentResultSet)
                {
                    results.Add(result);
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Updates an item in the container.
    /// </summary>
    public async Task<T> UpdateItemAsync<T>(string databaseId, string containerId, T newItem, Expression<Func<T, dynamic>> idProperty, Expression<Func<T, dynamic>> partitionKeyProperty)
    {
        string? idPropertyValue = ObjectUtilities.GetPropertyValue<T, string>(newItem, idProperty);
        string? partitionKeyValue = ObjectUtilities.GetPropertyValue<T, string>(newItem, partitionKeyProperty);
        PartitionKey partitionKey = new PartitionKey(partitionKeyValue);
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            try
            {
                ItemResponse<T> dataResponse = await container.ReadItemAsync<T>(idPropertyValue, partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new Exception("No record exists for update.");
            }
            ItemResponse<T> updateResponse = await container.ReplaceItemAsync(newItem, idPropertyValue, partitionKey);
            return updateResponse.Resource;
        }
    }

    /// <summary>
    /// Deletes an item from the container.
    /// </summary>
    public async Task DeleteItemAsync<T>(string databaseId, string containerId, string id, Func<T, PropertyInformation> partitionKeyProperty)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            PartitionKey partitionKey = new PartitionKey(partitionKeyProperty.GetType().Name);
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            ItemResponse<T> deleteResponse = await container.DeleteItemAsync<T>(id, partitionKey);
        }
    }

    /// <summary>
    /// Deletes the database and disposes of the Cosmos Client instance.
    /// </summary>
    public async Task DeleteDatabaseAndCleanupAsync(string databaseId)
    {
        using (CosmosClient cosmosClient = InstatiateCosmosClient())
        {
            Microsoft.Azure.Cosmos.Database db = cosmosClient.GetDatabase(databaseId);
            DatabaseResponse databaseResourceResponse = await db.DeleteAsync();
        }
    }
}
