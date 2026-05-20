using DeepSigma.DataAccess.MongoDB;
using DeepSigma.DataAccess.MongoDB.Tests.Models;
using MongoDB.Driver;
using Xunit;

namespace DeepSigma.DataAccess.MongoDB.Tests.Tests;

[Trait("Category", "Integration")]
public class MongoDB_Tests
{
    private static MongoDbApi api = new("mongodb://localhost:27017/");

    [Fact]
    public async Task InsertAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests(), cancellationToken: ct);
        Assert.True(true);

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""), cancellationToken: ct);
    }

    [Fact]
    public async Task DeleteAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"), cancellationToken: ct);
        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb", cancellationToken: ct);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteManyAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests(), cancellationToken: ct);

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""), cancellationToken: ct);
        Assert.True(result);
    }

    [Fact]
    public async Task CountAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests(), cancellationToken: ct);
        long count = await api.CountAsync<DataRequest>("TestDB", "Requests", cancellationToken: ct);
        Assert.True(count > 1);

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""), cancellationToken: ct);
    }

    [Fact]
    public async Task FindAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"), cancellationToken: ct);
        IEnumerable<DataRequest> results = await api.FindAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name == "Test1"), cancellationToken: ct);
        Assert.True(results.Count() > 0);

        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb", cancellationToken: ct);
    }

    [Fact]
    public async Task GetByIdAsync_Test()
    {
        var ct = TestContext.Current.CancellationToken;
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"), cancellationToken: ct);
        DataRequest? request = await api.GetByIdAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb", cancellationToken: ct);
        Assert.NotNull(request);

        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb", cancellationToken: ct);
    }

    private static List<DataRequest> GetDataRequests()
    {
        List<DataRequest> requests = [
            new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"),
            new DataRequest("Test2", "A test description", [1, 2, 3], "68d0a9d8ad3512e0e907c0cc"),
            new DataRequest("Test3", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cd"),
            new DataRequest("Test4", "A test description", [4, 2], "68d0a9d8ad3512e0e907c0ce"),
            new DataRequest("Test5", "A test description", [13, 2], "68d0a9d8ad3512e0e907c0cf"),
            new DataRequest("Test6", "A test description", [11, 2], "68d0a9d8ad3512e0e907c0d0"),
            ];
        return requests;
    }
}
