using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.SqlServer;
using Xunit;

namespace DeepSigma.DataAccess.SqlServer.Tests.Tests;

[Trait("Category", "Integration")]
public class SqlServerSchema_Tests
{
    const string connection = "Data Source=localhost;Database=AutoML;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;Packet Size=4096;Command Timeout=0;";

    [Fact]
    public async Task GetTablesShouldReturnValues()
    {
        SqlServerSchemaService service = new(connection);
        IEnumerable<TableName> tables = await service.GetTables();

        Assert.True(tables.Count() > 0);
    }

    [Fact]
    public async Task GetConstraintsShouldGetValues()
    {
        SqlServerSchemaService service = new(connection);
        IEnumerable<TableConstraint> tables = await service.GetConstraints();

        Assert.True(tables.Count() > 0);
    }

    [Fact]
    public async Task GetFKShouldGetValues()
    {
        SqlServerSchemaService service = new(connection);
        IEnumerable<TableForeignKey> tables = await service.GetForeignKeys();

        Assert.True(tables.Count() > 0);
    }

    [Fact]
    public async Task GetTableFieldShouldGetValues()
    {
        SqlServerSchemaService service = new(connection);
        IEnumerable<TableField> tables = await service.GetTableFields();

        Assert.True(tables.Count() > 0);
    }
}
