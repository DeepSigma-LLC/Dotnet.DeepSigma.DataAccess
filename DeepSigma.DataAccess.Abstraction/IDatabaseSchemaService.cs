using DeepSigma.DataAccess.Abstraction.Models;

namespace DeepSigma.DataAccess.Abstraction;

/// <summary>
/// Retrieves schema metadata (tables, fields, constraints, foreign keys)
/// from a relational database. Each provider package supplies its own
/// implementation backed by provider-specific catalog queries.
/// </summary>
public interface IDatabaseSchemaService
{
    /// <summary>
    /// Gets the list of tables in the database.
    /// </summary>
    Task<IEnumerable<TableName>> GetTables(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of fields (columns) in the database tables.
    /// </summary>
    Task<IEnumerable<TableField>> GetTableFields(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of non-foreign-key constraints in the database tables.
    /// </summary>
    Task<IEnumerable<TableConstraint>> GetConstraints(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of foreign keys in the database tables.
    /// </summary>
    Task<IEnumerable<TableForeignKey>> GetForeignKeys(CancellationToken cancellationToken = default);
}
