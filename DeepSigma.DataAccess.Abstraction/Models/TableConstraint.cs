namespace DeepSigma.DataAccess.Abstraction.Models;

/// <summary>
/// Represents a constraint on a database table (primary key, unique, check).
/// Foreign keys are surfaced separately via <see cref="TableForeignKey"/>.
/// </summary>
public sealed record TableConstraint
{
    /// <summary>
    /// The name of the constraint.
    /// </summary>
    public string? ConstraintName { get; init; }

    /// <summary>
    /// The constraint type as reported by the catalog (e.g. <c>"PRIMARY KEY"</c>, <c>"UNIQUE"</c>, <c>"CHECK"</c>).
    /// </summary>
    public string? ConstraintType { get; init; }

    /// <summary>
    /// The schema of the table containing the constraint.
    /// </summary>
    public string? TableSchema { get; init; }

    /// <summary>
    /// The name of the table containing the constraint.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// The name of the column associated with the constraint.
    /// </summary>
    public string? ColumnName { get; init; }
}
