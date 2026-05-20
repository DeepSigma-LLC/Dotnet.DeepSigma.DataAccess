using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeepSigma.DataAccess.Sqlite.Tests;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void Create_returns_SqliteConnection()
    {
        var factory = new SqliteConnectionFactory("Data Source=:memory:");

        using IDbConnection connection = factory.Create();

        Assert.IsType<SqliteConnection>(connection);
    }

    [Fact]
    public void Create_returns_closed_connection()
    {
        var factory = new SqliteConnectionFactory("Data Source=:memory:");

        using IDbConnection connection = factory.Create();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Create_uses_supplied_connection_string()
    {
        const string connStr = "Data Source=test.db";
        var factory = new SqliteConnectionFactory(connStr);

        using var connection = (SqliteConnection)factory.Create();

        Assert.Equal(connStr, connection.ConnectionString);
    }
}
