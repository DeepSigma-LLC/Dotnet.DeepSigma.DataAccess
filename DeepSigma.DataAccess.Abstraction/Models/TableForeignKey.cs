namespace DeepSigma.DataAccess.Abstraction.Models;

/// <summary>
/// Represents a foreign key constraint in a database table.
/// </summary>
public sealed record TableForeignKey
{
    /// <summary>
    /// The name of the foreign key constraint.
    /// </summary>
    public string? ConstraintName { get; init; }

    /// <summary>
    /// The name of the column containing the foreign key.
    /// </summary>
    public string? ForeignColumnName { get; init; }

    /// <summary>
    /// The schema of the primary table referenced by the foreign key.
    /// </summary>
    public string? PrimaryTableSchema { get; init; }

    /// <summary>
    /// The name of the primary table referenced by the foreign key.
    /// </summary>
    public string? PrimaryTableName { get; init; }

    /// <summary>
    /// The name of the primary column referenced by the foreign key.
    /// </summary>
    public string? PrimaryColumnName { get; init; }
}
