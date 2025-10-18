

using StackExchange.Redis;

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a field (column) in a database table.
/// </summary>
public class TableField()
{
    /// <summary>
    /// The schema of the table containing the field.
    /// </summary>
    public string? TableSchema { get; set; }
    /// <summary>
    /// The column name of the field.
    /// </summary>
    public string? ColumnName { get; set; }
    /// <summary>
    /// The data type of the field.
    /// </summary>
    public string? DataType { get; set; }
    /// <summary>
    /// The maximum length of the field, if applicable.
    /// </summary>
    public int? CharacterMaximumLength { get; set; }
    /// <summary>
    /// The numeric precision of the field, if applicable.
    /// </summary>
    public byte? NumericPrecision { get; set; }
    /// <summary>
    /// Indicates whether the field allows null values.
    /// </summary>
    public string? IsNullable { get; set; }
    /// <summary>
    /// The default value of the field, if any.
    /// </summary>
    public int ColumnDefault { get; set; } = 0;
}