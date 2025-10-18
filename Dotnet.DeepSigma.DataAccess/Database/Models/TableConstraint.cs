
namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a constraint on a database table.
/// </summary>
public class TableConstraint()
{
    /// <summary>
    /// The name of the constraint.
    /// </summary>
    public string? ConstraintName { get; set; }
    /// <summary>
    /// The schema of the table containing the constraint.
    /// </summary>
    public string? TableSchema { get; set; }
    /// <summary>
    /// The name of the table containing the constraint.
    /// </summary>
    public string? TableName { get; set; }
    /// <summary>
    /// The name of the column associated with the constraint.
    /// </summary>
    public string? ColumnName { get; set; }

}


