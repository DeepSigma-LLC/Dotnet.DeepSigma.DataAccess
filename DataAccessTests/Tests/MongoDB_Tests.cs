using DataAccessTests.Models;
using DeepSigma.DataAccess.Database;
using MongoDB.Driver;
using Xunit;

namespace DataAccessTests.Tests;

public class MongoDB_Tests
{
    private static MongoDBAPI api = new("mongodb://localhost:27017/");
    [Fact]
    public async Task InsertAsync_Test()
    {
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests());
        Assert.True(true);

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""));
    }

    [Fact]
    public async Task DeleteAsync_Test()
    {
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"));
        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb");
        Assert.True(result);

    }

    [Fact]
    public async Task DeleteManyAsync_Test()
    {
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests());

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""));
        Assert.True(result);
    }

    [Fact]
    public async Task CountAsync_Test()
    {
        await api.InsertAsync<DataRequest>("TestDB", "Requests", GetDataRequests());
        long count = await api.CountAsync<DataRequest>("TestDB", "Requests");
        Assert.True(count > 1);

        bool result = await api.DeleteManyAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name != ""));
    }

    [Fact]
    public async Task FindAsync_Test()
    {
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"));
        IEnumerable<DataRequest> results = await api.FindAsync("TestDB", "Requests", Builders<DataRequest>.Filter.Where(x => x.Name == "Test1"));
        Assert.True(results.Count() > 0);

        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb");
    }

    [Fact]
    public async Task GetByIdAsync_Test()
    {
        await api.InsertAsync("TestDB", "Requests", new DataRequest("Test1", "A test description", [1, 2], "68d0a9d8ad3512e0e907c0cb"));
        DataRequest? request = await api.GetByIdAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb");
        Assert.NotNull(request);

        bool result = await api.DeleteAsync<DataRequest>("TestDB", "Requests", "68d0a9d8ad3512e0e907c0cb");
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
