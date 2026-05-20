namespace DeepSigma.DataAccess.Abstraction.Models;

/// <summary>
/// Represents the name of a database table.
/// </summary>
public sealed record TableName
{
    /// <summary>
    /// The schema of the table.
    /// </summary>
    public string? TableSchema { get; init; }

    /// <summary>
    /// The name of the table.
    /// </summary>
    public string? Name { get; init; }
}
