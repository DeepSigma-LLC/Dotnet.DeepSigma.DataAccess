

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents the name of a database table.
/// </summary>
/// <param name="TABLE_SCHEMA"></param>
/// <param name="TABLE_NAME"></param>
public class TableName()
{
    public string? TABLE_SCHEMA { get; set; }
    public string? TABLE_NAME { get; set; }
}
