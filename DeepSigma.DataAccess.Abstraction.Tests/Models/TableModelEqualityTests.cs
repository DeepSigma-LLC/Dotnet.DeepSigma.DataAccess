using DeepSigma.DataAccess.Abstraction.Models;
using Xunit;

namespace DeepSigma.DataAccess.Abstraction.Tests.Models;

public class TableModelEqualityTests
{
    [Fact]
    public void TableName_uses_structural_equality()
    {
        var a = new TableName { TableSchema = "dbo", Name = "users" };
        var b = new TableName { TableSchema = "dbo", Name = "users" };
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void TableConstraint_uses_structural_equality_including_ConstraintType()
    {
        var a = new TableConstraint { ConstraintName = "pk_users", ConstraintType = "PRIMARY KEY", TableName = "users", ColumnName = "id" };
        var b = new TableConstraint { ConstraintName = "pk_users", ConstraintType = "PRIMARY KEY", TableName = "users", ColumnName = "id" };
        Assert.Equal(a, b);
    }

    [Fact]
    public void TableConstraint_differs_when_ConstraintType_differs()
    {
        var pk = new TableConstraint { ConstraintName = "x", ConstraintType = "PRIMARY KEY", TableName = "users", ColumnName = "id" };
        var unique = new TableConstraint { ConstraintName = "x", ConstraintType = "UNIQUE", TableName = "users", ColumnName = "id" };
        Assert.NotEqual(pk, unique);
    }

    [Fact]
    public void TableForeignKey_uses_structural_equality()
    {
        var a = new TableForeignKey { ConstraintName = "fk_a", ForeignColumnName = "user_id", PrimaryTableName = "users", PrimaryColumnName = "id" };
        var b = new TableForeignKey { ConstraintName = "fk_a", ForeignColumnName = "user_id", PrimaryTableName = "users", PrimaryColumnName = "id" };
        Assert.Equal(a, b);
    }
}
