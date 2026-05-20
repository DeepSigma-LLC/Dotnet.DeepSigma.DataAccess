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
    Task<IEnumerable<TableName>> GetTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of fields (columns) in the database tables.
    /// </summary>
    Task<IEnumerable<TableField>> GetTableFieldsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of non-foreign-key constraints in the database tables.
    /// </summary>
    Task<IEnumerable<TableConstraint>> GetConstraintsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of foreign keys in the database tables.
    /// </summary>
    Task<IEnumerable<TableForeignKey>> GetForeignKeysAsync(CancellationToken cancellationToken = default);
}
