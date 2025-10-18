using DeepSigma.DataAccess.Database;
using DeepSigma.DataAccess.Database.Models;
using Xunit;

namespace DataAccessTests.Tests;

public class SQLDatabaseSchema_Tests
{
    const string connection = "Data Source=localhost;Database=AutoML;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;Packet Size=4096;Command Timeout=0;";

    [Fact]
    public async Task GetTablesShouldReturnValues()
    {
        SQLServerDatabaseSchemaService service = new(connection);
        IEnumerable<TableName> tables = await service.GetTables();

        Assert.True(tables.Count() > 0);
    }


    [Fact]
    public async Task GetConstraintsShouldGetValues()
    {
        SQLServerDatabaseSchemaService service = new(connection);
        IEnumerable<TableConstraint> tables = await service.GetConstraints();

        Assert.True(tables.Count() > 0);
    }

    [Fact]
    public async Task GetFKShouldGetValues()
    {
        SQLServerDatabaseSchemaService service = new(connection);
        IEnumerable<TableForeignKey> tables = await service.GetForiegnKeys();

        Assert.True(tables.Count() > 0);
    }

    [Fact]
    public async Task GetTableFieldShouldGetValues()
    {
        SQLServerDatabaseSchemaService service = new(connection);
        IEnumerable<TableField> tables = await service.GetTableFields();

        Assert.True(tables.Count() > 0);
    }
}
