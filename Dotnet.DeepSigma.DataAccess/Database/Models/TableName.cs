

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents the name of a database table.
/// </summary>
/// <param name="TABLE_SCHEMA"></param>
/// <param name="TABLE_NAME"></param>
public record class TableName(string TABLE_SCHEMA, string TABLE_NAME);
