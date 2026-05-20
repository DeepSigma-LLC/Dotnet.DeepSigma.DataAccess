namespace DeepSigma.DataAccess.Abstraction.Models;

/// <summary>
/// Represents the name of a database table.
/// </summary>
public class TableName
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
