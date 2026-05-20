namespace DeepSigma.DataAccess.Abstraction.Models;

/// <summary>
/// Represents a field (column) in a database table.
/// </summary>
public sealed record TableField
{
    /// <summary>
    /// The schema of the table containing the field.
    /// </summary>
    public string? TableSchema { get; init; }

    /// <summary>
    /// The name of the table containing the field.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// The column name of the field.
    /// </summary>
    public string? ColumnName { get; init; }

    /// <summary>
    /// The data type of the field.
    /// </summary>
    public string? DataType { get; init; }

    /// <summary>
    /// The maximum length of the field, if applicable.
    /// </summary>
    public int? CharacterMaximumLength { get; init; }

    /// <summary>
    /// The numeric precision of the field, if applicable.
    /// </summary>
    public byte? NumericPrecision { get; init; }

    /// <summary>
    /// Indicates whether the field allows null values. Catalog views typically return
    /// the strings <c>"YES"</c> or <c>"NO"</c>.
    /// </summary>
    public string? IsNullable { get; init; }

    /// <summary>
    /// The default-value expression for the field, if any (e.g. <c>(getdate())</c>, <c>((0))</c>).
    /// Null when the column has no default.
    /// </summary>
    public string? ColumnDefault { get; init; }

    /// <summary>
    /// Convenience: <c>true</c> when <see cref="ColumnDefault"/> is a non-empty expression.
    /// </summary>
    public bool HasDefault => !string.IsNullOrEmpty(ColumnDefault);
}
