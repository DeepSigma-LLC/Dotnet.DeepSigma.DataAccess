

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a foreign key constraint in a database table.
/// </summary>
public class TableForeignKey()
{
    /// <summary>
    /// The name of the foreign key constraint.
    /// </summary>
    public string? ConstraintName { get; set; }
    /// <summary>
    /// The name of the column containing the foreign key.
    /// </summary>
    public string? ForeignColumnName { get; set; }
    /// <summary>
    /// The schema of the primary table referenced by the foreign key.
    /// </summary>
    public string? PrimaryTableSchema { get; set; }
    /// <summary>
    /// The name of the primary table referenced by the foreign key.
    /// </summary>
    public string? PrimaryTableName { get; set; }
    /// <summary>
    /// The name of the primary column referenced by the foreign key.
    /// </summary>
    public string? PrimaryColumnName { get; set; }
}
    
