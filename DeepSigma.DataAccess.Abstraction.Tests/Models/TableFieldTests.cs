using DeepSigma.DataAccess.Abstraction.Models;
using Xunit;

namespace DeepSigma.DataAccess.Abstraction.Tests.Models;

public class TableFieldTests
{
    [Fact]
    public void HasDefault_returns_false_when_ColumnDefault_is_null()
    {
        var field = new TableField { ColumnDefault = null };
        Assert.False(field.HasDefault);
    }

    [Fact]
    public void HasDefault_returns_false_when_ColumnDefault_is_empty()
    {
        var field = new TableField { ColumnDefault = "" };
        Assert.False(field.HasDefault);
    }

    [Fact]
    public void HasDefault_returns_true_when_ColumnDefault_has_value()
    {
        var field = new TableField { ColumnDefault = "(getdate())" };
        Assert.True(field.HasDefault);
    }

    [Fact]
    public void Records_with_identical_values_are_equal()
    {
        var a = new TableField { TableSchema = "dbo", TableName = "users", ColumnName = "id", DataType = "int" };
        var b = new TableField { TableSchema = "dbo", TableName = "users", ColumnName = "id", DataType = "int" };
        Assert.Equal(a, b);
    }

    [Fact]
    public void Records_with_different_values_are_not_equal()
    {
        var a = new TableField { ColumnName = "id" };
        var b = new TableField { ColumnName = "name" };
        Assert.NotEqual(a, b);
    }
}
