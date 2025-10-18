

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a field (column) in a database table.
/// </summary>
/// <param name="TABLE_SCHEMA"></param>
/// <param name="TABLE_NAME"></param>
/// <param name="COLUMN_NAME"></param>
/// <param name="DATA_TYPE"></param>
/// <param name="CHARACTER_MAXIMUM_LENGTH"></param>
/// <param name="IS_NULLABLE"></param>
/// <param name="NUMERIC_PRECISION"></param>
public record class TableField(
    string TABLE_SCHEMA,
    string TABLE_NAME,
    string COLUMN_NAME,
    string DATA_TYPE,
    string? CHARACTER_MAXIMUM_LENGTH,
    int? NUMERIC_PRECISION, 
    string IS_NULLABLE);
