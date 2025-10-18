

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents the name of a database table.
/// </summary>
/// <param name="TABLE_SCHEMA"></param>
/// <param name="TABLE_NAME"></param>
public class TableName()
{
    /// <summary>
    /// The schema of the table.
    /// </summary>
    public string? TableSchema { get; set; }
    /// <summary>
    /// The name of the table.
    /// </summary>
    public string? Name { get; set; }
}
