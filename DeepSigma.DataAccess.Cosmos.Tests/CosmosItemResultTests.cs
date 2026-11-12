using System.Net;
using DeepSigma.DataAccess.Cosmos.Results;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using Xunit;

namespace DeepSigma.DataAccess.Cosmos.Tests;

public class CosmosItemResultTests
{
    private const string DatabaseId = "database";
    private const string ContainerId = "container";
    private readonly CosmosClient _client = Substitute.For<CosmosClient>();
    private readonly Container _container = Substitute.For<Container>();
    private readonly Item _item = new() { Id = "item-1", TenantId = "tenant-1" };

    public CosmosItemResultTests()
    {
        _client.GetContainer(DatabaseId, ContainerId).Returns(_container);
    }

    [Fact]
    public async Task Insert_returns_created_item_and_forwards_request()
    {
        using var cts = new CancellationTokenSource();
        ItemResponse<Item> response = Substitute.For<ItemResponse<Item>>();
        response.Resource.Returns(_item);
        _container.CreateItemAsync(_item, new PartitionKey("tenant-1"), cancellationToken: cts.Token).Returns(response);
        using var api = new CosmosDbApi(_client);

        CosmosInsertResult<Item> result = await api.InsertAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId, cts.Token);

        Assert.Equal("created:item-1", Describe(result));
        Assert.Same(_item, Assert.IsType<CosmosCreated<Item>>(result.Value).Item);
        await _container.Received(1).CreateItemAsync(_item, new PartitionKey("tenant-1"), cancellationToken: cts.Token);
    }

    [Fact]
    public async Task Insert_returns_conflict_with_original_exception_and_context()
    {
        var error = Error(HttpStatusCode.Conflict);
        _container.CreateItemAsync(_item, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        using var api = new CosmosDbApi(_client);

        CosmosInsertResult<Item> result = await api.InsertAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId);

        Assert.Equal("conflict:item-1", Describe(result));
        var conflict = Assert.IsType<CosmosConflict>(result.Value);
        Assert.Equal(DatabaseId, conflict.DatabaseId);
        Assert.Equal(ContainerId, conflict.ContainerId);
        Assert.Equal(_item.Id, conflict.ItemId);
        Assert.Same(error, conflict.Exception);
    }

    [Fact]
    public async Task Update_returns_updated_item_and_forwards_request()
    {
        using var cts = new CancellationTokenSource();
        ItemResponse<Item> response = Substitute.For<ItemResponse<Item>>();
        response.Resource.Returns(_item);
        _container.ReplaceItemAsync(_item, _item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token).Returns(response);
        using var api = new CosmosDbApi(_client);

        CosmosUpdateResult<Item> result = await api.UpdateItemAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId, cts.Token);

        Assert.Equal("updated:item-1", Describe(result));
        Assert.Same(_item, Assert.IsType<CosmosUpdated<Item>>(result.Value).Item);
        await _container.Received(1).ReplaceItemAsync(_item, _item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token);
    }

    [Fact]
    public async Task Update_returns_resource_not_found_with_original_exception_and_context()
    {
        var error = Error(HttpStatusCode.NotFound);
        _container.ReplaceItemAsync(_item, _item.Id, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        using var api = new CosmosDbApi(_client);

        CosmosUpdateResult<Item> result = await api.UpdateItemAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId);

        Assert.Equal("not-found:item-1", Describe(result));
        AssertNotFound(result.Value, error);
    }

    [Fact]
    public async Task Delete_returns_deleted_and_forwards_request()
    {
        using var cts = new CancellationTokenSource();
        _container.DeleteItemAsync<Item>(_item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token)
            .Returns(Substitute.For<ItemResponse<Item>>());
        using var api = new CosmosDbApi(_client);

        CosmosDeleteResult result = await api.DeleteItemAsync<Item>(DatabaseId, ContainerId, _item.Id, _item.TenantId, cts.Token);

        Assert.Equal("deleted", Describe(result));
        Assert.IsType<CosmosDeleted>(result.Value);
        await _container.Received(1).DeleteItemAsync<Item>(_item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token);
    }

    [Fact]
    public async Task Delete_returns_resource_not_found_with_original_exception_and_context()
    {
        var error = Error(HttpStatusCode.NotFound);
        _container.DeleteItemAsync<Item>(_item.Id, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        using var api = new CosmosDbApi(_client);

        CosmosDeleteResult result = await api.DeleteItemAsync<Item>(DatabaseId, ContainerId, _item.Id, _item.TenantId);

        Assert.Equal("not-found:item-1", Describe(result));
        AssertNotFound(result.Value, error);
    }

    [Theory]
    [InlineData("insert", HttpStatusCode.NotFound, 0)]
    [InlineData("insert", HttpStatusCode.Conflict, 1000)]
    [InlineData("update", HttpStatusCode.Conflict, 0)]
    [InlineData("update", HttpStatusCode.NotFound, 1002)]
    [InlineData("delete", HttpStatusCode.NotFound, 1002)]
    [InlineData("delete", HttpStatusCode.TooManyRequests, 0)]
    public async Task Other_cosmos_errors_propagate_unchanged(string operation, HttpStatusCode status, int substatus)
    {
        var error = Error(status, substatus);
        _container.CreateItemAsync(_item, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        _container.ReplaceItemAsync(_item, _item.Id, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        _container.DeleteItemAsync<Item>(_item.Id, new PartitionKey("tenant-1"), cancellationToken: default).Returns<Task<ItemResponse<Item>>>(_ => throw error);
        using var api = new CosmosDbApi(_client);

        CosmosException thrown = await Assert.ThrowsAsync<CosmosException>(() => Invoke(api, operation, default));

        Assert.Same(error, thrown);
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Cancellation_propagates(string operation)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _container.CreateItemAsync(_item, new PartitionKey("tenant-1"), cancellationToken: cts.Token).Returns<Task<ItemResponse<Item>>>(_ => throw new OperationCanceledException(cts.Token));
        _container.ReplaceItemAsync(_item, _item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token).Returns<Task<ItemResponse<Item>>>(_ => throw new OperationCanceledException(cts.Token));
        _container.DeleteItemAsync<Item>(_item.Id, new PartitionKey("tenant-1"), cancellationToken: cts.Token).Returns<Task<ItemResponse<Item>>>(_ => throw new OperationCanceledException(cts.Token));
        using var api = new CosmosDbApi(_client);

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => Invoke(api, operation, cts.Token));

        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Fact]
    public void Default_results_match_null_case()
    {
        Assert.Equal("uninitialized", Describe(default(CosmosInsertResult<Item>)));
        Assert.Equal("uninitialized", Describe(default(CosmosUpdateResult<Item>)));
        Assert.Equal("uninitialized", Describe(default(CosmosDeleteResult)));
    }

    [Fact]
    public async Task Delete_with_unsupported_partition_key_throws_before_calling_sdk()
    {
        using var api = new CosmosDbApi(_client);

        await Assert.ThrowsAsync<ArgumentException>(() => api.DeleteItemAsync<Item>(DatabaseId, ContainerId, _item.Id, new object()));

        await _container.DidNotReceiveWithAnyArgs().DeleteItemAsync<Item>(default!, default, default, default);
    }

    private static string Describe(CosmosInsertResult<Item> result) => result switch
    {
        CosmosCreated<Item> created => $"created:{created.Item.Id}",
        CosmosConflict conflict => $"conflict:{conflict.ItemId}",
        null => "uninitialized",
    };

    private static string Describe(CosmosUpdateResult<Item> result) => result switch
    {
        CosmosUpdated<Item> updated => $"updated:{updated.Item.Id}",
        CosmosResourceNotFound missing => $"not-found:{missing.ItemId}",
        null => "uninitialized",
    };

    private static string Describe(CosmosDeleteResult result) => result switch
    {
        CosmosDeleted => "deleted",
        CosmosResourceNotFound missing => $"not-found:{missing.ItemId}",
        null => "uninitialized",
    };

    private Task Invoke(CosmosDbApi api, string operation, CancellationToken token) => operation switch
    {
        "insert" => api.InsertAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId, token),
        "update" => api.UpdateItemAsync(DatabaseId, ContainerId, _item, x => x.Id, x => x.TenantId, token),
        "delete" => api.DeleteItemAsync<Item>(DatabaseId, ContainerId, _item.Id, _item.TenantId, token),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private void AssertNotFound(object? value, CosmosException error)
    {
        var notFound = Assert.IsType<CosmosResourceNotFound>(value);
        Assert.Equal(DatabaseId, notFound.DatabaseId);
        Assert.Equal(ContainerId, notFound.ContainerId);
        Assert.Equal(_item.Id, notFound.ItemId);
        Assert.Same(error, notFound.Exception);
    }

    private static CosmosException Error(HttpStatusCode status, int substatus = 0)
        => new("Cosmos test failure", status, substatus, "activity-id", 1);

    public sealed class Item
    {
        public string Id { get; set; } = "";
        public string TenantId { get; set; } = "";
    }
}
