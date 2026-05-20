using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.Sqlite.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.Sqlite.Tests;

public class SqliteSchemaServiceTests : IClassFixture<SqliteSharedMemoryFixture>
{
    private readonly SqliteSchemaService _schema;

    public SqliteSchemaServiceTests(SqliteSharedMemoryFixture fixture)
    {
        _schema = new SqliteSchemaService(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetTables_returns_user_defined_tables()
    {
        IEnumerable<TableName> tables = await _schema.GetTablesAsync(TestContext.Current.CancellationToken);
        var names = tables.Select(t => t.Name).ToHashSet();

        Assert.Contains("users", names);
        Assert.Contains("orders", names);
    }

    [Fact]
    public async Task GetTables_does_not_include_sqlite_internal_tables()
    {
        IEnumerable<TableName> tables = await _schema.GetTablesAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(tables, t => t.Name is not null && t.Name.StartsWith("sqlite_"));
    }

    [Fact]
    public async Task GetTableFields_includes_TableName_for_grouping()
    {
        IEnumerable<TableField> fields = await _schema.GetTableFieldsAsync(TestContext.Current.CancellationToken);

        Assert.All(fields, f => Assert.False(string.IsNullOrEmpty(f.TableName)));
        Assert.Contains(fields, f => f.TableName == "users" && f.ColumnName == "id");
        Assert.Contains(fields, f => f.TableName == "orders" && f.ColumnName == "user_id");
    }

    [Fact]
    public async Task GetTableFields_reports_HasDefault_true_for_columns_with_defaults()
    {
        IEnumerable<TableField> fields = await _schema.GetTableFieldsAsync(TestContext.Current.CancellationToken);
        TableField? createdAt = fields.FirstOrDefault(f => f.TableName == "users" && f.ColumnName == "created_at");

        Assert.NotNull(createdAt);
        Assert.True(createdAt!.HasDefault);
        Assert.False(string.IsNullOrEmpty(createdAt.ColumnDefault));
    }

    [Fact]
    public async Task GetTableFields_reports_HasDefault_false_for_columns_without_defaults()
    {
        IEnumerable<TableField> fields = await _schema.GetTableFieldsAsync(TestContext.Current.CancellationToken);
        TableField? name = fields.FirstOrDefault(f => f.TableName == "users" && f.ColumnName == "name");

        Assert.NotNull(name);
        Assert.False(name!.HasDefault);
        Assert.Null(name.ColumnDefault);
    }

    [Fact]
    public async Task GetConstraints_includes_primary_key_columns()
    {
        IEnumerable<TableConstraint> constraints = await _schema.GetConstraintsAsync(TestContext.Current.CancellationToken);
        var pks = constraints.Where(c => c.ConstraintType == "PRIMARY KEY").ToList();

        Assert.Contains(pks, c => c.TableName == "users" && c.ColumnName == "id");
        Assert.Contains(pks, c => c.TableName == "orders" && c.ColumnName == "id");
    }

    [Fact]
    public async Task GetConstraints_includes_unique_columns()
    {
        IEnumerable<TableConstraint> constraints = await _schema.GetConstraintsAsync(TestContext.Current.CancellationToken);
        var uniques = constraints.Where(c => c.ConstraintType == "UNIQUE").ToList();

        Assert.Contains(uniques, c => c.TableName == "users" && c.ColumnName == "email");
    }

    [Fact]
    public async Task GetForeignKeys_returns_declared_foreign_keys()
    {
        IEnumerable<TableForeignKey> fks = await _schema.GetForeignKeysAsync(TestContext.Current.CancellationToken);

        Assert.Contains(fks, fk => fk.ForeignColumnName == "user_id"
                                && fk.PrimaryTableName == "users"
                                && fk.PrimaryColumnName == "id");
    }
}
