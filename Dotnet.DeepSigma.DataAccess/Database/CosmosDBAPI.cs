using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using DeepSigma.DataAccess.Utilities;

namespace DeepSigma.DataAccess.Database
{
    /// <summary>
    /// Provides methods to interact with Azure Cosmos DB, including creating databases and containers, inserting, querying, updating, and deleting items.
    /// </summary>
    /// <param name="end_point_uri"></param>
    /// <param name="api_key"></param>
    /// <param name="app_name"></param>
    public class CosmosDBAPI(string end_point_uri, string api_key, string app_name)
    {
        private readonly string EndpointUri = end_point_uri;
        private readonly string PrimaryKey = api_key;
        private readonly string AppName = app_name;

        /// <summary>
        /// Creates instance of a cosmos client.
        /// </summary>
        /// <returns></returns>
        private CosmosClient InstatiateCosmosClient()
        {
            return new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = AppName });
        }

        /// <summary>
        ///  Creates database at configured cosmos instance.
        /// </summary>
        /// <param name="databaseIdName">DatabaseIdName</param>
        /// <returns></returns>
        public async Task CreateDatabaseAsync(string databaseIdName)
        {
            using (CosmosClient cosmosClient = InstatiateCosmosClient())
            {
                await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseIdName);
            }
        }

        /// <summary>
        /// Create the container if it does not exist. 
        /// For example, specifiy "/LastName" as the partition key when we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        public async Task CreateContainerAsync(string databaseId, string containerIdName, string PartitionKeyPath, int? throughput = null)
        {
            using (CosmosClient cosmosClient = InstatiateCosmosClient())
            {
                Microsoft.Azure.Cosmos.Database db = cosmosClient.GetDatabase(databaseId);
                await db.CreateContainerIfNotExistsAsync(containerIdName, "/" + PartitionKeyPath, throughput);
            }
        }

        /// <summary>
        /// Scale the throughput provisioned on an existing Container.
        /// You can scale the throughput (RU/s) of your container up and down to meet the needs of the workload.
        /// </summary>
        /// <returns></returns>
        public async Task ScaleContainerAsync(string databaseId, string containerId, int throughputIncrease)
        {
            using (CosmosClient cosmosClient = InstatiateCosmosClient())
            {
                Container container = cosmosClient.GetContainer(databaseId, containerId);
                int? currentThroughput = await container.ReadThroughputAsync();
                if (currentThroughput.HasValue)
                {
                    int newThroughput = currentThroughput.Value + throughputIncrease;
                    // Update throughput
                    await container.ReplaceThroughputAsync(newThroughput);
                }
            }
        }

        /// <summary>
        /// Add item to the container
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
                    ///createResponse.RequestCharge; this returns the charge of the requested query
                    return createResponse.Resource;
                }
            }
        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// Including the partition key value in the WHERE filter results in a more efficient query since it explicitly states which partition to search.
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
        /// Update an item in the container
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
        /// Delete an item in the container
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
        /// Delete the database and dispose of the Cosmos Client instance
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
}
