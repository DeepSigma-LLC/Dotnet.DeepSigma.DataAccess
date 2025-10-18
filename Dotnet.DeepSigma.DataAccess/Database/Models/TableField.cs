

using StackExchange.Redis;

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a field (column) in a database table.
/// </summary>
public class TableField()
{
    public string? TABLE_SCHEMA { get; set; }
    public string? COLUMN_NAME { get; set; }
    public string? DATA_TYPE { get; set; }
    public int? CHARACTER_MAXIMUM_LENGTH { get; set; }
    public byte? NUMERIC_PRECISION { get; set; }
    public string? IS_NULLABLE { get; set; }
    public int? COLUMN_DEFAULT { get; set; }
}